using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Proveedores;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Proveedores;

public sealed class ProveedoresService(ApplicationDbContext db, ITenantProvider tenant)
{
    public async Task<IReadOnlyList<ProveedorResponse>> ListarAsync(
        string? q,
        bool? activo,
        CancellationToken cancellationToken)
    {
        var query = db.Proveedores.AsNoTracking().AsQueryable();
        if (activo.HasValue)
        {
            query = query.Where(p => p.Activo == activo.Value);
        }

        var filtro = q?.Trim() ?? string.Empty;
        if (filtro.Length > 0)
        {
            var like = filtro.ToLower();
            query = query.Where(p =>
                p.RazonSocial.ToLower().Contains(like)
                || (p.NombreComercial != null && p.NombreComercial.ToLower().Contains(like))
                || p.Ruc.Contains(filtro));
        }

        var items = await query
            .OrderBy(p => p.RazonSocial)
            .Take(200)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<ProveedorResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var proveedor = await db.Proveedores.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return proveedor is null ? null : Map(proveedor);
    }

    public async Task<ProveedorResponse> CrearAsync(
        UpsertProveedorRequest request,
        CancellationToken cancellationToken)
    {
        var ruc = DocumentoIdentidad.ValidarRucProveedor(request.Ruc);
        var razon = request.RazonSocial.Trim();
        if (razon.Length < 2)
        {
            throw new BusinessRuleException("Indica la razón social del proveedor.");
        }

        if (await db.Proveedores.AnyAsync(p => p.Ruc == ruc, cancellationToken))
        {
            throw new BusinessRuleException("Ya existe un proveedor con ese RUC.");
        }

        var proveedor = new Proveedor
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Ruc = ruc,
            RazonSocial = razon,
            NombreComercial = TextoOpcional(request.NombreComercial, 200),
            Telefono = TextoOpcional(request.Telefono, 32),
            Activo = request.Activo,
            FechaCreacion = DateTimeOffset.UtcNow
        };
        db.Proveedores.Add(proveedor);
        await db.SaveChangesAsync(cancellationToken);
        return Map(proveedor);
    }

    public async Task<ProveedorResponse?> ActualizarAsync(
        Guid id,
        UpsertProveedorRequest request,
        CancellationToken cancellationToken)
    {
        var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (proveedor is null)
        {
            return null;
        }

        var ruc = DocumentoIdentidad.ValidarRucProveedor(request.Ruc);
        var razon = request.RazonSocial.Trim();
        if (razon.Length < 2)
        {
            throw new BusinessRuleException("Indica la razón social del proveedor.");
        }

        if (await db.Proveedores.AnyAsync(p => p.Ruc == ruc && p.Id != id, cancellationToken))
        {
            throw new BusinessRuleException("Ya existe un proveedor con ese RUC.");
        }

        proveedor.Ruc = ruc;
        proveedor.RazonSocial = razon;
        proveedor.NombreComercial = TextoOpcional(request.NombreComercial, 200);
        proveedor.Telefono = TextoOpcional(request.Telefono, 32);
        proveedor.Activo = request.Activo;
        await db.SaveChangesAsync(cancellationToken);
        return Map(proveedor);
    }

    private static string? TextoOpcional(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    internal static ProveedorResponse Map(Proveedor proveedor) => new()
    {
        Id = proveedor.Id,
        Ruc = proveedor.Ruc,
        RazonSocial = proveedor.RazonSocial,
        NombreComercial = proveedor.NombreComercial,
        Telefono = proveedor.Telefono,
        Activo = proveedor.Activo,
        FechaCreacion = proveedor.FechaCreacion
    };
}
