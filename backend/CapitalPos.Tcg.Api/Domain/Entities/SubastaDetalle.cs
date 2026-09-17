using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Línea de producto en una subasta (Single Hit = 1×1, Bulk = 1×N, Combo = N SKUs,
/// o Evento individuales = N cartas independientes en la misma sala).
/// </summary>
public class SubastaDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid SubastaTcgId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProductoId { get; set; }
    public decimal Cantidad { get; set; }
    public int Orden { get; set; }
    /// <summary>Nombre visible en Live/Kanban/Checkout; si es null se usa el del catálogo.</summary>
    public string? TituloPersonalizado { get; set; }
    public EstadoSubastaDetalle Estado { get; set; } = EstadoSubastaDetalle.PENDIENTE;
    public Guid? PujaGanadoraId { get; set; }
    public Guid? PedidoDigitalId { get; set; }

    public SubastaTcg Subasta { get; set; } = null!;
    public Producto Producto { get; set; } = null!;
}
