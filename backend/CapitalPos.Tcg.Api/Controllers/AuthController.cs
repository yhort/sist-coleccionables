using CapitalPos.Tcg.Api.Contracts.Auth;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Infrastructure.Auth;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    ApplicationDbContext db,
    ITenantProvider tenant,
    IPasswordHasher<Usuario> passwordHasher,
    IJwtTokenService jwt) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("empresas")]
    public async Task<ActionResult<EmpresasDisponiblesResponse>> ListarEmpresas(
        CancellationToken cancellationToken)
    {
        var empresas = await db.Empresas.AsNoTracking()
            .Where(e => e.Activa)
            .OrderBy(e => e.NombreComercial)
            .ThenBy(e => e.RazonSocial)
            .Select(e => new EmpresaPublicaDto
            {
                Id = e.Id,
                NombreComercial = e.NombreComercial,
                RazonSocial = e.RazonSocial
            })
            .ToListAsync(cancellationToken);

        return Ok(new EmpresasDisponiblesResponse
        {
            Empresas = empresas,
            UnicoTenant = empresas.Count == 1
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var empresaId = await ResolverEmpresaAsync(request, email, cancellationToken);
        if (empresaId is null)
        {
            return BadRequest(new
            {
                title = "Empresa requerida",
                detail = "Selecciona la empresa por su nombre comercial o razón social."
            });
        }

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empresaId.Value, cancellationToken);
        if (empresa is null || !empresa.Activa)
        {
            return Unauthorized(new { title = "Credenciales inválidas" });
        }

        tenant.SetEmpresa(empresa.Id);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower(), cancellationToken);

        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { title = "Credenciales inválidas" });
        }

        var verificacion = passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, request.Password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { title = "Credenciales inválidas" });
        }

        var (token, expiresAt) = jwt.Create(usuario);
        return Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            Usuario = new UsuarioAuthDto
            {
                Id = usuario.Id,
                EmpresaId = usuario.EmpresaId,
                Nombre = usuario.Nombre,
                Email = usuario.Email,
                Rol = usuario.Rol
            }
        });
    }

    private async Task<Guid?> ResolverEmpresaAsync(
        LoginRequest request,
        string email,
        CancellationToken cancellationToken)
    {
        if (request.EmpresaId is { } fromBody && fromBody != Guid.Empty)
        {
            return fromBody;
        }

        if (Guid.TryParse(Request.Headers[TenantHeaderMiddleware.HeaderName], out var fromHeader)
            && fromHeader != Guid.Empty)
        {
            return fromHeader;
        }

        var activas = await db.Empresas.AsNoTracking()
            .Where(e => e.Activa)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        if (activas.Count == 1)
        {
            return activas[0];
        }

        var coincidencias = await db.Usuarios.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Activo && u.Email.ToLower() == email.ToLower())
            .Select(u => u.EmpresaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return coincidencias.Count == 1 ? coincidencias[0] : null;
    }
}
