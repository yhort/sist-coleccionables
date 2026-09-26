using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Contracts.Common;
using CapitalPos.Tcg.Api.Contracts.Inventario;
using CapitalPos.Tcg.Api.Contracts.Productos;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Productos;

public sealed class ProductosTcgService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    KardexWriter kardex)
{
    public async Task<PagedResult<ProductoTcgResponse>> ListarAsync(
        TipoProducto? tipoProducto,
        string? juego,
        string? q,
        bool? activo,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = db.Productos.AsNoTracking().AsQueryable();

        if (tipoProducto.HasValue)
        {
            query = query.Where(p => p.TipoProducto == tipoProducto.Value);
        }

        if (activo.HasValue)
        {
            query = query.Where(p => p.Activo == activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(juego))
        {
            var juegoNorm = NormalizarFiltroJuego(juego);
            query = query.Where(p =>
                db.Set<ProductoCarta>().Any(c => c.Id == p.Id && EF.Functions.ILike(c.Juego, juegoNorm))
                || db.Set<ProductoSellado>().Any(s => s.Id == p.Id && EF.Functions.ILike(s.Juego, juegoNorm)));
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Nombre, $"%{term}%")
                || EF.Functions.ILike(p.CodigoSku, $"%{term}%")
                || db.Set<ProductoCarta>().Any(c =>
                    c.Id == p.Id && (
                        EF.Functions.ILike(c.NumeroCarta, $"%{term}%")
                        || EF.Functions.ILike(c.SetCodigo, $"%{term}%")
                        || EF.Functions.ILike(c.SetNombre, $"%{term}%")
                        || EF.Functions.ILike(c.Juego, $"%{term}%")))
                || db.Set<ProductoSellado>().Any(s =>
                    s.Id == p.Id && (
                        EF.Functions.ILike(s.Juego, $"%{term}%")
                        || EF.Functions.ILike(s.Edicion, $"%{term}%"))));
        }

        var total = await query.CountAsync(cancellationToken);
        query = query.OrderBy(p => p.Nombre);

        int pagina;
        int tamano;
        if (Paginacion.EstaActiva(page, pageSize))
        {
            (pagina, tamano) = Paginacion.Normalizar(page, pageSize, total);
            query = query.Skip((pagina - 1) * tamano).Take(tamano);
        }
        else
        {
            pagina = 1;
            tamano = Math.Max(total, 1);
        }

