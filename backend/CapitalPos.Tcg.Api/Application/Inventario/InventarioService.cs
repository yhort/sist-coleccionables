using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Common;
using CapitalPos.Tcg.Api.Contracts.Inventario;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Inventario;

public sealed class InventarioService(
    ApplicationDbContext db,
    KardexWriter kardex,
    ITenantProvider tenant,
    ICurrentUser currentUser)
{
    public async Task<StockProductoResponse?> ObtenerStockAsync(
        Guid productoId,
        Guid sedeId,
        CancellationToken cancellationToken)
    {
        var producto = await db.Productos.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productoId, cancellationToken);
        var sede = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sedeId, cancellationToken);

        if (producto is null || sede is null)
        {
            return null;
        }

        var stock = await db.StocksProductos.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ProductoId == productoId && s.SedeId == sedeId, cancellationToken);

        return MapStock(stock, producto, sede);
    }

    public async Task<StockSedePagedResponse> ListarStockPorSedeAsync(
        Guid sedeId,
        string? q,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sedeId, cancellationToken)
            ?? throw new BusinessRuleException("No existe la sede.", StatusCodes.Status404NotFound);

        var productosQuery = db.Productos.AsNoTracking().Where(p => p.Activo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            productosQuery = productosQuery.Where(p =>
                EF.Functions.ILike(p.Nombre, $"%{term}%")
                || EF.Functions.ILike(p.CodigoSku, $"%{term}%"));
        }

        var total = await productosQuery.CountAsync(cancellationToken);
        var productoIds = productosQuery.Select(p => p.Id);
        var stockFiltro = db.StocksProductos.AsNoTracking()
            .Where(s => s.SedeId == sedeId && productoIds.Contains(s.ProductoId));
        var disponible = await stockFiltro.SumAsync(s => s.CantidadDisponible, cancellationToken);
        var reservado = await stockFiltro.SumAsync(s => s.CantidadReservada, cancellationToken);
        var libre = await stockFiltro.SumAsync(s => s.CantidadDisponible - s.CantidadReservada, cancellationToken);

        var ordenados = productosQuery.OrderBy(p => p.Nombre);
        int pagina;
        int tamano;
        List<Producto> productos;
        if (Paginacion.EstaActiva(page, pageSize))
        {
            (pagina, tamano) = Paginacion.Normalizar(page, pageSize, total);
            productos = await ordenados.Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(cancellationToken);
        }
        else
        {
            pagina = 1;
            tamano = Math.Max(total, 1);
            productos = await ordenados.ToListAsync(cancellationToken);
        }

        var idsPagina = productos.Select(p => p.Id).ToList();
        var stocks = await db.StocksProductos.AsNoTracking()
            .Where(s => s.SedeId == sedeId && idsPagina.Contains(s.ProductoId))
            .ToListAsync(cancellationToken);
        var porProducto = stocks.ToDictionary(s => s.ProductoId);

        return new StockSedePagedResponse
        {
            Items = productos
                .Select(producto => MapStock(porProducto.GetValueOrDefault(producto.Id), producto, sede))
                .ToList(),
            Total = total,
            Page = pagina,
            PageSize = tamano,
            Resumen = new StockResumenResponse
            {
                Skus = total,
                Disponible = disponible,
                Reservado = reservado,
                Libre = libre
            }
        };
    }

    public async Task<StockProductoResponse> AjustarAsync(
        AjustarStockRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad debe ser mayor que cero.");
        }

        var motivo = request.Motivo.Trim();
        if (motivo.Length < 3)
        {
            throw new BusinessRuleException("Indica un motivo o justificación.");
        }

        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == request.ProductoId, cancellationToken)
            ?? throw new BusinessRuleException("No existe el producto.", StatusCodes.Status404NotFound);

        if (!producto.Activo)
        {
            throw new BusinessRuleException("El producto está inactivo; reactívalo en el catálogo para ajustar stock.");
        }

        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
            ?? throw new BusinessRuleException("No existe la sede.", StatusCodes.Status404NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await kardex.AplicarAsync(
            new KardexComando(
                sede.Id,
                producto.Id,
                TipoMovimientoInventario.AJUSTE,
                request.Cantidad,
                motivo,
                "AJUSTE_MANUAL",
                ReferenciaId: null,
                Sentido: request.Sentido),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var stock = await db.StocksProductos
            .FirstAsync(s => s.ProductoId == producto.Id && s.SedeId == sede.Id, cancellationToken);
        await db.Entry(stock).ReloadAsync(cancellationToken);
        return MapStock(stock, producto, sede);
    }

    public async Task<IReadOnlyList<KardexMovimientoResponse>> ListarKardexAsync(
        Guid? productoId,
        Guid? sedeId,
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        TipoMovimientoInventario? tipoMovimiento,
        CancellationToken cancellationToken)
    {
        var query = db.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Producto)
            .Include(m => m.Sede)
            .Include(m => m.Usuario)
            .AsQueryable();

        if (productoId.HasValue)
        {
            query = query.Where(m => m.ProductoId == productoId.Value);
        }

        if (sedeId.HasValue)
        {
            query = query.Where(m => m.SedeId == sedeId.Value);
        }

        if (tipoMovimiento.HasValue)
        {
            query = query.Where(m => m.TipoMovimiento == tipoMovimiento.Value);
        }

        if (desde.HasValue)
        {
            query = query.Where(m => m.FechaCreacion >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(m => m.FechaCreacion <= hasta.Value);
        }

        var movimientos = await query
            .OrderByDescending(m => m.FechaCreacion)
            .Take(500)
            .ToListAsync(cancellationToken);

        return movimientos.Select(MapKardex).ToList();
    }

    public async Task<IReadOnlyList<CompraResponse>> ListarComprasAsync(
        Guid? sedeId,
        Guid? proveedorId,
        CancellationToken cancellationToken)
    {
        var query = db.Compras
            .AsNoTracking()
            .Include(c => c.Proveedor)
            .Include(c => c.Sede)
            .Include(c => c.Detalles)
                .ThenInclude(d => d.Producto)
            .AsQueryable();

        if (sedeId.HasValue)
        {
            query = query.Where(c => c.SedeId == sedeId.Value);
        }

        if (proveedorId.HasValue)
        {
            query = query.Where(c => c.ProveedorId == proveedorId.Value);
        }

        var compras = await query
            .OrderByDescending(c => c.Fecha)
            .Take(100)
            .ToListAsync(cancellationToken);

        return compras.Select(MapCompra).ToList();
    }

    public async Task<CompraResponse> CrearCompraAsync(
        CrearCompraRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Detalles.Count == 0)
        {
            throw new BusinessRuleException("La compra debe tener al menos un ítem.");
        }

        var proveedor = await db.Proveedores.FirstOrDefaultAsync(p => p.Id == request.ProveedorId, cancellationToken)
            ?? throw new BusinessRuleException("No existe el proveedor.", StatusCodes.Status404NotFound);
        if (!proveedor.Activo)
        {
            throw new BusinessRuleException("El proveedor no está activo.");
        }

        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
            ?? throw new BusinessRuleException("No existe la sede.", StatusCodes.Status404NotFound);

        var ahora = DateTimeOffset.UtcNow;
        var compraId = Guid.NewGuid();
        var compra = new Compra
        {
            Id = compraId,
            EmpresaId = tenant.EmpresaId,
            ProveedorId = proveedor.Id,
            SedeId = sede.Id,
            UsuarioId = currentUser.UserId,
            Fecha = ahora,
            Observacion = string.IsNullOrWhiteSpace(request.Observacion)
                ? null
                : request.Observacion.Trim()[..Math.Min(request.Observacion.Trim().Length, 500)],
            FechaCreacion = ahora
        };

        var lineas = new List<CompraDetalle>();
        foreach (var input in request.Detalles)
        {
            var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == input.ProductoId, cancellationToken)
                ?? throw new BusinessRuleException("Selecciona un producto del catálogo.");
            if (input.Cantidad <= 0)
            {
                throw new BusinessRuleException($"La cantidad de {producto.Nombre} debe ser mayor que cero.");
            }

            var costo = IgvCalculo.Round2(input.CostoUnitario);
            if (costo < 0)
            {
                throw new BusinessRuleException($"El costo de {producto.Nombre} no es válido.");
            }

            lineas.Add(new CompraDetalle
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                CompraId = compraId,
                ProductoId = producto.Id,
                Cantidad = input.Cantidad,
                CostoUnitario = costo,
                Total = IgvCalculo.Round2(input.Cantidad * costo),
                Producto = producto
            });
        }

        compra.Detalles = lineas;
        compra.Total = IgvCalculo.Round2(lineas.Sum(l => l.Total));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Compras.Add(compra);

        await kardex.AplicarMuchosAsync(
            lineas.Select(l => new KardexComando(
                compra.SedeId,
                l.ProductoId,
                TipoMovimientoInventario.INGRESO_COMPRA,
                l.Cantidad,
                $"Ingreso compra {proveedor.RazonSocial} · {l.Producto.Nombre}",
                "COMPRA",
                compra.Id)),
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        compra.Proveedor = proveedor;
        compra.Sede = sede;
        return MapCompra(compra);
    }

    private static StockProductoResponse MapStock(StockProducto? stock, Producto producto, Sede sede)
    {
        var disponible = stock?.CantidadDisponible ?? 0;
        var reservada = stock?.CantidadReservada ?? 0;
        var libre = stock?.CantidadLibre ?? (disponible - reservada);

        return new StockProductoResponse
        {
            Id = stock?.Id,
            SedeId = sede.Id,
            SedeNombre = sede.Nombre,
            ProductoId = producto.Id,
            ProductoNombre = producto.Nombre,
            CodigoSku = producto.CodigoSku,
            TipoProducto = producto.TipoProducto,
            CantidadDisponible = disponible,
            CantidadReservada = reservada,
            CantidadLibre = libre
        };
    }

    private static KardexMovimientoResponse MapKardex(MovimientoInventario movimiento) => new()
    {
        Id = movimiento.Id,
        SedeId = movimiento.SedeId,
        SedeNombre = movimiento.Sede.Nombre,
        ProductoId = movimiento.ProductoId,
        ProductoNombre = movimiento.Producto.Nombre,
        CodigoSku = movimiento.Producto.CodigoSku,
        TipoProducto = movimiento.Producto.TipoProducto,
        TipoMovimiento = movimiento.TipoMovimiento,
        Cantidad = movimiento.Cantidad,
        StockAnterior = movimiento.StockAnterior,
        StockPosterior = movimiento.StockPosterior,
        ReservadoAnterior = movimiento.ReservadoAnterior,
        ReservadoPosterior = movimiento.ReservadoPosterior,
        ReferenciaTipo = movimiento.ReferenciaTipo,
        ReferenciaId = movimiento.ReferenciaId,
        Motivo = movimiento.Motivo,
        UsuarioId = movimiento.UsuarioId,
        UsuarioNombre = movimiento.Usuario?.Nombre,
        FechaCreacion = movimiento.FechaCreacion
    };

    private static CompraResponse MapCompra(Compra compra) => new()
    {
        Id = compra.Id,
        ProveedorId = compra.ProveedorId,
        ProveedorRuc = compra.Proveedor.Ruc,
        ProveedorRazonSocial = compra.Proveedor.RazonSocial,
        SedeId = compra.SedeId,
        SedeNombre = compra.Sede.Nombre,
        Fecha = compra.Fecha,
        Observacion = compra.Observacion,
        Total = compra.Total,
        Detalles = compra.Detalles
            .Select(d => new CompraDetalleResponse
            {
                Id = d.Id,
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto.Nombre,
                CodigoSku = d.Producto.CodigoSku,
                Cantidad = d.Cantidad,
                CostoUnitario = d.CostoUnitario,
                Total = d.Total
            })
            .ToList()
    };
}
