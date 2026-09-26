using CapitalPos.Tcg.Api.Application.Productos;
using CapitalPos.Tcg.Api.Contracts.Common;
using CapitalPos.Tcg.Api.Contracts.Productos;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/productos-tcg")]
public sealed class ProductosTcgController(ProductosTcgService productos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductoTcgResponse>>> Listar(
        [FromQuery] TipoProducto? tipoProducto,
        [FromQuery] string? juego,
        [FromQuery] string? q,
        [FromQuery] bool? activo,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var items = await productos.ListarAsync(
            tipoProducto,
            juego,
            q,
            activo,
            page,
            pageSize,
            cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductoTcgResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var producto = await productos.ObtenerAsync(id, cancellationToken);
        return producto is null ? NotFound() : Ok(producto);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> Crear(
        [FromBody] UpsertProductoTcgRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await productos.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPost("variantes")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> CrearVariante(
        [FromBody] CrearVarianteProductoCartaRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await productos.CrearVarianteDesdeCatalogoAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> Actualizar(
        Guid id,
        [FromBody] UpsertProductoTcgRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await productos.ActualizarAsync(id, request, cancellationToken);
        return actualizado is null ? NotFound() : Ok(actualizado);
    }

    [HttpPatch("{id:guid}/activar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> Activar(Guid id, CancellationToken cancellationToken)
    {
        var producto = await productos.CambiarActivoAsync(id, activo: true, cancellationToken);
        return producto is null ? NotFound() : Ok(producto);
    }

    [HttpPatch("{id:guid}/desactivar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> Desactivar(Guid id, CancellationToken cancellationToken)
    {
        var producto = await productos.CambiarActivoAsync(id, activo: false, cancellationToken);
        return producto is null ? NotFound() : Ok(producto);
    }

    /// <summary>
    /// Soft delete si hay historial/kardex; borrado físico solo sin ID Woo ni dependencias.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<EliminarProductoTcgResponse>> Eliminar(
        Guid id,
        CancellationToken cancellationToken)
    {
        var resultado = await productos.EliminarODesactivarAsync(id, cancellationToken);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    /// <summary>
    /// Limpia el ID externo WooCommerce y deja el mapeo en NO_MAPEADO.
    /// </summary>
    [HttpPost("{id:guid}/desvincular-woocommerce")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<ProductoTcgResponse>> DesvincularWooCommerce(
        Guid id,
        CancellationToken cancellationToken)
    {
        var producto = await productos.DesvincularWooCommerceAsync(id, cancellationToken);
        return producto is null ? NotFound() : Ok(producto);
    }

    [HttpGet("{id:guid}/componentes")]
    public ActionResult ListarComponentes(Guid id) => BomNoImplementado();

    [HttpPost("{id:guid}/componentes")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public ActionResult AgregarComponente(Guid id) => BomNoImplementado();

    [HttpDelete("{id:guid}/componentes/{componenteId:guid}")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public ActionResult QuitarComponente(Guid id, Guid componenteId) => BomNoImplementado();

    private ObjectResult BomNoImplementado() => StatusCode(
        StatusCodes.Status501NotImplemented,
        new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "No implementado",
            Detail = "El BOM de compuestos (ProductoComponente) se implementa en Sprint 1.b."
        });
}
