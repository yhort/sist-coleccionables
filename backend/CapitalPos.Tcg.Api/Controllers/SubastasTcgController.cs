using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Application.Subastas;
using CapitalPos.Tcg.Api.Contracts.Subastas;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/subastas-tcg")]
public sealed class SubastasTcgController(SubastasTcgService subastas) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubastaTcgResponse>>> Listar(
        [FromQuery] EstadoSubastaTcg? estado,
        [FromQuery] CanalSubastaTcg? canal,
        [FromQuery] Guid? sedeId,
        CancellationToken cancellationToken)
    {
        var items = await subastas.ListarAsync(estado, canal, sedeId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SubastaTcgResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var subasta = await subastas.ObtenerAsync(id, cancellationToken);
        return subasta is null ? NotFound() : Ok(subasta);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> Crear(
        [FromBody] CrearSubastaTcgRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var creada = await subastas.CrearAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
        }
        catch (BusinessRuleException ex)
        {
            return RespuestaRegla(ex);
        }
    }

    [HttpPost("{id:guid}/activar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> Activar(Guid id, CancellationToken cancellationToken)
    {
        return await EjecutarAsync(() => subastas.ActivarAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/pujas")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> RegistrarPuja(
        Guid id,
        [FromBody] RegistrarPujaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var actualizada = await subastas.RegistrarPujaAsync(id, request, cancellationToken);
            return Ok(actualizada);
        }
        catch (BusinessRuleException ex)
        {
            return RespuestaRegla(ex);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "No se pudo registrar la puja."
                    : ex.Message
            });
        }
    }

    [HttpPost("{id:guid}/cerrar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> Cerrar(Guid id, CancellationToken cancellationToken)
    {
        return await EjecutarAsync(() => subastas.CerrarAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/adjudicar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> Adjudicar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AdjudicarSubastaRequest? request,
        CancellationToken cancellationToken)
    {
        return await EjecutarAsync(() => subastas.AdjudicarAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/cancelar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<SubastaTcgResponse>> Cancelar(Guid id, CancellationToken cancellationToken)
    {
        return await EjecutarAsync(() => subastas.CancelarAsync(id, cancellationToken));
    }

    private async Task<ActionResult<SubastaTcgResponse>> EjecutarAsync(Func<Task<SubastaTcgResponse>> accion)
    {
        try
        {
            return Ok(await accion());
        }
        catch (BusinessRuleException ex)
        {
            return RespuestaRegla(ex);
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "No se pudo completar la operación."
                    : ex.Message
            });
        }
    }

    private static ActionResult<SubastaTcgResponse> RespuestaRegla(BusinessRuleException ex) =>
        ex.StatusCode == StatusCodes.Status404NotFound
            ? new NotFoundObjectResult(new { message = ex.Message })
            : new BadRequestObjectResult(new { message = ex.Message });
}
