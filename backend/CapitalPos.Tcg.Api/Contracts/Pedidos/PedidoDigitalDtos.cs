using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Pedidos;

public sealed class PedidoDigitalDetalleInput
{
    [Required]
    public Guid ProductoId { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal PrecioUnitario { get; set; }
}

public sealed class PedidoDigitalEntregaInput
{
    [MaxLength(160)]
    public string? DestinatarioNombre { get; set; }

    /// <summary>Solo dígitos, máximo 9.</summary>
    [MaxLength(9)]
    [RegularExpression(@"^\d{0,9}$", ErrorMessage = "El teléfono solo admite números (máximo 9 dígitos).")]
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
    public string? Courier { get; set; }

    public bool EsRecojoTienda { get; set; } = true;

    [MaxLength(80)]
    public string? NumeroTracking { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal CostoEnvio { get; set; }

    [MaxLength(500)]
    public string? NotasEmpaque { get; set; }

    [MaxLength(80)]
    public string? Agencia { get; set; }

    [MaxLength(80)]
    public string? PuntoEntrega { get; set; }

    public CanalContactoCliente? CanalContacto { get; set; }

    [MaxLength(160)]
    public string? ContactoReferencia { get; set; }
}

public sealed class CrearPedidoDigitalRequest
{
    public Guid? ClienteId { get; set; }

    [Required]
    [MinLength(2)]
    [MaxLength(160)]
    public string ClienteNombre { get; set; } = string.Empty;

    /// <summary>Solo dígitos, máximo 9.</summary>
    [MaxLength(9)]
    [RegularExpression(@"^\d{0,9}$", ErrorMessage = "El teléfono solo admite números (máximo 9 dígitos).")]
    public string? ClienteTelefono { get; set; }

    public TipoDocumentoIdentidad? TipoDocumento { get; set; }

    /// <summary>Opcional. Vacío/nulo → SIN_DOCUMENTO (válido para boletas ≤ S/ 700).</summary>
    [MaxLength(16)]
    public string? NumeroDocumento { get; set; }

    public bool EsClienteVarios { get; set; }

    [Required]
    public Guid SedeId { get; set; }

    public CanalPedidoDigital CanalPedido { get; set; } = CanalPedidoDigital.OTRO;

    [MaxLength(80)]
    public string? ReferenciaExterna { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<PedidoDigitalDetalleInput> Detalles { get; set; } = [];

    public PedidoDigitalEntregaInput? Entrega { get; set; }

    /// <summary>Si true, actualiza punto/canal/teléfono predeterminados en la ficha del cliente.</summary>
    public bool GuardarPuntoEnCliente { get; set; }

    /// <summary>Cobro POS/caja: crea el pedido ya en Pagado y registra el pago confirmado.</summary>
    public CobroInmediatoPedidoInput? CobroInmediato { get; set; }
}

public sealed class CobroInmediatoPedidoInput
{
    public OrigenPago Origen { get; set; } = OrigenPago.EFECTIVO;

    [MaxLength(80)]
    public string? CodigoOperacion { get; set; }

    [MaxLength(120)]
    public string? ReferenciaExterna { get; set; }

