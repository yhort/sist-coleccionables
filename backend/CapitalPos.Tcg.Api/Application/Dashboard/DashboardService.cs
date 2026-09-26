using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Dashboard;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Dashboard;

public sealed class DashboardService(ApplicationDbContext db)
{
    public const decimal UmbralStockBajo = 3m;

    public async Task<DashboardResumenResponse> ResumenAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var fechaLocal = ZonaHorariaPeru.FechaLocal(ahora);
        var (inicio, fin) = ZonaHorariaPeru.RangoUtcDelDia(fechaLocal);

        var ventasHoy = await db.Ventas.AsNoTracking()
            .Where(v => !v.EsConsolidacion && v.Fecha >= inicio && v.Fecha < fin)
            .Select(v => new { v.Id, v.Total, v.PedidoDigitalId })
            .ToListAsync(cancellationToken);

        var pagosHoySinVenta = await db.Pagos.AsNoTracking()
            .Where(p =>
                p.Estado == EstadoPago.CONFIRMADO
                && p.VentaId == null
                && ((p.FechaConfirmacion ?? p.FechaNotificacion) >= inicio)
                && ((p.FechaConfirmacion ?? p.FechaNotificacion) < fin))
            .SumAsync(p => (decimal?)p.Monto, cancellationToken) ?? 0m;

        var comprobantesEmitidos = await db.Comprobantes.AsNoTracking()
            .CountAsync(c => c.FechaEmision >= inicio && c.FechaEmision < fin, cancellationToken);

        var subastasActivas = await db.SubastasTcg.AsNoTracking()
            .CountAsync(s => s.Estado == EstadoSubastaTcg.ACTIVA, cancellationToken);

        var adjudicadasPendientePago = await db.SubastasTcg.AsNoTracking()
            .Where(s => s.Estado == EstadoSubastaTcg.ADJUDICADA && s.PedidoDigitalId != null)
            .Join(
                db.PedidosDigitales.AsNoTracking(),
                s => s.PedidoDigitalId,
                p => p.Id,
                (_, p) => p)
            .CountAsync(p => p.Estado == EstadoPedidoDigital.PendientePago, cancellationToken);

        var cerradasConGanador = await db.SubastasTcg.AsNoTracking()
            .CountAsync(
                s => s.Estado == EstadoSubastaTcg.CERRADA && s.PujaGanadoraId != null,
                cancellationToken);

        var ipnQuery = db.Pagos.AsNoTracking()
            .Where(p =>
                p.Estado == EstadoPago.NOTIFICADO
                && p.PedidoDigitalId == null
                && (p.Origen == OrigenPago.IZIPAY || p.Origen == OrigenPago.YAPE || p.Origen == OrigenPago.PLIN));

        var ipnPendientes = await ipnQuery.CountAsync(cancellationToken);
        var ipnMonto = await ipnQuery.SumAsync(p => (decimal?)p.Monto, cancellationToken) ?? 0m;

        var stockBajo = await db.StocksProductos.AsNoTracking()
            .Where(s => s.Producto.Activo && s.CantidadLibre <= UmbralStockBajo)
            .Select(s => s.ProductoId)
            .Distinct()
            .CountAsync(cancellationToken);

        var actividad = await CargarActividadRecienteAsync(cancellationToken);

