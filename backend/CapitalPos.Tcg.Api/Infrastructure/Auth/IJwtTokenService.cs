using CapitalPos.Tcg.Api.Domain.Entities;

namespace CapitalPos.Tcg.Api.Infrastructure.Auth;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) Create(Usuario usuario);
}
