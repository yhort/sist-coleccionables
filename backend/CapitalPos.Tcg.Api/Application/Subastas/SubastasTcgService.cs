using CapitalPos.Tcg.Api.Application.Common;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Subastas;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Subastas;

public sealed class SubastasTcgService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    KardexWriter kardex)
{
    /// <summary>Zona horaria operativa del negocio (Perú).</summary>
    private static readonly TimeSpan ZonaHorariaOperacion = TimeSpan.FromHours(-5);

    public async Task<IReadOnlyList<SubastaTcgResponse>> ListarAsync(
        EstadoSubastaTcg? estado,
        CanalSubastaTcg? canal,
        Guid? sedeId,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();

        if (estado.HasValue)
        {
            query = query.Where(s => s.Estado == estado.Value);
        }

        if (canal.HasValue)
        {
            query = query.Where(s => s.Canal == canal.Value);
        }

        if (sedeId.HasValue)
        {
            query = query.Where(s => s.SedeId == sedeId.Value);
        }

        // Día operativo en hora de Perú (UTC-5), alineado con el filtro "Desde"/"Hasta" del frontend.
        if (fechaDesde.HasValue)
        {
            var inicio = new DateTimeOffset(fechaDesde.Value, TimeOnly.MinValue, ZonaHorariaOperacion);
            query = query.Where(s => s.FechaInicio >= inicio);
        }

        if (fechaHasta.HasValue)
        {
            var finExclusivo = new DateTimeOffset(
                fechaHasta.Value.AddDays(1),
                TimeOnly.MinValue,
                ZonaHorariaOperacion);
            query = query.Where(s => s.FechaInicio < finExclusivo);
        }

        var subastas = await query
            .OrderByDescending(s => s.FechaInicio)
            .ToListAsync(cancellationToken);

        return subastas.Select(Map).ToList();
    }