        var productos = await query.ToListAsync(cancellationToken);
        await CargarContenidoFijoAsync(productos, cancellationToken);
        var extras = await CargarExtrasAsync(productos.Select(p => p.Id).ToList(), cancellationToken);
        return new PagedResult<ProductoTcgResponse>
        {
            Items = productos.Select(p => Map(p, extras.GetValueOrDefault(p.Id))).ToList(),
            Total = total,
            Page = pagina,
            PageSize = tamano
        };
    }

    public async Task<ProductoTcgResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var producto = await db.Productos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        await CargarContenidoFijoAsync([producto], cancellationToken);
        var extras = await CargarExtrasAsync([id], cancellationToken);
        return Map(producto, extras.GetValueOrDefault(id));
    }

    public async Task<ProductoTcgResponse> CrearAsync(
        UpsertProductoTcgRequest request,
        CancellationToken cancellationToken)
    {
        ValidarTipoSoportado(request.TipoProducto);
        await AsegurarSkuDisponibleAsync(request.CodigoSku, excluirId: null, cancellationToken);

        if (request.StockInicial > 0 && (request.SedeId is null || request.SedeId == Guid.Empty))
        {
            throw new BusinessRuleException("sedeId es obligatorio cuando hay stock inicial.");
        }

        Sede? sede = null;
        if (request.StockInicial > 0)
        {
            sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
                ?? throw new BusinessRuleException("No existe la sede.", StatusCodes.Status404NotFound);
        }

        var ahora = DateTimeOffset.UtcNow;
        Producto producto = request.TipoProducto switch
        {
            TipoProducto.CARTA => CrearCarta(request, ahora),
            TipoProducto.SELLADO => CrearSellado(request, ahora),
            _ => throw new BusinessRuleException("Tipo de producto no soportado en este sprint.", StatusCodes.Status501NotImplemented)
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Productos.Add(producto);
        if (producto is ProductoSellado selladoNuevo)
        {
            await AplicarContenidoFijoAsync(selladoNuevo, RequerirSellado(request), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (request.StockInicial > 0 && sede is not null)
        {
            var tipoMovimiento = request.TipoIngresoStock == TipoIngresoStockSku.INGRESO_COMPRA
                ? TipoMovimientoInventario.INGRESO_COMPRA
                : TipoMovimientoInventario.AJUSTE;

            await kardex.AplicarAsync(
                new KardexComando(
                    sede.Id,
                    producto.Id,
                    tipoMovimiento,
                    request.StockInicial,
                    $"Alta producto TCG · {producto.Nombre}",
                    "ALTA_SKU",
                    producto.Id,
                    Sentido: tipoMovimiento == TipoMovimientoInventario.AJUSTE
                        ? SentidoAjuste.ENTRADA
                        : null),
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        await CargarContenidoFijoAsync([producto], cancellationToken);
        var extrasAlta = await CargarExtrasAsync([producto.Id], cancellationToken);
        return Map(
            producto,
            extrasAlta.GetValueOrDefault(producto.Id),
            stockLibre: request.StockInicial > 0 ? request.StockInicial : null,
            sedeStockId: request.StockInicial > 0 && sede is not null ? sede.Id : null);
    }

    public async Task<ProductoTcgResponse> CrearVarianteDesdeCatalogoAsync(
        CrearVarianteProductoCartaRequest request,
        CancellationToken cancellationToken)
    {
        var ficha = await db.TcgCartas
            .Include(c => c.Set)
                .ThenInclude(s => s.Serie)
            .FirstOrDefaultAsync(c => c.Id == request.CartaCatalogoId, cancellationToken)
            ?? throw new BusinessRuleException("No existe la ficha de catálogo.", StatusCodes.Status404NotFound);

        var duplicada = await db.ProductosCarta.AnyAsync(
            c => c.CartaCatalogoId == ficha.Id
                && c.EsFoil == request.EsFoil
                && c.Condicion == request.Condicion
                && c.Idioma == request.Idioma,
            cancellationToken);
        if (duplicada)
        {
            throw new BusinessRuleException(
                "Ya existe un SKU para esa ficha con el mismo acabado, condición e idioma.",
                StatusCodes.Status409Conflict);
        }

        var sku = string.IsNullOrWhiteSpace(request.CodigoSku)
            ? SkuVarianteTcg.Generar(
                ficha.Set.Codigo,
                ficha.Numero,
                ficha.Rareza,
                request.EsFoil,
                request.Condicion,
                request.Idioma)
            : request.CodigoSku.Trim();
        await AsegurarSkuDisponibleAsync(sku, excluirId: null, cancellationToken);

        if (request.StockInicial > 0 && (request.SedeId is null || request.SedeId == Guid.Empty))
        {
            throw new BusinessRuleException("sedeId es obligatorio cuando hay stock inicial.");
        }

        Sede? sede = null;
        if (request.StockInicial > 0)
        {
            sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
                ?? throw new BusinessRuleException("No existe la sede.", StatusCodes.Status404NotFound);
        }

        var imagenes = request.Imagenes;
        if ((imagenes is null || imagenes.Count == 0) && !string.IsNullOrWhiteSpace(ficha.ImagenOficialUrl))
        {
            imagenes = [ficha.ImagenOficialUrl];
        }

        var producto = new ProductoCarta
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            TipoProducto = TipoProducto.CARTA,
            Nombre = SkuVarianteTcg.NombreComercial(ficha, request.EsFoil, request.Condicion),
            CodigoSku = sku,
            CodigoBarras = string.IsNullOrWhiteSpace(request.CodigoBarras) ? null : request.CodigoBarras.Trim(),
            PrecioVenta = request.PrecioVenta,
            Costo = request.Costo,
            Imagenes = ProductoImagenes.Serializar(imagenes),
            Activo = true,
            FechaCreacion = DateTimeOffset.UtcNow,
            CartaCatalogoId = ficha.Id,
            Juego = ficha.Set.Serie.Juego,
            SetCodigo = ficha.Set.Codigo,
            SetNombre = ficha.Set.Nombre,
            NumeroCarta = ficha.Numero,
            Rareza = ficha.Rareza,
            Idioma = request.Idioma,
            Condicion = request.Condicion,
            EsFoil = request.EsFoil,
            Artista = ficha.Artista
        };

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Productos.Add(producto);
        await db.SaveChangesAsync(cancellationToken);

        if (request.StockInicial > 0 && sede is not null)
        {
            var tipoMovimiento = request.TipoIngresoStock == TipoIngresoStockSku.INGRESO_COMPRA
                ? TipoMovimientoInventario.INGRESO_COMPRA
                : TipoMovimientoInventario.AJUSTE;

            await kardex.AplicarAsync(
                new KardexComando(
                    sede.Id,
                    producto.Id,
                    tipoMovimiento,
                    request.StockInicial,
                    $"Alta variante TCG · {producto.Nombre}",
                    "ALTA_SKU",
                    producto.Id,
                    Sentido: tipoMovimiento == TipoMovimientoInventario.AJUSTE
                        ? SentidoAjuste.ENTRADA
                        : null),
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        var extrasVariante = await CargarExtrasAsync([producto.Id], cancellationToken);
        return Map(
            producto,
            extrasVariante.GetValueOrDefault(producto.Id),
            stockLibre: request.StockInicial > 0 ? request.StockInicial : null,
            sedeStockId: request.StockInicial > 0 && sede is not null ? sede.Id : null);
    }

    public async Task<ProductoTcgResponse?> ActualizarAsync(
        Guid id,
        UpsertProductoTcgRequest request,
        CancellationToken cancellationToken)
    {
        ValidarTipoSoportado(request.TipoProducto);

        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        if (producto.TipoProducto != request.TipoProducto)
        {
            throw new BusinessRuleException("No se puede cambiar el tipo de producto.");
        }

        await AsegurarSkuDisponibleAsync(request.CodigoSku, id, cancellationToken);
        CopiarComunes(producto, request);

        switch (producto)
        {
            case ProductoCarta carta:
                AplicarCarta(carta, RequerirCarta(request));
                break;
            case ProductoSellado sellado:
                AplicarSellado(sellado, RequerirSellado(request));
                await db.Entry(sellado).Collection(s => s.ContenidoFijo).LoadAsync(cancellationToken);
                await AplicarContenidoFijoAsync(sellado, RequerirSellado(request), cancellationToken);
                break;
            default:
                throw new BusinessRuleException("Tipo de producto no soportado en este sprint.", StatusCodes.Status501NotImplemented);
        }

        await db.SaveChangesAsync(cancellationToken);
        await CargarContenidoFijoAsync([producto], cancellationToken);
        var extrasUpdate = await CargarExtrasAsync([producto.Id], cancellationToken);
        return Map(producto, extrasUpdate.GetValueOrDefault(producto.Id));
    }

    public async Task<ProductoTcgResponse?> CambiarActivoAsync(Guid id, bool activo, CancellationToken cancellationToken)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        producto.Activo = activo;
        await db.SaveChangesAsync(cancellationToken);
        await CargarContenidoFijoAsync([producto], cancellationToken);
        var extras = await CargarExtrasAsync([id], cancellationToken);
        return Map(producto, extras.GetValueOrDefault(id));
    }

    /// <summary>
    /// Soft delete si hay historial/kardex/ventas/stock; borrado físico solo si no tiene
    /// ID de WooCommerce ni dependencias.
    /// </summary>
    public async Task<EliminarProductoTcgResponse?> EliminarODesactivarAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        var extras = await CargarExtrasAsync([id], cancellationToken);
        var info = extras.GetValueOrDefault(id) ?? ProductoExtras.Vacio;

        if (info.TieneDependencias)
        {
            producto.Activo = false;
            await db.SaveChangesAsync(cancellationToken);
            await CargarContenidoFijoAsync([producto], cancellationToken);
            extras = await CargarExtrasAsync([id], cancellationToken);
            return new EliminarProductoTcgResponse
            {
                Id = id,
                Accion = "DESACTIVADA",
                Motivo =
                    "El producto tiene historial (kardex, ventas, stock, pedidos u otros registros). " +
                    "Se desactivó para conservar la trazabilidad; seguirá visible en reportes históricos y kardex de fechas pasadas, " +
                    "pero no en selectores operativos ni en el stock actual.",
                Producto = Map(producto, extras.GetValueOrDefault(id))
            };
        }

        if (info.WooVinculado)
        {
            throw new BusinessRuleException(
                "El producto está vinculado a WooCommerce y no tiene historial local. " +
                "Desvincúlalo primero para eliminarlo, o desactívalo para ocultarlo de operaciones.");
        }

        await EliminarProductoLimpioAsync(producto, cancellationToken);
        return new EliminarProductoTcgResponse
        {
            Id = id,
            Accion = "ELIMINADA",
            Motivo = "El producto no tenía ID de WooCommerce ni movimientos asociados y se eliminó de forma permanente.",
            Producto = null
        };
    }

    public async Task<ProductoTcgResponse?> DesvincularWooCommerceAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (producto is null)
        {
            return null;
        }

        var mapeo = await db.WooCommerceMapeosProducto
            .FirstOrDefaultAsync(m => m.ProductoId == id, cancellationToken);
        if (mapeo is null || (mapeo.WooProductId is null && mapeo.WooVariationId is null))
        {
            throw new BusinessRuleException("El producto no está vinculado a WooCommerce.");
        }

        mapeo.WooProductId = null;
        mapeo.WooVariationId = null;
        mapeo.EstadoMapeo = EstadoMapeoWoo.NO_MAPEADO;
        mapeo.StockWoo = null;
        mapeo.PrecioRebajadoWoo = null;
        mapeo.Mensaje = "Desvinculado de WooCommerce. No entra al batch de sincronización.";
        mapeo.UltimaSincronizacion = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await CargarContenidoFijoAsync([producto], cancellationToken);
        var extras = await CargarExtrasAsync([id], cancellationToken);
        return Map(producto, extras.GetValueOrDefault(id));
    }

    private async Task AsegurarSkuDisponibleAsync(string codigoSku, Guid? excluirId, CancellationToken cancellationToken)
    {
        var sku = codigoSku.Trim();
        var existe = await db.Productos.AnyAsync(
            p => p.CodigoSku == sku && (!excluirId.HasValue || p.Id != excluirId.Value),
            cancellationToken);

        if (existe)
        {
            throw new BusinessRuleException("El SKU ya existe en la empresa.", StatusCodes.Status409Conflict);
        }
    }

    private ProductoCarta CrearCarta(UpsertProductoTcgRequest request, DateTimeOffset ahora)
    {
        var carta = new ProductoCarta { TipoProducto = TipoProducto.CARTA };
        Inicializar(carta, request, ahora);
        AplicarCarta(carta, RequerirCarta(request));
        return carta;
    }

    private ProductoSellado CrearSellado(UpsertProductoTcgRequest request, DateTimeOffset ahora)
    {
        var sellado = new ProductoSellado { TipoProducto = TipoProducto.SELLADO };
        Inicializar(sellado, request, ahora);
        AplicarSellado(sellado, RequerirSellado(request));
        return sellado;
    }

    private void Inicializar(Producto producto, UpsertProductoTcgRequest request, DateTimeOffset ahora)
    {
        producto.Id = Guid.NewGuid();
        producto.EmpresaId = tenant.EmpresaId;
        producto.FechaCreacion = ahora;
        CopiarComunes(producto, request);
    }

    private static void CopiarComunes(Producto producto, UpsertProductoTcgRequest request)
    {
        producto.Nombre = request.Nombre.Trim();
        producto.CodigoSku = request.CodigoSku.Trim();
        producto.CodigoBarras = string.IsNullOrWhiteSpace(request.CodigoBarras) ? null : request.CodigoBarras.Trim();
        producto.PrecioVenta = request.PrecioVenta;
        producto.Costo = request.Costo;
        producto.CategoriaId = request.CategoriaId;
        producto.MarcaId = request.MarcaId;
        producto.Imagenes = ProductoImagenes.Serializar(request.Imagenes);
    }

    private static void AplicarCarta(ProductoCarta carta, ProductoCartaRequest request)
    {
        carta.Juego = request.Juego.Trim();
        carta.SetCodigo = request.SetCodigo.Trim();
        carta.SetNombre = request.SetNombre.Trim();
        carta.NumeroCarta = request.NumeroCarta.Trim();
        carta.Rareza = request.Rareza;
        carta.Idioma = request.Idioma;
        carta.Condicion = request.Condicion;
        carta.EsFoil = request.EsFoil;
        carta.Artista = string.IsNullOrWhiteSpace(request.Artista) ? null : request.Artista.Trim();
    }

    private static void AplicarSellado(ProductoSellado sellado, ProductoSelladoRequest request)
    {
        sellado.Juego = request.Juego.Trim();
        sellado.Edicion = request.Edicion.Trim();
        sellado.TipoSellado = request.TipoSellado;
        sellado.CartasEsperadas = request.CartasEsperadas;
        sellado.PermiteApertura = request.PermiteApertura;
    }

    private async Task AplicarContenidoFijoAsync(
        ProductoSellado sellado,
        ProductoSelladoRequest request,
        CancellationToken cancellationToken)
    {
        var deseados = request.PermiteApertura
            ? (request.ContenidoFijo ?? [])
                .Where(item => item.ProductoId != Guid.Empty && item.Cantidad > 0)
                .ToList()
            : [];

        if (deseados.Any(item => item.ProductoId == sellado.Id))
        {
            throw new BusinessRuleException("El sellado no puede incluirse a sí mismo como contenido fijo.");
        }

        var duplicados = deseados
            .GroupBy(item => item.ProductoId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicados.Count > 0)
        {
            throw new BusinessRuleException("Hay productos repetidos en el contenido fijo.");
        }

        var ids = deseados.Select(item => item.ProductoId).ToList();
        var componentes = ids.Count == 0
            ? []
            : await db.Productos
                .Where(p => ids.Contains(p.Id))
                .ToListAsync(cancellationToken);

        if (componentes.Count != ids.Count)
        {
            throw new BusinessRuleException("Uno o más SKUs del contenido fijo no existen.", StatusCodes.Status404NotFound);
        }

        var vigentes = sellado.ContenidoFijo.ToList();
        foreach (var actual in vigentes)
        {
            if (!ids.Contains(actual.ProductoComponenteId))
            {
                db.ProductoSelladoContenidoFijo.Remove(actual);
            }
        }

        foreach (var item in deseados)
        {
            var fila = sellado.ContenidoFijo.FirstOrDefault(c => c.ProductoComponenteId == item.ProductoId);
            if (fila is null)
            {
                var nuevaFila = new ProductoSelladoContenidoFijo
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = sellado.EmpresaId,
                    ProductoSelladoId = sellado.Id,
                    ProductoComponenteId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    ProductoComponente = componentes.First(p => p.Id == item.ProductoId)
                };

                sellado.ContenidoFijo.Add(nuevaFila);
                db.ProductoSelladoContenidoFijo.Add(nuevaFila); // <--- Esta línea le indica a EF Core que es un INSERT
            }
            else
            {
                fila.Cantidad = item.Cantidad;
            }
        }
        
        /*
        foreach (var item in deseados)
        {
            var fila = sellado.ContenidoFijo.FirstOrDefault(c => c.ProductoComponenteId == item.ProductoId);
            if (fila is null)
            {
                sellado.ContenidoFijo.Add(new ProductoSelladoContenidoFijo
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = sellado.EmpresaId,
                    ProductoSelladoId = sellado.Id,
                    ProductoComponenteId = item.ProductoId,
                    Cantidad = item.Cantidad,
                    ProductoComponente = componentes.First(p => p.Id == item.ProductoId)
                });
            }
            else
            {
                fila.Cantidad = item.Cantidad;
            }
        }*/
    }

    private async Task CargarContenidoFijoAsync(IReadOnlyCollection<Producto> productos, CancellationToken cancellationToken)
    {
        var sellados = productos.OfType<ProductoSellado>().ToList();
        if (sellados.Count == 0)
        {
            return;
        }

        var ids = sellados.Select(s => s.Id).ToList();
        var filas = await db.ProductoSelladoContenidoFijo
            .AsNoTracking()
            .Include(c => c.ProductoComponente)
            .Where(c => ids.Contains(c.ProductoSelladoId))
            .ToListAsync(cancellationToken);

        foreach (var sellado in sellados)
        {
            sellado.ContenidoFijo = filas.Where(c => c.ProductoSelladoId == sellado.Id).ToList();
        }
    }

    private static ProductoCartaRequest RequerirCarta(UpsertProductoTcgRequest request)
    {
        if (request.Carta is null)
        {
            throw new BusinessRuleException("Los datos de carta son obligatorios cuando tipoProducto es CARTA.");
        }

        return request.Carta;
    }

    private static ProductoSelladoRequest RequerirSellado(UpsertProductoTcgRequest request)
    {
        if (request.Sellado is null)
        {
            throw new BusinessRuleException("Los datos de sellado son obligatorios cuando tipoProducto es SELLADO.");
        }

        return request.Sellado;
    }

    private static string NormalizarFiltroJuego(string juego)
    {
        var texto = juego.Trim();
        return texto.ToUpperInvariant() switch
        {
            "POKEMON" or "POKÉMON" => "Pokémon",
            "MAGIC" => "Magic",
            "YUGIOH" or "YU-GI-OH" or "YU-GI-OH!" => "Yu-Gi-Oh!",
            _ => texto
        };
    }

    private static void ValidarTipoSoportado(TipoProducto tipo)
    {
        if (tipo is TipoProducto.ACCESORIO or TipoProducto.COMPUESTO)
        {
            throw new BusinessRuleException(
                "ACCESORIO y COMPUESTO se implementan en Sprint 1.b (ProductoAccesorio / BOM).",
                StatusCodes.Status501NotImplemented);
        }

        if (tipo is not (TipoProducto.CARTA or TipoProducto.SELLADO))
        {
            throw new BusinessRuleException("tipoProducto inválido.");
        }
    }

    private async Task EliminarProductoLimpioAsync(Producto producto, CancellationToken cancellationToken)
    {
        var mapeos = await db.WooCommerceMapeosProducto
            .Where(m => m.ProductoId == producto.Id)
            .ToListAsync(cancellationToken);
        db.WooCommerceMapeosProducto.RemoveRange(mapeos);

        var contenidoPropio = await db.ProductoSelladoContenidoFijo
            .Where(c => c.ProductoSelladoId == producto.Id)
            .ToListAsync(cancellationToken);
        db.ProductoSelladoContenidoFijo.RemoveRange(contenidoPropio);

        var stocksVacios = await db.StocksProductos
            .Where(s => s.ProductoId == producto.Id)
            .ToListAsync(cancellationToken);
        db.StocksProductos.RemoveRange(stocksVacios);

        db.Productos.Remove(producto);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, ProductoExtras>> CargarExtrasAsync(
        IReadOnlyList<Guid> productoIds,
        CancellationToken cancellationToken)
    {
        var result = productoIds.ToDictionary(id => id, _ => ProductoExtras.Vacio);
        if (productoIds.Count == 0)
        {
            return result;
        }

        var mapeos = await db.WooCommerceMapeosProducto
            .AsNoTracking()
            .Where(m => productoIds.Contains(m.ProductoId))
            .ToListAsync(cancellationToken);
        foreach (var mapeo in mapeos)
        {
            var estado = mapeo.WooProductId is null ? EstadoMapeoWoo.NO_MAPEADO : mapeo.EstadoMapeo;
            result[mapeo.ProductoId] = result[mapeo.ProductoId] with
            {
                WooVinculado = mapeo.WooProductId is not null,
                WooProductId = mapeo.WooProductId,
                WooVariationId = mapeo.WooVariationId,
                EstadoMapeo = estado,
                PrecioNormalWoo = mapeo.PrecioNormalWoo,
                PrecioRebajadoWoo = mapeo.PrecioRebajadoWoo,
                StockWoo = mapeo.StockWoo,
                MensajeWoo = mapeo.Mensaje,
                UltimaSincronizacion = mapeo.UltimaSincronizacion
            };
        }

        var deps = await CargarMapaDependenciasAsync(productoIds, cancellationToken);
        foreach (var id in productoIds)
        {
            result[id] = result[id] with { TieneDependencias = deps.GetValueOrDefault(id) };
        }

        return result;
    }

    private async Task<Dictionary<Guid, bool>> CargarMapaDependenciasAsync(
        IReadOnlyList<Guid> productoIds,
        CancellationToken cancellationToken)
    {
        var result = productoIds.ToDictionary(id => id, _ => false);
        if (productoIds.Count == 0)
        {
            return result;
        }

        async Task MarcarSiExiste(IQueryable<Guid> query)
        {
            var ids = await query.Distinct().ToListAsync(cancellationToken);
            foreach (var id in ids)
            {
                if (result.ContainsKey(id))
                {
                    result[id] = true;
                }
            }
        }

        await MarcarSiExiste(
            db.MovimientosInventario
                .Where(m => productoIds.Contains(m.ProductoId))
                .Select(m => m.ProductoId));
        await MarcarSiExiste(
            db.StocksProductos
                .Where(s => productoIds.Contains(s.ProductoId)
                    && (s.CantidadDisponible > 0 || s.CantidadReservada > 0))
                .Select(s => s.ProductoId));
        await MarcarSiExiste(
            db.VentaDetalles
                .Where(d => productoIds.Contains(d.ProductoId))
                .Select(d => d.ProductoId));
        await MarcarSiExiste(
            db.PedidoDigitalDetalles
                .Where(d => productoIds.Contains(d.ProductoId))
                .Select(d => d.ProductoId));
        await MarcarSiExiste(
            db.CompraDetalles
                .Where(d => productoIds.Contains(d.ProductoId))
                .Select(d => d.ProductoId));
        await MarcarSiExiste(
            db.SubastaDetalles
                .Where(d => productoIds.Contains(d.ProductoId))
                .Select(d => d.ProductoId));
        await MarcarSiExiste(
            db.SubastasTcg
                .Where(s => productoIds.Contains(s.ProductoId))
                .Select(s => s.ProductoId));
        await MarcarSiExiste(
            db.AperturasTcg
                .Where(a => productoIds.Contains(a.ProductoSelladoId))
                .Select(a => a.ProductoSelladoId));
        await MarcarSiExiste(
            db.AperturaTcgDetalles
                .Where(d => productoIds.Contains(d.ProductoCartaId))
                .Select(d => d.ProductoCartaId));
        await MarcarSiExiste(
            db.ProductoSelladoContenidoFijo
                .Where(c => productoIds.Contains(c.ProductoComponenteId))
                .Select(c => c.ProductoComponenteId));

        return result;
    }

    private static ProductoTcgResponse Map(
        Producto producto,
        ProductoExtras? extras = null,
        decimal? stockLibre = null,
        Guid? sedeStockId = null)
    {
        extras ??= ProductoExtras.Vacio;
        return new()
        {
            Id = producto.Id,
            TipoProducto = producto.TipoProducto,
            Nombre = producto.Nombre,
            CodigoSku = producto.CodigoSku,
            CodigoBarras = producto.CodigoBarras,
            PrecioVenta = producto.PrecioVenta,
            Costo = producto.Costo,
            CategoriaId = producto.CategoriaId,
            MarcaId = producto.MarcaId,
            Imagenes = ProductoImagenes.Parse(producto.Imagenes),
            Activo = producto.Activo,
            FechaCreacion = producto.FechaCreacion,
            StockLibre = stockLibre,
            SedeStockId = sedeStockId,
            TieneDependencias = extras.TieneDependencias,
            WooVinculado = extras.WooVinculado,
            Woo = new ProductoWooResumenResponse
            {
                WooProductId = extras.WooProductId,
                WooVariationId = extras.WooVariationId,
                EstadoMapeo = extras.EstadoMapeo,
                PrecioNormalWoo = extras.PrecioNormalWoo,
                PrecioRebajadoWoo = extras.PrecioRebajadoWoo,
                StockWoo = extras.StockWoo,
                Mensaje = extras.MensajeWoo,
                UltimaSincronizacion = extras.UltimaSincronizacion
            },
            Carta = producto is ProductoCarta carta
                ? new ProductoCartaResponse
                {
                    CartaCatalogoId = carta.CartaCatalogoId,
                    Juego = carta.Juego,
                    SetCodigo = carta.SetCodigo,
                    SetNombre = carta.SetNombre,
                    NumeroCarta = carta.NumeroCarta,
                    Rareza = carta.Rareza,
                    Idioma = carta.Idioma,
                    Condicion = carta.Condicion,
                    EsFoil = carta.EsFoil,
                    Artista = carta.Artista
                }
                : null,
            Sellado = producto is ProductoSellado sellado
                ? new ProductoSelladoResponse
                {
                    Juego = sellado.Juego,
                    Edicion = sellado.Edicion,
                    TipoSellado = sellado.TipoSellado,
                    CartasEsperadas = sellado.CartasEsperadas,
                    PermiteApertura = sellado.PermiteApertura,
                    ContenidoFijo = (sellado.ContenidoFijo ?? [])
                        .OrderBy(c => c.ProductoComponente?.Nombre ?? "")
                        .Select(c => new ProductoSelladoContenidoFijoResponse
                        {
                            ProductoId = c.ProductoComponenteId,
                            Nombre = c.ProductoComponente?.Nombre ?? "",
                            CodigoSku = c.ProductoComponente?.CodigoSku ?? "",
                            Cantidad = c.Cantidad
                        })
                        .ToList()
                }
                : null
        };
    }

    private sealed record ProductoExtras(
        bool TieneDependencias,
        bool WooVinculado,
        long? WooProductId,
        long? WooVariationId,
        EstadoMapeoWoo EstadoMapeo,
        decimal PrecioNormalWoo,
        decimal? PrecioRebajadoWoo,
        decimal? StockWoo,
        string? MensajeWoo,
        DateTimeOffset? UltimaSincronizacion)
    {
        public static ProductoExtras Vacio { get; } = new(
            false,
            false,
            null,
            null,
            EstadoMapeoWoo.NO_MAPEADO,
            0,
            null,
            null,
            null,
            null);
    }
}
