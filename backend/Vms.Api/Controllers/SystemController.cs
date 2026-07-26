using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Vms.Api.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class SystemController : ControllerBase
{
    [HttpGet("/")]
    public IActionResult Root() =>
        Ok(new
        {
            service = "VMS API",
            status = "ready",
            step = 4
        });

    [HttpGet("/health")]
    public IActionResult Health() =>
        Ok(new
        {
            status = "Healthy",
            service = "vms-api",
            timestamp = DateTimeOffset.UtcNow
        });

    [HttpGet("/api/system/info")]
    public IActionResult Info() =>
        Ok(new
        {
            name = "Video Management System",
            foundation = "ASP.NET Core, React, PostgreSQL, MediaMTX, FFmpeg",
            implementedStep = 4
        });
}
