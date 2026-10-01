using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class CatalogoTcgTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public CatalogoTcgTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Importar_set_no_mueve_kardex_y_variante_ingresa_stock_con_ajuste()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var codigoSet = $"T{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var importar = await client.PostAsJsonAsync(
            "/api/tcg/cartas/importar-set",
            new
            {
                serie = new { juego = "Pokemon", codigo = "MEGA", nombre = "Megaevolución", activa = true },
                set = new
                {
                    codigo = codigoSet,
                    nombre = "Equilibrio Perfecto",
                    nombreEn = "Perfect Order",
                    codigoImpresion = "P11218",
                    totalCartas = 124
                },
                cartas = new[]
                {
                    new
                    {
                        numero = "047",
                        nombre = "Mega Zygarde ex",
                        tipoCarta = "POKEMON",
                        rareza = "ULTRA",
                        artista = (string?)null,
                        imagenOficialUrl = (string?)null
                    },
                    new
                    {
                        numero = "104",
                        nombre = "Mega Zygarde ex",
                        tipoCarta = "POKEMON",
                        rareza = "ILUSTRACION_RARA",
                        artista = (string?)null,
                        imagenOficialUrl = (string?)null
                    }
                }
            },
            Json);
        Assert.True(importar.IsSuccessStatusCode, await importar.Content.ReadAsStringAsync());
        var catalogo = await importar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(2, catalogo.GetProperty("cartasCreadas").GetInt32());
        var ficha047 = catalogo.GetProperty("cartas").EnumerateArray()
            .First(c => c.GetProperty("numero").GetString() == "047");
        var cartaCatalogoId = ficha047.GetProperty("id").GetGuid();

        var series = await client.GetFromJsonAsync<JsonElement>("/api/tcg/series", Json);
        Assert.True(series.GetArrayLength() >= 1);

        var serieId = catalogo.GetProperty("serie").GetProperty("id").GetGuid();
        var sets = await client.GetFromJsonAsync<JsonElement>($"/api/tcg/sets?serieId={serieId}", Json);
        Assert.Contains(sets.EnumerateArray(), s => s.GetProperty("codigo").GetString() == codigoSet);

        var setId = catalogo.GetProperty("set").GetProperty("id").GetGuid();
        var cartas = await client.GetFromJsonAsync<JsonElement>(
            $"/api/tcg/cartas?setId={setId}&numero=047&rareza=ULTRA",
            Json);
        Assert.Equal(1, cartas.GetArrayLength());

        var variante = await client.PostAsJsonAsync(
            "/api/productos-tcg/variantes",
            new
            {
                cartaCatalogoId,
                esFoil = true,
                condicion = "NM",
                idioma = "EN",
                precioVenta = 85m,
                costo = 40m,
                sedeId = DevelopmentDataSeeder.SedeId,
                stockInicial = 2m,
                tipoIngresoStock = "AJUSTE"
            },
            Json);
        Assert.True(variante.IsSuccessStatusCode, await variante.Content.ReadAsStringAsync());
        var sku = await variante.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(cartaCatalogoId, sku.GetProperty("carta").GetProperty("cartaCatalogoId").GetGuid());
        Assert.True(sku.GetProperty("carta").GetProperty("esFoil").GetBoolean());
        var productoId = sku.GetProperty("id").GetGuid();

        var stock = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/stock/{productoId}?sedeId={DevelopmentDataSeeder.SedeId}",
            Json);
        Assert.Equal(2m, stock.GetProperty("cantidadLibre").GetDecimal());

        var kardex = await client.GetFromJsonAsync<JsonElement>(
            $"/api/inventario/kardex?productoId={productoId}&sedeId={DevelopmentDataSeeder.SedeId}&tipoMovimiento=AJUSTE",
            Json);
        Assert.True(kardex.GetArrayLength() >= 1);
        Assert.Equal("ALTA_SKU", kardex[0].GetProperty("referenciaTipo").GetString());
    }

    [Fact]
    public async Task Actualizar_set_permite_nombres_y_bloquea_codigos_con_fichas()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var codigoSet = $"E{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var importar = await client.PostAsJsonAsync(
            "/api/tcg/cartas/importar-set",
            new
            {
                serie = new { juego = "Pokemon", codigo = "MEGA", nombre = "Megaevolución", activa = true },
                set = new
                {
                    codigo = codigoSet,
                    nombre = "Set Editable",
                    nombreEn = "Editable Set",
                    totalCartas = 1
                },
                cartas = new[]
                {
                    new
                    {
                        numero = "001",
                        nombre = "Carta Demo",
                        tipoCarta = "POKEMON",
                        rareza = "COMUN",
                        artista = (string?)null,
                        imagenOficialUrl = (string?)null
                    }
                }
            },
            Json);
        Assert.True(importar.IsSuccessStatusCode, await importar.Content.ReadAsStringAsync());
        var catalogo = await importar.Content.ReadFromJsonAsync<JsonElement>(Json);
        var setId = catalogo.GetProperty("set").GetProperty("id").GetGuid();

        var ok = await client.PutAsJsonAsync(
            $"/api/tcg/sets/{setId}",
            new
            {
                nombreSerie = "Megaevolución Renombrada",
                nombreSet = "Set Renombrado",
                nombreEn = "Renamed Set",
                codigoSerie = "MEGA",
                codigoSet = codigoSet
            },
            Json);
        Assert.True(ok.IsSuccessStatusCode, await ok.Content.ReadAsStringAsync());
        var actualizado = await ok.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Megaevolución Renombrada", actualizado.GetProperty("serieNombre").GetString());
        Assert.Equal("Set Renombrado", actualizado.GetProperty("nombre").GetString());
        Assert.Equal("Renamed Set", actualizado.GetProperty("nombreEn").GetString());
        Assert.True(actualizado.GetProperty("codigosBloqueados").GetBoolean());

        var bloqueado = await client.PutAsJsonAsync(
            $"/api/tcg/sets/{setId}",
            new
            {
                nombreSerie = "Megaevolución Renombrada",
                nombreSet = "Set Renombrado",
                nombreEn = "Renamed Set",
                codigoSerie = "OTRO",
                codigoSet = codigoSet
            },
            Json);
        Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);
    }

    [Fact]
    public async Task Eliminar_set_sin_skus_borra_fichas_y_con_skus_falla()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var codigoLibre = $"L{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var libre = await client.PostAsJsonAsync(
            "/api/tcg/cartas/importar-set",
            new
            {
                serie = new { juego = "Pokemon", codigo = "MEGA", nombre = "Megaevolución", activa = true },
                set = new { codigo = codigoLibre, nombre = "Set Libre", totalCartas = 1 },
                cartas = new[]
                {
                    new
                    {
                        numero = "010",
                        nombre = "Ficha Libre",
                        tipoCarta = "POKEMON",
                        rareza = "COMUN",
                        artista = (string?)null,
                        imagenOficialUrl = (string?)null
                    }
                }
            },
            Json);
        Assert.True(libre.IsSuccessStatusCode, await libre.Content.ReadAsStringAsync());
        var libreBody = await libre.Content.ReadFromJsonAsync<JsonElement>(Json);
        var setLibreId = libreBody.GetProperty("set").GetProperty("id").GetGuid();

        var deleteOk = await client.DeleteAsync($"/api/tcg/sets/{setLibreId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteOk.StatusCode);

        var cartas = await client.GetAsync($"/api/tcg/cartas?setId={setLibreId}");
        Assert.Equal(HttpStatusCode.NotFound, cartas.StatusCode);

        var codigoConSku = $"S{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        var conSku = await client.PostAsJsonAsync(
            "/api/tcg/cartas/importar-set",
            new
            {
                serie = new { juego = "Pokemon", codigo = "MEGA", nombre = "Megaevolución", activa = true },
                set = new { codigo = codigoConSku, nombre = "Set Con SKU", totalCartas = 1 },
                cartas = new[]
                {
                    new
                    {
                        numero = "020",
                        nombre = "Ficha Con SKU",
                        tipoCarta = "POKEMON",
                        rareza = "COMUN",
                        artista = (string?)null,
                        imagenOficialUrl = (string?)null
                    }
                }
            },
            Json);
        Assert.True(conSku.IsSuccessStatusCode, await conSku.Content.ReadAsStringAsync());
        var conSkuBody = await conSku.Content.ReadFromJsonAsync<JsonElement>(Json);
        var setConSkuId = conSkuBody.GetProperty("set").GetProperty("id").GetGuid();
        var cartaId = conSkuBody.GetProperty("cartas")[0].GetProperty("id").GetGuid();

        var variante = await client.PostAsJsonAsync(
            "/api/productos-tcg/variantes",
            new
            {
                cartaCatalogoId = cartaId,
                esFoil = false,
                condicion = "NM",
                idioma = "ES",
                precioVenta = 10m,
                sedeId = DevelopmentDataSeeder.SedeId,
                stockInicial = 1m,
                tipoIngresoStock = "AJUSTE"
            },
            Json);
        Assert.True(variante.IsSuccessStatusCode, await variante.Content.ReadAsStringAsync());

        var deleteFail = await client.DeleteAsync($"/api/tcg/sets/{setConSkuId}");
        Assert.Equal(HttpStatusCode.Conflict, deleteFail.StatusCode);
    }

    [Fact]
    public async Task Importar_set_exige_serieId_en_listado_de_sets()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);

        var sets = await client.GetAsync("/api/tcg/sets");
        Assert.Equal(HttpStatusCode.BadRequest, sets.StatusCode);
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
