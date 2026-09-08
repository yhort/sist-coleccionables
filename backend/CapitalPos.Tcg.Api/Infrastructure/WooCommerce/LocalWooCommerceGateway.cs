using System.Collections.Concurrent;
using System.Globalization;
using CapitalPos.Tcg.Api.Application.WooCommerce;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

/// <summary>
/// Gateway de desarrollo: asigna IDs Woo deterministas por SKU y recuerda stock publicado.
/// </summary>
public sealed class LocalWooCommerceGateway : IWooCommerceGateway
{
    private static readonly ConcurrentDictionary<string, WooProductoRemoto> Productos = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, WooPedidoRemoto> Pedidos = new(StringComparer.OrdinalIgnoreCase);

    public Task ProbarConexionAsync(string urlTienda, string consumerKey, string consumerSecret, CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        return Task.CompletedTask;
    }

    public Task<WooProductoRemoto?> BuscarProductoPorSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        string sku,
        CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        Productos.TryGetValue(Clave(urlTienda, sku), out var producto);
        return Task.FromResult(producto);
    }

    public Task AlinearSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        string sku,
        CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        var existente = Productos.Values.FirstOrDefault(p => p.Id == wooProductId && p.VariationId == wooVariationId);
        if (existente is not null)
        {
            Productos[Clave(urlTienda, sku)] = existente with { Sku = sku };
        }

        return Task.CompletedTask;
    }

    public Task<WooProductoRemoto> CrearOActualizarProductoAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long? wooProductId,
        long? wooVariationId,
        WooProductoUpsert payload,
        CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        var id = wooProductId ?? IdDesdeSku(payload.Sku);
        var remoto = new WooProductoRemoto(
            id,
            wooVariationId,
            payload.Sku,
            payload.Name,
            payload.RegularPrice,
            null,
            payload.StockQuantity);
        Productos[Clave(urlTienda, payload.Sku)] = remoto;
        return Task.FromResult(remoto);
    }

    public Task ActualizarStockAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        decimal stockLibre,
        CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        var existente = Productos.Values.FirstOrDefault(p => p.Id == wooProductId && p.VariationId == wooVariationId)
            ?? Productos.Values.FirstOrDefault(p => p.Id == wooProductId);
        if (existente is not null)
        {
            Productos[Clave(urlTienda, existente.Sku)] = existente with { StockQuantity = stockLibre, VariationId = wooVariationId };
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WooPedidoRemoto>> ListarPedidosPendientesAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken)
    {
        ValidarCredenciales(urlTienda, consumerKey, consumerSecret);
        IReadOnlyList<WooPedidoRemoto> lista = Pedidos.Values.ToList();
        return Task.FromResult(lista);
    }

    public static void RegistrarPedidoPrueba(WooPedidoRemoto pedido) =>
        Pedidos[$"woo-{pedido.Id}"] = pedido;

    private static void ValidarCredenciales(string urlTienda, string consumerKey, string consumerSecret)
    {
        if (string.IsNullOrWhiteSpace(urlTienda) || !Uri.TryCreate(urlTienda, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("La URL de la tienda WooCommerce no es válida.");
        }

        if (!consumerKey.StartsWith("ck_", StringComparison.OrdinalIgnoreCase)
            || !consumerSecret.StartsWith("cs_", StringComparison.OrdinalIgnoreCase)
            || consumerSecret.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || consumerSecret.Contains("wrong", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("WooCommerce rechazó las credenciales (ck_ / cs_).");
        }
    }

    private static long IdDesdeSku(string sku)
    {
        var hash = unchecked((uint)StringComparer.OrdinalIgnoreCase.GetHashCode(sku));
        return 10_000 + hash % 90_000;
    }

    private static string Clave(string url, string sku) =>
        $"{url.TrimEnd('/')}|{sku}".ToLower(CultureInfo.InvariantCulture);
}
