using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class EventService(
    VmsDbContext database,
    DashboardUpdatePublisher dashboardUpdates,
    TimeProvider timeProvider)
{
    private static readonly SystemEventType[] AuthenticationEventTypes =
    [
        SystemEventType.UserLogin,
        SystemEventType.UserLogout
    ];

    public async Task<EventSearchResponse> SearchAsync(
        EventQuery request,
        CancellationToken cancellationToken)
    {
        var query = ApplyFilters(
            database.SystemEvents.AsNoTracking(),
            request);
        var matchingCount = await query.CountAsync(cancellationToken);
        var events = await query
            .OrderByDescending(item => item.Timestamp)
            .Take(request.Take)
            .ToListAsync(cancellationToken);

        var activeAlarmCount = await database.SystemEvents
            .AsNoTracking()
            .CountAsync(
                item => item.Status == EventStatus.Open
                    && (item.Severity == EventSeverity.Warning
                        || item.Severity == EventSeverity.Critical),
                cancellationToken);
        var incidentCount = await database.SystemEvents
            .AsNoTracking()
            .CountAsync(
                item => !AuthenticationEventTypes.Contains(item.Type),
                cancellationToken);

        return new EventSearchResponse(
            timeProvider.GetUtcNow(),
            matchingCount,
            activeAlarmCount,
            incidentCount,
            await ToResponsesAsync(events, cancellationToken));
    }

    public async Task<EventResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await database.SystemEvents
            .AsNoTracking()
            .SingleOrDefaultAsync(
                systemEvent => systemEvent.Id == id,
                cancellationToken);
        if (item is null)
        {
            return null;
        }

        return (await ToResponsesAsync([item], cancellationToken))[0];
    }

    public async Task<EventResponse?> CloseAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await database.SystemEvents.SingleOrDefaultAsync(
            systemEvent => systemEvent.Id == id,
            cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.Status == EventStatus.Open)
        {
            item.Status = EventStatus.Closed;
            await database.SaveChangesAsync(cancellationToken);
            await dashboardUpdates.PublishAsync(
                "event-closed",
                cancellationToken);
        }

        return (await ToResponsesAsync([item], cancellationToken))[0];
    }

    public static bool IsIncident(SystemEventType type) =>
        !AuthenticationEventTypes.Contains(type);

    private static IQueryable<SystemEvent> ApplyFilters(
        IQueryable<SystemEvent> query,
        EventQuery request)
    {
        if (request.From.HasValue)
        {
            query = query.Where(item =>
                item.Timestamp >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(item =>
                item.Timestamp <= request.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.CameraId))
        {
            query = query.Where(item =>
                item.CameraId == request.CameraId.Trim());
        }

        if (request.Type.HasValue)
        {
            query = query.Where(item => item.Type == request.Type.Value);
        }

        if (request.Severity.HasValue)
        {
            query = query.Where(item =>
                item.Severity == request.Severity.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(item => item.Status == request.Status.Value);
        }

        return query;
    }

    private async Task<IReadOnlyList<EventResponse>> ToResponsesAsync(
        IReadOnlyList<SystemEvent> events,
        CancellationToken cancellationToken)
    {
        var cameraIds = events
            .Where(item => item.CameraId is not null)
            .Select(item => item.CameraId!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var cameraNames = await database.Cameras
            .AsNoTracking()
            .Where(camera => cameraIds.Contains(camera.Id))
            .ToDictionaryAsync(
                camera => camera.Id,
                camera => camera.Name,
                cancellationToken);

        return events.Select(item => new EventResponse(
            item.Id,
            item.Type,
            item.Timestamp,
            item.CameraId,
            item.CameraId is not null
                && cameraNames.TryGetValue(item.CameraId, out var cameraName)
                    ? cameraName
                    : null,
            item.Severity,
            item.Description,
            item.Status,
            IsActiveAlarm(item),
            IsIncident(item.Type))).ToArray();
    }

    private static bool IsActiveAlarm(SystemEvent item) =>
        item.Status == EventStatus.Open
        && (item.Severity == EventSeverity.Warning
            || item.Severity == EventSeverity.Critical);
}
