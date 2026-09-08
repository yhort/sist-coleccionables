using System.ComponentModel.DataAnnotations;

namespace CapitalPos.Tcg.Api.Contracts.Proveedores;

public sealed class UpsertProveedorRequest
{
    [Required]
    [MaxLength(11)]
    public string Ruc { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(200)]
    public string RazonSocial { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? NombreComercial { get; set; }

    [MaxLength(32)]
    public string? Telefono { get; set; }

    public bool Activo { get; set; } = true;
}

public sealed class ProveedorResponse
{
    public required Guid Id { get; init; }
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public string? Telefono { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}
