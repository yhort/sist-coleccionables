using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class WooCommerceMapeoProducto : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid IntegracionId { get; set; }
    public Guid ProductoId { get; set; }
    public long? WooProductId { get; set; }
    public long? WooVariationId { get; set; }
    public decimal PrecioNormalWoo { get; set; }
    public decimal? PrecioRebajadoWoo { get; set; }
    public decimal? StockWoo { get; set; }
    public EstadoMapeoWoo EstadoMapeo { get; set; } = EstadoMapeoWoo.PENDIENTE_SUBIDA;
    public string? Mensaje { get; set; }
    public DateTimeOffset? UltimaSincronizacion { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public IntegracionWooCommerce Integracion { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
