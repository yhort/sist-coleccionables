using CapitalPos.Tcg.Api.Application.Aperturas;
using CapitalPos.Tcg.Api.Contracts.Aperturas;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/aperturas-tcg")]
public sealed class AperturasTcgController(AperturasTcgService aperturas) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AperturaTcgResponse>>> Listar(
        [FromQuery] Guid? sedeId,
        [FromQuery] EstadoAperturaTcg? estado,
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        CancellationToken cancellationToken)
    {
        var items = await aperturas.ListarAsync(sedeId, estado, desde, hasta, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AperturaTcgResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var apertura = await aperturas.ObtenerAsync(id, cancellationToken);
        return apertura is null ? NotFound() : Ok(apertura);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<AperturaTcgResponse>> Crear(
        [FromBody] CrearAperturaTcgRequest request,
        CancellationToken cancellationToken)
    {
        var creada = await aperturas.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<AperturaTcgResponse>> ActualizarBorrador(
        Guid id,
        [FromBody] CrearAperturaTcgRequest request,
        CancellationToken cancellationToken)
    {
        var actualizada = await aperturas.ActualizarBorradorAsync(id, request, cancellationToken);
        return Ok(actualizada);
    }

    [HttpPut("{id:guid}/detalles")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<AperturaTcgResponse>> ReemplazarDetalles(
        Guid id,
        [FromBody] IReadOnlyList<AperturaDetalleInput> detalles,
        CancellationToken cancellationToken)
    {
        var actualizada = await aperturas.ReemplazarDetallesAsync(id, detalles, cancellationToken);
        return Ok(actualizada);
    }

    [HttpPost("{id:guid}/confirmar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<AperturaTcgResponse>> Confirmar(Guid id, CancellationToken cancellationToken)
    {
        var confirmada = await aperturas.ConfirmarAsync(id, cancellationToken);
        return Ok(confirmada);
    }

    [HttpPost("{id:guid}/anular")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<AperturaTcgResponse>> Anular(Guid id, CancellationToken cancellationToken)
    {
        var anulada = await aperturas.AnularAsync(id, cancellationToken);
        return Ok(anulada);
    }
}
