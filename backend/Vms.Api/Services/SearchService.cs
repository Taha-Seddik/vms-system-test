using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class SearchService(
    VmsDbContext database,
    TimeProvider timeProvider)
{
    public async Task<GlobalSearchResponse> SearchAsync(
        GlobalSearchQuery request,
        bool includeUsers,
        CancellationToken cancellationToken)
    {
        var term = request.Q?.Trim().ToLower();
        var cameraId = request.CameraId?.Trim();

        var cameraQuery = database.Cameras
            .AsNoTracking()
            .Include(item => item.Group)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            cameraQuery = cameraQuery.Where(item =>
                item.Name.ToLower().Contains(term)
                || item.Location.ToLower().Contains(term)
                || (item.Group != null
                    && item.Group.Name.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            cameraQuery = cameraQuery.Where(item => item.Id == cameraId);
        }

        if (request.CameraGroupId.HasValue)
        {
            cameraQuery = cameraQuery.Where(item =>
                item.GroupId == request.CameraGroupId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var hasConnectionStatus = Enum.TryParse<CameraConnectionStatus>(
                request.Status,
                true,
                out var connectionStatus);
            var hasRecordingStatus = Enum.TryParse<CameraRecordingStatus>(
                request.Status,
                true,
                out var recordingStatus);
            cameraQuery = hasConnectionStatus || hasRecordingStatus
                ? cameraQuery.Where(item =>
                    (hasConnectionStatus
                        && item.ConnectionStatus == connectionStatus)
                    || (hasRecordingStatus
                        && item.RecordingStatus == recordingStatus))
                : cameraQuery.Where(_ => false);
        }

        var recordingQuery = database.Recordings
            .AsNoTracking()
            .Include(item => item.Camera)
            .ThenInclude(item => item.Group)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            recordingQuery = recordingQuery.Where(item =>
                item.Camera.Name.ToLower().Contains(term)
                || item.Camera.Location.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            recordingQuery = recordingQuery.Where(item =>
                item.CameraId == cameraId);
        }

        if (request.CameraGroupId.HasValue)
        {
            recordingQuery = recordingQuery.Where(item =>
                item.Camera.GroupId == request.CameraGroupId.Value);
        }

        if (request.From.HasValue)
        {
            recordingQuery = recordingQuery.Where(item =>
                item.StartedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            recordingQuery = recordingQuery.Where(item =>
                item.StartedAt <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            recordingQuery = Enum.TryParse<RecordingState>(
                request.Status,
                true,
                out var recordingState)
                ? recordingQuery.Where(item => item.State == recordingState)
                : recordingQuery.Where(_ => false);
        }

        var eventQuery = database.SystemEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            var matchingCameraIds = database.Cameras
                .Where(item =>
                    item.Name.ToLower().Contains(term)
                    || item.Location.ToLower().Contains(term))
                .Select(item => item.Id);
            eventQuery = eventQuery.Where(item =>
                item.Description.ToLower().Contains(term)
                || (item.CameraId != null
                    && matchingCameraIds.Contains(item.CameraId)));
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            eventQuery = eventQuery.Where(item => item.CameraId == cameraId);
        }

        if (request.CameraGroupId.HasValue)
        {
            var groupCameraIds = database.Cameras
                .Where(item => item.GroupId == request.CameraGroupId.Value)
                .Select(item => item.Id);
            eventQuery = eventQuery.Where(item =>
                item.CameraId != null
                && groupCameraIds.Contains(item.CameraId));
        }

        if (request.From.HasValue)
        {
            eventQuery = eventQuery.Where(item =>
                item.Timestamp >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            eventQuery = eventQuery.Where(item =>
                item.Timestamp <= request.To.Value);
        }

        if (request.EventType.HasValue)
        {
            eventQuery = eventQuery.Where(item =>
                item.Type == request.EventType.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            eventQuery = Enum.TryParse<EventStatus>(
                request.Status,
                true,
                out var eventStatus)
                ? eventQuery.Where(item => item.Status == eventStatus)
                : eventQuery.Where(_ => false);
        }

        var cameras = await cameraQuery
            .OrderBy(item => item.Name)
            .Take(request.Take)
            .Select(item => new SearchCameraResponse(
                item.Id,
                item.Name,
                item.Location,
                item.GroupId,
                item.Group == null ? null : item.Group.Name,
                item.ConnectionStatus,
                item.RecordingStatus))
            .ToListAsync(cancellationToken);
        var recordings = await recordingQuery
            .OrderByDescending(item => item.StartedAt)
            .Take(request.Take)
            .Select(item => new SearchRecordingResponse(
                item.Id,
                item.CameraId,
                item.Camera.Name,
                item.Camera.GroupId,
                item.Camera.Group == null ? null : item.Camera.Group.Name,
                item.Mode,
                item.State,
                item.StartedAt,
                item.DurationSeconds))
            .ToListAsync(cancellationToken);
        var eventRows = await eventQuery
            .OrderByDescending(item => item.Timestamp)
            .Take(request.Take)
            .ToListAsync(cancellationToken);
        var eventCameraIds = eventRows
            .Where(item => item.CameraId is not null)
            .Select(item => item.CameraId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var eventCameraNames = await database.Cameras
            .AsNoTracking()
            .Where(item => eventCameraIds.Contains(item.Id))
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name,
                cancellationToken);
        var events = eventRows.Select(item => new SearchEventResponse(
            item.Id,
            item.Type,
            item.Timestamp,
            item.CameraId,
            item.CameraId is not null
                && eventCameraNames.TryGetValue(item.CameraId, out var cameraName)
                    ? cameraName
                    : null,
            item.Severity,
            item.Status,
            item.Description)).ToArray();

        var users = includeUsers
            ? await SearchUsersAsync(
                request,
                term,
                cameraId,
                cancellationToken)
            : [];

        return new GlobalSearchResponse(
            timeProvider.GetUtcNow(),
            cameras,
            recordings,
            events,
            users);
    }

    private async Task<IReadOnlyList<SearchUserResponse>> SearchUsersAsync(
        GlobalSearchQuery request,
        string? term,
        string? cameraId,
        CancellationToken cancellationToken)
    {
        var query = database.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(item =>
                item.UserName!.ToLower().Contains(term)
                || item.DisplayName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            query = query.Where(item => item.CameraAssignments.Any(assignment =>
                assignment.CameraId == cameraId));
        }

        if (request.CameraGroupId.HasValue)
        {
            query = query.Where(item => item.CameraAssignments.Any(assignment =>
                assignment.Camera.GroupId == request.CameraGroupId.Value));
        }

        if (request.From.HasValue)
        {
            query = query.Where(item => item.CreatedAt >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(item => item.CreatedAt <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (string.Equals(
                request.Status,
                "Enabled",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => item.IsEnabled);
            }
            else if (string.Equals(
                request.Status,
                "Disabled",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => !item.IsEnabled);
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        var users = await query
            .OrderBy(item => item.UserName)
            .Take(request.Take)
            .ToListAsync(cancellationToken);
        var ids = users.Select(item => item.Id).ToArray();
        var roleNames = await (
            from userRole in database.UserRoles.AsNoTracking()
            join role in database.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where ids.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                role.Name
            })
            .ToDictionaryAsync(
                item => item.UserId,
                item => item.Name!,
                cancellationToken);

        return users
            .Where(item => roleNames.ContainsKey(item.Id))
            .Select(item => new SearchUserResponse(
                item.Id,
                item.UserName ?? string.Empty,
                item.DisplayName,
                Enum.Parse<AppRole>(roleNames[item.Id]),
                item.IsEnabled,
                item.CreatedAt))
            .ToArray();
    }
}
