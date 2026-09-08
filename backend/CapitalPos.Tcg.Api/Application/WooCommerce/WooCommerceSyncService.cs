using System.Security.Cryptography;
using System.Text;
using CapitalPos.Tcg.Api.Application.Productos;
using CapitalPos.Tcg.Api.Contracts.WooCommerce;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using CapitalPos.Tcg.Api.Infrastructure.WooCommerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Application.WooCommerce;

public sealed class WooCommerceSyncService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    IWooCommerceGateway gateway,
    WooCredentialProtector protector,
    IOptions<WooCommerceOptions> options,
    ILogger<WooCommerceSyncService> logger) : IWooStockChangeNotifier
{
    public async Task<WooConfigResponse?> ObtenerConfigAsync(CancellationToken cancellationToken)
    {
        var integracion = await CargarIntegracionAsync(cancellationToken);
        return integracion is null ? null : MapConfig(integracion);
    }

    public async Task<WooConfigResponse> GuardarConfigAsync(GuardarWooConfigRequest request, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SedeOrigenId, cancellationToken)
            ?? throw new BusinessRuleException("Selecciona una sede de origen válida.");

        var url = request.UrlTienda.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new BusinessRuleException("La URL de la tienda no es válida.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var integracion = await db.IntegracionesWooCommerce.FirstOrDefaultAsync(cancellationToken);
        if (integracion is null)
        {
            if (string.IsNullOrWhiteSpace(request.ConsumerKey) || string.IsNullOrWhiteSpace(request.ConsumerSecret))
            {
                throw new BusinessRuleException("Indica Consumer Key y Consumer Secret.");
            }

            integracion = new IntegracionWooCommerce
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                FechaCreacion = ahora
            };
            db.IntegracionesWooCommerce.Add(integracion);
        }

        integracion.UrlTienda = url;
        integracion.SedeOrigenId = sede.Id;
        integracion.ModoSincronizacion = request.ModoSincronizacion;
        integracion.ModoRecepcionPedidos = request.ModoRecepcionPedidos;
        integracion.Activa = request.Activa;
        integracion.FechaActualizacion = ahora;
        integracion.EstadoConexion = EstadoConexionWoo.DESCONECTADO;
        integracion.MensajeConexion = "Configuración actualizada. Vuelve a probar la conexión.";

        if (!string.IsNullOrWhiteSpace(request.ConsumerKey))
        {
            integracion.ConsumerKeyCifrado = protector.Cifrar(request.ConsumerKey.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.ConsumerSecret))
        {
            integracion.ConsumerSecretCifrado = protector.Cifrar(request.ConsumerSecret.Trim());
        }

        if (string.IsNullOrWhiteSpace(integracion.ConsumerKeyCifrado)
            || string.IsNullOrWhiteSpace(integracion.ConsumerSecretCifrado))
        {
            throw new BusinessRuleException("Indica Consumer Key y Consumer Secret.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerConfigAsync(cancellationToken))!;
    }

    public async Task<WooConfigResponse> ProbarConexionAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirIntegracionAsync(cancellationToken);
        var (key, secret) = Credenciales(integracion);
        var ahora = DateTimeOffset.UtcNow;
        try
        {
            await gateway.ProbarConexionAsync(integracion.UrlTienda, key, secret, cancellationToken);
            integracion.EstadoConexion = EstadoConexionWoo.CONECTADO;
            integracion.MensajeConexion = $"REST API de {integracion.UrlTienda} autenticada. Sede origen lista para publicar stock.";
        }
        catch (UnauthorizedAccessException ex)
        {
            integracion.EstadoConexion = EstadoConexionWoo.ERROR_AUTENTICACION;
            integracion.MensajeConexion = ex.Message;
        }
        catch (Exception ex)
        {
            integracion.EstadoConexion = EstadoConexionWoo.DESCONECTADO;
            integracion.MensajeConexion = ex.Message;
        }

        integracion.UltimoIntentoConexion = ahora;
        await db.SaveChangesAsync(cancellationToken);
        return MapConfig(integracion);
    }

    public async Task<WooSyncCatalogoResponse> SincronizarCatalogoAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirConectadaAsync(cancellationToken);
        var (key, secret) = Credenciales(integracion);
        var cartas = await db.ProductosCarta.Where(p => p.Activo).ToListAsync(cancellationToken);
        var sellados = await db.ProductosSellado.Where(p => p.Activo).ToListAsync(cancellationToken);
        var productos = cartas.Cast<Producto>().Concat(sellados).ToList();

        var mapeos = await db.WooCommerceMapeosProducto.ToListAsync(cancellationToken);
        var porProducto = mapeos.ToDictionary(m => m.ProductoId);
        var ahora = DateTimeOffset.UtcNow;
        var creados = 0;
        var actualizados = 0;
        var omitidos = 0;
        var errores = 0;
        var skusOk = new List<string>();

        foreach (var producto in productos)
        {
            var libre = await LibreSedeAsync(integracion.SedeOrigenId, producto.Id, cancellationToken);
            if (!porProducto.TryGetValue(producto.Id, out var mapeo))
            {
                mapeo = new WooCommerceMapeoProducto
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = tenant.EmpresaId,
                    IntegracionId = integracion.Id,
                    ProductoId = producto.Id,
                    EstadoMapeo = EstadoMapeoWoo.PENDIENTE_SUBIDA,
                    FechaCreacion = ahora
                };
                db.WooCommerceMapeosProducto.Add(mapeo);
                porProducto[producto.Id] = mapeo;
            }

            try
            {
                var eraNuevo = mapeo.WooProductId is null;
                if (eraNuevo)
                {
                    var existente = await gateway.BuscarProductoPorSkuAsync(
                        integracion.UrlTienda,
                        key,
                        secret,
                        producto.CodigoSku,
                        cancellationToken);
                    if (existente is not null)
                    {
                        mapeo.WooProductId = existente.Id;
                        mapeo.WooVariationId = existente.VariationId;
                        eraNuevo = false;
                    }
                }

                var remoto = await gateway.CrearOActualizarProductoAsync(
                    integracion.UrlTienda,
                    key,
                    secret,
                    mapeo.WooProductId,
                    mapeo.WooVariationId,
                    new WooProductoUpsert(
                        producto.CodigoSku,
                        producto.Nombre,
                        DescripcionCatalogo(producto),
                        producto.PrecioVenta,
                        libre,
                        AtributosTpt(producto),
                        ProductoImagenes.Parse(producto.Imagenes)),
                    cancellationToken);

                mapeo.WooProductId = remoto.Id;
                if (remoto.VariationId is > 0)
                {
                    mapeo.WooVariationId = remoto.VariationId;
                }
                mapeo.PrecioNormalWoo = remoto.RegularPrice;
                mapeo.PrecioRebajadoWoo = remoto.SalePrice;
                mapeo.StockWoo = remoto.StockQuantity;
                mapeo.EstadoMapeo = EstadoMapeoWoo.SINCRONIZADO;
                mapeo.Mensaje = null;
                mapeo.UltimaSincronizacion = ahora;
                if (eraNuevo)
                {
                    creados++;
                }
                else
                {
                    actualizados++;
                }

                skusOk.Add(producto.CodigoSku);
            }
            catch (Exception ex)
            {
                errores++;
                mapeo.EstadoMapeo = EstadoMapeoWoo.DESFASADO;
                mapeo.Mensaje = ex.Message;
                mapeo.UltimaSincronizacion = ahora;
                logger.LogWarning(ex, "Fallo sync catálogo SKU {Sku}", producto.CodigoSku);
            }
        }

        RegistrarLog(
            integracion,
            TipoSyncWoo.PRODUCTO_OUT,
            errores > 0 ? ResultadoSyncWoo.ERROR : ResultadoSyncWoo.OK,
            $"Catálogo TPT: {creados} creados, {actualizados} actualizados, {omitidos} omitidos. SKU: {string.Join(", ", skusOk.Take(12))}",
            errores > 0 ? $"{errores} SKU con error." : null,
            ahora);

        await db.SaveChangesAsync(cancellationToken);
        return new WooSyncCatalogoResponse
        {
            Enviados = creados + actualizados,
            Creados = creados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            Errores = errores
        };
    }

    public async Task<WooSyncStockResponse> SincronizarStockAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirConectadaAsync(cancellationToken);
        var mapeos = await db.WooCommerceMapeosProducto
            .Include(m => m.Producto)
            .Where(m => m.WooProductId != null)
            .ToListAsync(cancellationToken);

        var publicados = 0;
        var omitidos = 0;
        var errores = 0;
        var ahora = DateTimeOffset.UtcNow;
        var skus = new List<string>();

        foreach (var mapeo in mapeos)
        {
            if (mapeo.WooProductId is null || mapeo.Producto is null || !mapeo.Producto.Activo)
            {
                omitidos++;
                continue;
            }

            try
            {
                await PublicarMapeoAsync(integracion, mapeo, cancellationToken);
                publicados++;
                skus.Add(mapeo.Producto.CodigoSku);
            }
            catch (Exception ex)
            {
                errores++;
                mapeo.EstadoMapeo = EstadoMapeoWoo.DESFASADO;
                mapeo.Mensaje = ex.Message;
                mapeo.UltimaSincronizacion = ahora;
                logger.LogWarning(ex, "Fallo stock-out SKU {Sku}", mapeo.Producto.CodigoSku);
            }
        }

        RegistrarLog(
            integracion,
            TipoSyncWoo.STOCK_OUT,
            errores > 0 ? ResultadoSyncWoo.ERROR : ResultadoSyncWoo.OK,
            $"Stock libre de sede origen publicado ({publicados} SKU). Omitidos: {omitidos}.",
            errores > 0 ? $"{errores} SKU con error." : null,
            ahora);

        await db.SaveChangesAsync(cancellationToken);
        return new WooSyncStockResponse
        {
            Publicados = publicados,
            Omitidos = omitidos,
            Errores = errores
        };
    }

    public async Task<WooSyncProductoResponse> SincronizarProductoAsync(
        Guid productoId,
        CancellationToken cancellationToken)
    {
        var integracion = await ExigirConectadaAsync(cancellationToken);
        var mapeo = await db.WooCommerceMapeosProducto
            .Include(m => m.Producto)
            .FirstOrDefaultAsync(m => m.ProductoId == productoId && m.WooProductId != null, cancellationToken)
            ?? throw new BusinessRuleException(
                "El producto no tiene un mapeo WooCommerce con WooProductId.",
                StatusCodes.Status404NotFound);

        var (key, secret) = Credenciales(integracion);
        await gateway.AlinearSkuAsync(
            integracion.UrlTienda,
            key,
            secret,
            mapeo.WooProductId!.Value,
            mapeo.WooVariationId,
            mapeo.Producto.CodigoSku,
            cancellationToken);
        await PublicarMapeoAsync(integracion, mapeo, cancellationToken);
        var fecha = mapeo.UltimaSincronizacion ?? DateTimeOffset.UtcNow;
        RegistrarLog(
            integracion,
            TipoSyncWoo.STOCK_OUT,
            ResultadoSyncWoo.OK,
            $"Sincronización forzada SKU {mapeo.Producto.CodigoSku}: CantidadLibre {mapeo.StockWoo} → Woo #{mapeo.WooProductId}.",
            null,
            fecha);
        await db.SaveChangesAsync(cancellationToken);

        return new WooSyncProductoResponse
        {
            ProductoId = mapeo.ProductoId,
            Sku = mapeo.Producto.CodigoSku,
            WooProductId = mapeo.WooProductId!.Value,
            WooVariationId = mapeo.WooVariationId,
            CantidadLibrePublicada = mapeo.StockWoo ?? 0,
            FechaSincronizacion = fecha
        };
    }

    public async Task PublicarStockProductoAsync(Guid productoId, CancellationToken cancellationToken)
    {
        try
        {
            var integracion = await db.IntegracionesWooCommerce.FirstOrDefaultAsync(cancellationToken);
            if (integracion is null || !integracion.Activa || integracion.EstadoConexion != EstadoConexionWoo.CONECTADO)
            {
                return;
            }

            var mapeo = await db.WooCommerceMapeosProducto
                .Include(m => m.Producto)
                .FirstOrDefaultAsync(m => m.ProductoId == productoId && m.WooProductId != null, cancellationToken);
            if (mapeo is null)
            {
                return;
            }

            await PublicarMapeoAsync(integracion, mapeo, cancellationToken);
            RegistrarLog(
                integracion,
                TipoSyncWoo.STOCK_OUT,
                ResultadoSyncWoo.OK,
                $"Stock-out en tiempo real SKU {mapeo.Producto.CodigoSku} (libre {mapeo.StockWoo}).",
                null,
                DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "No se pudo publicar stock Woo del producto {ProductoId}", productoId);
            try
            {
                var mapeo = await db.WooCommerceMapeosProducto
                    .FirstOrDefaultAsync(m => m.ProductoId == productoId && m.WooProductId != null, cancellationToken);
                if (mapeo is not null)
                {
                    mapeo.EstadoMapeo = EstadoMapeoWoo.DESFASADO;
                    mapeo.Mensaje = Truncar(ex.Message, 500);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception persistEx)
            {
                logger.LogWarning(persistEx, "No se pudo marcar DESFASADO el mapeo Woo {ProductoId}", productoId);
            }
        }
    }

    public async Task RecibirWebhookProductoAsync(
        WooWebhookProductoRequest request,
        string? firmaHeader,
        string rawBody,
        CancellationToken cancellationToken)
    {
        if (request.EmpresaId == Guid.Empty)
        {
            throw new BusinessRuleException("El webhook WooCommerce requiere empresaId.");
        }

        tenant.SetEmpresa(request.EmpresaId);
        var integracion = await ExigirIntegracionAsync(cancellationToken);
        ValidarFirma(integracion, firmaHeader, rawBody);

        var mapeo = request.VariationId is > 0
            ? await db.WooCommerceMapeosProducto
                .Include(m => m.Producto)
                .FirstOrDefaultAsync(m => m.WooVariationId == request.VariationId, cancellationToken)
            : null;
        mapeo ??= await db.WooCommerceMapeosProducto
            .Include(m => m.Producto)
            .FirstOrDefaultAsync(m => m.WooProductId == request.Id, cancellationToken);
        if (mapeo is null)
        {
            return;
        }

        var libre = await LibreSedeAsync(integracion.SedeOrigenId, mapeo.ProductoId, cancellationToken);
        mapeo.StockWoo = request.StockQuantity;
        if (decimal.TryParse(request.RegularPrice, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var precio))
        {
            mapeo.PrecioNormalWoo = precio;
        }

        mapeo.UltimaSincronizacion = DateTimeOffset.UtcNow;
        var desfasado = mapeo.StockWoo != libre || mapeo.PrecioNormalWoo != mapeo.Producto.PrecioVenta;
        mapeo.EstadoMapeo = desfasado ? EstadoMapeoWoo.DESFASADO : EstadoMapeoWoo.SINCRONIZADO;
        mapeo.Mensaje = desfasado
            ? $"stock local {libre} vs Woo {mapeo.StockWoo} · se republicará el libre de la sede origen"
            : null;

        RegistrarLog(
            integracion,
            TipoSyncWoo.STOCK_IN,
            ResultadoSyncWoo.OK,
            $"Webhook product.updated {request.Id} SKU {mapeo.Producto.CodigoSku}. Local sigue siendo la fuente de verdad.",
            null,
            DateTimeOffset.UtcNow);

        if (desfasado && integracion.EstadoConexion == EstadoConexionWoo.CONECTADO)
        {
            try
            {
                await PublicarMapeoAsync(integracion, mapeo, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "No se pudo republicar stock tras webhook de producto {WooId}", request.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WooMapeoResponse>> ListarMapeosAsync(CancellationToken cancellationToken)
    {
        var integracion = await CargarIntegracionAsync(cancellationToken);
        if (integracion is null)
        {
            return [];
        }

        var productos = await db.Productos
            .Where(p => p.Activo && (p.TipoProducto == TipoProducto.CARTA || p.TipoProducto == TipoProducto.SELLADO))
            .ToListAsync(cancellationToken);
        var mapeos = await db.WooCommerceMapeosProducto.ToListAsync(cancellationToken);
        var porProducto = mapeos.ToDictionary(m => m.ProductoId);
        var filas = new List<WooMapeoResponse>();

        foreach (var producto in productos.OrderBy(p => p.CodigoSku))
        {
            porProducto.TryGetValue(producto.Id, out var mapeo);
            var libre = await LibreSedeAsync(integracion.SedeOrigenId, producto.Id, cancellationToken);
            filas.Add(MapMapeo(producto, mapeo, libre));
        }

        return filas;
    }

    public async Task<IReadOnlyList<WooMapeoResponse>> GuardarMapeosAsync(
        IReadOnlyList<WooMapeoInput> inputs,
        CancellationToken cancellationToken)
    {
        var integracion = await ExigirIntegracionAsync(cancellationToken);
        var ahora = DateTimeOffset.UtcNow;
        foreach (var input in inputs)
        {
            var producto = await db.Productos.FirstOrDefaultAsync(p => p.Id == input.ProductoId, cancellationToken)
                ?? throw new BusinessRuleException("Producto no encontrado para el mapeo.");

            var mapeo = await db.WooCommerceMapeosProducto
                .FirstOrDefaultAsync(m => m.ProductoId == input.ProductoId, cancellationToken);
            if (mapeo is null)
            {
                mapeo = new WooCommerceMapeoProducto
                {
                    Id = Guid.NewGuid(),
                    EmpresaId = tenant.EmpresaId,
                    IntegracionId = integracion.Id,
                    ProductoId = producto.Id,
                    FechaCreacion = ahora
                };
                db.WooCommerceMapeosProducto.Add(mapeo);
            }

            mapeo.WooProductId = input.WooProductId;
            mapeo.WooVariationId = input.WooVariationId;
            mapeo.EstadoMapeo = input.WooProductId is null ? EstadoMapeoWoo.NO_MAPEADO : EstadoMapeoWoo.PENDIENTE_SUBIDA;
            mapeo.Mensaje = input.WooProductId is null ? "Sin ID de WooCommerce. No entra al batch." : "Mapeado, pendiente de primera publicación.";
        }

        await db.SaveChangesAsync(cancellationToken);
        return await ListarMapeosAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WooSyncLogResponse>> ListarLogsAsync(int take, CancellationToken cancellationToken)
    {
        var logs = await db.WooCommerceSyncLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Fecha)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return logs.Select(l => new WooSyncLogResponse
        {
            Id = l.Id,
            Tipo = l.Tipo,
            Estado = l.Estado,
            PayloadResumen = l.PayloadResumen,
            MensajeError = l.MensajeError,
            Intentos = l.Intentos,
            ProximoReintento = l.ProximoReintento,
            Fecha = l.Fecha
        }).ToList();
    }

    private async Task PublicarMapeoAsync(
        IntegracionWooCommerce integracion,
        WooCommerceMapeoProducto mapeo,
        CancellationToken cancellationToken)
    {
        var (key, secret) = Credenciales(integracion);
        var libre = await LibreSedeAsync(integracion.SedeOrigenId, mapeo.ProductoId, cancellationToken);
        // Solo stock: no enviar images aquí. Woo duplica adjuntos en la librería si el PUT
        // de stock-out incluye src de imagen.
        await gateway.ActualizarStockAsync(
            integracion.UrlTienda,
            key,
            secret,
            mapeo.WooProductId!.Value,
            mapeo.WooVariationId,
            libre,
            cancellationToken);
        mapeo.StockWoo = libre;
        mapeo.PrecioNormalWoo = mapeo.Producto.PrecioVenta;
        mapeo.EstadoMapeo = EstadoMapeoWoo.SINCRONIZADO;
        mapeo.Mensaje = null;
        mapeo.UltimaSincronizacion = DateTimeOffset.UtcNow;
    }

    private void ValidarFirma(IntegracionWooCommerce integracion, string? firmaHeader, string rawBody)
    {
        var rest = string.Equals(options.Value.Modo, "Rest", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(firmaHeader))
        {
            if (rest)
            {
                throw new BusinessRuleException("Falta la firma X-WC-Webhook-Signature.", StatusCodes.Status401Unauthorized);
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new BusinessRuleException("No se pudo leer el cuerpo del webhook para validar la firma.", StatusCodes.Status401Unauthorized);
        }

        var secret = protector.Descifrar(integracion.ConsumerSecretCifrado);
        var hash = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawBody)));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(hash), Encoding.UTF8.GetBytes(firmaHeader)))
        {
            throw new BusinessRuleException("Firma del webhook WooCommerce inválida.", StatusCodes.Status401Unauthorized);
        }
    }

    private async Task<decimal> LibreSedeAsync(Guid sedeId, Guid productoId, CancellationToken cancellationToken)
    {
        var stock = await db.StocksProductos.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SedeId == sedeId && s.ProductoId == productoId, cancellationToken);
        return stock is null ? 0 : stock.CantidadDisponible - stock.CantidadReservada;
    }

    private void RegistrarLog(
        IntegracionWooCommerce integracion,
        TipoSyncWoo tipo,
        ResultadoSyncWoo estado,
        string resumen,
        string? error,
        DateTimeOffset fecha)
    {
        db.WooCommerceSyncLogs.Add(new WooCommerceSyncLog
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            IntegracionId = integracion.Id,
            Tipo = tipo,
            Estado = estado,
            PayloadResumen = resumen.Length <= 2000 ? resumen : resumen[..2000],
            MensajeError = error,
            Fecha = fecha
        });
    }

    private async Task<IntegracionWooCommerce?> CargarIntegracionAsync(CancellationToken cancellationToken) =>
        await db.IntegracionesWooCommerce.Include(i => i.SedeOrigen).FirstOrDefaultAsync(cancellationToken);

    private async Task<IntegracionWooCommerce> ExigirIntegracionAsync(CancellationToken cancellationToken) =>
        await CargarIntegracionAsync(cancellationToken)
        ?? throw new BusinessRuleException("Configura WooCommerce antes de sincronizar.");

    private async Task<IntegracionWooCommerce> ExigirConectadaAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirIntegracionAsync(cancellationToken);
        if (!integracion.Activa)
        {
            throw new BusinessRuleException("La integración WooCommerce está inactiva.");
        }

        if (integracion.EstadoConexion != EstadoConexionWoo.CONECTADO)
        {
            throw new BusinessRuleException("Conecta la REST API de WooCommerce antes de sincronizar.");
        }

        return integracion;
    }

    private (string Key, string Secret) Credenciales(IntegracionWooCommerce integracion)
    {
        try
        {
            return (protector.Descifrar(integracion.ConsumerKeyCifrado), protector.Descifrar(integracion.ConsumerSecretCifrado));
        }
        catch (Exception ex)
        {
            throw new BusinessRuleException("No se pudieron descifrar las credenciales WooCommerce. Vuelve a guardarlas.", innerException: ex);
        }
    }

    private static string DescripcionCatalogo(Producto producto) => producto switch
    {
        ProductoCarta carta => $"{carta.Juego} · {carta.SetNombre} {carta.NumeroCarta} · {carta.Rareza} {carta.Condicion}",
        ProductoSellado sellado => $"{sellado.Juego} · {sellado.Edicion} · {sellado.TipoSellado}",
        _ => producto.Nombre
    };

    private static IReadOnlyDictionary<string, string> AtributosTpt(Producto producto) => producto switch
    {
        ProductoCarta carta => new Dictionary<string, string>
        {
            ["pa_juego"] = carta.Juego,
            ["pa_set"] = carta.SetNombre,
            ["pa_numero"] = carta.NumeroCarta,
            ["pa_rareza"] = carta.Rareza.ToString(),
            ["pa_condicion"] = carta.Condicion.ToString(),
            ["pa_idioma"] = carta.Idioma.ToString(),
            ["pa_foil"] = carta.EsFoil ? "Foil" : "Non-foil",
            ["tipo_producto"] = "CARTA"
        },
        ProductoSellado sellado => new Dictionary<string, string>
        {
            ["pa_juego"] = sellado.Juego,
            ["pa_edicion"] = sellado.Edicion,
            ["pa_tipo_sellado"] = sellado.TipoSellado.ToString(),
            ["tipo_producto"] = "SELLADO"
        },
        _ => new Dictionary<string, string> { ["tipo_producto"] = producto.TipoProducto.ToString() }
    };

    private WooConfigResponse MapConfig(IntegracionWooCommerce i)
    {
        var keyEnmascarada = "ck_****";
        try
        {
            if (!string.IsNullOrWhiteSpace(i.ConsumerKeyCifrado))
            {
                keyEnmascarada = WooCredentialProtector.Enmascarar(protector.Descifrar(i.ConsumerKeyCifrado));
            }
        }
        catch
        {
            keyEnmascarada = "ck_****";
        }

        return new WooConfigResponse
        {
            Id = i.Id,
            UrlTienda = i.UrlTienda,
            ConsumerKeyEnmascarada = keyEnmascarada,
            TieneSecret = !string.IsNullOrWhiteSpace(i.ConsumerSecretCifrado),
            SedeOrigenId = i.SedeOrigenId,
            SedeOrigenNombre = i.SedeOrigen?.Nombre ?? string.Empty,
            ModoSincronizacion = i.ModoSincronizacion,
            ModoRecepcionPedidos = i.ModoRecepcionPedidos,
            EstadoConexion = i.EstadoConexion,
            MensajeConexion = i.MensajeConexion,
            UltimoIntentoConexion = i.UltimoIntentoConexion,
            Activa = i.Activa
        };
    }

    private static WooMapeoResponse MapMapeo(Producto producto, WooCommerceMapeoProducto? mapeo, decimal stockLocal)
    {
        var estado = mapeo is null
            ? EstadoMapeoWoo.NO_MAPEADO
            : mapeo.WooProductId is null
                ? EstadoMapeoWoo.NO_MAPEADO
                : mapeo.EstadoMapeo;
        return new WooMapeoResponse
        {
            Id = mapeo?.Id ?? Guid.Empty,
            ProductoId = producto.Id,
            Nombre = producto.Nombre,
            Sku = producto.CodigoSku,
            TipoProducto = producto.TipoProducto,
            PrecioLocal = producto.PrecioVenta,
            StockLocal = stockLocal,
            WooProductId = mapeo?.WooProductId,
            WooVariationId = mapeo?.WooVariationId,
            PrecioNormalWoo = mapeo?.PrecioNormalWoo ?? 0,
            PrecioRebajadoWoo = mapeo?.PrecioRebajadoWoo,
            StockWoo = mapeo?.StockWoo,
            EstadoMapeo = estado,
            Mensaje = mapeo?.Mensaje ?? (estado == EstadoMapeoWoo.NO_MAPEADO ? "Sin ID de WooCommerce. No entra al batch." : null),
            UltimaSincronizacion = mapeo?.UltimaSincronizacion
        };
    }

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];
}
