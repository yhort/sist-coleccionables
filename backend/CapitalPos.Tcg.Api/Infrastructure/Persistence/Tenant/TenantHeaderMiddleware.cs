using CapitalPos.Tcg.Api.Infrastructure.Authorization;

namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;

public sealed class TenantHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CapitalPos-EmpresaId";

    public async Task InvokeAsync(HttpContext context, ITenantProvider tenant, ICurrentUser currentUser)
    {
        if (ShouldSkip(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (!TryReadEmpresaId(context, out var empresaId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Tenant requerido",
                detail = $"Falta o es inválido el header {HeaderName}."
            });
            return;
        }

        if (currentUser.IsAuthenticated
            && currentUser.EmpresaId is { } jwtEmpresa
            && jwtEmpresa != empresaId)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "Tenant no autorizado",
                detail = $"El header {HeaderName} no coincide con la empresa del token."
            });
            return;
        }

        tenant.SetEmpresa(empresaId);
        await next(context);
    }

    private static bool TryReadEmpresaId(HttpContext context, out Guid empresaId)
    {
        empresaId = Guid.Empty;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            return false;
        }

        return Guid.TryParse(values.FirstOrDefault(), out empresaId) && empresaId != Guid.Empty;
    }

    private static bool ShouldSkip(PathString path) =>
        path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi")
        || path.StartsWithSegments("/api/auth")
        || path.StartsWithSegments("/api/pagos/izipay/ipn")
        || path.StartsWithSegments("/api/woocommerce/webhooks/pedidos")
        || path.StartsWithSegments("/api/woocommerce/webhooks/productos");
}
