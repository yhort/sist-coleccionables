using CapitalPos.Tcg.Api.Application.Caja;
using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Cpe;

/// <summary>
/// Efectos operativos de una NC aceptada: kardex, caja y estados de pedidos.
/// Idempotente; el caller envuelve en la misma transacción que la emisión.
/// </summary>
public sealed class NotaCreditoImpactoService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    KardexWriter kardex,
    CajaService caja)
{
    public async Task AplicarTrasEmisionExitosaAsync(
        Guid ventaId,
        ComprobanteResponse notaCredito,
        EmitirNotaCreditoRequest motivo,
        CancellationToken cancellationToken)
    {
        if (!EsEmisionExitosa(notaCredito.Estado))
        {
            return;
        }

        var venta = await db.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .FirstOrDefaultAsync(v => v.Id == ventaId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la venta de la nota de crédito.", StatusCodes.Status404NotFound);

        var lineas = NotaCreditoLineas.Resolver(venta, motivo);
        var anulacionTotal = NotaCreditoLineas.EsAnulacionTotal(venta, motivo);

        var pedidos = await db.PedidosDigitales
            .Include(p => p.Detalles)
            .Include(p => p.Historial)
            .Where(p => p.VentaId == venta.Id)
            .ToListAsync(cancellationToken);

        await RevertirInventarioAsync(venta, pedidos, lineas, notaCredito, cancellationToken);
        ActualizarPedidos(pedidos, notaCredito, motivo, anulacionTotal);
        await caja.RegistrarReversoNotaCreditoAsync(
            venta,
            notaCredito,
            motivo.CodigoMotivo,
            anulacionTotal,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RevertirInventarioAsync(
        Venta venta,
        IReadOnlyList<PedidoDigital> pedidos,
        IReadOnlyList<NotaCreditoLineas.LineaResuelta> lineas,
        ComprobanteResponse notaCredito,
        CancellationToken cancellationToken)
    {
        if (lineas.Count == 0)
        {
            return;
        }

        var etiqueta = $"{notaCredito.Serie}-{notaCredito.Correlativo:00000000}";
        var marcaRef = $"NOTA_CREDITO:{notaCredito.Id:N}";

        var yaAplicado = await db.MovimientosInventario.AnyAsync(
            m => m.ReferenciaId == venta.Id
                && m.ReferenciaTipo == marcaRef
                && (m.TipoMovimiento == TipoMovimientoInventario.ANULACION_VENTA
                    || m.TipoMovimiento == TipoMovimientoInventario.LIBERACION_RESERVA),
            cancellationToken);
        if (yaAplicado)
        {
            return;
        }

        var huboVenta = pedidos.Any(p => p.IndicadorReserva == IndicadorReservaPedido.Confirmado)
            || await db.MovimientosInventario.AnyAsync(
                m => m.ReferenciaId == venta.Id && m.TipoMovimiento == TipoMovimientoInventario.VENTA,
                cancellationToken);

        if (huboVenta)
        {
            await kardex.AplicarMuchosAsync(
                lineas.Select(l => new KardexComando(
                    venta.SedeId,
                    l.ProductoId,
                    TipoMovimientoInventario.ANULACION_VENTA,
                    l.Cantidad,
                    $"Entrada por anulación NC {etiqueta} · {l.Descripcion}",
                    marcaRef,
                    venta.Id)),
                cancellationToken);

            foreach (var pedido in pedidos.Where(p => p.IndicadorReserva == IndicadorReservaPedido.Confirmado))
            {
                // Solo libera el indicador si la NC cubre todo el stock confirmado del pedido.
                if (PedidoCubiertoPorLineas(pedido, lineas))
                {
                    pedido.IndicadorReserva = IndicadorReservaPedido.Liberado;
                }
            }

            return;
        }

        var pendientesReserva = pedidos
            .Where(p => p.IndicadorReserva == IndicadorReservaPedido.Reservado)
            .ToList();
        if (pendientesReserva.Count == 0)
        {
            return;
        }

        var restante = lineas.ToDictionary(l => l.ProductoId, l => l.Cantidad);
        var comandos = new List<KardexComando>();
        foreach (var pedido in pendientesReserva)
        {
            var liberadoTodo = true;
            foreach (var detalle in pedido.Detalles)
            {
                if (!restante.TryGetValue(detalle.ProductoId, out var disponible) || disponible <= 0)
                {
                    if (detalle.Cantidad > 0)
                    {
                        liberadoTodo = false;
                    }

                    continue;
                }

                var qty = Math.Min(detalle.Cantidad, disponible);
                restante[detalle.ProductoId] = disponible - qty;
                if (qty < detalle.Cantidad)
                {
                    liberadoTodo = false;
                }

                if (qty > 0)
                {
                    comandos.Add(new KardexComando(
                        pedido.SedeId,
                        detalle.ProductoId,
                        TipoMovimientoInventario.LIBERACION_RESERVA,
                        qty,
                        $"Liberación por NC {etiqueta} · pedido {pedido.Id:N} · {detalle.Descripcion}",
                        marcaRef,
                        venta.Id));
                }
            }

            if (liberadoTodo)
            {
                pedido.IndicadorReserva = IndicadorReservaPedido.Liberado;
            }
        }

        if (comandos.Count > 0)
        {
            await kardex.AplicarMuchosAsync(comandos, cancellationToken);
        }
    }

    private static bool PedidoCubiertoPorLineas(
        PedidoDigital pedido,
        IReadOnlyList<NotaCreditoLineas.LineaResuelta> lineas)
    {
        var mapa = lineas
            .GroupBy(l => l.ProductoId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Cantidad));
        foreach (var detalle in pedido.Detalles)
        {
            if (!mapa.TryGetValue(detalle.ProductoId, out var qty) || qty < detalle.Cantidad)
            {
                return false;
            }
        }

        return pedido.Detalles.Count > 0;
    }

    private void ActualizarPedidos(
        IReadOnlyList<PedidoDigital> pedidos,
        ComprobanteResponse notaCredito,
        EmitirNotaCreditoRequest motivo,
        bool anulacionTotal)
    {
        if (pedidos.Count == 0)
        {
            return;
        }

        var etiqueta = $"{notaCredito.Serie}-{notaCredito.Correlativo:00000000}";
        var codigo = Catalogo09Motivos.NormalizarCodigo(motivo.CodigoMotivo);

        if (!anulacionTotal)
        {
            var obsParcial =
                $"NC parcial {etiqueta} ({codigo}) · {motivo.DescripcionMotivo}. Stock/caja proporcionales; el pedido sigue operativo.";
            foreach (var pedido in pedidos)
            {
                if (pedido.Estado is EstadoPedidoDigital.Anulado or EstadoPedidoDigital.Devuelto)
                {
                    continue;
                }

                var evento = new PedidoDigitalHistorialEstado
                {
                    Id = Guid.NewGuid(),
                    PedidoDigitalId = pedido.Id,
                    EmpresaId = tenant.EmpresaId,
                    EstadoAnterior = pedido.Estado,
                    EstadoNuevo = pedido.Estado,
                    UsuarioId = currentUser.UserId,
                    Fecha = DateTimeOffset.UtcNow,
                    Observacion = obsParcial[..Math.Min(obsParcial.Length, 500)]
                };
                pedido.Historial.Add(evento);
                db.PedidoDigitalHistorialEstados.Add(evento);
            }

            return;
        }

        var estadoDestino = Catalogo09Motivos.EstadoPedidoTrasMotivo(motivo.CodigoMotivo);
        var observacion =
            $"Nota de crédito {etiqueta} ({codigo}) · {motivo.DescripcionMotivo}. Pedido {estadoDestino}.";

        foreach (var pedido in pedidos)
        {
            if (pedido.Estado is EstadoPedidoDigital.Anulado or EstadoPedidoDigital.Devuelto)
            {
                continue;
            }

            var anterior = pedido.Estado;
            pedido.Estado = estadoDestino;
            pedido.IndicadorReserva = IndicadorReservaPedido.Liberado;

            var evento = new PedidoDigitalHistorialEstado
            {
                Id = Guid.NewGuid(),
                PedidoDigitalId = pedido.Id,
                EmpresaId = tenant.EmpresaId,
                EstadoAnterior = anterior,
                EstadoNuevo = estadoDestino,
                UsuarioId = currentUser.UserId,
                Fecha = DateTimeOffset.UtcNow,
                Observacion = observacion[..Math.Min(observacion.Length, 500)]
            };
            pedido.Historial.Add(evento);
            db.PedidoDigitalHistorialEstados.Add(evento);
        }
    }

    private static bool EsEmisionExitosa(EstadoEmisionSunat estado) =>
        estado is EstadoEmisionSunat.ACEPTADO or EstadoEmisionSunat.SIMULADO;
}
