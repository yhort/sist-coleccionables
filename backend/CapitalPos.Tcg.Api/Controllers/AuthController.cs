using CapitalPos.Tcg.Api.Application.Permisos;
using CapitalPos.Tcg.Api.Contracts.Auth;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Infrastructure.Auth;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
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
    IJwtTokenService jwt,
    PermisosModuloService permisos,
    ICurrentUser currentUser) : ControllerBase
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
        var password = (request.Password ?? string.Empty).Trim();
        if (credencial.Length == 0 || password.Length == 0)
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
        var usuario = await BuscarUsuarioPorCredencialAsync(credencial, empresa.Id, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized(new { title = "Credenciales inválidas" });
        }

        var verificacion = passwordHasher.VerifyHashedPassword(usuario, usuario.PasswordHash, password);
        if (verificacion == PasswordVerificationResult.Failed)
        {
            // En Development, si el hash quedó corrupto/truncado, repara el admin del seed.
            if (!await IntentarRepararPasswordAdminDevAsync(usuario, password, cancellationToken))
            {
                return Unauthorized(new { title = "Credenciales inválidas" });
            }
        }
        else if (verificacion == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.PasswordHash = passwordHasher.HashPassword(usuario, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        var (token, expiresAt) = jwt.Create(usuario);
        var permisosRol = await permisos.ObtenerParaRolAsync(usuario.Rol, cancellationToken);
        return Ok(new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            Usuario = MapUsuario(usuario),
            Permisos = permisosRol
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<SesionActualResponse>> Me(CancellationToken cancellationToken)
    {
        // TenantHeaderMiddleware omite /api/auth*; sin EmpresaId el query filter de Usuarios
        // oculta al usuario y este endpoint devolvía 401 tras un login válido.
        if (currentUser.UserId is null || currentUser.EmpresaId is null)
        {
            return Unauthorized();
        }

        tenant.SetEmpresa(currentUser.EmpresaId.Value);

        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == currentUser.UserId.Value, cancellationToken);
        if (usuario is null || !usuario.Activo)
        {
            return Unauthorized();
        }

        var permisosRol = await permisos.ObtenerParaRolAsync(usuario.Rol, cancellationToken);
        return Ok(new SesionActualResponse
        {
            Usuario = MapUsuario(usuario),
            Permisos = permisosRol
        });
    }

    private static UsuarioAuthDto MapUsuario(Usuario usuario) => new()
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        Nombre = usuario.Nombre,
        Dni = usuario.Dni,
        Nombres = usuario.Nombres,
        Apellidos = usuario.Apellidos,
        Email = usuario.Email,
        Rol = usuario.Rol
    };

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
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        // Preferencia: correo exacto → DNI → usuario (parte local del correo).
        // IgnoreQueryFilters + EmpresaId evita falsos 401 por tenant aún no propagado.
        var lower = credencial.ToLowerInvariant();
        var query = db.Usuarios.IgnoreQueryFilters().Where(u => u.EmpresaId == empresaId);

        var porEmail = await query
            .FirstOrDefaultAsync(u => u.Email.ToLower() == lower, cancellationToken);
        if (porEmail is not null)
        {
            return porEmail;
        }

        var porDni = await query
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
        return await query
            .FirstOrDefaultAsync(u => u.Email.ToLower().StartsWith(prefijo), cancellationToken);
    }

    /// <summary>
    /// Solo Development: si la contraseña coincide con DevelopmentSeed:AdminPassword
    /// y el email con DevelopmentSeed:AdminEmail, regenera el hash y permite el login.
    /// </summary>
    private async Task<bool> IntentarRepararPasswordAdminDevAsync(
        Usuario usuario,
        string password,
        CancellationToken cancellationToken)
    {
        var env = HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (!env.IsDevelopment())
        {
            return false;
        }

        var config = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!config.GetValue("DevelopmentSeed:Enabled", false))
        {
            return false;
        }

        var adminEmail = (config["DevelopmentSeed:AdminEmail"] ?? "admin@trunqi.local").Trim();
        var adminPassword = config["DevelopmentSeed:AdminPassword"] ?? "Admin123!";
        if (!string.Equals(usuario.Email, adminEmail, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(password, adminPassword, StringComparison.Ordinal))
        {
            return false;
        }

        usuario.Activo = true;
        usuario.Rol = Domain.Enums.RolUsuario.ADMIN;
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, adminPassword);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
