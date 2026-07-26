using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
[Route("api/command-center")]
public sealed class CommandCenterController(
    CommandCenterService commandCenter) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CommandCenterResponse>> Get(
        CancellationToken cancellationToken) =>
        Ok(await commandCenter.GetAsync(cancellationToken));
}
