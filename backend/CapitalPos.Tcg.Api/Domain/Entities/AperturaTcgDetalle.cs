using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class AperturaTcgDetalle : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid AperturaTcgId { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProductoCartaId { get; set; }
    public int Cantidad { get; set; }
    public decimal? CostoUnitarioAsignado { get; set; }
    public EstadoCartaObtenida Estado { get; set; }
    public bool EsFoil { get; set; }

    public AperturaTcg Apertura { get; set; } = null!;
    public ProductoCarta ProductoCarta { get; set; } = null!;
}
