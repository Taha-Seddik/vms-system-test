using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cameras")]
public sealed class CamerasController(CameraAccessService cameraAccess) : ControllerBase
{
    [HttpGet("accessible")]
    public async Task<ActionResult<IReadOnlyList<AccessibleCameraResponse>>> GetAccessible(
        CancellationToken cancellationToken) =>
        Ok(await cameraAccess.GetAccessibleAsync(
            User.GetRequiredUserId(),
            User.GetRequiredRole(),
            cancellationToken));
}
