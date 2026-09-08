using CapitalPos.Tcg.Api.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Infrastructure;

public sealed class BusinessRuleExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BusinessRuleException rule)
        {
            return false;
        }

        httpContext.Response.StatusCode = rule.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = rule.StatusCode,
                Title = "Regla de negocio",
                Detail = rule.Message
            },
            cancellationToken);

        return true;
    }
}
