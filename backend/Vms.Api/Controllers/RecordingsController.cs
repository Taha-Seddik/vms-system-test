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
    RecordingService recordings,
    RecordingKeyframeService keyframes,
    RecordingStoragePathResolver paths) : ControllerBase
{
    [HttpGet("recordings")]
    public async Task<ActionResult<IReadOnlyList<RecordingResponse>>> GetRecent(
        [FromQuery] string? cameraId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Vms.Api.Domain.RecordingMode? mode,
        [FromQuery] Vms.Api.Domain.RecordingState? state,
        [FromQuery] int take = 25,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The from date must be before the to date.");
        }

        return Ok(await recordings.GetRecentAsync(
            cameraId,
            from,
            to,
            mode,
            state,
            take,
            cancellationToken));
    }

    [HttpGet("recordings/{recordingId:guid}")]
    public async Task<ActionResult<RecordingDetailsResponse>> GetDetails(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var result = await recordings.GetDetailsAsync(
            recordingId,
            keyframes,
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("recordings/{recordingId:guid}/media")]
    public async Task<IActionResult> GetMedia(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var file = await recordings.GetRecordingFileAsync(
            recordingId,
            paths,
            download: false,
            cancellationToken);
        return file is null
            ? NotFound()
            : PhysicalFile(
                file.FullPath,
                file.ContentType,
                enableRangeProcessing: true);
    }

    [HttpGet("recordings/{recordingId:guid}/download")]
    public async Task<IActionResult> Download(
        Guid recordingId,
        CancellationToken cancellationToken)
    {
        var file = await recordings.GetRecordingFileAsync(
            recordingId,
            paths,
            download: true,
            cancellationToken);
        return file is null
            ? NotFound()
            : PhysicalFile(
                file.FullPath,
                file.ContentType,
                file.DownloadName,
                enableRangeProcessing: true);
    }

    [HttpGet(
        "recordings/{recordingId:guid}/keyframes/{keyframeId:guid}")]
    public async Task<IActionResult> GetKeyframe(
        Guid recordingId,
        Guid keyframeId,
        CancellationToken cancellationToken)
    {
        var file = await recordings.GetKeyframeFileAsync(
            recordingId,
            keyframeId,
            keyframes,
            paths,
            cancellationToken);
        return file is null
            ? NotFound()
            : PhysicalFile(file.FullPath, file.ContentType);
    }

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
