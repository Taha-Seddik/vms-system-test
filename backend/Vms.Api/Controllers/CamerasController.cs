using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/cameras")]
public sealed class CamerasController(
    CameraAccessService cameraAccess,
    CameraManagementService cameraManagement,
    CameraHealthService cameraHealth) : ControllerBase
{
    [HttpGet]
    [HttpGet("accessible")]
    public async Task<ActionResult<IReadOnlyList<AccessibleCameraResponse>>> GetAccessible(
        CancellationToken cancellationToken) =>
        Ok(await cameraAccess.GetAccessibleAsync(
            User.GetRequiredUserId(),
            User.GetRequiredRole(),
            cancellationToken));

    [HttpGet("manage")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<IReadOnlyList<ManagedCameraResponse>>> GetManaged(
        CancellationToken cancellationToken) =>
        Ok(await cameraManagement.GetAllAsync(cancellationToken));

    [HttpGet("manage/{id}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<ManagedCameraResponse>> GetManagedById(
        string id,
        CancellationToken cancellationToken)
    {
        var camera = await cameraManagement.GetAsync(id, cancellationToken);
        return camera is null ? NotFound() : Ok(camera);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<ManagedCameraResponse>> Create(
        CreateCameraRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cameraManagement.CreateAsync(request, cancellationToken);
        if (result.ErrorType != CameraMutationError.None)
        {
            return MutationProblem(result.ErrorType, result.Error);
        }

        return CreatedAtAction(
            nameof(GetManagedById),
            new { id = result.Camera!.Id },
            result.Camera);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<ManagedCameraResponse>> Update(
        string id,
        UpdateCameraRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cameraManagement.UpdateAsync(
            id,
            request,
            cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? Ok(result.Camera)
            : MutationProblem(result.ErrorType, result.Error);
    }

    [HttpPatch("{id}/enabled")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<ManagedCameraResponse>> SetEnabled(
        string id,
        SetCameraEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cameraManagement.SetEnabledAsync(
            id,
            request.IsEnabled,
            cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? Ok(result.Camera)
            : MutationProblem(result.ErrorType, result.Error);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> Delete(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await cameraManagement.DeleteAsync(id, cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? NoContent()
            : MutationProblem(result.ErrorType, result.Error);
    }

    [HttpPost("{id}/test-connection")]
    [Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
    public async Task<ActionResult<CameraConnectionTestResponse>> TestConnection(
        string id,
        CancellationToken cancellationToken)
    {
        var result = await cameraHealth.TestAsync(id, cancellationToken);
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Camera '{id}' was not found.")
            : Ok(result);
    }

    private ObjectResult MutationProblem(
        CameraMutationError errorType,
        string? detail) =>
        Problem(
            statusCode: errorType switch
            {
                CameraMutationError.NotFound => StatusCodes.Status404NotFound,
                CameraMutationError.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            },
            detail: detail);
}
