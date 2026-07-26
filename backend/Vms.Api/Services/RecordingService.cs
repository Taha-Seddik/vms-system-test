using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class RecordingService(
    VmsDbContext database,
    RecordingCoordinator coordinator)
{
    public Task<RecordingMutationResult> StartManualAsync(
        string cameraId,
        Guid userId,
        CancellationToken cancellationToken) =>
        coordinator.StartRecordingAsync(
            cameraId,
            RecordingMode.Manual,
            userId,
            cancellationToken);

    public Task<RecordingMutationResult> StartContinuousAsync(
        string cameraId,
        Guid userId,
        CancellationToken cancellationToken) =>
        coordinator.StartRecordingAsync(
            cameraId,
            RecordingMode.Continuous,
            userId,
            cancellationToken);

    public Task<RecordingMutationResult> SimulateMotionAsync(
        string cameraId,
        Guid userId,
        CancellationToken cancellationToken) =>
        coordinator.StartRecordingAsync(
            cameraId,
            RecordingMode.Event,
            userId,
            cancellationToken);

    public Task<RecordingMutationResult> StopAsync(
        string cameraId,
        CancellationToken cancellationToken) =>
        coordinator.StopRecordingAsync(cameraId, cancellationToken);

    public async Task<IReadOnlyList<RecordingResponse>> GetRecentAsync(
        string? cameraId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        RecordingMode? mode,
        RecordingState? state,
        int take,
        CancellationToken cancellationToken)
    {
        var recordings = await database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .Where(item => cameraId == null || item.CameraId == cameraId)
            .Where(item => from == null || item.StartedAt >= from)
            .Where(item => to == null || item.StartedAt <= to)
            .Where(item => mode == null || item.Mode == mode)
            .Where(item => state == null || item.State == state)
            .OrderByDescending(item => item.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return recordings.Select(ToResponse).ToArray();
    }

    public async Task<RecordingDetailsResponse?> GetDetailsAsync(
        Guid recordingId,
        RecordingKeyframeService keyframes,
        CancellationToken cancellationToken)
    {
        var recording = await database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .SingleOrDefaultAsync(
                item => item.Id == recordingId,
                cancellationToken);
        if (recording is null)
        {
            return null;
        }

        var timeline = recording.State == RecordingState.Completed
            ? await keyframes.EnsureAsync(recordingId, cancellationToken)
            : [];
        return new RecordingDetailsResponse(ToResponse(recording), timeline);
    }

    public async Task<RecordingFile?> GetRecordingFileAsync(
        Guid recordingId,
        RecordingStoragePathResolver paths,
        bool download,
        CancellationToken cancellationToken)
    {
        var recording = await database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == recordingId
                    && item.State == RecordingState.Completed,
                cancellationToken);
        if (recording is null)
        {
            return null;
        }

        var fullPath = paths.GetRecordingPath(recording.FileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        var downloadName = download
            ? BuildDownloadName(recording)
            : null;
        return new RecordingFile(fullPath, "video/mp4", downloadName);
    }

    public async Task<RecordingFile?> GetKeyframeFileAsync(
        Guid recordingId,
        Guid keyframeId,
        RecordingKeyframeService keyframes,
        RecordingStoragePathResolver paths,
        CancellationToken cancellationToken)
    {
        await keyframes.EnsureAsync(recordingId, cancellationToken);
        var keyframe = await database.RecordingKeyframes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.Id == keyframeId
                    && item.RecordingId == recordingId,
                cancellationToken);
        if (keyframe is null)
        {
            return null;
        }

        var fullPath = paths.GetKeyframePath(recordingId, keyframe.FileName);
        return File.Exists(fullPath)
            ? new RecordingFile(fullPath, "image/jpeg", null)
            : null;
    }

    public static RecordingResponse ToResponse(Recording recording) =>
        new(
            recording.Id,
            recording.CameraId,
            recording.Camera.Name,
            recording.Mode,
            recording.State,
            recording.StartedAt,
            recording.EndedAt,
            recording.DurationSeconds,
            recording.FileSizeBytes,
            recording.FailureReason,
            recording.TriggerEventId);

    private static string BuildDownloadName(Recording recording)
    {
        var rawName =
            $"{recording.CameraId}-{recording.StartedAt:yyyyMMdd-HHmmss}-{recording.Mode}.mp4";
        var invalid = Path.GetInvalidFileNameChars();
        return new string(rawName
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray());
    }
}

public sealed record RecordingFile(
    string FullPath,
    string ContentType,
    string? DownloadName);
