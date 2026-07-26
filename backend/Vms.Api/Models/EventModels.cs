using System.ComponentModel.DataAnnotations;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed class EventQuery
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [StringLength(100)]
    public string? CameraId { get; init; }

    public SystemEventType? Type { get; init; }

    public EventSeverity? Severity { get; init; }

    public EventStatus? Status { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 100;
}

public sealed record EventResponse(
    Guid Id,
    SystemEventType Type,
    DateTimeOffset Timestamp,
    string? CameraId,
    string? CameraName,
    EventSeverity Severity,
    string Description,
    EventStatus Status,
    bool IsActiveAlarm,
    bool IsIncident);

public sealed record EventSearchResponse(
    DateTimeOffset GeneratedAt,
    int MatchingCount,
    int ActiveAlarmCount,
    int IncidentCount,
    IReadOnlyList<EventResponse> Items);
