using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/camera-groups")]
public sealed class CameraGroupsController(
    CameraGroupService cameraGroups) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CameraGroupResponse>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await cameraGroups.GetAllAsync(cancellationToken));

    [HttpPost]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<CameraGroupResponse>> Create(
        CreateCameraGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cameraGroups.CreateAsync(request, cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? Created($"/api/camera-groups/{result.Group!.Id}", result.Group)
            : MutationProblem(result.ErrorType, result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<ActionResult<CameraGroupResponse>> Update(
        Guid id,
        UpdateCameraGroupRequest request,
        CancellationToken cancellationToken)
    {
        var result = await cameraGroups.UpdateAsync(
            id,
            request,
            cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? Ok(result.Group)
            : MutationProblem(result.ErrorType, result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AppPolicies.AdministratorOnly)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await cameraGroups.DeleteAsync(id, cancellationToken);
        return result.ErrorType == CameraMutationError.None
            ? NoContent()
            : MutationProblem(result.ErrorType, result.Error);
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
