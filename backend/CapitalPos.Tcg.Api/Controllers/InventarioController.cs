using CapitalPos.Tcg.Api.Application.Inventario;
using CapitalPos.Tcg.Api.Contracts.Inventario;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/inventario")]
public sealed class InventarioController(InventarioService inventario) : ControllerBase
{
    [HttpGet("stock/{productoId:guid}")]
    public async Task<ActionResult<StockProductoResponse>> ObtenerStock(
        Guid productoId,
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

        var stock = await inventario.ObtenerStockAsync(productoId, sedeId, cancellationToken);
        return stock is null ? NotFound() : Ok(stock);
    }

    [HttpGet("stock")]
    public async Task<ActionResult<StockSedePagedResponse>> ListarStock(
        [FromQuery] Guid sedeId,
        [FromQuery] string? q,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
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

        var items = await inventario.ListarStockPorSedeAsync(sedeId, q, page, pageSize, cancellationToken);
        return Ok(items);
    }

    [HttpPut("ajustar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<StockProductoResponse>> Ajustar(
        [FromBody] AjustarStockRequest request,
        CancellationToken cancellationToken)
    {
        var stock = await inventario.AjustarAsync(request, cancellationToken);
        return Ok(stock);
    }

    [HttpGet("kardex")]
    public async Task<ActionResult<IReadOnlyList<KardexMovimientoResponse>>> Kardex(
        [FromQuery] Guid? productoId,
        [FromQuery] Guid? sedeId,
        [FromQuery] DateTimeOffset? desde,
        [FromQuery] DateTimeOffset? hasta,
        [FromQuery] TipoMovimientoInventario? tipoMovimiento,
        CancellationToken cancellationToken)
    {
        var movimientos = await inventario.ListarKardexAsync(
            productoId,
            sedeId,
            desde,
            hasta,
            tipoMovimiento,
            cancellationToken);
        return Ok(movimientos);
    }

    [HttpPost("compuestos/{id:guid}/armar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public ActionResult Armar(Guid id) => CompuestoNoImplementado();

    [HttpPost("compuestos/{id:guid}/desarmar")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public ActionResult Desarmar(Guid id) => CompuestoNoImplementado();

    [HttpGet("compras")]
    public async Task<ActionResult<IReadOnlyList<CompraResponse>>> ListarCompras(
        [FromQuery] Guid? sedeId,
        [FromQuery] Guid? proveedorId,
        CancellationToken cancellationToken)
    {
        var items = await inventario.ListarComprasAsync(sedeId, proveedorId, cancellationToken);
        return Ok(items);
    }

    [HttpPost("compras")]
    [RequiresPermission(Permiso.OperarAlmacen)]
    public async Task<ActionResult<CompraResponse>> CrearCompra(
        [FromBody] CrearCompraRequest request,
        CancellationToken cancellationToken)
    {
        var compra = await inventario.CrearCompraAsync(request, cancellationToken);
        return Ok(compra);
    }

    private ObjectResult CompuestoNoImplementado() => StatusCode(
        StatusCodes.Status501NotImplemented,
        new ProblemDetails
        {
            Status = StatusCodes.Status501NotImplemented,
            Title = "No implementado",
            Detail = "Armar/desarmar compuestos se implementa en Sprint 1.b junto al BOM."
        });

}
