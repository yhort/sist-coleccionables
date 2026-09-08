using System.ComponentModel.DataAnnotations;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Contracts.Cpe;

public sealed class EmitirCpeEmisorDto
{
    public required string Ruc { get; init; }
    public required string RazonSocial { get; init; }
    public required string NombreComercial { get; init; }
    public required string Ubigeo { get; init; }
    public required string Direccion { get; init; }
    public required string Departamento { get; init; }
    public required string Provincia { get; init; }
    public required string Distrito { get; init; }
}

public sealed class EmitirCpeClienteDto
{
    public required string TipoDocumento { get; init; }
    public required string NumeroDocumento { get; init; }
    public required string RazonSocial { get; init; }
}

public sealed class EmitirCpeItemDto
{
    public required string Codigo { get; init; }
    public required string Descripcion { get; init; }
    public required string UnidadMedida { get; init; }
    public required decimal Cantidad { get; init; }
    public required decimal ValorUnitario { get; init; }
    public required decimal PrecioUnitario { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Igv { get; init; }
    public required decimal Total { get; init; }
    public required string CodigoAfectacionIgv { get; init; }
}

public sealed class EmitirCpeDocumentoReferenciaDto
{
    public required string TipoComprobante { get; init; }
    public required string SerieCorrelativo { get; init; }
}

public sealed class EmitirCpeCuotaDto
{
    public required int Numero { get; init; }
    public required DateTime FechaVencimiento { get; init; }
    public required decimal Monto { get; init; }
}

public sealed class EmitirCpeRequest
{
    public required string RucEmisor { get; init; }
    public required EmitirCpeEmisorDto Emisor { get; init; }
    public required string TipoComprobante { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required DateTime FechaEmision { get; init; }
    public required string Moneda { get; init; }
    public required string TipoOperacion { get; init; }
    public string? Observacion { get; init; }
    public required string FormaPago { get; init; }
    public required decimal MontoPendientePago { get; init; }
    public IReadOnlyList<EmitirCpeCuotaDto> Cuotas { get; init; } = [];
    public required EmitirCpeClienteDto Cliente { get; init; }
    public required IReadOnlyList<EmitirCpeItemDto> Items { get; init; }
    public required decimal TotalGravada { get; init; }
    public required decimal TotalExonerada { get; init; }
    public required decimal TotalInafecta { get; init; }
    public required decimal TotalIgv { get; init; }
    public required decimal Total { get; init; }
    public required string MontoEnLetras { get; init; }
    public string? CodigoMotivo { get; init; }
    public string? DescripcionMotivo { get; init; }
    public EmitirCpeDocumentoReferenciaDto? DocumentoReferencia { get; init; }
}

public sealed class CpeEmisionResultado
{
    public required EstadoEmisionSunat Estado { get; init; }
    public required string Xml { get; init; }
    public string? Cdr { get; init; }
    public string? HashFirma { get; init; }
    public required string Mensaje { get; init; }
    public string? Serie { get; init; }
    public int? Correlativo { get; init; }
    public string? NombreXml { get; init; }
    public string? NombreCdr { get; init; }
}

public sealed class EmitirNotaCreditoRequest
{
    [Required]
    [MaxLength(8)]
    public string CodigoMotivo { get; set; } = "01";

    [Required]
    [MinLength(3)]
    [MaxLength(250)]
    public string DescripcionMotivo { get; set; } = string.Empty;
}

public sealed class ComprobanteResponse
{
    public required Guid Id { get; init; }
    public required Guid VentaId { get; init; }
    public required TipoComprobanteSunat Tipo { get; init; }
    public required string TipoSunat { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required EstadoEmisionSunat Estado { get; init; }
    public string? Xml { get; init; }
    public string? Cdr { get; init; }
    public string? HashFirma { get; init; }
    public string? Mensaje { get; init; }
    public string? CodigoMotivo { get; init; }
    public string? DescripcionMotivo { get; init; }
    public string? DocumentoReferencia { get; init; }
    public string? ClienteNombre { get; init; }
    public decimal? Total { get; init; }
    public Guid? BoletaConsolidadaId { get; init; }
    public required bool TieneXml { get; init; }
    public required bool TieneCdr { get; init; }
    public required bool TienePdf { get; init; }
    public required DateTimeOffset FechaEmision { get; init; }
    public DateTimeOffset? FechaEnvioSunat { get; init; }
}

public sealed class CpeArchivoDescarga
{
    public required byte[] Contenido { get; init; }
    public required string ContentType { get; init; }
    public required string NombreArchivo { get; init; }
}

public sealed class NotaVentaPendienteResponse
{
    public required Guid Id { get; init; }
    public required Guid VentaId { get; init; }
    public required string Serie { get; init; }
    public required int Correlativo { get; init; }
    public required EstadoEmisionSunat Estado { get; init; }
    public required string ClienteNombre { get; init; }
    public required decimal Total { get; init; }
    public required DateTimeOffset FechaEmision { get; init; }
    public required bool EsVentaMenor { get; init; }
}

public sealed class GenerarBoletaConsolidadaRequest
{
    public FiltroBoletaConsolidada Filtro { get; set; } = FiltroBoletaConsolidada.VENTAS_MENORES;

    public DateOnly? Fecha { get; set; }

    public IReadOnlyList<Guid> NotaVentaIds { get; set; } = [];
}

public sealed class BoletaConsolidadaResponse
{
    public required Guid Id { get; init; }
    public required FiltroBoletaConsolidada Filtro { get; init; }
    public required DateOnly FechaOperacion { get; init; }
    public required int CantidadNotas { get; init; }
    public required decimal Total { get; init; }
    public required ComprobanteResponse Comprobante { get; init; }
    public required IReadOnlyList<NotaVentaPendienteResponse> Notas { get; init; }
}
