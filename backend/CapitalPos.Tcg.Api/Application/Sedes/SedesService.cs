using CapitalPos.Tcg.Api.Contracts.Sedes;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Sedes;

public sealed class SedesService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<SedeResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        var sedes = await db.Sedes.AsNoTracking()
            .OrderByDescending(s => s.EsAlmacenPrincipal)
            .ThenBy(s => s.Nombre)
            .ToListAsync(cancellationToken);

        return sedes.Select(sede => new SedeResponse
        {
            Id = sede.Id,
            Nombre = sede.Nombre,
            Tipo = sede.Tipo,
            Direccion = sede.Direccion,
            Distrito = sede.Distrito,
            Provincia = sede.Provincia,
            Departamento = sede.Departamento,
            Ubigeo = sede.Ubigeo,
            EsAlmacenPrincipal = sede.EsAlmacenPrincipal,
            Activa = sede.Activa
        }).ToList();
    }
}
