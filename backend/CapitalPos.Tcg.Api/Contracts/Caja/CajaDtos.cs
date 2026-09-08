using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Caja;

public sealed class AbrirCajaRequest
{
    [Required]
    public Guid SedeId { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal MontoApertura { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class CerrarCajaRequest
{
    public Guid? SedeId { get; set; }

    public Guid? CajaSesionId { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal MontoEfectivoReal { get; set; }

    [MaxLength(500)]
    public string? Observacion { get; set; }
}

public sealed class RegistrarCajaMovimientoRequest
{
    public Guid? SedeId { get; set; }

    public Guid? CajaSesionId { get; set; }

    public TipoCajaMovimiento Tipo { get; set; } = TipoCajaMovimiento.INGRESO;

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal Monto { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(200)]
    public string Concepto { get; set; } = string.Empty;
}

public sealed class CajaMovimientoResponse
{
    public required Guid Id { get; init; }
    public required Guid CajaSesionId { get; init; }
    public required TipoCajaMovimiento Tipo { get; init; }
    public required decimal Monto { get; init; }
    public required string Concepto { get; init; }
    public Guid? UsuarioId { get; init; }
    public string? UsuarioNombre { get; init; }
    public required DateTimeOffset Fecha { get; init; }
}

public sealed class CajaMedioPagoFila
{
    public required string Grupo { get; init; }
    public required string Etiqueta { get; init; }
    public required decimal Monto { get; init; }
    public required int Cantidad { get; init; }
}

public sealed class CajaDocumentoFila
{
    public required TipoComprobanteSunat Tipo { get; init; }
    public required string Etiqueta { get; init; }
    public required int Cantidad { get; init; }
    public required decimal Total { get; init; }
}

public sealed class CajaVentaFila
{
    public required Guid VentaId { get; init; }
    public required DateTimeOffset Fecha { get; init; }
    public required decimal Total { get; init; }
    public string? ClienteNombre { get; init; }
    public required string MediosPago { get; init; }
    public string? Comprobante { get; init; }
}

public sealed class CajaEfectivoResumen
{
    public required decimal MontoApertura { get; init; }
    public required decimal VentasEfectivo { get; init; }
    public required decimal Ingresos { get; init; }
    public required decimal Egresos { get; init; }
    public required decimal MontoEfectivoTeorico { get; init; }
    public decimal? MontoEfectivoReal { get; init; }
    public decimal? Diferencia { get; init; }
    public TipoDiferenciaCaja? TipoDiferencia { get; init; }
}

public sealed class CajaResumenResponse
{
    public required Guid CajaSesionId { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required EstadoCajaSesion Estado { get; init; }
    public required DateTimeOffset FechaApertura { get; init; }
    public DateTimeOffset? FechaCierre { get; init; }
    public required string UsuarioAperturaNombre { get; init; }
    public string? UsuarioCierreNombre { get; init; }
    public required int CantidadVentas { get; init; }
    public required decimal TotalVentas { get; init; }
    public required CajaEfectivoResumen Efectivo { get; init; }
    public required IReadOnlyList<CajaMedioPagoFila> MediosPago { get; init; }
    public required IReadOnlyList<CajaDocumentoFila> Documentos { get; init; }
    public required IReadOnlyList<CajaMovimientoResponse> Movimientos { get; init; }
    public required IReadOnlyList<CajaVentaFila> Ventas { get; init; }
}

public sealed class CajaSesionResponse
{
    public required Guid Id { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public required Guid UsuarioId { get; init; }
    public required string UsuarioNombre { get; init; }
    public Guid? UsuarioCierreId { get; init; }
    public string? UsuarioCierreNombre { get; init; }
    public required decimal MontoApertura { get; init; }
    public required DateTimeOffset FechaApertura { get; init; }
    public DateTimeOffset? FechaCierre { get; init; }
    public required EstadoCajaSesion Estado { get; init; }
    public required decimal MontoEfectivoTeorico { get; init; }
    public decimal? MontoEfectivoReal { get; init; }
    public decimal? Diferencia { get; init; }
    public TipoDiferenciaCaja? TipoDiferencia { get; init; }
    public string? ObservacionApertura { get; init; }
    public string? ObservacionCierre { get; init; }
    public required int CantidadVentas { get; init; }
    public required decimal TotalVentas { get; init; }
}

public sealed class CajaEstadoActualResponse
{
    public required bool Abierta { get; init; }
    public required Guid SedeId { get; init; }
    public required string SedeNombre { get; init; }
    public string? Mensaje { get; init; }
    public CajaSesionResponse? Sesion { get; init; }
    public CajaResumenResponse? Resumen { get; init; }
}

public sealed class CajaTicketEmpresa
{
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public required string NombreComercial { get; init; }
}

public sealed class CajaTicketLinea
{
    public required string Etiqueta { get; init; }
    public required string Valor { get; init; }
}

public sealed class CajaTicketReporteResponse
{
    public required Guid CajaSesionId { get; init; }
    public required string Titulo { get; init; }
    public required string AnchoMm { get; init; }
    public required CajaTicketEmpresa Empresa { get; init; }
    public required string SedeNombre { get; init; }
    public required string UsuarioAperturaNombre { get; init; }
    public string? UsuarioCierreNombre { get; init; }
    public required string FechaAperturaLocal { get; init; }
    public string? FechaCierreLocal { get; init; }
    public required EstadoCajaSesion Estado { get; init; }
    public required IReadOnlyList<CajaTicketLinea> Encabezado { get; init; }
    public required IReadOnlyList<CajaMedioPagoFila> MediosPago { get; init; }
    public required IReadOnlyList<CajaDocumentoFila> Documentos { get; init; }
    public required IReadOnlyList<CajaMovimientoResponse> Movimientos { get; init; }
    public required CajaEfectivoResumen Efectivo { get; init; }
    public required string LeyendaDiferencia { get; init; }
    public required IReadOnlyList<string> Pie { get; init; }
}
