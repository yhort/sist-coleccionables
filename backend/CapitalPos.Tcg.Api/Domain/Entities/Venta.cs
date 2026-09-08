using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Venta : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SedeId { get; set; }
    public Guid? ClienteId { get; set; }
    public Guid? PedidoDigitalId { get; set; }
    public CanalPedidoDigital Canal { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public Guid? UsuarioId { get; set; }
    public Guid? CajaSesionId { get; set; }
    public bool EsConsolidacion { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public Cliente? Cliente { get; set; }
    public PedidoDigital? Pedido { get; set; }
    public Usuario? Usuario { get; set; }
    public CajaSesion? CajaSesion { get; set; }
    public ICollection<VentaDetalle> Detalles { get; set; } = new List<VentaDetalle>();
    public ICollection<VentaPago> Pagos { get; set; } = new List<VentaPago>();
    public ICollection<Comprobante> Comprobantes { get; set; } = new List<Comprobante>();
}
