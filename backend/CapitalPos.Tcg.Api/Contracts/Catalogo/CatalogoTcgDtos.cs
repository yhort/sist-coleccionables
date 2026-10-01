using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Catalogo;

public sealed class UpsertTcgSerieRequest
{
    [Required]
    [MaxLength(80)]
    public string Juego { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activa { get; set; } = true;
}

public sealed class UpsertTcgSetRequest
{
    [Required]
    public Guid SerieId { get; set; }

    [Required]
    [MaxLength(32)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? NombreEn { get; set; }

    [MaxLength(32)]
    public string? CodigoImpresion { get; set; }

    [Range(0, int.MaxValue)]
    public int TotalCartas { get; set; }

    public DateTimeOffset? FechaLanzamiento { get; set; }
}

/// <summary>
/// Edición de set/serie. Los nombres siempre se pueden cambiar.
/// <see cref="CodigoSerie"/> / <see cref="CodigoSet"/> solo si el set aún no tiene fichas ni SKUs.
/// </summary>
public sealed class ActualizarTcgSetRequest
{
    [Required]
    [MaxLength(120)]
    public string NombreSerie { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string NombreSet { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? NombreEn { get; set; }

    [MaxLength(32)]
    public string? CodigoSerie { get; set; }

    [MaxLength(32)]
    public string? CodigoSet { get; set; }
}

public sealed class UpsertTcgCartaRequest
{
    [Required]
    [MaxLength(16)]
    public string Numero { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Nombre { get; set; } = string.Empty;

    public TipoCartaTcg TipoCarta { get; set; }

    public RarezaTcg Rareza { get; set; }

    [MaxLength(120)]
    public string? Artista { get; set; }

    [MaxLength(500)]
    public string? ImagenOficialUrl { get; set; }
}

public sealed class ImportarSetTcgRequest
{
    [Required]
    public UpsertTcgSerieRequest Serie { get; set; } = new();

    [Required]
    public ImportarTcgSetRequest Set { get; set; } = new();

    [Required]
    [MinLength(1)]
    public List<UpsertTcgCartaRequest> Cartas { get; set; } = [];
}

public sealed class ImportarTcgSetRequest
{
    [Required]
    [MaxLength(32)]
    public string Codigo { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? NombreEn { get; set; }

    [MaxLength(32)]
    public string? CodigoImpresion { get; set; }

    [Range(0, int.MaxValue)]
    public int TotalCartas { get; set; }

    public DateTimeOffset? FechaLanzamiento { get; set; }
}

public sealed class TcgSerieResponse
{
    public required Guid Id { get; init; }
    public required string Juego { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public required bool Activa { get; init; }
}

public sealed class TcgSetResponse
{
    public required Guid Id { get; init; }
    public required Guid SerieId { get; init; }
    public required string SerieCodigo { get; init; }
    public required string SerieNombre { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public string? NombreEn { get; init; }
    public string? CodigoImpresion { get; init; }
    public required int TotalCartas { get; init; }
    public DateTimeOffset? FechaLanzamiento { get; init; }
    /// <summary>Fichas oficiales del set.</summary>
    public int CartasCount { get; init; }
    /// <summary>SKUs (variantes) vinculados a fichas del set.</summary>
    public int SkusCount { get; init; }
    /// <summary>True si hay fichas o SKUs: no se pueden cambiar CodigoSerie / CodigoSet.</summary>
    public bool CodigosBloqueados { get; init; }
}

public sealed class TcgCartaResponse
{
    public required Guid Id { get; init; }
    public required Guid SetId { get; init; }
    public required string SetCodigo { get; init; }
    public required string SetNombre { get; init; }
    public required string Numero { get; init; }
    public required string Nombre { get; init; }
    public required TipoCartaTcg TipoCarta { get; init; }
    public required RarezaTcg Rareza { get; init; }
    public string? Artista { get; init; }
    public string? ImagenOficialUrl { get; init; }
}

public sealed class ImportarSetTcgResponse
{
    public required TcgSerieResponse Serie { get; init; }
    public required TcgSetResponse Set { get; init; }
    public required int CartasCreadas { get; init; }
    public required int CartasActualizadas { get; init; }
    public required IReadOnlyList<TcgCartaResponse> Cartas { get; init; }
}
