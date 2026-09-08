namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

public sealed class WooCommerceOptions
{
    public const string SectionName = "WooCommerce";

    /// <summary>Local = mapeos y stock sin REST real. Rest = wp-json/wc/v3.</summary>
    public string Modo { get; set; } = "Local";

    public string? UrlTienda { get; set; }

    public string? ConsumerKey { get; set; }

    public string? ConsumerSecret { get; set; }
}

