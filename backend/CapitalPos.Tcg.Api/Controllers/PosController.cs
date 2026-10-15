using CapitalPos.Tcg.Api.Application.Pos;
using CapitalPos.Tcg.Api.Contracts.Pedidos;
using CapitalPos.Tcg.Api.Contracts.Pos;
using CapitalPos.Tcg.Api.Domain.Tenancy;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

/// <summary>
/// POS mostrador: pedidos pendientes FB + cobro consolidado con emisión CPE (SUNAT).
/// </summary>
[Authorize]
[RequiresFeature(TenantFeature.PermitirPosMostrador)]
[ApiController]
[Route("api/pos")]
public sealed class PosController(PosService pos) : ControllerBase
{
    [HttpGet("estado")]
    public ActionResult<object> Estado()
    {
        return Ok(new
        {
            disponible = true,
            mensaje = "POS mostrador listo para cobro y emisión de CPE."
        });
    }

    /// <summary>
    /// Pedidos en PendientePago (artículos ganados / FB) filtrables por nombre, DNI o código.
    /// </summary>
    [HttpGet("pedidos-pendientes")]
    public async Task<ActionResult<IReadOnlyList<PedidoDigitalResponse>>> PedidosPendientes(
        [FromQuery] string? q,
        [FromQuery] Guid? sedeId,
        CancellationToken cancellationToken)
    {
        var items = await pos.ListarPedidosPendientesAsync(q, sedeId, cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Facade: valida caja → registra pagos → marca Pagado → emite CPE vía IServicioFiscal.
    /// </summary>
    [HttpPost("cobrar-y-emitir")]
    public async Task<ActionResult<CobrarYEmitirPosResponse>> CobrarYEmitir(
        [FromBody] CobrarYEmitirPosRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await pos.CobrarYEmitirAsync(request, cancellationToken);
        return Ok(resultado);
    }
}
