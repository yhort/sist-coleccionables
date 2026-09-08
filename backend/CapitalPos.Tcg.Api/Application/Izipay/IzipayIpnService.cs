using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Izipay;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Izipay;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CapitalPos.Tcg.Api.Application.Izipay;

public sealed class IzipayIpnService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    IzipayHmacValidator hmac,
    IzipayCredentialProtector protector,
    ILogger<IzipayIpnService> logger)
{
    private static readonly JsonSerializerOptions JsonAnswer = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IntegracionIzipayResponse?> ObtenerConfigAsync(CancellationToken cancellationToken)
    {
        var integracion = await db.IntegracionesIzipay.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        return integracion is null ? null : MapConfig(integracion);
    }

    public async Task<IntegracionIzipayResponse> GuardarConfigAsync(
        GuardarIntegracionIzipayRequest request,
        CancellationToken cancellationToken)
    {
        var shopId = request.ShopId.Trim();
        if (shopId.Length == 0)
        {
            throw new BusinessRuleException("Indica el ShopId de Izipay.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var integracion = await db.IntegracionesIzipay.FirstOrDefaultAsync(cancellationToken);
        if (integracion is null)
        {
            if (string.IsNullOrWhiteSpace(request.HmacSha256Clave))
            {
                throw new BusinessRuleException("Indica la clave HMAC-SHA-256 del Back Office Izipay.");
            }

            integracion = new IntegracionIzipay
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                FechaCreacion = ahora
            };
            db.IntegracionesIzipay.Add(integracion);
        }

        integracion.ShopId = shopId;
        integracion.Modo = request.Modo;
        integracion.Activa = request.Activa;
        integracion.FechaActualizacion = ahora;
        if (!string.IsNullOrWhiteSpace(request.HmacSha256Clave))
        {
            integracion.HmacSha256Clave = protector.Cifrar(request.HmacSha256Clave.Trim());
        }

        if (string.IsNullOrWhiteSpace(integracion.HmacSha256Clave))
        {
            throw new BusinessRuleException("Indica la clave HMAC-SHA-256 del Back Office Izipay.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapConfig(integracion);
    }

    public async Task<IzipayIpnResult> ProcesarAsync(
        Guid empresaId,
        IzipayIpnForm form,
        CancellationToken cancellationToken)
    {
        if (empresaId == Guid.Empty)
        {
            return IzipayIpnResult.Unauthorized;
        }

        tenant.SetEmpresa(empresaId);

        var empresaActiva = await db.Empresas
            .AnyAsync(e => e.Id == empresaId && e.Activa, cancellationToken);
        if (!empresaActiva)
        {
            return IzipayIpnResult.Unauthorized;
        }

        var krAnswer = form.KrAnswer ?? string.Empty;
        var uuidPreliminar = ExtraerUuid(krAnswer) ?? HuellaRechazo(krAnswer, form.KrHash);

        var integracion = await db.IntegracionesIzipay.AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        var hmacPlano = DescifrarHmac(integracion);
        var firmaValida = hmacPlano is not null
            && hmac.EsValida(krAnswer, form.KrHash, hmacPlano, form.KrHashAlgorithm);

        if (!firmaValida)
        {
            await RegistrarRechazoFirmaAsync(empresaId, uuidPreliminar, krAnswer, cancellationToken);
            logger.LogWarning("IPN Izipay con firma inválida. Empresa {EmpresaId}", empresaId);
            return IzipayIpnResult.Unauthorized;
        }

        var answer = DeserializarAnswer(krAnswer);
        if (!ShopIdCoincide(integracion!, answer))
        {
            await RegistrarRechazoFirmaAsync(empresaId, uuidPreliminar, krAnswer, cancellationToken);
            logger.LogWarning("IPN Izipay con ShopId distinto al configurado. Empresa {EmpresaId}", empresaId);
            return IzipayIpnResult.Unauthorized;
        }

        var transaccion = answer.Transactions?.FirstOrDefault();
        var uuid = Truncar(PrimeraNoVacia(transaccion?.Uuid, uuidPreliminar), 80);

        var logExistente = await db.PasarelaWebhookLogs
            .FirstOrDefaultAsync(l => l.TransaccionUuid == uuid, cancellationToken);
        if (logExistente is { FirmaValida: true })
        {
            return IzipayIpnResult.Ok;
        }

        if (await PagoYaExisteAsync(uuid, cancellationToken))
        {
            await GuardarLogAsync(
                logExistente,
                empresaId,
                uuid,
                krAnswer,
                firmaValida: true,
                EstadoPasarelaWebhook.DUPLICADO,
                pagoId: null,
                cancellationToken);
            return IzipayIpnResult.Ok;
        }

        if (!EsPaid(answer.OrderStatus))
        {
            await GuardarLogAsync(
                logExistente,
                empresaId,
                uuid,
                krAnswer,
                firmaValida: true,
                EstadoPasarelaWebhook.IGNORADO_STATUS,
                pagoId: null,
                cancellationToken);
            return IzipayIpnResult.Ok;
        }

        var centavos = answer.OrderDetails?.OrderTotalAmount ?? transaccion?.Amount ?? 0;
        var monto = IgvCalculo.Round2(centavos / 100m);
        if (monto <= 0)
        {
            await GuardarLogAsync(
                logExistente,
                empresaId,
                uuid,
                krAnswer,
                firmaValida: true,
                EstadoPasarelaWebhook.IGNORADO_STATUS,
                pagoId: null,
                cancellationToken);
            return IzipayIpnResult.Ok;
        }

        var ahora = DateTimeOffset.UtcNow;
        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            EmpresaId = empresaId,
            Origen = MapearOrigen(transaccion),
            Estado = EstadoPago.NOTIFICADO,
            Monto = monto,
            CodigoOperacion = uuid,
            ReferenciaExterna = Truncar(
                PrimeraNoVacia(answer.OrderDetails?.OrderId, answer.ShopId, "IPN Izipay"),
                120),
            ClienteNombre = NombreCliente(answer),
            FechaNotificacion = ahora,
            Observacion = "Ingreso NOTIFICADO desde IPN Izipay (QR estático).",
            FechaCreacion = ahora
        };
        db.Pagos.Add(pago);

        AdjuntarLog(
            logExistente,
            empresaId,
            uuid,
            krAnswer,
            firmaValida: true,
            EstadoPasarelaWebhook.ACEPTADO,
            pago.Id);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionUnica(ex))
        {
            logger.LogInformation("IPN Izipay duplicado (uuid {Uuid}). Empresa {EmpresaId}", uuid, empresaId);
            return IzipayIpnResult.Ok;
        }

        logger.LogInformation(
            "IPN Izipay PAID registrado como NOTIFICADO {Origen} S/ {Monto}. Uuid {Uuid}",
            pago.Origen,
            pago.Monto,
            uuid);
        return IzipayIpnResult.Ok;
    }

    internal static OrigenPago MapearOrigen(IzipayTransaction? transaccion)
    {
        var blob = string.Join(
            ' ',
            transaccion?.PaymentMethodType,
            transaccion?.PaymentMethodBrand,
            transaccion?.TransactionDetails?.CardDetails?.EffectiveBrand);
        if (blob.Contains("YAPE", StringComparison.OrdinalIgnoreCase))
        {
            return OrigenPago.YAPE;
        }

        if (blob.Contains("PLIN", StringComparison.OrdinalIgnoreCase))
        {
            return OrigenPago.PLIN;
        }

        return OrigenPago.IZIPAY;
    }

    private IntegracionIzipayResponse MapConfig(IntegracionIzipay integracion)
    {
        var enmascarada = string.Empty;
        var tieneHmac = !string.IsNullOrWhiteSpace(integracion.HmacSha256Clave);
        if (tieneHmac)
        {
            try
            {
                enmascarada = IzipayCredentialProtector.Enmascarar(protector.Descifrar(integracion.HmacSha256Clave));
            }
            catch (CryptographicException)
            {
                enmascarada = "****";
            }
        }

        return new IntegracionIzipayResponse
        {
            Id = integracion.Id,
            ShopId = integracion.ShopId,
            HmacSha256Enmascarada = enmascarada,
            TieneHmac = tieneHmac,
            Modo = integracion.Modo,
            Activa = integracion.Activa
        };
    }

    private string? DescifrarHmac(IntegracionIzipay? integracion)
    {
        if (integracion is not { Activa: true } || string.IsNullOrWhiteSpace(integracion.HmacSha256Clave))
        {
            return null;
        }

        try
        {
            var plano = protector.Descifrar(integracion.HmacSha256Clave);
            return string.IsNullOrWhiteSpace(plano) ? null : plano;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private async Task RegistrarRechazoFirmaAsync(
        Guid empresaId,
        string uuid,
        string payload,
        CancellationToken cancellationToken)
    {
        var existente = await db.PasarelaWebhookLogs
            .FirstOrDefaultAsync(l => l.TransaccionUuid == uuid, cancellationToken);
        if (existente is { FirmaValida: true })
        {
            return;
        }

        if (existente is not null)
        {
            return;
        }

        db.PasarelaWebhookLogs.Add(NuevoLog(
            empresaId,
            uuid,
            payload,
            firmaValida: false,
            EstadoPasarelaWebhook.RECHAZADO_FIRMA,
            pagoId: null));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionUnica(ex))
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task GuardarLogAsync(
        PasarelaWebhookLog? existente,
        Guid empresaId,
        string uuid,
        string payload,
        bool firmaValida,
        EstadoPasarelaWebhook estado,
        Guid? pagoId,
        CancellationToken cancellationToken)
    {
        AdjuntarLog(existente, empresaId, uuid, payload, firmaValida, estado, pagoId);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (EsViolacionUnica(ex))
        {
            db.ChangeTracker.Clear();
        }
    }

    private void AdjuntarLog(
        PasarelaWebhookLog? existente,
        Guid empresaId,
        string uuid,
        string payload,
        bool firmaValida,
        EstadoPasarelaWebhook estado,
        Guid? pagoId)
    {
        if (existente is null)
        {
            db.PasarelaWebhookLogs.Add(NuevoLog(empresaId, uuid, payload, firmaValida, estado, pagoId));
            return;
        }

        existente.FirmaValida = firmaValida;
        existente.PayloadRaw = Truncar(payload, 32_000);
        existente.Estado = estado;
        existente.PagoId = pagoId ?? existente.PagoId;
        existente.CreatedAt = DateTimeOffset.UtcNow;
    }

    private static PasarelaWebhookLog NuevoLog(
        Guid empresaId,
        string uuid,
        string payload,
        bool firmaValida,
        EstadoPasarelaWebhook estado,
        Guid? pagoId) => new()
    {
        Id = Guid.NewGuid(),
        EmpresaId = empresaId,
        Proveedor = ProveedorPasarela.IZIPAY,
        TransaccionUuid = uuid,
        FirmaValida = firmaValida,
        PayloadRaw = Truncar(payload, 32_000),
        Estado = estado,
        PagoId = pagoId,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private async Task<bool> PagoYaExisteAsync(string uuid, CancellationToken cancellationToken) =>
        await db.Pagos.AnyAsync(
            p => p.CodigoOperacion == uuid && p.Estado != EstadoPago.RECHAZADO,
            cancellationToken);

    private static IzipayKrAnswer DeserializarAnswer(string krAnswer)
    {
        try
        {
            return JsonSerializer.Deserialize<IzipayKrAnswer>(krAnswer, JsonAnswer) ?? new IzipayKrAnswer();
        }
        catch (JsonException)
        {
            return new IzipayKrAnswer();
        }
    }

    private static string? ExtraerUuid(string krAnswer)
    {
        var answer = DeserializarAnswer(krAnswer);
        var uuid = answer.Transactions?.FirstOrDefault()?.Uuid;
        return string.IsNullOrWhiteSpace(uuid) ? null : Truncar(uuid.Trim(), 80);
    }

    private static string HuellaRechazo(string krAnswer, string? krHash)
    {
        var material = Encoding.UTF8.GetBytes($"{krAnswer}\n{krHash}");
        var hash = SHA256.HashData(material);
        return Truncar("rej-" + Convert.ToHexString(hash).ToLowerInvariant(), 80);
    }

    private static bool ShopIdCoincide(IntegracionIzipay integracion, IzipayKrAnswer answer)
    {
        if (string.IsNullOrWhiteSpace(integracion.ShopId) || string.IsNullOrWhiteSpace(answer.ShopId))
        {
            return true;
        }

        return string.Equals(integracion.ShopId.Trim(), answer.ShopId.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool EsPaid(string? orderStatus) =>
        string.Equals(orderStatus, "PAID", StringComparison.OrdinalIgnoreCase);

    private static string? NombreCliente(IzipayKrAnswer answer)
    {
        var billing = answer.Customer?.BillingDetails;
        var nombre = $"{billing?.FirstName} {billing?.LastName}".Trim();
        if (nombre.Length > 0)
        {
            return Truncar(nombre, 160);
        }

        return string.IsNullOrWhiteSpace(answer.Customer?.Email)
            ? null
            : Truncar(answer.Customer.Email, 160);
    }

    private static string Truncar(string valor, int max) =>
        valor.Length <= max ? valor : valor[..max];

    private static string PrimeraNoVacia(params string?[] valores)
    {
        foreach (var valor in valores)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor.Trim();
            }
        }

        return string.Empty;
    }

    private static bool EsViolacionUnica(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
