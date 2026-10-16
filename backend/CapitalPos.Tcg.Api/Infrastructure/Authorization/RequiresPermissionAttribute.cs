using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresPermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequiresPermissionAttribute(params Permiso[] permisos)
    {
        if (permisos is null || permisos.Length == 0)
        {
            throw new ArgumentException("Debe indicar al menos un permiso.", nameof(permisos));
        }

        Permisos = permisos;
    }

    public IReadOnlyList<Permiso> Permisos { get; }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }

        if (currentUser.Rol is null ||
            !Permisos.Any(permiso => PermisosPorRol.Tiene(currentUser.Rol.Value, permiso)))
        {
            context.Result = new ForbidResult();
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}
