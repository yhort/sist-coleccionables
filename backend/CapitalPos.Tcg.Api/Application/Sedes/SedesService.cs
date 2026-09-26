using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Sedes;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Sedes;

public sealed class SedesService(ApplicationDbContext db, ITenantProvider tenant)
{
    public async Task<IReadOnlyList<SedeResponse>> ListarAsync(
        bool? activa,
        CancellationToken cancellationToken)
    {
        var query = db.Sedes.AsNoTracking().AsQueryable();
        if (activa.HasValue)
        {
            query = query.Where(s => s.Activa == activa.Value);
        }

        var sedes = await query
            .OrderByDescending(s => s.EsAlmacenPrincipal)
            .ThenBy(s => s.Nombre)
            .ToListAsync(cancellationToken);

        var deps = await CargarMapaDependenciasAsync(sedes.Select(s => s.Id).ToList(), cancellationToken);
        return sedes.Select(s => Map(s, deps.GetValueOrDefault(s.Id))).ToList();
    }

    public async Task<SedeResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sede is null)
        {
            return null;
        }

        var tiene = await TieneDependenciasAsync(id, cancellationToken);
        return Map(sede, tiene);
    }

    public async Task<SedeResponse> CrearAsync(UpsertSedeRequest request, CancellationToken cancellationToken)
    {
        var datos = Normalizar(request);
        await AsegurarNombreLibreAsync(datos.Nombre, excluirId: null, cancellationToken);

        if (datos.EsAlmacenPrincipal)
        {
            await LimpiarAlmacenPrincipalAsync(excluirId: null, cancellationToken);
        }

        var sede = new Sede
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = datos.Nombre,
            Tipo = datos.Tipo,
            Direccion = datos.Direccion,
            Distrito = datos.Distrito,
            Provincia = datos.Provincia,
            Departamento = datos.Departamento,
            Ubigeo = datos.Ubigeo,
            EsPuntoPartidaGre = datos.EsPuntoPartidaGre,
            EsPuntoLlegadaGre = datos.EsPuntoLlegadaGre,
            EsAlmacenPrincipal = datos.EsAlmacenPrincipal,
            Activa = datos.Activa,
            FechaCreacion = DateTimeOffset.UtcNow
        };
        db.Sedes.Add(sede);
        await db.SaveChangesAsync(cancellationToken);
        return Map(sede, tieneDependencias: false);
    }

    public async Task<SedeResponse?> ActualizarAsync(
        Guid id,
        UpsertSedeRequest request,
        CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sede is null)
        {
            return null;
        }

        var datos = Normalizar(request);
        await AsegurarNombreLibreAsync(datos.Nombre, id, cancellationToken);

        if (!datos.Activa && sede.Activa)
        {
            await AsegurarPuedeDesactivarAsync(id, cancellationToken);
        }

        if (datos.EsAlmacenPrincipal)
        {
            await LimpiarAlmacenPrincipalAsync(id, cancellationToken);
        }

        sede.Nombre = datos.Nombre;
        sede.Tipo = datos.Tipo;
        sede.Direccion = datos.Direccion;
        sede.Distrito = datos.Distrito;
        sede.Provincia = datos.Provincia;
        sede.Departamento = datos.Departamento;
        sede.Ubigeo = datos.Ubigeo;
        sede.EsPuntoPartidaGre = datos.EsPuntoPartidaGre;
        sede.EsPuntoLlegadaGre = datos.EsPuntoLlegadaGre;
        sede.EsAlmacenPrincipal = datos.EsAlmacenPrincipal && datos.Activa;
        sede.Activa = datos.Activa;
        await db.SaveChangesAsync(cancellationToken);

        var tiene = await TieneDependenciasAsync(id, cancellationToken);
        return Map(sede, tiene);
    }

    /// <summary>
    /// Si la sede tiene historial/dependencias → soft delete (Activa=false).
    /// Si está limpia → borrado físico.
    /// </summary>
    public async Task<EliminarSedeResponse?> EliminarODesactivarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sede is null)
        {
            return null;
        }

        var tieneDeps = await TieneDependenciasAsync(id, cancellationToken);
        if (tieneDeps)
        {
            await AsegurarPuedeDesactivarAsync(id, cancellationToken);
            sede.Activa = false;
            sede.EsAlmacenPrincipal = false;
            await db.SaveChangesAsync(cancellationToken);
            return new EliminarSedeResponse
            {
                Id = id,
                Accion = "DESACTIVADA",
                Motivo =
                    "La sede/almacén tiene stock, movimientos, ventas, cajas u otros registros asociados. " +
                    "Se desactivó para conservar el historial (no se eliminó físicamente).",
                Sede = Map(sede, tieneDependencias: true)
            };
        }

        await AsegurarPuedeDesactivarAsync(id, cancellationToken);
        db.Sedes.Remove(sede);
        await db.SaveChangesAsync(cancellationToken);
        return new EliminarSedeResponse
        {
            Id = id,
            Accion = "ELIMINADA",
            Motivo = "La sede/almacén no tenía dependencias y se eliminó de forma permanente.",
            Sede = null
        };
    }

    public async Task<SedeResponse?> ReactivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (sede is null)
        {
            return null;
        }

        sede.Activa = true;
        await db.SaveChangesAsync(cancellationToken);
        var tiene = await TieneDependenciasAsync(id, cancellationToken);
        return Map(sede, tiene);
    }

    public async Task<bool> TieneDependenciasAsync(Guid sedeId, CancellationToken cancellationToken)
    {
        var mapa = await CargarMapaDependenciasAsync([sedeId], cancellationToken);
        return mapa.GetValueOrDefault(sedeId);
    }

    private async Task<Dictionary<Guid, bool>> CargarMapaDependenciasAsync(
        IReadOnlyList<Guid> sedeIds,
        CancellationToken cancellationToken)
    {
        var result = sedeIds.ToDictionary(id => id, _ => false);
        if (sedeIds.Count == 0)
        {
            return result;
        }

        async Task MarcarSiExiste(IQueryable<Guid> query)
        {
            var ids = await query.Distinct().ToListAsync(cancellationToken);
            foreach (var id in ids)
            {
                result[id] = true;
            }
        }

        await MarcarSiExiste(db.StocksProductos.Where(s => sedeIds.Contains(s.SedeId)).Select(s => s.SedeId));
        await MarcarSiExiste(db.MovimientosInventario.Where(m => sedeIds.Contains(m.SedeId)).Select(m => m.SedeId));
        await MarcarSiExiste(db.Compras.Where(c => sedeIds.Contains(c.SedeId)).Select(c => c.SedeId));
        await MarcarSiExiste(db.Ventas.Where(v => sedeIds.Contains(v.SedeId)).Select(v => v.SedeId));
        await MarcarSiExiste(db.CajaSesiones.Where(c => sedeIds.Contains(c.SedeId)).Select(c => c.SedeId));
        await MarcarSiExiste(db.PedidosDigitales.Where(p => sedeIds.Contains(p.SedeId)).Select(p => p.SedeId));
        await MarcarSiExiste(db.Entregas.Where(e => sedeIds.Contains(e.SedeOrigenId)).Select(e => e.SedeOrigenId));
        await MarcarSiExiste(db.AperturasTcg.Where(a => sedeIds.Contains(a.SedeId)).Select(a => a.SedeId));
        await MarcarSiExiste(db.SubastasTcg.Where(s => sedeIds.Contains(s.SedeId)).Select(s => s.SedeId));
        await MarcarSiExiste(
            db.IntegracionesWooCommerce
                .Where(i => sedeIds.Contains(i.SedeOrigenId))
                .Select(i => i.SedeOrigenId));

        return result;
    }

    private async Task AsegurarPuedeDesactivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var activas = await db.Sedes.CountAsync(s => s.Activa && s.Id != id, cancellationToken);
        if (activas == 0)
        {
            throw new BusinessRuleException(
                "No se puede desactivar o eliminar la única sede/almacén activa. Crea otra sede activa primero.");
        }
    }

    private async Task AsegurarNombreLibreAsync(
        string nombre,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        var query = db.Sedes.Where(s => s.Nombre.ToLower() == nombre.ToLower());
        if (excluirId.HasValue)
        {
            query = query.Where(s => s.Id != excluirId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BusinessRuleException("Ya existe una sede o almacén con ese nombre.");
        }
    }

    private async Task LimpiarAlmacenPrincipalAsync(Guid? excluirId, CancellationToken cancellationToken)
    {
        var query = db.Sedes.Where(s => s.EsAlmacenPrincipal);
        if (excluirId.HasValue)
        {
            query = query.Where(s => s.Id != excluirId.Value);
        }

        var actuales = await query.ToListAsync(cancellationToken);
        foreach (var sede in actuales)
        {
            sede.EsAlmacenPrincipal = false;
        }
    }

    private static (string Nombre, TipoSede Tipo, string? Direccion, string? Distrito, string? Provincia,
        string? Departamento, string? Ubigeo, bool EsPuntoPartidaGre, bool EsPuntoLlegadaGre,
        bool EsAlmacenPrincipal, bool Activa) Normalizar(UpsertSedeRequest request)
    {
        var nombre = request.Nombre.Trim();
        if (nombre.Length < 2)
        {
            throw new BusinessRuleException("Indica el nombre de la sede o almacén.");
        }

        var ubigeo = request.Ubigeo?.Trim() ?? string.Empty;
        if (ubigeo.Length is > 0 and not 6)
        {
            throw new BusinessRuleException("El UBIGEO debe tener 6 dígitos.");
        }

        return (
            nombre,
            request.Tipo,
            TextoOpcional(request.Direccion, 300),
            TextoOpcional(request.Distrito, 80),
            TextoOpcional(request.Provincia, 80) ?? "Lima",
            TextoOpcional(request.Departamento, 80) ?? "Lima",
            ubigeo.Length == 0 ? null : ubigeo,
            request.EsPuntoPartidaGre,
            request.EsPuntoLlegadaGre,
            request.EsAlmacenPrincipal,
            request.Activa);
    }

    private static string? TextoOpcional(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    private static SedeResponse Map(Sede sede, bool tieneDependencias) => new()
    {
        Id = sede.Id,
        Nombre = sede.Nombre,
        Tipo = sede.Tipo,
        Direccion = sede.Direccion,
        Distrito = sede.Distrito,
        Provincia = sede.Provincia,
        Departamento = sede.Departamento,
        Ubigeo = sede.Ubigeo,
        EsPuntoPartidaGre = sede.EsPuntoPartidaGre,
        EsPuntoLlegadaGre = sede.EsPuntoLlegadaGre,
        EsAlmacenPrincipal = sede.EsAlmacenPrincipal,
        Activa = sede.Activa,
        FechaCreacion = sede.FechaCreacion,
        TieneDependencias = tieneDependencias
    };
}
