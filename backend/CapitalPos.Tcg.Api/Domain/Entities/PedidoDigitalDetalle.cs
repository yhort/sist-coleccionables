namespace CapitalPos.Tcg.Api.Domain.Entities;

public class PedidoDigitalDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid PedidoDigitalId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProductoId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }

    public PedidoDigital Pedido { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
