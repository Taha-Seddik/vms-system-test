using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Domain;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AdministratorOnly)]
[Route("api/users")]
public sealed class UsersController(UserManagementService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ManagedUserResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] AppRole? role,
        [FromQuery] bool? isEnabled,
        [FromQuery] string? cameraId,
        CancellationToken cancellationToken) =>
        Ok(await users.GetAllAsync(
            search,
            role,
            isEnabled,
            cameraId,
            cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await users.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<ManagedUserResponse>> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.CreateAsync(request, cancellationToken);
        return result.ErrorType == UserMutationError.None
            ? CreatedAtAction(
                nameof(Get),
                new { id = result.User!.Id },
                result.User)
            : MutationProblem(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ManagedUserResponse>> Update(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await users.UpdateAsync(
            id,
            User.GetRequiredUserId(),
            request,
            cancellationToken);
        return result.ErrorType == UserMutationError.None
            ? Ok(result.User)
            : MutationProblem(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await users.DeleteAsync(
            id,
            User.GetRequiredUserId(),
            cancellationToken);
        return result.ErrorType == UserMutationError.None
            ? NoContent()
            : MutationProblem(result);
    }

    private ObjectResult MutationProblem(UserMutationResult result) =>
        Problem(
            statusCode: result.ErrorType switch
            {
                UserMutationError.NotFound => StatusCodes.Status404NotFound,
                UserMutationError.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            },
            detail: result.Error);
}
