using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
[Route("api")]
public sealed class RecordingsController(
    RecordingService recordings) : ControllerBase
{
    [HttpGet("recordings")]
    public async Task<ActionResult<IReadOnlyList<RecordingResponse>>> GetRecent(
        [FromQuery] string? cameraId,
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default) =>
        Ok(await recordings.GetRecentAsync(cameraId, take, cancellationToken));

    [HttpPost("cameras/{cameraId}/recordings/manual/start")]
    public async Task<ActionResult<RecordingCommandResponse>> StartManual(
        string cameraId,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await recordings.StartManualAsync(
                cameraId,
                User.GetRequiredUserId(),
                cancellationToken),
            "Manual recording started.");

    [HttpPost("cameras/{cameraId}/recordings/continuous/start")]
    public async Task<ActionResult<RecordingCommandResponse>> StartContinuous(
        string cameraId,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await recordings.StartContinuousAsync(
                cameraId,
                User.GetRequiredUserId(),
                cancellationToken),
            "Continuous recording started.");

    [HttpPost("cameras/{cameraId}/motion/simulate")]
    public async Task<ActionResult<RecordingCommandResponse>> SimulateMotion(
        string cameraId,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await recordings.SimulateMotionAsync(
                cameraId,
                User.GetRequiredUserId(),
                cancellationToken),
            "Motion event created and event recording started.");

    [HttpPost("cameras/{cameraId}/recordings/stop")]
    public async Task<ActionResult<RecordingCommandResponse>> Stop(
        string cameraId,
        CancellationToken cancellationToken) =>
        ToActionResult(
            await recordings.StopAsync(cameraId, cancellationToken),
            "Recording stopped.");

    private ActionResult<RecordingCommandResponse> ToActionResult(
        RecordingMutationResult result,
        string successMessage)
    {
        if (result.ErrorType == RecordingMutationError.None)
        {
            return Ok(new RecordingCommandResponse(
                successMessage,
                result.Recording!));
        }

        return Problem(
            statusCode: result.ErrorType switch
            {
                RecordingMutationError.NotFound =>
                    StatusCodes.Status404NotFound,
                RecordingMutationError.Conflict =>
                    StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            },
            detail: result.Error);
    }
}
