using CapitalPos.Tcg.Api.Application.WooCommerce;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

public sealed class WooCommerceGatewayRouter(
    LocalWooCommerceGateway local,
    RestWooCommerceGateway rest,
    IOptions<WooCommerceOptions> options) : IWooCommerceGateway
{
    private IWooCommerceGateway Activo =>
        string.Equals(options.Value.Modo, "Rest", StringComparison.OrdinalIgnoreCase) ? rest : local;

    public Task ProbarConexionAsync(string urlTienda, string consumerKey, string consumerSecret, CancellationToken cancellationToken) =>
        Activo.ProbarConexionAsync(urlTienda, consumerKey, consumerSecret, cancellationToken);

    public Task<WooProductoRemoto?> BuscarProductoPorSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        string sku,
        CancellationToken cancellationToken) =>
        Activo.BuscarProductoPorSkuAsync(urlTienda, consumerKey, consumerSecret, sku, cancellationToken);

    public Task AlinearSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        string sku,
        CancellationToken cancellationToken) =>
        Activo.AlinearSkuAsync(
            urlTienda, consumerKey, consumerSecret, wooProductId, wooVariationId, sku, cancellationToken);

    public Task<WooProductoRemoto> CrearOActualizarProductoAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long? wooProductId,
        long? wooVariationId,
        WooProductoUpsert payload,
        CancellationToken cancellationToken) =>
        Activo.CrearOActualizarProductoAsync(
            urlTienda, consumerKey, consumerSecret, wooProductId, wooVariationId, payload, cancellationToken);

    public Task ActualizarStockAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        decimal stockLibre,
        CancellationToken cancellationToken) =>
        Activo.ActualizarStockAsync(
            urlTienda, consumerKey, consumerSecret, wooProductId, wooVariationId, stockLibre, cancellationToken);

    public Task<IReadOnlyList<WooPedidoRemoto>> ListarPedidosPendientesAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken) =>
        Activo.ListarPedidosPendientesAsync(urlTienda, consumerKey, consumerSecret, cancellationToken);
}
