using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class ProductoCarta : Producto
{
    public Guid? CartaCatalogoId { get; set; }
    public string Juego { get; set; } = string.Empty;
    public string SetCodigo { get; set; } = string.Empty;
    public string SetNombre { get; set; } = string.Empty;
    public string NumeroCarta { get; set; } = string.Empty;
    public RarezaTcg Rareza { get; set; }
    public IdiomaTcg Idioma { get; set; }
    public CondicionTcg Condicion { get; set; }
    public bool EsFoil { get; set; }
    public string? Artista { get; set; }

    public TcgCarta? CartaCatalogo { get; set; }
}
