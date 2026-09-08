using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Application.Ecosistema;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Contracts.Ecosistema;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ecosistema")]
public sealed class EcosistemaController(
    EcosistemaService ecosistema,
    IServicioFiscal fiscal) : ControllerBase
{
    [HttpGet("conexiones")]
    public async Task<ActionResult<IReadOnlyList<EcosistemaConexionResponse>>> Conexiones(
        CancellationToken cancellationToken)
    {
        var items = await ecosistema.ListarConexionesAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost("conexiones/{id:guid}/ping")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<EcosistemaConexionResponse>> Ping(Guid id, CancellationToken cancellationToken)
    {
        var conexion = await ecosistema.PingAsync(id, cancellationToken);
        return Ok(conexion);
    }

    [HttpGet("fiscal")]
    public async Task<ActionResult<ConfiguracionFiscalResponse>> Fiscal(CancellationToken cancellationToken)
    {
        var fiscalCfg = await ecosistema.ObtenerFiscalAsync(cancellationToken);
        return Ok(fiscalCfg);
    }

    [HttpPut("fiscal")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<ConfiguracionFiscalResponse>> GuardarFiscal(
        [FromBody] GuardarConfiguracionFiscalRequest request,
        CancellationToken cancellationToken)
    {
        var fiscalCfg = await ecosistema.GuardarFiscalAsync(request, cancellationToken);
        return Ok(fiscalCfg);
    }

    [HttpGet("series")]
    public async Task<ActionResult<IReadOnlyList<SerieComprobanteResponse>>> Series(
        CancellationToken cancellationToken)
    {
        var series = await ecosistema.ListarSeriesAsync(cancellationToken);
        return Ok(series);
    }

    [HttpGet("webhooks")]
    public async Task<ActionResult<WebhookSalidaResponse>> Webhook(CancellationToken cancellationToken)
    {
        var webhook = await ecosistema.ObtenerWebhookAsync(cancellationToken);
        return Ok(webhook);
    }

    [HttpPut("webhooks")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WebhookSalidaResponse>> GuardarWebhook(
        [FromBody] GuardarWebhookSalidaRequest request,
        CancellationToken cancellationToken)
    {
        var webhook = await ecosistema.GuardarWebhookAsync(request, cancellationToken);
        return Ok(webhook);
    }

    [HttpPost("webhooks/probar")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WebhookLogResponse>> ProbarWebhook(
        [FromBody] ProbarWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var log = await ecosistema.ProbarWebhookAsync(request, cancellationToken);
        return Ok(log);
    }

    [HttpGet("cpe")]
    public async Task<ActionResult<IReadOnlyList<ComprobanteResponse>>> ListarCpe(
        CancellationToken cancellationToken)
    {
        var comprobantes = await fiscal.ListarAsync(cancellationToken);
        return Ok(comprobantes);
    }

    [HttpPost("cpe/emitir-desde-venta/{ventaId:guid}")]
    [RequiresPermission(Permiso.EmitirCpe)]
    public async Task<ActionResult<ComprobanteResponse>> EmitirDesdeVenta(
        Guid ventaId,
        [FromQuery] TipoComprobanteSunat tipo = TipoComprobanteSunat.BOLETA,
        CancellationToken cancellationToken = default)
    {
        var comprobante = await ecosistema.EmitirDesdeVentaAsync(ventaId, tipo, cancellationToken);
        return Ok(comprobante);
    }

    [HttpPost("cpe/nota-credito/{ventaId:guid}")]
    [RequiresPermission(Permiso.EmitirCpe)]
    public async Task<ActionResult<ComprobanteResponse>> NotaCredito(
        Guid ventaId,
        [FromBody] EmitirNotaCreditoRequest request,
        CancellationToken cancellationToken)
    {
        var comprobante = await ecosistema.EmitirNotaCreditoAsync(ventaId, request, cancellationToken);
        return Ok(comprobante);
    }

    [HttpGet("cpe/venta/{ventaId:guid}")]
    public async Task<ActionResult<ComprobanteResponse>> ComprobanteVenta(
        Guid ventaId,
        CancellationToken cancellationToken)
    {
        var comprobante = await fiscal.ObtenerPorVentaAsync(ventaId, cancellationToken);
        return comprobante is null ? NotFound() : Ok(comprobante);
    }

    [HttpGet("cpe/{id:guid}/xml")]
    public async Task<IActionResult> DescargarXml(Guid id, CancellationToken cancellationToken)
    {
        var archivo = await fiscal.ObtenerArchivoAsync(id, "xml", cancellationToken);
        return archivo is null
            ? NotFound()
            : File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }

    [HttpGet("cpe/{id:guid}/cdr")]
    public async Task<IActionResult> DescargarCdr(Guid id, CancellationToken cancellationToken)
    {
        var archivo = await fiscal.ObtenerArchivoAsync(id, "cdr", cancellationToken);
        return archivo is null
            ? NotFound()
            : File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }

    [HttpGet("cpe/{id:guid}/pdf")]
    public async Task<IActionResult> DescargarPdf(
        Guid id,
        [FromQuery] string formato = "a4",
        CancellationToken cancellationToken = default)
    {
        var pdfFormato = formato.Equals("ticket", StringComparison.OrdinalIgnoreCase)
            ? CpePdfFormato.Ticket
            : CpePdfFormato.A4;
        var archivo = await fiscal.ObtenerPdfAsync(id, pdfFormato, cancellationToken);
        if (archivo is null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = $"inline; filename=\"{archivo.NombreArchivo}\"";
        return File(archivo.Contenido, archivo.ContentType);
    }

    [HttpGet("cpe/notas-venta-pendientes")]
    public async Task<ActionResult<IReadOnlyList<NotaVentaPendienteResponse>>> NotasPendientes(
        [FromQuery] DateOnly? fecha,
        CancellationToken cancellationToken)
    {
        var notas = await fiscal.ListarNotasPendientesAsync(fecha, cancellationToken);
        return Ok(notas);
    }

    [HttpPost("cpe/boleta-consolidada")]
    [RequiresPermission(Permiso.EmitirCpe)]
    public async Task<ActionResult<BoletaConsolidadaResponse>> GenerarBoletaConsolidada(
        [FromBody] GenerarBoletaConsolidadaRequest request,
        CancellationToken cancellationToken)
    {
        var consolidada = await fiscal.GenerarBoletaConsolidadaAsync(request, cancellationToken);
        return Ok(consolidada);
    }
}
