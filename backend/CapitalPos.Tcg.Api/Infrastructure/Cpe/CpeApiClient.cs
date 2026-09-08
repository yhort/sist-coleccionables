using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// Cliente HTTP de <c>capitalpos-cpe-api</c>: <c>POST /api/cpe/emitir</c>
/// con el contrato <c>ApiResponse&lt;CpeEmisionResponse&gt;</c>.
/// </summary>
public sealed class CpeApiClient(HttpClient http, IOptions<CpeApiOptions> options) : ICpeEmisor
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Nombre => "CpeApi";

    public async Task<CpeEmisionResultado> EmitirAsync(EmitirCpeRequest request, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/cpe/emitir")
        {
            Content = JsonContent.Create(request, options: Json)
        };
        AdjuntarApiKey(message, cfg);

        using var respuesta = await http.SendAsync(message, cancellationToken);
        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        var envelope = DeserializarEnvelope(cuerpo);
        var data = envelope?.Data;

        if (data is null)
        {
            var errores = envelope?.Errores is { Count: > 0 }
                ? string.Join(" | ", envelope.Errores)
                : Truncar(cuerpo);
            return new CpeEmisionResultado
            {
                Estado = respuesta.IsSuccessStatusCode
                    ? EstadoEmisionSunat.ERROR_VALIDACION
                    : EstadoEmisionSunat.ERROR_SUNAT,
                Xml = string.Empty,
                Mensaje = Truncar(envelope?.Mensaje ?? $"CPE API {(int)respuesta.StatusCode}: {errores}")
            };
        }

        var xml = await ObtenerXmlAsync(data.NombreXml, cfg, cancellationToken);
        var cdr = await ObtenerCdrAsync(data.NombreCdr, cfg, cancellationToken);
        TryParseSerieCorrelativo(data.Comprobante, out var serie, out var correlativo);

        return new CpeEmisionResultado
        {
            Estado = ParseEstado(data.Estado),
            Xml = xml ?? data.NombreXml ?? string.Empty,
            Cdr = cdr ?? data.NombreCdr,
            HashFirma = data.Hash,
            Mensaje = Truncar(ComponerMensaje(envelope, data)),
            Serie = serie,
            Correlativo = correlativo,
            NombreXml = data.NombreXml,
            NombreCdr = data.NombreCdr
        };
    }

    private async Task<string?> ObtenerXmlAsync(
        string? nombreXml,
        CpeApiOptions cfg,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nombreXml))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/cpe/archivos/xml/{Uri.EscapeDataString(nombreXml)}");
            AdjuntarApiKey(request, cfg);
            using var respuesta = await http.SendAsync(request, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                return null;
            }

            var contenido = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(contenido) ? null : contenido;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<string?> ObtenerCdrAsync(
        string? nombreCdr,
        CpeApiOptions cfg,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nombreCdr))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/cpe/archivos/cdr/{Uri.EscapeDataString(nombreCdr)}/descargar");
            AdjuntarApiKey(request, cfg);
            using var respuesta = await http.SendAsync(request, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await respuesta.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
            {
                return null;
            }

            return ExtraerXmlDeZip(bytes) ?? Convert.ToBase64String(bytes);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? ExtraerXmlDeZip(byte[] zipBytes)
    {
        try
        {
            using var stream = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entrada = zip.Entries.FirstOrDefault(e =>
                e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            if (entrada is null)
            {
                return null;
            }

            using var reader = new StreamReader(entrada.Open());
            var xml = reader.ReadToEnd();
            return string.IsNullOrWhiteSpace(xml) ? null : xml;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static CpeApiEnvelope? DeserializarEnvelope(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CpeApiEnvelope>(cuerpo, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void AdjuntarApiKey(HttpRequestMessage message, CpeApiOptions cfg)
    {
        var apiKey = cfg.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || message.Headers.Contains(CpeApiKeyHandler.HeaderName))
        {
            return;
        }

        message.Headers.TryAddWithoutValidation(CpeApiKeyHandler.HeaderName, apiKey);
    }

    internal static bool TryParseSerieCorrelativo(string? comprobante, out string? serie, out int? correlativo)
    {
        serie = null;
        correlativo = null;
        if (string.IsNullOrWhiteSpace(comprobante))
        {
            return false;
        }

        var partes = comprobante.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length >= 2
            && partes[^2].Length == 4
            && int.TryParse(partes[^1], out var numero)
            && numero > 0)
        {
            serie = partes[^2];
            correlativo = numero;
            return true;
        }

        return false;
    }

    internal static EstadoEmisionSunat ParseEstado(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado))
        {
            return EstadoEmisionSunat.ERROR_SUNAT;
        }

        if (Enum.TryParse<EstadoEmisionSunat>(estado, true, out var parsed)
            && parsed is EstadoEmisionSunat.SIMULADO
                or EstadoEmisionSunat.ACEPTADO
                or EstadoEmisionSunat.RECHAZADO
                or EstadoEmisionSunat.ERROR_VALIDACION
                or EstadoEmisionSunat.ERROR_SUNAT
                or EstadoEmisionSunat.PENDIENTE_CONSOLIDAR
                or EstadoEmisionSunat.CONSOLIDADA)
        {
            return parsed;
        }

        return estado.ToUpperInvariant() switch
        {
            "OBSERVADO" => EstadoEmisionSunat.ACEPTADO,
            "PENDIENTE" => EstadoEmisionSunat.SIMULADO,
            "ERROR_VALIDACION" => EstadoEmisionSunat.ERROR_VALIDACION,
            "ERROR_XML" or "ERROR_FIRMA" or "ERROR_CDR" or "ERROR_INTERNO" or "ERROR_SUNAT"
                => EstadoEmisionSunat.ERROR_SUNAT,
            _ => EstadoEmisionSunat.ERROR_SUNAT
        };
    }

    private static string ComponerMensaje(CpeApiEnvelope? envelope, CpeApiEmisionData data)
    {
        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.Mensaje))
        {
            partes.Add(data.Mensaje);
        }
        else if (!string.IsNullOrWhiteSpace(envelope?.Mensaje))
        {
            partes.Add(envelope.Mensaje);
        }

        var errores = (data.Errores ?? []).Concat(envelope?.Errores ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (errores.Count > 0)
        {
            partes.Add(string.Join(" | ", errores));
        }

        return partes.Count == 0 ? "Respuesta de capitalpos-cpe-api." : string.Join(" ", partes);
    }

    private static string Truncar(string texto) =>
        texto.Length <= 500 ? texto : texto[..500];

    private sealed class CpeApiEnvelope
    {
        public bool Ok { get; set; }
        public string? Mensaje { get; set; }
        public CpeApiEmisionData? Data { get; set; }
        public List<string>? Errores { get; set; }
    }

    private sealed class CpeApiEmisionData
    {
        public bool Ok { get; set; }
        public string? Estado { get; set; }
        public string? Mensaje { get; set; }
        public string? Comprobante { get; set; }
        public string? Hash { get; set; }
        public string? NombreXml { get; set; }
        public string? NombreZip { get; set; }
        public bool XmlFirmado { get; set; }
        public string? NombreCdr { get; set; }
        public List<string>? Errores { get; set; }
    }
}
