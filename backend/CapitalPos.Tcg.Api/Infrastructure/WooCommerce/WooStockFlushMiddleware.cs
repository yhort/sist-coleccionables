using CapitalPos.Tcg.Api.Application.WooCommerce;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

/// <summary>
/// Publica CantidadLibre a WooCommerce al terminar un request exitoso,
/// cubriendo venta, ajuste, apertura, reserva y liberación.
/// </summary>
public sealed class WooStockFlushMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, WooStockPublisher publisher)
    {
        await next(context);

        if (context.Response.StatusCode >= 400)
        {
            return;
        }

        await publisher.FlushAsync(context.RequestAborted);
    }
}
