using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Catálogo de empresa (TPT). El stock vive por sede, no aquí.
/// </summary>
public class Producto : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public TipoProducto TipoProducto { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string CodigoSku { get; set; } = string.Empty;
    public string? CodigoBarras { get; set; }
    public decimal PrecioVenta { get; set; }
    public decimal? Costo { get; set; }
    public Guid? CategoriaId { get; set; }
    public Guid? MarcaId { get; set; }
    /// <summary>URLs de galería (CSV o una por línea), usadas al publicar la ficha en WooCommerce.</summary>
    public string? Imagenes { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public ICollection<StockProducto> Stocks { get; set; } = new List<StockProducto>();
    public ICollection<MovimientoInventario> Movimientos { get; set; } = new List<MovimientoInventario>();
}
