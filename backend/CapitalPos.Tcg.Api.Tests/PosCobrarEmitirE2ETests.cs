using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class PosCobrarEmitirE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public PosCobrarEmitirE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rcp_lista_pendientes_cobra_y_emite_cpe()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");
        var sedeId = DevelopmentDataSeeder.RcpSedeId;
        await AsegurarCajaAsync(client, sedeId);

        var productoId = await CrearProductoConStockAsync(client, sedeId, "Sobre Mundial POS", 12m);
        var cliente = $"Paola FB {Guid.NewGuid():N}"[..22];
        var pedidoId = await CrearPedidoPendienteAsync(client, sedeId, cliente, productoId, 12m);

        var pendientes = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/pos/pedidos-pendientes?q={Uri.EscapeDataString(cliente)}&sedeId={sedeId}",
            Json);
        Assert.NotNull(pendientes);
        Assert.Contains(pendientes, p => GuidDe(p, "id") == pedidoId);
        Assert.All(pendientes, p => Assert.Equal("PendientePago", StringDe(p, "estado")));

        var cobrar = await client.PostAsJsonAsync(
            "/api/pos/cobrar-y-emitir",
            new
            {
                sedeId,
                pedidoDigitalIds = new[] { pedidoId },
                origenPago = "EFECTIVO",
                montoRecibido = 12m,
                tipoComprobante = "BOLETA",
                clienteNombre = cliente,
                observacion = "E2E POS Sprint 4"
            },
            Json);
        Assert.True(cobrar.IsSuccessStatusCode, await cobrar.Content.ReadAsStringAsync());
        var body = await cobrar.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.NotEqual(Guid.Empty, GuidDe(body, "ventaId"));
        Assert.True(body.TryGetProperty("comprobante", out var cpe));
        Assert.False(string.IsNullOrWhiteSpace(StringDe(cpe, "serie")));
        Assert.True(cpe.GetProperty("correlativo").GetInt32() > 0);
        var estado = StringDe(cpe, "estado");
        Assert.True(
            estado is "SIMULADO" or "ACEPTADO" or "PENDIENTE_CONSOLIDAR",
            $"Estado CPE inesperado: {estado}");

        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        Assert.Equal("Pagado", StringDe(pedido, "estado"));
        Assert.True(pedido.TryGetProperty("comprobante", out var resumen) && resumen.ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Rcp_cobro_mostrador_desde_items()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");
        var sedeId = DevelopmentDataSeeder.RcpSedeId;
        await AsegurarCajaAsync(client, sedeId);

        var productoId = await CrearProductoConStockAsync(client, sedeId, "Álbum POS Mostrador", 25m);

        var cobrar = await client.PostAsJsonAsync(
            "/api/pos/cobrar-y-emitir",
            new
            {
                sedeId,
                items = new[]
                {
                    new { productoId, cantidad = 1, precioUnitario = 25m }
                },
                clienteNombre = "Cliente mostrador",
                origenPago = "YAPE",
                codigoOperacion = $"YAPE-POS-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
                tipoComprobante = "NOTA_VENTA"
            },
            Json);
        Assert.True(cobrar.IsSuccessStatusCode, await cobrar.Content.ReadAsStringAsync());
        var body = await cobrar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(25m, body.GetProperty("totalCobrado").GetDecimal());
        Assert.Equal("NOTA_VENTA", StringDe(body.GetProperty("comprobante"), "tipo"));
        Assert.NotEmpty(body.GetProperty("pedidos").EnumerateArray());
    }

    [Fact]
    public async Task Trunqi_bloquea_pos_cobrar_y_emitir()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.EmpresaId, "admin@trunqi.local");

        var pendientes = await client.GetAsync("/api/pos/pedidos-pendientes");
        Assert.Equal(HttpStatusCode.Forbidden, pendientes.StatusCode);

        var cobrar = await client.PostAsJsonAsync(
            "/api/pos/cobrar-y-emitir",
            new
            {
                sedeId = DevelopmentDataSeeder.SedeId,
                items = Array.Empty<object>(),
                origenPago = "EFECTIVO",
                tipoComprobante = "BOLETA"
            },
            Json);
        Assert.Equal(HttpStatusCode.Forbidden, cobrar.StatusCode);
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
            new { sedeId, montoApertura = 100m, observacion = "Apertura E2E POS" },
            Json);
        Assert.True(abrir.IsSuccessStatusCode, await abrir.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CrearProductoConStockAsync(
        HttpClient client,
        Guid sedeId,
        string nombre,
        decimal precio)
    {
        var crear = await client.PostAsJsonAsync(
            "/api/productos-tcg",
            new
            {
                tipoProducto = "ACCESORIO",
                nombre,
                esColeccionableSinCodigo = true,
                codigoCategoriaSku = "SOB",
                tipoAccesorio = "SOBRE",
                precioVenta = precio,
                stockInicial = 5m,
                sedeId,
                tipoIngresoStock = "AJUSTE"
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var producto = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        return GuidDe(producto, "id");
    }

    private async Task<Guid> CrearPedidoPendienteAsync(
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
        return GuidDe(pedido, "id");
    }

    private async Task LoginAsync(HttpClient client, Guid empresaId, string email)
    {
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Admin123!", empresaId },
            Json);
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = cuerpo.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", empresaId.ToString());
    }

    private static Guid GuidDe(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return Guid.Empty;
        }

        return value.ValueKind == JsonValueKind.String
            ? Guid.Parse(value.GetString()!)
            : value.GetGuid();
    }

    private static string StringDe(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
}
