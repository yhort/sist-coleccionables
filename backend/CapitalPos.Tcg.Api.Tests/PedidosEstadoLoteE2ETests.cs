using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class PedidosEstadoLoteE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public PedidosEstadoLoteE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Lote_logistico_avanza_pagado_empaquetado_entrega_con_clientes_distintos()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var cartaA = await CrearCartaConStockAsync(client, sedeId, "Carta lote Ana");
        var cartaB = await CrearCartaConStockAsync(client, sedeId, "Carta lote Bruno");
        var pedidoA = await CrearPedidoAsync(client, sedeId, "Ana Lote", cartaA, 18m);
        var pedidoB = await CrearPedidoAsync(client, sedeId, "Bruno Lote", cartaB, 22m);

        await PagarAsync(client, 18m, pedidoA);
        await PagarAsync(client, 22m, pedidoB);

        await LoteEstadoAsync(client, "Empaquetado", pedidoA, pedidoB);
        Assert.Equal("Empaquetado", await EstadoDe(client, pedidoA));
        Assert.Equal("Empaquetado", await EstadoDe(client, pedidoB));

        await LoteEstadoAsync(client, "PendienteEntrega", pedidoA, pedidoB);
        Assert.Equal("PendienteEntrega", await EstadoDe(client, pedidoA));
        Assert.Equal("PendienteEntrega", await EstadoDe(client, pedidoB));

        await AsegurarCajaAsync(client, sedeId);
        await LoteEstadoAsync(client, "Entregado", pedidoA, pedidoB);
        Assert.Equal("Entregado", await EstadoDe(client, pedidoA));
        Assert.Equal("Entregado", await EstadoDe(client, pedidoB));

        var consolidadoMixto = await client.PostAsJsonAsync(
            "/api/pedidos-digitales/comprobante-consolidado",
            new { tipoComprobante = "BOLETA", pedidoDigitalIds = new[] { pedidoA, pedidoB } },
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, consolidadoMixto.StatusCode);
        var cuerpo = await consolidadoMixto.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("mismo cliente", DetalleError(cuerpo), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Comprobante_consolidado_une_ventas_del_mismo_cliente()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var cartaA = await CrearCartaConStockAsync(client, sedeId, "Carta consolidado A");
        var cartaB = await CrearCartaConStockAsync(client, sedeId, "Carta consolidado B");
        var pedidoA = await CrearPedidoAsync(client, sedeId, "Luis Consolidado", cartaA, 30m);
        var pedidoB = await CrearPedidoAsync(client, sedeId, "Luis Consolidado", cartaB, 15m);

        await PagarAsync(client, 45m, pedidoA, pedidoB);
        await LoteEstadoAsync(client, "Empaquetado", pedidoA, pedidoB);
        await LoteEstadoAsync(client, "PendienteEntrega", pedidoA, pedidoB);
        await AsegurarCajaAsync(client, sedeId);
        await LoteEstadoAsync(client, "Entregado", pedidoA, pedidoB);

        var ventaAntesA = GuidDe(await PedidoAsync(client, pedidoA), "ventaId");
        var ventaAntesB = GuidDe(await PedidoAsync(client, pedidoB), "ventaId");
        Assert.NotEqual(Guid.Empty, ventaAntesA);
        Assert.NotEqual(Guid.Empty, ventaAntesB);
        Assert.NotEqual(ventaAntesA, ventaAntesB);

        var emitir = await client.PostAsJsonAsync(
            "/api/pedidos-digitales/comprobante-consolidado",
            new { tipoComprobante = "BOLETA", pedidoDigitalIds = new[] { pedidoA, pedidoB } },
            Json);
        Assert.True(emitir.IsSuccessStatusCode, await emitir.Content.ReadAsStringAsync());
        var resultado = await emitir.Content.ReadFromJsonAsync<JsonElement>(Json);
        var ventaId = GuidDe(resultado, "ventaId");
        Assert.NotEqual(Guid.Empty, ventaId);
        Assert.Equal(45m, resultado.GetProperty("comprobante").GetProperty("total").GetDecimal());

        var actualA = await PedidoAsync(client, pedidoA);
        var actualB = await PedidoAsync(client, pedidoB);
        Assert.Equal(ventaId, GuidDe(actualA, "ventaId"));
        Assert.Equal(ventaId, GuidDe(actualB, "ventaId"));
    }

    [Fact]
    public async Task Lote_desde_pendiente_pago_a_empaquetado_devuelve_400()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var carta = await CrearCartaConStockAsync(client, sedeId, "Carta lote invalido");
        var pedido = await CrearPedidoAsync(client, sedeId, "Marta Lote", carta, 12m);

        var lote = await client.PostAsJsonAsync(
            "/api/pedidos-digitales/estado-lote",
            new { estado = "Empaquetado", pedidoDigitalIds = new[] { pedido } },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, lote.StatusCode);
        var cuerpo = await lote.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("Pagado", DetalleError(cuerpo), StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoteEstadoAsync(HttpClient client, string estado, params Guid[] ids)
    {
        var resp = await client.PostAsJsonAsync(
            "/api/pedidos-digitales/estado-lote",
            new { estado, pedidoDigitalIds = ids },
            Json);
        Assert.True(resp.IsSuccessStatusCode, await resp.Content.ReadAsStringAsync());
    }

    private async Task PagarAsync(HttpClient client, decimal monto, params Guid[] ids)
    {
        var lote = await client.PostAsJsonAsync(
            "/api/pagos/lote",
            new
            {
                origen = "YAPE",
                monto,
                codigoOperacion = $"YAPE-EST-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                confirmar = true,
                pedidoDigitalIds = ids
            },
            Json);
        Assert.True(lote.IsSuccessStatusCode, await lote.Content.ReadAsStringAsync());
    }

    private async Task AsegurarCajaAsync(HttpClient client, Guid sedeId)
    {
        var estado = await client.GetFromJsonAsync<JsonElement>(
            $"/api/caja/sesion-actual?sedeId={sedeId}",
            Json);
        if (estado.GetProperty("abierta").GetBoolean())
        {
            return;
        }

        var abrir = await client.PostAsJsonAsync(
            "/api/caja/apertura",
            new { sedeId, montoApertura = 50m, observacion = "Apertura prueba lote estados" },
            Json);
        Assert.True(abrir.IsSuccessStatusCode, await abrir.Content.ReadAsStringAsync());
    }

    private async Task<string> EstadoDe(HttpClient client, Guid pedidoId) =>
        StringDe(await PedidoAsync(client, pedidoId), "estado");

    private async Task<JsonElement> PedidoAsync(HttpClient client, Guid pedidoId)
    {
        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        Assert.True(pedido.ValueKind == JsonValueKind.Object);
        return pedido;
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
        return id;
    }

    private async Task<Guid> CrearCartaConStockAsync(
        HttpClient client,
        Guid sedeId,
        string nombre,
        decimal cantidad = 1m)
    {
        var sku = $"EST-{Guid.NewGuid():N}";
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
                    setCodigo = "EST",
                    setNombre = "Live estado lote",
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
                motivo = "Stock inicial prueba lote estados"
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
}
