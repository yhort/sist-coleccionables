using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Application.Common;
using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Entregas;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Entregas;

public sealed class EntregasService(
    ApplicationDbContext db,
    PedidosDigitalesService pedidos)
{
    public async Task<IReadOnlyList<EntregaResponse>> ListarAsync(
        EstadoLogistica? estado,
        Guid? sedeOrigenId,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();
        if (estado.HasValue)
        {
            query = query.Where(e => e.Estado == estado.Value);
        }

        if (sedeOrigenId.HasValue)
        {
            query = query.Where(e => e.SedeOrigenId == sedeOrigenId.Value);
        }

        var entregas = await query
            .OrderByDescending(e => e.FechaCreacion)
            .ToListAsync(cancellationToken);

        return entregas.Select(Map).ToList();
    }

    public async Task<EntregaResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var entrega = await QueryBase().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entrega is null ? null : Map(entrega);
    }

    public async Task<EntregaResponse> EmpaquetarAsync(
        EmpaquetarEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var notas = request.NotasEmpaque.Trim();
        if (notas.Length < 3)
        {
            throw new BusinessRuleException("Registra una nota de empaque (mínimo 3 caracteres).");
        }

        var pedido = await pedidos.CargarAsync(request.PedidoDigitalId, cancellationToken);
        if (pedido.Estado is not (EstadoPedidoDigital.Pagado or EstadoPedidoDigital.Empaquetado))
        {
            throw new BusinessRuleException("Solo se empaqueta un pedido Pagado (o se actualizan notas en Empaquetado).");
        }

        if (pedido.Estado == EstadoPedidoDigital.Pagado)
        {
            await pedidos.CambiarEstadoAsync(pedido.Id, EstadoPedidoDigital.Empaquetado, notas, cancellationToken);
            pedido = await pedidos.CargarAsync(pedido.Id, cancellationToken);
        }
        else
        {
            pedido.NotasEmpaque = notas[..Math.Min(notas.Length, 500)];
        }

        var entrega = await AsegurarAsync(pedido, cancellationToken);
        entrega.NotasEmpaque = pedido.NotasEmpaque;
        entrega.FechaProgramada ??= DateTimeOffset.UtcNow;
        entrega.Observacion = notas;
        await pedidos.SincronizarSnapshotEntregaAsync(pedido, entrega);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(entrega.Id, cancellationToken))!;
    }

    public async Task<EntregaResponse> ProgramarAsync(
        ProgramarEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var pedido = await pedidos.CargarAsync(request.PedidoDigitalId, cancellationToken);
        if (pedido.Estado is not (EstadoPedidoDigital.Empaquetado or EstadoPedidoDigital.PendienteEntrega or EstadoPedidoDigital.Pagado))
        {
            throw new BusinessRuleException("Programa la entrega desde Pagado, Empaquetado o Pendiente de entrega.");
        }

        if (pedido.Estado == EstadoPedidoDigital.Pagado)
        {
            var notas = request.NotasEmpaque?.Trim();
            if (string.IsNullOrEmpty(notas) || notas.Length < 3)
            {
                notas = "Empaque para despacho.";
            }

            await pedidos.CambiarEstadoAsync(pedido.Id, EstadoPedidoDigital.Empaquetado, notas, cancellationToken);
            pedido = await pedidos.CargarAsync(pedido.Id, cancellationToken);
        }

        if (request.MetodoEnvio != MetodoEnvio.RECOJO_TIENDA)
        {
            var direccion = (request.Direccion ?? pedido.EntregaDireccion)?.Trim() ?? string.Empty;
            if (direccion.Length < 5)
            {
                throw new BusinessRuleException("Indica la dirección de entrega o usa recojo en tienda.");
            }
        }

        var entrega = await AsegurarAsync(pedido, cancellationToken);
        AplicarDestino(entrega, pedido, request);
        entrega.FechaProgramada ??= DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Observacion))
        {
            entrega.Observacion = request.Observacion.Trim();
        }

        await pedidos.SincronizarSnapshotEntregaAsync(pedido, entrega);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(entrega.Id, cancellationToken))!;
    }

    public async Task<EntregaResponse> DespacharAsync(
        Guid id,
        DespacharEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var entrega = await CargarAsync(id, cancellationToken);
        var pedido = entrega.Pedido;
        if (pedido.Estado is not (EstadoPedidoDigital.Empaquetado or EstadoPedidoDigital.PendienteEntrega))
        {
            throw new BusinessRuleException("Despacha desde Empaquetado o actualiza el tracking en Pendiente de entrega.");
        }

        if (request.CostoEnvio < 0)
        {
            throw new BusinessRuleException("El costo de envío no puede ser negativo.");
        }

        var tracking = TextoOpcional(request.NumeroTracking, 80);
        if (request.MetodoEnvio != MetodoEnvio.RECOJO_TIENDA && tracking is null)
        {
            throw new BusinessRuleException("Indica el N° de tracking o la clave de recojo de la agencia.");
        }

        if (pedido.Estado == EstadoPedidoDigital.Empaquetado)
        {
            var observacion = request.MetodoEnvio == MetodoEnvio.RECOJO_TIENDA
                ? "Listo para recojo en tienda."
                : $"Despachado · {request.MetodoEnvio}{(tracking is null ? string.Empty : $" · {tracking}")}";
            await pedidos.CambiarEstadoAsync(pedido.Id, EstadoPedidoDigital.PendienteEntrega, observacion, cancellationToken);
            pedido = await pedidos.CargarAsync(pedido.Id, cancellationToken);
            entrega = pedido.Entrega ?? entrega;
        }

        var ahora = DateTimeOffset.UtcNow;
        entrega.MetodoEnvio = request.MetodoEnvio;
        entrega.NumeroTracking = tracking
            ?? (request.MetodoEnvio == MetodoEnvio.RECOJO_TIENDA
                ? $"RECOJO-{pedido.Id.ToString("N")[..8]}".ToUpperInvariant()
                : null);

        entrega.Agencia = TextoOpcional(request.Agencia, 80);
        entrega.CostoEnvio = IgvCalculo.Round2(request.CostoEnvio);
        entrega.Estado = request.MetodoEnvio == MetodoEnvio.RECOJO_TIENDA
            ? EstadoLogistica.PROGRAMADA
            : EstadoLogistica.DESPACHADA;
        entrega.FechaDespacho = ahora;
        if (!string.IsNullOrWhiteSpace(request.Observacion))
        {
            entrega.Observacion = request.Observacion.Trim();
        }

        await pedidos.SincronizarSnapshotEntregaAsync(pedido, entrega);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(entrega.Id, cancellationToken))!;
    }

    public async Task<(EntregaResponse Entrega, ConversionVentaResponse Conversion)> ConfirmarAsync(
        Guid id,
        ConvertirVentaRequest? request,
        CancellationToken cancellationToken)
    {
        var entrega = await CargarAsync(id, cancellationToken);
        var conversion = await pedidos.ConvertirVentaAsync(
            entrega.PedidoDigitalId,
            request ?? new ConvertirVentaRequest(),
            cancellationToken);

        entrega = await CargarAsync(id, cancellationToken);
        if (entrega.Estado != EstadoLogistica.ENTREGADA)
        {
            entrega.Estado = EstadoLogistica.ENTREGADA;
            entrega.FechaEntrega ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ((await ObtenerAsync(id, cancellationToken))!, conversion);
    }

    public async Task<EntregaResponse> FallarAsync(
        Guid id,
        string? observacion,
        CancellationToken cancellationToken)
    {
        var entrega = await CargarAsync(id, cancellationToken);
        if (entrega.Estado == EstadoLogistica.ENTREGADA)
        {
            throw new BusinessRuleException("No se puede marcar como fallida una entrega ya confirmada.");
        }

        entrega.Estado = EstadoLogistica.FALLIDA;
        var nota = TextoOpcional(observacion, 500);
        if (nota is not null)
        {
            entrega.Observacion = nota;
        }

        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    private async Task<Entrega> AsegurarAsync(PedidoDigital pedido, CancellationToken cancellationToken)
    {
        if (pedido.Entrega is not null)
        {
            return pedido.Entrega;
        }

        var existente = await db.Entregas
            .FirstOrDefaultAsync(e => e.PedidoDigitalId == pedido.Id, cancellationToken);
        if (existente is not null)
        {
            return existente;
        }

        var ahora = DateTimeOffset.UtcNow;
        var creada = new Entrega
        {
            Id = Guid.NewGuid(),
            EmpresaId = pedido.EmpresaId,
            PedidoDigitalId = pedido.Id,
            SedeOrigenId = pedido.SedeId,
            MetodoEnvio = PedidoKanban.MetodoDesdeSnapshot(pedido.EsRecojoTienda, pedido.Courier),
            Estado = EstadoLogistica.PROGRAMADA,
            DestinatarioNombre = string.IsNullOrWhiteSpace(pedido.DestinatarioNombre)
                ? pedido.ClienteNombre ?? "Cliente"
                : pedido.DestinatarioNombre,
            DestinatarioTelefono = pedido.DestinatarioTelefono ?? pedido.ClienteTelefono,
            Direccion = pedido.EntregaDireccion,
            Distrito = pedido.EntregaDistrito,
            Provincia = pedido.EntregaProvincia,
            Departamento = pedido.EntregaDepartamento,
            Agencia = pedido.Agencia,
            PuntoEntrega = pedido.PuntoEntrega,
            CanalContacto = pedido.CanalContacto,
            ContactoReferencia = pedido.ContactoReferencia,
            NumeroTracking = pedido.NumeroTracking,
            CostoEnvio = pedido.CostoEnvio,
            NotasEmpaque = pedido.NotasEmpaque,
            FechaProgramada = ahora,
            Observacion = pedido.Observacion,
            FechaCreacion = ahora
        };
        db.Entregas.Add(creada);
        pedido.Entrega = creada;
        return creada;
    }

    private static void AplicarDestino(Entrega entrega, PedidoDigital pedido, ProgramarEntregaRequest request)
    {
        entrega.MetodoEnvio = request.MetodoEnvio;
        entrega.DestinatarioNombre = string.IsNullOrWhiteSpace(request.DestinatarioNombre)
            ? (string.IsNullOrWhiteSpace(pedido.DestinatarioNombre) ? pedido.ClienteNombre ?? "Cliente" : pedido.DestinatarioNombre)
            : request.DestinatarioNombre.Trim();
        entrega.DestinatarioTelefono = TelefonoCliente.Normalizar(request.DestinatarioTelefono)
            ?? pedido.DestinatarioTelefono
            ?? pedido.ClienteTelefono;
        entrega.Direccion = TextoOpcional(request.Direccion, 300) ?? pedido.EntregaDireccion;
        entrega.Distrito = TextoOpcional(request.Distrito, 80) ?? pedido.EntregaDistrito;
        entrega.Provincia = TextoOpcional(request.Provincia, 80) ?? pedido.EntregaProvincia;
        entrega.Departamento = TextoOpcional(request.Departamento, 80) ?? pedido.EntregaDepartamento;
        entrega.Agencia = TextoOpcional(request.Agencia, 80) ?? pedido.Agencia;
        entrega.PuntoEntrega = TextoOpcional(request.PuntoEntrega, 80) ?? pedido.PuntoEntrega;
        entrega.CanalContacto = request.CanalContacto ?? pedido.CanalContacto;
        entrega.ContactoReferencia = TextoOpcional(request.ContactoReferencia, 160) ?? pedido.ContactoReferencia;
        entrega.NumeroTracking = TextoOpcional(request.NumeroTracking, 80) ?? pedido.NumeroTracking;
        entrega.CostoEnvio = IgvCalculo.Round2(request.CostoEnvio);
        if (!string.IsNullOrWhiteSpace(request.NotasEmpaque))
        {
            entrega.NotasEmpaque = request.NotasEmpaque.Trim();
        }
    }

    private IQueryable<Entrega> QueryBase() =>
        db.Entregas
            .AsNoTracking()
            .Include(e => e.SedeOrigen)
            .Include(e => e.Pedido);

    private async Task<Entrega> CargarAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Entregas
            .Include(e => e.SedeOrigen)
            .Include(e => e.Pedido).ThenInclude(p => p.Detalles).ThenInclude(d => d.Producto)
            .Include(e => e.Pedido).ThenInclude(p => p.Historial)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró la entrega.", StatusCodes.Status404NotFound);

    private static string? TextoOpcional(string? valor, int max)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, max)];
    }

    private static EntregaResponse Map(Entrega entrega) => new()
    {
        Id = entrega.Id,
        PedidoDigitalId = entrega.PedidoDigitalId,
        PedidoCodigo = CodigoAmigable.Pedido(entrega.PedidoDigitalId),
        ClienteNombre = entrega.Pedido.ClienteNombre ?? entrega.DestinatarioNombre,
        EstadoPedido = entrega.Pedido.Estado,
        VentaId = entrega.Pedido.VentaId,
        SedeOrigenId = entrega.SedeOrigenId,
        SedeOrigenNombre = entrega.SedeOrigen.Nombre,
        MetodoEnvio = entrega.MetodoEnvio,
        Estado = entrega.Estado,
        DestinatarioNombre = entrega.DestinatarioNombre,
        DestinatarioTelefono = entrega.DestinatarioTelefono,
        Direccion = entrega.Direccion,
        Distrito = entrega.Distrito,
        Provincia = entrega.Provincia,
        Departamento = entrega.Departamento,
        Agencia = entrega.Agencia,
        PuntoEntrega = entrega.PuntoEntrega,
        CanalContacto = entrega.CanalContacto,
        ContactoReferencia = entrega.ContactoReferencia,
        NumeroTracking = entrega.NumeroTracking,
        CostoEnvio = entrega.CostoEnvio,
        NotasEmpaque = entrega.NotasEmpaque,
        FechaProgramada = entrega.FechaProgramada,
        FechaDespacho = entrega.FechaDespacho,
        FechaEntrega = entrega.FechaEntrega,
        Observacion = entrega.Observacion
    };
}
