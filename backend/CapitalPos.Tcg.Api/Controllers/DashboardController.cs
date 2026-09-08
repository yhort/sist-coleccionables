using CapitalPos.Tcg.Api.Application.Dashboard;
using CapitalPos.Tcg.Api.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<ActionResult<DashboardResumenResponse>> Resumen(CancellationToken cancellationToken)
    {
        return Ok(await dashboard.ResumenAsync(cancellationToken));
    }
}
