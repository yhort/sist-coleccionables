using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Usuarios;

public sealed class UsuarioResponse
{
    public required Guid Id { get; init; }
    public required string Dni { get; init; }
    public required string Nombres { get; init; }
    public required string Apellidos { get; init; }
    public required string Nombre { get; init; }
    public required string Email { get; init; }
    public required RolUsuario Rol { get; init; }
    public required bool Activo { get; init; }
    public required DateTimeOffset FechaCreacion { get; init; }
}

public sealed class CrearUsuarioRequest
{
    [Required]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener 8 dígitos.")]
    public string Dni { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Apellidos { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public RolUsuario Rol { get; set; } = RolUsuario.CAJERO;

    public bool Activo { get; set; } = true;
}

public sealed class ActualizarUsuarioRequest
{
    [Required]
    [RegularExpression(@"^\d{8}$", ErrorMessage = "El DNI debe tener 8 dígitos.")]
    public string Dni { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Nombres { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Apellidos { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Opcional: si viene vacío no se cambia la contraseña.</summary>
    [MaxLength(100)]
    public string? Password { get; set; }

    [Required]
    public RolUsuario Rol { get; set; } = RolUsuario.CAJERO;

    public bool Activo { get; set; } = true;
}
