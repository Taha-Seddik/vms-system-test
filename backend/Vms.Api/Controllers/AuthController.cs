using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthenticationService authentication) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["credentials"] = ["Username and password are required."]
                }));
        }

        var result = await authentication.LoginAsync(request, cancellationToken);

        return result.Failure switch
        {
            LoginFailure.None => Ok(result.Response),
            LoginFailure.ViewerHasNoAssignments => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Viewer has no camera assignments.",
                detail: "An administrator must assign at least one camera before this Viewer can sign in."),
            _ => Unauthorized()
        };
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authentication.LogoutAsync(
            User.GetRequiredUserId(),
            User.GetRequiredSessionId(),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUserResponse>> GetCurrentUser(
        CancellationToken cancellationToken) =>
        Ok(await authentication.GetCurrentUserAsync(
            User.GetRequiredUserId(),
            cancellationToken));

    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    [HttpGet("activity")]
    public async Task<ActionResult<AuthActivityResponse>> GetActivity(
        CancellationToken cancellationToken) =>
        Ok(await authentication.GetActivityAsync(cancellationToken));
}
