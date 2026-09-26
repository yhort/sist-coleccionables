using CapitalPos.Tcg.Api.Application.Sedes;
using CapitalPos.Tcg.Api.Contracts.Sedes;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sedes")]
public sealed class SedesController(SedesService sedes) : ControllerBase
{
    /// <summary>Por defecto solo sedes/almacenes activos (uso en selectores operativos).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SedeResponse>>> Listar(
        [FromQuery] bool? activa,
        CancellationToken cancellationToken)
    {
        var items = await sedes.ListarAsync(activa ?? true, cancellationToken);
        return Ok(items);
    }

    /// <summary>Listado de gestión (incluye inactivas). Usar activa=null vía query explícita.</summary>
    [HttpGet("gestion")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<IReadOnlyList<SedeResponse>>> ListarGestion(
        [FromQuery] bool? activa,
        CancellationToken cancellationToken)
    {
        var items = await sedes.ListarAsync(activa, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SedeResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var sede = await sedes.ObtenerAsync(id, cancellationToken);
        return sede is null ? NotFound() : Ok(sede);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<SedeResponse>> Crear(
        [FromBody] UpsertSedeRequest request,
        CancellationToken cancellationToken)
    {
        var creada = await sedes.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creada.Id }, creada);
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<SedeResponse>> Actualizar(
        Guid id,
        [FromBody] UpsertSedeRequest request,
        CancellationToken cancellationToken)
    {
        var actualizada = await sedes.ActualizarAsync(id, request, cancellationToken);
        return actualizada is null ? NotFound() : Ok(actualizada);
    }

    /// <summary>
    /// Soft delete si hay dependencias; borrado físico si la sede/almacén está limpia.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<EliminarSedeResponse>> Eliminar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await sedes.EliminarODesactivarAsync(id, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpPost("{id:guid}/desactivar")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<EliminarSedeResponse>> Desactivar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await sedes.EliminarODesactivarAsync(id, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpPost("{id:guid}/reactivar")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<SedeResponse>> Reactivar(Guid id, CancellationToken cancellationToken)
    {
        var sede = await sedes.ReactivarAsync(id, cancellationToken);
        return sede is null ? NotFound() : Ok(sede);
    }
}
