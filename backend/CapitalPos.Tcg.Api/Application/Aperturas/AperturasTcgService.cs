using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Contracts.Aperturas;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Aperturas;

public sealed class AperturasTcgService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser,
    KardexWriter kardex)
{
    /// <summary>Zona horaria operativa del negocio (Perú).</summary>
    private static readonly TimeSpan ZonaHorariaOperacion = TimeSpan.FromHours(-5);

    public async Task<IReadOnlyList<AperturaTcgResponse>> ListarAsync(
        Guid? sedeId,
        EstadoAperturaTcg? estado,
        DateOnly? fechaDesde,
        DateOnly? fechaHasta,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();

        if (sedeId.HasValue)
        {
            query = query.Where(a => a.SedeId == sedeId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(a => a.Estado == estado.Value);
        }

        // Día operativo en hora de Perú (UTC-5), alineado con el filtro "Desde"/"Hasta" del frontend.
        if (fechaDesde.HasValue)
        {
            var inicio = new DateTimeOffset(fechaDesde.Value, TimeOnly.MinValue, ZonaHorariaOperacion);
            query = query.Where(a => a.FechaCreacion >= inicio);
        }

        if (fechaHasta.HasValue)
        {
            var finExclusivo = new DateTimeOffset(
                fechaHasta.Value.AddDays(1),
                TimeOnly.MinValue,
                ZonaHorariaOperacion);
            query = query.Where(a => a.FechaCreacion < finExclusivo);
        }

        var aperturas = await query
            .OrderByDescending(a => a.FechaCreacion)
            .ToListAsync(cancellationToken);

        return aperturas.Select(Map).ToList();
    }

    public async Task<AperturaTcgResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var apertura = await QueryBase().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return apertura is null ? null : Map(apertura);
    }

    public async Task<AperturaTcgResponse> CrearAsync(
        CrearAperturaTcgRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = currentUser.UserId
            ?? throw new BusinessRuleException("No se pudo resolver el usuario del token.", StatusCodes.Status401Unauthorized);

        await ValidarCabeceraAsync(request, cancellationToken);

        var apertura = new AperturaTcg
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SedeId = request.SedeId,
            ProductoSelladoId = request.ProductoSelladoId,
            CantidadSellados = request.CantidadSellados,
            Estado = EstadoAperturaTcg.BORRADOR,
            UsuarioId = usuarioId,
            Observacion = TextoOpcional(request.Observacion),
            FechaCreacion = DateTimeOffset.UtcNow
        };

        db.AperturasTcg.Add(apertura);
        await db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(apertura.Id, cancellationToken))!;
    }

    public async Task<AperturaTcgResponse> ActualizarBorradorAsync(
        Guid id,
        CrearAperturaTcgRequest request,
        CancellationToken cancellationToken)
    {
        var apertura = await CargarParaEdicionAsync(id, cancellationToken);
        AsegurarBorrador(apertura);
        await ValidarCabeceraAsync(request, cancellationToken);

        apertura.SedeId = request.SedeId;
        apertura.ProductoSelladoId = request.ProductoSelladoId;
        apertura.CantidadSellados = request.CantidadSellados;
        apertura.Observacion = TextoOpcional(request.Observacion);
        await db.SaveChangesAsync(cancellationToken);

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<AperturaTcgResponse> ReemplazarDetallesAsync(
        Guid id,
        IReadOnlyList<AperturaDetalleInput> inputs,
        CancellationToken cancellationToken)
    {
        var apertura = await CargarParaEdicionAsync(id, cancellationToken);
        AsegurarBorrador(apertura);
        await AplicarDetallesAsync(apertura, inputs, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<AperturaTcgResponse> ConfirmarAsync(Guid id, CancellationToken cancellationToken)
    {
        var apertura = await CargarParaEdicionAsync(id, cancellationToken);
        AsegurarBorrador(apertura);

        await ValidarCabeceraAsync(
            new CrearAperturaTcgRequest
            {
                SedeId = apertura.SedeId,
                ProductoSelladoId = apertura.ProductoSelladoId,
                CantidadSellados = apertura.CantidadSellados,
                Observacion = apertura.Observacion
            },
            cancellationToken);

        if (apertura.Detalles.Count == 0)
        {
            throw new BusinessRuleException("Registra al menos una carta obtenida antes de confirmar.");
        }

        await ProrratearDetallesAsync(apertura, cancellationToken);

        var selladoNombre = apertura.ProductoSellado.Nombre;
        var comandos = new List<KardexComando>
        {
            new(
                apertura.SedeId,
                apertura.ProductoSelladoId,
                TipoMovimientoInventario.APERTURA_SALIDA_SELLADO,
                apertura.CantidadSellados,
                $"Apertura {selladoNombre} ×{apertura.CantidadSellados}",
                "APERTURA_TCG",
                apertura.Id)
        };

        comandos.AddRange(apertura.Detalles.Select(detalle => new KardexComando(
            apertura.SedeId,
            detalle.ProductoCartaId,
            TipoMovimientoInventario.APERTURA_INGRESO_CARTA,
            detalle.Cantidad,
            $"Ingreso por apertura · {detalle.ProductoCarta.Nombre}",
            "APERTURA_TCG",
            apertura.Id)));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await kardex.AplicarMuchosAsync(comandos, cancellationToken);
        apertura.Estado = EstadoAperturaTcg.CONFIRMADA;
        apertura.FechaConfirmacion = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    public async Task<AperturaTcgResponse> AnularAsync(Guid id, CancellationToken cancellationToken)
    {
        var apertura = await CargarParaEdicionAsync(id, cancellationToken);
        if (apertura.Estado == EstadoAperturaTcg.ANULADA)
        {
            throw new BusinessRuleException("La apertura ya está anulada.");
        }

        if (apertura.Estado == EstadoAperturaTcg.BORRADOR)
        {
            apertura.Estado = EstadoAperturaTcg.ANULADA;
            await db.SaveChangesAsync(cancellationToken);
            return (await ObtenerAsync(id, cancellationToken))!;
        }

        foreach (var detalle in apertura.Detalles)
        {
            var libre = await kardex.LibreAsync(apertura.SedeId, detalle.ProductoCartaId, cancellationToken);
            if (libre < detalle.Cantidad)
            {
                throw new BusinessRuleException(
                    "No se puede anular: hay cartas ingresadas que ya fueron vendidas o reservadas.");
            }
        }

        var comandos = apertura.Detalles.Select(detalle => new KardexComando(
            apertura.SedeId,
            detalle.ProductoCartaId,
            TipoMovimientoInventario.APERTURA_INGRESO_CARTA,
            detalle.Cantidad,
            $"Anulación de apertura · {detalle.ProductoCarta.Nombre}",
            "APERTURA_TCG",
            apertura.Id,
            Invertir: true)).ToList();

        comandos.Add(new KardexComando(
            apertura.SedeId,
            apertura.ProductoSelladoId,
            TipoMovimientoInventario.APERTURA_SALIDA_SELLADO,
            apertura.CantidadSellados,
            $"Anulación de apertura · {apertura.ProductoSellado.Nombre}",
            "APERTURA_TCG",
            apertura.Id,
            Invertir: true));

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await kardex.AplicarMuchosAsync(comandos, cancellationToken);
        apertura.Estado = EstadoAperturaTcg.ANULADA;
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    private IQueryable<AperturaTcg> QueryBase() =>
        db.AperturasTcg
            .AsNoTracking()
            .Include(a => a.Sede)
            .Include(a => a.Usuario)
            .Include(a => a.ProductoSellado)
            .Include(a => a.Detalles)
            .ThenInclude(d => d.ProductoCarta);

    private async Task<AperturaTcg> CargarParaEdicionAsync(Guid id, CancellationToken cancellationToken) =>
        await db.AperturasTcg
            .Include(a => a.Sede)
            .Include(a => a.ProductoSellado)
            .Include(a => a.Detalles)
            .ThenInclude(d => d.ProductoCarta)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
        ?? throw new BusinessRuleException("No se encontró la apertura.", StatusCodes.Status404NotFound);

    private async Task ValidarCabeceraAsync(CrearAperturaTcgRequest request, CancellationToken cancellationToken)
    {
        if (request.CantidadSellados < 1)
        {
            throw new BusinessRuleException("La cantidad de sellados debe ser un entero mayor o igual a 1.");
        }

        var sede = await db.Sedes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SedeId, cancellationToken)
            ?? throw new BusinessRuleException("Selecciona una sede válida.");

        var sellado = await db.Set<ProductoSellado>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductoSelladoId, cancellationToken);
        if (sellado is null || sellado.TipoProducto != TipoProducto.SELLADO)
        {
            throw new BusinessRuleException("Selecciona un producto sellado del catálogo.");
        }

        if (!sellado.Activo)
        {
            throw new BusinessRuleException("El producto sellado está inactivo.");
        }

        if (!sellado.PermiteApertura)
        {
            throw new BusinessRuleException("Este sellado no permite apertura; solo se vende cerrado.");
        }

        var libre = await kardex.LibreAsync(request.SedeId, request.ProductoSelladoId, cancellationToken);
        if (libre < request.CantidadSellados)
        {
            throw new BusinessRuleException(
                $"No hay stock libre suficiente en {sede.Nombre}. Disponible: {libre}.");
        }
    }

    private async Task AplicarDetallesAsync(
        AperturaTcg apertura,
        IReadOnlyList<AperturaDetalleInput> inputs,
        CancellationToken cancellationToken)
    {
        if (apertura.Detalles.Count > 0)
        {
            db.AperturaTcgDetalles.RemoveRange(apertura.Detalles.ToList());
            apertura.Detalles.Clear();
            await db.SaveChangesAsync(cancellationToken);
        }

        if (inputs.Count == 0)
        {
            return;
        }

        var construidos = await ConstruirDetallesAsync(apertura, inputs, cancellationToken);
        foreach (var detalle in construidos)
        {
            db.AperturaTcgDetalles.Add(detalle);
        }
    }

    private async Task ProrratearDetallesAsync(AperturaTcg apertura, CancellationToken cancellationToken)
    {
        var lineas = apertura.Detalles
            .Select(d => ((Producto?)d.ProductoCarta, d.Cantidad))
            .ToList();
        var costos = YieldApertura.Prorratear(lineas, YieldApertura.CostoSellado(apertura.ProductoSellado, apertura.CantidadSellados));
        for (var i = 0; i < apertura.Detalles.Count; i++)
        {
            apertura.Detalles.ElementAt(i).CostoUnitarioAsignado = costos[i];
        }

        await Task.CompletedTask;
    }

    private async Task<List<AperturaTcgDetalle>> ConstruirDetallesAsync(
        AperturaTcg apertura,
        IReadOnlyList<AperturaDetalleInput> inputs,
        CancellationToken cancellationToken)
    {
        var ids = inputs.Select(i => i.ProductoCartaId).Distinct().ToList();
        var cartas = await db.Set<ProductoCarta>()
            .AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var normalizados = new List<(AperturaDetalleInput Input, ProductoCarta Carta)>();
        foreach (var input in inputs)
        {
            if (input.Cantidad < 1)
            {
                throw new BusinessRuleException("La cantidad de cartas debe ser un entero mayor o igual a 1.");
            }

            if (!cartas.TryGetValue(input.ProductoCartaId, out var carta) || carta.TipoProducto != TipoProducto.CARTA)
            {
                throw new BusinessRuleException("Cada línea debe ser una carta existente en el catálogo.");
            }

            if (!carta.Activo)
            {
                throw new BusinessRuleException($"La carta {carta.Nombre} está inactiva.");
            }

            normalizados.Add((input, carta));
        }

        var lineas = normalizados.Select(n => ((Producto?)n.Carta, n.Input.Cantidad)).ToList();
        var costos = YieldApertura.Prorratear(
            lineas,
            YieldApertura.CostoSellado(apertura.ProductoSellado, apertura.CantidadSellados));

        return normalizados.Select((item, index) => new AperturaTcgDetalle
        {
            Id = Guid.NewGuid(),
            AperturaTcgId = apertura.Id,
            EmpresaId = tenant.EmpresaId,
            ProductoCartaId = item.Carta.Id,
            Cantidad = item.Input.Cantidad,
            CostoUnitarioAsignado = costos[index],
            Estado = item.Input.Estado,
            EsFoil = item.Input.EsFoil
        }).ToList();
    }

    private static void AsegurarBorrador(AperturaTcg apertura)
    {
        if (apertura.Estado != EstadoAperturaTcg.BORRADOR)
        {
            throw new BusinessRuleException("Solo se puede editar una apertura en borrador.");
        }
    }

    private static string? TextoOpcional(string? valor)
    {
        var texto = valor?.Trim() ?? string.Empty;
        return texto.Length == 0 ? null : texto[..Math.Min(texto.Length, 500)];
    }

    private static AperturaTcgResponse Map(AperturaTcg apertura)
    {
        var lineas = apertura.Detalles
            .Select(d => ((Producto?)d.ProductoCarta, d.Cantidad))
            .ToList();
        var rendimiento = YieldApertura.Calcular(
            YieldApertura.CostoSellado(apertura.ProductoSellado, apertura.CantidadSellados),
            YieldApertura.ValorEstimadoCartas(lineas));

        return new AperturaTcgResponse
        {
            Id = apertura.Id,
            SedeId = apertura.SedeId,
            SedeNombre = apertura.Sede.Nombre,
            ProductoSelladoId = apertura.ProductoSelladoId,
            ProductoSelladoNombre = apertura.ProductoSellado.Nombre,
            ProductoSelladoSku = apertura.ProductoSellado.CodigoSku,
            CantidadSellados = apertura.CantidadSellados,
            Estado = apertura.Estado,
            UsuarioId = apertura.UsuarioId,
            UsuarioNombre = apertura.Usuario.Nombre,
            Observacion = apertura.Observacion,
            FechaCreacion = apertura.FechaCreacion,
            FechaConfirmacion = apertura.FechaConfirmacion,
            Detalles = apertura.Detalles
                .Select(d => new AperturaTcgDetalleResponse
                {
                    Id = d.Id,
                    ProductoCartaId = d.ProductoCartaId,
                    ProductoCartaNombre = d.ProductoCarta.Nombre,
                    CodigoSku = d.ProductoCarta.CodigoSku,
                    Cantidad = d.Cantidad,
                    CostoUnitarioAsignado = d.CostoUnitarioAsignado,
                    Estado = d.Estado,
                    EsFoil = d.EsFoil
                })
                .ToList(),
            Rendimiento = rendimiento
        };
    }
}
