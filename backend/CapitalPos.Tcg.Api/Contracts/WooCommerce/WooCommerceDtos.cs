using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.WooCommerce;

public sealed class GuardarWooConfigRequest
{
    [Required]
    [MaxLength(300)]
    public string UrlTienda { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ConsumerKey { get; set; }

    [MaxLength(200)]
    public string? ConsumerSecret { get; set; }

    [Required]
    public Guid SedeOrigenId { get; set; }

    public ModoSincronizacionWoo ModoSincronizacion { get; set; } = ModoSincronizacionWoo.MANUAL;

    public ModoRecepcionPedidosWoo ModoRecepcionPedidos { get; set; } = ModoRecepcionPedidosWoo.WEBHOOK;

    public bool Activa { get; set; } = true;
}

public sealed class WooConfigResponse
{
    public required Guid Id { get; init; }
    public required string UrlTienda { get; init; }
    public required string ConsumerKeyEnmascarada { get; init; }
    public required bool TieneSecret { get; init; }
    public required Guid SedeOrigenId { get; init; }
    public required string SedeOrigenNombre { get; init; }
    public required ModoSincronizacionWoo ModoSincronizacion { get; init; }
    public required ModoRecepcionPedidosWoo ModoRecepcionPedidos { get; init; }
    public required EstadoConexionWoo EstadoConexion { get; init; }
    public string? MensajeConexion { get; init; }
    public DateTimeOffset? UltimoIntentoConexion { get; init; }
    public required bool Activa { get; init; }
}

public sealed class WooMapeoInput
{
    [Required]
    public Guid ProductoId { get; set; }

    public long? WooProductId { get; set; }

    public long? WooVariationId { get; set; }
}

public sealed class WooMapeoResponse
{
    public required Guid Id { get; init; }
    public required Guid ProductoId { get; init; }
    public required string Nombre { get; init; }
    public required string Sku { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required decimal PrecioLocal { get; init; }
    public required decimal StockLocal { get; init; }
    public long? WooProductId { get; init; }
    public long? WooVariationId { get; init; }
    public required decimal PrecioNormalWoo { get; init; }
    public decimal? PrecioRebajadoWoo { get; init; }
    public decimal? StockWoo { get; init; }
    public required EstadoMapeoWoo EstadoMapeo { get; init; }
    public string? Mensaje { get; init; }
    public DateTimeOffset? UltimaSincronizacion { get; init; }
}

public sealed class WooSyncLogResponse
{
    public required Guid Id { get; init; }
    public required TipoSyncWoo Tipo { get; init; }
    public required ResultadoSyncWoo Estado { get; init; }
    public required string PayloadResumen { get; init; }
    public string? MensajeError { get; init; }
    public int Intentos { get; init; }
    public DateTimeOffset? ProximoReintento { get; init; }
    public required DateTimeOffset Fecha { get; init; }
}

public sealed class WooSyncCatalogoResponse
{
    public required int Enviados { get; init; }
    public required int Creados { get; init; }
    public required int Actualizados { get; init; }
    public required int Omitidos { get; init; }
    public required int Errores { get; init; }
}

public sealed class WooSyncStockResponse
{
    public required int Publicados { get; init; }
    public required int Omitidos { get; init; }
    public required int Errores { get; init; }
}

public sealed class WooSyncProductoResponse
{
    public required Guid ProductoId { get; init; }
    public required string Sku { get; init; }
    public required long WooProductId { get; init; }
    public long? WooVariationId { get; init; }
    public required decimal CantidadLibrePublicada { get; init; }
    public required DateTimeOffset FechaSincronizacion { get; init; }
}

public sealed class WooImportPedidosResponse
{
    public required int Importados { get; init; }
    public required int Omitidos { get; init; }
    public required int Errores { get; init; }
    public required IReadOnlyList<string> Referencias { get; init; }
}

public sealed class WooWebhookPedidoLinea
{
    [JsonPropertyName("product_id")]
    public long? ProductId { get; set; }

    [JsonPropertyName("variation_id")]
    public long? VariationId { get; set; }

    public string? Sku { get; set; }

    public int Quantity { get; set; }

    [JsonConverter(typeof(WooDecimalJsonConverter))]
    public decimal Price { get; set; }
}

public sealed class WooWebhookPedidoRequest
{
    public Guid EmpresaId { get; set; }
    public long Id { get; set; }
    public string? Status { get; set; }

    [JsonPropertyName("payment_method")]
    public string? PaymentMethod { get; set; }

    public WooWebhookBilling? Billing { get; set; }
    public WooWebhookShipping? Shipping { get; set; }

    [JsonPropertyName("line_items")]
    public IReadOnlyList<WooWebhookPedidoLinea> LineItems { get; set; } = [];
}

public sealed class WooWebhookBilling
{
    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }

    public string? Phone { get; set; }
}

public sealed class WooWebhookShipping
{
    [JsonPropertyName("address_1")]
    public string? Address1 { get; set; }

    public string? City { get; set; }
    public string? State { get; set; }
}

public sealed class WooWebhookProductoRequest
{
    public Guid EmpresaId { get; set; }
    public long Id { get; set; }
    public string? Sku { get; set; }

    [JsonPropertyName("regular_price")]
    public string? RegularPrice { get; set; }

    [JsonPropertyName("stock_quantity")]
    public int? StockQuantity { get; set; }

    [JsonPropertyName("variation_id")]
    public long? VariationId { get; set; }
}

internal sealed class WooDecimalJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var numero))
        {
            return numero;
        }

        if (reader.TokenType == JsonTokenType.String
            && decimal.TryParse(reader.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var texto))
        {
            return texto;
        }

        return 0;
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}
