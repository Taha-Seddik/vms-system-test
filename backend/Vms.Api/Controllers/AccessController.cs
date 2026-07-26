using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;

namespace Vms.Api.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController : ControllerBase
{
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    [HttpGet("admin")]
    public IActionResult Administrator() =>
        Ok(new { message = "Administrator access granted." });

    [Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
    [HttpGet("operator")]
    public IActionResult Operator() =>
        Ok(new { message = "Operator access granted." });
}
