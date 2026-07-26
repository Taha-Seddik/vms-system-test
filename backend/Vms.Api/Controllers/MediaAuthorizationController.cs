using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/media/authorize")]
public sealed class MediaAuthorizationController(
    MediaAuthorizationService mediaAuthorization) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> AuthorizeMedia(
        MediaAuthorizationRequest request,
        CancellationToken cancellationToken) =>
        await mediaAuthorization.AuthorizeAsync(request, cancellationToken)
            ? NoContent()
            : Unauthorized();
}
