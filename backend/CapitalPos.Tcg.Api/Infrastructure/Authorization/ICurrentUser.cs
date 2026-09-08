using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? EmpresaId { get; }
    string? Email { get; }
    string? Nombre { get; }
    RolUsuario? Rol { get; }
}
