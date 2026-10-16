using CapitalPos.Tcg.Api.Contracts.Permisos;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Permisos;

public sealed class PermisosModuloService(
    ApplicationDbContext db,
    ITenantProvider tenant)
{
    public async Task<IReadOnlyList<PermisoModuloDto>> ObtenerParaRolAsync(
        RolUsuario rol,
        CancellationToken cancellationToken)
    {
        await AsegurarMatrizEmpresaAsync(cancellationToken);
        var items = await db.RolModuloPermisos.AsNoTracking()
            .Where(p => p.Rol == rol)
            .OrderBy(p => p.Modulo)
            .ToListAsync(cancellationToken);
        return CompletarModulos(rol, items);
    }

    public async Task<MatrizPermisosResponse> ObtenerMatrizAsync(CancellationToken cancellationToken)
    {
        await AsegurarMatrizEmpresaAsync(cancellationToken);
        var items = await db.RolModuloPermisos.AsNoTracking()
            .OrderBy(p => p.Rol)
            .ThenBy(p => p.Modulo)
            .ToListAsync(cancellationToken);

        return new MatrizPermisosResponse
        {
            Admin = CompletarModulos(RolUsuario.ADMIN, items.Where(p => p.Rol == RolUsuario.ADMIN)),
            Cajero = CompletarModulos(RolUsuario.CAJERO, items.Where(p => p.Rol == RolUsuario.CAJERO)),
            Almacen = CompletarModulos(RolUsuario.ALMACEN, items.Where(p => p.Rol == RolUsuario.ALMACEN))
        };
    }

    public async Task<MatrizPermisosResponse> GuardarMatrizAsync(
        GuardarMatrizPermisosRequest request,
        CancellationToken cancellationToken)
    {
        await AsegurarMatrizEmpresaAsync(cancellationToken);
        var existentes = await db.RolModuloPermisos.ToListAsync(cancellationToken);
        UpsertRol(existentes, RolUsuario.ADMIN, request.Admin);
        UpsertRol(existentes, RolUsuario.CAJERO, request.Cajero);
        UpsertRol(existentes, RolUsuario.ALMACEN, request.Almacen);
        await db.SaveChangesAsync(cancellationToken);
        return await ObtenerMatrizAsync(cancellationToken);
    }

    public async Task<bool> TieneAsync(
        RolUsuario rol,
        string modulo,
        bool requiereEscritura,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(modulo))
        {
            return false;
        }

        await AsegurarMatrizEmpresaAsync(cancellationToken);
        var permiso = await db.RolModuloPermisos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Rol == rol && p.Modulo == modulo, cancellationToken);

        if (permiso is null)
        {
            var lectura = MatrizPermisosDefaults.LecturaPorDefecto(rol, modulo);
            var escritura = MatrizPermisosDefaults.EscrituraPorDefecto(rol, modulo);
            return requiereEscritura ? escritura : lectura;
        }

        if (requiereEscritura)
        {
            return permiso.Escritura;
        }

        return permiso.Lectura || permiso.Escritura;
    }

    public async Task AsegurarMatrizEmpresaAsync(CancellationToken cancellationToken)
    {
        var empresaId = tenant.EmpresaId;
        if (empresaId == Guid.Empty)
        {
            return;
        }

        var existentes = await db.RolModuloPermisos
            .Where(p => p.EmpresaId == empresaId)
            .Select(p => new { p.Rol, p.Modulo })
            .ToListAsync(cancellationToken);
        var keys = existentes.Select(p => (p.Rol, p.Modulo)).ToHashSet();
        var faltantes = new List<RolModuloPermiso>();

        foreach (var rol in Enum.GetValues<RolUsuario>())
        {
            foreach (var modulo in ModulosSistema.Todos)
            {
                if (keys.Contains((rol, modulo)))
                {
                    continue;
                }

                faltantes.Add(new RolModuloPermiso
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = empresaId,
                    Rol = rol,
                    Modulo = modulo,
                    Lectura = MatrizPermisosDefaults.LecturaPorDefecto(rol, modulo),
                    Escritura = MatrizPermisosDefaults.EscrituraPorDefecto(rol, modulo)
                });
            }
        }

        if (faltantes.Count == 0)
        {
            return;
        }

        db.RolModuloPermisos.AddRange(faltantes);
        await db.SaveChangesAsync(cancellationToken);
    }

    private void UpsertRol(
        List<RolModuloPermiso> existentes,
        RolUsuario rol,
        IEnumerable<PermisoModuloItemRequest> items)
    {
        var porModulo = items
            .Where(i => !string.IsNullOrWhiteSpace(i.Modulo))
            .GroupBy(i => i.Modulo.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

        foreach (var modulo in ModulosSistema.Todos)
        {
            var lectura = true;
            var escritura = MatrizPermisosDefaults.EscrituraPorDefecto(rol, modulo);
            if (porModulo.TryGetValue(modulo, out var req))
            {
                escritura = req.Escritura;
                lectura = req.Lectura || escritura;
            }

            var actual = existentes.FirstOrDefault(p => p.Rol == rol && p.Modulo == modulo);
            if (actual is null)
            {
                var creado = new RolModuloPermiso
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = tenant.EmpresaId,
                    Rol = rol,
                    Modulo = modulo,
                    Lectura = lectura,
                    Escritura = escritura
                };
                db.RolModuloPermisos.Add(creado);
                existentes.Add(creado);
            }
            else
            {
                actual.Lectura = lectura;
                actual.Escritura = escritura;
            }
        }
    }

    private static IReadOnlyList<PermisoModuloDto> CompletarModulos(
        RolUsuario rol,
        IEnumerable<RolModuloPermiso> items)
    {
        var map = items.ToDictionary(p => p.Modulo, StringComparer.Ordinal);
        return ModulosSistema.Todos.Select(modulo =>
        {
            if (map.TryGetValue(modulo, out var permiso))
            {
                return new PermisoModuloDto
                {
                    Modulo = modulo,
                    Lectura = permiso.Lectura || permiso.Escritura,
                    Escritura = permiso.Escritura
                };
            }

            return new PermisoModuloDto
            {
                Modulo = modulo,
                Lectura = MatrizPermisosDefaults.LecturaPorDefecto(rol, modulo),
                Escritura = MatrizPermisosDefaults.EscrituraPorDefecto(rol, modulo)
            };
        }).ToList();
    }
}
