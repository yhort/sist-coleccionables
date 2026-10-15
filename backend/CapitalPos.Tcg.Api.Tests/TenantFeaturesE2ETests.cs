using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class TenantFeaturesE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public TenantFeaturesE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Trunqi_tiene_singles_y_woo_pero_no_pos()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.EmpresaId, "admin@trunqi.local");

        var woo = await client.GetAsync("/api/woocommerce/config");
        Assert.NotEqual(HttpStatusCode.Forbidden, woo.StatusCode);

        var pos = await client.GetAsync("/api/pos/estado");
        Assert.Equal(HttpStatusCode.Forbidden, pos.StatusCode);
    }

    [Fact]
    public async Task Rcp_tiene_pos_y_bloquea_woocommerce()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");

        var pos = await client.GetAsync("/api/pos/estado");
        Assert.True(pos.IsSuccessStatusCode, await pos.Content.ReadAsStringAsync());

        var woo = await client.GetAsync("/api/woocommerce/config");
        Assert.Equal(HttpStatusCode.Forbidden, woo.StatusCode);

        // RCP tiene cromos sin código: catálogo de productos accesible (no singles TCG).
        var productos = await client.GetAsync("/api/productos-tcg");
        Assert.True(productos.IsSuccessStatusCode, await productos.Content.ReadAsStringAsync());

        var aperturas = await client.GetAsync("/api/aperturas-tcg");
        Assert.Equal(HttpStatusCode.Forbidden, aperturas.StatusCode);
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
