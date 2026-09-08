using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Application.WooCommerce;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

public sealed class RestWooCommerceGateway(IHttpClientFactory httpFactory) : IWooCommerceGateway
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task ProbarConexionAsync(string urlTienda, string consumerKey, string consumerSecret, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(
            urlTienda, consumerKey, consumerSecret, HttpMethod.Get, "products?per_page=1", null, cancellationToken);
        if (respuesta.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("WooCommerce rechazó las credenciales (ck_ / cs_).");
        }

        respuesta.EnsureSuccessStatusCode();
    }

    public async Task<WooProductoRemoto?> BuscarProductoPorSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        string sku,
        CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(
            urlTienda,
            consumerKey,
            consumerSecret,
            HttpMethod.Get,
            $"products?sku={Uri.EscapeDataString(sku)}&per_page=10",
            null,
            cancellationToken);
        await AsegurarOk(respuesta, cancellationToken);
        var productos = await respuesta.Content.ReadFromJsonAsync<List<WooProductJson>>(Json, cancellationToken) ?? [];
        var producto = productos.SingleOrDefault(p => string.Equals(p.Sku, sku, StringComparison.OrdinalIgnoreCase));
        return producto is null ? null : MapProductoBuscado(producto);
    }

    public async Task AlinearSkuAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        string sku,
        CancellationToken cancellationToken)
    {
        var ruta = wooVariationId is { } variationId
            ? $"products/{wooProductId}/variations/{variationId}"
            : $"products/{wooProductId}";
        using var respuesta = await EnviarAsync(
            urlTienda,
            consumerKey,
            consumerSecret,
            HttpMethod.Put,
            ruta,
            new { sku },
            cancellationToken);
        await AsegurarOk(respuesta, cancellationToken);
    }

    public async Task<WooProductoRemoto> CrearOActualizarProductoAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long? wooProductId,
        long? wooVariationId,
        WooProductoUpsert payload,
        CancellationToken cancellationToken)
    {
        var stock = (int)Math.Round(payload.StockQuantity, 0, MidpointRounding.AwayFromZero);
        object body = wooVariationId is null
            ? CuerpoProductoSimple(payload, stock)
            : new
            {
                sku = payload.Sku,
                regular_price = payload.RegularPrice.ToString("0.00", CultureInfo.InvariantCulture),
                manage_stock = true,
                stock_quantity = stock,
                description = payload.Description,
                attributes = payload.Atributos.Select(a => new { name = a.Key, option = a.Value }).ToArray()
            };

        var ruta = wooProductId is { } productId
            ? wooVariationId is { } variationId
                ? $"products/{productId}/variations/{variationId}"
                : $"products/{productId}"
            : "products";
        var metodo = wooProductId is null ? HttpMethod.Post : HttpMethod.Put;

        using var respuesta = await EnviarAsync(urlTienda, consumerKey, consumerSecret, metodo, ruta, body, cancellationToken);
        await AsegurarOk(respuesta, cancellationToken);
        var remoto = await respuesta.Content.ReadFromJsonAsync<WooProductJson>(Json, cancellationToken)
            ?? throw new InvalidOperationException("WooCommerce no devolvió el producto.");
        return MapProductoActualizado(remoto, wooProductId, wooVariationId);
    }

    private static object CuerpoProductoSimple(WooProductoUpsert payload, int stock)
    {
        var regularPrice = payload.RegularPrice.ToString("0.00", CultureInfo.InvariantCulture);
        var atributos = payload.Atributos
            .Select(a => new { name = a.Key, options = new[] { a.Value }, visible = true, variation = false })
            .ToArray();
        var images = (payload.Imagenes ?? [])
            .Select(u => u.Trim())
            .Where(u => u.Length > 0)
            .Select(u => new { src = u })
            .ToArray();

        if (images.Length == 0)
        {
            return new
            {
                name = payload.Name,
                sku = payload.Sku,
                type = "simple",
                regular_price = regularPrice,
                manage_stock = true,
                stock_quantity = stock,
                description = payload.Description,
                attributes = atributos
            };
        }

        return new
        {
            name = payload.Name,
            sku = payload.Sku,
            type = "simple",
            regular_price = regularPrice,
            manage_stock = true,
            stock_quantity = stock,
            description = payload.Description,
            attributes = atributos,
            images
        };
    }

    /// <summary>
    /// Actualización rápida de inventario. No incluye <c>images</c>: WooCommerce re-descarga
    /// cada src y duplica archivos en la librería de medios.
    /// </summary>
    public async Task ActualizarStockAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        long wooProductId,
        long? wooVariationId,
        decimal stockLibre,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            manage_stock = true,
            stock_quantity = (int)Math.Round(stockLibre, 0, MidpointRounding.AwayFromZero)
        };
        var ruta = wooVariationId is { } variationId
            ? $"products/{wooProductId}/variations/{variationId}"
            : $"products/{wooProductId}";
        using var respuesta = await EnviarAsync(
            urlTienda, consumerKey, consumerSecret, HttpMethod.Put, ruta, body, cancellationToken);
        await AsegurarOk(respuesta, cancellationToken);
    }

    public async Task<IReadOnlyList<WooPedidoRemoto>> ListarPedidosPendientesAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(
            urlTienda,
            consumerKey,
            consumerSecret,
            HttpMethod.Get,
            "orders?status=pending,processing,on-hold&per_page=50",
            null,
            cancellationToken);
        await AsegurarOk(respuesta, cancellationToken);
        var pedidos = await respuesta.Content.ReadFromJsonAsync<List<WooOrderJson>>(Json, cancellationToken) ?? [];
        return pedidos.Select(MapPedido).ToList();
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        string urlTienda,
        string consumerKey,
        string consumerSecret,
        HttpMethod metodo,
        string ruta,
        object? body,
        CancellationToken cancellationToken)
    {
        var client = httpFactory.CreateClient("WooCommerce");
        HttpResponseMessage? ultima = null;
        var usarQuery = false;
        for (var intento = 1; intento <= 3; intento++)
        {
            ultima?.Dispose();
            var destino = usarQuery
                ? UriTienda(urlTienda, ConQueryCredenciales(ruta, consumerKey, consumerSecret))
                : UriTienda(urlTienda, ruta);
            using var request = new HttpRequestMessage(metodo, destino);
            if (!usarQuery)
            {
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            }

            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            ultima = await client.SendAsync(request, cancellationToken);
            if (!usarQuery
                && ultima.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden
                && intento < 3)
            {
                usarQuery = true;
                continue;
            }

            var transitorio = (int)ultima.StatusCode is 429 or >= 500 && intento < 3;
            if (!transitorio)
            {
                return ultima;
            }

            var espera = ultima.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(intento * 2);
            await Task.Delay(espera, cancellationToken);
        }

        return ultima!;
    }

    private static string ConQueryCredenciales(string ruta, string consumerKey, string consumerSecret)
    {
        var sep = ruta.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{ruta}{sep}consumer_key={Uri.EscapeDataString(consumerKey)}&consumer_secret={Uri.EscapeDataString(consumerSecret)}";
    }

    private static Uri UriTienda(string urlTienda, string ruta) =>
        new($"{urlTienda.TrimEnd('/')}/wp-json/wc/v3/{ruta.TrimStart('/')}");

    private static async Task AsegurarOk(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        if (respuesta.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            throw new UnauthorizedAccessException("WooCommerce rechazó las credenciales (ck_ / cs_).");
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"WooCommerce {(int)respuesta.StatusCode}: {cuerpo[..Math.Min(cuerpo.Length, 300)]}");
        }
    }

    private static WooProductoRemoto MapProductoActualizado(
        WooProductJson p,
        long? wooProductId,
        long? wooVariationId) =>
        wooVariationId is null
            ? new(p.Id, null, p.Sku ?? string.Empty, p.Name ?? string.Empty, ParseDec(p.RegularPrice), ParseDecN(p.SalePrice), p.StockQuantity)
            : new(wooProductId ?? p.ParentId ?? 0, p.Id, p.Sku ?? string.Empty, p.Name ?? string.Empty, ParseDec(p.RegularPrice), ParseDecN(p.SalePrice), p.StockQuantity);

    private static WooProductoRemoto MapProductoBuscado(WooProductJson p) =>
        p.ParentId is > 0
            ? new(p.ParentId.Value, p.Id, p.Sku ?? string.Empty, p.Name ?? string.Empty, ParseDec(p.RegularPrice), ParseDecN(p.SalePrice), p.StockQuantity)
            : new(p.Id, null, p.Sku ?? string.Empty, p.Name ?? string.Empty, ParseDec(p.RegularPrice), ParseDecN(p.SalePrice), p.StockQuantity);

    private static WooPedidoRemoto MapPedido(WooOrderJson o)
    {
        var nombre = $"{o.Billing?.FirstName} {o.Billing?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "Cliente WooCommerce";
        }

        var pagado = o.Status is "processing" or "completed";
        return new WooPedidoRemoto(
            o.Id,
            o.Status ?? "pending",
            nombre,
            o.Billing?.Phone,
            pagado,
            o.Shipping?.Address1,
            o.Shipping?.City,
            (o.LineItems ?? []).Select(l => new WooPedidoLineaRemota(l.ProductId, l.VariationId, l.Sku ?? string.Empty, l.Quantity, ParseDec(l.Price))).ToList());
    }

    private static decimal ParseDec(string? valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static decimal? ParseDecN(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : ParseDec(valor);

    private sealed class WooProductJson
    {
        public long Id { get; set; }
        public long? ParentId { get; set; }
        public string? Sku { get; set; }
        public string? Name { get; set; }
        public string? RegularPrice { get; set; }
        public string? SalePrice { get; set; }
        public decimal? StockQuantity { get; set; }
    }

    private sealed class WooOrderJson
    {
        public long Id { get; set; }
        public string? Status { get; set; }
        public WooBillingJson? Billing { get; set; }
        public WooShippingJson? Shipping { get; set; }

        [JsonPropertyName("line_items")]
        public List<WooLineJson>? LineItems { get; set; }
    }

    private sealed class WooBillingJson
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
    }

    private sealed class WooShippingJson
    {
        public string? Address1 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
    }

    private sealed class WooLineJson
    {
        public long ProductId { get; set; }
        public long VariationId { get; set; }
        public string? Sku { get; set; }
        public int Quantity { get; set; }
        public string? Price { get; set; }
    }
}
