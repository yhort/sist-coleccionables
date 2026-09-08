using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Caja;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Caja;

public sealed class CajaService(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICurrentUser currentUser)
{
    private static readonly string[] GruposMedioPago =
        ["EFECTIVO", "YAPE_PLIN", "IZIPAY", "TARJETA", "TRANSFERENCIA", "OTRO"];

    public async Task<IReadOnlyList<CajaSesionResponse>> ListarAsync(
        Guid? sedeId,
        EstadoCajaSesion? estado,
        DateTimeOffset? desde,
        DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var query = QueryBase();
        if (sedeId.HasValue)
        {
            query = query.Where(s => s.SedeId == sedeId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(s => s.Estado == estado.Value);
        }

        if (desde.HasValue)
        {
            query = query.Where(s => s.FechaApertura >= desde.Value);
        }

        if (hasta.HasValue)
        {
            query = query.Where(s => s.FechaApertura <= hasta.Value);
        }

        var sesiones = await query
            .OrderByDescending(s => s.FechaApertura)
            .Take(80)
            .ToListAsync(cancellationToken);

        return sesiones.Select(MapSesion).ToList();
    }

    public async Task<CajaSesionResponse?> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var sesion = await QueryBase().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return sesion is null ? null : MapSesion(sesion);
    }

    public async Task<CajaEstadoActualResponse> EstadoActualAsync(
        Guid sedeId,
        CancellationToken cancellationToken)
    {
        var sede = await RequerirSedeAsync(sedeId, cancellationToken);
        var sesion = await QueryBase()
            .FirstOrDefaultAsync(
                s => s.SedeId == sedeId && s.Estado == EstadoCajaSesion.ABIERTA,
                cancellationToken);

        if (sesion is null)
        {
            return new CajaEstadoActualResponse
            {
                Abierta = false,
                SedeId = sede.Id,
                SedeNombre = sede.Nombre,
                Mensaje = "La caja de esta sede está cerrada. Abre el turno para registrar ventas."
            };
        }

        return new CajaEstadoActualResponse
        {
            Abierta = true,
            SedeId = sede.Id,
            SedeNombre = sede.Nombre,
            Sesion = MapSesion(sesion),
            Resumen = MapResumen(sesion)
        };
    }

    public async Task<CajaResumenResponse> ResumenActualAsync(
        Guid sedeId,
        CancellationToken cancellationToken)
    {
        var estado = await EstadoActualAsync(sedeId, cancellationToken);
        if (estado.Resumen is null)
        {
            throw new BusinessRuleException(
                estado.Mensaje ?? "No hay un turno de caja abierto en esta sede.");
        }

        return estado.Resumen;
    }

    public async Task<CajaSesionResponse> AbrirAsync(
        AbrirCajaRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = currentUser.UserId
            ?? throw new BusinessRuleException("No se pudo resolver el usuario del token.", StatusCodes.Status401Unauthorized);

        var sede = await RequerirSedeAsync(request.SedeId, cancellationToken);
        var abierta = await db.CajaSesiones
            .FirstOrDefaultAsync(
                s => s.SedeId == request.SedeId && s.Estado == EstadoCajaSesion.ABIERTA,
                cancellationToken);
        if (abierta is not null)
        {
            throw new BusinessRuleException(
                $"Ya hay un turno abierto en {sede.Nombre} desde {ZonaHorariaPeru.ToLocal(abierta.FechaApertura):dd/MM/yyyy HH:mm}.");
        }

        var ahora = DateTimeOffset.UtcNow;
        var monto = IgvCalculo.Round2(request.MontoApertura);
        var sesion = new CajaSesion
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            SedeId = sede.Id,
            UsuarioId = usuarioId,
            MontoApertura = monto,
            FechaApertura = ahora,
            Estado = EstadoCajaSesion.ABIERTA,
            MontoEfectivoTeorico = monto,
            ObservacionApertura = TextoOpcional(request.Observacion),
            FechaCreacion = ahora
        };

