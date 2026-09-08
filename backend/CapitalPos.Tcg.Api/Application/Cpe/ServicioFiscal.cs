using System.Text;
using System.Text.Json;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Entities;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Cpe;

public sealed class ServicioFiscal(
    ApplicationDbContext db,
    ITenantProvider tenant,
    ICpeEmisor emisor,
    ICpePdfGenerator pdf) : IServicioFiscal
{
    public const decimal UmbralVentasMenores = 5.00m;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ComprobanteResponse> EmitirDesdeVentaAsync(
        Guid ventaId,
        TipoComprobanteSunat tipo,
        EmitirNotaCreditoRequest? notaCredito,
        CancellationToken cancellationToken)
    {
        var venta = await db.Ventas
            .Include(v => v.Detalles)
            .Include(v => v.Cliente)
            .Include(v => v.Comprobantes)
            .FirstOrDefaultAsync(v => v.Id == ventaId, cancellationToken)
            ?? throw new BusinessRuleException("No se encontró la venta.", StatusCodes.Status404NotFound);

        Comprobante? existente = null;
        if (EsDocumentoDeVenta(tipo))
        {
            existente = await db.Comprobantes.FirstOrDefaultAsync(
                c => c.VentaId == ventaId
                    && (c.Tipo == TipoComprobanteSunat.BOLETA
                        || c.Tipo == TipoComprobanteSunat.FACTURA
                        || c.Tipo == TipoComprobanteSunat.NOTA_VENTA),
                cancellationToken);
            if (existente is not null && tipo == existente.Tipo)
            {
                if (tipo == TipoComprobanteSunat.NOTA_VENTA
                    || (existente.Estado is EstadoEmisionSunat.ACEPTADO && !string.IsNullOrWhiteSpace(existente.Xml)))
                {
                    return Map(existente, venta);
                }
            }

            if (existente is not null && tipo != existente.Tipo)
            {
                throw new BusinessRuleException(
                    existente.Tipo == TipoComprobanteSunat.NOTA_VENTA
                        ? "Esta venta ya tiene una nota de venta. Regularízala con Boleta Consolidada."
                        : "La venta ya tiene un comprobante de otro tipo.");
            }
        }

        ValidarTipo(venta, tipo, notaCredito);
        var ahora = DateTimeOffset.UtcNow;
        var serie = await ResolverSerieAsync(tipo, venta, ahora, cancellationToken);
        var reintento = existente is { Correlativo: > 0, Serie.Length: 4 }
            && existente.Tipo == tipo
            && existente.Estado is not EstadoEmisionSunat.ACEPTADO
                and not EstadoEmisionSunat.PENDIENTE_CONSOLIDAR
                and not EstadoEmisionSunat.CONSOLIDADA;
        if (!reintento)
        {
            serie.Correlativo += 1;
        }

        var serieCodigo = reintento ? existente!.Serie : serie.Serie;
        var correlativo = reintento ? existente!.Correlativo : serie.Correlativo;

        var fiscal = await ResolverFiscalAsync(cancellationToken);
        var cliente = venta.Cliente
            ?? throw new BusinessRuleException("La venta no tiene cliente para emitir CPE.");

        var payload = ArmarPayload(venta, cliente, fiscal, tipo, serieCodigo, correlativo, notaCredito);
        CpeEmisionResultado resultado;
        if (tipo == TipoComprobanteSunat.NOTA_VENTA)
        {
            resultado = new CpeEmisionResultado
            {
                Estado = EstadoEmisionSunat.PENDIENTE_CONSOLIDAR,
                Xml = string.Empty,
                Mensaje = "Nota de venta interna. No enviada a SUNAT.",
                Serie = serieCodigo,
                Correlativo = correlativo
            };
        }
        else
        {
            resultado = await emisor.EmitirAsync(payload, cancellationToken);
        }

        Comprobante comprobante;
        if (EsDocumentoDeVenta(tipo))
        {
            comprobante = existente ?? new Comprobante
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                VentaId = venta.Id,
                FechaCreacion = ahora
            };

            if (db.Entry(comprobante).State == EntityState.Detached)
            {
                db.Comprobantes.Add(comprobante);
            }
        }
        else
        {
            comprobante = new Comprobante
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                VentaId = venta.Id,
                FechaCreacion = ahora
            };
            db.Comprobantes.Add(comprobante);
        }

        comprobante.Tipo = tipo;
        comprobante.TipoSunat = FiscalCodes.CodigoComprobante(tipo);
        comprobante.Serie = resultado.Serie is { Length: 4 } ? resultado.Serie : serieCodigo;
        comprobante.Correlativo = resultado.Correlativo is > 0 ? resultado.Correlativo.Value : correlativo;
        if (!reintento && resultado.Correlativo is > 0)
        {
            serie.Correlativo = Math.Max(serie.Correlativo, resultado.Correlativo.Value);
        }

        comprobante.Estado = resultado.Estado;
        comprobante.PayloadJson = JsonSerializer.Serialize(payload, Json);
        comprobante.Xml = resultado.Xml;
        comprobante.Cdr = resultado.Cdr;
        comprobante.HashFirma = Truncar(resultado.HashFirma, 128);
        comprobante.Mensaje = Truncar(resultado.Mensaje, 500);
        comprobante.CodigoMotivo = notaCredito?.CodigoMotivo;
        comprobante.DescripcionMotivo = notaCredito?.DescripcionMotivo;
        comprobante.DocumentoReferencia = payload.DocumentoReferencia?.SerieCorrelativo;
        comprobante.FechaEmision = ahora;
        comprobante.FechaEnvioSunat = tipo == TipoComprobanteSunat.NOTA_VENTA ? null : ahora;

        await db.SaveChangesAsync(cancellationToken);
        return Map(comprobante, venta);
    }

    public async Task<ComprobanteResponse?> ObtenerPorVentaAsync(Guid ventaId, CancellationToken cancellationToken)
    {
        var comprobante = await db.Comprobantes
            .AsNoTracking()
            .Include(c => c.Venta).ThenInclude(v => v.Cliente)
            .Where(c => c.VentaId == ventaId)
            .OrderByDescending(c => c.FechaCreacion)
            .FirstOrDefaultAsync(cancellationToken);
        return comprobante is null ? null : Map(comprobante);
    }

    public async Task<IReadOnlyList<ComprobanteResponse>> ListarAsync(CancellationToken cancellationToken)
    {
        var comprobantes = await db.Comprobantes
            .AsNoTracking()
            .Include(c => c.Venta).ThenInclude(v => v.Cliente)
            .OrderByDescending(c => c.FechaEmision)
            .Take(50)
            .ToListAsync(cancellationToken);
        return comprobantes.Select(c => Map(c, incluirArchivos: false)).ToList();
    }

    public async Task<CpeArchivoDescarga?> ObtenerArchivoAsync(
        Guid comprobanteId,
        string tipo,
        CancellationToken cancellationToken)
    {
        var comprobante = await db.Comprobantes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, cancellationToken);
        if (comprobante is null)
        {
            return null;
        }

        var esCdr = tipo.Equals("cdr", StringComparison.OrdinalIgnoreCase);
        var bruto = esCdr ? comprobante.Cdr : comprobante.Xml;
        var archivo = MaterializarArchivo(bruto, comprobante, esCdr);
        return archivo;
    }

    public async Task<CpeArchivoDescarga?> ObtenerPdfAsync(
        Guid comprobanteId,
        CpePdfFormato formato,
        CancellationToken cancellationToken)
    {
        var comprobante = await db.Comprobantes
            .AsNoTracking()
            .Include(c => c.Venta).ThenInclude(v => v.Detalles)
            .Include(c => c.Venta).ThenInclude(v => v.Cliente)
            .FirstOrDefaultAsync(c => c.Id == comprobanteId, cancellationToken);
        if (comprobante is null)
        {
            return null;
        }

        var payload = ResolverPayload(comprobante);
        if (payload is null)
        {
            return null;
        }

        var bytes = pdf.Generar(payload, comprobante.HashFirma, formato);
        var sufijo = formato == CpePdfFormato.Ticket ? "-ticket80" : "";
        return new CpeArchivoDescarga
        {
            Contenido = bytes,
            ContentType = "application/pdf",
            NombreArchivo = $"{comprobante.Serie}-{comprobante.Correlativo:00000000}{sufijo}.pdf"
        };
    }

    public async Task<IReadOnlyList<NotaVentaPendienteResponse>> ListarNotasPendientesAsync(
        DateOnly? fecha,
        CancellationToken cancellationToken)
    {
        var dia = fecha ?? ZonaHorariaPeru.FechaLocal(DateTimeOffset.UtcNow);
        var notas = await ConsultarNotasDelDiaAsync(dia, cancellationToken);
        return notas
            .Where(c => c.Estado == EstadoEmisionSunat.PENDIENTE_CONSOLIDAR && c.BoletaConsolidadaId is null)
            .Select(MapNotaPendiente)
            .ToList();
    }

    public async Task<BoletaConsolidadaResponse> GenerarBoletaConsolidadaAsync(
        GenerarBoletaConsolidadaRequest request,
        CancellationToken cancellationToken)
    {
        var dia = request.Fecha ?? ZonaHorariaPeru.FechaLocal(DateTimeOffset.UtcNow);
        var pendientes = (await ConsultarNotasDelDiaAsync(dia, cancellationToken))
            .Where(c => c.Estado == EstadoEmisionSunat.PENDIENTE_CONSOLIDAR && c.BoletaConsolidadaId is null)
            .ToList();

        IReadOnlyList<Comprobante> seleccionadas = request.Filtro switch
        {
            FiltroBoletaConsolidada.VENTAS_MENORES =>
                pendientes.Where(c => (c.Venta?.Total ?? 0) < UmbralVentasMenores).ToList(),
            FiltroBoletaConsolidada.SELECCION_GENERAL => ResolverSeleccionGeneral(pendientes, request.NotaVentaIds),
            _ => throw new BusinessRuleException("Filtro de consolidación no soportado.")
        };

        if (seleccionadas.Count == 0)
        {
            throw new BusinessRuleException(
                request.Filtro == FiltroBoletaConsolidada.VENTAS_MENORES
                    ? "No hay notas de venta del día menores a S/ 5.00 pendientes de consolidar."
                    : "Selecciona al menos una nota de venta pendiente del día.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var ahora = DateTimeOffset.UtcNow;
        var ventaConsolidada = await CrearVentaConsolidadaAsync(seleccionadas, ahora, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var boleta = await EmitirDesdeVentaAsync(ventaConsolidada.Id, TipoComprobanteSunat.BOLETA, null, cancellationToken);
        if (boleta.Estado != EstadoEmisionSunat.ACEPTADO)
        {
            await tx.CommitAsync(cancellationToken);
            throw new BusinessRuleException(
                $"SUNAT no aceptó la boleta consolidada ({boleta.Estado}). {boleta.Mensaje}".Trim());
        }

        var comprobanteBoleta = await db.Comprobantes.FirstAsync(c => c.Id == boleta.Id, cancellationToken);
        var consolidacion = new BoletaConsolidada
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            ComprobanteId = comprobanteBoleta.Id,
            VentaId = ventaConsolidada.Id,
            Filtro = request.Filtro,
            FechaOperacion = dia,
            Total = IgvCalculo.Round2(seleccionadas.Sum(c => c.Venta?.Total ?? 0)),
            CantidadNotas = seleccionadas.Count,
            FechaCreacion = ahora
        };
        db.BoletasConsolidadas.Add(consolidacion);

        foreach (var nota in seleccionadas)
        {
            nota.Estado = EstadoEmisionSunat.CONSOLIDADA;
            nota.BoletaConsolidada = consolidacion;
            nota.Mensaje = Truncar(
                $"Facturada / Consolidada en {boleta.Serie}-{boleta.Correlativo:00000000}",
                500);
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new BoletaConsolidadaResponse
        {
            Id = consolidacion.Id,
            Filtro = consolidacion.Filtro,
            FechaOperacion = dia,
            CantidadNotas = consolidacion.CantidadNotas,
            Total = consolidacion.Total,
            Comprobante = boleta,
            Notas = seleccionadas.Select(MapNotaPendiente).ToList()
        };
    }

    private static void ValidarTipo(Venta venta, TipoComprobanteSunat tipo, EmitirNotaCreditoRequest? nota)
    {
        var cliente = venta.Cliente
            ?? throw new BusinessRuleException("La venta no tiene cliente para emitir CPE.");

        switch (tipo)
        {
            case TipoComprobanteSunat.FACTURA:
                DocumentoIdentidad.ValidarComprobante(
                    tipo,
                    cliente.TipoDocumento,
                    cliente.NumeroDocumento);
                break;
            case TipoComprobanteSunat.NOTA_CREDITO:
                if (nota is null || string.IsNullOrWhiteSpace(nota.DescripcionMotivo))
                {
                    throw new BusinessRuleException("La nota de crédito exige motivo y descripción.");
                }

                break;
            case TipoComprobanteSunat.GUIA_REMISION:
                throw new BusinessRuleException("La guía de remisión no se emite desde este sprint.");
            case TipoComprobanteSunat.BOLETA:
            case TipoComprobanteSunat.NOTA_VENTA:
                DocumentoIdentidad.ValidarComprobante(
                    tipo,
                    cliente.TipoDocumento,
                    cliente.NumeroDocumento);
                break;
            default:
                throw new BusinessRuleException("Tipo de comprobante no soportado.");
        }
    }

    private async Task<SerieComprobante> ResolverSerieAsync(
        TipoComprobanteSunat tipo,
        Venta venta,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var serieCodigo = tipo == TipoComprobanteSunat.NOTA_CREDITO
            ? FiscalCodes.SerieNotaCredito(ComprobanteOrigen(venta).Tipo)
            : FiscalCodes.SeriePorDefecto(tipo);
        var serie = await db.SeriesComprobante
            .FirstOrDefaultAsync(s => s.Tipo == tipo && s.Activa && s.Serie == serieCodigo, cancellationToken);

        if (serie is not null)
        {
            return serie;
        }

        serie = new SerieComprobante
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Tipo = tipo,
            Serie = serieCodigo,
            Correlativo = 0,
            Activa = true,
            FechaCreacion = ahora
        };
        db.SeriesComprobante.Add(serie);
        return serie;
    }

    private async Task<ConfiguracionFiscalEmpresa> ResolverFiscalAsync(CancellationToken cancellationToken)
    {
        var fiscal = await db.ConfiguracionesFiscales.FirstOrDefaultAsync(cancellationToken);
        if (fiscal is not null)
        {
            if (fiscal.Ruc.Length != 11)
            {
                throw new BusinessRuleException("La ficha fiscal no tiene un RUC de 11 dígitos. No se emite CPE.");
            }

            return fiscal;
        }

        var empresa = await db.Empresas.AsNoTracking()
            .FirstAsync(e => e.Id == tenant.EmpresaId, cancellationToken);
        if (empresa.Ruc.Length != 11)
        {
            throw new BusinessRuleException("Sin RUC válido no se emite CPE.");
        }

        fiscal = new ConfiguracionFiscalEmpresa
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Ruc = empresa.Ruc,
            RazonSocial = empresa.RazonSocial,
            NombreComercial = empresa.NombreComercial,
            DireccionFiscal = "SIN DIRECCION",
            Ubigeo = "150101",
            Departamento = "LIMA",
            Provincia = "LIMA",
            Distrito = "LIMA",
            FechaCreacion = DateTimeOffset.UtcNow,
            FechaActualizacion = DateTimeOffset.UtcNow
        };
        db.ConfiguracionesFiscales.Add(fiscal);
        return fiscal;
    }

    private static EmitirCpeRequest ArmarPayload(
        Venta venta,
        Cliente cliente,
        ConfiguracionFiscalEmpresa fiscal,
        TipoComprobanteSunat tipo,
        string serieCodigo,
        int correlativo,
        EmitirNotaCreditoRequest? nota)
    {
        EmitirCpeDocumentoReferenciaDto? referencia = null;
        if (tipo == TipoComprobanteSunat.NOTA_CREDITO)
        {
            var origen = ComprobanteOrigen(venta);
            referencia = new EmitirCpeDocumentoReferenciaDto
            {
                TipoComprobante = origen.TipoSunat,
                SerieCorrelativo = $"{origen.Serie}-{origen.Correlativo}"
            };
        }

        var items = venta.Detalles.Select(d => new EmitirCpeItemDto
        {
            Codigo = d.CodigoSku,
            Descripcion = d.Descripcion,
            UnidadMedida = "NIU",
            Cantidad = IgvCalculo.Round2(d.Cantidad),
            ValorUnitario = IgvCalculo.Round2(d.ValorUnitario),
            PrecioUnitario = IgvCalculo.Round2(d.PrecioUnitario),
            Subtotal = IgvCalculo.Round2(d.Subtotal),
            Igv = IgvCalculo.Round2(d.Igv),
            Total = IgvCalculo.Round2(d.Total),
            CodigoAfectacionIgv = string.IsNullOrWhiteSpace(d.CodigoAfectacionIgv)
                ? "10"
                : d.CodigoAfectacionIgv
        }).ToList();

        var totalGravada = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "10").Sum(i => i.Subtotal));
        var totalExonerada = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "20").Sum(i => i.Subtotal));
        var totalInafecta = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "30").Sum(i => i.Subtotal));
        var totalIgv = IgvCalculo.Round2(items.Sum(i => i.Igv));
        var total = IgvCalculo.Round2(items.Sum(i => i.Total));

        return new EmitirCpeRequest
        {
            RucEmisor = fiscal.Ruc,
            Emisor = new EmitirCpeEmisorDto
            {
                Ruc = fiscal.Ruc,
                RazonSocial = fiscal.RazonSocial,
                NombreComercial = fiscal.NombreComercial,
                Ubigeo = fiscal.Ubigeo,
                Direccion = fiscal.DireccionFiscal,
                Departamento = fiscal.Departamento,
                Provincia = fiscal.Provincia,
                Distrito = fiscal.Distrito
            },
            TipoComprobante = FiscalCodes.CodigoComprobante(tipo),
            Serie = serieCodigo,
            Correlativo = correlativo,
            FechaEmision = FechaEmisionPeru(venta.Fecha),
            Moneda = "PEN",
            TipoOperacion = "0101",
            FormaPago = "CONTADO",
            MontoPendientePago = 0,
            Cuotas = [],
            Cliente = new EmitirCpeClienteDto
            {
                TipoDocumento = FiscalCodes.CodigoDocumento(cliente.TipoDocumento),
                NumeroDocumento = cliente.NumeroDocumento ?? "00000000",
                RazonSocial = cliente.Nombre
            },
            Items = items,
            TotalGravada = totalGravada,
            TotalExonerada = totalExonerada,
            TotalInafecta = totalInafecta,
            TotalIgv = totalIgv,
            Total = total,
            MontoEnLetras = FiscalCodes.MontoEnLetras(total),
            CodigoMotivo = nota?.CodigoMotivo,
            DescripcionMotivo = nota?.DescripcionMotivo,
            DocumentoReferencia = referencia,
            Observacion = venta.EsConsolidacion
                ? "Boleta consolidada de notas de venta del día."
                : null
        };
    }

    private static Comprobante ComprobanteOrigen(Venta venta) =>
        venta.Comprobantes
            .Where(c => c.Tipo is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.FACTURA)
            .OrderByDescending(c => c.FechaCreacion)
            .FirstOrDefault()
        ?? throw new BusinessRuleException("La nota de crédito exige un comprobante de venta previo.");

    private static DateTime FechaEmisionPeru(DateTimeOffset fecha)
    {
        var local = ZonaHorariaPeru.FechaEmisionLocal(fecha);
        return local;
    }

    private static EmitirCpeRequest? ResolverPayload(Comprobante comprobante)
    {
        if (!string.IsNullOrWhiteSpace(comprobante.PayloadJson))
        {
            try
            {
                var desdeJson = JsonSerializer.Deserialize<EmitirCpeRequest>(comprobante.PayloadJson, Json);
                if (desdeJson is not null && desdeJson.Items.Count > 0)
                {
                    return desdeJson;
                }
            }
            catch (JsonException)
            {
            }
        }

        var venta = comprobante.Venta;
        var cliente = venta?.Cliente;
        if (venta is null || cliente is null || venta.Detalles.Count == 0)
        {
            return null;
        }

        return ReconstruirPayload(comprobante, venta, cliente);
    }

    private static EmitirCpeRequest ReconstruirPayload(Comprobante comprobante, Venta venta, Cliente cliente)
    {
        var items = venta.Detalles.Select(d => new EmitirCpeItemDto
        {
            Codigo = d.CodigoSku,
            Descripcion = d.Descripcion,
            UnidadMedida = "NIU",
            Cantidad = IgvCalculo.Round2(d.Cantidad),
            ValorUnitario = IgvCalculo.Round2(d.ValorUnitario),
            PrecioUnitario = IgvCalculo.Round2(d.PrecioUnitario),
            Subtotal = IgvCalculo.Round2(d.Subtotal),
            Igv = IgvCalculo.Round2(d.Igv),
            Total = IgvCalculo.Round2(d.Total),
            CodigoAfectacionIgv = string.IsNullOrWhiteSpace(d.CodigoAfectacionIgv)
                ? "10"
                : d.CodigoAfectacionIgv
        }).ToList();

        return new EmitirCpeRequest
        {
            RucEmisor = string.Empty,
            Emisor = new EmitirCpeEmisorDto
            {
                Ruc = string.Empty,
                RazonSocial = string.Empty,
                NombreComercial = string.Empty,
                Ubigeo = string.Empty,
                Direccion = string.Empty,
                Departamento = string.Empty,
                Provincia = string.Empty,
                Distrito = string.Empty
            },
            TipoComprobante = comprobante.TipoSunat,
            Serie = comprobante.Serie,
            Correlativo = comprobante.Correlativo,
            FechaEmision = comprobante.FechaEmision.UtcDateTime,
            Moneda = "PEN",
            TipoOperacion = "0101",
            FormaPago = "CONTADO",
            MontoPendientePago = 0,
            Cuotas = [],
            Cliente = new EmitirCpeClienteDto
            {
                TipoDocumento = FiscalCodes.CodigoDocumento(cliente.TipoDocumento),
                NumeroDocumento = cliente.NumeroDocumento ?? "00000000",
                RazonSocial = cliente.Nombre
            },
            Items = items,
            TotalGravada = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "10").Sum(i => i.Subtotal)),
            TotalExonerada = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "20").Sum(i => i.Subtotal)),
            TotalInafecta = IgvCalculo.Round2(items.Where(i => i.CodigoAfectacionIgv == "30").Sum(i => i.Subtotal)),
            TotalIgv = IgvCalculo.Round2(items.Sum(i => i.Igv)),
            Total = IgvCalculo.Round2(items.Sum(i => i.Total)),
            MontoEnLetras = FiscalCodes.MontoEnLetras(IgvCalculo.Round2(items.Sum(i => i.Total))),
            CodigoMotivo = comprobante.CodigoMotivo,
            DescripcionMotivo = comprobante.DescripcionMotivo,
            DocumentoReferencia = string.IsNullOrWhiteSpace(comprobante.DocumentoReferencia)
                ? null
                : new EmitirCpeDocumentoReferenciaDto
                {
                    TipoComprobante = string.Empty,
                    SerieCorrelativo = comprobante.DocumentoReferencia
                }
        };
    }

    private static bool PuedeGenerarPdf(Comprobante c) =>
        !string.IsNullOrWhiteSpace(c.PayloadJson)
        || (c.Venta is { Detalles.Count: > 0, Cliente: not null });

    private static string? Truncar(string? valor, int maximo)
    {
        if (string.IsNullOrEmpty(valor) || valor.Length <= maximo)
        {
            return valor;
        }

        return valor[..maximo];
    }

    internal static ComprobanteResponse Map(
        Comprobante c,
        Venta? venta = null,
        bool incluirArchivos = true)
    {
        venta ??= c.Venta;
        return new ComprobanteResponse
        {
            Id = c.Id,
            VentaId = c.VentaId,
            Tipo = c.Tipo,
            TipoSunat = c.TipoSunat,
            Serie = c.Serie,
            Correlativo = c.Correlativo,
            Estado = c.Estado,
            Xml = incluirArchivos ? c.Xml : null,
            Cdr = incluirArchivos ? c.Cdr : null,
            HashFirma = c.HashFirma,
            Mensaje = c.Mensaje,
            CodigoMotivo = c.CodigoMotivo,
            DescripcionMotivo = c.DescripcionMotivo,
            DocumentoReferencia = c.DocumentoReferencia,
            ClienteNombre = venta?.Cliente?.Nombre,
            Total = venta?.Total,
            BoletaConsolidadaId = c.BoletaConsolidadaId,
            TieneXml = EsXml(c.Xml),
            TieneCdr = EsXml(c.Cdr) || EsZipBase64(c.Cdr),
            TienePdf = PuedeGenerarPdf(c),
            FechaEmision = c.FechaEmision,
            FechaEnvioSunat = c.FechaEnvioSunat
        };
    }

    private static CpeArchivoDescarga? MaterializarArchivo(string? bruto, Comprobante comprobante, bool esCdr)
    {
        if (EsXml(bruto))
        {
            var nombre = esCdr
                ? $"R-{comprobante.Serie}-{comprobante.Correlativo:00000000}.xml"
                : $"{comprobante.Serie}-{comprobante.Correlativo:00000000}.xml";
            return new CpeArchivoDescarga
            {
                Contenido = Encoding.UTF8.GetBytes(bruto!),
                ContentType = "application/xml; charset=utf-8",
                NombreArchivo = nombre
            };
        }

        if (esCdr && EsZipBase64(bruto, out var zip))
        {
            return new CpeArchivoDescarga
            {
                Contenido = zip,
                ContentType = "application/zip",
                NombreArchivo = $"R-{comprobante.Serie}-{comprobante.Correlativo:00000000}.zip"
            };
        }

        return null;
    }

    private static bool EsXml(string? valor) =>
        !string.IsNullOrWhiteSpace(valor)
        && valor.Contains('<')
        && (valor.Contains("<?xml", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("<Invoice", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("<CreditNote", StringComparison.OrdinalIgnoreCase)
            || valor.Contains("<ApplicationResponse", StringComparison.OrdinalIgnoreCase)
            || valor.TrimStart().StartsWith('<'));

    private static bool EsZipBase64(string? valor) => EsZipBase64(valor, out _);

    private static bool EsZipBase64(string? valor, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(valor) || valor.Contains('<') || valor.Length < 8)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(valor.Trim());
            return bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private async Task<List<Comprobante>> ConsultarNotasDelDiaAsync(DateOnly dia, CancellationToken cancellationToken)
    {
        var (desde, hasta) = ZonaHorariaPeru.RangoUtcDelDia(dia);
        return await db.Comprobantes
            .Include(c => c.Venta).ThenInclude(v => v.Cliente)
            .Include(c => c.Venta).ThenInclude(v => v.Detalles)
            .Where(c =>
                c.Tipo == TipoComprobanteSunat.NOTA_VENTA
                && c.FechaEmision >= desde
                && c.FechaEmision < hasta)
            .OrderBy(c => c.FechaEmision)
            .ThenBy(c => c.Correlativo)
            .ToListAsync(cancellationToken);
    }

    private static List<Comprobante> ResolverSeleccionGeneral(
        IReadOnlyList<Comprobante> pendientes,
        IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            throw new BusinessRuleException("Selecciona las notas de venta a consolidar.");
        }

        var mapa = pendientes.ToDictionary(c => c.Id);
        var seleccionadas = new List<Comprobante>();
        foreach (var id in ids.Distinct())
        {
            if (!mapa.TryGetValue(id, out var nota))
            {
                throw new BusinessRuleException(
                    "Hay notas de venta que no están pendientes de consolidar en la fecha indicada.");
            }

            seleccionadas.Add(nota);
        }

        return seleccionadas;
    }

    private async Task<Venta> CrearVentaConsolidadaAsync(
        IReadOnlyList<Comprobante> notas,
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var origenes = notas
            .Select(n => n.Venta ?? throw new BusinessRuleException("La nota de venta no tiene venta asociada."))
            .ToList();
        var sedeId = origenes[0].SedeId;
        var cliente = await ResolverClienteVariosAsync(ahora, cancellationToken);
        var ventaId = Guid.NewGuid();
        var (inicioDia, _) = ZonaHorariaPeru.RangoUtcDelDia(ZonaHorariaPeru.FechaLocal(ahora));

        var venta = new Venta
        {
            Id = ventaId,
            EmpresaId = tenant.EmpresaId,
            SedeId = sedeId,
            ClienteId = cliente.Id,
            Canal = origenes[0].Canal,
            Fecha = inicioDia,
            UsuarioId = origenes[0].UsuarioId,
            EsConsolidacion = true,
            FechaCreacion = ahora
        };

        foreach (var grupo in origenes.SelectMany(v => v.Detalles).GroupBy(d => d.ProductoId))
        {
            var cantidad = grupo.Sum(d => d.Cantidad);
            var total = IgvCalculo.Round2(grupo.Sum(d => d.Total));
            var precio = cantidad == 0 ? 0 : IgvCalculo.Round2(total / cantidad);
            var (valorUnitario, subtotal, igv, totalLinea) = IgvCalculo.Linea(cantidad, precio);
            var primero = grupo.First();
            venta.Detalles.Add(new VentaDetalle
            {
                Id = Guid.NewGuid(),
                EmpresaId = tenant.EmpresaId,
                VentaId = ventaId,
                ProductoId = primero.ProductoId,
                CodigoSku = primero.CodigoSku,
                Descripcion = primero.Descripcion,
                Cantidad = cantidad,
                PrecioUnitario = precio,
                ValorUnitario = valorUnitario,
                Subtotal = subtotal,
                Igv = igv,
                Total = totalLinea,
                CodigoAfectacionIgv = string.IsNullOrWhiteSpace(primero.CodigoAfectacionIgv)
                    ? "10"
                    : primero.CodigoAfectacionIgv
            });
        }

        if (venta.Detalles.Count == 0)
        {
            throw new BusinessRuleException("Las notas de venta seleccionadas no tienen ítems para consolidar.");
        }

        venta.Subtotal = IgvCalculo.Round2(venta.Detalles.Sum(d => d.Subtotal));
        venta.Igv = IgvCalculo.Round2(venta.Detalles.Sum(d => d.Igv));
        venta.Total = IgvCalculo.Round2(venta.Detalles.Sum(d => d.Total));
        db.Ventas.Add(venta);
        return venta;
    }

    private async Task<Cliente> ResolverClienteVariosAsync(DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var existente = await db.Clientes.FirstOrDefaultAsync(
            c => c.NumeroDocumento == DocumentoIdentidad.NumeroSinDocumento
                && (c.Nombre == DocumentoIdentidad.NombreClienteVarios
                    || c.Nombre == DocumentoIdentidad.NombrePublicoGeneral),
            cancellationToken);
        if (existente is not null)
        {
            existente.TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO;
            existente.NumeroDocumento = DocumentoIdentidad.NumeroSinDocumento;
            return existente;
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            EmpresaId = tenant.EmpresaId,
            Nombre = DocumentoIdentidad.NombreClienteVarios,
            TipoDocumento = TipoDocumentoIdentidad.SIN_DOCUMENTO,
            NumeroDocumento = DocumentoIdentidad.NumeroSinDocumento,
            FechaCreacion = ahora
        };
        db.Clientes.Add(cliente);
        return cliente;
    }

    private static NotaVentaPendienteResponse MapNotaPendiente(Comprobante c) => new()
    {
        Id = c.Id,
        VentaId = c.VentaId,
        Serie = c.Serie,
        Correlativo = c.Correlativo,
        Estado = c.Estado,
        ClienteNombre = c.Venta?.Cliente?.Nombre ?? string.Empty,
        Total = c.Venta?.Total ?? 0,
        FechaEmision = c.FechaEmision,
        EsVentaMenor = (c.Venta?.Total ?? 0) < UmbralVentasMenores
    };

    private static bool EsDocumentoDeVenta(TipoComprobanteSunat tipo) =>
        tipo is TipoComprobanteSunat.BOLETA or TipoComprobanteSunat.FACTURA or TipoComprobanteSunat.NOTA_VENTA;
}
