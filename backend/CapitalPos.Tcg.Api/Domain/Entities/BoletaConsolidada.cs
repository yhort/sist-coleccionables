using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class BoletaConsolidada : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ComprobanteId { get; set; }
    public Guid VentaId { get; set; }
    public FiltroBoletaConsolidada Filtro { get; set; }
    public DateOnly FechaOperacion { get; set; }
    public decimal Total { get; set; }
    public int CantidadNotas { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Comprobante Comprobante { get; set; } = null!;
    public Venta Venta { get; set; } = null!;
    public ICollection<Comprobante> Notas { get; set; } = new List<Comprobante>();
}
