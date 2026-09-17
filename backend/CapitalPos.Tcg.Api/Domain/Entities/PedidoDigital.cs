using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Pedido Kanban omnicanal. Reserva al crear (salvo adjudicación: ya cubierto por
/// <c>PUJA_GANADORA_RESERVA</c>). Entregado materializa <see cref="Venta"/> + CPE local.
/// </summary>
public class PedidoDigital : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid? ClienteId { get; set; }
    public string? ClienteNombre { get; set; }
    public string? ClienteTelefono { get; set; }
    public Guid SedeId { get; set; }
    public CanalPedidoDigital CanalPedido { get; set; }
    public EstadoPedidoDigital Estado { get; set; } = EstadoPedidoDigital.PendientePago;
    public IndicadorReservaPedido IndicadorReserva { get; set; } = IndicadorReservaPedido.Reservado;
    public DateTimeOffset FechaPedido { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? Observacion { get; set; }
    public Guid? SubastaTcgId { get; set; }
    public Guid? VentaId { get; set; }

    public string DestinatarioNombre { get; set; } = string.Empty;
    public string? DestinatarioTelefono { get; set; }
    public string? EntregaDireccion { get; set; }
    public string? EntregaDistrito { get; set; }
    public string? EntregaProvincia { get; set; }
    public string? EntregaDepartamento { get; set; }
    public string? Courier { get; set; }
    public bool EsRecojoTienda { get; set; } = true;
    public string? NumeroTracking { get; set; }
    public decimal CostoEnvio { get; set; }
    public string? NotasEmpaque { get; set; }
    public string? Agencia { get; set; }
    /// <summary>Punto de entrega de este pedido (puede diferir del preferido del cliente).</summary>
    public string? PuntoEntrega { get; set; }
    public CanalContactoCliente? CanalContacto { get; set; }
    /// <summary>Persona autorizada a recoger / contacto alternativo de este pedido.</summary>
    public string? ContactoReferencia { get; set; }

    /// <summary>True cuando se copió/envió el resumen de WhatsApp al cliente.</summary>
    public bool Notificado { get; set; }
    public DateTimeOffset? FechaNotificacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Cliente? Cliente { get; set; }
    public SubastaTcg? Subasta { get; set; }
    public Entrega? Entrega { get; set; }
    public ICollection<PedidoDigitalDetalle> Detalles { get; set; } = new List<PedidoDigitalDetalle>();
    public ICollection<PedidoDigitalHistorialEstado> Historial { get; set; } = new List<PedidoDigitalHistorialEstado>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
