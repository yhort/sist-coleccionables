namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Línea de producto en una subasta (Single Hit = 1×1, Bulk = 1×N, Combo = N SKUs).
/// </summary>
public class SubastaDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid SubastaTcgId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public int Orden { get; set; }

    public SubastaTcg Subasta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
