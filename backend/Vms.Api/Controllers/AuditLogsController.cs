using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AdministratorOnly)]
[Route("api/audit-logs")]
public sealed class AuditLogsController(AuditService audit) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditLogSearchResponse>> Search(
        [FromQuery] AuditLogQuery request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            ModelState.AddModelError(
                nameof(request.From),
                "From must be earlier than or equal to To.");
            return ValidationProblem(ModelState);
        }

        return Ok(await audit.SearchAsync(request, cancellationToken));
    }
}
