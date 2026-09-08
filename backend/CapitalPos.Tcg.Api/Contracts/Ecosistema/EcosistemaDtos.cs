using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Ecosistema;

public sealed class ConfiguracionFiscalResponse
{
    public required Guid Id { get; init; }
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public required string NombreComercial { get; init; }
    public required string DireccionFiscal { get; init; }
    public required string Ubigeo { get; init; }
    public required string Departamento { get; init; }
    public required string Provincia { get; init; }
    public required string Distrito { get; init; }
}

public sealed class GuardarConfiguracionFiscalRequest
{
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string NombreComercial { get; set; } = string.Empty;
    public string DireccionFiscal { get; set; } = string.Empty;
    public string Ubigeo { get; set; } = string.Empty;
    public string Departamento { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string Distrito { get; set; } = string.Empty;
}

public sealed class EcosistemaConexionResponse
{
    public required Guid Id { get; init; }
    public required TipoConexionEcosistema Tipo { get; init; }
    public required string Nombre { get; init; }
    public required EstadoSaludConexion EstadoSalud { get; init; }
    public DateTimeOffset? UltimoPing { get; init; }
    public string? UltimoError { get; init; }
}

public sealed class SerieComprobanteResponse
{
    public required Guid Id { get; init; }
    public required TipoComprobanteSunat Tipo { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required bool Activa { get; init; }
}

public sealed class WebhookSalidaResponse
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public required string Url { get; init; }
    public required string Token { get; init; }
    public required string Canal { get; init; }
    public required IReadOnlyList<string> Eventos { get; init; }
    public required bool Activo { get; init; }
    public required IReadOnlyList<WebhookLogResponse> Logs { get; init; }
}

public sealed class GuardarWebhookSalidaRequest
{
    [MaxLength(160)]
    public string Nombre { get; set; } = "WhatsApp pedidos y guías";

    [Required]
    [MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    public IReadOnlyList<string> Eventos { get; set; } = ["pedido.estado"];

    public bool Activo { get; set; } = true;
}

public sealed class ProbarWebhookRequest
{
    [Required]
    public string Evento { get; set; } = "pedido.estado";
}

public sealed class WebhookLogResponse
{
    public required Guid Id { get; init; }
    public required Guid WebhookId { get; init; }
    public required string Evento { get; init; }
    public required string Destino { get; init; }
    public required string PayloadResumen { get; init; }
    public required string Estado { get; init; }
    public required DateTimeOffset Fecha { get; init; }
}
