using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

/// <summary>
/// Sprint 5 — Flujo Paola: lote FB 10 → texto → adjudicar → POS cobro consolidado → CPE (PDF/XML).
/// </summary>
[Collection("SubastaE2E")]
public sealed class PaolaFlujoE2ETests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private const string Png1x1Base64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly ApiFactory _factory;

    public PaolaFlujoE2ETests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Paola_lote10_texto_fb_pos_cobra_y_emite_cpe_con_pdf_xml()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.RcpEmpresaId, "admin@rcp.local");
        var sedeId = DevelopmentDataSeeder.RcpSedeId;
        await AsegurarCajaAsync(client, sedeId);

        const int n = 10;
        var ahora = DateTimeOffset.UtcNow;
        var items = Enumerable.Range(1, n).Select(i => (object)new
        {
            titulo = $"Sobre Paola Mundial {i:D2}",
            precioBase = 5m + i,
            observaciones = "E2E Paola Sprint 5",
            imagenBase64 = Png1x1Base64,
            imagenNombreArchivo = $"paola-{i}.png",
            imagenContentType = "image/png"
        }).ToArray();

        var loteResp = await client.PostAsJsonAsync(
            "/api/subastas-tcg/lote-masivo",
            new
            {
                sedeId,
                fechaInicio = ahora,
                horaFinBase = ahora.AddHours(2),
                intervaloMinutos = 1,
                incrementoMinimo = 1m,
                items
            },
            Json);
        Assert.True(loteResp.IsSuccessStatusCode, await loteResp.Content.ReadAsStringAsync());
        var lote = await loteResp.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(n, lote.GetProperty("totalItems").GetInt32());

        var loteItems = lote.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(n, loteItems.Count);
        foreach (var item in loteItems)
        {
            var texto = item.GetProperty("textoFacebook").GetString() ?? string.Empty;
            Assert.Contains("ENTREGA: Jr. Carabaya #1150", texto, StringComparison.Ordinal);
            Assert.Contains("ARTÍCULO #", texto, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("imagenUrl").GetString()));
        }

        var pedidoIds = new List<Guid>(n);
        decimal totalEsperado = 0;
        foreach (var item in loteItems)
        {
            var subastaId = GuidDe(item, "subastaId");
            var precio = item.GetProperty("precioBase").GetDecimal();
            totalEsperado += precio;

            var activar = await client.PostAsync($"/api/subastas-tcg/{subastaId}/activar", null);
            Assert.True(activar.IsSuccessStatusCode, await activar.Content.ReadAsStringAsync());

            var adjudicar = await client.PostAsJsonAsync(
                $"/api/subastas-tcg/{subastaId}/adjudicar",
                new
                {
                    nombrePostor = "Paola FB",
                    montoAdjudicado = precio,
                    metodoEnvio = "RECOJO_TIENDA"
                },
                Json);
            Assert.True(adjudicar.IsSuccessStatusCode, await adjudicar.Content.ReadAsStringAsync());
            var adjudicada = await adjudicar.Content.ReadFromJsonAsync<JsonElement>(Json);
            var pedidoId = GuidDe(adjudicada, "pedidoDigitalId");
            Assert.NotEqual(Guid.Empty, pedidoId);
            pedidoIds.Add(pedidoId);
        }

        var pendientes = await client.GetFromJsonAsync<List<JsonElement>>(
            $"/api/pos/pedidos-pendientes?q=Paola&sedeId={sedeId}",
            Json);
        Assert.NotNull(pendientes);
        Assert.True(pendientes.Count >= n, $"Se esperaban >= {n} pendientes Paola, hay {pendientes.Count}");
        foreach (var id in pedidoIds)
        {
            Assert.Contains(pendientes, p => GuidDe(p, "id") == id);
        }

        var cobrar = await client.PostAsJsonAsync(
            "/api/pos/cobrar-y-emitir",
            new
            {
                sedeId,
                pedidoDigitalIds = pedidoIds,
                origenPago = "YAPE",
                codigoOperacion = $"YAPE-PAOLA-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
                tipoComprobante = "BOLETA",
                clienteNombre = "Paola FB",
                observacion = "E2E Paola Sprint 5"
            },
            Json);
        Assert.True(cobrar.IsSuccessStatusCode, await cobrar.Content.ReadAsStringAsync());
        var body = await cobrar.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(Math.Round(totalEsperado, 2), Math.Round(body.GetProperty("totalCobrado").GetDecimal(), 2));

        Assert.True(body.TryGetProperty("comprobante", out var cpe));
        var cpeId = GuidDe(cpe, "id");
        Assert.NotEqual(Guid.Empty, cpeId);
        var estado = StringDe(cpe, "estado");
        Assert.True(
            estado is "SIMULADO" or "ACEPTADO" or "PENDIENTE_CONSOLIDAR",
            $"Estado CPE inesperado: {estado}");
        Assert.True(cpe.GetProperty("tienePdf").GetBoolean());
        Assert.True(cpe.GetProperty("tieneXml").GetBoolean());

        var pdf = await client.GetAsync($"/api/ecosistema/cpe/{cpeId}/pdf?formato=ticket");
        Assert.True(pdf.IsSuccessStatusCode, await pdf.Content.ReadAsStringAsync());
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(pdfBytes.Length > 50);

        var xml = await client.GetAsync($"/api/ecosistema/cpe/{cpeId}/xml");
        Assert.True(xml.IsSuccessStatusCode, await xml.Content.ReadAsStringAsync());
        var xmlBytes = await xml.Content.ReadAsByteArrayAsync();
        Assert.True(xmlBytes.Length > 20);

        foreach (var pedidoId in pedidoIds)
        {
            var pedido = await client.GetFromJsonAsync<JsonElement>($"/api/pedidos-digitales/{pedidoId}", Json);
            Assert.Equal("Pagado", StringDe(pedido, "estado"));
            Assert.True(
                pedido.TryGetProperty("comprobante", out var resumen)
                && resumen.ValueKind == JsonValueKind.Object);
        }
    }

    [Fact]
    public async Task Trunqi_sigue_aislado_de_lote_pos_y_cromos()
    {
        using var client = _factory.CreateClient();
        await LoginAsync(client, DevelopmentDataSeeder.EmpresaId, "admin@trunqi.local");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/pos/estado")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/pos/pedidos-pendientes")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync(
                "/api/subastas-tcg/lote-masivo",
                new { sedeId = DevelopmentDataSeeder.SedeId, items = Array.Empty<object>() },
                Json)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/productos-tcg/sku-interno/preview?categoria=ALB")).StatusCode);
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
            new { sedeId, montoApertura = 200m, observacion = "Apertura E2E Paola" },
            Json);
        Assert.True(abrir.IsSuccessStatusCode, await abrir.Content.ReadAsStringAsync());
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
        if (!element.TryGetProperty(property, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
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
