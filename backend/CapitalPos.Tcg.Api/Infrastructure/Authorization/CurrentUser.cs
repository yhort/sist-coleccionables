using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Auth;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

public sealed class CurrentUser : ICurrentUser
{
    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        IsAuthenticated = true;
        UserId = ParseGuid(principal, ClaimTypes.NameIdentifier, JwtRegisteredClaimNames.Sub);
        EmpresaId = ParseGuid(principal, AuthClaims.EmpresaId);
        Email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Email);
        Nombre = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? principal.FindFirstValue("name");
        if (Enum.TryParse<RolUsuario>(FindRoleValue(principal), ignoreCase: true, out var rol))
        {
            Rol = rol;
        }
    }

    public bool IsAuthenticated { get; }
    public Guid? UserId { get; }
    public Guid? EmpresaId { get; }
    public string? Email { get; }
    public string? Nombre { get; }
    public RolUsuario? Rol { get; }

    private static string? FindRoleValue(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Role)
        ?? principal.FindFirstValue("role")
        ?? principal.Claims
            .FirstOrDefault(c => c.Type.EndsWith("/identity/claims/role", StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static Guid? ParseGuid(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var value = principal.FindFirstValue(type);
            if (Guid.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
