namespace CapitalPos.Tcg.Api.Application.WooCommerce;

/// <summary>
/// Acumula productos cuyo CantidadLibre cambió en el request (Kardex)
/// para publicarlos a WooCommerce después de un HTTP 2xx.
/// </summary>
public sealed class WooStockPublishQueue
{
    private readonly HashSet<Guid> _productoIds = [];

    public void Encolar(Guid productoId)
    {
        if (productoId != Guid.Empty)
        {
            _productoIds.Add(productoId);
        }
    }

    public IReadOnlyList<Guid> Vaciar()
    {
        if (_productoIds.Count == 0)
        {
            return [];
        }

        var ids = _productoIds.ToArray();
        _productoIds.Clear();
        return ids;
    }
}
