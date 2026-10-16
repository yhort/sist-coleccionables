using CapitalPos.Tcg.Api.Application.Permisos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

/// <summary>
/// Valida la matriz L/E del módulo.
/// Sin forzarEscritura: GET/HEAD/OPTIONS → Lectura; POST/PUT/PATCH/DELETE → Escritura.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresModulePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequiresModulePermissionAttribute(string modulo)
    {
        Modulo = modulo;
        ForzarEscritura = false;
        InferirPorHttp = true;
    }

    public RequiresModulePermissionAttribute(string modulo, bool exigirEscritura)
    {
        Modulo = modulo;
        ForzarEscritura = exigirEscritura;
        InferirPorHttp = false;
    }

    public string Modulo { get; }
    public bool ForzarEscritura { get; }
    public bool InferirPorHttp { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated || currentUser.Rol is null)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var method = context.HttpContext.Request.Method;
        var requiereEscritura = InferirPorHttp ? !EsLecturaHttp(method) : ForzarEscritura;
        var permisos = context.HttpContext.RequestServices.GetRequiredService<PermisosModuloService>();
        var permitido = await permisos.TieneAsync(
            currentUser.Rol.Value,
            Modulo,
            requiereEscritura,
            context.HttpContext.RequestAborted);

        if (!permitido)
        {
            context.Result = new ForbidResult();
        }
    }

    private static bool EsLecturaHttp(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method);
}
