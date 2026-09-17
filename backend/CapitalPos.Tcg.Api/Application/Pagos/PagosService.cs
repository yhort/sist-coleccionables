using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Application.Common;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pagos;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Pagos;

public sealed class PagosService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    PedidosDigitalesService pedidos)
{
    public async Task<IReadOnlyList<PagoResponse>> ListarAsync(
        EstadoPago? estado,
        OrigenPago? origen,
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        Guid? pedidoDigitalId,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();
        if (estado.HasValue)
        {
            query = query.Where(p => p.Estado == estado.Value);
        }

        if (origen.HasValue)
        {
            query = query.Where(p => p.Origen == origen.Value);
        }

        if (desde.HasValue)
        {
            query = query.Where(p => p.FechaNotificacion >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(p => p.FechaNotificacion <= hasta.Value);
        }

        if (pedidoDigitalId.HasValue && pedidoDigitalId.Value != Guid.Empty)
        {
            query = query.Where(p => p.PedidoDigitalId == pedidoDigitalId.Value);
        }

        var pagos = await query
            .OrderByDescending(p => p.FechaNotificacion)
            .ToListAsync(cancellationToken);

        return pagos.Select(Map).ToList();
    }

    public async Task<PagoResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var pago = await QueryBase().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pago is null ? null : Map(pago);
    }

