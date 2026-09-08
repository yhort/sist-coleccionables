using CapitalPos.Tcg.Api.Application.Caja;
using CapitalPos.Tcg.Api.Application.Clientes;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Application.Common;
using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Application.Subastas;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Pedidos;

public sealed class PedidosDigitalesService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    KardexWriter kardex,
    IServicioFiscal fiscal,
    CajaService caja,
    SubastasTcgService subastas,
    ClientesService clientes)
{
    public async Task<IReadOnlyList<PedidoDigitalResponse>> ListarAsync(
        EstadoPedidoDigital? estado,
        CanalPedidoDigital? canalPedido,
        Guid? sedeId,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();
        if (estado.HasValue)
        {
            query = query.Where(p => p.Estado == estado.Value);
        }

        if (canalPedido.HasValue)
        {
            query = query.Where(p => p.CanalPedido == canalPedido.Value);
        }

        if (sedeId.HasValue)
        {
            query = query.Where(p => p.SedeId == sedeId.Value);
        }

        var pedidos = await query
            .OrderByDescending(p => p.FechaPedido)
            .ToListAsync(cancellationToken);

        return pedidos.Select(Map).ToList();
    }

    public async Task<PedidoDigitalResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await QueryBase().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pedido is null ? null : Map(pedido);
    }

    public async Task<PedidoDigitalResponse> CrearAsync(
        CrearPedidoDigitalRequest request,
        CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
            ?? throw new BusinessRuleException("Selecciona una sede válida.");

        var clienteNombre = request.EsClienteVarios && request.ClienteNombre.Trim().Length < 2
            ? DocumentoIdentidad.NombreClienteVarios
            : request.ClienteNombre.Trim();
        if (clienteNombre.Length < 2)
        {
            throw new BusinessRuleException("Indica el nombre del cliente.");
        }

        if (request.Detalles.Count == 0)
        {
            throw new BusinessRuleException("El pedido debe tener al menos un ítem.");
        }

        var referencia = TextoOpcional(request.ReferenciaExterna, 80);
        if (referencia is not null
            && await db.PedidosDigitales.AnyAsync(p => p.ReferenciaExterna == referencia, cancellationToken))
        {
            throw new BusinessRuleException($"El pedido {referencia} ya fue importado.");
        }

        var entrega = NormalizarEntrega(request.Entrega, clienteNombre);
        var ahora = DateTimeOffset.UtcNow;
        var cliente = await ResolverClienteAltaAsync(request, clienteNombre, ahora, cancellationToken);

        // Completa snapshot desde ficha si el formulario dejó vacío.
        if (string.IsNullOrWhiteSpace(entrega.DestinatarioTelefono))
        {
            entrega.DestinatarioTelefono = TextoOpcional(request.ClienteTelefono, 32) ?? cliente.Telefono;
        }

        if (string.IsNullOrWhiteSpace(entrega.PuntoEntrega))
        {
            entrega.PuntoEntrega = cliente.PuntoEntregaPreferido;
        }

        entrega.CanalContacto ??= cliente.CanalContacto;
        if (string.IsNullOrWhiteSpace(entrega.ContactoReferencia))
        {
            entrega.ContactoReferencia = cliente.ContactoReferencia;
        }

        if (string.IsNullOrWhiteSpace(request.ClienteTelefono) && cliente.Telefono is not null)
        {
            request.ClienteTelefono = cliente.Telefono;
        }

        var pedidoId = Guid.NewGuid();
        var lineas = new List<PedidoDigitalDetalle>();

        foreach (var input in request.Detalles)
        {
            var producto = await db.Productos
                .FirstOrDefaultAsync(p => p.Id == input.ProductoId, cancellationToken);
            if (producto is null || !producto.Activo)
            {
                throw new BusinessRuleException("Selecciona un producto activo del catálogo.");
            }

            if (input.Cantidad < 1)
            {
                throw new BusinessRuleException($"La cantidad de {producto.Nombre} debe ser un entero mayor o igual a 1.");
            }

            var precio = IgvCalculo.Round2(input.PrecioUnitario);
            if (precio < 0)
            {
                throw new BusinessRuleException($"El precio de {producto.Nombre} no es válido.");
            }

            var libre = await kardex.LibreAsync(request.SedeId, producto.Id, cancellationToken);
            if (libre < input.Cantidad)
            {
                throw new BusinessRuleException(
                    $"No hay stock libre suficiente para {producto.Nombre}. Disponible: {libre}.");
            }

            lineas.Add(new PedidoDigitalDetalle
            {
                Id = Guid.NewGuid(),
                PedidoDigitalId = pedidoId,
                EmpresaId = tenant.EmpresaId,
                ProductoId = producto.Id,
                Descripcion = producto.Nombre,
                Cantidad = input.Cantidad,
                PrecioUnitario = precio,
                Total = IgvCalculo.Round2(input.Cantidad * precio)
            });
        }

        var total = IgvCalculo.Round2(lineas.Sum(l => l.Total));
        var (subtotal, igv, totalR) = IgvCalculo.DesdeTotal(total);

        var pedido = new PedidoDigital
        {
            Id = pedidoId,
            EmpresaId = tenant.EmpresaId,
            ClienteId = cliente.Id,
            ClienteNombre = clienteNombre,
            ClienteTelefono = TextoOpcional(request.ClienteTelefono, 32),
            SedeId = sede.Id,
            CanalPedido = request.CanalPedido,
            Estado = EstadoPedidoDigital.PendientePago,
            IndicadorReserva = IndicadorReservaPedido.Reservado,
            FechaPedido = ahora,
            Subtotal = subtotal,
            Igv = igv,
            Total = totalR,
            ReferenciaExterna = referencia,
            Observacion = TextoOpcional(request.Observacion, 500),
            DestinatarioNombre = entrega.DestinatarioNombre ?? clienteNombre,
            DestinatarioTelefono = entrega.DestinatarioTelefono,
            EntregaDireccion = entrega.Direccion,
            EntregaDistrito = entrega.Distrito,
            EntregaProvincia = entrega.Provincia,
            EntregaDepartamento = entrega.Departamento,
            Courier = entrega.Courier,
            EsRecojoTienda = entrega.EsRecojoTienda,
            NumeroTracking = entrega.NumeroTracking,
            CostoEnvio = entrega.CostoEnvio,
            NotasEmpaque = entrega.NotasEmpaque,
            Agencia = entrega.Agencia,
            PuntoEntrega = entrega.PuntoEntrega,
            CanalContacto = entrega.CanalContacto,
            ContactoReferencia = entrega.ContactoReferencia
        };

        pedido.Detalles = lineas;
        pedido.Historial.Add(NuevoHistorial(pedidoId, null, EstadoPedidoDigital.PendientePago, ahora, "Alta del pedido digital."));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PedidosDigitales.Add(pedido);

        if (request.GuardarPuntoEnCliente)
        {
            AplicarPreferenciasCliente(
                cliente,
                pedido.ClienteTelefono,
                pedido.PuntoEntrega,
                pedido.CanalContacto,
                pedido.ContactoReferencia);
        }

        await kardex.AplicarMuchosAsync(
            lineas.Select(l => new KardexComando(
                pedido.SedeId,
                l.ProductoId,
                TipoMovimientoInventario.RESERVA,
                l.Cantidad,
                $"Reserva pedido {pedido.Id} · {l.Descripcion}",
                "PEDIDO_DIGITAL",
                pedido.Id)),
            cancellationToken);

        if (request.CobroInmediato is { } cobro)
        {
            await AplicarCobroInmediatoAsync(pedido, cobro, ahora, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await ObtenerAsync(pedido.Id, cancellationToken))!;
    }

    public async Task<PedidoDigitalResponse> CambiarEstadoAsync(
        Guid id,
        EstadoPedidoDigital estadoNuevo,
        string? observacion,
        CancellationToken cancellationToken)
    {
        if (estadoNuevo == EstadoPedidoDigital.Cancelado)
        {
            return await CancelarAsync(id, observacion, cancellationToken);
        }

        if (estadoNuevo == EstadoPedidoDigital.Entregado)
        {
            var conversion = await ConvertirVentaAsync(
                id,
                new ConvertirVentaRequest { Observacion = observacion, EmitirComprobante = false },
                cancellationToken);
            return conversion.Pedido;
        }

        var pedido = await CargarAsync(id, cancellationToken);
        if (!PedidoKanban.PuedeTransicionar(pedido.Estado, estadoNuevo, pedido.EsRecojoTienda))
        {
            throw new BusinessRuleException(
                $"No se puede pasar de {pedido.Estado} a {estadoNuevo}. El flujo solo avanza o se cancela.");
        }

        if (estadoNuevo == EstadoPedidoDigital.Empaquetado)
        {
            var notas = observacion?.Trim() ?? string.Empty;
            if (notas.Length < 3)
            {
                throw new BusinessRuleException("Registra una nota de empaque (mínimo 3 caracteres).");
            }

            pedido.NotasEmpaque = notas[..Math.Min(notas.Length, 500)];
        }

        AplicarHistorial(pedido, estadoNuevo, observacion ?? PedidoKanban.EtiquetaTransicion(estadoNuevo));
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<PedidoDigitalResponse> CancelarAsync(
        Guid id,
        string? observacion,
        CancellationToken cancellationToken)
    {
        var pedido = await CargarAsync(id, cancellationToken);
        if (pedido.Estado == EstadoPedidoDigital.Entregado)
        {
            throw new BusinessRuleException("No se puede cancelar un pedido ya entregado.");
        }

        if (pedido.Estado == EstadoPedidoDigital.Cancelado)
        {
            return Map(pedido);
        }

        var motivo = observacion?.Trim();
        if (string.IsNullOrEmpty(motivo))
        {
            motivo = "Cancelación del pedido digital.";
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await LiberarReservaAsync(pedido, motivo, cancellationToken);
        AplicarHistorial(pedido, EstadoPedidoDigital.Cancelado, $"{motivo} Se libera la reserva de stock.");
        await subastas.LiberarAdjudicacionPorPedidoCanceladoAsync(
            pedido.Id,
            pedido.SubastaTcgId,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task MarcarPagadoPorPagoAsync(
        Guid pedidoId,
        string observacion,
        CancellationToken cancellationToken)
    {
        var pedido = await db.PedidosDigitales
            .Include(p => p.Historial)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró el pedido digital.", StatusCodes.Status404NotFound);

        if (pedido.Estado != EstadoPedidoDigital.PendientePago)
        {
            return;
        }

        AplicarHistorial(pedido, EstadoPedidoDigital.Pagado, observacion);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SincronizarSnapshotEntregaAsync(
        PedidoDigital pedido,
        Entrega entrega)
    {
        pedido.DestinatarioNombre = entrega.DestinatarioNombre;
        pedido.DestinatarioTelefono = entrega.DestinatarioTelefono;
        pedido.EntregaDireccion = entrega.Direccion;
        pedido.EntregaDistrito = entrega.Distrito;
        pedido.EntregaProvincia = entrega.Provincia;
        pedido.EntregaDepartamento = entrega.Departamento;
        pedido.Courier = PedidoKanban.EtiquetaCourier(entrega.MetodoEnvio);
        pedido.EsRecojoTienda = entrega.MetodoEnvio == MetodoEnvio.RECOJO_TIENDA;
        pedido.NumeroTracking = entrega.NumeroTracking;
        pedido.CostoEnvio = entrega.CostoEnvio;
        pedido.NotasEmpaque = entrega.NotasEmpaque;
        pedido.Agencia = entrega.Agencia;
        pedido.PuntoEntrega = entrega.PuntoEntrega;
        pedido.CanalContacto = entrega.CanalContacto;
        pedido.ContactoReferencia = entrega.ContactoReferencia;
        await Task.CompletedTask;
    }

    public async Task<ConversionVentaResponse> ConvertirVentaAsync(
        Guid id,
        ConvertirVentaRequest request,
        CancellationToken cancellationToken)
    {
        var pedido = await CargarAsync(id, cancellationToken);
        if (pedido.Estado == EstadoPedidoDigital.Entregado && pedido.VentaId is { } ventaExistente)
        {
            var existente = await db.Comprobantes.AsNoTracking()
                .FirstOrDefaultAsync(c => c.VentaId == ventaExistente, cancellationToken);
            return new ConversionVentaResponse
            {
                Pedido = Map(pedido),
                VentaId = ventaExistente,
                ComprobanteId = existente?.Id,
                Serie = existente?.Serie ?? string.Empty,
                Correlativo = existente?.Correlativo ?? 0,
                TipoComprobante = existente?.Tipo ?? TipoComprobanteSunat.BOLETA,
                EstadoEmision = existente?.Estado ?? EstadoEmisionSunat.SIMULADO
            };
        }

        if (!PedidoKanban.PuedeTransicionar(pedido.Estado, EstadoPedidoDigital.Entregado, pedido.EsRecojoTienda))
        {
            throw new BusinessRuleException(
                pedido.EsRecojoTienda
                    ? "El recojo en tienda se confirma desde Pagado, Empaquetado o Pendiente de entrega."
                    : "La entrega se confirma desde Pendiente de entrega.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var cliente = await ResolverClienteAsync(pedido, request, ahora, cancellationToken);
        var ventaId = Guid.NewGuid();
        var cajaSesionId = await caja.ExigirSesionAbiertaAsync(pedido.SedeId, cancellationToken);

        var venta = new Venta
        {
            Id = ventaId,
            EmpresaId = tenant.EmpresaId,
            SedeId = pedido.SedeId,
            ClienteId = cliente.Id,
            PedidoDigitalId = pedido.Id,
            Canal = pedido.CanalPedido,
            Fecha = ahora,
            Subtotal = pedido.Subtotal,
            Igv = pedido.Igv,
            Total = pedido.Total,
            UsuarioId = currentUser.UserId,
            CajaSesionId = cajaSesionId,
            FechaCreacion = ahora
        };

        foreach (var linea in pedido.Detalles)
        {
            var sku = linea.Producto?.CodigoSku ?? string.Empty;
            var (valorUnitario, subtotal, igv, total) = IgvCalculo.Linea(linea.Cantidad, linea.PrecioUnitario);
            venta.Detalles.Add(new VentaDetalle
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                VentaId = ventaId,
                ProductoId = linea.ProductoId,
                CodigoSku = sku,
                Descripcion = linea.Descripcion,
                Cantidad = linea.Cantidad,
                PrecioUnitario = linea.PrecioUnitario,
                ValorUnitario = valorUnitario,
                Subtotal = subtotal,
                Igv = igv,
                Total = total,
                CodigoAfectacionIgv = "10"
            });
        }

        var pagosConfirmados = await db.Pagos
            .Where(p => p.PedidoDigitalId == pedido.Id && p.Estado == EstadoPago.CONFIRMADO)
            .ToListAsync(cancellationToken);

        foreach (var pago in pagosConfirmados)
        {
            pago.VentaId = ventaId;
            venta.Pagos.Add(new VentaPago
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                VentaId = ventaId,
                PagoId = pago.Id,
                Origen = pago.Origen,
                Monto = pago.Monto,
                CodigoOperacion = pago.CodigoOperacion
            });
        }

        db.Ventas.Add(venta);

        await kardex.AplicarMuchosAsync(
            pedido.Detalles.Select(l => new KardexComando(
                pedido.SedeId,
                l.ProductoId,
                TipoMovimientoInventario.VENTA,
                l.Cantidad,
                $"Venta desde pedido {pedido.Id} · {l.Descripcion}",
                "VENTA",
                ventaId)),
            cancellationToken);

        var tipo = request.TipoComprobante switch
        {
            TipoComprobanteSunat.FACTURA => TipoComprobanteSunat.FACTURA,
            TipoComprobanteSunat.NOTA_VENTA => TipoComprobanteSunat.NOTA_VENTA,
            _ => TipoComprobanteSunat.BOLETA
        };
        if (tipo == TipoComprobanteSunat.FACTURA && cliente.TipoDocumento != TipoDocumentoIdentidad.RUC)
        {
            throw new BusinessRuleException("La factura exige un cliente con RUC.");
        }

        DocumentoIdentidad.ValidarComprobante(tipo, cliente.TipoDocumento, cliente.NumeroDocumento);

        pedido.ClienteId = cliente.Id;
        pedido.VentaId = ventaId;
        var nota = request.Observacion?.Trim();
        if (string.IsNullOrEmpty(nota))
        {
            nota = pedido.EsRecojoTienda
                ? "Recojo en tienda. Se convierte a venta y se confirma la reserva."
                : "Entrega confirmada. Se convierte a venta y se confirma la reserva.";
        }

        AplicarHistorial(pedido, EstadoPedidoDigital.Entregado, nota);

        if (pedido.Entrega is { } entrega)
        {
            entrega.Estado = EstadoLogistica.ENTREGADA;
            entrega.FechaEntrega = ahora;
        }

        await db.SaveChangesAsync(cancellationToken);
        await caja.ActualizarTeoricoAsync(cajaSesionId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        ComprobanteResponse? comprobante = null;
        if (request.EmitirComprobante)
        {
            comprobante = await fiscal.EmitirDesdeVentaAsync(ventaId, tipo, null, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new ConversionVentaResponse
        {
            Pedido = (await ObtenerAsync(id, cancellationToken))!,
            VentaId = ventaId,
            ComprobanteId = comprobante?.Id,
            Serie = comprobante?.Serie ?? string.Empty,
            Correlativo = comprobante?.Correlativo ?? 0,
            TipoComprobante = comprobante?.Tipo ?? tipo,
            EstadoEmision = comprobante?.Estado ?? EstadoEmisionSunat.SIMULADO
        };
    }

    public async Task<PedidoDigital> CargarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.PedidosDigitales
            .Include(p => p.Sede)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .Include(p => p.Entrega)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró el pedido digital.", StatusCodes.Status404NotFound);

    private IQueryable<PedidoDigital> QueryBase() =>
        db.PedidosDigitales
            .AsNoTracking()
            .Include(p => p.Sede)
            .Include(p => p.Cliente)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .Include(p => p.Entrega);

    private async Task LiberarReservaAsync(PedidoDigital pedido, string motivo, CancellationToken cancellationToken)
    {
        await kardex.AplicarMuchosAsync(
            pedido.Detalles.Select(l => new KardexComando(
                pedido.SedeId,
                l.ProductoId,
                TipoMovimientoInventario.LIBERACION_RESERVA,
                l.Cantidad,
                $"{motivo} · {l.Descripcion}",
                "PEDIDO_DIGITAL",
                pedido.Id)),
            cancellationToken);
    }

    private async Task AplicarCobroInmediatoAsync(
        PedidoDigital pedido,
        CobroInmediatoPedidoInput cobro,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        await caja.ExigirSesionAbiertaAsync(pedido.SedeId, cancellationToken);

        var codigo = NormalizarCodigoOperacion(cobro.CodigoOperacion);
        if (EsOrigenPagoDigital(cobro.Origen) && codigo is null)
        {
            codigo = $"POS-{ahora:yyMMddHHmmss}";
        }

        if (codigo is not null
            && await db.Pagos.AnyAsync(
                p => p.CodigoOperacion == codigo && p.Estado != EstadoPago.RECHAZADO,
                cancellationToken))
        {
            throw new BusinessRuleException($"Ya existe un pago con el código de operación {codigo}.");
        }

        if (cobro.Origen == OrigenPago.EFECTIVO
            && cobro.MontoRecibido is { } recibido
            && recibido + IgvCalculo.ToleranciaPago < pedido.Total)
        {
            throw new BusinessRuleException(
                $"El monto recibido (S/ {recibido:0.00}) es menor que el total (S/ {pedido.Total:0.00}).");
        }

        var referencia = TextoOpcional(cobro.ReferenciaExterna, 120);
        db.Pagos.Add(new Pago
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Origen = cobro.Origen,
            Estado = EstadoPago.CONFIRMADO,
            Monto = pedido.Total,
            CodigoOperacion = codigo,
            ReferenciaExterna = referencia,
            PedidoDigitalId = pedido.Id,
            ClienteNombre = pedido.ClienteNombre,
            FechaNotificacion = ahora,
            FechaConfirmacion = ahora,
            UsuarioAsocioId = currentUser.UserId,
            Observacion = "Cobro inmediato POS / caja.",
            FechaCreacion = ahora
        });

        var etiqueta = codigo ?? cobro.Origen.ToString();
        AplicarHistorial(
            pedido,
            EstadoPedidoDigital.Pagado,
            $"Cobro inmediato {cobro.Origen} ({etiqueta}). Pedido listo para empaque/entregas.");
    }

    private static bool EsOrigenPagoDigital(OrigenPago origen) =>
        origen is OrigenPago.YAPE or OrigenPago.PLIN or OrigenPago.IZIPAY or OrigenPago.TARJETA;

    private static string? NormalizarCodigoOperacion(string? codigo)
    {
        var texto = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, 80)];
    }

    private void AplicarHistorial(PedidoDigital pedido, EstadoPedidoDigital estadoNuevo, string observacion)
    {
        var anterior = pedido.Estado;
        pedido.Estado = estadoNuevo;
        pedido.IndicadorReserva = PedidoKanban.IndicadorDe(estadoNuevo);
        var evento = NuevoHistorial(pedido.Id, anterior, estadoNuevo, DateTimeOffset.UtcNow, observacion);
        pedido.Historial.Add(evento);
        db.PedidoDigitalHistorialEstados.Add(evento);
    }

    private PedidoDigitalHistorialEstado NuevoHistorial(
        Guid pedidoId,
        EstadoPedidoDigital? anterior,
        EstadoPedidoDigital nuevo,
        DateTimeOffset fecha,
        string observacion) =>
        new()
        {
            Id = Guid.NewGuid(),
            PedidoDigitalId = pedidoId,
            EmpresaId = tenant.EmpresaId,
            EstadoAnterior = anterior,
            EstadoNuevo = nuevo,
            UsuarioId = currentUser.UserId,
            Fecha = fecha,
            Observacion = observacion
        };

    private async Task<Cliente> ResolverClienteAltaAsync(
        CrearPedidoDigitalRequest request,
        string clienteNombre,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (request.ClienteId is { } clienteId && clienteId != Guid.Empty)
        {
            return await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken)
                ?? throw new BusinessRuleException("No se encontró el cliente seleccionado.");
        }

        if (request.EsClienteVarios)
        {
            return await clientes.ResolverPublicoGeneralAsync(ahora, cancellationToken);
        }

        var dto = await clientes.CrearAsync(
            new Contracts.Clientes.UpsertClienteRequest
            {
                Nombre = clienteNombre,
                Telefono = request.ClienteTelefono,
                TipoDocumento = request.TipoDocumento
                    ?? (string.IsNullOrWhiteSpace(request.NumeroDocumento)
                        ? TipoDocumentoIdentidad.SIN_DOCUMENTO
                        : TipoDocumentoIdentidad.DNI),
                NumeroDocumento = request.NumeroDocumento,
                EsPublicoGeneral = false
            },
            cancellationToken);
        return await db.Clientes.FirstAsync(c => c.Id == dto.Id, cancellationToken);
    }

    private async Task<Cliente> ResolverClienteAsync(
        PedidoDigital pedido,
        ConvertirVentaRequest request,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        if (pedido.ClienteId is { } clienteId)
        {
            var existente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);
            if (existente is not null)
            {
                if (!string.IsNullOrWhiteSpace(request.NumeroDocumento))
                {
                    existente.NumeroDocumento = request.NumeroDocumento.Trim();
                    existente.TipoDocumento = request.TipoDocumento ?? existente.TipoDocumento;
                }

                return existente;
            }
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = pedido.ClienteNombre?.Trim() is { Length: >= 2 } nombre ? nombre : "Cliente",
            Telefono = pedido.ClienteTelefono,
            TipoDocumento = request.TipoDocumento ?? TipoDocumentoIdentidad.DNI,
            NumeroDocumento = TextoOpcional(request.NumeroDocumento, 16) ?? "00000000",
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    private static PedidoDigitalEntregaInput NormalizarEntrega(PedidoDigitalEntregaInput? input, string clienteNombre)
    {
        var entrega = input ?? new PedidoDigitalEntregaInput { EsRecojoTienda = true };
        var destinatario = string.IsNullOrWhiteSpace(entrega.DestinatarioNombre)
            ? clienteNombre
            : entrega.DestinatarioNombre.Trim();

        if (entrega.EsRecojoTienda)
        {
            return new PedidoDigitalEntregaInput
            {
                DestinatarioNombre = destinatario,
                DestinatarioTelefono = TextoOpcional(entrega.DestinatarioTelefono, 32),
                EsRecojoTienda = true,
                Courier = "Recojo en tienda",
                CostoEnvio = 0,
                PuntoEntrega = TextoOpcional(entrega.PuntoEntrega, 80),
                CanalContacto = entrega.CanalContacto,
                ContactoReferencia = TextoOpcional(entrega.ContactoReferencia, 160),
                Agencia = TextoOpcional(entrega.Agencia, 80)
            };
        }

        var direccion = entrega.Direccion?.Trim() ?? string.Empty;
        var punto = TextoOpcional(entrega.PuntoEntrega, 80);
        if (direccion.Length < 5 && punto is null)
        {
            throw new BusinessRuleException(
                "Indica el punto de entrega o la dirección, o marca recojo en tienda.");
        }

        return new PedidoDigitalEntregaInput
        {
            DestinatarioNombre = destinatario,
            DestinatarioTelefono = TextoOpcional(entrega.DestinatarioTelefono, 32),
            Direccion = direccion.Length >= 5 ? direccion : null,
            Distrito = TextoOpcional(entrega.Distrito, 80),
            Provincia = TextoOpcional(entrega.Provincia, 80),
            Departamento = TextoOpcional(entrega.Departamento, 80),
            Courier = TextoOpcional(entrega.Courier, 80),
            EsRecojoTienda = false,
            NumeroTracking = TextoOpcional(entrega.NumeroTracking, 80),
            CostoEnvio = entrega.CostoEnvio < 0 ? 0 : IgvCalculo.Round2(entrega.CostoEnvio),
            NotasEmpaque = TextoOpcional(entrega.NotasEmpaque, 500),
            Agencia = TextoOpcional(entrega.Agencia, 80) ?? punto,
            PuntoEntrega = punto,
            CanalContacto = entrega.CanalContacto,
            ContactoReferencia = TextoOpcional(entrega.ContactoReferencia, 160)
        };
    }

    private static void AplicarPreferenciasCliente(
        Cliente cliente,
        string? telefono,
        string? puntoEntrega,
        CanalContactoCliente? canalContacto,
        string? contactoReferencia)
    {
        if (!string.IsNullOrWhiteSpace(telefono))
        {
            cliente.Telefono = telefono.Trim()[..Math.Min(telefono.Trim().Length, 32)];
        }

        if (!string.IsNullOrWhiteSpace(puntoEntrega))
        {
            var punto = puntoEntrega.Trim();
            cliente.PuntoEntregaPreferido = punto[..Math.Min(punto.Length, 80)];
        }

        if (canalContacto.HasValue)
        {
            cliente.CanalContacto = canalContacto;
        }

        if (!string.IsNullOrWhiteSpace(contactoReferencia))
        {
            var refContacto = contactoReferencia.Trim();
            cliente.ContactoReferencia = refContacto[..Math.Min(refContacto.Length, 160)];
        }
    }

    private static string? TextoOpcional(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    internal static PedidoDigitalResponse Map(PedidoDigital pedido)
    {
        var destinatario = string.IsNullOrWhiteSpace(pedido.DestinatarioNombre)
            ? pedido.ClienteNombre ?? "Cliente"
            : pedido.DestinatarioNombre;

        return new PedidoDigitalResponse
        {
            Id = pedido.Id,
            Codigo = CodigoAmigable.Pedido(pedido.Id),
            ClienteId = pedido.ClienteId,
            ClienteNombre = pedido.ClienteNombre ?? destinatario,
            ClienteTelefono = pedido.ClienteTelefono,
            ClienteTipoDocumento = pedido.Cliente?.TipoDocumento,
            ClienteNumeroDocumento = pedido.Cliente?.NumeroDocumento,
            SedeId = pedido.SedeId,
            SedeNombre = pedido.Sede.Nombre,
            CanalPedido = pedido.CanalPedido,
            Estado = pedido.Estado,
            IndicadorReserva = pedido.IndicadorReserva,
            FechaPedido = pedido.FechaPedido,
            Subtotal = pedido.Subtotal,
            Igv = pedido.Igv,
            Total = pedido.Total,
            ReferenciaExterna = pedido.ReferenciaExterna,
            Observacion = pedido.Observacion,
            SubastaTcgId = pedido.SubastaTcgId,
            CodigoSubasta = CodigoAmigable.Subasta(pedido.SubastaTcgId),
            VentaId = pedido.VentaId,
            CodigoVenta = CodigoAmigable.Venta(pedido.VentaId),
            EntregaId = pedido.Entrega?.Id,
            Entrega = new PedidoDigitalEntregaResponse
            {
                DestinatarioNombre = destinatario,
                DestinatarioTelefono = pedido.DestinatarioTelefono,
                Direccion = pedido.EntregaDireccion,
                Distrito = pedido.EntregaDistrito,
                Provincia = pedido.EntregaProvincia,
                Departamento = pedido.EntregaDepartamento,
                Courier = pedido.Courier,
                EsRecojoTienda = pedido.EsRecojoTienda,
                NumeroTracking = pedido.NumeroTracking,
                CostoEnvio = pedido.CostoEnvio,
                NotasEmpaque = pedido.NotasEmpaque,
                Agencia = pedido.Agencia,
                PuntoEntrega = pedido.PuntoEntrega,
                CanalContacto = pedido.CanalContacto,
                ContactoReferencia = pedido.ContactoReferencia
            },
            Detalles = pedido.Detalles.Select(d => new PedidoDigitalDetalleResponse
            {
                Id = d.Id,
                Codigo = CodigoAmigable.Item(d.Id),
                ProductoId = d.ProductoId,
                Descripcion = d.Descripcion,
                CodigoSku = d.Producto?.CodigoSku,
                Cantidad = d.Cantidad,
                PrecioUnitario = d.PrecioUnitario,
                Total = d.Total
            }).ToList(),
            HistorialEstados = pedido.Historial
                .OrderBy(h => h.Fecha)
                .Select(h => new PedidoDigitalHistorialResponse
                {
                    Id = h.Id,
                    EstadoAnterior = h.EstadoAnterior,
                    EstadoNuevo = h.EstadoNuevo,
                    UsuarioId = h.UsuarioId,
                    UsuarioNombre = h.Usuario?.Nombre,
                    Fecha = h.Fecha,
                    Observacion = h.Observacion
                }).ToList()
        };
    }
}
