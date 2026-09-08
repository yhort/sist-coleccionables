using CapitalPos.Tcg.Api.Application.Aperturas;
using CapitalPos.Tcg.Api.Contracts.Reportes;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Reportes;

public sealed class ReportesService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<KardexResumenFila>> KardexResumenAsync(
        Guid? sedeId,
        TipoProducto? tipoProducto,
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var query = db.MovimientosInventario
            .AsNoTracking()
            .Include(m => m.Producto)
            .Include(m => m.Sede)
            .AsQueryable();

        if (sedeId.HasValue)
        {
            query = query.Where(m => m.SedeId == sedeId.Value);
        }

        if (tipoProducto.HasValue)
        {
            query = query.Where(m => m.Producto.TipoProducto == tipoProducto.Value);
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
            .OrderBy(m => m.FechaCreacion)
            .ToListAsync(cancellationToken);

        return movimientos
            .GroupBy(m => new { m.SedeId, m.ProductoId })
            .Select(grupo =>
            {
                var primero = grupo.First();
                var ultimo = grupo.Last();
                var entradas = grupo.Where(m => m.StockPosterior > m.StockAnterior).Sum(m => m.StockPosterior - m.StockAnterior);
                var salidas = grupo.Where(m => m.StockPosterior < m.StockAnterior).Sum(m => m.StockAnterior - m.StockPosterior);
                return new KardexResumenFila
                {
                    SedeId = grupo.Key.SedeId,
                    SedeNombre = primero.Sede.Nombre,
                    ProductoId = grupo.Key.ProductoId,
                    ProductoNombre = primero.Producto.Nombre,
                    CodigoSku = primero.Producto.CodigoSku,
                    TipoProducto = primero.Producto.TipoProducto,
                    SaldoInicial = primero.StockAnterior,
                    Entradas = YieldApertura.Round2(entradas),
                    Salidas = YieldApertura.Round2(salidas),
                    SaldoFinal = ultimo.StockPosterior,
                    Movimientos = grupo.Count()
                };
            })
            .OrderBy(f => f.SedeNombre)
            .ThenBy(f => f.CodigoSku)
            .ToList();
    }

    public async Task<VentasPorSedeResponse> VentasPorSedeAsync(
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var query = db.Ventas.AsNoTracking().Include(v => v.Sede)
            .Where(v => !v.EsConsolidacion);
        if (desde.HasValue)
        {
            query = query.Where(v => v.Fecha >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(v => v.Fecha <= hasta.Value);
        }

        var ventas = await query.ToListAsync(cancellationToken);
        var total = ventas.Sum(v => v.Total);
        var filas = ventas
            .GroupBy(v => new { v.SedeId, v.Sede.Nombre })
            .Select(g => new VentasPorSedeFila
            {
                SedeId = g.Key.SedeId,
                SedeNombre = g.Key.Nombre,
                CantidadVentas = g.Count(),
                Subtotal = YieldApertura.Round2(g.Sum(v => v.Subtotal)),
                Igv = YieldApertura.Round2(g.Sum(v => v.Igv)),
                Total = YieldApertura.Round2(g.Sum(v => v.Total)),
                Porcentaje = total > 0 ? YieldApertura.Round2(g.Sum(v => v.Total) / total * 100) : 0
            })
            .OrderByDescending(f => f.Total)
            .ToList();

        return new VentasPorSedeResponse
        {
            Desde = desde,
            Hasta = hasta,
            Total = YieldApertura.Round2(total),
            Filas = filas
        };
    }

    public async Task<YieldAperturasResponse> MargenAperturasAsync(
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        string? juego,
        CancellationToken cancellationToken)
    {
        var query = db.AperturasTcg
            .AsNoTracking()
            .Include(a => a.Sede)
            .Include(a => a.ProductoSellado)
            .Include(a => a.Detalles).ThenInclude(d => d.ProductoCarta)
            .Where(a => a.Estado == EstadoAperturaTcg.CONFIRMADA);

        if (desde.HasValue)
        {
            query = query.Where(a => (a.FechaConfirmacion ?? a.FechaCreacion) >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(a => (a.FechaConfirmacion ?? a.FechaCreacion) <= hasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(juego))
        {
            var filtro = juego.Trim();
            query = query.Where(a => a.ProductoSellado.Juego.Contains(filtro));
        }

        var aperturas = await query
            .OrderByDescending(a => a.FechaConfirmacion)
            .ToListAsync(cancellationToken);

        var ids = aperturas.Select(a => a.Id).ToList();
        var ingresosKardex = await db.MovimientosInventario.AsNoTracking()
            .Where(m => m.TipoMovimiento == TipoMovimientoInventario.APERTURA_INGRESO_CARTA
                && m.ReferenciaId != null
                && ids.Contains(m.ReferenciaId.Value))
            .GroupBy(m => m.ReferenciaId)
            .Select(g => new { AperturaId = g.Key!.Value, Unidades = (int)Math.Round(g.Sum(m => m.Cantidad), 0, MidpointRounding.AwayFromZero) })
            .ToListAsync(cancellationToken);
        var kardexPorApertura = ingresosKardex.ToDictionary(x => x.AperturaId, x => x.Unidades);

        var filas = new List<YieldAperturaFila>();
        foreach (var apertura in aperturas)
        {
            var costo = YieldApertura.CostoSellado(apertura.ProductoSellado, apertura.CantidadSellados);
            var valor = YieldApertura.ValorEstimadoCartas(
                apertura.Detalles.Select(d => ((Domain.Entities.Producto?)d.ProductoCarta, d.Cantidad)));
            var rendimiento = YieldApertura.Calcular(costo, valor);
            kardexPorApertura.TryGetValue(apertura.Id, out var cartasKardex);

            filas.Add(new YieldAperturaFila
            {
                Id = apertura.Id,
                Fecha = apertura.FechaConfirmacion ?? apertura.FechaCreacion,
                SedeNombre = apertura.Sede.Nombre,
                SelladoNombre = apertura.ProductoSellado.Nombre,
                CantidadSellados = apertura.CantidadSellados,
                CartasObtenidas = apertura.Detalles.Sum(d => d.Cantidad),
                CartasIngresadasKardex = cartasKardex,
                CostoCompra = rendimiento.CostoSellado,
                ValorComercial = rendimiento.ValorEstimadoCartas,
                Diferencia = rendimiento.Diferencia,
                YieldPorcentaje = rendimiento.YieldPorcentaje
            });
        }

        var costoTotal = filas.Sum(f => f.CostoCompra);
        var valorTotal = filas.Sum(f => f.ValorComercial);
        var global = YieldApertura.Calcular(costoTotal, valorTotal);

        return new YieldAperturasResponse
        {
            Desde = desde,
            Hasta = hasta,
            CostoCompra = global.CostoSellado,
            ValorComercial = global.ValorEstimadoCartas,
            Diferencia = global.Diferencia,
            YieldPorcentaje = global.YieldPorcentaje,
            Filas = filas
        };
    }
}
