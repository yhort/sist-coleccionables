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
        var credencial = (request.Email ?? string.Empty).Trim();
        if (credencial.Length == 0)
        {
            return Unauthorized(new { title = "Credenciales inválidas" });
        }

        var empresaId = await ResolverEmpresaAsync(request, credencial, cancellationToken);
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
        var usuario = await BuscarUsuarioPorCredencialAsync(credencial, cancellationToken);
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
                Dni = usuario.Dni,
                Nombres = usuario.Nombres,
                Apellidos = usuario.Apellidos,
                Email = usuario.Email,
                Rol = usuario.Rol
            }
        });
    }

    private async Task<Guid?> ResolverEmpresaAsync(
        LoginRequest request,
        string credencial,
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

        var lower = credencial.ToLowerInvariant();
        var esUsuarioLocal = !credencial.Contains('@', StringComparison.Ordinal);
        var prefijoUsuario = lower + "@";

        var coincidencias = await db.Usuarios.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.Activo && (
                u.Email.ToLower() == lower
                || u.Dni == credencial
                || (esUsuarioLocal && u.Email.ToLower().StartsWith(prefijoUsuario))))
            .Select(u => u.EmpresaId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return coincidencias.Count == 1 ? coincidencias[0] : null;
    }

    private async Task<Usuario?> BuscarUsuarioPorCredencialAsync(
        string credencial,
        CancellationToken cancellationToken)
    {
        // Preferencia: correo exacto → DNI → usuario (parte local del correo).
        var lower = credencial.ToLowerInvariant();
        var porEmail = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == lower, cancellationToken);
        if (porEmail is not null)
        {
            return porEmail;
        }

        var porDni = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Dni == credencial, cancellationToken);
        if (porDni is not null)
        {
            return porDni;
        }

        if (credencial.Contains('@', StringComparison.Ordinal))
        {
            return null;
        }

        // "katherinne" coincide con "katherinne@trunqi.local"
        var prefijo = lower + "@";
        return await db.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower().StartsWith(prefijo), cancellationToken);
    }
}