    /// <summary>Monto recibido en efectivo (solo informativo / validación de vuelto en UI).</summary>
    [Range(typeof(decimal), "0", "999999999")]
    public decimal? MontoRecibido { get; set; }
}

public sealed class CambiarEstadoPedidoRequest
{
    public EstadoPedidoDigital Estado { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class ActualizarNotificacionPedidoRequest
{
    public bool Notificado { get; set; }
}

public sealed class ActualizarNotificacionLoteRequest
{
    public bool Notificado { get; set; } = true;

    [Required]
    [MinLength(1)]
    public List<Guid> PedidoDigitalIds { get; set; } = [];
}

public sealed class CambiarEstadoLoteRequest
{
    public EstadoPedidoDigital Estado { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }

    [Required]
    [MinLength(1)]
    public List<Guid> PedidoDigitalIds { get; set; } = [];
}

public sealed class EmitirComprobanteConsolidadoRequest
{
    public TipoComprobanteSunat TipoComprobante { get; set; } = TipoComprobanteSunat.BOLETA;

    [Required]
    [MinLength(1)]
    public List<Guid> PedidoDigitalIds { get; set; } = [];
}

public sealed class ConvertirVentaRequest
{
    [MaxLength(500)]
    public string? Observacion { get; set; }

    public TipoComprobanteSunat TipoComprobante { get; set; } = TipoComprobanteSunat.BOLETA;

    public bool EmitirComprobante { get; set; } = true;

    public TipoDocumentoIdentidad? TipoDocumento { get; set; }

    /// <summary>
    /// Opcional en boleta o nota de venta con total ≤ S/ 700 (SUNAT no exige identificar al adquirente).
    /// Vacío/nulo: no rellenar con ceros; se emite como consumidor final sin documento.
    /// Obligatorio (DNI u otro) si la boleta supera S/ 700. Factura exige RUC.
    /// </summary>
    [MaxLength(16)]
    public string? NumeroDocumento { get; set; }
}

public sealed class PedidoDigitalDetalleResponse
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public required Guid ProductoId { get; init; }
    public required string Descripcion { get; init; }
    public string? CodigoSku { get; init; }
    public required decimal Cantidad { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public required decimal Total { get; init; }
}

public sealed class PedidoDigitalHistorialResponse
{
    public required Guid Id { get; init; }
    public EstadoPedidoDigital? EstadoAnterior { get; init; }
    public required EstadoPedidoDigital EstadoNuevo { get; init; }
    public Guid? UsuarioId { get; init; }
    public string? UsuarioNombre { get; init; }
    public required DateTimeOffset Fecha { get; init; }
    public string? Observacion { get; init; }
}

public sealed class PedidoDigitalEntregaResponse
{
    public required string DestinatarioNombre { get; init; }
    public string? DestinatarioTelefono { get; init; }
    public string? Direccion { get; init; }
    public string? Distrito { get; init; }
    public string? Provincia { get; init; }
    public string? Departamento { get; init; }
    public string? Courier { get; init; }
    public required bool EsRecojoTienda { get; init; }
    public string? NumeroTracking { get; init; }
    public required decimal CostoEnvio { get; init; }
    public string? NotasEmpaque { get; init; }
    public string? Agencia { get; init; }
    public string? PuntoEntrega { get; init; }
    public CanalContactoCliente? CanalContacto { get; init; }
    public string? ContactoReferencia { get; init; }
}

public sealed class PedidoDigitalResponse
{
    public required Guid Id { get; init; }
    public required string Codigo { get; init; }
    public Guid? ClienteId { get; init; }
    public required string ClienteNombre { get; init; }
    public string? ClienteTelefono { get; init; }
    public TipoDocumentoIdentidad? ClienteTipoDocumento { get; init; }
    public string? ClienteNumeroDocumento { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required CanalPedidoDigital CanalPedido { get; init; }
    public required EstadoPedidoDigital Estado { get; init; }
    public required IndicadorReservaPedido IndicadorReserva { get; init; }
    public required DateTimeOffset FechaPedido { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Igv { get; init; }
    public required decimal Total { get; init; }
    public string? ReferenciaExterna { get; init; }
    public string? Observacion { get; init; }
    public Guid? SubastaTcgId { get; init; }
    public string? CodigoSubasta { get; init; }
    public string? TituloSubasta { get; init; }
    public bool Notificado { get; init; }
    public DateTimeOffset? FechaNotificacion { get; init; }
    public Guid? VentaId { get; init; }
    public string? CodigoVenta { get; init; }
    public Guid? EntregaId { get; init; }
    /// <summary>Documento de venta (boleta/factura/nota de venta) si ya se emitió.</summary>
    public PedidoComprobanteResumen? Comprobante { get; init; }
    /// <summary>Última nota de crédito asociada a la venta, si existe.</summary>
    public PedidoComprobanteResumen? NotaCredito { get; init; }
    public required PedidoDigitalEntregaResponse Entrega { get; init; }
    public required IReadOnlyList<PedidoDigitalDetalleResponse> Detalles { get; init; }
    public required IReadOnlyList<PedidoDigitalHistorialResponse> HistorialEstados { get; init; }
}

public sealed class PedidoComprobanteResumen
{
    public required Guid Id { get; init; }
    public required TipoComprobanteSunat Tipo { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required EstadoEmisionSunat Estado { get; init; }
    public string? DocumentoReferencia { get; init; }
    public string? CodigoMotivo { get; init; }
    public string? DescripcionMotivo { get; init; }
}

public sealed class ConversionVentaResponse
{
    public required PedidoDigitalResponse Pedido { get; init; }
    public required Guid VentaId { get; init; }
    public required Guid? ComprobanteId { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required TipoComprobanteSunat TipoComprobante { get; init; }
    public required EstadoEmisionSunat EstadoEmision { get; init; }
}

public sealed class ComprobanteConsolidadoResponse
{
    public required Guid VentaId { get; init; }
    public required Contracts.Cpe.ComprobanteResponse Comprobante { get; init; }
    public required IReadOnlyList<PedidoDigitalResponse> Pedidos { get; init; }
}