    public async Task<SubastaTcgResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var subasta = await QueryBase().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return subasta is null ? null : Map(subasta);
    }

    public async Task<SubastaTcgResponse> CrearAsync(
        CrearSubastaTcgRequest request,
        CancellationToken cancellationToken)
    {
        var lineas = await ValidarCabeceraYLineasAsync(request, cancellationToken);
        var principal = lineas[0];

        var subasta = new SubastaTcg
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SedeId = request.SedeId,
            ProductoId = principal.Producto.Id,
            Titulo = request.Titulo.Trim(),
            Canal = request.Canal,
            Modo = request.Modo,
            PrecioBase = Round2(request.PrecioBase),
            IncrementoMinimo = Round2(request.IncrementoMinimo),
            PrecioReserva = request.PrecioReserva is null ? null : Round2(request.PrecioReserva.Value),
            FechaInicio = request.FechaInicio.ToUniversalTime(),
            FechaCierre = request.FechaCierre.ToUniversalTime(),
            Estado = EstadoSubastaTcg.BORRADOR,
            Observacion = TextoOpcional(request.Observacion)
        };

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            subasta.Detalles.Add(new SubastaDetalle
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                SubastaTcgId = subasta.Id,
                ProductoId = linea.Producto.Id,
                Cantidad = linea.Cantidad,
                Orden = i + 1,
                TituloPersonalizado = linea.TituloPersonalizado,
                Estado = EstadoSubastaDetalle.PENDIENTE
            });
        }

        db.SubastasTcg.Add(subasta);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(subasta.Id, cancellationToken))!;
    }

    public async Task<SubastaTcgResponse> ActivarAsync(Guid id, CancellationToken cancellationToken)
    {
        var subasta = await CargarAsync(id, cancellationToken);
        if (subasta.Estado != EstadoSubastaTcg.BORRADOR)
        {
            throw new BusinessRuleException("Solo se puede activar una subasta en borrador.");
        }

        if (subasta.Detalles.Count == 0)
        {
            throw new BusinessRuleException("La subasta no tiene productos para activar.");
        }

        subasta.Estado = EstadoSubastaTcg.ACTIVA;
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<SubastaTcgResponse> RegistrarPujaAsync(
        Guid id,
        RegistrarPujaRequest request,
        CancellationToken cancellationToken)
    {
        var subasta = await CargarAsync(id, cancellationToken);
        if (subasta.Estado != EstadoSubastaTcg.ACTIVA)
        {
            throw new BusinessRuleException("Solo se registran pujas en una subasta activa.");
        }

        if (EstaVencida(subasta.FechaCierre))
        {
            throw new BusinessRuleException("La subasta ha finalizado y no acepta más pujas.");
        }

        var nombre = request.NombrePostor.Trim();
        if (nombre.Length < 2)
        {
            throw new BusinessRuleException("Indica el nombre o alias del postor.");
        }

        SubastaDetalle? linea = null;
        if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES)
        {
            if (request.SubastaDetalleId is not { } detalleId || detalleId == Guid.Empty)
            {
                throw new BusinessRuleException("Selecciona la carta/producto a la que aplica la puja.");
            }

            linea = subasta.Detalles.FirstOrDefault(d => d.Id == detalleId)
                ?? throw new BusinessRuleException("La carta seleccionada no pertenece a este evento.");

            if (linea.Estado != EstadoSubastaDetalle.PENDIENTE)
            {
                throw new BusinessRuleException("Esa carta ya no acepta pujas; elige otra pendiente.");
            }
        }
        else if (request.SubastaDetalleId is { } detalleCombo && detalleCombo != Guid.Empty)
        {
            throw new BusinessRuleException("En modo Combo la puja aplica al lote completo, no a una carta.");
        }

        var monto = Round2(request.Monto);
        var minimo = MontoMinimoSiguiente(subasta, linea?.Id);
        if (monto < minimo)
        {
            throw new BusinessRuleException($"La puja debe ser de al menos {minimo:0.00}.");
        }

        var puja = new Puja
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SubastaTcgId = subasta.Id,
            SubastaDetalleId = linea?.Id,
            ClienteId = request.ClienteId,
            NombrePostor = nombre,
            Monto = monto,
            Fecha = DateTimeOffset.UtcNow,
            EsGanadora = false
        };
        db.Pujas.Add(puja);

        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<SubastaTcgResponse> CerrarAsync(Guid id, CancellationToken cancellationToken)
    {
        return await EjecutarConReintentoConcurrenciaAsync(
            id,
            async (subasta, ct) =>
            {
                if (subasta.Estado is EstadoSubastaTcg.CERRADA or EstadoSubastaTcg.ADJUDICADA)
                {
                    return;
                }

                if (subasta.Estado != EstadoSubastaTcg.ACTIVA)
                {
                    throw new BusinessRuleException("Solo se puede cerrar una subasta activa.");
                }

                MarcarCierre(subasta);
                await GuardarCambiosAsync(ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Cierra la puja sin ganador: en Individuales declara desierta la carta seleccionada;
    /// en Combo cierra el lote sin asignar ganador ni pedido.
    /// </summary>
    public async Task<SubastaTcgResponse> DeclararDesiertaAsync(
        Guid id,
        Guid? subastaDetalleId,
        CancellationToken cancellationToken)
    {
        return await EjecutarConReintentoConcurrenciaAsync(
            id,
            async (subasta, ct) =>
            {
                if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES)
                {
                    if (subastaDetalleId is not { } detalleId || detalleId == Guid.Empty)
                    {
                        throw new BusinessRuleException("Selecciona la carta a declarar desierta.");
                    }

                    var linea = subasta.Detalles.FirstOrDefault(d => d.Id == detalleId)
                        ?? throw new BusinessRuleException("La carta seleccionada no pertenece a este evento.");

                    if (linea.Estado != EstadoSubastaDetalle.PENDIENTE)
                    {
                        throw new BusinessRuleException("Solo se puede declarar desierta una carta pendiente.");
                    }

                    if (subasta.Estado is not (EstadoSubastaTcg.ACTIVA or EstadoSubastaTcg.CERRADA))
                    {
                        throw new BusinessRuleException("El evento debe estar activo o cerrado.");
                    }

                    foreach (var puja in PujasDeLinea(subasta, linea.Id))
                    {
                        puja.EsGanadora = false;
                    }

                    linea.Estado = EstadoSubastaDetalle.DESIERTA;
                    linea.PujaGanadoraId = null;
                    ActualizarEstadoEventoTrasLineas(subasta);
                    await GuardarCambiosAsync(ct);
                    return;
                }

                if (subasta.Estado != EstadoSubastaTcg.ACTIVA)
                {
                    throw new BusinessRuleException("Solo se puede declarar desierta una subasta activa.");
                }

                MarcarCierreSinGanador(subasta);
                await GuardarCambiosAsync(ct);
            },
            cancellationToken);
    }

    public async Task<SubastaTcgResponse> EliminarPujaAsync(Guid pujaId, CancellationToken cancellationToken)
    {
        var puja = await db.Pujas
            .FirstOrDefaultAsync(p => p.Id == pujaId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la puja.", StatusCodes.Status404NotFound);

        var subastaId = puja.SubastaTcgId;
        return await EjecutarConReintentoConcurrenciaAsync(
            subastaId,
            async (subasta, ct) =>
            {
                var actual = subasta.Pujas.FirstOrDefault(p => p.Id == pujaId)
                    ?? throw new BusinessRuleException("No se encontró la puja.", StatusCodes.Status404NotFound);

                if (subasta.Estado is EstadoSubastaTcg.ADJUDICADA or EstadoSubastaTcg.CANCELADA)
                {
                    throw new BusinessRuleException("No se pueden anular pujas de una subasta adjudicada o cancelada.");
                }

                if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES && actual.SubastaDetalleId is { } detalleId)
                {
                    var linea = subasta.Detalles.FirstOrDefault(d => d.Id == detalleId);
                    if (linea?.Estado == EstadoSubastaDetalle.ADJUDICADO)
                    {
                        throw new BusinessRuleException("No se puede anular una puja de una carta ya adjudicada.");
                    }
                }
                else if (subasta.PedidoDigitalId.HasValue)
                {
                    throw new BusinessRuleException("No se pueden anular pujas: el lote ya tiene pedido digital.");
                }

                db.Pujas.Remove(actual);
                subasta.Pujas.Remove(actual);

                if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES && actual.SubastaDetalleId is { } lineaId)
                {
                    RecalcularLiderLinea(subasta, lineaId);
                }
                else
                {
                    RecalcularLiderLote(subasta);
                }

                await GuardarCambiosAsync(ct);
            },
            cancellationToken);
    }

    public async Task<CierreVencidasResultado> CerrarVencidasAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var activas = await db.SubastasTcg
            .IgnoreQueryFilters()
            .Include(s => s.Pujas)
            .Include(s => s.Detalles)
            .Where(s => s.Estado == EstadoSubastaTcg.ACTIVA)
            .ToListAsync(cancellationToken);

        var vencidas = activas.Where(s => EstaVencida(s.FechaCierre, ahora)).ToList();
        if (vencidas.Count == 0)
        {
            return new CierreVencidasResultado(0);
        }

        foreach (var grupo in vencidas.GroupBy(s => s.EmpresaId))
        {
            tenant.SetEmpresa(grupo.Key);
            foreach (var subasta in grupo)
            {
                MarcarCierre(subasta);
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return new CierreVencidasResultado(vencidas.Count);
    }

    public async Task LiberarAdjudicacionPorPedidoCanceladoAsync(
        Guid pedidoId,
        Guid? subastaTcgId,
        CancellationToken cancellationToken)
    {
        if (subastaTcgId is null)
        {
            return;
        }

        var subasta = await db.SubastasTcg
            .Include(s => s.Pujas)
            .Include(s => s.Detalles)
            .FirstOrDefaultAsync(s => s.Id == subastaTcgId.Value, cancellationToken);

        if (subasta is null)
        {
            return;
        }

        var linea = subasta.Detalles.FirstOrDefault(d => d.PedidoDigitalId == pedidoId);
        if (linea is not null)
        {
            PromoverSiguienteGanadoraLinea(subasta, linea);
            linea.PedidoDigitalId = null;
            linea.Estado = EstadoSubastaDetalle.PENDIENTE;
            if (subasta.Estado == EstadoSubastaTcg.ADJUDICADA)
            {
                subasta.Estado = EstadoSubastaTcg.ACTIVA;
                subasta.PedidoDigitalId = null;
                subasta.PujaGanadoraId = null;
            }

            return;
        }

        if (subasta.Estado != EstadoSubastaTcg.ADJUDICADA
            || subasta.PedidoDigitalId != pedidoId)
        {
            return;
        }

        PromoverSiguienteGanadora(subasta);
        subasta.PedidoDigitalId = null;
        subasta.Estado = EstadoSubastaTcg.CERRADA;
    }

    public async Task<SubastaTcgResponse> AdjudicarAsync(
        Guid id,
        AdjudicarSubastaRequest? checkout,
        CancellationToken cancellationToken)
    {
        return await EjecutarConReintentoConcurrenciaAsync(
            id,
            async (subasta, ct) =>
            {
                if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES)
                {
                    await AdjudicarLineaIndividualCoreAsync(subasta, checkout, ct);
                    return;
                }

                if (subasta.Estado == EstadoSubastaTcg.ACTIVA)
                {
                    var maxima = await ResolverGanadoraLoteAsync(subasta, checkout, ct);
                    if (!AlcanzaReserva(subasta, maxima))
                    {
                        throw new BusinessRuleException(MensajeSinGanadora(subasta));
                    }

                    MarcarCierre(subasta);
                }

                if (subasta.Estado != EstadoSubastaTcg.CERRADA)
                {
                    throw new BusinessRuleException("La subasta debe estar cerrada (o activa) para adjudicar.");
                }

                if (subasta.PedidoDigitalId.HasValue)
                {
                    throw new BusinessRuleException("Esta subasta ya tiene un pedido digital asociado.");
                }

                var ganadora = subasta.Pujas.FirstOrDefault(p => p.EsGanadora)
                    ?? await ResolverGanadoraLoteAsync(subasta, checkout, ct)
                    ?? throw new BusinessRuleException(MensajeSinGanadora(subasta));

                var lineas = LineasOrdenadas(subasta);
                if (lineas.Count == 0)
                {
                    throw new BusinessRuleException("La subasta no tiene productos para adjudicar.");
                }

                foreach (var linea in lineas)
                {
                    var libre = await kardex.LibreAsync(subasta.SedeId, linea.ProductoId, ct);
                    if (libre < linea.Cantidad)
                    {
                        throw new BusinessRuleException(
                            $"No hay stock libre en la sede para reservar {NombreVisibleLinea(linea)} (necesario: {linea.Cantidad}, libre: {libre}).");
                    }
                }

                Cliente? clienteGanador = null;
                if (ganadora.ClienteId is { } clienteId)
                {
                    clienteGanador = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, ct);
                }

                await using var tx = await db.Database.BeginTransactionAsync(ct);

                await kardex.AplicarMuchosAsync(
                    lineas.Select(linea => new KardexComando(
                        subasta.SedeId,
                        linea.ProductoId,
                        TipoMovimientoInventario.PUJA_GANADORA_RESERVA,
                        linea.Cantidad,
                        $"Puja ganadora {EtiquetaCanal(subasta.Canal)} · {NombreVisibleLinea(linea)}",
                        "SUBASTA_TCG",
                        subasta.Id)),
                    ct);

                var pedido = CrearPedidoDesdeSubasta(subasta, ganadora, lineas, checkout, clienteGanador);
                db.PedidosDigitales.Add(pedido);
                AplicarPreferenciasCliente(checkout, clienteGanador, pedido);

                foreach (var linea in lineas)
                {
                    linea.Estado = EstadoSubastaDetalle.ADJUDICADO;
                    linea.PujaGanadoraId = ganadora.Id;
                    linea.PedidoDigitalId = pedido.Id;
                }

                foreach (var puja in subasta.Pujas)
                {
                    puja.EsGanadora = puja.Id == ganadora.Id;
                }

                subasta.PedidoDigitalId = pedido.Id;
                subasta.PujaGanadoraId = ganadora.Id;
                subasta.Estado = EstadoSubastaTcg.ADJUDICADA;

                await GuardarCambiosAsync(ct);
                await tx.CommitAsync(ct);
            },
            cancellationToken);
    }

    private async Task AdjudicarLineaIndividualCoreAsync(
        SubastaTcg subasta,
        AdjudicarSubastaRequest? checkout,
        CancellationToken cancellationToken)
    {
        if (subasta.Estado is not (EstadoSubastaTcg.ACTIVA or EstadoSubastaTcg.CERRADA))
        {
            throw new BusinessRuleException("El evento debe estar activo o cerrado para adjudicar una carta.");
        }

        if (checkout?.SubastaDetalleId is not { } detalleId || detalleId == Guid.Empty)
        {
            throw new BusinessRuleException("Selecciona la carta a adjudicar dentro del evento.");
        }

        var linea = subasta.Detalles.FirstOrDefault(d => d.Id == detalleId)
            ?? throw new BusinessRuleException("La carta seleccionada no pertenece a este evento.");

        if (linea.Estado != EstadoSubastaDetalle.PENDIENTE || linea.PedidoDigitalId.HasValue)
        {
            throw new BusinessRuleException("Esa carta ya no está pendiente de adjudicación.");
        }

        var ganadora = await ResolverGanadoraLineaAsync(subasta, linea, checkout, cancellationToken);

        if (!AlcanzaReserva(subasta, ganadora))
        {
            throw new BusinessRuleException(MensajeSinGanadora(subasta));
        }

        var libre = await kardex.LibreAsync(subasta.SedeId, linea.ProductoId, cancellationToken);
        if (libre < linea.Cantidad)
        {
            throw new BusinessRuleException(
                $"No hay stock libre en la sede para reservar {NombreVisibleLinea(linea)} (necesario: {linea.Cantidad}, libre: {libre}).");
        }

        Cliente? clienteGanador = null;
        if (ganadora.ClienteId is { } clienteId)
        {
            clienteGanador = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await kardex.AplicarMuchosAsync(
            [
                new KardexComando(
                    subasta.SedeId,
                    linea.ProductoId,
                    TipoMovimientoInventario.PUJA_GANADORA_RESERVA,
                    linea.Cantidad,
                    $"Puja ganadora {EtiquetaCanal(subasta.Canal)} · {NombreVisibleLinea(linea)}",
                    "SUBASTA_TCG",
                    subasta.Id)
            ],
            cancellationToken);

        var pedido = CrearPedidoDesdeSubasta(subasta, ganadora, [linea], checkout, clienteGanador);
        db.PedidosDigitales.Add(pedido);
        AplicarPreferenciasCliente(checkout, clienteGanador, pedido);

        foreach (var puja in PujasDeLinea(subasta, linea.Id))
        {
            puja.EsGanadora = puja.Id == ganadora.Id;
        }

        linea.Estado = EstadoSubastaDetalle.ADJUDICADO;
        linea.PujaGanadoraId = ganadora.Id;
        linea.PedidoDigitalId = pedido.Id;

        ActualizarEstadoEventoTrasLineas(subasta, pedido.Id, ganadora.Id);

        await GuardarCambiosAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static void AplicarPreferenciasCliente(
        AdjudicarSubastaRequest? checkout,
        Cliente? clienteGanador,
        PedidoDigital pedido)
    {
        if (checkout?.GuardarPuntoEnCliente != true || clienteGanador is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(pedido.ClienteTelefono))
        {
            clienteGanador.Telefono = pedido.ClienteTelefono;
        }

        if (!string.IsNullOrWhiteSpace(pedido.PuntoEntrega))
        {
            clienteGanador.PuntoEntregaPreferido = pedido.PuntoEntrega;
        }

        if (pedido.CanalContacto.HasValue)
        {
            clienteGanador.CanalContacto = pedido.CanalContacto;
        }

        if (!string.IsNullOrWhiteSpace(pedido.ContactoReferencia))
        {
            clienteGanador.ContactoReferencia = pedido.ContactoReferencia;
        }
    }

    public async Task<SubastaTcgResponse> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var subasta = await CargarAsync(id, cancellationToken);
        if (subasta.Estado == EstadoSubastaTcg.CANCELADA)
        {
            throw new BusinessRuleException("La subasta ya está cancelada.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES)
        {
            var pedidosLinea = subasta.Detalles
                .Where(d => d.PedidoDigitalId.HasValue)
                .Select(d => d.PedidoDigitalId!.Value)
                .Distinct()
                .ToList();

            foreach (var pedidoId in pedidosLinea)
            {
                var pedido = await db.PedidosDigitales
                    .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);

                if (pedido is not null
                    && pedido.Estado != EstadoPedidoDigital.PendientePago
                    && pedido.Estado != EstadoPedidoDigital.Cancelado)
                {
                    throw new BusinessRuleException(
                        "No se puede cancelar: hay pedidos asociados que ya avanzaron en el Kanban.");
                }

                if (pedido is { Estado: EstadoPedidoDigital.PendientePago })
                {
                    var linea = subasta.Detalles.First(d => d.PedidoDigitalId == pedidoId);
                    await kardex.AplicarMuchosAsync(
                        [
                            new KardexComando(
                                subasta.SedeId,
                                linea.ProductoId,
                                TipoMovimientoInventario.LIBERACION_RESERVA,
                                linea.Cantidad,
                                $"Cancelación de evento · {linea.Producto.Nombre}",
                                "SUBASTA_TCG",
                                subasta.Id)
                        ],
                        cancellationToken);

                    var anterior = pedido.Estado;
                    pedido.Estado = EstadoPedidoDigital.Cancelado;
                    pedido.IndicadorReserva = IndicadorReservaPedido.Liberado;
                    pedido.Historial.Add(new PedidoDigitalHistorialEstado
                    {
                        Id = Guid.NewGuid(),
                        PedidoDigitalId = pedido.Id,
                        EmpresaId = tenant.EmpresaId,
                        EstadoAnterior = anterior,
                        EstadoNuevo = EstadoPedidoDigital.Cancelado,
                        UsuarioId = currentUser.UserId,
                        Fecha = DateTimeOffset.UtcNow,
                        Observacion = "Cancelación de evento de subasta."
                    });
                }
            }
        }
        else if (subasta.Estado == EstadoSubastaTcg.ADJUDICADA && subasta.PedidoDigitalId is { } pedidoCabeceraId)
        {
            var pedido = await db.PedidosDigitales
                .FirstOrDefaultAsync(p => p.Id == pedidoCabeceraId, cancellationToken);

            if (pedido is not null
                && pedido.Estado != EstadoPedidoDigital.PendientePago
                && pedido.Estado != EstadoPedidoDigital.Cancelado)
            {
                throw new BusinessRuleException("No se puede cancelar: el pedido asociado ya avanzó en el Kanban.");
            }

            if (pedido is { Estado: EstadoPedidoDigital.PendientePago })
            {
                var lineas = LineasOrdenadas(subasta);
                await kardex.AplicarMuchosAsync(
                    lineas.Select(linea => new KardexComando(
                        subasta.SedeId,
                        linea.ProductoId,
                        TipoMovimientoInventario.LIBERACION_RESERVA,
                        linea.Cantidad,
                        $"Cancelación de subasta · {linea.Producto.Nombre}",
                        "SUBASTA_TCG",
                        subasta.Id)),
                    cancellationToken);

                var anterior = pedido.Estado;
                pedido.Estado = EstadoPedidoDigital.Cancelado;
                pedido.IndicadorReserva = IndicadorReservaPedido.Liberado;
                pedido.Historial.Add(new PedidoDigitalHistorialEstado
                {
                    Id = Guid.NewGuid(),
                    PedidoDigitalId = pedido.Id,
                    EmpresaId = tenant.EmpresaId,
                    EstadoAnterior = anterior,
                    EstadoNuevo = EstadoPedidoDigital.Cancelado,
                    UsuarioId = currentUser.UserId,
                    Fecha = DateTimeOffset.UtcNow,
                    Observacion = "Cancelación de subasta adjudicada."
                });
            }
        }

        subasta.Estado = EstadoSubastaTcg.CANCELADA;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    private IQueryable<SubastaTcg> QueryBase() =>
        db.SubastasTcg
            .AsNoTracking()
            .Include(s => s.Sede)
            .Include(s => s.Producto)
            .Include(s => s.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(s => s.Pujas);

    private async Task<SubastaTcg> CargarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.SubastasTcg
            .Include(s => s.Sede)
            .Include(s => s.Producto)
            .Include(s => s.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(s => s.Pujas)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró la subasta.", StatusCodes.Status404NotFound);

    private async Task<IReadOnlyList<LineaValidada>> ValidarCabeceraYLineasAsync(
        CrearSubastaTcgRequest request,
        CancellationToken cancellationToken)
    {
        _ = await db.Sedes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
            ?? throw new BusinessRuleException("Selecciona una sede válida.");

        if (request.Titulo.Trim().Length < 3)
        {
            throw new BusinessRuleException("El título debe tener al menos 3 caracteres.");
        }

        if (request.PrecioBase <= 0)
        {
            throw new BusinessRuleException("El precio base debe ser mayor que cero.");
        }

        if (request.IncrementoMinimo <= 0)
        {
            throw new BusinessRuleException("El incremento mínimo debe ser mayor que cero.");
        }

        if (request.PrecioReserva is { } reserva && reserva < request.PrecioBase)
        {
            throw new BusinessRuleException("El precio reserva no puede ser menor que el precio base.");
        }

        if (request.FechaCierre <= request.FechaInicio)
        {
            throw new BusinessRuleException("La fecha de cierre debe ser posterior al inicio.");
        }

        var inputs = NormalizarInputsLinea(request);
        if (inputs.Count == 0)
        {
            throw new BusinessRuleException("Indica al menos un producto para la subasta (Single Hit, Bulk o Combo).");
        }

        var lineas = new List<LineaValidada>();
        var vistos = new HashSet<Guid>();
        var esIndividuales = request.Modo == ModoSubastaTcg.INDIVIDUALES;

        foreach (var input in inputs)
        {
            if (input.Cantidad < 1 || input.Cantidad != Math.Floor(input.Cantidad))
            {
                throw new BusinessRuleException("Cada cantidad debe ser un entero mayor o igual a 1.");
            }

            if (!esIndividuales && !vistos.Add(input.ProductoId))
            {
                throw new BusinessRuleException("No repitas el mismo producto en las líneas; suma la cantidad en una sola línea.");
            }

            var producto = await db.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == input.ProductoId, cancellationToken);
            if (producto is null || !producto.Activo)
            {
                throw new BusinessRuleException("Selecciona un producto activo del catálogo.");
            }

            var titulo = TextoOpcional(input.TituloPersonalizado, 200);
            if (esIndividuales)
            {
                // Cada unidad física = fila independiente (cantidad 1) para su propio ganador/pedido.
                var unidades = (int)input.Cantidad;
                for (var i = 0; i < unidades; i++)
                {
                    lineas.Add(new LineaValidada(producto, 1m, titulo));
                }
            }
            else
            {
                lineas.Add(new LineaValidada(producto, Round3(input.Cantidad), titulo));
            }
        }

        return lineas;
    }

    private static List<SubastaDetalleInput> NormalizarInputsLinea(CrearSubastaTcgRequest request)
    {
        if (request.Detalles.Count > 0)
        {
            return request.Detalles;
        }

        if (request.ProductoId is { } productoId && productoId != Guid.Empty)
        {
            return [new SubastaDetalleInput { ProductoId = productoId, Cantidad = 1 }];
        }

        return [];
    }

    private PedidoDigital CrearPedidoDesdeSubasta(
        SubastaTcg subasta,
        Puja ganadora,
        IReadOnlyList<SubastaDetalle> lineas,
        AdjudicarSubastaRequest? checkout,
        Cliente? clienteGanador)
    {
        var total = Round2(ganadora.Monto);
        var (subtotal, igv, totalR) = IgvCalculo.DesdeTotal(total);
        var ahora = DateTimeOffset.UtcNow;
        var pedidoId = Guid.NewGuid();
        var precios = DistribuirMonto(lineas, totalR);
        var metodo = checkout?.MetodoEnvio ?? MetodoEnvio.RECOJO_TIENDA;
        var esRecojo = metodo == MetodoEnvio.RECOJO_TIENDA;
        var destinatario = TextoOpcional(checkout?.DestinatarioNombre, 160) ?? ganadora.NombrePostor;
        var telefono = TextoOpcional(checkout?.DestinatarioTelefono, 32)
            ?? clienteGanador?.Telefono;
        var puntoEntrega = TextoOpcional(checkout?.PuntoEntrega, 80)
            ?? clienteGanador?.PuntoEntregaPreferido;
        var canalContacto = checkout?.CanalContacto ?? clienteGanador?.CanalContacto;
        var contactoReferencia = TextoOpcional(checkout?.ContactoReferencia, 160)
            ?? clienteGanador?.ContactoReferencia;
        var preferenciaPago = checkout?.OrigenPagoPreferido is { } origen
            ? $"Pago preferido: {EtiquetaOrigenPago(origen)}."
            : null;
        var observacion = string.Join(
            " · ",
            new[]
            {
                lineas.Count == 1
                    ? $"Adjudicación · {NombreVisibleLinea(lineas[0])} · {subasta.Titulo}"
                    : $"Adjudicación subasta · {subasta.Titulo}",
                preferenciaPago
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var pedido = new PedidoDigital
        {
            Id = pedidoId,
            EmpresaId = tenant.EmpresaId,
            ClienteId = ganadora.ClienteId,
            ClienteNombre = ganadora.NombrePostor,
            ClienteTelefono = telefono,
            SedeId = subasta.SedeId,
            CanalPedido = CanalPedidoDesdeSubasta(subasta.Canal),
            Estado = EstadoPedidoDigital.PendientePago,
            IndicadorReserva = IndicadorReservaPedido.Reservado,
            FechaPedido = ahora,
            Subtotal = subtotal,
            Igv = igv,
            Total = totalR,
            ReferenciaExterna = $"{subasta.Id:N}-{pedidoId:N}",
            Observacion = observacion,
            SubastaTcgId = subasta.Id,
            DestinatarioNombre = destinatario,
            DestinatarioTelefono = telefono,
            EntregaDireccion = esRecojo ? null : TextoOpcional(checkout?.EntregaDireccion, 300),
            EntregaDistrito = esRecojo ? null : TextoOpcional(checkout?.EntregaDistrito, 80),
            EntregaProvincia = esRecojo ? null : TextoOpcional(checkout?.EntregaProvincia, 80),
            EntregaDepartamento = esRecojo ? null : TextoOpcional(checkout?.EntregaDepartamento, 80),
            Agencia = esRecojo ? null : (TextoOpcional(checkout?.Agencia, 80) ?? puntoEntrega),
            PuntoEntrega = puntoEntrega,
            CanalContacto = canalContacto,
            ContactoReferencia = contactoReferencia,
            EsRecojoTienda = esRecojo,
            Courier = PedidoKanban.EtiquetaCourier(metodo)
        };

        for (var i = 0; i < lineas.Count; i++)
        {
            var linea = lineas[i];
            var totalLinea = precios[i];
            var precioUnitario = linea.Cantidad == 0 ? 0 : Round2(totalLinea / linea.Cantidad);
            pedido.Detalles.Add(new PedidoDigitalDetalle
            {
                Id = Guid.NewGuid(),
                PedidoDigitalId = pedidoId,
                EmpresaId = tenant.EmpresaId,
                ProductoId = linea.ProductoId,
                Descripcion = NombreVisibleLinea(linea),
                Cantidad = linea.Cantidad,
                PrecioUnitario = precioUnitario,
                Total = totalLinea
            });
        }

        pedido.Historial.Add(new PedidoDigitalHistorialEstado
        {
            Id = Guid.NewGuid(),
            PedidoDigitalId = pedidoId,
            EmpresaId = tenant.EmpresaId,
            EstadoAnterior = null,
            EstadoNuevo = EstadoPedidoDigital.PendientePago,
            UsuarioId = currentUser.UserId,
            Fecha = ahora,
            Observacion = "Pedido generado al adjudicar la subasta. Reserva cubierta por PUJA_GANADORA_RESERVA."
        });

        return pedido;
    }

    /// <summary>
    /// Reparte el monto ganador entre líneas proporcional al precio de catálogo × cantidad;
    /// la última línea absorbe el redondeo para que la suma coincida exacta.
    /// </summary>
    private static List<decimal> DistribuirMonto(IReadOnlyList<SubastaDetalle> lineas, decimal total)
    {
        if (lineas.Count == 1)
        {
            return [total];
        }

        var pesos = lineas
            .Select(l => Math.Max(l.Producto.PrecioVenta, 0.01m) * l.Cantidad)
            .ToList();
        var sumaPesos = pesos.Sum();
        var asignados = new List<decimal>(lineas.Count);
        decimal acumulado = 0;

        for (var i = 0; i < lineas.Count; i++)
        {
            if (i == lineas.Count - 1)
            {
                asignados.Add(Round2(total - acumulado));
                break;
            }

            var parte = Round2(total * (pesos[i] / sumaPesos));
            asignados.Add(parte);
            acumulado += parte;
        }

        return asignados;
    }

    private static List<SubastaDetalle> LineasOrdenadas(SubastaTcg subasta) =>
        subasta.Detalles.OrderBy(d => d.Orden).ThenBy(d => d.Id).ToList();

    private static bool EstaVencida(DateTimeOffset fechaCierre, DateTimeOffset? ahora = null) =>
        (ahora ?? DateTimeOffset.UtcNow).UtcTicks >= fechaCierre.UtcTicks;

    private static void MarcarCierre(SubastaTcg subasta)
    {
        subasta.Estado = EstadoSubastaTcg.CERRADA;
        subasta.FechaCierreReal ??= DateTimeOffset.UtcNow;

        if (subasta.Modo == ModoSubastaTcg.INDIVIDUALES)
        {
            foreach (var linea in LineasOrdenadas(subasta).Where(d => d.Estado == EstadoSubastaDetalle.PENDIENTE))
            {
                var maxima = PujaMaximaDeLinea(subasta, linea.Id);
                var alcanza = AlcanzaReserva(subasta, maxima);
                linea.PujaGanadoraId = alcanza && maxima is not null ? maxima.Id : null;
                foreach (var puja in PujasDeLinea(subasta, linea.Id))
                {
                    puja.EsGanadora = alcanza && maxima is not null && puja.Id == maxima.Id;
                }
            }

            return;
        }

        var maximaLote = PujaMaxima(subasta);
        var alcanzaLote = AlcanzaReserva(subasta, maximaLote);
        subasta.PujaGanadoraId = alcanzaLote && maximaLote is not null ? maximaLote.Id : null;
        foreach (var puja in subasta.Pujas)
        {
            puja.EsGanadora = alcanzaLote && maximaLote is not null && puja.Id == maximaLote.Id;
        }
    }

    private static void MarcarCierreSinGanador(SubastaTcg subasta)
    {
        subasta.Estado = EstadoSubastaTcg.CERRADA;
        subasta.FechaCierreReal ??= DateTimeOffset.UtcNow;
        subasta.PujaGanadoraId = null;
        foreach (var puja in subasta.Pujas)
        {
            puja.EsGanadora = false;
        }

        foreach (var linea in subasta.Detalles.Where(d => d.Estado == EstadoSubastaDetalle.PENDIENTE))
        {
            linea.PujaGanadoraId = null;
            linea.Estado = EstadoSubastaDetalle.DESIERTA;
        }
    }

    private static void RecalcularLiderLote(SubastaTcg subasta)
    {
        foreach (var puja in subasta.Pujas)
        {
            puja.EsGanadora = false;
        }

        var lider = PujaMaxima(subasta);
        if (lider is null)
        {
            subasta.PujaGanadoraId = null;
            return;
        }

        lider.EsGanadora = true;
        subasta.PujaGanadoraId = lider.Id;
    }

    private static void RecalcularLiderLinea(SubastaTcg subasta, Guid detalleId)
    {
        foreach (var puja in PujasDeLinea(subasta, detalleId))
        {
            puja.EsGanadora = false;
        }

        var linea = subasta.Detalles.FirstOrDefault(d => d.Id == detalleId);
        var lider = PujaMaximaDeLinea(subasta, detalleId);
        if (linea is not null)
        {
            linea.PujaGanadoraId = lider?.Id;
        }

        if (lider is null)
        {
            if (subasta.PujaGanadoraId is { } id &&
                !subasta.Pujas.Any(p => p.Id == id))
            {
                subasta.PujaGanadoraId = null;
            }

            return;
        }

        lider.EsGanadora = true;
    }

    private static void ActualizarEstadoEventoTrasLineas(
        SubastaTcg subasta,
        Guid? ultimoPedidoId = null,
        Guid? ultimaPujaId = null)
    {
        var pendientes = subasta.Detalles.Count(d => d.Estado == EstadoSubastaDetalle.PENDIENTE);
        if (pendientes > 0)
        {
            if (subasta.Estado != EstadoSubastaTcg.CERRADA)
            {
                subasta.Estado = EstadoSubastaTcg.ACTIVA;
            }

            return;
        }

        subasta.FechaCierreReal ??= DateTimeOffset.UtcNow;
        var huboAdjudicacion = subasta.Detalles.Any(d => d.Estado == EstadoSubastaDetalle.ADJUDICADO);
        subasta.Estado = huboAdjudicacion ? EstadoSubastaTcg.ADJUDICADA : EstadoSubastaTcg.CERRADA;
        if (ultimoPedidoId is { } pedidoId)
        {
            subasta.PedidoDigitalId = pedidoId;
        }

        if (ultimaPujaId is { } pujaId)
        {
            subasta.PujaGanadoraId = pujaId;
        }
    }

    private async Task<Puja> ResolverGanadoraLineaAsync(
        SubastaTcg subasta,
        SubastaDetalle linea,
        AdjudicarSubastaRequest? checkout,
        CancellationToken cancellationToken)
    {
        var existente = PujaMaximaDeLinea(subasta, linea.Id);
        if (existente is not null)
        {
            return existente;
        }

        return await CrearPujaDirectaAsync(subasta, linea.Id, checkout, cancellationToken);
    }

    private async Task<Puja?> ResolverGanadoraLoteAsync(
        SubastaTcg subasta,
        AdjudicarSubastaRequest? checkout,
        CancellationToken cancellationToken)
    {
        var existente = PujaMaxima(subasta);
        if (existente is not null)
        {
            return existente;
        }

        if (checkout?.MontoAdjudicado is null && string.IsNullOrWhiteSpace(checkout?.NombrePostor))
        {
            return null;
        }

        return await CrearPujaDirectaAsync(subasta, null, checkout, cancellationToken);
    }

    private async Task<Puja> CrearPujaDirectaAsync(
        SubastaTcg subasta,
        Guid? subastaDetalleId,
        AdjudicarSubastaRequest? checkout,
        CancellationToken cancellationToken)
    {
        var nombre = checkout?.NombrePostor?.Trim()
            ?? checkout?.DestinatarioNombre?.Trim()
            ?? string.Empty;
        if (nombre.Length < 2)
        {
            throw new BusinessRuleException(
                "Sin pujas previas: indica el ganador y el monto para adjudicar directamente.");
        }

        var monto = checkout?.MontoAdjudicado is { } m && m > 0
            ? Round2(m)
            : throw new BusinessRuleException(
                "Sin pujas previas: indica el monto adjudicado.");

        if (monto < subasta.PrecioBase)
        {
            throw new BusinessRuleException(
                $"El monto adjudicado debe ser al menos el precio base ({subasta.PrecioBase:0.00}).");
        }

        Guid? clienteId = checkout?.ClienteId;
        if (clienteId is { } cid)
        {
            var existe = await db.Clientes.AsNoTracking().AnyAsync(c => c.Id == cid, cancellationToken);
            if (!existe)
            {
                throw new BusinessRuleException("El cliente indicado no existe.");
            }
        }

        var puja = new Puja
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SubastaTcgId = subasta.Id,
            SubastaDetalleId = subastaDetalleId,
            ClienteId = clienteId,
            NombrePostor = nombre,
            Monto = monto,
            Fecha = DateTimeOffset.UtcNow,
            EsGanadora = true
        };
        db.Pujas.Add(puja);
        subasta.Pujas.Add(puja);
        return puja;
    }

    private async Task<SubastaTcgResponse> EjecutarConReintentoConcurrenciaAsync(
        Guid id,
        Func<SubastaTcg, CancellationToken, Task> accion,
        CancellationToken cancellationToken,
        int maxIntentos = 3)
    {
        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            try
            {
                db.ChangeTracker.Clear();
                var subasta = await CargarAsync(id, cancellationToken);
                await accion(subasta, cancellationToken);
                return (await ObtenerAsync(id, cancellationToken))!;
            }
            catch (DbUpdateConcurrencyException) when (intento < maxIntentos)
            {
                db.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BusinessRuleException(
                    "No se pudo guardar: otro proceso actualizó la subasta. Recarga e intenta de nuevo.");
            }
        }

        throw new BusinessRuleException(
            "No se pudo guardar: otro proceso actualizó la subasta. Recarga e intenta de nuevo.");
    }

    private async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw;
        }
    }

    private static void PromoverSiguienteGanadora(SubastaTcg subasta)
    {
        var anterior = subasta.Pujas.FirstOrDefault(p => p.Id == subasta.PujaGanadoraId)
            ?? subasta.Pujas.FirstOrDefault(p => p.EsGanadora);

        foreach (var puja in subasta.Pujas)
        {
            puja.EsGanadora = false;
        }

        IEnumerable<Puja> candidatas = subasta.Pujas;
        if (anterior is not null)
        {
            candidatas = candidatas.Where(p => p.Id != anterior.Id);
            if (anterior.ClienteId is { } clienteId)
            {
                candidatas = candidatas.Where(p => p.ClienteId != clienteId);
            }
            else
            {
                candidatas = candidatas.Where(p =>
                    !string.Equals(p.NombrePostor, anterior.NombrePostor, StringComparison.OrdinalIgnoreCase));
            }
        }

        var siguiente = candidatas.MaxBy(p => p.Monto);
        if (AlcanzaReserva(subasta, siguiente) && siguiente is not null)
        {
            siguiente.EsGanadora = true;
            subasta.PujaGanadoraId = siguiente.Id;
            return;
        }

        subasta.PujaGanadoraId = null;
    }

    private static void PromoverSiguienteGanadoraLinea(SubastaTcg subasta, SubastaDetalle linea)
    {
        var anterior = PujasDeLinea(subasta, linea.Id).FirstOrDefault(p => p.Id == linea.PujaGanadoraId)
            ?? PujasDeLinea(subasta, linea.Id).FirstOrDefault(p => p.EsGanadora);

        foreach (var puja in PujasDeLinea(subasta, linea.Id))
        {
            puja.EsGanadora = false;
        }

        IEnumerable<Puja> candidatas = PujasDeLinea(subasta, linea.Id);
        if (anterior is not null)
        {
            candidatas = candidatas.Where(p => p.Id != anterior.Id);
            if (anterior.ClienteId is { } clienteId)
            {
                candidatas = candidatas.Where(p => p.ClienteId != clienteId);
            }
            else
            {
                candidatas = candidatas.Where(p =>
                    !string.Equals(p.NombrePostor, anterior.NombrePostor, StringComparison.OrdinalIgnoreCase));
            }
        }

        var siguiente = candidatas.MaxBy(p => p.Monto);
        if (AlcanzaReserva(subasta, siguiente) && siguiente is not null)
        {
            siguiente.EsGanadora = true;
            linea.PujaGanadoraId = siguiente.Id;
            return;
        }

        linea.PujaGanadoraId = null;
    }

    private static bool AlcanzaReserva(SubastaTcg subasta, Puja? maxima) =>
        maxima is not null && (subasta.PrecioReserva is null || maxima.Monto >= subasta.PrecioReserva);

    private static string MensajeSinGanadora(SubastaTcg subasta) =>
        subasta.PrecioReserva is { } reserva
            ? $"No se alcanzó el precio reserva (S/ {reserva:0.00})."
            : "No hay una puja ganadora para adjudicar.";

    private static IEnumerable<Puja> PujasDeLinea(SubastaTcg subasta, Guid detalleId) =>
        subasta.Pujas.Where(p => p.SubastaDetalleId == detalleId);

    private static Puja? PujaMaxima(SubastaTcg subasta) =>
        subasta.Pujas.Count == 0 ? null : subasta.Pujas.MaxBy(p => p.Monto);

    private static Puja? PujaMaximaDeLinea(SubastaTcg subasta, Guid detalleId)
    {
        var pujas = PujasDeLinea(subasta, detalleId).ToList();
        return pujas.Count == 0 ? null : pujas.MaxBy(p => p.Monto);
    }

    private static decimal MontoMinimoSiguiente(SubastaTcg subasta, Guid? detalleId = null)
    {
        IEnumerable<Puja> universo = detalleId is { } id
            ? PujasDeLinea(subasta, id)
            : subasta.Pujas;
        var ultima = universo.OrderBy(p => p.Fecha).LastOrDefault();
        return ultima is null ? Round2(subasta.PrecioBase) : Round2(ultima.Monto + subasta.IncrementoMinimo);
    }

    private static CanalPedidoDigital CanalPedidoDesdeSubasta(CanalSubastaTcg canal) => canal switch
    {
        CanalSubastaTcg.FACEBOOK_SUBASTA => CanalPedidoDigital.FACEBOOK_SUBASTA,
        CanalSubastaTcg.WEB => CanalPedidoDigital.WEB,
        _ => CanalPedidoDigital.OTRO
    };

    private static string EtiquetaCanal(CanalSubastaTcg canal) => canal switch
    {
        CanalSubastaTcg.FACEBOOK_SUBASTA => "Facebook",
        CanalSubastaTcg.PRESENCIAL => "presencial",
        CanalSubastaTcg.WEB => "web",
        _ => "otro canal"
    };

    private static string EtiquetaOrigenPago(OrigenPago origen) => origen switch
    {
        OrigenPago.YAPE => "Yape",
        OrigenPago.PLIN => "Plin",
        OrigenPago.TRANSFERENCIA => "Transferencia",
        OrigenPago.IZIPAY => "Izipay",
        OrigenPago.EFECTIVO => "Efectivo",
        OrigenPago.TARJETA => "Tarjeta",
        _ => origen.ToString()
    };

    private static string? TextoOpcional(string? valor, int maxLength = 500)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, maxLength)];
    }

    private static decimal Round2(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private static decimal Round3(decimal valor) => Math.Round(valor, 3, MidpointRounding.AwayFromZero);

    private static SubastaTcgResponse Map(SubastaTcg subasta)
    {
        var pujas = subasta.Pujas.OrderBy(p => p.Fecha).ToList();
        var ganadora = pujas.FirstOrDefault(p => p.Id == subasta.PujaGanadoraId)
            ?? pujas.MaxBy(p => p.Monto);
        var oferta = ganadora?.Monto;
        decimal? diferencia = oferta is null ? null : Round2(oferta.Value - subasta.PrecioBase);

        var detalles = LineasOrdenadas(subasta)
            .Select(d => new SubastaDetalleResponse
            {
                Id = d.Id,
                ProductoId = d.ProductoId,
                ProductoNombre = d.Producto.Nombre,
                TituloPersonalizado = d.TituloPersonalizado,
                CodigoSku = d.Producto.CodigoSku,
                TipoProducto = d.Producto.TipoProducto,
                Cantidad = d.Cantidad,
                Orden = d.Orden,
                Estado = d.Estado,
                PujaGanadoraId = d.PujaGanadoraId,
                PedidoDigitalId = d.PedidoDigitalId,
                PedidoCodigo = CodigoAmigable.Pedido(d.PedidoDigitalId)
            })
            .ToList();

        var lineaPrincipal = LineasOrdenadas(subasta).FirstOrDefault();
        var productoPrincipal = lineaPrincipal?.Producto ?? subasta.Producto
            ?? throw new InvalidOperationException("La subasta no tiene producto asociado.");

        // Compat: si aún no hay backfill de detalles, sintetizar desde Producto cabecera.
        if (detalles.Count == 0)
        {
            detalles.Add(new SubastaDetalleResponse
            {
                Id = Guid.Empty,
                ProductoId = subasta.ProductoId,
                ProductoNombre = productoPrincipal.Nombre,
                TituloPersonalizado = null,
                CodigoSku = productoPrincipal.CodigoSku,
                TipoProducto = productoPrincipal.TipoProducto,
                Cantidad = 1,
                Orden = 1,
                Estado = EstadoSubastaDetalle.PENDIENTE,
                PujaGanadoraId = null,
                PedidoDigitalId = null,
                PedidoCodigo = null
            });
        }

        return new SubastaTcgResponse
        {
            Id = subasta.Id,
            Codigo = CodigoAmigable.Subasta(subasta.Id),
            SedeId = subasta.SedeId,
            SedeNombre = subasta.Sede.Nombre,
            ProductoId = subasta.ProductoId,
            ProductoNombre = lineaPrincipal is null
                ? productoPrincipal.Nombre
                : NombreVisibleLinea(lineaPrincipal),
            CodigoSku = productoPrincipal.CodigoSku,
            TipoProducto = productoPrincipal.TipoProducto,
            Detalles = detalles,
            Titulo = subasta.Titulo,
            Canal = subasta.Canal,
            Modo = subasta.Modo,
            PrecioBase = subasta.PrecioBase,
            IncrementoMinimo = subasta.IncrementoMinimo,
            PrecioReserva = subasta.PrecioReserva,
            FechaInicio = subasta.FechaInicio,
            FechaCierre = subasta.FechaCierre,
            FechaCierreReal = subasta.FechaCierreReal,
            Estado = subasta.Estado,
            PujaGanadoraId = subasta.PujaGanadoraId,
            PedidoDigitalId = subasta.PedidoDigitalId,
            PedidoCodigo = CodigoAmigable.Pedido(subasta.PedidoDigitalId),
            Observacion = subasta.Observacion,
            Pujas = pujas.Select(p => new PujaResponse
            {
                Id = p.Id,
                SubastaTcgId = p.SubastaTcgId,
                SubastaDetalleId = p.SubastaDetalleId,
                ClienteId = p.ClienteId,
                NombrePostor = p.NombrePostor,
                Monto = p.Monto,
                Fecha = p.Fecha,
                EsGanadora = p.EsGanadora
            }).ToList(),
            Margen = new MargenSubastaDto
            {
                PrecioBase = subasta.PrecioBase,
                OfertaReferencia = oferta,
                Diferencia = diferencia,
                Porcentaje = oferta is null || subasta.PrecioBase <= 0
                    ? null
                    : Round2(diferencia!.Value / subasta.PrecioBase * 100)
            }
        };
    }

    private readonly record struct LineaValidada(Producto Producto, decimal Cantidad, string? TituloPersonalizado);

    private static string NombreVisibleLinea(SubastaDetalle linea) =>
        string.IsNullOrWhiteSpace(linea.TituloPersonalizado)
            ? linea.Producto.Nombre
            : linea.TituloPersonalizado.Trim();
}

public readonly record struct CierreVencidasResultado(int Cerradas);
