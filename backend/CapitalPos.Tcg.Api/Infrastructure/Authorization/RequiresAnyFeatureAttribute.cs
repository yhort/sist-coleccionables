using CapitalPos.Tcg.Api.Domain.Tenancy;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

/// <summary>
/// Exige que el tenant activo tenga habilitado al menos uno de los features indicados.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresAnyFeatureAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequiresAnyFeatureAttribute(params TenantFeature[] features)
    {
        if (features is null || features.Length == 0)
        {
            throw new ArgumentException("Debe indicar al menos un feature.", nameof(features));
        }

        Features = features;
    }

    public IReadOnlyList<TenantFeature> Features { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        if (!currentUser.IsAuthenticated)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantProvider>();
        if (tenant.EmpresaId == Guid.Empty)
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var empresa = await db.Empresas.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == tenant.EmpresaId, context.HttpContext.RequestAborted);

        if (empresa is null || !empresa.Activa)
        {
            context.Result = new ObjectResult(new
            {
                title = "Feature no habilitado",
                detail = "Empresa no disponible.",
                features = Features.Select(f => f.ToString()).ToArray()
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        if (Features.Any(f => empresa.Settings.IsEnabled(f)))
        {
            return;
        }

        context.Result = new ObjectResult(new
        {
            title = "Feature no habilitado",
            detail = $"El tenant necesita uno de: {string.Join(", ", Features)}.",
            features = Features.Select(f => f.ToString()).ToArray()
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
