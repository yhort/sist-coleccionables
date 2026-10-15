using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[Collection("SubastaE2E")]
public sealed class LoteMasivoSubastaE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    // 1x1 PNG
    private const string Png1x1Base64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly ApiFactory _factory;

    public LoteMasivoSubastaE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rcp_crea_lote_masivo_con_texto_facebook()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");

        var ahora = DateTimeOffset.UtcNow;
        var body = new
        {
            sedeId = DevelopmentDataSeeder.RcpSedeId,
            fechaInicio = ahora,
            horaFinBase = ahora.AddHours(2),
            intervaloMinutos = 1,
            incrementoMinimo = 1m,
            items = new object[]
            {
                new
                {
                    titulo = "Sobre Panini Mundial 01",
                    precioBase = 5m,
                    observaciones = "Lote E2E",
                    imagenBase64 = Png1x1Base64,
                    imagenNombreArchivo = "sobre1.png",
                    imagenContentType = "image/png"
                },
                new
                {
                    titulo = "Sobre Panini Mundial 02",
                    precioBase = 7.5m,
                    horaFin = ahora.AddHours(2).AddMinutes(1),
                    imagenBase64 = "data:image/png;base64," + Png1x1Base64,
                    imagenNombreArchivo = "sobre2.png",
                    imagenContentType = "image/png"
                }
            }
        };

        var response = await client.PostAsJsonAsync("/api/subastas-tcg/lote-masivo", body, Json);
        var bodyTxt = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, bodyTxt);

        var lote = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(2, lote.GetProperty("totalItems").GetInt32());
        var items = lote.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        var primero = items[0];
        Assert.Equal(1, primero.GetProperty("numArticulo").GetInt32());
        var texto = primero.GetProperty("textoFacebook").GetString() ?? string.Empty;
        Assert.Contains("SUBASTA : #", texto, StringComparison.Ordinal);
        Assert.Contains("ARTÍCULO #1", texto, StringComparison.Ordinal);
        Assert.Contains("PRODUCTO: Sobre Panini Mundial 01", texto, StringComparison.Ordinal);
        Assert.Contains("PRECIO: 5 SOLES", texto, StringComparison.Ordinal);
        Assert.Contains("ENTREGA: Jr. Carabaya #1150", texto, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(primero.GetProperty("imagenUrl").GetString()));
    }

    [Fact]
    public async Task Trunqi_no_puede_usar_lote_masivo()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.EmpresaId, "admin@trunqi.local");

        var response = await client.PostAsJsonAsync(
            "/api/subastas-tcg/lote-masivo",
            new
            {
                sedeId = DevelopmentDataSeeder.SedeId,
                items = new[]
                {
                    new { titulo = "No debe pasar", precioBase = 1m }
                }
            },
            Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Remove("X-CapitalPos-EmpresaId");
        client.DefaultRequestHeaders.Add("X-CapitalPos-EmpresaId", empresaId.ToString());
    }
}
