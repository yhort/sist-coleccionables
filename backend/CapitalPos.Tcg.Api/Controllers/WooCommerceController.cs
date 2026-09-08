using System.Text.Json;
using CapitalPos.Tcg.Api.Application.WooCommerce;
using CapitalPos.Tcg.Api.Contracts.WooCommerce;
using CapitalPos.Tcg.Api.Infrastructure.Authorization;
using CapitalPos.Tcg.Api.Infrastructure.WooCommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/woocommerce")]
public sealed class WooCommerceController(
    WooCommerceSyncService sync,
    WooCommercePedidoImportService pedidosIn) : ControllerBase
{
    [HttpGet("config")]
    public async Task<ActionResult<WooConfigResponse>> Config(CancellationToken cancellationToken)
    {
        var config = await sync.ObtenerConfigAsync(cancellationToken);
        return config is null ? NotFound() : Ok(config);
    }

    [HttpPut("config")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooConfigResponse>> GuardarConfig(
        [FromBody] GuardarWooConfigRequest request,
        CancellationToken cancellationToken)
    {
        var config = await sync.GuardarConfigAsync(request, cancellationToken);
        return Ok(config);
    }

    [HttpPost("probar")]
    [HttpPost("config/probar")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooConfigResponse>> Probar(CancellationToken cancellationToken)
    {
        var config = await sync.ProbarConexionAsync(cancellationToken);
        return Ok(config);
    }

    [HttpPost("sync/catalogo")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooSyncCatalogoResponse>> SyncCatalogo(CancellationToken cancellationToken)
    {
        var resultado = await sync.SincronizarCatalogoAsync(cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("sync/stock")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooSyncStockResponse>> SyncStock(CancellationToken cancellationToken)
    {
        var resultado = await sync.SincronizarStockAsync(cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("sincronizar-producto/{productoId:guid}")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooSyncProductoResponse>> SincronizarProducto(
        Guid productoId,
        CancellationToken cancellationToken)
    {
        var resultado = await sync.SincronizarProductoAsync(productoId, cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("sync/pedidos")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooImportPedidosResponse>> SyncPedidos(CancellationToken cancellationToken)
    {
        var resultado = await pedidosIn.ImportarPedidosAsync(cancellationToken);
        return Ok(resultado);
    }

    [HttpGet("mapeos")]
    public async Task<ActionResult<IReadOnlyList<WooMapeoResponse>>> Mapeos(CancellationToken cancellationToken)
    {
        var mapeos = await sync.ListarMapeosAsync(cancellationToken);
        return Ok(mapeos);
    }

    [HttpPut("mapeos")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<IReadOnlyList<WooMapeoResponse>>> GuardarMapeos(
        [FromBody] IReadOnlyList<WooMapeoInput> request,
        CancellationToken cancellationToken)
    {
        var mapeos = await sync.GuardarMapeosAsync(request, cancellationToken);
        return Ok(mapeos);
    }

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<WooSyncLogResponse>>> Logs(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var logs = await sync.ListarLogsAsync(take, cancellationToken);
        return Ok(logs);
    }

    [AllowAnonymous]
    [WooWebhookRawBody]
    [HttpPost("webhooks/pedidos")]
    public async Task<ActionResult> WebhookPedido(
        CancellationToken cancellationToken,
        [FromQuery] Guid? empresaId = null)
    {
        var raw = HttpContext.Items[WooWebhookRawBodyAttribute.ItemKey] as string ?? string.Empty;
        var firma = Request.Headers["X-WC-Webhook-Signature"].FirstOrDefault();
        if (EsPingWooCommerce(raw, firma))
        {
            return Ok(new { status = "ok", tipo = "ping" });
        }

        WooWebhookPedidoRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<WooWebhookPedidoRequest>(
                raw,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Payload WooCommerce inválido",
                Detail = "El cuerpo del webhook no contiene JSON válido."
            });
        }

        if (request is null || request.Id <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Pedido WooCommerce inválido",
                Detail = "El payload debe incluir un id de pedido válido."
            });
        }

        request.EmpresaId = request.EmpresaId == Guid.Empty ? empresaId ?? Guid.Empty : request.EmpresaId;
        var resultado = await pedidosIn.RecibirWebhookPedidoAsync(request, firma, raw, cancellationToken);
        return Ok(resultado);
    }

    private bool EsPingWooCommerce(string raw, string? firma)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (string.Equals(
                Request.Headers["X-WC-Webhook-Topic"].FirstOrDefault(),
                "action.woocommerce_webhook_delivery",
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            using var json = JsonDocument.Parse(raw);
            if (json.RootElement.ValueKind != JsonValueKind.Object)
            {
                return string.IsNullOrWhiteSpace(firma);
            }

            if (json.RootElement.TryGetProperty("webhook_id", out _))
            {
                return true;
            }

            var parecePedido = json.RootElement.TryGetProperty("id", out var id)
                && id.ValueKind == JsonValueKind.Number
                && id.TryGetInt64(out var orderId)
                && orderId > 0
                && json.RootElement.TryGetProperty("line_items", out var lineas)
                && lineas.ValueKind == JsonValueKind.Array;
            return string.IsNullOrWhiteSpace(firma) && !parecePedido;
        }
        catch (JsonException)
        {
            return string.IsNullOrWhiteSpace(firma);
        }
    }

    [HttpPost("webhooks/simular")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooImportPedidosResponse>> SimularWebhook(CancellationToken cancellationToken)
    {
        var resultado = await pedidosIn.SimularWebhookPedidoAsync(cancellationToken);
        return Ok(resultado);
    }

    [HttpPost("webhooks/reintentar")]
    [RequiresPermission(Permiso.OperarIntegraciones)]
    public async Task<ActionResult<WooImportPedidosResponse>> ReintentarWebhooks(CancellationToken cancellationToken)
    {
        var resultado = await pedidosIn.ReintentarPendientesAsync(cancellationToken);
        return Ok(resultado);
    }

    [AllowAnonymous]
    [WooWebhookRawBody]
    [HttpPost("webhooks/productos")]
    public async Task<ActionResult> WebhookProducto(
        [FromQuery] Guid empresaId,
        [FromBody] WooWebhookProductoRequest request,
        CancellationToken cancellationToken)
    {
        request.EmpresaId = request.EmpresaId == Guid.Empty ? empresaId : request.EmpresaId;
        var firma = Request.Headers["X-WC-Webhook-Signature"].FirstOrDefault();
        var raw = HttpContext.Items[WooWebhookRawBodyAttribute.ItemKey] as string ?? string.Empty;
        await sync.RecibirWebhookProductoAsync(request, firma, raw, cancellationToken);
        return Ok(new { status = "ok" });
    }
}
