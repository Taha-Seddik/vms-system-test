using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CommandCenterService(
    VmsDbContext database,
    IStorageMetricsProvider storageMetrics,
    ApplicationUptime uptime,
    TimeProvider timeProvider)
{
    private static readonly SystemEventType[] AuthenticationEventTypes =
    [
        SystemEventType.UserLogin,
        SystemEventType.UserLogout
    ];

    public async Task<CommandCenterResponse> GetAsync(
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cameras = await database.Cameras
            .AsNoTracking()
            .Include(camera => camera.Group)
            .OrderBy(camera => camera.Name)
            .ToListAsync(cancellationToken);
        var cameraNames = cameras.ToDictionary(
            camera => camera.Id,
            camera => camera.Name,
            StringComparer.Ordinal);

        var activeUsers = await database.UserSessions
            .AsNoTracking()
            .Where(session =>
                session.RevokedAt == null
                && session.ExpiresAt > now
                && session.LastActivityAt > now.AddMinutes(-5)
                && session.User.IsEnabled)
            .Select(session => session.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var recentEvents = await database.SystemEvents
            .AsNoTracking()
            .OrderByDescending(item => item.Timestamp)
            .Take(12)
            .ToListAsync(cancellationToken);
        var recentIncidents = await database.SystemEvents
            .AsNoTracking()
            .Where(item => !AuthenticationEventTypes.Contains(item.Type))
            .OrderByDescending(item => item.Timestamp)
            .Take(8)
            .ToListAsync(cancellationToken);
        var recordingFailures = await database.SystemEvents
            .AsNoTracking()
            .Where(item => item.Type == SystemEventType.RecordingFailure)
            .OrderByDescending(item => item.Timestamp)
            .Take(8)
            .ToListAsync(cancellationToken);
        var activeAlarms = await database.SystemEvents
            .AsNoTracking()
            .Where(item =>
                item.Status == EventStatus.Open
                && (item.Severity == EventSeverity.Warning
                    || item.Severity == EventSeverity.Critical))
            .OrderByDescending(item => item.Timestamp)
            .Take(8)
            .ToListAsync(cancellationToken);
        var operatorActivity = await GetOperatorActivityAsync(cancellationToken);
        var storage = await storageMetrics.GetAsync(cancellationToken);

        var cameraHealth = cameras.Select(ToCameraResponse).ToArray();
        var offlineCameras = cameraHealth
            .Where(camera =>
                camera.ConnectionStatus == CameraConnectionStatus.Offline)
            .ToArray();

        return new CommandCenterResponse(
            now,
            new DashboardMetricsResponse(
                cameras.Count,
                cameras.Count(camera =>
                    camera.ConnectionStatus == CameraConnectionStatus.Online),
                offlineCameras.Length,
                cameras.Count(camera =>
                    camera.ConnectionStatus == CameraConnectionStatus.Disabled),
                cameras.Count(camera =>
                    camera.IsEnabled
                    && camera.ConnectionStatus == CameraConnectionStatus.Online),
                cameras.Count(camera =>
                    camera.RecordingStatus == CameraRecordingStatus.Recording),
                activeUsers,
                Math.Max(0, (long)uptime.Elapsed.TotalSeconds)),
            storage,
            cameraHealth,
            offlineCameras,
            recentEvents.Select(item =>
                ToEventResponse(item, cameraNames)).ToArray(),
            recordingFailures.Select(item =>
                ToEventResponse(item, cameraNames)).ToArray(),
            activeAlarms.Select(item =>
                ToEventResponse(item, cameraNames)).ToArray(),
            recentIncidents.Select(item =>
                ToEventResponse(item, cameraNames)).ToArray(),
            operatorActivity);
    }

    private async Task<IReadOnlyList<OperatorActivityResponse>>
        GetOperatorActivityAsync(CancellationToken cancellationToken)
    {
        var operatorRoleId = await database.Roles
            .AsNoTracking()
            .Where(role => role.NormalizedName == nameof(AppRole.Operator).ToUpper())
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!operatorRoleId.HasValue)
        {
            return [];
        }

        var operatorIds = await database.UserRoles
            .AsNoTracking()
            .Where(item => item.RoleId == operatorRoleId.Value)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        var activity = await database.AuditLogs
            .AsNoTracking()
            .Where(item =>
                operatorIds.Contains(item.UserId))
            .OrderByDescending(item => item.Timestamp)
            .Take(8)
            .ToListAsync(cancellationToken);
        var users = await database.Users
            .AsNoTracking()
            .Where(user => operatorIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        return activity
            .Where(item => users.ContainsKey(item.UserId))
            .Select(item => new OperatorActivityResponse(
                item.Id,
                item.UserId,
                users[item.UserId].DisplayName,
                item.Action switch
                {
                    "Login" => SystemEventType.UserLogin,
                    "Logout" => SystemEventType.UserLogout,
                    _ => null
                },
                item.Action,
                item.Timestamp,
                item.Description))
            .ToArray();
    }

    private static DashboardCameraResponse ToCameraResponse(Camera camera) =>
        new(
            camera.Id,
            camera.Name,
            camera.Location,
            camera.Group?.Name,
            camera.ConnectionStatus,
            camera.RecordingStatus,
            camera.IsEnabled,
            camera.ResolutionWidth.HasValue && camera.ResolutionHeight.HasValue
                ? $"{camera.ResolutionWidth}x{camera.ResolutionHeight}"
                : null,
            camera.FramesPerSecond,
            camera.LastHeartbeatAt,
            camera.LastCheckedAt,
            camera.LastConnectionError);

    private static DashboardEventResponse ToEventResponse(
        SystemEvent item,
        IReadOnlyDictionary<string, string> cameraNames) =>
        new(
            item.Id,
            item.Type,
            item.Timestamp,
            item.CameraId,
            item.CameraId is not null
                && cameraNames.TryGetValue(item.CameraId, out var name)
                    ? name
                    : null,
            item.Severity,
            item.Description,
            item.Status);
}
