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
        int take,
        CancellationToken cancellationToken)
    {
        var recordings = await database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .Where(item => cameraId == null || item.CameraId == cameraId)
            .OrderByDescending(item => item.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return recordings.Select(ToResponse).ToArray();
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
}
