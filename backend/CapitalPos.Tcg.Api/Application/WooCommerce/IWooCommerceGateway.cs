namespace CapitalPos.Tcg.Api.Application.WooCommerce;

public sealed record WooProductoRemoto(
    long Id,
    long? VariationId,
    string Sku,
    string Name,
    decimal RegularPrice,
    decimal? SalePrice,
    decimal? StockQuantity);

public sealed record WooPedidoLineaRemota(
    long ProductId,
    long VariationId,
    string Sku,
    int Quantity,
    decimal Price);

public sealed record WooPedidoRemoto(
    long Id,
    string Status,
    string ClienteNombre,
    string? ClienteTelefono,
    bool Pagado,
    string? Direccion,
    string? Distrito,
    IReadOnlyList<WooPedidoLineaRemota> Lineas);

public sealed record WooProductoUpsert(
    string Sku,
    string Name,
    string Description,
    decimal RegularPrice,
    decimal StockQuantity,
    IReadOnlyDictionary<string, string> Atributos,
    IReadOnlyList<string> Imagenes);

public interface IWooCommerceGateway
{
    Task ProbarConexionAsync(string urlTienda, string consumerKey, string consumerSecret, CancellationToken cancellationToken);

    Task<WooProductoRemoto?> BuscarProductoPorSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        string sku,
        CancellationToken cancellationToken);

    Task AlinearSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        string sku,
        CancellationToken cancellationToken);

    Task<WooProductoRemoto> CrearOActualizarProductoAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long? wooProductId,
        long? wooVariationId,
        WooProductoUpsert payload,
        CancellationToken cancellationToken);

    Task ActualizarStockAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        decimal stockLibre,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WooPedidoRemoto>> ListarPedidosPendientesAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken);
}
