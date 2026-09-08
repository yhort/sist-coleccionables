namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Expansión / set (ej. ME03 Equilibrio Perfecto). Sin stock.
/// </summary>
public class TcgSet : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SerieId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? NombreEn { get; set; }
    public string? CodigoImpresion { get; set; }
    public int TotalCartas { get; set; }
    public DateTimeOffset? FechaLanzamiento { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public TcgSerie Serie { get; set; } = null!;
    public ICollection<TcgCarta> Cartas { get; set; } = new List<TcgCarta>();
}
