using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CameraHealthService(
    VmsDbContext database,
    ICameraProbe cameraProbe,
    IOptions<CameraMonitoringOptions> options,
    CameraHealthCheckCoordinator coordinator,
    ILogger<CameraHealthService> logger)
{
    public async Task<CameraConnectionTestResponse?> TestAsync(
        string cameraId,
        CancellationToken cancellationToken) =>
        await CheckAsync(cameraId, allowDisabled: true, cancellationToken);

    public async Task CheckAllEnabledAsync(CancellationToken cancellationToken)
    {
        var cameraIds = await database.Cameras
            .AsNoTracking()
            .Where(camera => camera.IsEnabled)
            .OrderBy(camera => camera.Id)
            .Select(camera => camera.Id)
            .ToListAsync(cancellationToken);

        foreach (var cameraId in cameraIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CheckAsync(cameraId, allowDisabled: false, cancellationToken);
        }
    }

    private async Task<CameraConnectionTestResponse?> CheckAsync(
        string cameraId,
        bool allowDisabled,
        CancellationToken cancellationToken)
    {
        await using var lease = await coordinator.EnterAsync(cancellationToken);
        var camera = await database.Cameras.SingleOrDefaultAsync(
            item => item.Id == cameraId,
            cancellationToken);
        if (camera is null || (!allowDisabled && !camera.IsEnabled))
        {
            return null;
        }

        var previousStatus = camera.ConnectionStatus;
        var result = await cameraProbe.ProbeAsync(
            camera.RtspUrl,
            TimeSpan.FromSeconds(options.Value.ProbeTimeoutSeconds),
            cancellationToken);
        var checkedAt = DateTimeOffset.UtcNow;

        camera.LastCheckedAt = checkedAt;
        camera.UpdatedAt = checkedAt;
        camera.LastConnectionError = result.Error;

        if (result.Succeeded)
        {
            camera.ResolutionWidth = result.ResolutionWidth;
            camera.ResolutionHeight = result.ResolutionHeight;
            camera.FramesPerSecond = result.FramesPerSecond;
            camera.LastHeartbeatAt = checkedAt;
            camera.LastConnectionError = null;
        }

        var resultingStatus = camera.IsEnabled
            ? result.Succeeded
                ? CameraConnectionStatus.Online
                : CameraConnectionStatus.Offline
            : CameraConnectionStatus.Disabled;
        camera.ConnectionStatus = resultingStatus;

        if (camera.IsEnabled)
        {
            AddTransitionEvent(camera, previousStatus, resultingStatus, checkedAt);
        }

        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Camera {CameraId} connection check completed with status {Status} in {ElapsedMilliseconds} ms",
            camera.Id,
            resultingStatus,
            result.Elapsed.TotalMilliseconds);

        return new CameraConnectionTestResponse(
            camera.Id,
            result.Succeeded,
            resultingStatus,
            checkedAt,
            (long)Math.Round(result.Elapsed.TotalMilliseconds),
            result.Codec,
            result.ResolutionWidth.HasValue && result.ResolutionHeight.HasValue
                ? $"{result.ResolutionWidth}x{result.ResolutionHeight}"
                : null,
            result.FramesPerSecond,
            result.Error);
    }

    private void AddTransitionEvent(
        Camera camera,
        CameraConnectionStatus previousStatus,
        CameraConnectionStatus resultingStatus,
        DateTimeOffset timestamp)
    {
        if (resultingStatus == CameraConnectionStatus.Offline
            && previousStatus != CameraConnectionStatus.Offline)
        {
            database.SystemEvents.Add(new SystemEvent
            {
                Id = Guid.NewGuid(),
                Type = SystemEventType.CameraOffline,
                Timestamp = timestamp,
                CameraId = camera.Id,
                Severity = EventSeverity.Warning,
                Description = $"{camera.Name} is offline. {camera.LastConnectionError}",
                Status = EventStatus.Open
            });
        }
        else if (resultingStatus == CameraConnectionStatus.Online
                 && previousStatus == CameraConnectionStatus.Offline)
        {
            database.SystemEvents.Add(new SystemEvent
            {
                Id = Guid.NewGuid(),
                Type = SystemEventType.CameraReconnected,
                Timestamp = timestamp,
                CameraId = camera.Id,
                Severity = EventSeverity.Information,
                Description = $"{camera.Name} reconnected and is reporting normally.",
                Status = EventStatus.Closed
            });
        }
    }
}
