using CapitalPos.Tcg.Api.Application.Caja;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Application.Common;
using CapitalPos.Tcg.Api.Application.Pagos;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pagos;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pos;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Pos;

public sealed class PosService(
    ApplicationDbContext db,
    CajaService caja,
    PagosService pagos,
    PedidosDigitalesService pedidos)
{
    public async Task<IReadOnlyList<PedidoDigitalResponse>> ListarPedidosPendientesAsync(
        string? q,
        Guid? sedeId,
        CancellationToken cancellationToken)
    {
        var lista = await pedidos.ListarAsync(
            EstadoPedidoDigital.PendientePago,
            canalPedido: null,
            sedeId,
            fechaDesde: null,
            fechaHasta: null,
            cancellationToken);

        var termino = q?.Trim() ?? string.Empty;
        if (termino.Length == 0)
        {
            return lista;
        }

        var needle = termino.ToUpperInvariant();
        return lista
            .Where(p =>
                Contiene(p.ClienteNombre, needle)
                || Contiene(p.ClienteNumeroDocumento, needle)
                || Contiene(p.Codigo, needle)
                || Contiene(p.CodigoSubasta, needle)
                || Contiene(p.TituloSubasta, needle)
                || Contiene(p.ReferenciaExterna, needle)
                || p.Detalles.Any(d => Contiene(d.Descripcion, needle)))
            .ToList();
    }

    public async Task<CobrarYEmitirPosResponse> CobrarYEmitirAsync(
        CobrarYEmitirPosRequest request,
        CancellationToken cancellationToken)
    {
        if (request.SedeId == Guid.Empty)
        {
            throw new BusinessRuleException("Selecciona la sede del POS.");
        }

        var sedeExiste = await db.Sedes.AsNoTracking()
            .AnyAsync(s => s.Id == request.SedeId, cancellationToken);
        if (!sedeExiste)
        {
            throw new BusinessRuleException("Selecciona una sede válida.");
        }

        await caja.ExigirSesionAbiertaAsync(request.SedeId, cancellationToken);

        var tipo = ResolverTipoComprobante(request.TipoComprobante);
        var idsPendientes = (request.PedidoDigitalIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var items = (request.Items ?? [])
            .Where(i => i.ProductoId != Guid.Empty && i.Cantidad > 0)
            .ToList();

        if (idsPendientes.Count == 0 && items.Count == 0)
        {
            throw new BusinessRuleException(
                "Agrega pedidos pendientes de cobro o productos al carrito del POS.");
        }

        var pagosRegistrados = new List<PagoResponse>();
        var pedidoIds = new List<Guid>();

        if (idsPendientes.Count > 0)
        {
            var (pagosLote, ids) = await CobrarPedidosPendientesAsync(
                idsPendientes,
                request,
                cancellationToken);
            pagosRegistrados.AddRange(pagosLote);
            pedidoIds.AddRange(ids);
        }

        if (items.Count > 0)
        {
            var (pagoMostrador, pedidoId) = await CobrarMostradorAsync(
                items,
                request,
                cancellationToken);
            if (pagoMostrador is not null)
            {
                pagosRegistrados.Add(pagoMostrador);
            }

            pedidoIds.Add(pedidoId);
        }

        pedidoIds = pedidoIds.Distinct().ToList();
        await AplicarDocumentoClienteAsync(pedidoIds, request, cancellationToken);

        var consolidado = await pedidos.EmitirComprobanteConsolidadoAsync(
            new EmitirComprobanteConsolidadoRequest
            {
                TipoComprobante = tipo,
                PedidoDigitalIds = pedidoIds
            },
            cancellationToken);

        var total = IgvCalculo.Round2(pagosRegistrados.Sum(p => p.Monto));
        if (total <= 0)
        {
            total = IgvCalculo.Round2(consolidado.Pedidos.Sum(p => p.Total));
        }

        var cpe = consolidado.Comprobante;
        var numero = $"{cpe.Serie}-{cpe.Correlativo:D8}";
        return new CobrarYEmitirPosResponse
        {
            VentaId = consolidado.VentaId,
            Comprobante = cpe,
            Pedidos = consolidado.Pedidos,
            Pagos = pagosRegistrados,
            TotalCobrado = total,
            Mensaje = $"Cobro OK · CPE {numero} · {cpe.Estado}"
                + (string.IsNullOrWhiteSpace(cpe.Mensaje) ? string.Empty : $" · {cpe.Mensaje}")
        };
    }

    private async Task<(IReadOnlyList<PagoResponse> Pagos, List<Guid> Ids)> CobrarPedidosPendientesAsync(
        List<Guid> ids,
        CobrarYEmitirPosRequest request,
        CancellationToken cancellationToken)
    {
        // AsNoTracking: evita dejar el pedido trackeado sin Detalles y romper
        // el Include de EmitirComprobanteConsolidado / CrearVentaFiscalAsync.
        var pedidosDb = await db.PedidosDigitales.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (pedidosDb.Count != ids.Count)
        {
            throw new BusinessRuleException("Uno o más pedidos no existen o no pertenecen a la empresa.");
        }

        if (pedidosDb.Any(p => p.Estado != EstadoPedidoDigital.PendientePago))
        {
            throw new BusinessRuleException("Solo se cobran pedidos en Pendiente de pago.");
        }

        if (pedidosDb.Any(p => p.SedeId != request.SedeId))
        {
            throw new BusinessRuleException("Los pedidos pendientes deben ser de la sede del POS.");
        }

        var monto = IgvCalculo.Round2(pedidosDb.Sum(p => p.Total));
        if (request.OrigenPago == OrigenPago.EFECTIVO
            && request.MontoRecibido is { } recibido
            && recibido + IgvCalculo.ToleranciaPago < monto)
        {
            throw new BusinessRuleException(
                $"El monto recibido (S/ {recibido:0.00}) es menor que el total (S/ {monto:0.00}).");
        }

        var pagosLote = await pagos.RegistrarLoteAsync(
            new RegistrarPagoLoteRequest
            {
                Origen = request.OrigenPago,
                Monto = monto,
                CodigoOperacion = request.CodigoOperacion,
                ReferenciaExterna = request.ReferenciaExterna,
                Observacion = request.Observacion ?? "Cobro POS mostrador",
                Confirmar = true,
                PedidoDigitalIds = ids
            },
            cancellationToken);

        return (pagosLote, ids);
    }

    private async Task<(PagoResponse? Pago, Guid PedidoId)> CobrarMostradorAsync(
        List<PosLineaInput> items,
        CobrarYEmitirPosRequest request,
        CancellationToken cancellationToken)
    {
        var detalles = new List<PedidoDigitalDetalleInput>(items.Count);
        foreach (var item in items)
        {
            var producto = await db.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == item.ProductoId, cancellationToken)
                ?? throw new BusinessRuleException("Selecciona un producto activo del catálogo.");
            if (!producto.Activo)
            {
                throw new BusinessRuleException($"El producto {producto.Nombre} no está activo.");
            }

            var precio = item.PrecioUnitario is { } p && p > 0
                ? IgvCalculo.Round2(p)
                : IgvCalculo.Round2(producto.PrecioVenta);
            if (precio <= 0)
            {
                throw new BusinessRuleException($"El producto {producto.Nombre} no tiene precio de venta.");
            }

            detalles.Add(new PedidoDigitalDetalleInput
            {
                ProductoId = producto.Id,
                Cantidad = item.Cantidad,
                PrecioUnitario = precio
            });
        }

        var nombre = string.IsNullOrWhiteSpace(request.ClienteNombre)
            ? "Cliente varios"
            : request.ClienteNombre.Trim();
        var esVarios = string.Equals(nombre, "Cliente varios", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(request.ClienteNombre);

        var pedido = await pedidos.CrearAsync(
            new CrearPedidoDigitalRequest
            {
                ClienteNombre = nombre,
                TipoDocumento = request.TipoDocumento,
                NumeroDocumento = request.NumeroDocumento,
                EsClienteVarios = esVarios,
                SedeId = request.SedeId,
                CanalPedido = CanalPedidoDigital.OTRO,
                Observacion = request.Observacion ?? "Venta mostrador POS",
                Detalles = detalles,
                Entrega = new PedidoDigitalEntregaInput
                {
                    DestinatarioNombre = nombre,
                    EsRecojoTienda = true
                },
                CobroInmediato = new CobroInmediatoPedidoInput
                {
                    Origen = request.OrigenPago,
                    CodigoOperacion = request.CodigoOperacion,
                    ReferenciaExterna = request.ReferenciaExterna,
                    MontoRecibido = request.MontoRecibido
                }
            },
            cancellationToken);

        var pago = (await pagos.ListarAsync(
                estado: null,
                origen: null,
                fechaDesde: null,
                fechaHasta: null,
                pedidoDigitalId: pedido.Id,
                cancellationToken))
            .OrderByDescending(p => p.FechaConfirmacion ?? p.FechaNotificacion)
            .FirstOrDefault();

        return (pago, pedido.Id);
    }

    private async Task AplicarDocumentoClienteAsync(
        IReadOnlyList<Guid> pedidoIds,
        CobrarYEmitirPosRequest request,
        CancellationToken cancellationToken)
    {
        var numero = request.NumeroDocumento?.Trim();
        if (request.TipoDocumento is null && string.IsNullOrWhiteSpace(numero))
        {
            return;
        }

        var clienteIds = await db.PedidosDigitales
            .Where(p => pedidoIds.Contains(p.Id) && p.ClienteId != null)
            .Select(p => p.ClienteId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (clienteIds.Count == 0)
        {
            return;
        }

        var clientes = await db.Clientes
            .Where(c => clienteIds.Contains(c.Id))
            .ToListAsync(cancellationToken);
        foreach (var cliente in clientes)
        {
            if (request.TipoDocumento is { } tipo)
            {
                cliente.TipoDocumento = tipo;
            }

            if (!string.IsNullOrWhiteSpace(numero))
            {
                cliente.NumeroDocumento = numero[..Math.Min(numero.Length, 16)];
            }

            if (!string.IsNullOrWhiteSpace(request.ClienteNombre)
                && request.ClienteNombre.Trim().Length >= 2)
            {
                cliente.Nombre = request.ClienteNombre.Trim();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static TipoComprobanteSunat ResolverTipoComprobante(TipoComprobanteSunat tipo) =>
        tipo switch
        {
            TipoComprobanteSunat.BOLETA
                or TipoComprobanteSunat.FACTURA
                or TipoComprobanteSunat.NOTA_VENTA => tipo,
            _ => throw new BusinessRuleException(
                "En el POS solo puedes emitir boleta, factura o nota de venta.")
        };

    private static bool Contiene(string? valor, string needleUpper) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.ToUpperInvariant().Contains(needleUpper, StringComparison.Ordinal);
}
