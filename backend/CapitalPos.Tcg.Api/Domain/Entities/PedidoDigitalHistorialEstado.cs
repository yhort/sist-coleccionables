using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class PedidoDigitalHistorialEstado : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid PedidoDigitalId { get; set; }
    public Guid EmpresaId { get; set; }
    public EstadoPedidoDigital? EstadoAnterior { get; set; }
    public EstadoPedidoDigital EstadoNuevo { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string? Observacion { get; set; }

    public PedidoDigital Pedido { get; set; } = null!;
    public Usuario? Usuario { get; set; }
}
