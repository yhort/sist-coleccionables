using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Contracts.WooCommerce;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using CapitalPos.Tcg.Api.Infrastructure.WooCommerce;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Application.WooCommerce;

public sealed class WooCommercePedidoImportService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    PedidosDigitalesService pedidos,
    IWooCommerceGateway gateway,
    WooCredentialProtector protector,
    IOptions<WooCommerceOptions> options,
    ILogger<WooCommercePedidoImportService> logger)
{
    private static readonly JsonSerializerOptions JsonRemoto = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(60)
    ];

    public async Task<WooImportPedidosResponse> ImportarPedidosAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirConectadaAsync(cancellationToken);
        var (key, secret) = Credenciales(integracion);
        var remotos = await gateway.ListarPedidosPendientesAsync(integracion.UrlTienda, key, secret, cancellationToken);
        return await ImportarRemotosAsync(integracion, remotos, cancellationToken);
    }

    public async Task<WooImportPedidosResponse> RecibirWebhookPedidoAsync(
        WooWebhookPedidoRequest request,
        string? firmaHeader,
        string rawBody,
        CancellationToken cancellationToken)
    {
        if (request.EmpresaId == Guid.Empty)
        {
            throw new BusinessRuleException("El webhook WooCommerce requiere empresaId.");
        }

        tenant.SetEmpresa(request.EmpresaId);
        var integracion = await db.IntegracionesWooCommerce
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Configura WooCommerce antes de sincronizar.");

        ValidarFirma(integracion, firmaHeader, rawBody);

        var nombre = $"{request.Billing?.FirstName} {request.Billing?.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = "Cliente WooCommerce";
        }

        var pagado = request.Status is "processing" or "completed";
        var remoto = new WooPedidoRemoto(
            request.Id,
            request.Status ?? "pending",
            nombre,
            request.Billing?.Phone,
            pagado,
            request.Shipping?.Address1,
            request.Shipping?.City,
            request.LineItems.Select(l => new WooPedidoLineaRemota(
                l.ProductId ?? 0,
                l.VariationId ?? 0,
                l.Sku ?? string.Empty,
                l.Quantity,
                l.Price)).ToList());

        return await ImportarRemotosAsync(integracion, [remoto], cancellationToken);
    }

    public async Task<WooImportPedidosResponse> SimularWebhookPedidoAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirActivaAsync(cancellationToken);
        var productoId = await db.StocksProductos
            .Where(s => s.SedeId == integracion.SedeOrigenId
                && s.CantidadDisponible - s.CantidadReservada >= 1)
            .OrderBy(s => s.Producto.CodigoSku)
            .Select(s => s.ProductoId)
            .FirstOrDefaultAsync(cancellationToken);
        var producto = await db.Productos
            .FirstOrDefaultAsync(
                p => p.Id == productoId
                    && p.Activo
                    && (p.TipoProducto == TipoProducto.CARTA || p.TipoProducto == TipoProducto.SELLADO),
                cancellationToken)
            ?? throw new BusinessRuleException(
                "No hay un producto TCG con stock libre en la sede origen para simular el webhook.");
        var mapeo = await db.WooCommerceMapeosProducto
            .FirstOrDefaultAsync(m => m.ProductoId == producto.Id, cancellationToken);
        var wooProductId = mapeo?.WooProductId ?? 0;
        var wooVariationId = mapeo?.WooVariationId ?? 0;

        var id = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var remoto = new WooPedidoRemoto(
            id,
            "processing",
            "Cliente webhook demo",
            "999000111",
            true,
            null,
            null,
            [new WooPedidoLineaRemota(wooProductId, wooVariationId, producto.CodigoSku, 1, producto.PrecioVenta)]);

        return await ImportarRemotosAsync(integracion, [remoto], cancellationToken);
    }

    public async Task<WooImportPedidosResponse> ReintentarPendientesAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var pendientes = await db.WooCommerceSyncLogs
            .IgnoreQueryFilters()
            .Where(l => l.Tipo == TipoSyncWoo.PEDIDO_IN
                && l.Estado == ResultadoSyncWoo.REINTENTO
                && l.PayloadJson != null
                && l.ProximoReintento != null
                && l.ProximoReintento <= ahora
                && l.Intentos < 5)
            .OrderBy(l => l.ProximoReintento)
            .Take(20)
            .ToListAsync(cancellationToken);

        var importados = 0;
        var omitidos = 0;
        var errores = 0;
        var referencias = new List<string>();

        foreach (var log in pendientes)
        {
            tenant.SetEmpresa(log.EmpresaId);
            var integracion = await db.IntegracionesWooCommerce
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == log.IntegracionId, cancellationToken);
            if (integracion is null)
            {
                log.Estado = ResultadoSyncWoo.ERROR;
                log.MensajeError = "Integración WooCommerce no encontrada para el reintento.";
                errores++;
                continue;
            }

            WooPedidoRemoto? remoto;
            try
            {
                remoto = JsonSerializer.Deserialize<WooPedidoRemoto>(log.PayloadJson!, JsonRemoto);
            }
            catch (Exception ex)
            {
                log.Estado = ResultadoSyncWoo.ERROR;
                log.MensajeError = $"Payload de reintento inválido: {ex.Message}";
                errores++;
                continue;
            }

            if (remoto is null)
            {
                log.Estado = ResultadoSyncWoo.ERROR;
                log.MensajeError = "Payload de reintento vacío.";
                errores++;
                continue;
            }

            var resultado = await ImportarRemotosAsync(integracion, [remoto], cancellationToken, log);
            importados += resultado.Importados;
            omitidos += resultado.Omitidos;
            errores += resultado.Errores;
            referencias.AddRange(resultado.Referencias);
        }

        if (pendientes.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new WooImportPedidosResponse
        {
            Importados = importados,
            Omitidos = omitidos,
            Errores = errores,
            Referencias = referencias
        };
    }

    private async Task<WooImportPedidosResponse> ImportarRemotosAsync(
        IntegracionWooCommerce integracion,
        IReadOnlyList<WooPedidoRemoto> remotos,
        CancellationToken cancellationToken,
        WooCommerceSyncLog? reintento = null)
    {
        var importados = new List<string>();
        var omitidos = 0;
        var errores = 0;
        var ahora = DateTimeOffset.UtcNow;

        foreach (var remoto in remotos)
        {
            var referencia = $"woo-{remoto.Id}";
            try
            {
                if (await db.PedidosDigitales.AnyAsync(p => p.ReferenciaExterna == referencia, cancellationToken))
                {
                    omitidos++;
                    MarcarReintentoResuelto(reintento, ahora, $"Pedido {referencia} ya existía (deduplicado).");
                    continue;
                }

                var detalles = new List<PedidoDigitalDetalleInput>();
                foreach (var linea in remoto.Lineas)
                {
                    var producto = await ResolverProductoLineaAsync(linea, cancellationToken)
                        ?? throw new BusinessRuleException($"SKU Woo '{linea.Sku}' (product_id {linea.ProductId}, variation_id {linea.VariationId}) no está mapeado.");

                    detalles.Add(new PedidoDigitalDetalleInput
                    {
                        ProductoId = producto.Id,
                        Cantidad = linea.Quantity,
                        PrecioUnitario = linea.Price
                    });
                }

                var direccion = SanitizarDireccion(remoto.Direccion);
                var pedido = await pedidos.CrearAsync(
                    new CrearPedidoDigitalRequest
                    {
                        ClienteNombre = remoto.ClienteNombre,
                        ClienteTelefono = remoto.ClienteTelefono,
                        SedeId = integracion.SedeOrigenId,
                        CanalPedido = CanalPedidoDigital.WOOCOMMERCE,
                        ReferenciaExterna = referencia,
                        Observacion = $"Checkout WooCommerce #{remoto.Id} · {remoto.Status}",
                        Detalles = detalles,
                        Entrega = new PedidoDigitalEntregaInput
                        {
                            DestinatarioNombre = remoto.ClienteNombre,
                            DestinatarioTelefono = remoto.ClienteTelefono,
                            Direccion = direccion,
                            Distrito = remoto.Distrito,
                            EsRecojoTienda = false
                        }
                    },
                    cancellationToken);

                if (remoto.Pagado)
                {
                    await pedidos.CambiarEstadoAsync(
                        pedido.Id,
                        EstadoPedidoDigital.Pagado,
                        "Pago confirmado en WooCommerce. Entra al Kanban en Pagado.",
                        cancellationToken);
                }

                importados.Add(referencia);
                MarcarReintentoResuelto(reintento, ahora, $"Pedido {referencia} importado en reintento.");
            }
            catch (BusinessRuleException ex) when (ex.Message.Contains("ya fue importado", StringComparison.OrdinalIgnoreCase))
            {
                omitidos++;
                MarcarReintentoResuelto(reintento, ahora, $"Pedido {referencia} ya fue importado.");
            }
            catch (Exception ex)
            {
                errores++;
                logger.LogWarning(ex, "No se importó el pedido Woo {Id}", remoto.Id);
                ProgramarReintento(integracion, remoto, ex, ahora, reintento);
            }
        }

        if (reintento is null)
        {
            db.WooCommerceSyncLogs.Add(new WooCommerceSyncLog
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                IntegracionId = integracion.Id,
                Tipo = TipoSyncWoo.PEDIDO_IN,
                Estado = errores > 0 ? ResultadoSyncWoo.ERROR : ResultadoSyncWoo.OK,
                PayloadResumen = $"{importados.Count} pedido(s) insertados en Pedidos Digitales (canal WOOCOMMERCE). Omitidos: {omitidos}.",
                MensajeError = errores > 0 ? $"{errores} con error. Se reintentarán automáticamente." : null,
                Fecha = ahora
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new WooImportPedidosResponse
        {
            Importados = importados.Count,
            Omitidos = omitidos,
            Errores = errores,
            Referencias = importados
        };
    }

    private async Task<Producto?> ResolverProductoLineaAsync(WooPedidoLineaRemota linea, CancellationToken cancellationToken)
    {
        if (linea.VariationId > 0)
        {
            var porVariacion = await db.WooCommerceMapeosProducto
                .Include(m => m.Producto)
                .FirstOrDefaultAsync(m => m.WooVariationId == linea.VariationId, cancellationToken);
            if (porVariacion?.Producto is not null)
            {
                return porVariacion.Producto;
            }
        }

        if (!string.IsNullOrWhiteSpace(linea.Sku))
        {
            var porSku = await db.Productos.FirstOrDefaultAsync(p => p.CodigoSku == linea.Sku, cancellationToken);
            if (porSku is not null)
            {
                return porSku;
            }
        }

        if (linea.ProductId > 0)
        {
            var mapeo = await db.WooCommerceMapeosProducto
                .Include(m => m.Producto)
                .Where(m => m.WooProductId == linea.ProductId)
                .OrderBy(m => m.WooVariationId == null ? 0 : 1)
                .FirstOrDefaultAsync(cancellationToken);
            return mapeo?.Producto;
        }

        return null;
    }

    private static string SanitizarDireccion(string? direccionRecibida)
    {
        var direccion = direccionRecibida?.Trim() ?? string.Empty;
        return direccion.Length >= 5
            ? direccion
            : string.IsNullOrEmpty(direccion)
                ? "Sin dirección completa"
                : $"Dirección no especificada - {direccion}";
    }

    private void ProgramarReintento(
        IntegracionWooCommerce integracion,
        WooPedidoRemoto remoto,
        Exception error,
        DateTimeOffset ahora,
        WooCommerceSyncLog? existente)
    {
        var log = existente ?? new WooCommerceSyncLog
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            IntegracionId = integracion.Id,
            Tipo = TipoSyncWoo.PEDIDO_IN,
            Fecha = ahora
        };

        if (existente is null)
        {
            db.WooCommerceSyncLogs.Add(log);
        }

        log.Intentos = Math.Max(log.Intentos, 0) + 1;
        log.PayloadJson = JsonSerializer.Serialize(remoto, JsonRemoto);
        log.PayloadResumen = $"Reintento webhook pedido woo-{remoto.Id} (intento {log.Intentos}/5).";
        log.MensajeError = Truncar(error.Message, 500);

        if (log.Intentos >= 5)
        {
            log.Estado = ResultadoSyncWoo.ERROR;
            log.ProximoReintento = null;
            log.PayloadResumen = $"Pedido woo-{remoto.Id} agotó 5 reintentos.";
            return;
        }

        log.Estado = ResultadoSyncWoo.REINTENTO;
        var espera = Backoff[Math.Min(log.Intentos - 1, Backoff.Length - 1)];
        log.ProximoReintento = ahora.Add(espera);
    }

    private static void MarcarReintentoResuelto(WooCommerceSyncLog? log, DateTimeOffset ahora, string resumen)
    {
        if (log is null)
        {
            return;
        }

        log.Estado = ResultadoSyncWoo.OK;
        log.MensajeError = null;
        log.ProximoReintento = null;
        log.PayloadResumen = resumen;
        log.Fecha = ahora;
    }

    private async Task<IntegracionWooCommerce> ExigirActivaAsync(CancellationToken cancellationToken)
    {
        var integracion = await db.IntegracionesWooCommerce
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("Configura WooCommerce antes de sincronizar.");

        if (!integracion.Activa)
        {
            throw new BusinessRuleException("La integración WooCommerce está inactiva.");
        }

        return integracion;
    }

    private async Task<IntegracionWooCommerce> ExigirConectadaAsync(CancellationToken cancellationToken)
    {
        var integracion = await ExigirActivaAsync(cancellationToken);
        if (integracion.EstadoConexion != EstadoConexionWoo.CONECTADO)
        {
            throw new BusinessRuleException("Conecta la REST API de WooCommerce antes de sincronizar.");
        }

        return integracion;
    }

    private (string Key, string Secret) Credenciales(IntegracionWooCommerce integracion) =>
        (protector.Descifrar(integracion.ConsumerKeyCifrado), protector.Descifrar(integracion.ConsumerSecretCifrado));

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

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];
}
