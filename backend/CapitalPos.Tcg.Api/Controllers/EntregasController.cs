using CapitalPos.Tcg.Api.Application.Entregas;
using CapitalPos.Tcg.Api.Contracts.Entregas;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/entregas")]
public sealed class EntregasController(EntregasService entregas) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EntregaResponse>>> Listar(
        [FromQuery] EstadoLogistica? estado,
        [FromQuery] Guid? sedeOrigenId,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken cancellationToken)
    {
        var items = await entregas.ListarAsync(
            estado, sedeOrigenId, fechaDesde, fechaHasta, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EntregaResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var entrega = await entregas.ObtenerAsync(id, cancellationToken);
        return entrega is null ? NotFound() : Ok(entrega);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<EntregaResponse>> Programar(
        [FromBody] ProgramarEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var creada = await entregas.ProgramarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPost("empaquetar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<EntregaResponse>> Empaquetar(
        [FromBody] EmpaquetarEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var actualizada = await entregas.EmpaquetarAsync(request, cancellationToken);
        return Ok(actualizada);
    }

    [HttpPost("{id:guid}/despachar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<EntregaResponse>> Despachar(
        Guid id,
        [FromBody] DespacharEntregaRequest request,
        CancellationToken cancellationToken)
    {
        var despachada = await entregas.DespacharAsync(id, request, cancellationToken);
        return Ok(despachada);
    }

    [HttpPost("{id:guid}/confirmar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<object>> Confirmar(
        Guid id,
        [FromBody] ConvertirVentaRequest? request,
        CancellationToken cancellationToken)
    {
        var (entrega, conversion) = await entregas.ConfirmarAsync(id, request, cancellationToken);
        return Ok(new { entrega, conversion });
    }

    [HttpPost("{id:guid}/fallar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<EntregaResponse>> Fallar(
        Guid id,
        [FromBody] FallarEntregaRequest? request,
        CancellationToken cancellationToken)
    {
        var fallida = await entregas.FallarAsync(id, request?.Observacion, cancellationToken);
        return Ok(fallida);
    }
}
