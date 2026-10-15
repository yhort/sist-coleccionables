using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Contracts.Pagos;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Pos;

public sealed class PosLineaInput
{
    [Required]
    public Guid ProductoId { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; } = 1;

    /// <summary>Si se omite o es ≤ 0, se usa el precio de venta del producto.</summary>
    [Range(typeof(decimal), "0", "999999999")]
    public decimal? PrecioUnitario { get; set; }
}

/// <summary>
/// Facade POS: cobra pedidos FB pendientes y/o ítems de mostrador, y emite CPE.
/// Debe enviarse <see cref="PedidoDigitalIds"/> o <see cref="Items"/> (o ambos).
/// </summary>
public sealed class CobrarYEmitirPosRequest
{
    [Required]
    public Guid SedeId { get; set; }

    /// <summary>Pedidos digitales en PendientePago (p. ej. adjudicados de subasta FB).</summary>
    public List<Guid>? PedidoDigitalIds { get; set; }

    /// <summary>Ítems de venta mostrador (crea pedido + cobro inmediato).</summary>
    public List<PosLineaInput>? Items { get; set; }

    [MaxLength(160)]
    public string? ClienteNombre { get; set; }

    public TipoDocumentoIdentidad? TipoDocumento { get; set; }

    [MaxLength(16)]
    public string? NumeroDocumento { get; set; }

    public OrigenPago OrigenPago { get; set; } = OrigenPago.EFECTIVO;

    [MaxLength(80)]
    public string? CodigoOperacion { get; set; }

    [MaxLength(120)]
    public string? ReferenciaExterna { get; set; }

    /// <summary>Monto recibido en efectivo (validación de vuelto).</summary>
    [Range(typeof(decimal), "0", "999999999")]
    public decimal? MontoRecibido { get; set; }

    public TipoComprobanteSunat TipoComprobante { get; set; } = TipoComprobanteSunat.BOLETA;

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class CobrarYEmitirPosResponse
{
    public required Guid VentaId { get; init; }
    public required ComprobanteResponse Comprobante { get; init; }
    public required IReadOnlyList<PedidoDigitalResponse> Pedidos { get; init; }
    public required IReadOnlyList<PagoResponse> Pagos { get; init; }
    public required decimal TotalCobrado { get; init; }
    public string? Mensaje { get; init; }
}
