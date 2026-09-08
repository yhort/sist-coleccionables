using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class Pago : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public OrigenPago Origen { get; set; }
    public EstadoPago Estado { get; set; } = EstadoPago.NOTIFICADO;
    public decimal Monto { get; set; }
    public string? CodigoOperacion { get; set; }
    public string? ReferenciaExterna { get; set; }
    public Guid? PedidoDigitalId { get; set; }
    public Guid? VentaId { get; set; }
    public string? ClienteNombre { get; set; }
    public DateTimeOffset FechaNotificacion { get; set; }
    public DateTimeOffset? FechaConfirmacion { get; set; }
    public Guid? UsuarioAsocioId { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public PedidoDigital? Pedido { get; set; }
    public Venta? Venta { get; set; }
    public Usuario? UsuarioAsocio { get; set; }
}
