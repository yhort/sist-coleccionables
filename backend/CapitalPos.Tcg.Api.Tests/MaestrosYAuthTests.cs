using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class MaestrosYAuthTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public MaestrosYAuthTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_lista_empresas_por_nombre_y_no_exige_guid()
    {
        using var client = _factory.CreateClient();
        var empresas = await client.GetFromJsonAsync<JsonElement>("/api/auth/empresas", Json);
        Assert.True(empresas.GetProperty("unicoTenant").GetBoolean());
        var lista = empresas.GetProperty("empresas");
        Assert.True(lista.GetArrayLength() >= 1);
        Assert.Equal("TRUNQI", lista[0].GetProperty("nombreComercial").GetString());
        Assert.False(string.IsNullOrWhiteSpace(lista[0].GetProperty("razonSocial").GetString()));

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email = "admin@trunqi.local", password = "Admin123!" },
            Json);
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cliente_valida_dni_ruc_y_publico_general()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var dniCorto = await client.PostAsJsonAsync(
            "/api/clientes",
            new { nombre = "Ana", tipoDocumento = "DNI", numeroDocumento = "123", esPublicoGeneral = false },
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, dniCorto.StatusCode);

        var rucCorto = await client.PostAsJsonAsync(
            "/api/clientes",
            new { nombre = "Empresa SAC", tipoDocumento = "RUC", numeroDocumento = "20123456", esPublicoGeneral = false },
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, rucCorto.StatusCode);

        var dni = await client.PostAsJsonAsync(
            "/api/clientes",
            new { nombre = "Ana Pérez", tipoDocumento = "DNI", numeroDocumento = "12345678", esPublicoGeneral = false },
            Json);
        Assert.True(dni.IsSuccessStatusCode, await dni.Content.ReadAsStringAsync());

        var varios = await client.PostAsJsonAsync(
            "/api/clientes",
            new { nombre = "CLIENTES VARIOS", tipoDocumento = "SIN_DOCUMENTO", esPublicoGeneral = true },
            Json);
        Assert.True(varios.IsSuccessStatusCode, await varios.Content.ReadAsStringAsync());
        var cuerpo = await varios.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(cuerpo.GetProperty("esPublicoGeneral").GetBoolean());
        Assert.True(
            cuerpo.GetProperty("numeroDocumento").ValueKind == JsonValueKind.Null
                || string.IsNullOrEmpty(cuerpo.GetProperty("numeroDocumento").GetString()));

        var soloNombre = await client.PostAsJsonAsync(
            "/api/clientes",
            new { nombre = "María López", tipoDocumento = "DNI", numeroDocumento = "", esPublicoGeneral = false },
            Json);
        Assert.True(soloNombre.IsSuccessStatusCode, await soloNombre.Content.ReadAsStringAsync());
        var maria = await soloNombre.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("SIN_DOCUMENTO", maria.GetProperty("tipoDocumento").GetString());
        Assert.False(maria.GetProperty("esPublicoGeneral").GetBoolean());
        var numeroMaria = maria.GetProperty("numeroDocumento");
        Assert.True(
            numeroMaria.ValueKind is JsonValueKind.Null or JsonValueKind.String
                && (numeroMaria.ValueKind == JsonValueKind.Null
                    || string.IsNullOrEmpty(numeroMaria.GetString())));
    }

    [Fact]
    public async Task Proveedor_compra_ingresa_stock_al_kardex()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var rucCorto = await client.PostAsJsonAsync(
            "/api/proveedores",
            new { ruc = "20123", razonSocial = "Demo SAC", activo = true },
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, rucCorto.StatusCode);

        var proveedorRes = await client.PostAsJsonAsync(
            "/api/proveedores",
            new
            {
                ruc = "20555666777",
                razonSocial = "Distribuidora TCG SAC",
                nombreComercial = "Distro TCG",
                activo = true
            },
            Json);
        Assert.True(proveedorRes.IsSuccessStatusCode, await proveedorRes.Content.ReadAsStringAsync());
        var proveedor = await proveedorRes.Content.ReadFromJsonAsync<JsonElement>(Json);
        var proveedorId = proveedor.GetProperty("id").GetGuid();

        var sku = $"CMP-{Guid.NewGuid():N}";
        var productoRes = await client.PostAsJsonAsync(
            "/api/productos-tcg",
            new
            {
                tipoProducto = "CARTA",
                nombre = "Carta E2E compra",
                codigoSku = sku,
                precioVenta = 10m,
                costo = 4m,
                carta = new
                {
                    juego = "Pokemon",
                    setCodigo = "CMP",
                    setNombre = "Compras",
                    numeroCarta = "001",
                    rareza = "COMUN",
                    idioma = "ES",
                    condicion = "NM",
                    esFoil = false
                }
            },
            Json);
        Assert.True(productoRes.IsSuccessStatusCode, await productoRes.Content.ReadAsStringAsync());
        var producto = await productoRes.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productoId = producto.GetProperty("id").GetGuid();

        var stockAntes = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/stock/{productoId}?sedeId={DevelopmentDataSeeder.SedeId}",
            Json);
        var libreAntes = stockAntes.GetProperty("cantidadLibre").GetDecimal();

        var compra = await client.PostAsJsonAsync(
            "/api/inventario/compras",
            new
            {
                proveedorId,
                sedeId = DevelopmentDataSeeder.SedeId,
                observacion = "Ingreso prueba E2E",
                detalles = new[]
                {
                    new { productoId, cantidad = 5m, costoUnitario = 4m }
                }
            },
            Json);
        Assert.True(compra.IsSuccessStatusCode, await compra.Content.ReadAsStringAsync());

        var stockDespues = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/stock/{productoId}?sedeId={DevelopmentDataSeeder.SedeId}",
            Json);
        Assert.Equal(libreAntes + 5m, stockDespues.GetProperty("cantidadLibre").GetDecimal());

        var kardex = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/kardex?productoId={productoId}&sedeId={DevelopmentDataSeeder.SedeId}&tipoMovimiento=INGRESO_COMPRA",
            Json);
        Assert.True(kardex.ValueKind == JsonValueKind.Array && kardex.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Dashboard_resumen_retorna_kpis_del_tenant()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var respuesta = await client.GetAsync("/api/dashboard/resumen");
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync());

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(cuerpo.TryGetProperty("fechaLocal", out _));
        Assert.True(cuerpo.GetProperty("ventasHoy").GetProperty("totalRecaudado").GetDecimal() >= 0m);
        Assert.True(cuerpo.GetProperty("ventasHoy").GetProperty("comprobantesEmitidos").GetInt32() >= 0);
        Assert.True(cuerpo.GetProperty("subastas").GetProperty("activas").GetInt32() >= 0);
        Assert.True(cuerpo.GetProperty("subastas").GetProperty("ganadoresPendientePago").GetInt32() >= 0);
        Assert.True(cuerpo.GetProperty("pagosNotificados").GetProperty("pendientesAsociar").GetInt32() >= 0);
        Assert.True(cuerpo.GetProperty("stockBajo").GetProperty("cantidad").GetInt32() >= 0);
        Assert.Equal(3m, cuerpo.GetProperty("stockBajo").GetProperty("umbral").GetDecimal());
        Assert.Equal(JsonValueKind.Array, cuerpo.GetProperty("actividadReciente").ValueKind);
        Assert.True(cuerpo.GetProperty("actividadReciente").GetArrayLength() <= 5);
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
        var token = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", DevelopmentDataSeeder.EmpresaId.ToString());
    }
}
