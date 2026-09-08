namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Era / serie de un juego (ej. Megaevolución, Escarlata y Púrpura). Sin stock.
/// </summary>
public class TcgSerie : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public string Juego { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;

    public Empresa Empresa { get; set; } = null!;
    public ICollection<TcgSet> Sets { get; set; } = new List<TcgSet>();
}
