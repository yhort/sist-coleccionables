using CapitalPos.Tcg.Api.Application.Clientes;
using CapitalPos.Tcg.Api.Contracts.Clientes;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/clientes")]
public sealed class ClientesController(ClientesService clientes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteResponse>>> Listar(
        [FromQuery] string? q,
        [FromQuery] bool? activo,
        CancellationToken cancellationToken)
    {
        // activo=true/false filtra; sin param = todos (maestros). Los selectores operativos pasan activo=true.
        var items = await clientes.ListarAsync(q, activo, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClienteResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var cliente = await clientes.ObtenerAsync(id, cancellationToken);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ClienteResponse>> Crear(
        [FromBody] UpsertClienteRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await clientes.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ClienteResponse>> Actualizar(
        Guid id,
        [FromBody] UpsertClienteRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await clientes.ActualizarAsync(id, request, cancellationToken);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }

    /// <summary>Soft delete: desactiva el cliente sin borrar historial de pedidos/pagos.</summary>
    [HttpPost("{id:guid}/desactivar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ClienteResponse>> Desactivar(Guid id, CancellationToken cancellationToken)
    {
        var desactivado = await clientes.DesactivarAsync(id, cancellationToken);
        return desactivado is null ? NotFound() : Ok(desactivado);
    }

    [HttpPost("{id:guid}/reactivar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ClienteResponse>> Reactivar(Guid id, CancellationToken cancellationToken)
    {
        var reactivado = await clientes.ReactivarAsync(id, cancellationToken);
        return reactivado is null ? NotFound() : Ok(reactivado);
    }
}
