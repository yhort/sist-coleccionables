using System.Text.Json;
using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Contracts.Ecosistema;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Cpe;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalPos.Tcg.Api.Application.Ecosistema;

public sealed class EcosistemaService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    IServicioFiscal fiscal,
    ICpeEmisor cpeEmisor,
    IOptions<CpeApiOptions> cpeOptions)
{
    public async Task<IReadOnlyList<EcosistemaConexionResponse>> ListarConexionesAsync(CancellationToken cancellationToken)
    {
        await AsegurarConexionesAsync(cancellationToken);
        var items = await db.EcosistemaConexiones.AsNoTracking()
            .OrderBy(c => c.Tipo)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<EcosistemaConexionResponse> PingAsync(Guid id, CancellationToken cancellationToken)
    {
        await AsegurarConexionesAsync(cancellationToken);
        var conexion = await db.EcosistemaConexiones.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la conexión.", StatusCodes.Status404NotFound);

        conexion.UltimoPing = DateTimeOffset.UtcNow;
        try
        {
            switch (conexion.Tipo)
            {
                case TipoConexionEcosistema.CPE_LOCAL:
                    conexion.EstadoSalud = EstadoSaludConexion.OK;
                    conexion.UltimoError = null;
                    conexion.ConfiguracionJson = JsonSerializer.Serialize(new
                    {
                        emisor = cpeEmisor.Nombre,
                        modo = cpeOptions.Value.Modo,
                        baseUrl = string.IsNullOrWhiteSpace(cpeOptions.Value.BaseUrl) ? null : cpeOptions.Value.BaseUrl
                    });
                    break;
                case TipoConexionEcosistema.WOOCOMMERCE:
                    var woo = await db.IntegracionesWooCommerce.FirstOrDefaultAsync(cancellationToken);
                    conexion.EstadoSalud = woo?.EstadoConexion == EstadoConexionWoo.CONECTADO
                        ? EstadoSaludConexion.OK
                        : woo is null ? EstadoSaludConexion.DESCONOCIDO : EstadoSaludConexion.DEGRADADO;
                    conexion.UltimoError = woo?.MensajeConexion;
                    break;
                default:
                    conexion.EstadoSalud = EstadoSaludConexion.OK;
                    conexion.UltimoError = null;
                    break;
            }
        }
        catch (Exception ex)
        {
            conexion.EstadoSalud = EstadoSaludConexion.CAIDO;
            conexion.UltimoError = ex.Message;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Map(conexion);
    }

    public async Task<ConfiguracionFiscalResponse> ObtenerFiscalAsync(CancellationToken cancellationToken)
    {
        var fiscalCfg = await db.ConfiguracionesFiscales.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("No hay ficha fiscal. Guárdala en Ecosistema.", StatusCodes.Status404NotFound);
        return MapFiscal(fiscalCfg);
    }

    public async Task<ConfiguracionFiscalResponse> GuardarFiscalAsync(
        GuardarConfiguracionFiscalRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Ruc.Trim().Length != 11)
        {
            throw new BusinessRuleException("El RUC debe tener 11 dígitos.");
        }

        if (request.Ubigeo.Trim().Length != 6)
        {
            throw new BusinessRuleException("El UBIGEO debe tener 6 dígitos.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var fiscalCfg = await db.ConfiguracionesFiscales.FirstOrDefaultAsync(cancellationToken);
        if (fiscalCfg is null)
        {
            fiscalCfg = new ConfiguracionFiscalEmpresa
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                FechaCreacion = ahora
            };
            db.ConfiguracionesFiscales.Add(fiscalCfg);
        }

        fiscalCfg.Ruc = request.Ruc.Trim();
        fiscalCfg.RazonSocial = request.RazonSocial.Trim();
        fiscalCfg.NombreComercial = request.NombreComercial.Trim();
        fiscalCfg.DireccionFiscal = request.DireccionFiscal.Trim();
        fiscalCfg.Ubigeo = request.Ubigeo.Trim();
        fiscalCfg.Departamento = request.Departamento.Trim();
        fiscalCfg.Provincia = request.Provincia.Trim();
        fiscalCfg.Distrito = request.Distrito.Trim();
        fiscalCfg.FechaActualizacion = ahora;
        await db.SaveChangesAsync(cancellationToken);
        return MapFiscal(fiscalCfg);
    }

    public async Task<WebhookSalidaResponse> ObtenerWebhookAsync(CancellationToken cancellationToken)
    {
        var conexion = await AsegurarWebhookAsync(cancellationToken);
        return MapWebhook(conexion);
    }

    public async Task<WebhookSalidaResponse> GuardarWebhookAsync(
        GuardarWebhookSalidaRequest request,
        CancellationToken cancellationToken)
    {
        var url = request.Url.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("La URL del webhook debe comenzar con http:// o https://.");
        }

        var eventos = (request.Eventos ?? [])
            .Select(e => e.Trim())
            .Where(e => e is "pedido.estado" or "guia.estado")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (eventos.Count == 0)
        {
            eventos.Add("pedido.estado");
        }

        var conexion = await AsegurarWebhookAsync(cancellationToken);
        var estado = LeerEstado(conexion);
        estado.Nombre = string.IsNullOrWhiteSpace(request.Nombre) ? "WhatsApp pedidos y guías" : request.Nombre.Trim();
        estado.Url = url;
        estado.Token = request.Token?.Trim() ?? string.Empty;
        estado.Canal = "WHATSAPP";
        estado.Eventos = eventos;
        estado.Activo = request.Activo;
        conexion.Nombre = estado.Nombre;
        conexion.ConfiguracionJson = JsonSerializer.Serialize(estado);
        conexion.EstadoSalud = estado.Activo ? EstadoSaludConexion.OK : EstadoSaludConexion.DESCONOCIDO;
        await db.SaveChangesAsync(cancellationToken);
        return MapWebhook(conexion);
    }

    public async Task<WebhookLogResponse> ProbarWebhookAsync(
        ProbarWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var evento = string.IsNullOrWhiteSpace(request.Evento) ? "pedido.estado" : request.Evento.Trim();
        if (evento is not "pedido.estado" and not "guia.estado")
        {
            throw new BusinessRuleException("El evento del webhook debe ser pedido.estado o guia.estado.");
        }

        var conexion = await AsegurarWebhookAsync(cancellationToken);
        var estado = LeerEstado(conexion);
        var log = new WebhookLogPersistido
        {
            Id = Guid.NewGuid(),
            Evento = evento,
            Destino = "Equipo Trunqi",
            PayloadResumen = $"Prueba manual de {evento}",
            Estado = !estado.Activo || !estado.Eventos.Contains(evento)
                ? "OMITIDO"
                : "ENVIADO",
            Fecha = DateTimeOffset.UtcNow
        };
        estado.Logs = new[] { log }.Concat(estado.Logs).Take(20).ToList();
        conexion.ConfiguracionJson = JsonSerializer.Serialize(estado);
        conexion.UltimoPing = log.Fecha;
        conexion.EstadoSalud = log.Estado == "ENVIADO" ? EstadoSaludConexion.OK : EstadoSaludConexion.DEGRADADO;
        conexion.UltimoError = log.Estado == "ENVIADO" ? null : "Webhook inactivo u evento no suscrito.";
        await db.SaveChangesAsync(cancellationToken);
        return MapLog(conexion.Id, log);
    }

    public async Task<IReadOnlyList<SerieComprobanteResponse>> ListarSeriesAsync(CancellationToken cancellationToken)
    {
        var series = await db.SeriesComprobante.AsNoTracking()
            .OrderBy(s => s.Tipo)
            .ThenBy(s => s.Serie)
            .ToListAsync(cancellationToken);
        return series.Select(s => new SerieComprobanteResponse
        {
            Id = s.Id,
            Tipo = s.Tipo,
            Serie = s.Serie,
            Correlativo = s.Correlativo,
            Activa = s.Activa
        }).ToList();
    }

    public Task<ComprobanteResponse> EmitirDesdeVentaAsync(
        Guid ventaId,
        TipoComprobanteSunat tipo,
        CancellationToken cancellationToken) =>
        fiscal.EmitirDesdeVentaAsync(ventaId, tipo, null, cancellationToken);

    public Task<ComprobanteResponse> EmitirNotaCreditoAsync(
        Guid ventaId,
        EmitirNotaCreditoRequest request,
        CancellationToken cancellationToken) =>
        fiscal.EmitirDesdeVentaAsync(ventaId, TipoComprobanteSunat.NOTA_CREDITO, request, cancellationToken);

    private async Task AsegurarConexionesAsync(CancellationToken cancellationToken)
    {
        var existentes = await db.EcosistemaConexiones.Select(c => c.Tipo).ToListAsync(cancellationToken);
        var ahora = DateTimeOffset.UtcNow;
        foreach (var tipo in new[]
                 {
                     TipoConexionEcosistema.CPE_LOCAL,
                     TipoConexionEcosistema.WOOCOMMERCE,
                     TipoConexionEcosistema.YAPE,
                     TipoConexionEcosistema.IZIPAY,
                     TipoConexionEcosistema.WEBHOOK_SALIDA
                 })
        {
            if (existentes.Contains(tipo))
            {
                continue;
            }

            db.EcosistemaConexiones.Add(new EcosistemaConexion
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                Tipo = tipo,
                Nombre = tipo switch
                {
                    TipoConexionEcosistema.CPE_LOCAL => "CPE local / SUNAT",
                    TipoConexionEcosistema.WOOCOMMERCE => "WooCommerce",
                    TipoConexionEcosistema.YAPE => "Yape",
                    TipoConexionEcosistema.WEBHOOK_SALIDA => "Webhook WhatsApp",
                    _ => "Izipay"
                },
                EstadoSalud = EstadoSaludConexion.DESCONOCIDO,
                FechaCreacion = ahora
            });
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static EcosistemaConexionResponse Map(EcosistemaConexion c) => new()
    {
        Id = c.Id,
        Tipo = c.Tipo,
        Nombre = c.Nombre,
        EstadoSalud = c.EstadoSalud,
        UltimoPing = c.UltimoPing,
        UltimoError = c.UltimoError
    };

    private static ConfiguracionFiscalResponse MapFiscal(ConfiguracionFiscalEmpresa f) => new()
    {
        Id = f.Id,
        Ruc = f.Ruc,
        RazonSocial = f.RazonSocial,
        NombreComercial = f.NombreComercial,
        DireccionFiscal = f.DireccionFiscal,
        Ubigeo = f.Ubigeo,
        Departamento = f.Departamento,
        Provincia = f.Provincia,
        Distrito = f.Distrito
    };

    private async Task<EcosistemaConexion> AsegurarWebhookAsync(CancellationToken cancellationToken)
    {
        await AsegurarConexionesAsync(cancellationToken);
        return await db.EcosistemaConexiones
            .FirstAsync(c => c.Tipo == TipoConexionEcosistema.WEBHOOK_SALIDA, cancellationToken);
    }

    private static WebhookEstadoPersistido LeerEstado(EcosistemaConexion conexion)
    {
        if (string.IsNullOrWhiteSpace(conexion.ConfiguracionJson))
        {
            return WebhookEstadoPersistido.PorDefecto();
        }

        try
        {
            return JsonSerializer.Deserialize<WebhookEstadoPersistido>(conexion.ConfiguracionJson)
                ?? WebhookEstadoPersistido.PorDefecto();
        }
        catch (JsonException)
        {
            return WebhookEstadoPersistido.PorDefecto();
        }
    }

    private static WebhookSalidaResponse MapWebhook(EcosistemaConexion conexion)
    {
        var estado = LeerEstado(conexion);
        return new WebhookSalidaResponse
        {
            Id = conexion.Id,
            Nombre = string.IsNullOrWhiteSpace(estado.Nombre) ? conexion.Nombre : estado.Nombre,
            Url = estado.Url,
            Token = estado.Token,
            Canal = string.IsNullOrWhiteSpace(estado.Canal) ? "WHATSAPP" : estado.Canal,
            Eventos = estado.Eventos,
            Activo = estado.Activo,
            Logs = estado.Logs.Select(l => MapLog(conexion.Id, l)).ToList()
        };
    }

    private static WebhookLogResponse MapLog(Guid webhookId, WebhookLogPersistido log) => new()
    {
        Id = log.Id,
        WebhookId = webhookId,
        Evento = log.Evento,
        Destino = log.Destino,
        PayloadResumen = log.PayloadResumen,
        Estado = log.Estado,
        Fecha = log.Fecha
    };

    private sealed class WebhookEstadoPersistido
    {
        public string Nombre { get; set; } = "WhatsApp pedidos y guías";
        public string Url { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Canal { get; set; } = "WHATSAPP";
        public List<string> Eventos { get; set; } = ["pedido.estado"];
        public bool Activo { get; set; } = true;
        public List<WebhookLogPersistido> Logs { get; set; } = [];

        public static WebhookEstadoPersistido PorDefecto() => new()
        {
            Nombre = "WhatsApp · pedidos y guías",
            Url = "https://hooks.trunqi.pe/whatsapp",
            Token = string.Empty,
            Eventos = ["pedido.estado", "guia.estado"],
            Activo = true
        };
    }

    private sealed class WebhookLogPersistido
    {
        public Guid Id { get; set; }
        public string Evento { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;
        public string PayloadResumen { get; set; } = string.Empty;
        public string Estado { get; set; } = "OMITIDO";
        public DateTimeOffset Fecha { get; set; }
    }
}
