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
    public async Task<IReadOnlyList<SubastaTcgResponse>> ListarAsync(
        EstadoSubastaTcg? estado,
        CanalSubastaTcg? canal,
        Guid? sedeId,
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
                Orden = i + 1
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

        var monto = Round2(request.Monto);
        var minimo = MontoMinimoSiguiente(subasta);
        if (monto < minimo)
        {
            throw new BusinessRuleException($"La puja debe ser de al menos {minimo:0.00}.");
        }

        var puja = new Puja
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SubastaTcgId = subasta.Id,
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
        var subasta = await CargarAsync(id, cancellationToken);
        if (subasta.Estado != EstadoSubastaTcg.ACTIVA)
        {
            throw new BusinessRuleException("Solo se puede cerrar una subasta activa.");
        }

        MarcarCierre(subasta);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<CierreVencidasResultado> CerrarVencidasAsync(CancellationToken cancellationToken)
    {
        var ahora = DateTimeOffset.UtcNow;
        var activas = await db.SubastasTcg
            .IgnoreQueryFilters()
            .Include(s => s.Pujas)
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
            .FirstOrDefaultAsync(s => s.Id == subastaTcgId.Value, cancellationToken);

        if (subasta is null
            || subasta.Estado != EstadoSubastaTcg.ADJUDICADA
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
        var subasta = await CargarAsync(id, cancellationToken);

        if (subasta.Estado == EstadoSubastaTcg.ACTIVA)
        {
            var maxima = PujaMaxima(subasta);
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
            ?? throw new BusinessRuleException(MensajeSinGanadora(subasta));

        var lineas = LineasOrdenadas(subasta);
        if (lineas.Count == 0)
        {
            throw new BusinessRuleException("La subasta no tiene productos para adjudicar.");
        }

        foreach (var linea in lineas)
        {
            var libre = await kardex.LibreAsync(subasta.SedeId, linea.ProductoId, cancellationToken);
            if (libre < linea.Cantidad)
            {
                throw new BusinessRuleException(
                    $"No hay stock libre en la sede para reservar {linea.Producto.Nombre} (necesario: {linea.Cantidad}, libre: {libre}).");
            }
        }

        Cliente? clienteGanador = null;
        if (ganadora.ClienteId is { } clienteId)
        {
            clienteGanador = await db.Clientes.FirstOrDefaultAsync(c => c.Id == clienteId, cancellationToken);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        await kardex.AplicarMuchosAsync(
            lineas.Select(linea => new KardexComando(
                subasta.SedeId,
                linea.ProductoId,
                TipoMovimientoInventario.PUJA_GANADORA_RESERVA,
                linea.Cantidad,
                $"Puja ganadora {EtiquetaCanal(subasta.Canal)} · {linea.Producto.Nombre}",
                "SUBASTA_TCG",
                subasta.Id)),
            cancellationToken);

        var pedido = CrearPedidoDesdeSubasta(subasta, ganadora, lineas, checkout, clienteGanador);
        db.PedidosDigitales.Add(pedido);

        if (checkout?.GuardarPuntoEnCliente == true && clienteGanador is not null)
        {
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

        subasta.PedidoDigitalId = pedido.Id;
        subasta.PujaGanadoraId = ganadora.Id;
        subasta.Estado = EstadoSubastaTcg.ADJUDICADA;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<SubastaTcgResponse> CancelarAsync(Guid id, CancellationToken cancellationToken)
    {
        var subasta = await CargarAsync(id, cancellationToken);
        if (subasta.Estado == EstadoSubastaTcg.CANCELADA)
        {
            throw new BusinessRuleException("La subasta ya está cancelada.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        if (subasta.Estado == EstadoSubastaTcg.ADJUDICADA && subasta.PedidoDigitalId is { } pedidoId)
        {
            var pedido = await db.PedidosDigitales
                .FirstOrDefaultAsync(p => p.Id == pedidoId, cancellationToken);

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

        foreach (var input in inputs)
        {
            if (input.Cantidad < 1 || input.Cantidad != Math.Floor(input.Cantidad))
            {
                throw new BusinessRuleException("Cada cantidad debe ser un entero mayor o igual a 1.");
            }

            if (!vistos.Add(input.ProductoId))
            {
                throw new BusinessRuleException("No repitas el mismo producto en las líneas; suma la cantidad en una sola línea.");
            }

            var producto = await db.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == input.ProductoId, cancellationToken);
            if (producto is null || !producto.Activo)
            {
                throw new BusinessRuleException("Selecciona un producto activo del catálogo.");
            }

            lineas.Add(new LineaValidada(producto, Round3(input.Cantidad)));
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
                $"Adjudicación subasta · {subasta.Titulo}",
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
                Descripcion = linea.Producto.Nombre,
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
        var maxima = PujaMaxima(subasta);
        var alcanza = AlcanzaReserva(subasta, maxima);
        subasta.Estado = EstadoSubastaTcg.CERRADA;
        subasta.FechaCierreReal ??= DateTimeOffset.UtcNow;
        subasta.PujaGanadoraId = alcanza && maxima is not null ? maxima.Id : null;
        foreach (var puja in subasta.Pujas)
        {
            puja.EsGanadora = alcanza && maxima is not null && puja.Id == maxima.Id;
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

    private static bool AlcanzaReserva(SubastaTcg subasta, Puja? maxima) =>
        maxima is not null && (subasta.PrecioReserva is null || maxima.Monto >= subasta.PrecioReserva);

    private static string MensajeSinGanadora(SubastaTcg subasta) =>
        subasta.PrecioReserva is { } reserva
            ? $"No se alcanzó el precio reserva (S/ {reserva:0.00})."
            : "No hay una puja ganadora para adjudicar.";

    private static Puja? PujaMaxima(SubastaTcg subasta) =>
        subasta.Pujas.Count == 0 ? null : subasta.Pujas.MaxBy(p => p.Monto);

    private static decimal MontoMinimoSiguiente(SubastaTcg subasta)
    {
        var ultima = subasta.Pujas.OrderBy(p => p.Fecha).LastOrDefault();
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
                CodigoSku = d.Producto.CodigoSku,
                TipoProducto = d.Producto.TipoProducto,
                Cantidad = d.Cantidad,
                Orden = d.Orden
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
                CodigoSku = productoPrincipal.CodigoSku,
                TipoProducto = productoPrincipal.TipoProducto,
                Cantidad = 1,
                Orden = 1
            });
        }

        return new SubastaTcgResponse
        {
            Id = subasta.Id,
            Codigo = CodigoAmigable.Subasta(subasta.Id),
            SedeId = subasta.SedeId,
            SedeNombre = subasta.Sede.Nombre,
            ProductoId = subasta.ProductoId,
            ProductoNombre = productoPrincipal.Nombre,
            CodigoSku = productoPrincipal.CodigoSku,
            TipoProducto = productoPrincipal.TipoProducto,
            Detalles = detalles,
            Titulo = subasta.Titulo,
            Canal = subasta.Canal,
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

    private readonly record struct LineaValidada(Producto Producto, decimal Cantidad);
}

public readonly record struct CierreVencidasResultado(int Cerradas);