    public async Task<PagoResponse> RegistrarAsync(
        RegistrarPagoRequest request,
        CancellationToken cancellationToken)
    {
        var monto = IgvCalculo.Round2(request.Monto);
        if (monto <= 0)
        {
            throw new BusinessRuleException("El monto debe ser mayor que cero.");
        }

        var codigo = NormalizarCodigo(request.CodigoOperacion);
        if (EsOrigenDigital(request.Origen) && codigo is null)
        {
            // Cobro POS / confirmación inmediata: genera referencia si la cajera no la ingresó.
            if (request.Confirmar && request.PedidoDigitalId is not null)
            {
                codigo = $"POS-{DateTimeOffset.UtcNow:yyMMddHHmmssfff}";
            }
            else
            {
                throw new BusinessRuleException("Indica el código de operación Yape, Plin, Izipay o tarjeta.");
            }
        }

        await AsegurarCodigoLibreAsync(codigo, null, cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var pago = new Pago
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Origen = request.Origen,
            Estado = EstadoPago.NOTIFICADO,
            Monto = monto,
            CodigoOperacion = codigo,
            ReferenciaExterna = TextoOpcional(request.ReferenciaExterna, 120),
            ClienteNombre = TextoOpcional(request.ClienteNombre, 160),
            FechaNotificacion = ahora,
            Observacion = TextoOpcional(request.Observacion, 500),
            FechaCreacion = ahora
        };

        if (request.PedidoDigitalId is { } pedidoId)
        {
            var pedido = await RequierePendientePagoAsync(pedidoId, cancellationToken);
            await AsegurarNoExcedeTotalAsync(pedido, monto, null, [EstadoPago.ASOCIADO, EstadoPago.CONFIRMADO], cancellationToken);
            pago.PedidoDigitalId = pedido.Id;
            pago.ClienteNombre = pedido.ClienteNombre;
            pago.Estado = EstadoPago.ASOCIADO;
            pago.UsuarioAsocioId = currentUser.UserId;
        }

        db.Pagos.Add(pago);
        await db.SaveChangesAsync(cancellationToken);

        if (request.Confirmar)
        {
            return await ConfirmarAsync(pago.Id, cancellationToken);
        }

        return (await ObtenerAsync(pago.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<PagoResponse>> RegistrarLoteAsync(
        RegistrarPagoLoteRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.PedidoDigitalIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Selecciona al menos un pedido pendiente de pago.");
        }

        var monto = IgvCalculo.Round2(request.Monto);
        if (monto <= 0)
        {
            throw new BusinessRuleException("El monto debe ser mayor que cero.");
        }

        var codigoBase = NormalizarCodigo(request.CodigoOperacion);
        if (EsOrigenDigital(request.Origen) && codigoBase is null)
        {
            if (request.Confirmar)
            {
                codigoBase = $"POS-{DateTimeOffset.UtcNow:yyMMddHHmmssfff}";
            }
            else
            {
                throw new BusinessRuleException("Indica el código de operación Yape, Plin, Izipay o tarjeta.");
            }
        }

        var pedidos = await db.PedidosDigitales
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (pedidos.Count != ids.Count)
        {
            throw new BusinessRuleException("Uno o más pedidos no existen o no pertenecen a la empresa.");
        }

        if (pedidos.Any(p => p.Estado != EstadoPedidoDigital.PendientePago))
        {
            throw new BusinessRuleException("Solo se cobran pedidos en Pendiente de pago.");
        }

        if (!MismoCliente(pedidos))
        {
            throw new BusinessRuleException("Solo puedes cobrar juntos pedidos del mismo cliente.");
        }

        var ordenados = ids
            .Select(id => pedidos.First(p => p.Id == id))
            .ToList();
        var saldos = new List<(PedidoDigital Pedido, decimal Saldo)>(ordenados.Count);
        foreach (var pedido in ordenados)
        {
            var cubierto = await MontoCubiertoAsync(
                pedido.Id,
                [EstadoPago.ASOCIADO, EstadoPago.CONFIRMADO],
                null,
                cancellationToken);
            var saldo = IgvCalculo.Round2(Math.Max(0, pedido.Total - cubierto));
            if (saldo <= 0)
            {
                throw new BusinessRuleException("Hay pedidos sin saldo pendiente de cobro.");
            }

            saldos.Add((pedido, saldo));
        }

        var sumaSaldos = IgvCalculo.Round2(saldos.Sum(s => s.Saldo));
        if (Math.Abs(sumaSaldos - monto) > IgvCalculo.ToleranciaPago)
        {
            throw new BusinessRuleException(
                $"El monto debe coincidir con el total de los pedidos (S/ {sumaSaldos:0.00}).");
        }

        var ahora = DateTimeOffset.UtcNow;
        var codigos = saldos
            .Select((_, i) => CodigoLote(codigoBase, i, saldos.Count))
            .ToList();
        foreach (var codigo in codigos.Where(c => c is not null).Distinct())
        {
            await AsegurarCodigoLibreAsync(codigo, null, cancellationToken);
        }

        var referencia = TextoOpcional(request.ReferenciaExterna, 120);
        var notaBase = TextoOpcional(request.Observacion, 500);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var pagos = new List<Pago>(saldos.Count);
        for (var i = 0; i < saldos.Count; i++)
        {
            var (pedido, saldo) = saldos[i];
            var pago = new Pago
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                Origen = request.Origen,
                Estado = EstadoPago.ASOCIADO,
                Monto = saldo,
                CodigoOperacion = codigos[i],
                ReferenciaExterna = referencia,
                PedidoDigitalId = pedido.Id,
                ClienteNombre = pedido.ClienteNombre,
                FechaNotificacion = ahora,
                UsuarioAsocioId = currentUser.UserId,
                Observacion = NotaLote(notaBase, i, saldos.Count, codigoBase),
                FechaCreacion = ahora
            };
            db.Pagos.Add(pago);
            pagos.Add(pago);
        }

        await db.SaveChangesAsync(cancellationToken);

        if (request.Confirmar)
        {
            foreach (var pago in pagos)
            {
                pago.Estado = EstadoPago.CONFIRMADO;
                pago.FechaConfirmacion = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(cancellationToken);
            foreach (var pago in pagos)
            {
                await MarcarPedidoPagadoSiCubreAsync(pago, cancellationToken);
            }
        }

        await tx.CommitAsync(cancellationToken);

        var idsPagos = pagos.Select(p => p.Id).ToList();
        var creados = await QueryBase()
            .Where(p => idsPagos.Contains(p.Id))
            .OrderBy(p => p.FechaCreacion)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);
        return creados.Select(Map).ToList();
    }

    public async Task<PagoResponse> AsociarAsync(
        Guid id,
        Guid pedidoDigitalId,
        CancellationToken cancellationToken)
    {
        var pago = await CargarAsync(id, cancellationToken);
        if (pago.Estado == EstadoPago.RECHAZADO)
        {
            throw new BusinessRuleException("No se puede asociar un pago rechazado.");
        }

        if (pago.Estado == EstadoPago.CONFIRMADO)
        {
            throw new BusinessRuleException("El pago ya está confirmado.");
        }

        var pedido = await RequierePendientePagoAsync(pedidoDigitalId, cancellationToken);
        await AsegurarNoExcedeTotalAsync(
            pedido,
            pago.Monto,
            pago.Id,
            [EstadoPago.ASOCIADO, EstadoPago.CONFIRMADO],
            cancellationToken);

        pago.Estado = EstadoPago.ASOCIADO;
        pago.PedidoDigitalId = pedido.Id;
        pago.ClienteNombre = pedido.ClienteNombre;
        pago.UsuarioAsocioId = currentUser.UserId;
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<PagoResponse> ConfirmarAsync(Guid id, CancellationToken cancellationToken)
    {
        var pago = await CargarAsync(id, cancellationToken);
        if (pago.Estado == EstadoPago.RECHAZADO)
        {
            throw new BusinessRuleException("No se puede confirmar un pago rechazado.");
        }

        if (pago.Estado != EstadoPago.CONFIRMADO)
        {
            if (pago.PedidoDigitalId is not null && pago.Estado != EstadoPago.ASOCIADO)
            {
                throw new BusinessRuleException("Asocia el pago a un pedido antes de confirmarlo.");
            }

            if (pago.PedidoDigitalId is { } pedidoId)
            {
                var pedido = await db.PedidosDigitales.FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken)
                    ?? throw new BusinessRuleException("No se encontró el pedido digital asociado.");
                await AsegurarNoExcedeTotalAsync(pedido, pago.Monto, pago.Id, [EstadoPago.CONFIRMADO], cancellationToken);
            }

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            pago.Estado = EstadoPago.CONFIRMADO;
            pago.FechaConfirmacion = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await MarcarPedidoPagadoSiCubreAsync(pago, cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        else
        {
            await MarcarPedidoPagadoSiCubreAsync(pago, cancellationToken);
        }

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<PagoResponse> RechazarAsync(
        Guid id,
        string? observacion,
        CancellationToken cancellationToken)
    {
        var pago = await CargarAsync(id, cancellationToken);
        if (pago.Estado == EstadoPago.CONFIRMADO)
        {
            throw new BusinessRuleException("No se puede rechazar un pago ya confirmado.");
        }

        if (pago.Estado == EstadoPago.RECHAZADO)
        {
            return Map(pago);
        }

        pago.Estado = EstadoPago.RECHAZADO;
        var nota = TextoOpcional(observacion, 500);
        if (nota is not null)
        {
            pago.Observacion = nota;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    private async Task MarcarPedidoPagadoSiCubreAsync(Pago pago, CancellationToken cancellationToken)
    {
        if (pago.PedidoDigitalId is not { } pedidoId)
        {
            return;
        }

        var pedido = await db.PedidosDigitales.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);
        if (pedido is null || pedido.Estado != EstadoPedidoDigital.PendientePago)
        {
            return;
        }

        var cubierto = await MontoCubiertoAsync(pedido.Id, [EstadoPago.CONFIRMADO], null, cancellationToken);
        if (cubierto + 0.000000001m < pedido.Total)
        {
            return;
        }

        var etiqueta = pago.CodigoOperacion ?? pago.Id.ToString();
        await pedidos.MarcarPagadoPorPagoAsync(
            pedido.Id,
            $"Pago {pago.Origen} confirmado ({etiqueta}).",
            cancellationToken);
    }

    private async Task AsegurarCodigoLibreAsync(
        string? codigo,
        Guid? excluirId,
        CancellationToken cancellationToken)
    {
        if (codigo is null)
        {
            return;
        }

        var existe = await db.Pagos.AnyAsync(
            p => p.CodigoOperacion == codigo
                && p.Estado != EstadoPago.RECHAZADO
                && (excluirId == null || p.Id != excluirId),
            cancellationToken);
        if (existe)
        {
            throw new BusinessRuleException($"Ya existe un pago con el código de operación {codigo}.");
        }
    }

    private async Task AsegurarNoExcedeTotalAsync(
        PedidoDigital pedido,
        decimal montoNuevo,
        Guid? excluirPagoId,
        EstadoPago[] estados,
        CancellationToken cancellationToken)
    {
        var cubierto = await MontoCubiertoAsync(pedido.Id, estados, excluirPagoId, cancellationToken);
        if (IgvCalculo.Round2(cubierto + montoNuevo) - pedido.Total > IgvCalculo.ToleranciaPago)
        {
            var saldo = IgvCalculo.Round2(pedido.Total - cubierto);
            throw new BusinessRuleException(
                $"El pago excede el total del pedido (S/ {pedido.Total:0.00}). Saldo: S/ {saldo:0.00}.");
        }
    }

    private async Task<decimal> MontoCubiertoAsync(
        Guid pedidoId,
        EstadoPago[] estados,
        Guid? excluirPagoId,
        CancellationToken cancellationToken)
    {
        var suma = await db.Pagos
            .Where(p => p.PedidoDigitalId == pedidoId
                && estados.Contains(p.Estado)
                && (excluirPagoId == null || p.Id != excluirPagoId))
            .SumAsync(p => (decimal?)p.Monto, cancellationToken);
        return IgvCalculo.Round2(suma ?? 0);
    }

    private async Task<PedidoDigital> RequierePendientePagoAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await db.PedidosDigitales.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("Selecciona un pedido digital pendiente.");
        if (pedido.Estado != EstadoPedidoDigital.PendientePago)
        {
            throw new BusinessRuleException("Solo se asocian pagos a pedidos en Pendiente de pago.");
        }

        return pedido;
    }

    private IQueryable<Pago> QueryBase() =>
        db.Pagos.AsNoTracking().Include(p => p.UsuarioAsocio);

    private async Task<Pago> CargarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Pagos.Include(p => p.UsuarioAsocio)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró el pago.", StatusCodes.Status404NotFound);

    private static bool MismoCliente(IReadOnlyList<PedidoDigital> pedidos)
    {
        if (pedidos.Count <= 1)
        {
            return true;
        }

        var nombres = pedidos
            .Select(p => (p.ClienteNombre ?? string.Empty).Trim().ToUpperInvariant())
            .Where(nombre => nombre.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (nombres.Count == 1)
        {
            return true;
        }

        var ids = pedidos
            .Select(p => p.ClienteId)
            .Where(id => id is { } valor && valor != Guid.Empty)
            .Distinct()
            .ToList();
        return ids.Count == 1 && pedidos.All(p => p.ClienteId == ids[0]);
    }

    private static string? CodigoLote(string? codigoBase, int index, int total)
    {
        if (codigoBase is null)
        {
            return null;
        }

        if (total <= 1 || index == 0)
        {
            return codigoBase;
        }

        var sufijo = $"-{index + 1}";
        var max = 80 - sufijo.Length;
        var raiz = codigoBase.Length <= max ? codigoBase : codigoBase[..max];
        return raiz + sufijo;
    }

    private static string? NotaLote(string? nota, int index, int total, string? codigoBase)
    {
        var partes = new List<string>();
        if (total > 1)
        {
            partes.Add($"Pago conjunto live {index + 1}/{total}");
            if (!string.IsNullOrEmpty(codigoBase) && index > 0)
            {
                partes.Add($"código {codigoBase}");
            }
        }

        if (nota is not null)
        {
            partes.Add(nota);
        }

        if (partes.Count == 0)
        {
            return null;
        }

        var texto = string.Join(". ", partes);
        return texto[..Math.Min(texto.Length, 500)];
    }

    private static bool EsOrigenDigital(OrigenPago origen) =>
        origen is OrigenPago.YAPE or OrigenPago.PLIN or OrigenPago.IZIPAY or OrigenPago.TARJETA;

    private static string? NormalizarCodigo(string? codigo)
    {
        var texto = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, 80)];
    }

    private static string? TextoOpcional(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    private static PagoResponse Map(Pago pago) => new()
    {
        Id = pago.Id,
        Origen = pago.Origen,
        Estado = pago.Estado,
        Monto = pago.Monto,
        CodigoOperacion = pago.CodigoOperacion,
        ReferenciaExterna = pago.ReferenciaExterna,
        PedidoDigitalId = pago.PedidoDigitalId,
        PedidoCodigo = CodigoAmigable.Pedido(pago.PedidoDigitalId),
        VentaId = pago.VentaId,
        ClienteNombre = pago.ClienteNombre,
        FechaNotificacion = pago.FechaNotificacion,
        FechaConfirmacion = pago.FechaConfirmacion,
        UsuarioAsocioId = pago.UsuarioAsocioId,
        UsuarioAsocioNombre = pago.UsuarioAsocio?.Nombre,
        Observacion = pago.Observacion
    };
}
