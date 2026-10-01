using CapitalPos.Tcg.Api.Application.Izipay;
using CapitalPos.Tcg.Api.Application.Pagos;
using CapitalPos.Tcg.Api.Contracts.Izipay;
using CapitalPos.Tcg.Api.Contracts.Pagos;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pagos")]
public sealed class PagosController(PagosService pagos, IzipayIpnService izipay) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PagoResponse>>> Listar(
        [FromQuery] EstadoPago? estado,
        [FromQuery] OrigenPago? origen,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        [FromQuery] Guid? pedidoDigitalId,
        CancellationToken cancellationToken)
    {
        var items = await pagos.ListarAsync(
            estado, origen, fechaDesde, fechaHasta, pedidoDigitalId, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PagoResponse>> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var pago = await pagos.ObtenerAsync(id, cancellationToken);
        return pago is null ? NotFound() : Ok(pago);
    }

    [HttpGet("izipay/config")]
    public async Task<ActionResult<IntegracionIzipayResponse>> ObtenerIzipay(CancellationToken cancellationToken)
    {
        var config = await izipay.ObtenerConfigAsync(cancellationToken);
        return config is null ? NotFound() : Ok(config);
    }

    [HttpPut("izipay/config")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<IntegracionIzipayResponse>> GuardarIzipay(
        [FromBody] GuardarIntegracionIzipayRequest request,
        CancellationToken cancellationToken)
    {
        var config = await izipay.GuardarConfigAsync(request, cancellationToken);
        return Ok(config);
    }

    [HttpPost]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PagoResponse>> Registrar(
        [FromBody] RegistrarPagoRequest request,
        CancellationToken cancellationToken)
    {
        var creado = await pagos.RegistrarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id = creado.Id }, creado);
    }

    [HttpPost("lote")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<IReadOnlyList<PagoResponse>>> RegistrarLote(
        [FromBody] RegistrarPagoLoteRequest request,
        CancellationToken cancellationToken)
    {
        var creados = await pagos.RegistrarLoteAsync(request, cancellationToken);
        return Ok(creados);
    }

    [AllowAnonymous]
    [HttpPost("izipay/ipn/{empresaId:guid}")]
    public async Task<IActionResult> IzipayIpn(Guid empresaId, CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            return StatusCode(StatusCodes.Status401Unauthorized);
        }

        var form = IzipayIpnForm.From(await Request.ReadFormAsync(cancellationToken));
        var resultado = await izipay.ProcesarAsync(empresaId, form, cancellationToken);
        return StatusCode(resultado.StatusCode);
    }

    [HttpPost("{id:guid}/asociar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PagoResponse>> Asociar(
        Guid id,
        [FromBody] AsociarPagoRequest request,
        CancellationToken cancellationToken)
    {
        var actualizado = await pagos.AsociarAsync(id, request.PedidoDigitalId, cancellationToken);
        return Ok(actualizado);
    }

    [HttpPost("{id:guid}/confirmar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PagoResponse>> Confirmar(Guid id, CancellationToken cancellationToken)
    {
        var confirmado = await pagos.ConfirmarAsync(id, cancellationToken);
        return Ok(confirmado);
    }

    [HttpPost("{id:guid}/rechazar")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PagoResponse>> Rechazar(
        Guid id,
        [FromBody] RechazarPagoRequest? request,
        CancellationToken cancellationToken)
    {
        var rechazado = await pagos.RechazarAsync(id, request?.Observacion, cancellationToken);
        return Ok(rechazado);
    }

    /// <summary>
    /// Soft-anulación contable: el pago queda en estado ANULADO (no se borra).
    /// Si el pedido quedó solo en Pagado y deja de cubrirse, vuelve a Pendiente de pago.
    /// </summary>
    [HttpPost("{id:guid}/anular")]
    [RequiresPermission(Permiso.OperarVentas)]
    public async Task<ActionResult<PagoResponse>> Anular(
        Guid id,
        [FromBody] AnularPagoRequest? request,
        CancellationToken cancellationToken)
    {
        var anulado = await pagos.AnularAsync(id, request?.Observacion, cancellationToken);
        return Ok(anulado);
    }
}
