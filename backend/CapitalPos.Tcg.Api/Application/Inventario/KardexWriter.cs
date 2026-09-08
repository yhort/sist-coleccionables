using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Application.WooCommerce;
using CapitalPos.Tcg.Api.Contracts.Inventario;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Inventario;

public readonly record struct KardexComando(
    Guid SedeId,
    Guid ProductoId,
    TipoMovimientoInventario TipoMovimiento,
    decimal Cantidad,
    string Motivo,
    string? ReferenciaTipo,
    Guid? ReferenciaId,
    bool Invertir = false,
    SentidoAjuste? Sentido = null);

/// <summary>
/// Libro append-only. Nunca edita ni borra asientos; el caller envuelve en transacción.
/// </summary>
public sealed class KardexWriter(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    WooStockPublishQueue wooStock)
{
    public async Task<decimal> LibreAsync(Guid sedeId, Guid productoId, CancellationToken cancellationToken)
    {
        var stock = await db.StocksProductos.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SedeId == sedeId && s.ProductoId == productoId, cancellationToken);
        return stock is null ? 0 : stock.CantidadDisponible - stock.CantidadReservada;
    }

    public async Task AplicarMuchosAsync(IEnumerable<KardexComando> comandos, CancellationToken cancellationToken)
    {
        foreach (var comando in comandos)
        {
            await AplicarAsync(comando, cancellationToken);
        }
    }

    public async Task AplicarAsync(KardexComando comando, CancellationToken cancellationToken)
    {
        if (comando.Cantidad <= 0)
        {
            throw new BusinessRuleException("La cantidad debe ser mayor que cero.");
        }

        var motivo = comando.Motivo.Trim();
        if (motivo.Length < 3)
        {
            throw new BusinessRuleException("Indica un motivo o justificación.");
        }

        var stock = await ObtenerOCrearAsync(comando.SedeId, comando.ProductoId, cancellationToken);
        var (deltaDisponible, deltaReservada) =
            comando.TipoMovimiento == TipoMovimientoInventario.VENTA && !comando.Invertir
                ? (-comando.Cantidad, -Math.Min(comando.Cantidad, stock.CantidadReservada))
                : CalcularEfecto(comando);

        var disponibleAnterior = stock.CantidadDisponible;
        var reservadoAnterior = stock.CantidadReservada;
        var disponibleSiguiente = disponibleAnterior + deltaDisponible;
        var reservadoSiguiente = reservadoAnterior + deltaReservada;

        if (disponibleSiguiente < 0)
        {
            throw new BusinessRuleException("El stock disponible no puede quedar negativo.");
        }

        if (reservadoSiguiente < 0)
        {
            throw new BusinessRuleException("El stock reservado no puede quedar negativo.");
        }

        if (disponibleSiguiente < reservadoSiguiente)
        {
            throw new BusinessRuleException("La reserva no puede superar el stock disponible.");
        }

        stock.CantidadDisponible = disponibleSiguiente;
        stock.CantidadReservada = reservadoSiguiente;
        wooStock.Encolar(comando.ProductoId);

        db.MovimientosInventario.Add(new MovimientoInventario
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SedeId = comando.SedeId,
            ProductoId = comando.ProductoId,
            TipoMovimiento = comando.TipoMovimiento,
            Cantidad = comando.Cantidad,
            StockAnterior = disponibleAnterior,
            StockPosterior = disponibleSiguiente,
            ReservadoAnterior = reservadoAnterior,
            ReservadoPosterior = reservadoSiguiente,
            ReferenciaTipo = comando.ReferenciaTipo,
            ReferenciaId = comando.ReferenciaId,
            Motivo = motivo,
            UsuarioId = currentUser.UserId,
            FechaCreacion = DateTimeOffset.UtcNow
        });
    }

    private async Task<StockProducto> ObtenerOCrearAsync(
        Guid sedeId,
        Guid productoId,
        CancellationToken cancellationToken)
    {
        var stock = await db.StocksProductos
            .FirstOrDefaultAsync(s => s.SedeId == sedeId && s.ProductoId == productoId, cancellationToken);
        if (stock is not null)
        {
            return stock;
        }

        stock = new StockProducto
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SedeId = sedeId,
            ProductoId = productoId,
            CantidadDisponible = 0,
            CantidadReservada = 0
        };
        db.StocksProductos.Add(stock);
        return stock;
    }

    private static (decimal DeltaDisponible, decimal DeltaReservada) CalcularEfecto(KardexComando comando)
    {
        var cantidad = comando.Cantidad;
        var (deltaDisponible, deltaReservada) = comando.TipoMovimiento switch
        {
            TipoMovimientoInventario.AJUSTE when comando.Sentido == SentidoAjuste.SALIDA => (-cantidad, 0m),
            TipoMovimientoInventario.AJUSTE => (cantidad, 0m),
            TipoMovimientoInventario.INGRESO_COMPRA => (cantidad, 0m),
            TipoMovimientoInventario.ANULACION_VENTA => (cantidad, 0m),
            TipoMovimientoInventario.APERTURA_INGRESO_CARTA => (cantidad, 0m),
            TipoMovimientoInventario.APERTURA_SALIDA_SELLADO => (-cantidad, 0m),
            TipoMovimientoInventario.VENTA => (-cantidad, 0m),
            TipoMovimientoInventario.RESERVA => (0m, cantidad),
            TipoMovimientoInventario.PUJA_GANADORA_RESERVA => (0m, cantidad),
            TipoMovimientoInventario.LIBERACION_RESERVA => (0m, -cantidad),
            TipoMovimientoInventario.ARMADO_COMPUESTO when comando.Sentido == SentidoAjuste.SALIDA => (-cantidad, 0m),
            TipoMovimientoInventario.ARMADO_COMPUESTO => (cantidad, 0m),
            TipoMovimientoInventario.DESARME_COMPUESTO when comando.Sentido == SentidoAjuste.SALIDA => (-cantidad, 0m),
            TipoMovimientoInventario.DESARME_COMPUESTO => (cantidad, 0m),
            _ => throw new BusinessRuleException($"Tipo de movimiento no soportado: {comando.TipoMovimiento}.")
        };

        return comando.Invertir
            ? (-deltaDisponible, -deltaReservada)
            : (deltaDisponible, deltaReservada);
    }
}
