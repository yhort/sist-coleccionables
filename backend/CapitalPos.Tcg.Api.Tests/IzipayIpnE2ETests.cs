using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Izipay;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

[CollectionDefinition("IzipayIpnE2E", DisableParallelization = true)]
public sealed class IzipayIpnE2ECollection : ICollectionFixture<ApiFactory>;

[Collection("IzipayIpnE2E")]
public sealed class IzipayIpnE2ETests
{
    private const string HmacKey = "test-hmac-sha256-key-izipay";
    private const string ShopId = "12345678";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;

    public IzipayIpnE2ETests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ipn_paid_yape_registra_notificado_y_no_duplica()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        await GuardarConfigAsync(client);

        var uuid = Guid.NewGuid().ToString("N");
        var answer = KrAnswer("PAID", uuid, "YAPE", 15000);
        var hash = IzipayHmacValidator.CalcularHex(answer, HmacKey);

        var ipn = await PostIpnAsync(client, answer, hash);
        Assert.Equal(HttpStatusCode.OK, ipn.StatusCode);

        var pagos = await ListarNotificadosAsync(client, uuid);
        var pago = Assert.Single(pagos);
        Assert.Equal("YAPE", pago.GetProperty("origen").GetString());
        Assert.Equal("NOTIFICADO", pago.GetProperty("estado").GetString());
        Assert.Equal(150m, pago.GetProperty("monto").GetDecimal());

        var repetido = await PostIpnAsync(client, answer, hash);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Single(await ListarNotificadosAsync(client, uuid));
    }

    [Fact]
    public async Task Ipn_firma_invalida_devuelve_401()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        await GuardarConfigAsync(client);

        var answer = KrAnswer("PAID", Guid.NewGuid().ToString("N"), "VISA", 1000);
        var ipn = await PostIpnAsync(client, answer, "00" + new string('a', 62));
        Assert.Equal(HttpStatusCode.Unauthorized, ipn.StatusCode);
    }

    [Fact]
    public async Task Ipn_unpaid_no_crea_pago()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client);
        await GuardarConfigAsync(client);

        var uuid = Guid.NewGuid().ToString("N");
        var answer = KrAnswer("UNPAID", uuid, "YAPE", 2000);
        var hash = IzipayHmacValidator.CalcularHex(answer, HmacKey);
        var ipn = await PostIpnAsync(client, answer, hash);
        Assert.Equal(HttpStatusCode.OK, ipn.StatusCode);
        Assert.Empty(await ListarNotificadosAsync(client, uuid));
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

    private static async Task GuardarConfigAsync(HttpClient client)
    {
        var guardar = await client.PutAsJsonAsync(
            "/api/pagos/izipay/config",
            new { shopId = ShopId, hmacSha256Clave = HmacKey, modo = "TEST", activa = true },
            Json);
        Assert.True(guardar.IsSuccessStatusCode, await guardar.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PostIpnAsync(HttpClient client, string answer, string hash)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["kr-hash"] = hash,
            ["kr-hash-algorithm"] = "sha256",
            ["kr-answer"] = answer
        });
        return client.PostAsync($"/api/pagos/izipay/ipn/{DevelopmentDataSeeder.EmpresaId}", content);
    }

    private static async Task<List<JsonElement>> ListarNotificadosAsync(HttpClient client, string uuid)
    {
        var lista = await client.GetFromJsonAsync<List<JsonElement>>("/api/pagos?estado=NOTIFICADO", Json)
            ?? [];
        return lista.Where(p => p.GetProperty("codigoOperacion").GetString() == uuid).ToList();
    }

    private static string KrAnswer(string orderStatus, string uuid, string method, int amountCentavos) =>
        $$"""{"shopId":"{{ShopId}}","orderStatus":"{{orderStatus}}","orderDetails":{"orderTotalAmount":{{amountCentavos}}},"transactions":[{"uuid":"{{uuid}}","status":"{{orderStatus}}","amount":{{amountCentavos}},"paymentMethodType":"{{method}}"}]}""";
}
