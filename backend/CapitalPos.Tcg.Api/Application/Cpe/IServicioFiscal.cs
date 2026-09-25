using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Application.Cpe;

public interface IServicioFiscal
{
    Task<ComprobanteResponse> EmitirDesdeVentaAsync(
        Guid ventaId,
        TipoComprobanteSunat tipo,
        EmitirNotaCreditoRequest? notaCredito,
        CancellationToken cancellationToken);

    Task<ComprobanteResponse?> ObtenerPorVentaAsync(Guid ventaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ComprobanteResponse>> ListarPorVentaAsync(Guid ventaId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ComprobanteResponse>> ListarAsync(CancellationToken cancellationToken);

    Task<CpeArchivoDescarga?> ObtenerArchivoAsync(
        Guid comprobanteId,
        string tipo,
        CancellationToken cancellationToken);

    Task<CpeArchivoDescarga?> ObtenerPdfAsync(
        Guid comprobanteId,
        CpePdfFormato formato,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotaVentaPendienteResponse>> ListarNotasPendientesAsync(
        DateOnly? fecha,
        CancellationToken cancellationToken);

    Task<BoletaConsolidadaResponse> GenerarBoletaConsolidadaAsync(
        GenerarBoletaConsolidadaRequest request,
        CancellationToken cancellationToken);
}
