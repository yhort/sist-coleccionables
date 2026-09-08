using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Productos;

public sealed class UpsertProductoTcgRequest
{
    [Required]
    public TipoProducto TipoProducto { get; set; }

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string CodigoSku { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? CodigoBarras { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecioVenta { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Costo { get; set; }

    public Guid? CategoriaId { get; set; }
    public Guid? MarcaId { get; set; }

    /// <summary>URLs de imagen (CSV, ; o una por línea).</summary>
    public IReadOnlyList<string>? Imagenes { get; set; }

    public ProductoCartaRequest? Carta { get; set; }
    public ProductoSelladoRequest? Sellado { get; set; }

    /// <summary>Sede del stock inicial al crear (obligatoria si <see cref="StockInicial"/> &gt; 0).</summary>
    public Guid? SedeId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal StockInicial { get; set; }

    public TipoIngresoStockSku TipoIngresoStock { get; set; } = TipoIngresoStockSku.AJUSTE;
}

public sealed class ProductoCartaRequest
{
    [Required]
    [MaxLength(80)]
    public string Juego { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string SetCodigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string SetNombre { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string NumeroCarta { get; set; } = string.Empty;

    public RarezaTcg Rareza { get; set; }
    public IdiomaTcg Idioma { get; set; }
    public CondicionTcg Condicion { get; set; }
    public bool EsFoil { get; set; }

    [MaxLength(120)]
    public string? Artista { get; set; }
}

public sealed class ProductoSelladoRequest
{
    [Required]
    [MaxLength(80)]
    public string Juego { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Edicion { get; set; } = string.Empty;

    public TipoSellado TipoSellado { get; set; }

    [Range(0, int.MaxValue)]
    public int CartasEsperadas { get; set; }

    public bool PermiteApertura { get; set; } = true;

    public IReadOnlyList<ProductoSelladoContenidoFijoRequest>? ContenidoFijo { get; set; }
}

public sealed class ProductoSelladoContenidoFijoRequest
{
    [Required]
    public Guid ProductoId { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; } = 1;
}

public sealed class ProductoTcgResponse
{
    public required Guid Id { get; init; }
    public required TipoProducto TipoProducto { get; init; }
    public required string Nombre { get; init; }
    public required string CodigoSku { get; init; }
    public string? CodigoBarras { get; init; }
    public required decimal PrecioVenta { get; init; }
    public decimal? Costo { get; init; }
    public Guid? CategoriaId { get; init; }
    public Guid? MarcaId { get; init; }
    public required IReadOnlyList<string> Imagenes { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
    /// <summary>Stock libre en la sede del alta (solo informado al crear con stock inicial).</summary>
    public decimal? StockLibre { get; init; }
    public Guid? SedeStockId { get; init; }
    public ProductoCartaResponse? Carta { get; init; }
    public ProductoSelladoResponse? Sellado { get; init; }
}

public sealed class ProductoCartaResponse
{
    public Guid? CartaCatalogoId { get; init; }
    public required string Juego { get; init; }
    public required string SetCodigo { get; init; }
    public required string SetNombre { get; init; }
    public required string NumeroCarta { get; init; }
    public required RarezaTcg Rareza { get; init; }
    public required IdiomaTcg Idioma { get; init; }
    public required CondicionTcg Condicion { get; init; }
    public required bool EsFoil { get; init; }
    public string? Artista { get; init; }
}

public enum TipoIngresoStockSku
{
    AJUSTE,
    INGRESO_COMPRA
}

public sealed class CrearVarianteProductoCartaRequest
{
    [Required]
    public Guid CartaCatalogoId { get; set; }

    public bool EsFoil { get; set; }

    public CondicionTcg Condicion { get; set; }

    public IdiomaTcg Idioma { get; set; }

    [Range(0, double.MaxValue)]
    public decimal PrecioVenta { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Costo { get; set; }

    [MaxLength(64)]
    public string? CodigoSku { get; set; }

    [MaxLength(64)]
    public string? CodigoBarras { get; set; }

    public IReadOnlyList<string>? Imagenes { get; set; }

    public Guid? SedeId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal StockInicial { get; set; }

    public TipoIngresoStockSku TipoIngresoStock { get; set; } = TipoIngresoStockSku.AJUSTE;
}

public sealed class ProductoSelladoResponse
{
    public required string Juego { get; init; }
    public required string Edicion { get; init; }
    public required TipoSellado TipoSellado { get; init; }
    public required int CartasEsperadas { get; init; }
    public required bool PermiteApertura { get; init; }
    public required IReadOnlyList<ProductoSelladoContenidoFijoResponse> ContenidoFijo { get; init; }
}

public sealed class ProductoSelladoContenidoFijoResponse
{
    public required Guid ProductoId { get; init; }
    public required string Nombre { get; init; }
    public required string CodigoSku { get; init; }
    public required int Cantidad { get; init; }
}
