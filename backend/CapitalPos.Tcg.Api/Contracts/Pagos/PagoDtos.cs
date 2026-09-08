using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Pagos;

public sealed class RegistrarPagoRequest
{
    public OrigenPago Origen { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Monto { get; set; }

    [MaxLength(80)]
    public string? CodigoOperacion { get; set; }

    [MaxLength(120)]
    public string? ReferenciaExterna { get; set; }

    public Guid? PedidoDigitalId { get; set; }

    [MaxLength(160)]
    public string? ClienteNombre { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }

    public bool Confirmar { get; set; }
}

public sealed class AsociarPagoRequest
{
    [Required]
    public Guid PedidoDigitalId { get; set; }
}

public sealed class RechazarPagoRequest
{
    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class PagoResponse
{
    public required Guid Id { get; init; }
    public required OrigenPago Origen { get; init; }
    public required EstadoPago Estado { get; init; }
    public required decimal Monto { get; init; }
    public string? CodigoOperacion { get; init; }
    public string? ReferenciaExterna { get; init; }
    public Guid? PedidoDigitalId { get; init; }
    public string? PedidoCodigo { get; init; }
    public Guid? VentaId { get; init; }
    public string? ClienteNombre { get; init; }
    public required DateTimeOffset FechaNotificacion { get; init; }
    public DateTimeOffset? FechaConfirmacion { get; init; }
    public Guid? UsuarioAsocioId { get; init; }
    public string? UsuarioAsocioNombre { get; init; }
    public string? Observacion { get; init; }
}
