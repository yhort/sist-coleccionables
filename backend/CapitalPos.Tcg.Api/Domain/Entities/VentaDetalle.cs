namespace CapitalPos.Tcg.Api.Domain.Entities;

public class VentaDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid VentaId { get; set; }
    public Guid ProductoId { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Igv { get; set; }
    public decimal Total { get; set; }
    public string CodigoAfectacionIgv { get; set; } = "10";

    public Venta Venta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