        return new DashboardResumenResponse
        {
            FechaLocal = fechaLocal,
            GeneradoEn = ahora,
            VentasHoy = new VentasDiaKpi
            {
                TotalRecaudado = IgvCalculo.Round2(ventasHoy.Sum(v => v.Total) + pagosHoySinVenta),
                ComprobantesEmitidos = comprobantesEmitidos,
                VentasRegistradas = ventasHoy.Count
            },
            Subastas = new SubastasKpi
            {
                Activas = subastasActivas,
                GanadoresPendientePago = adjudicadasPendientePago + cerradasConGanador
            },
            PagosNotificados = new PagosIpnKpi
            {
                PendientesAsociar = ipnPendientes,
                MontoPendiente = IgvCalculo.Round2(ipnMonto)
            },
            StockBajo = new StockBajoKpi
            {
                Cantidad = stockBajo,
                Umbral = UmbralStockBajo
            },
            ActividadReciente = actividad
        };
    }

    private async Task<IReadOnlyList<ActividadRecienteItem>> CargarActividadRecienteAsync(
        CancellationToken cancellationToken)
    {
        var pedidos = await db.PedidosDigitales.AsNoTracking()
            .Include(p => p.Cliente)
            .Include(p => p.Pagos)
            .OrderByDescending(p => p.FechaPedido)
            .Take(5)
            .ToListAsync(cancellationToken);

        var ventasPos = await db.Ventas.AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Pagos)
            .Where(v => !v.EsConsolidacion && v.PedidoDigitalId == null)
            .OrderByDescending(v => v.Fecha)
            .Take(5)
            .ToListAsync(cancellationToken);

        return pedidos.Select(MapPedido)
            .Concat(ventasPos.Select(MapVenta))
            .OrderByDescending(item => item.Fecha)
            .Take(5)
            .ToList();
    }

    private static ActividadRecienteItem MapPedido(PedidoDigital pedido)
    {
        var metodos = pedido.Pagos
            .Where(p => p.Estado is not EstadoPago.RECHAZADO and not EstadoPago.ANULADO)
            .OrderByDescending(p => p.FechaNotificacion)
            .Select(p => EtiquetaOrigen(p.Origen))
            .Distinct()
            .ToList();

        return new ActividadRecienteItem
        {
            Id = pedido.Id,
            Tipo = "PEDIDO",
            ClienteNombre = NombreCliente(pedido.ClienteNombre, pedido.Cliente?.Nombre),
            Monto = IgvCalculo.Round2(pedido.Total),
            Estado = EtiquetaEstadoPedido(pedido.Estado),
            MetodoPago = metodos.Count == 0 ? "Pendiente" : string.Join(" · ", metodos),
            Fecha = pedido.FechaPedido
        };
    }

    private static ActividadRecienteItem MapVenta(Venta venta)
    {
        var metodos = venta.Pagos
            .Select(p => EtiquetaOrigen(p.Origen))
            .Distinct()
            .ToList();

        return new ActividadRecienteItem
        {
            Id = venta.Id,
            Tipo = "VENTA",
            ClienteNombre = NombreCliente(null, venta.Cliente?.Nombre),
            Monto = IgvCalculo.Round2(venta.Total),
            Estado = "Venta POS",
            MetodoPago = metodos.Count == 0 ? "—" : string.Join(" · ", metodos),
            Fecha = venta.Fecha
        };
    }

    private static string NombreCliente(string? directo, string? maestro)
    {
        var nombre = (directo ?? maestro)?.Trim();
        return string.IsNullOrWhiteSpace(nombre) ? "Público general" : nombre;
    }

    private static string EtiquetaEstadoPedido(EstadoPedidoDigital estado) => estado switch
    {
        EstadoPedidoDigital.PendientePago => "Pendiente de pago",
        EstadoPedidoDigital.Pagado => "Pagado",
        EstadoPedidoDigital.Empaquetado => "Empaquetado",
        EstadoPedidoDigital.PendienteEntrega => "Pendiente de entrega",
        EstadoPedidoDigital.Entregado => "Entregado",
        EstadoPedidoDigital.Cancelado => "Cancelado",
        EstadoPedidoDigital.Anulado => "Anulado",
        EstadoPedidoDigital.Devuelto => "Devuelto",
        _ => estado.ToString()
    };

    private static string EtiquetaOrigen(OrigenPago origen) => origen switch
    {
        OrigenPago.EFECTIVO => "Efectivo",
        OrigenPago.YAPE => "Yape",
        OrigenPago.PLIN => "Plin",
        OrigenPago.TARJETA => "Tarjeta",
        OrigenPago.IZIPAY => "Izipay",
        OrigenPago.TRANSFERENCIA => "Transferencia",
        _ => "Otro"
    };
}
