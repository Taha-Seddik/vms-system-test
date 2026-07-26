using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Domain;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
[Route("api/search")]
public sealed class SearchController(SearchService search) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GlobalSearchResponse>> Get(
        [FromQuery] GlobalSearchQuery request,
        CancellationToken cancellationToken)
    {
        if (request.From > request.To)
        {
            ModelState.AddModelError(
                nameof(request.From),
                "From must be earlier than or equal to To.");
            return ValidationProblem(ModelState);
        }

        return Ok(await search.SearchAsync(
            request,
            User.GetRequiredRole() == AppRole.Administrator,
            cancellationToken));
    }
}
