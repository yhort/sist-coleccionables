using CapitalPos.Tcg.Api.Domain.Tenancy;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Infrastructure.Authorization;

/// <summary>
/// Exige que el tenant activo tenga habilitado el feature indicado.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresFeatureAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequiresFeatureAttribute(TenantFeature feature)
    {
        Feature = feature;
    }

    public TenantFeature Feature { get; }

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

        if (empresa is null || !empresa.Activa || !empresa.Settings.IsEnabled(Feature))
        {
            context.Result = new ObjectResult(new
            {
                title = "Feature no habilitado",
                detail = $"El tenant no tiene habilitado {Feature}.",
                feature = Feature.ToString()
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
