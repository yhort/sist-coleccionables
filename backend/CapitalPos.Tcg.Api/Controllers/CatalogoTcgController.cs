using CapitalPos.Tcg.Api.Application.Catalogo;
using CapitalPos.Tcg.Api.Contracts.Catalogo;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/tcg")]
public sealed class CatalogoTcgController(CatalogoTcgService catalogo) : ControllerBase
{
    [HttpGet("series")]
    public async Task<ActionResult<IReadOnlyList<TcgSerieResponse>>> ListarSeries(
        CancellationToken cancellationToken)
    {
        return Ok(await catalogo.ListarSeriesAsync(cancellationToken));
    }

    [HttpPost("series")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<TcgSerieResponse>> CrearSerie(
        [FromBody] UpsertTcgSerieRequest request,
        CancellationToken cancellationToken)
    {
        var creada = await catalogo.CrearSerieAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListarSeries), new { id = creada.Id }, creada);
    }

    [HttpGet("sets")]
    public async Task<ActionResult<IReadOnlyList<TcgSetResponse>>> ListarSets(
        [FromQuery] Guid serieId,
        CancellationToken cancellationToken)
    {
        return Ok(await catalogo.ListarSetsAsync(serieId, cancellationToken));
    }

    [HttpPost("sets")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<TcgSetResponse>> CrearSet(
        [FromBody] UpsertTcgSetRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await catalogo.CrearSetAsync(request, cancellationToken);
        return CreatedAtAction(nameof(ListarSets), new { serieId = creado.SerieId }, creado);
    }

    [HttpPut("sets/{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<TcgSetResponse>> ActualizarSet(
        Guid id,
        [FromBody] ActualizarTcgSetRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await catalogo.ActualizarSetAsync(id, request, cancellationToken));
    }

    [HttpDelete("sets/{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<IActionResult> EliminarSet(Guid id, CancellationToken cancellationToken)
    {
        await catalogo.EliminarSetAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("cartas")]
    public async Task<ActionResult<IReadOnlyList<TcgCartaResponse>>> ListarCartas(
        [FromQuery] Guid setId,
        [FromQuery] string? numero,
        [FromQuery] RarezaTcg? rareza,
        CancellationToken cancellationToken)
    {
        return Ok(await catalogo.ListarCartasAsync(setId, numero, rareza, cancellationToken));
    }

    [HttpPost("cartas/importar-set")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ImportarSetTcgResponse>> ImportarSet(
        [FromBody] ImportarSetTcgRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await catalogo.ImportarSetAsync(request, cancellationToken);
        return Ok(resultado);
    }
}
