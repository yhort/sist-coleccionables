using CapitalPos.Tcg.Api.Application.Proveedores;
using CapitalPos.Tcg.Api.Contracts.Proveedores;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/proveedores")]
public sealed class ProveedoresController(ProveedoresService proveedores) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProveedorResponse>>> Listar(
        [FromQuery] string? q,
        [FromQuery] bool? activo,
        CancellationToken cancellationToken)
    {
        var items = await proveedores.ListarAsync(q, activo, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProveedorResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var proveedor = await proveedores.ObtenerAsync(id, cancellationToken);
        return proveedor is null ? NotFound() : Ok(proveedor);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProveedorResponse>> Crear(
        [FromBody] UpsertProveedorRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await proveedores.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProveedorResponse>> Actualizar(
        Guid id,
        [FromBody] UpsertProveedorRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await proveedores.ActualizarAsync(id, request, cancellationToken);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }
}
