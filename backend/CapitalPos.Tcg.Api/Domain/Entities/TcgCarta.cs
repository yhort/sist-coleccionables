using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Ficha de catálogo (ej. ME03 #047 Mega Zygarde ex Ultra Rara). Sin stock ni precio.
/// </summary>
public class TcgCarta : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid SetId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TipoCartaTcg TipoCarta { get; set; }
    public RarezaTcg Rareza { get; set; }
    public string? Artista { get; set; }
    public string? ImagenOficialUrl { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public TcgSet Set { get; set; } = null!;
    public ICollection<ProductoCarta> Productos { get; set; } = new List<ProductoCarta>();
}
