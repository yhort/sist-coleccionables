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
        CancellationToken cancellationToken)
    {
        var items = await clientes.ListarAsync(q, cancellationToken);
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
}
