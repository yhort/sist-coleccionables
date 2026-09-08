using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Aperturas;

public sealed class CrearAperturaTcgRequest
{
    [Required]
    public Guid SedeId { get; set; }

    [Required]
    public Guid ProductoSelladoId { get; set; }

    [Range(1, int.MaxValue)]
    public int CantidadSellados { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class AperturaDetalleInput
{
    [Required]
    public Guid ProductoCartaId { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    public EstadoCartaObtenida Estado { get; set; } = EstadoCartaObtenida.NM;
    public bool EsFoil { get; set; }
}

public sealed class ReemplazarDetallesAperturaRequest
{
    [Required]
    public IReadOnlyList<AperturaDetalleInput> Detalles { get; set; } = [];
}

public sealed class RendimientoAperturaDto
{
    public required decimal CostoSellado { get; init; }
    public required decimal ValorEstimadoCartas { get; init; }
    public required decimal Diferencia { get; init; }
    public decimal? YieldPorcentaje { get; init; }
}

public sealed class AperturaTcgDetalleResponse
{
    public required Guid Id { get; init; }
    public required Guid ProductoCartaId { get; init; }
    public required string ProductoCartaNombre { get; init; }
    public required string CodigoSku { get; init; }
    public required int Cantidad { get; init; }
    public decimal? CostoUnitarioAsignado { get; init; }
    public required EstadoCartaObtenida Estado { get; init; }
    public required bool EsFoil { get; init; }
}

public sealed class AperturaTcgResponse
{
    public required Guid Id { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required Guid ProductoSelladoId { get; init; }
    public required string ProductoSelladoNombre { get; init; }
    public required string ProductoSelladoSku { get; init; }
    public required int CantidadSellados { get; init; }
    public required EstadoAperturaTcg Estado { get; init; }
    public required Guid UsuarioId { get; init; }
    public required string UsuarioNombre { get; init; }
    public string? Observacion { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
    public DateTimeOffset? FechaConfirmacion { get; init; }
    public required IReadOnlyList<AperturaTcgDetalleResponse> Detalles { get; init; }
    public required RendimientoAperturaDto Rendimiento { get; init; }
}
