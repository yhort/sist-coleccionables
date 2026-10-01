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
        DateTime? fechaDesde,
        DateTime? fechaHasta,
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

        // Límites ya convertidos a UTC (inicio inclusivo / fin exclusivo del día operativo).
        if (fechaDesde.HasValue)
        {
            var inicio = fechaDesde.Value;
            query = query.Where(p => p.FechaPedido >= inicio);
        }

        if (fechaHasta.HasValue)
        {
            var finExclusivo = fechaHasta.Value;
            query = query.Where(p => p.FechaPedido < finExclusivo);
        }

        var pedidos = await query
            .OrderByDescending(p => p.FechaPedido)
            .ToListAsync(cancellationToken);

        return await MapManyAsync(pedidos, cancellationToken);
    }

    public async Task<PedidoDigitalResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await QueryBase().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (pedido is null)
        {
            return null;
        }

        var mapped = await MapManyAsync([pedido], cancellationToken);
        return mapped[0];
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
        var clienteTelefono = TelefonoCliente.Normalizar(request.ClienteTelefono);
        request.ClienteTelefono = clienteTelefono;
        var ahora = DateTimeOffset.UtcNow;
        var cliente = await ResolverClienteAltaAsync(request, clienteNombre, ahora, cancellationToken);

        // Completa snapshot desde ficha si el formulario dejó vacío.
        if (string.IsNullOrWhiteSpace(entrega.DestinatarioTelefono))
        {
            entrega.DestinatarioTelefono = clienteTelefono ?? cliente.Telefono;
        }
        else
        {
            entrega.DestinatarioTelefono = TelefonoCliente.Normalizar(entrega.DestinatarioTelefono);
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

        if (clienteTelefono is null && cliente.Telefono is not null)
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
            ClienteTelefono = request.ClienteTelefono,
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
            // Snapshot de contacto / punto / ref del formulario (persiste al refrescar).
            PuntoEntrega = entrega.PuntoEntrega,
            CanalContacto = entrega.CanalContacto,
            ContactoReferencia = entrega.ContactoReferencia
        };

        pedido.Detalles = lineas;
        pedido.Historial.Add(NuevoHistorial(pedidoId, null, EstadoPedidoDigital.PendientePago, ahora, "Alta del pedido digital."));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.PedidosDigitales.Add(pedido);

        // Persiste preferencias en ficha: al crear cliente nuevo desde el pedido,
        // o cuando el usuario marca "guardar en ficha del cliente".
        if (request.GuardarPuntoEnCliente || request.ClienteId is null)
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
        var pedidoEstado = await db.PedidosDigitales.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => (EstadoPedidoDigital?)p.Estado)
            .FirstOrDefaultAsync(cancellationToken);
        if (pedidoEstado is null)
        {
            throw new BusinessRuleException("No se encontró el pedido digital.", StatusCodes.Status404NotFound);
        }

        if (PedidoKanban.BloqueaOperacionesTrasNc(pedidoEstado.Value))
        {
            throw new BusinessRuleException(
                "El pedido fue anulado o devuelto con nota de crédito. No admite empaque, despacho ni nueva facturación.");
        }

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

    public async Task<IReadOnlyList<PedidoDigitalResponse>> CambiarEstadoLoteAsync(
        CambiarEstadoLoteRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.PedidoDigitalIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Selecciona al menos un pedido.");
        }

        var destino = request.Estado;
        var origenEsperado = destino switch
        {
            EstadoPedidoDigital.Empaquetado => EstadoPedidoDigital.Pagado,
            EstadoPedidoDigital.PendienteEntrega => EstadoPedidoDigital.Empaquetado,
            EstadoPedidoDigital.Entregado => EstadoPedidoDigital.PendienteEntrega,
            _ => throw new BusinessRuleException(
                "El lote solo cubre Empaquetado, Pendiente de entrega o Entregado.")
        };

        var pedidos = await db.PedidosDigitales
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (pedidos.Count != ids.Count)
        {
            throw new BusinessRuleException("Uno o más pedidos no existen o no pertenecen a la empresa.");
        }

        if (pedidos.Any(p => p.Estado != origenEsperado))
        {
            throw new BusinessRuleException(
                $"Todos los pedidos deben estar en {origenEsperado} para pasar a {destino}.");
        }

        var nota = request.Observacion?.Trim();
        if (string.IsNullOrEmpty(nota))
        {
            nota = destino == EstadoPedidoDigital.Empaquetado
                ? "Empaque en lote."
                : PedidoKanban.EtiquetaTransicion(destino);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var actualizados = new List<PedidoDigitalResponse>(ids.Count);
        foreach (var id in ids)
        {
            actualizados.Add(await CambiarEstadoAsync(id, destino, nota, cancellationToken));
        }

        await tx.CommitAsync(cancellationToken);
        return actualizados;
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

        if (pedido.Estado == EstadoPedidoDigital.Cancelado
            || PedidoKanban.BloqueaOperacionesTrasNc(pedido.Estado))
        {
            return Map(pedido);
        }

        if (pedido.VentaId is not null)
        {
            throw new BusinessRuleException(
                "El pedido ya tiene venta o comprobante fiscal. No se puede cancelar; gestiona una nota de crédito si corresponde.");
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

    public async Task<PedidoDigitalResponse> ActualizarNotificacionAsync(
        Guid id,
        bool notificado,
        CancellationToken cancellationToken)
    {
        var pedido = await CargarAsync(id, cancellationToken);
        AplicarNotificacion(pedido, notificado);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<PedidoDigitalResponse>> ActualizarNotificacionLoteAsync(
        ActualizarNotificacionLoteRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.PedidoDigitalIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Selecciona al menos un pedido.");
        }

        // Debe ser tracked (no QueryBase/AsNoTracking) para que SaveChanges persista.
        var pedidos = await db.PedidosDigitales
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (pedidos.Count != ids.Count)
        {
            throw new BusinessRuleException("Uno o más pedidos no existen o no están disponibles.");
        }

        foreach (var pedido in pedidos)
        {
            AplicarNotificacion(pedido, request.Notificado);
        }

        await db.SaveChangesAsync(cancellationToken);

        var actualizados = await QueryBase()
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
        return actualizados.Select(Map).ToList();
    }

    private static void AplicarNotificacion(PedidoDigital pedido, bool notificado)
    {
        if (notificado)
        {
            pedido.Notificado = true;
            pedido.FechaNotificacion = DateTimeOffset.UtcNow;
            return;
        }

        pedido.Notificado = false;
        pedido.FechaNotificacion = null;
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

    /// <summary>
    /// Tras anular un pago confirmado: si el pedido estaba en Pagado (sin venta),
    /// vuelve a Pendiente de pago para liberar el flujo de cobro.
    /// </summary>
    public async Task RevertirAPendientePagoPorAnulacionPagoAsync(
        Guid pedidoId,
        string observacion,
        CancellationToken cancellationToken)
    {
        var pedido = await db.PedidosDigitales
            .Include(p => p.Historial)
            .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró el pedido digital.", StatusCodes.Status404NotFound);

        if (pedido.VentaId is not null)
        {
            throw new BusinessRuleException(
                "El pedido ya tiene venta; no se revierte el estado por anulación de pago.");
        }

        if (pedido.Estado != EstadoPedidoDigital.Pagado)
        {
            return;
        }

        AplicarHistorial(pedido, EstadoPedidoDigital.PendientePago, observacion);
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
        if (PedidoKanban.BloqueaOperacionesTrasNc(pedido.Estado))
        {
            throw new BusinessRuleException(
                "El pedido fue anulado o devuelto con nota de crédito. No se puede confirmar entrega ni facturar.");
        }

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

        var tipo = ResolverTipoComprobante(request.TipoComprobante);
        var transaccionExterna = db.Database.CurrentTransaction is not null;
        await using var tx = transaccionExterna
            ? null
            : await db.Database.BeginTransactionAsync(cancellationToken);

        var ahora = DateTimeOffset.UtcNow;
        var cajaSesionId = await caja.ExigirSesionAbiertaAsync(pedido.SedeId, cancellationToken);
        Guid ventaId;

        if (pedido.VentaId is { } ventaPrevia)
        {
            ventaId = ventaPrevia;
            if (pedido.IndicadorReserva != IndicadorReservaPedido.Confirmado)
            {
                await ConfirmarStockVentaAsync(pedido, ventaId, cancellationToken);
            }
        }
        else
        {
            ventaId = await CrearVentaFiscalAsync(pedido, request, ahora, cajaSesionId, confirmarStock: true, cancellationToken);
        }

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

        if (tx is not null)
        {
            await tx.CommitAsync(cancellationToken);
        }

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

    public async Task<ComprobanteConsolidadoResponse> EmitirComprobanteConsolidadoAsync(
        EmitirComprobanteConsolidadoRequest request,
        CancellationToken cancellationToken)
    {
        var ids = (request.PedidoDigitalIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Selecciona al menos un pedido pagado para emitir comprobante.");
        }

        var pedidos = new List<PedidoDigital>(ids.Count);
        foreach (var id in ids)
        {
            pedidos.Add(await CargarAsync(id, cancellationToken));
        }

        if (pedidos.Any(p =>
                p.Estado is EstadoPedidoDigital.PendientePago
                    or EstadoPedidoDigital.Cancelado
                    or EstadoPedidoDigital.Anulado
                    or EstadoPedidoDigital.Devuelto))
        {
            throw new BusinessRuleException(
                "Solo se emite comprobante de pedidos Pagados, Empaquetados, en entrega o Entregados. Los anulados/devueltos están bloqueados.");
        }

        if (pedidos.Any(p => p.VentaId is not null))
        {
            var ventaIds = pedidos
                .Where(p => p.VentaId is not null)
                .Select(p => p.VentaId!.Value)
                .Distinct()
                .ToList();
            var conNc = await db.Comprobantes.AsNoTracking()
                .Where(c => ventaIds.Contains(c.VentaId)
                    && c.Tipo == TipoComprobanteSunat.NOTA_CREDITO
                    && (c.Estado == EstadoEmisionSunat.ACEPTADO
                        || c.Estado == EstadoEmisionSunat.SIMULADO))
                .Select(c => c.VentaId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (conNc.Count > 0)
            {
                throw new BusinessRuleException(
                    "Hay pedidos con nota de crédito emitida. No se puede volver a facturar.");
            }
        }

        if (!PedidoKanban.MismoCliente(pedidos.Select(p => (p.ClienteId, p.ClienteNombre)).ToList()))
        {
            throw new BusinessRuleException("Selecciona pedidos del mismo cliente para emitir un comprobante consolidado.");
        }

        var sedes = pedidos.Select(p => p.SedeId).Distinct().ToList();
        if (sedes.Count > 1)
        {
            throw new BusinessRuleException("Los pedidos de un comprobante consolidado deben ser de la misma sede.");
        }

        var tipo = ResolverTipoComprobante(request.TipoComprobante);

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var ahora = DateTimeOffset.UtcNow;
        foreach (var pedido in pedidos)
        {
            if (pedido.VentaId is not null)
            {
                continue;
            }

            var cajaSesionId = await caja.ExigirSesionAbiertaAsync(pedido.SedeId, cancellationToken);
            await CrearVentaFiscalAsync(
                pedido,
                new ConvertirVentaRequest { TipoComprobante = tipo, EmitirComprobante = false },
                ahora,
                cajaSesionId,
                confirmarStock: false,
                cancellationToken);
            await caja.ActualizarTeoricoAsync(cajaSesionId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        var venta = await UnificarVentasDePedidosAsync(pedidos, cancellationToken);
        var comprobante = await fiscal.EmitirDesdeVentaAsync(venta.Id, tipo, null, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var actualizados = new List<PedidoDigitalResponse>(pedidos.Count);
        foreach (var pedido in pedidos)
        {
            actualizados.Add((await ObtenerAsync(pedido.Id, cancellationToken))!);
        }

        return new ComprobanteConsolidadoResponse
        {
            VentaId = venta.Id,
            Comprobante = comprobante,
            Pedidos = actualizados
        };
    }

    /// <summary>
    /// Crea la venta fiscal vinculada al pedido sin forzar Entregado.
    /// Si <paramref name="confirmarStock"/> es false, la reserva se confirma al entregar.
    /// </summary>
    private async Task<Guid> CrearVentaFiscalAsync(
        PedidoDigital pedido,
        ConvertirVentaRequest request,
        DateTimeOffset ahora,
        Guid cajaSesionId,
        bool confirmarStock,
        CancellationToken cancellationToken)
    {
        if (pedido.VentaId is { } existente)
        {
            return existente;
        }

        var cliente = await ResolverClienteAsync(pedido, request, ahora, cancellationToken);
        var tipo = ResolverTipoComprobante(request.TipoComprobante);
        if (tipo == TipoComprobanteSunat.FACTURA && cliente.TipoDocumento != TipoDocumentoIdentidad.RUC)
        {
            throw new BusinessRuleException("La factura exige un cliente con RUC.");
        }

        DocumentoIdentidad.ValidarComprobante(tipo, cliente.TipoDocumento, cliente.NumeroDocumento, pedido.Total);

        var ventaId = Guid.NewGuid();
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
        pedido.ClienteId = cliente.Id;
        pedido.VentaId = ventaId;

        if (confirmarStock)
        {
            await ConfirmarStockVentaAsync(pedido, ventaId, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ventaId;
    }

    private async Task ConfirmarStockVentaAsync(
        PedidoDigital pedido,
        Guid ventaId,
        CancellationToken cancellationToken)
    {
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
        pedido.IndicadorReserva = IndicadorReservaPedido.Confirmado;
    }

    private static TipoComprobanteSunat ResolverTipoComprobante(TipoComprobanteSunat tipo) => tipo switch
    {
        TipoComprobanteSunat.FACTURA => TipoComprobanteSunat.FACTURA,
        TipoComprobanteSunat.NOTA_VENTA => TipoComprobanteSunat.NOTA_VENTA,
        _ => TipoComprobanteSunat.BOLETA
    };

    public async Task<PedidoDigital> CargarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.PedidosDigitales
            .Include(p => p.Sede)
            .Include(p => p.Subasta)
            .Include(p => p.Detalles).ThenInclude(d => d.Producto)
            .Include(p => p.Historial).ThenInclude(h => h.Usuario)
            .Include(p => p.Entrega)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró el pedido digital.", StatusCodes.Status404NotFound);

    private async Task<Venta> UnificarVentasDePedidosAsync(
        IReadOnlyList<PedidoDigital> pedidos,
        CancellationToken cancellationToken)
    {
        var ventaIds = pedidos
            .Select(p => p.VentaId)
            .Where(id => id is { } valor && valor != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (ventaIds.Count == 0)
        {
            throw new BusinessRuleException("Los pedidos no tienen venta para emitir comprobante.");
        }

        var ventas = await db.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Pagos)
            .Include(v => v.Comprobantes)
            .Include(v => v.Cliente)
            .Where(v => ventaIds.Contains(v.Id))
            .ToListAsync(cancellationToken);
        if (ventas.Count != ventaIds.Count)
        {
            throw new BusinessRuleException("No se encontraron todas las ventas de los pedidos.");
        }

        var conCpe = ventas
            .Where(v => v.Comprobantes.Any(c =>
                c.Tipo is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.FACTURA or TipoComprobanteSunat.NOTA_VENTA
                && c.Estado is EstadoEmisionSunat.ACEPTADO
                    or EstadoEmisionSunat.PENDIENTE_CONSOLIDAR
                    or EstadoEmisionSunat.CONSOLIDADA))
            .ToList();
        if (conCpe.Count > 1)
        {
            throw new BusinessRuleException(
                "Hay pedidos con comprobantes distintos. No se pueden unificar en una sola emisión.");
        }

        if (conCpe.Count == 1 && ventas.Count > 1)
        {
            throw new BusinessRuleException(
                "Uno de los pedidos ya tiene comprobante. Deselecciónalo o emite por separado.");
        }

        var principal = conCpe.FirstOrDefault() ?? ventas[0];
        if (ventas.Count == 1)
        {
            return principal;
        }

        foreach (var extra in ventas.Where(v => v.Id != principal.Id).ToList())
        {
            foreach (var detalle in extra.Detalles.ToList())
            {
                detalle.VentaId = principal.Id;
            }

            foreach (var pagoVenta in extra.Pagos.ToList())
            {
                pagoVenta.VentaId = principal.Id;
            }

            var pagos = await db.Pagos
                .Where(p => p.VentaId == extra.Id)
                .ToListAsync(cancellationToken);
            foreach (var pago in pagos)
            {
                pago.VentaId = principal.Id;
            }

            extra.PedidoDigitalId = null;
            db.Ventas.Remove(extra);
        }

        await db.SaveChangesAsync(cancellationToken);

        var unificada = await db.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Cliente)
            .Include(v => v.Comprobantes)
            .FirstAsync(v => v.Id == principal.Id, cancellationToken);
        unificada.Subtotal = IgvCalculo.Round2(unificada.Detalles.Sum(d => d.Subtotal));
        unificada.Igv = IgvCalculo.Round2(unificada.Detalles.Sum(d => d.Igv));
        unificada.Total = IgvCalculo.Round2(unificada.Detalles.Sum(d => d.Total));

        foreach (var pedido in pedidos)
        {
            pedido.VentaId = unificada.Id;
        }

        await db.SaveChangesAsync(cancellationToken);
        return unificada;
    }

    private IQueryable<PedidoDigital> QueryBase() =>
        db.PedidosDigitales
            .AsNoTracking()
            .Include(p => p.Sede)
            .Include(p => p.Cliente)
            .Include(p => p.Subasta)
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
            var existente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken)
                ?? throw new BusinessRuleException("No se encontró el cliente seleccionado.");
            if (!existente.Activo)
            {
                throw new BusinessRuleException(
                    "El cliente seleccionado está desactivado. Reactívalo o elige otro.");
            }

            return existente;
        }

        if (request.EsClienteVarios)
        {
            return await clientes.ResolverPublicoGeneralAsync(ahora, cancellationToken);
        }

        var entrega = request.Entrega;
        return await clientes.ResolverOCrearDesdePedidoAsync(
            new Contracts.Clientes.UpsertClienteRequest
            {
                Nombre = clienteNombre,
                Telefono = request.ClienteTelefono,
                PuntoEntregaPreferido = entrega?.PuntoEntrega,
                CanalContacto = entrega?.CanalContacto,
                ContactoReferencia = entrega?.ContactoReferencia,
                TipoDocumento = request.TipoDocumento
                    ?? (string.IsNullOrWhiteSpace(request.NumeroDocumento)
                        ? TipoDocumentoIdentidad.SIN_DOCUMENTO
                        : TipoDocumentoIdentidad.DNI),
                NumeroDocumento = request.NumeroDocumento,
                EsPublicoGeneral = false
            },
            cancellationToken);
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
                    existente.NumeroDocumento = DocumentoIdentidad.NumeroParaPersistir(request.NumeroDocumento);
                    existente.TipoDocumento = request.TipoDocumento ?? existente.TipoDocumento;
                }

                return existente;
            }
        }

        var nombreBase = pedido.ClienteNombre?.Trim() is { Length: >= 2 } n ? n : "Cliente";
        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            nombreBase,
            request.TipoDocumento,
            request.NumeroDocumento,
            esPublicoGeneral: false);
        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = nombre,
            Telefono = pedido.ClienteTelefono,
            TipoDocumento = tipo,
            NumeroDocumento = DocumentoIdentidad.NumeroParaPersistir(numero),
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
                DestinatarioTelefono = TelefonoCliente.Normalizar(entrega.DestinatarioTelefono),
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
            DestinatarioTelefono = TelefonoCliente.Normalizar(entrega.DestinatarioTelefono),
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
        var tel = TelefonoCliente.Normalizar(telefono);
        if (tel is not null)
        {
            cliente.Telefono = tel;
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

    internal static PedidoDigitalResponse Map(PedidoDigital pedido) =>
        Map(pedido, comprobante: null, notaCredito: null);

    private async Task<IReadOnlyList<PedidoDigitalResponse>> MapManyAsync(
        IReadOnlyList<PedidoDigital> pedidos,
        CancellationToken cancellationToken)
    {
        var ventaIds = pedidos
            .Where(p => p.VentaId is { } id && id != Guid.Empty)
            .Select(p => p.VentaId!.Value)
            .Distinct()
            .ToList();
        if (ventaIds.Count == 0)
        {
            return pedidos.Select(p => Map(p)).ToList();
        }

        var comprobantes = await db.Comprobantes
            .AsNoTracking()
            .Where(c => ventaIds.Contains(c.VentaId))
            .OrderByDescending(c => c.FechaCreacion)
            .ToListAsync(cancellationToken);

        var porVenta = comprobantes
            .GroupBy(c => c.VentaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return pedidos.Select(pedido =>
        {
            if (pedido.VentaId is not { } ventaId || !porVenta.TryGetValue(ventaId, out var lista))
            {
                return Map(pedido);
            }

            var documento = lista.FirstOrDefault(c =>
                c.Tipo is TipoComprobanteSunat.BOLETA
                    or TipoComprobanteSunat.FACTURA
                    or TipoComprobanteSunat.NOTA_VENTA);
            var nota = lista.FirstOrDefault(c => c.Tipo == TipoComprobanteSunat.NOTA_CREDITO);
            return Map(pedido, ResumirComprobante(documento), ResumirComprobante(nota));
        }).ToList();
    }

    private static PedidoComprobanteResumen? ResumirComprobante(Comprobante? comprobante) =>
        comprobante is null
            ? null
            : new PedidoComprobanteResumen
            {
                Id = comprobante.Id,
                Tipo = comprobante.Tipo,
                Serie = comprobante.Serie,
                Correlativo = comprobante.Correlativo,
                Estado = comprobante.Estado,
                DocumentoReferencia = comprobante.DocumentoReferencia,
                CodigoMotivo = comprobante.CodigoMotivo,
                DescripcionMotivo = comprobante.DescripcionMotivo
            };

    internal static PedidoDigitalResponse Map(
        PedidoDigital pedido,
        PedidoComprobanteResumen? comprobante,
        PedidoComprobanteResumen? notaCredito)
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
            TituloSubasta = string.IsNullOrWhiteSpace(pedido.Subasta?.Titulo)
                ? null
                : pedido.Subasta.Titulo.Trim(),
            Notificado = pedido.Notificado,
            FechaNotificacion = pedido.FechaNotificacion,
            VentaId = pedido.VentaId,
            CodigoVenta = CodigoAmigable.Venta(pedido.VentaId),
            EntregaId = pedido.Entrega?.Id,
            Comprobante = comprobante,
            NotaCredito = notaCredito,
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
