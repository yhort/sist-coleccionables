using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Entregas;

public sealed class ProgramarEntregaRequest
{
    [Required]
    public Guid PedidoDigitalId { get; set; }

    public MetodoEnvio MetodoEnvio { get; set; } = MetodoEnvio.RECOJO_TIENDA;

    [MaxLength(160)]
    public string? DestinatarioNombre { get; set; }

    [MaxLength(32)]
    public string? DestinatarioTelefono { get; set; }

    [MaxLength(300)]
    public string? Direccion { get; set; }

    [MaxLength(80)]
    public string? Distrito { get; set; }

    [MaxLength(80)]
    public string? Provincia { get; set; }

    [MaxLength(80)]
    public string? Departamento { get; set; }

    [MaxLength(80)]
    public string? Agencia { get; set; }

    [MaxLength(80)]
    public string? PuntoEntrega { get; set; }

    public CanalContactoCliente? CanalContacto { get; set; }

    [MaxLength(160)]
    public string? ContactoReferencia { get; set; }

    [MaxLength(80)]
    public string? NumeroTracking { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal CostoEnvio { get; set; }

    [MaxLength(500)]
    public string? NotasEmpaque { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class EmpaquetarEntregaRequest
{
    [Required]
    public Guid PedidoDigitalId { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(500)]
    public string NotasEmpaque { get; set; } = string.Empty;
}

public sealed class DespacharEntregaRequest
{
    public MetodoEnvio MetodoEnvio { get; set; }

    [MaxLength(80)]
    public string? NumeroTracking { get; set; }

    [MaxLength(80)]
    public string? Agencia { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal CostoEnvio { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class FallarEntregaRequest
{
    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class EntregaResponse
{
    public required Guid Id { get; init; }
    public required Guid PedidoDigitalId { get; init; }
    public required string PedidoCodigo { get; init; }
    public required string ClienteNombre { get; init; }
    public required EstadoPedidoDigital EstadoPedido { get; init; }
    public Guid? VentaId { get; init; }
    public required Guid SedeOrigenId { get; init; }
    public required string SedeOrigenNombre { get; init; }
    public required MetodoEnvio MetodoEnvio { get; init; }
    public required EstadoLogistica Estado { get; init; }
    public required string DestinatarioNombre { get; init; }
    public string? DestinatarioTelefono { get; init; }
    public string? Direccion { get; init; }
    public string? Distrito { get; init; }
    public string? Provincia { get; init; }
    public string? Departamento { get; init; }
    public string? Agencia { get; init; }
    public string? PuntoEntrega { get; init; }
    public CanalContactoCliente? CanalContacto { get; init; }
    public string? ContactoReferencia { get; init; }
    public string? NumeroTracking { get; init; }
    public required decimal CostoEnvio { get; init; }
    public string? NotasEmpaque { get; init; }
    public DateTimeOffset? FechaProgramada { get; init; }
    public DateTimeOffset? FechaDespacho { get; init; }
    public DateTimeOffset? FechaEntrega { get; init; }
    public string? Observacion { get; init; }
}
