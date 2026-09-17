using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class PagosLoteE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public PagosLoteE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lote_del_mismo_cliente_confirma_y_marca_pedidos_pagados()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var cartaA = await CrearCartaConStockAsync(client, sedeId, "Charizard Live");
        var cartaB = await CrearCartaConStockAsync(client, sedeId, "Pikachu Live");

        var pedidoA = await CrearPedidoAsync(client, sedeId, "Luis Live", cartaA, 40m);
        var pedidoB = await CrearPedidoAsync(client, sedeId, "Luis Live", cartaB, 25m);
        var codigo = $"YAPE-LIVE-{Guid.NewGuid():N}"[..18].ToUpperInvariant();

        var lote = await client.PostAsJsonAsync(
            "/api/pagos/lote",
            new
            {
                origen = "YAPE",
                monto = 65m,
                codigoOperacion = codigo,
                referenciaExterna = "Captura Yape live",
                confirmar = true,
                pedidoDigitalIds = new[] { pedidoA, pedidoB }
            },
            Json);

        Assert.True(lote.IsSuccessStatusCode, await lote.Content.ReadAsStringAsync());
        var pagos = await lote.Content.ReadFromJsonAsync<List<JsonElement>>(Json);
        Assert.NotNull(pagos);
        Assert.Equal(2, pagos.Count);
        Assert.All(pagos, pago => Assert.Equal("CONFIRMADO", StringDe(pago, "estado")));
        Assert.Equal(65m, pagos.Sum(p => p.GetProperty("monto").GetDecimal()));

        var actualA = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoA}", Json);
        var actualB = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoB}", Json);
        Assert.Equal("Pagado", StringDe(actualA, "estado"));
        Assert.Equal("Pagado", StringDe(actualB, "estado"));
        Assert.Equal("Reservado", StringDe(actualA, "indicadorReserva"));
    }

    [Fact]
    public async Task Lote_de_distintos_clientes_devuelve_400()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var cartaA = await CrearCartaConStockAsync(client, sedeId, "Carta cliente A");
        var cartaB = await CrearCartaConStockAsync(client, sedeId, "Carta cliente B");
        var pedidoA = await CrearPedidoAsync(client, sedeId, "Ana Live", cartaA, 10m);
        var pedidoB = await CrearPedidoAsync(client, sedeId, "Bruno Live", cartaB, 12m);

        var lote = await client.PostAsJsonAsync(
            "/api/pagos/lote",
            new
            {
                origen = "YAPE",
                monto = 22m,
                codigoOperacion = $"YAPE-MIX-{Guid.NewGuid():N}"[..18],
                confirmar = true,
                pedidoDigitalIds = new[] { pedidoA, pedidoB }
            },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, lote.StatusCode);
        var cuerpo = await lote.Content.ReadFromJsonAsync<JsonElement>(Json);
        var mensaje = DetalleError(cuerpo);
        Assert.Contains("mismo cliente", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancelar_pedido_pendiente_libera_reserva()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var carta = await CrearCartaConStockAsync(client, sedeId, "Carta anular live", 2m);
        var stockAntes = await StockAsync(client, carta);
        var pedidoId = await CrearPedidoAsync(client, sedeId, "Marta Live", carta, 18m);

        var stockReservado = await StockAsync(client, carta);
        Assert.Equal(stockAntes.Libre - 1, stockReservado.Libre);
        Assert.Equal(stockAntes.Reservada + 1, stockReservado.Reservada);

        var cancelar = await client.PostAsJsonAsync(
            $"/api/pedidos-digitales/{pedidoId}/cancelar",
            new { observacion = "Anulado desde Pedidos Digitales." },
            Json);
        Assert.True(cancelar.IsSuccessStatusCode, await cancelar.Content.ReadAsStringAsync());

        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        Assert.Equal("Cancelado", StringDe(pedido, "estado"));
        Assert.Equal("Liberado", StringDe(pedido, "indicadorReserva"));

        var stockLiberado = await StockAsync(client, carta);
        Assert.Equal(stockAntes.Libre, stockLiberado.Libre);
        Assert.Equal(stockAntes.Reservada, stockLiberado.Reservada);
    }

    private async Task<Guid> CrearPedidoAsync(
        HttpClient client,
        Guid sedeId,
        string clienteNombre,
        Guid productoId,
        decimal precio)
    {
        var crear = await client.PostAsJsonAsync(
            "/api/pedidos-digitales",
            new
            {
                clienteNombre,
                sedeId,
                canalPedido = "FACEBOOK_SUBASTA",
                detalles = new[]
                {
                    new { productoId, cantidad = 1, precioUnitario = precio }
                },
                entrega = new
                {
                    destinatarioNombre = clienteNombre,
                    esRecojoTienda = true
                }
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var pedido = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = GuidDe(pedido, "id");
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("PendientePago", StringDe(pedido, "estado"));
        return id;
    }

    private async Task<Guid> CrearCartaConStockAsync(
        HttpClient client,
        Guid sedeId,
        string nombre,
        decimal cantidad = 1m)
    {
        var sku = $"LOT-{Guid.NewGuid():N}";
        var crear = await client.PostAsJsonAsync(
            "/api/productos-tcg",
            new
            {
                tipoProducto = "CARTA",
                nombre,
                codigoSku = sku,
                precioVenta = 20m,
                costo = 8m,
                carta = new
                {
                    juego = "Pokemon",
                    setCodigo = "LOT",
                    setNombre = "Live lote",
                    numeroCarta = "001",
                    rareza = "COMUN",
                    idioma = "ES",
                    condicion = "NM",
                    esFoil = false
                }
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var producto = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productoId = GuidDe(producto, "id");

        var ajuste = await client.PutAsJsonAsync(
            "/api/inventario/ajustar",
            new
            {
                productoId,
                sedeId,
                cantidad,
                sentido = "ENTRADA",
                motivo = "Stock inicial prueba lote pagos"
            },
            Json);
        Assert.True(ajuste.IsSuccessStatusCode, await ajuste.Content.ReadAsStringAsync());
        return productoId;
    }

    private async Task LoginAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "admin@trunqi.local",
                password = "Admin123!",
                empresaId = DevelopmentDataSeeder.EmpresaId
            },
            Json);
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = StringDe(body, "accessToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", DevelopmentDataSeeder.EmpresaId.ToString());
    }

    private static Guid GuidDe(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var valor)
            || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Guid.Empty;
        }

        if (valor.ValueKind == JsonValueKind.String && Guid.TryParse(valor.GetString(), out var parsed))
        {
            return parsed;
        }

        return valor.GetGuid();
    }

    private static string StringDe(JsonElement element, string name) =>
        element.TryGetProperty(name, out var valor) ? valor.GetString() ?? string.Empty : string.Empty;

    private static string DetalleError(JsonElement cuerpo)
    {
        if (cuerpo.TryGetProperty("detail", out var detail) && detail.GetString() is { Length: > 0 } texto)
        {
            return texto;
        }

        return StringDe(cuerpo, "message");
    }

    private async Task<(decimal Libre, decimal Reservada)> StockAsync(HttpClient client, Guid productoId)
    {
        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/stock/{productoId}?sedeId={DevelopmentDataSeeder.SedeId}",
            Json);
        return (
            stock.GetProperty("cantidadLibre").GetDecimal(),
            stock.GetProperty("cantidadReservada").GetDecimal());
    }
}
