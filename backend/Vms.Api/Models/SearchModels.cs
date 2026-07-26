using System.ComponentModel.DataAnnotations;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed class GlobalSearchQuery
{
    [StringLength(200)]
    public string? Q { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    [StringLength(100)]
    public string? CameraId { get; init; }

    public Guid? CameraGroupId { get; init; }

    [StringLength(40)]
    public string? Status { get; init; }

    public SystemEventType? EventType { get; init; }

    [Range(1, 50)]
    public int Take { get; init; } = 20;
}

public sealed record SearchCameraResponse(
    string Id,
    string Name,
    string Location,
    Guid? CameraGroupId,
    string? CameraGroupName,
    CameraConnectionStatus Status,
    CameraRecordingStatus RecordingStatus);

public sealed record SearchRecordingResponse(
    Guid Id,
    string CameraId,
    string CameraName,
    Guid? CameraGroupId,
    string? CameraGroupName,
    RecordingMode Mode,
    RecordingState Status,
    DateTimeOffset StartedAt,
    double? DurationSeconds);

public sealed record SearchEventResponse(
    Guid Id,
    SystemEventType Type,
    DateTimeOffset Timestamp,
    string? CameraId,
    string? CameraName,
    EventSeverity Severity,
    EventStatus Status,
    string Description);

public sealed record SearchUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    AppRole Role,
    bool IsEnabled,
    DateTimeOffset CreatedAt);

public sealed record GlobalSearchResponse(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SearchCameraResponse> Cameras,
    IReadOnlyList<SearchRecordingResponse> Recordings,
    IReadOnlyList<SearchEventResponse> Events,
    IReadOnlyList<SearchUserResponse> Users);
