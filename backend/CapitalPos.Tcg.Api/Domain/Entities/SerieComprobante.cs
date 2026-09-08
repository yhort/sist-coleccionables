using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class SerieComprobante : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public TipoComprobanteSunat Tipo { get; set; }
    public string Serie { get; set; } = string.Empty;
    public int Correlativo { get; set; }
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
