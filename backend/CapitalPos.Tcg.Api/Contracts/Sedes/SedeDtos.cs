using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Sedes;

public sealed class UpsertSedeRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(120)]
    public string Nombre { get; set; } = string.Empty;

    public TipoSede Tipo { get; set; } = TipoSede.TIENDA;

    [MaxLength(300)]
    public string? Direccion { get; set; }

    [MaxLength(80)]
    public string? Distrito { get; set; }

    [MaxLength(80)]
    public string? Provincia { get; set; }

    [MaxLength(80)]
    public string? Departamento { get; set; }

    [MaxLength(6)]
    [RegularExpression(@"^\d{0,6}$", ErrorMessage = "El UBIGEO debe tener hasta 6 dígitos.")]
    public string? Ubigeo { get; set; }

    public bool EsPuntoPartidaGre { get; set; }

    public bool EsPuntoLlegadaGre { get; set; }

    public bool EsAlmacenPrincipal { get; set; }

    public bool Activa { get; set; } = true;
}

public sealed class SedeResponse
{
    public required Guid Id { get; init; }
    public required string Nombre { get; init; }
    public required TipoSede Tipo { get; init; }
    public string? Direccion { get; init; }
    public string? Distrito { get; init; }
    public string? Provincia { get; init; }
    public string? Departamento { get; init; }
    public string? Ubigeo { get; init; }
    public required bool EsPuntoPartidaGre { get; init; }
    public required bool EsPuntoLlegadaGre { get; init; }
    public required bool EsAlmacenPrincipal { get; init; }
    public required bool Activa { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
    /// <summary>True si tiene stock, movimientos, ventas, cajas u otros vínculos históricos.</summary>
    public required bool TieneDependencias { get; init; }
}

public sealed class EliminarSedeResponse
{
    public required Guid Id { get; init; }
    /// <summary>ELIMINADA (borrado físico) o DESACTIVADA (soft delete).</summary>
    public required string Accion { get; init; }
    public string? Motivo { get; init; }
    public SedeResponse? Sede { get; init; }
}
