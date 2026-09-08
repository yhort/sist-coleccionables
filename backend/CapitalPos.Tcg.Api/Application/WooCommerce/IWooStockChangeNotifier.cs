namespace CapitalPos.Tcg.Api.Application.WooCommerce;

public interface IWooStockChangeNotifier
{
    Task PublicarStockProductoAsync(Guid productoId, CancellationToken cancellationToken);
}
