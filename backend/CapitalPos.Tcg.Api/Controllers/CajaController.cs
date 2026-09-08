using CapitalPos.Tcg.Api.Application.Caja;
using CapitalPos.Tcg.Api.Contracts.Caja;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/caja")]
public sealed class CajaController(CajaService caja) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CajaSesionResponse>>> Listar(
        [FromQuery] Guid? sedeId,
        [FromQuery] EstadoCajaSesion? estado,
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var items = await caja.ListarAsync(sedeId, estado, desde, hasta, cancellationToken);
        return Ok(items);
    }

    [HttpGet("sesion-actual")]
    public async Task<ActionResult<CajaEstadoActualResponse>> SesionActual(
        [FromQuery] Guid sedeId,
        CancellationToken cancellationToken)
    {
        if (sedeId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "sedeId requerido",
                Detail = "El query sedeId es obligatorio."
            });
        }

        return Ok(await caja.EstadoActualAsync(sedeId, cancellationToken));
    }

    [HttpGet("resumen-actual")]
    public async Task<ActionResult<CajaResumenResponse>> ResumenActual(
        [FromQuery] Guid sedeId,
        CancellationToken cancellationToken)
    {
        if (sedeId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "sedeId requerido",
                Detail = "El query sedeId es obligatorio para armar el resumen del turno."
            });
        }

        return Ok(await caja.ResumenActualAsync(sedeId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CajaSesionResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var sesion = await caja.ObtenerAsync(id, cancellationToken);
        return sesion is null ? NotFound() : Ok(sesion);
    }

    [HttpGet("{id:guid}/reporte-ticket")]
    public async Task<ActionResult<CajaTicketReporteResponse>> ReporteTicket(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await caja.ReporteTicketAsync(id, cancellationToken));
    }

    [HttpPost("apertura")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<CajaSesionResponse>> Apertura(
        [FromBody] AbrirCajaRequest request,
        CancellationToken cancellationToken)
    {
        var abierta = await caja.AbrirAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = abierta.Id }, abierta);
    }

    [HttpPost("cierre")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<CajaResumenResponse>> Cierre(
        [FromBody] CerrarCajaRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await caja.CerrarAsync(request, cancellationToken));
    }

    [HttpPost("movimientos")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<CajaMovimientoResponse>> Movimiento(
        [FromBody] RegistrarCajaMovimientoRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await caja.RegistrarMovimientoAsync(request, cancellationToken);
        return Ok(creado);
    }
}
