using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[CollectionDefinition("SubastaE2E", DisableParallelization = true)]
public sealed class SubastaE2ECollection : ICollectionFixture<ApiFactory>;

[Collection("SubastaE2E")]
public sealed class SubastaFlujoE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public SubastaFlujoE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Puja_en_subasta_cerrada_devuelve_400_con_mensaje_no_500()
    {
        using var client = _factory.CreateClient();
        var ctx = await PrepararCartaConStockAsync(client);
        var subasta = await CrearYActivarAsync(client, ctx, TimeSpan.FromHours(2), "E2E cerrada");
        var cerrar = await client.PostAsync($"/api/subastas-tcg/{subasta.GetGuid("id")}/cerrar", null);
        cerrar.EnsureSuccessStatusCode();

        var puja = await client.PostAsJsonAsync(
            $"/api/subastas-tcg/{subasta.GetGuid("id")}/pujas",
            new { nombrePostor = "Ana", monto = 10m },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, puja.StatusCode);
        var cuerpo = await puja.Content.ReadFromJsonAsync<JsonElement>(Json);
        var mensaje = cuerpo.GetProperty("message").GetString() ?? string.Empty;
        Assert.Contains("activa", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Puja_vencida_devuelve_400_finalizada_no_500()
    {
        using var client = _factory.CreateClient();
        var ctx = await PrepararCartaConStockAsync(client);
        var subasta = await CrearYActivarAsync(client, ctx, TimeSpan.FromMinutes(-2), "E2E vencida");

        var puja = await client.PostAsJsonAsync(
            $"/api/subastas-tcg/{subasta.GetGuid("id")}/pujas",
            new { nombrePostor = "Ana", monto = 10m },
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, puja.StatusCode);
        var cuerpo = await puja.Content.ReadFromJsonAsync<JsonElement>(Json);
        var mensaje = cuerpo.GetProperty("message").GetString() ?? string.Empty;
        Assert.Contains("finalizado", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Flujo_completo_adjudica_reserva_kardex_y_readjudica_segundo_postor()
    {
        using var client = _factory.CreateClient();
        var ctx = await PrepararCartaConStockAsync(client);
        var stockAntes = await StockAsync(client, ctx.ProductoId);
        var subastaId = (await CrearYActivarAsync(client, ctx, TimeSpan.FromHours(3), "E2E flujo Kanban")).GetGuid("id");

        var puja1 = await client.PostAsJsonAsync(
            $"/api/subastas-tcg/{subastaId}/pujas",
            new { nombrePostor = "Luis", monto = 10m },
            Json);
        Assert.True(puja1.IsSuccessStatusCode, await puja1.Content.ReadAsStringAsync());

        var puja2 = await client.PostAsJsonAsync(
            $"/api/subastas-tcg/{subastaId}/pujas",
            new { nombrePostor = "Marta", monto = 15m },
            Json);
        Assert.True(puja2.IsSuccessStatusCode, await puja2.Content.ReadAsStringAsync());

        var adjudicar = await client.PostAsync($"/api/subastas-tcg/{subastaId}/adjudicar", null);
        Assert.True(adjudicar.IsSuccessStatusCode, await adjudicar.Content.ReadAsStringAsync());
        var adjudicada = await adjudicar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ADJUDICADA", adjudicada.GetString("estado"));
        var pedidoId = adjudicada.GetGuid("pedidoDigitalId");
        Assert.NotEqual(Guid.Empty, pedidoId);

        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        Assert.Equal("PendientePago", pedido.GetString("estado"));
        Assert.Equal("Reservado", pedido.GetString("indicadorReserva"));
        Assert.Equal(subastaId, pedido.GetGuid("subastaTcgId"));
        Assert.Equal("Marta", pedido.GetString("clienteNombre"));
        Assert.Equal(15m, pedido.GetProperty("total").GetDecimal());

        var stockReservado = await StockAsync(client, ctx.ProductoId);
        Assert.Equal(stockAntes.Libre - 1, stockReservado.Libre);
        Assert.Equal(stockAntes.Reservada + 1, stockReservado.Reservada);

        var kardex = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/kardex?productoId={ctx.ProductoId}&sedeId={ctx.SedeId}&tipoMovimiento=PUJA_GANADORA_RESERVA",
            Json);
        Assert.True(kardex.ValueKind == JsonValueKind.Array && kardex.GetArrayLength() >= 1);

        var cancelar = await client.PostAsJsonAsync(
            $"/api/pedidos-digitales/{pedidoId}/cancelar",
            new { observacion = "Ganador no pagó" },
            Json);
        Assert.True(cancelar.IsSuccessStatusCode, await cancelar.Content.ReadAsStringAsync());

        var stockLiberado = await StockAsync(client, ctx.ProductoId);
        Assert.Equal(stockAntes.Libre, stockLiberado.Libre);
        Assert.Equal(stockAntes.Reservada, stockLiberado.Reservada);

        var reabierta = await client.GetFromJsonAsync<JsonElement>($"/api/subastas-tcg/{subastaId}", Json);
        Assert.Equal("CERRADA", reabierta.GetString("estado"));
        Assert.True(reabierta.TryGetProperty("pedidoDigitalId", out var pedidoTrasCancelar));
        Assert.True(pedidoTrasCancelar.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

        var ganadora = reabierta.GetProperty("pujas").EnumerateArray()
            .First(p => p.GetProperty("esGanadora").GetBoolean());
        Assert.Equal("Luis", ganadora.GetString("nombrePostor"));
        Assert.Equal(10m, ganadora.GetProperty("monto").GetDecimal());

        var readjudicar = await client.PostAsync($"/api/subastas-tcg/{subastaId}/adjudicar", null);
        Assert.True(readjudicar.IsSuccessStatusCode, await readjudicar.Content.ReadAsStringAsync());
        var segunda = await readjudicar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ADJUDICADA", segunda.GetString("estado"));
        var pedido2 = await client.GetFromJsonAsync<JsonElement>(
            $"/api/pedidos-digitales/{segunda.GetGuid("pedidoDigitalId")}",
            Json);
        Assert.Equal("Luis", pedido2.GetString("clienteNombre"));
        Assert.Equal("PendientePago", pedido2.GetString("estado"));
    }

    [Fact]
    public async Task Combo_multi_sku_adjudica_reserva_todas_las_lineas()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;

        var productoA = await CrearCartaConStockAsync(client, sedeId, "Combo A", 5m);
        var productoB = await CrearCartaConStockAsync(client, sedeId, "Combo B", 4m);
        var stockAAntes = await StockAsync(client, productoA);
        var stockBAntes = await StockAsync(client, productoB);

        var ahora = DateTimeOffset.UtcNow;
        var crear = await client.PostAsJsonAsync(
            "/api/subastas-tcg",
            new
            {
                sedeId,
                titulo = "E2E combo multi-SKU",
                canal = "FACEBOOK_SUBASTA",
                precioBase = 20m,
                incrementoMinimo = 5m,
                fechaInicio = ahora.AddMinutes(-5),
                fechaCierre = ahora.AddHours(2),
                detalles = new[]
                {
                    new { productoId = productoA, cantidad = 2m },
                    new { productoId = productoB, cantidad = 1m }
                }
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var subasta = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(2, subasta.GetProperty("detalles").GetArrayLength());
        Assert.Equal(productoA, subasta.GetGuid("productoId"));

        var id = subasta.GetGuid("id");
        var activar = await client.PostAsync($"/api/subastas-tcg/{id}/activar", null);
        Assert.True(activar.IsSuccessStatusCode, await activar.Content.ReadAsStringAsync());

        var puja = await client.PostAsJsonAsync(
            $"/api/subastas-tcg/{id}/pujas",
            new { nombrePostor = "Diego", monto = 25m },
            Json);
        Assert.True(puja.IsSuccessStatusCode, await puja.Content.ReadAsStringAsync());

        var adjudicar = await client.PostAsync($"/api/subastas-tcg/{id}/adjudicar", null);
        Assert.True(adjudicar.IsSuccessStatusCode, await adjudicar.Content.ReadAsStringAsync());
        var adjudicada = await adjudicar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ADJUDICADA", adjudicada.GetString("estado"));

        var pedidoId = adjudicada.GetGuid("pedidoDigitalId");
        var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
        Assert.Equal("PendientePago", pedido.GetString("estado"));
        Assert.Equal(25m, pedido.GetProperty("total").GetDecimal());
        Assert.Equal(2, pedido.GetProperty("detalles").GetArrayLength());

        var stockA = await StockAsync(client, productoA);
        var stockB = await StockAsync(client, productoB);
        Assert.Equal(stockAAntes.Libre - 2, stockA.Libre);
        Assert.Equal(stockAAntes.Reservada + 2, stockA.Reservada);
        Assert.Equal(stockBAntes.Libre - 1, stockB.Libre);
        Assert.Equal(stockBAntes.Reservada + 1, stockB.Reservada);
    }

    private async Task<(Guid ProductoId, Guid SedeId)> PrepararCartaConStockAsync(HttpClient client)
    {
        await LoginAsync(client);
        var sedeId = DevelopmentDataSeeder.SedeId;
        var productoId = await CrearCartaConStockAsync(client, sedeId, "Carta E2E subasta", 3m);
        return (productoId, sedeId);
    }

    private async Task<Guid> CrearCartaConStockAsync(
        HttpClient client,
        Guid sedeId,
        string nombre,
        decimal cantidad)
    {
        var sku = $"E2E-{Guid.NewGuid():N}";
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
                    setCodigo = "E2E",
                    setNombre = "Pruebas",
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
        var productoId = producto.GetGuid("id");

        var ajuste = await client.PutAsJsonAsync(
            "/api/inventario/ajustar",
            new
            {
                productoId,
                sedeId,
                cantidad,
                sentido = "ENTRADA",
                motivo = "Stock inicial prueba E2E subasta"
            },
            Json);
        Assert.True(ajuste.IsSuccessStatusCode, await ajuste.Content.ReadAsStringAsync());
        return productoId;
    }

    private async Task<JsonElement> CrearYActivarAsync(
        HttpClient client,
        (Guid ProductoId, Guid SedeId) ctx,
        TimeSpan hastaCierre,
        string titulo)
    {
        var ahora = DateTimeOffset.UtcNow;
        var crear = await client.PostAsJsonAsync(
            "/api/subastas-tcg",
            new
            {
                sedeId = ctx.SedeId,
                productoId = ctx.ProductoId,
                titulo,
                canal = "FACEBOOK_SUBASTA",
                precioBase = 10m,
                incrementoMinimo = 5m,
                precioReserva = (decimal?)null,
                fechaInicio = ahora.AddMinutes(-5),
                fechaCierre = ahora.Add(hastaCierre),
                observacion = "Prueba automatizada"
            },
            Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var subasta = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);
        var id = subasta.GetGuid("id");
        var activar = await client.PostAsync($"/api/subastas-tcg/{id}/activar", null);
        Assert.True(activar.IsSuccessStatusCode, await activar.Content.ReadAsStringAsync());
        return await activar.Content.ReadFromJsonAsync<JsonElement>(Json);
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
        var token = body.GetString("accessToken");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", DevelopmentDataSeeder.EmpresaId.ToString());
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

file static class JsonElementExt
{
    public static Guid GetGuid(this JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return Guid.Empty;
        }

        if (valor.ValueKind == JsonValueKind.String && Guid.TryParse(valor.GetString(), out var parsed))
        {
            return parsed;
        }

        return valor.GetGuid();
    }

    public static string GetString(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var valor) ? valor.GetString() ?? string.Empty : string.Empty;
}
