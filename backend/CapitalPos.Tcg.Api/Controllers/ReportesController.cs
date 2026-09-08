using CapitalPos.Tcg.Api.Application.Reportes;
using CapitalPos.Tcg.Api.Contracts.Reportes;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/reportes")]
public sealed class ReportesController(ReportesService reportes) : ControllerBase
{
    [HttpGet("kardex-resumen")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<IReadOnlyList<KardexResumenFila>>> Kardex(
        [FromQuery] Guid? sedeId,
        [FromQuery] TipoProducto? tipoProducto,
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var filas = await reportes.KardexResumenAsync(sedeId, tipoProducto, desde, hasta, cancellationToken);
        return Ok(filas);
    }

    [HttpGet("ventas-por-sede")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<VentasPorSedeResponse>> VentasPorSede(
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var reporte = await reportes.VentasPorSedeAsync(desde, hasta, cancellationToken);
        return Ok(reporte);
    }

    [HttpGet("aperturas")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<YieldAperturasResponse>> Aperturas(
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        [FromQuery] string? juego,
        CancellationToken cancellationToken)
    {
        var reporte = await reportes.MargenAperturasAsync(desde, hasta, juego, cancellationToken);
        return Ok(reporte);
    }
}
