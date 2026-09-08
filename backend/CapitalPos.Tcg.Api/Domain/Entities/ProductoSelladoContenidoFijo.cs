namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Ítem que siempre viene en el sellado al abrirlo (dados, fundas, promo, moneda).
/// </summary>
public class ProductoSelladoContenidoFijo : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public Guid ProductoSelladoId { get; set; }
    public Guid ProductoComponenteId { get; set; }
    public int Cantidad { get; set; }

    public ProductoSellado ProductoSellado { get; set; } = null!;
    public Producto ProductoComponente { get; set; } = null!;
}