        db.CajaSesiones.Add(sesion);
        await db.SaveChangesAsync(cancellationToken);
        return (await ObtenerAsync(sesion.Id, cancellationToken))!;
    }

    public async Task<CajaResumenResponse> CerrarAsync(
        CerrarCajaRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = currentUser.UserId
            ?? throw new BusinessRuleException("No se pudo resolver el usuario del token.", StatusCodes.Status401Unauthorized);

        var sesion = await ResolverSesionAsync(request.CajaSesionId, request.SedeId, exigirAbierta: true, cancellationToken);
        var teorico = CalcularTeorico(sesion);
        var real = IgvCalculo.Round2(request.MontoEfectivoReal);
        var diferencia = IgvCalculo.Round2(real - teorico);
        var ahora = DateTimeOffset.UtcNow;

        sesion.MontoEfectivoTeorico = teorico;
        sesion.MontoEfectivoReal = real;
        sesion.Diferencia = diferencia;
        sesion.FechaCierre = ahora;
        sesion.Estado = EstadoCajaSesion.CERRADA;
        sesion.UsuarioCierreId = usuarioId;
        sesion.ObservacionCierre = TextoOpcional(request.Observacion);

        await db.SaveChangesAsync(cancellationToken);
        sesion = await QueryBase().FirstAsync(s => s.Id == sesion.Id, cancellationToken);
        return MapResumen(sesion);
    }

    public async Task<CajaMovimientoResponse> RegistrarMovimientoAsync(
        RegistrarCajaMovimientoRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = currentUser.UserId;
        var sesion = await ResolverSesionAsync(request.CajaSesionId, request.SedeId, exigirAbierta: true, cancellationToken);
        var concepto = request.Concepto.Trim();
        if (concepto.Length < 3)
        {
            throw new BusinessRuleException("El concepto debe tener al menos 3 caracteres.");
        }

        var movimiento = new CajaMovimiento
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            CajaSesionId = sesion.Id,
            Tipo = request.Tipo,
            Monto = IgvCalculo.Round2(request.Monto),
            Concepto = concepto[..Math.Min(concepto.Length, 200)],
            UsuarioId = usuarioId,
            Fecha = DateTimeOffset.UtcNow
        };

        db.CajaMovimientos.Add(movimiento);
        sesion.MontoEfectivoTeorico = CalcularTeorico(sesion, movimiento);
        await db.SaveChangesAsync(cancellationToken);

        return new CajaMovimientoResponse
        {
            Id = movimiento.Id,
            CajaSesionId = sesion.Id,
            Tipo = movimiento.Tipo,
            Monto = movimiento.Monto,
            Concepto = movimiento.Concepto,
            UsuarioId = usuarioId,
            UsuarioNombre = currentUser.Nombre,
            Fecha = movimiento.Fecha
        };
    }

    public async Task<CajaTicketReporteResponse> ReporteTicketAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sesion = await QueryBase().FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la sesión de caja.", StatusCodes.Status404NotFound);

        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == tenant.EmpresaId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la empresa.");

        var resumen = MapResumen(sesion);
        var diferencia = resumen.Efectivo.Diferencia ?? 0;
        var leyenda = resumen.Efectivo.TipoDiferencia switch
        {
            TipoDiferenciaCaja.SOBRANTE => $"Sobrante S/ {diferencia:0.00}",
            TipoDiferenciaCaja.FALTANTE => $"Faltante S/ {Math.Abs(diferencia):0.00}",
            TipoDiferenciaCaja.CUADRADO => "Caja cuadrada",
            _ => "Conteo pendiente"
        };

        return new CajaTicketReporteResponse
        {
            CajaSesionId = sesion.Id,
            Titulo = "ARQUEO DE CAJA",
            AnchoMm = "80",
            Empresa = new CajaTicketEmpresa
            {
                Ruc = empresa.Ruc,
                RazonSocial = empresa.RazonSocial,
                NombreComercial = empresa.NombreComercial
            },
            SedeNombre = sesion.Sede.Nombre,
            UsuarioAperturaNombre = sesion.Usuario.Nombre,
            UsuarioCierreNombre = sesion.UsuarioCierre?.Nombre,
            FechaAperturaLocal = ZonaHorariaPeru.ToLocal(sesion.FechaApertura).ToString("dd/MM/yyyy HH:mm"),
            FechaCierreLocal = sesion.FechaCierre is { } cierre
                ? ZonaHorariaPeru.ToLocal(cierre).ToString("dd/MM/yyyy HH:mm")
                : null,
            Estado = sesion.Estado,
            Encabezado =
            [
                new CajaTicketLinea { Etiqueta = "Sede", Valor = sesion.Sede.Nombre },
                new CajaTicketLinea { Etiqueta = "Turno", Valor = sesion.Id.ToString("N")[..8].ToUpperInvariant() },
                new CajaTicketLinea { Etiqueta = "Apertura", Valor = ZonaHorariaPeru.ToLocal(sesion.FechaApertura).ToString("dd/MM/yyyy HH:mm") },
                new CajaTicketLinea { Etiqueta = "Cajero", Valor = sesion.Usuario.Nombre }
            ],
            MediosPago = resumen.MediosPago,
            Documentos = resumen.Documentos,
            Movimientos = resumen.Movimientos,
            Efectivo = resumen.Efectivo,
            LeyendaDiferencia = leyenda,
            Pie =
            [
                $"{resumen.CantidadVentas} venta(s) · Total S/ {resumen.TotalVentas:0.00}",
                leyenda,
                "Ticket de arqueo 80mm · CapitalPOS"
            ]
        };
    }

    public async Task<Guid> ExigirSesionAbiertaAsync(Guid sedeId, CancellationToken cancellationToken)
    {
        var sesion = await db.CajaSesiones
            .FirstOrDefaultAsync(
                s => s.SedeId == sedeId && s.Estado == EstadoCajaSesion.ABIERTA,
                cancellationToken);
        if (sesion is null)
        {
            throw new BusinessRuleException(
                "La caja de esta sede está cerrada. Abre el turno antes de confirmar ventas.");
        }

        return sesion.Id;
    }

    public async Task ActualizarTeoricoAsync(Guid cajaSesionId, CancellationToken cancellationToken)
    {
        var sesion = await QueryBase().FirstOrDefaultAsync(s => s.Id == cajaSesionId, cancellationToken);
        if (sesion is null || sesion.Estado != EstadoCajaSesion.ABIERTA)
        {
            return;
        }

        sesion.MontoEfectivoTeorico = CalcularTeorico(sesion);
    }

    private async Task<CajaSesion> ResolverSesionAsync(
        Guid? cajaSesionId,
        Guid? sedeId,
        bool exigirAbierta,
        CancellationToken cancellationToken)
    {
        CajaSesion? sesion;
        if (cajaSesionId.HasValue && cajaSesionId.Value != Guid.Empty)
        {
            sesion = await QueryBase().FirstOrDefaultAsync(s => s.Id == cajaSesionId.Value, cancellationToken);
        }
        else if (sedeId.HasValue && sedeId.Value != Guid.Empty)
        {
            var query = QueryBase().Where(s => s.SedeId == sedeId.Value);
            if (exigirAbierta)
            {
                query = query.Where(s => s.Estado == EstadoCajaSesion.ABIERTA);
            }

            sesion = await query
                .OrderByDescending(s => s.FechaApertura)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            throw new BusinessRuleException("Indica la sede o el identificador de la sesión de caja.");
        }

        if (sesion is null)
        {
            throw new BusinessRuleException(
                exigirAbierta
                    ? "No hay un turno de caja abierto. Abre la caja para continuar."
                    : "No se encontró la sesión de caja.",
                exigirAbierta ? StatusCodes.Status400BadRequest : StatusCodes.Status404NotFound);
        }

        if (exigirAbierta && sesion.Estado != EstadoCajaSesion.ABIERTA)
        {
            throw new BusinessRuleException("El turno de caja ya está cerrado.");
        }

        return sesion;
    }

    private async Task<Sede> RequerirSedeAsync(Guid sedeId, CancellationToken cancellationToken)
    {
        var sede = await db.Sedes.FirstOrDefaultAsync(s => s.Id == sedeId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la sede.", StatusCodes.Status404NotFound);
        if (!sede.Activa)
        {
            throw new BusinessRuleException("La sede no está activa.");
        }

        return sede;
    }

    private IQueryable<CajaSesion> QueryBase() =>
        db.CajaSesiones
            .AsSplitQuery()
            .Include(s => s.Sede)
            .Include(s => s.Usuario)
            .Include(s => s.UsuarioCierre)
            .Include(s => s.Movimientos)
                .ThenInclude(m => m.Usuario)
            .Include(s => s.Ventas)
                .ThenInclude(v => v.Pagos)
            .Include(s => s.Ventas)
                .ThenInclude(v => v.Cliente)
            .Include(s => s.Ventas)
                .ThenInclude(v => v.Comprobantes);

    private static CajaSesionResponse MapSesion(CajaSesion sesion)
    {
        var ventas = VentasDeTurno(sesion);
        var teorico = CalcularTeorico(sesion);
        var diferencia = sesion.Diferencia ?? (sesion.MontoEfectivoReal is { } real
            ? IgvCalculo.Round2(real - teorico)
            : null);
        return new CajaSesionResponse
        {
            Id = sesion.Id,
            SedeId = sesion.SedeId,
            SedeNombre = sesion.Sede.Nombre,
            UsuarioId = sesion.UsuarioId,
            UsuarioNombre = sesion.Usuario.Nombre,
            UsuarioCierreId = sesion.UsuarioCierreId,
            UsuarioCierreNombre = sesion.UsuarioCierre?.Nombre,
            MontoApertura = sesion.MontoApertura,
            FechaApertura = sesion.FechaApertura,
            FechaCierre = sesion.FechaCierre,
            Estado = sesion.Estado,
            MontoEfectivoTeorico = teorico,
            MontoEfectivoReal = sesion.MontoEfectivoReal,
            Diferencia = diferencia,
            TipoDiferencia = TipoDe(diferencia),
            ObservacionApertura = sesion.ObservacionApertura,
            ObservacionCierre = sesion.ObservacionCierre,
            CantidadVentas = ventas.Count,
            TotalVentas = IgvCalculo.Round2(ventas.Sum(v => v.Total))
        };
    }

    private static CajaResumenResponse MapResumen(CajaSesion sesion)
    {
        var ventas = VentasDeTurno(sesion);
        var pagos = ventas.SelectMany(v => v.Pagos).ToList();
        var grupos = pagos
            .GroupBy(p => GrupoMedio(p.Origen))
            .ToDictionary(g => g.Key, g => new { Monto = IgvCalculo.Round2(g.Sum(x => x.Monto)), Cantidad = g.Count() });

        var medios = GruposMedioPago
            .Select(grupo =>
            {
                grupos.TryGetValue(grupo, out var fila);
                return new CajaMedioPagoFila
                {
                    Grupo = grupo,
                    Etiqueta = EtiquetaMedio(grupo),
                    Monto = fila?.Monto ?? 0,
                    Cantidad = fila?.Cantidad ?? 0
                };
            })
            .Where(f => f.Monto != 0 || f.Grupo is "EFECTIVO" or "YAPE_PLIN" or "IZIPAY" or "TARJETA")
            .ToList();

        var documentos = ventas
            .SelectMany(v => v.Comprobantes)
            .Where(c => c.Tipo is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.FACTURA or TipoComprobanteSunat.NOTA_VENTA)
            .GroupBy(c => c.Tipo)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ventaIds = g.Select(c => c.VentaId).Distinct().ToHashSet();
                return new CajaDocumentoFila
                {
                    Tipo = g.Key,
                    Etiqueta = EtiquetaDocumento(g.Key),
                    Cantidad = g.Count(),
                    Total = IgvCalculo.Round2(ventas.Where(v => ventaIds.Contains(v.Id)).Sum(v => v.Total))
                };
            })
            .ToList();

        var ingresos = IgvCalculo.Round2(sesion.Movimientos.Where(m => m.Tipo == TipoCajaMovimiento.INGRESO).Sum(m => m.Monto));
        var egresos = IgvCalculo.Round2(sesion.Movimientos.Where(m => m.Tipo == TipoCajaMovimiento.EGRESO).Sum(m => m.Monto));
        var ventasEfectivo = IgvCalculo.Round2(pagos.Where(p => p.Origen == OrigenPago.EFECTIVO).Sum(p => p.Monto));
        var teorico = CalcularTeorico(sesion);
        var diferencia = sesion.Diferencia ?? (sesion.MontoEfectivoReal is { } real
            ? IgvCalculo.Round2(real - teorico)
            : null);

        return new CajaResumenResponse
        {
            CajaSesionId = sesion.Id,
            SedeId = sesion.SedeId,
            SedeNombre = sesion.Sede.Nombre,
            Estado = sesion.Estado,
            FechaApertura = sesion.FechaApertura,
            FechaCierre = sesion.FechaCierre,
            UsuarioAperturaNombre = sesion.Usuario.Nombre,
            UsuarioCierreNombre = sesion.UsuarioCierre?.Nombre,
            CantidadVentas = ventas.Count,
            TotalVentas = IgvCalculo.Round2(ventas.Sum(v => v.Total)),
            Efectivo = new CajaEfectivoResumen
            {
                MontoApertura = sesion.MontoApertura,
                VentasEfectivo = ventasEfectivo,
                Ingresos = ingresos,
                Egresos = egresos,
                MontoEfectivoTeorico = teorico,
                MontoEfectivoReal = sesion.MontoEfectivoReal,
                Diferencia = diferencia,
                TipoDiferencia = TipoDe(diferencia)
            },
            MediosPago = medios,
            Documentos = documentos,
            Movimientos = sesion.Movimientos
                .OrderBy(m => m.Fecha)
                .Select(MapMovimiento)
                .ToList(),
            Ventas = ventas
                .OrderBy(v => v.Fecha)
                .Select(v => new CajaVentaFila
                {
                    VentaId = v.Id,
                    Fecha = v.Fecha,
                    Total = v.Total,
                    ClienteNombre = v.Cliente?.Nombre,
                    MediosPago = string.Join(" + ", v.Pagos.Select(p => EtiquetaOrigen(p.Origen)).Distinct()),
                    Comprobante = v.Comprobantes
                        .Where(c => c.Tipo is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.FACTURA or TipoComprobanteSunat.NOTA_VENTA)
                        .Select(c => $"{c.Serie}-{c.Correlativo:00000000}")
                        .FirstOrDefault()
                })
                .ToList()
        };
    }

    private static CajaMovimientoResponse MapMovimiento(CajaMovimiento movimiento) =>
        new()
        {
            Id = movimiento.Id,
            CajaSesionId = movimiento.CajaSesionId,
            Tipo = movimiento.Tipo,
            Monto = movimiento.Monto,
            Concepto = movimiento.Concepto,
            UsuarioId = movimiento.UsuarioId,
            UsuarioNombre = movimiento.Usuario?.Nombre,
            Fecha = movimiento.Fecha
        };

    private static List<Venta> VentasDeTurno(CajaSesion sesion) =>
        sesion.Ventas.Where(v => !v.EsConsolidacion).ToList();

    private static decimal CalcularTeorico(CajaSesion sesion, CajaMovimiento? extra = null)
    {
        var ventasEfectivo = VentasDeTurno(sesion)
            .SelectMany(v => v.Pagos)
            .Where(p => p.Origen == OrigenPago.EFECTIVO)
            .Sum(p => p.Monto);
        var ingresos = sesion.Movimientos.Where(m => m.Tipo == TipoCajaMovimiento.INGRESO).Sum(m => m.Monto);
        var egresos = sesion.Movimientos.Where(m => m.Tipo == TipoCajaMovimiento.EGRESO).Sum(m => m.Monto);
        if (extra is not null)
        {
            if (extra.Tipo == TipoCajaMovimiento.INGRESO)
            {
                ingresos += extra.Monto;
            }
            else
            {
                egresos += extra.Monto;
            }
        }

        return IgvCalculo.Round2(sesion.MontoApertura + ventasEfectivo + ingresos - egresos);
    }

    private static string GrupoMedio(OrigenPago origen) => origen switch
    {
        OrigenPago.EFECTIVO => "EFECTIVO",
        OrigenPago.YAPE or OrigenPago.PLIN => "YAPE_PLIN",
        OrigenPago.IZIPAY => "IZIPAY",
        OrigenPago.TARJETA => "TARJETA",
        OrigenPago.TRANSFERENCIA => "TRANSFERENCIA",
        _ => "OTRO"
    };

    private static string EtiquetaMedio(string grupo) => grupo switch
    {
        "EFECTIVO" => "Efectivo",
        "YAPE_PLIN" => "Yape / Plin",
        "IZIPAY" => "Izipay",
        "TARJETA" => "Tarjeta",
        "TRANSFERENCIA" => "Transferencia",
        _ => "Otro"
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

    private static string EtiquetaDocumento(TipoComprobanteSunat tipo) => tipo switch
    {
        TipoComprobanteSunat.BOLETA => "Boleta",
        TipoComprobanteSunat.FACTURA => "Factura",
        TipoComprobanteSunat.NOTA_VENTA => "Nota de venta",
        _ => tipo.ToString()
    };

    private static TipoDiferenciaCaja? TipoDe(decimal? diferencia)
    {
        if (diferencia is null)
        {
            return null;
        }

        if (diferencia > 0)
        {
            return TipoDiferenciaCaja.SOBRANTE;
        }

        if (diferencia < 0)
        {
            return TipoDiferenciaCaja.FALTANTE;
        }

        return TipoDiferenciaCaja.CUADRADO;
    }

    private static string? TextoOpcional(string? valor)
    {
        var texto = valor?.Trim();
        return string.IsNullOrEmpty(texto) ? null : texto[..Math.Min(texto.Length, 500)];
    }
}
