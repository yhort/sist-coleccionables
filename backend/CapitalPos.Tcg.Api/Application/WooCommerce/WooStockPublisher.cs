namespace CapitalPos.Tcg.Api.Application.WooCommerce;

public sealed class WooStockPublisher(
    WooStockPublishQueue cola,
    IWooStockChangeNotifier notifier,
    ILogger<WooStockPublisher> logger)
{
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        foreach (var productoId in cola.Vaciar())
        {
            try
            {
                await notifier.PublicarStockProductoAsync(productoId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo publicar stock Woo del producto {ProductoId}", productoId);
            }
        }
    }
}
