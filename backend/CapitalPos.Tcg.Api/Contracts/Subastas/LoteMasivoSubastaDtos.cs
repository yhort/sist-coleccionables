using System.ComponentModel.DataAnnotations;

namespace CapitalPos.Tcg.Api.Contracts.Subastas;

public sealed class CrearLoteMasivoSubastaRequest
{
    [Required]
    public Guid SedeId { get; set; }

    /// <summary>Inicio común del lote (UTC o con offset). Default: ahora.</summary>
    public DateTimeOffset? FechaInicio { get; set; }

    /// <summary>Hora de cierre del primer artículo. Si un ítem trae HoraFin, prevalece.</summary>
    public DateTimeOffset? HoraFinBase { get; set; }

    /// <summary>Minutos entre cierres escalonados cuando el ítem no define HoraFin. Default: 1.</summary>
    [Range(0, 120)]
    public int IntervaloMinutos { get; set; } = 1;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal IncrementoMinimo { get; set; } = 1m;

    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<LoteMasivoItemInput> Items { get; set; } = [];
}

public sealed class LoteMasivoItemInput
{
    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string Titulo { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal PrecioBase { get; set; }

    /// <summary>Cierre de este artículo. Si null, se calcula con HoraFinBase + (índice × IntervaloMinutos).</summary>
    public DateTimeOffset? HoraFin { get; set; }

    [MaxLength(500)]
    public string? Observaciones { get; set; }

    /// <summary>Imagen en data URL (data:image/jpeg;base64,...) o base64 puro.</summary>
    public string? ImagenBase64 { get; set; }

    [MaxLength(120)]
    public string? ImagenNombreArchivo { get; set; }

    [MaxLength(80)]
    public string? ImagenContentType { get; set; }
}

public sealed class LoteMasivoSubastaResponse
{
    public required string CodigoLote { get; init; }
    public required int TotalItems { get; init; }
    public required IReadOnlyList<LoteMasivoItemResponse> Items { get; init; }
}

public sealed class LoteMasivoItemResponse
{
    public required Guid SubastaId { get; init; }
    public required string CodigoSubasta { get; init; }
    public required string NumSubasta { get; init; }
    public required int NumArticulo { get; init; }
    public required string Titulo { get; init; }
    public required decimal PrecioBase { get; init; }
    public required DateTimeOffset FechaCierre { get; init; }
    public required string TextoFacebook { get; init; }
    public string? ImagenUrl { get; init; }
    public Guid? ProductoId { get; init; }
}
