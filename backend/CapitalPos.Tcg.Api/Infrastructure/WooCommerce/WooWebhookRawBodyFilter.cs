using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapitalPos.Tcg.Api.Infrastructure.WooCommerce;

/// <summary>
/// Conserva el cuerpo crudo del webhook antes del model binder para HMAC-SHA256.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WooWebhookRawBodyAttribute : Attribute, IAsyncResourceFilter
{
    public const string ItemKey = "WooWebhookRawBody";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var raw = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        context.HttpContext.Items[ItemKey] = raw;
        await next();
    }
}
