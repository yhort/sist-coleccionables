using CapitalPos.Tcg.Api.Contracts.Usuarios;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Usuarios;

public sealed class UsuariosService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    IPasswordHasher<Usuario> passwordHasher)
{
    public async Task<IReadOnlyList<UsuarioResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        var items = await db.Usuarios.AsNoTracking()
            .OrderBy(u => u.Apellidos)
            .ThenBy(u => u.Nombres)
            .ThenBy(u => u.Email)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<UsuarioResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        return usuario is null ? null : Map(usuario);
    }

    public async Task<UsuarioResponse> CrearAsync(
        CrearUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var dni = NormalizarDni(request.Dni);
        var nombres = NormalizarTexto(request.Nombres, "nombres");
        var apellidos = NormalizarTexto(request.Apellidos, "apellidos");
        var email = NormalizarEmail(request.Email);
        ValidarPassword(request.Password, obligatoria: true);
        ValidarRol(request.Rol);

        await AsegurarUnicidadAsync(null, dni, email, cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Dni = dni,
            Nombres = nombres,
            Apellidos = apellidos,
            Email = email,
            Rol = request.Rol,
            Activo = request.Activo,
            FechaCreacion = ahora
        };
        usuario.SincronizarNombreCompleto();
        usuario.PasswordHash = passwordHasher.HashPassword(usuario, request.Password);

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(cancellationToken);
        return Map(usuario);
    }

    public async Task<UsuarioResponse> ActualizarAsync(
        Guid id,
        ActualizarUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró el usuario.", StatusCodes.Status404NotFound);

        var dni = NormalizarDni(request.Dni);
        var nombres = NormalizarTexto(request.Nombres, "nombres");
        var apellidos = NormalizarTexto(request.Apellidos, "apellidos");
        var email = NormalizarEmail(request.Email);
        ValidarRol(request.Rol);
        await AsegurarUnicidadAsync(id, dni, email, cancellationToken);

        usuario.Dni = dni;
        usuario.Nombres = nombres;
        usuario.Apellidos = apellidos;
        usuario.Email = email;
        usuario.Rol = request.Rol;
        usuario.Activo = request.Activo;
        usuario.SincronizarNombreCompleto();

        var password = request.Password?.Trim() ?? string.Empty;
        if (password.Length > 0)
        {
            ValidarPassword(password, obligatoria: true);
            usuario.PasswordHash = passwordHasher.HashPassword(usuario, password);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(usuario);
    }

    private async Task AsegurarUnicidadAsync(
        Guid? usuarioId,
        string dni,
        string email,
        CancellationToken cancellationToken)
    {
        var emailTomado = await db.Usuarios.AnyAsync(
            u => u.Email == email && u.Id != usuarioId,
            cancellationToken);
        if (emailTomado)
        {
            throw new BusinessRuleException("Ya existe un usuario con ese correo / usuario de acceso.");
        }

        var dniTomado = await db.Usuarios.AnyAsync(
            u => u.Dni == dni && u.Id != usuarioId,
            cancellationToken);
        if (dniTomado)
        {
            throw new BusinessRuleException("Ya existe un usuario con ese DNI.");
        }
    }

    private static string NormalizarDni(string? valor)
    {
        var dni = (valor ?? string.Empty).Trim();
        if (dni.Length != 8 || !dni.All(char.IsDigit))
        {
            throw new BusinessRuleException("El DNI debe tener exactamente 8 dígitos.");
        }

        return dni;
    }

    private static string NormalizarEmail(string? valor)
    {
        var email = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (email.Length < 5 || !email.Contains('@'))
        {
            throw new BusinessRuleException("Indica un correo / usuario de acceso válido.");
        }

        return email[..Math.Min(email.Length, 200)];
    }

    private static string NormalizarTexto(string? valor, string campo)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length < 2)
        {
            throw new BusinessRuleException($"Indica {campo} (mínimo 2 caracteres).");
        }

        return texto[..Math.Min(texto.Length, 80)];
    }

    private static void ValidarPassword(string? password, bool obligatoria)
    {
        var valor = password?.Trim() ?? string.Empty;
        if (valor.Length == 0)
        {
            if (obligatoria)
            {
                throw new BusinessRuleException("La contraseña es obligatoria (mínimo 8 caracteres).");
            }

            return;
        }

        if (valor.Length < 8)
        {
            throw new BusinessRuleException("La contraseña debe tener al menos 8 caracteres.");
        }
    }

    private static void ValidarRol(RolUsuario rol)
    {
        if (!Enum.IsDefined(rol))
        {
            throw new BusinessRuleException("Rol de usuario no válido.");
        }
    }

    private static UsuarioResponse Map(Usuario u) => new()
    {
        Id = u.Id,
        Dni = u.Dni,
        Nombres = u.Nombres,
        Apellidos = u.Apellidos,
        Nombre = u.Nombre,
        Email = u.Email,
        Rol = u.Rol,
        Activo = u.Activo,
        FechaCreacion = u.FechaCreacion
    };
}
