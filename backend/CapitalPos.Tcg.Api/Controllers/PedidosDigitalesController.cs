using CapitalPos.Tcg.Api.Application.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pedidos-digitales")]
public sealed class PedidosDigitalesController(PedidosDigitalesService pedidos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PedidoDigitalResponse>>> Listar(
        [FromQuery] EstadoPedidoDigital? estado,
        [FromQuery] CanalPedidoDigital? canalPedido,
        [FromQuery] Guid? sedeId,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken cancellationToken)
    {
        var items = await pedidos.ListarAsync(
            estado,
            canalPedido,
            sedeId,
            fechaDesde,
            fechaHasta,
            cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PedidoDigitalResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var pedido = await pedidos.ObtenerAsync(id, cancellationToken);
        return pedido is null ? NotFound() : Ok(pedido);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PedidoDigitalResponse>> Crear(
        [FromBody] CrearPedidoDigitalRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await pedidos.CrearAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:guid}/estado")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PedidoDigitalResponse>> CambiarEstado(
        Guid id,
        [FromBody] CambiarEstadoPedidoRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await pedidos.CambiarEstadoAsync(id, request.Estado, request.Observacion, cancellationToken);
        return Ok(actualizado);
    }

    [HttpPost("estado-lote")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<IReadOnlyList<PedidoDigitalResponse>>> CambiarEstadoLote(
        [FromBody] CambiarEstadoLoteRequest request,
        CancellationToken cancellationToken)
    {
        var actualizados = await pedidos.CambiarEstadoLoteAsync(request, cancellationToken);
        return Ok(actualizados);
    }

    [HttpPost("comprobante-consolidado")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ComprobanteConsolidadoResponse>> EmitirComprobanteConsolidado(
        [FromBody] EmitirComprobanteConsolidadoRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await pedidos.EmitirComprobanteConsolidadoAsync(request, cancellationToken);
        return Ok(resultado);
    }

    [HttpPut("{id:guid}/notificacion")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PedidoDigitalResponse>> ActualizarNotificacion(
        Guid id,
        [FromBody] ActualizarNotificacionPedidoRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await pedidos.ActualizarNotificacionAsync(id, request.Notificado, cancellationToken);
        return Ok(actualizado);
    }

    [HttpPost("notificacion-lote")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<IReadOnlyList<PedidoDigitalResponse>>> ActualizarNotificacionLote(
        [FromBody] ActualizarNotificacionLoteRequest request,
        CancellationToken cancellationToken)
    {
        var actualizados = await pedidos.ActualizarNotificacionLoteAsync(request, cancellationToken);
        return Ok(actualizados);
    }

    [HttpPost("{id:guid}/cancelar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PedidoDigitalResponse>> Cancelar(
        Guid id,
        [FromBody] CambiarEstadoPedidoRequest? request,
        CancellationToken cancellationToken)
    {
        var cancelado = await pedidos.CancelarAsync(id, request?.Observacion, cancellationToken);
        return Ok(cancelado);
    }

    [HttpPost("{id:guid}/convertir-venta")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<ConversionVentaResponse>> ConvertirVenta(
        Guid id,
        [FromBody] ConvertirVentaRequest? request,
        CancellationToken cancellationToken)
    {
        var conversion = await pedidos.ConvertirVentaAsync(id, request ?? new ConvertirVentaRequest(), cancellationToken);
        return Ok(conversion);
    }
}
