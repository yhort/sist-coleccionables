using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Auth;

public sealed class LoginResponse
{
    public required string AccessToken { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required UsuarioAuthDto Usuario { get; init; }
}

public sealed class UsuarioAuthDto
{
    public required Guid Id { get; init; }
    public required Guid EmpresaId { get; init; }
    public required string Nombre { get; init; }
    public string? Dni { get; init; }
    public string? Nombres { get; init; }
    public string? Apellidos { get; init; }
    public required string Email { get; init; }
    public required RolUsuario Rol { get; init; }
}
