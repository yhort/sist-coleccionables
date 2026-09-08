using CapitalPos.Tcg.Api.Application.Sedes;
using CapitalPos.Tcg.Api.Contracts.Sedes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalPos.Tcg.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sedes")]
public sealed class SedesController(SedesService sedes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SedeResponse>>> Listar(CancellationToken cancellationToken)
    {
        var items = await sedes.ListarAsync(cancellationToken);
        return Ok(items);
    }
}
