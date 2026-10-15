using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class SkuInternoProductoE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly Regex SkuRcp = new(
        @"^RCP-ALB-\d{5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ApiFactory _factory;

    public SkuInternoProductoE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rcp_crea_producto_sin_ean_con_sku_unico()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");

        var preview = await client.GetAsync(
            "/api/productos-tcg/sku-interno/preview?categoria=ALB&tipoProducto=ACCESORIO&tipoAccesorio=BINDER");
        Assert.True(preview.IsSuccessStatusCode, await preview.Content.ReadAsStringAsync());
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>(Json);
        var skuPreview = previewBody.GetProperty("skuInterno").GetString();
        Assert.False(string.IsNullOrWhiteSpace(skuPreview));
        Assert.Matches(SkuRcp, skuPreview!);

        var body = new
        {
            tipoProducto = "ACCESORIO",
            nombre = "Álbum Panini Mundial 2026",
            esColeccionableSinCodigo = true,
            codigoCategoriaSku = "ALB",
            tipoAccesorio = "BINDER",
            ubicacionTienda = "Estante A-3",
            precioVenta = 25m,
            stockInicial = 3m,
            sedeId = DevelopmentDataSeeder.RcpSedeId,
            tipoIngresoStock = "AJUSTE"
        };

        var crear = await client.PostAsJsonAsync("/api/productos-tcg", body, Json);
        Assert.True(crear.IsSuccessStatusCode, await crear.Content.ReadAsStringAsync());
        var producto = await crear.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.True(producto.GetProperty("esColeccionableSinCodigo").GetBoolean());
        var sku = producto.GetProperty("codigoSku").GetString();
        var skuInterno = producto.GetProperty("skuInterno").GetString();
        Assert.Equal(sku, skuInterno);
        Assert.Matches(SkuRcp, sku!);
        Assert.True(
            producto.GetProperty("codigoBarras").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || string.IsNullOrWhiteSpace(producto.GetProperty("codigoBarras").GetString()));
        Assert.Equal("Estante A-3", producto.GetProperty("ubicacionTienda").GetString());

        var crear2 = await client.PostAsJsonAsync("/api/productos-tcg", new
        {
            tipoProducto = "ACCESORIO",
            nombre = "Álbum Panini Segundo",
            esColeccionableSinCodigo = true,
            codigoCategoriaSku = "ALB",
            precioVenta = 20m
        }, Json);
        Assert.True(crear2.IsSuccessStatusCode, await crear2.Content.ReadAsStringAsync());
        var producto2 = await crear2.Content.ReadFromJsonAsync<JsonElement>(Json);
        var sku2 = producto2.GetProperty("skuInterno").GetString();
        Assert.NotEqual(sku, sku2);
        Assert.Matches(SkuRcp, sku2!);
    }

    [Fact]
    public async Task Trunqi_bloquea_flujo_cromos_sin_codigo()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.EmpresaId, "admin@trunqi.local");

        var preview = await client.GetAsync("/api/productos-tcg/sku-interno/preview?categoria=ALB");
        Assert.Equal(HttpStatusCode.Forbidden, preview.StatusCode);

        var crear = await client.PostAsJsonAsync("/api/productos-tcg", new
        {
            tipoProducto = "ACCESORIO",
            nombre = "Cromo Trunqi bloqueado",
            esColeccionableSinCodigo = true,
            codigoCategoriaSku = "CRO",
            precioVenta = 1m
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, crear.StatusCode);
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
}
