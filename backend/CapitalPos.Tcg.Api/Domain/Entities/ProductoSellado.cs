using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class ProductoSellado : Producto
{
    public string Juego { get; set; } = string.Empty;
    public string Edicion { get; set; } = string.Empty;
    public TipoSellado TipoSellado { get; set; }
    public int CartasEsperadas { get; set; }
    public bool PermiteApertura { get; set; } = true;

    public ICollection<ProductoSelladoContenidoFijo> ContenidoFijo { get; set; } = new List<ProductoSelladoContenidoFijo>();
}
