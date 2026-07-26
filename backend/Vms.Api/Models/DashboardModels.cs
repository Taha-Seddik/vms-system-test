using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed record CommandCenterResponse(
    DateTimeOffset GeneratedAt,
    DashboardMetricsResponse Metrics,
    StorageHealthResponse Storage,
    IReadOnlyList<DashboardCameraResponse> CameraHealth,
    IReadOnlyList<DashboardCameraResponse> OfflineCameras,
    IReadOnlyList<DashboardEventResponse> RecentEvents,
    IReadOnlyList<DashboardEventResponse> RecordingFailures,
    IReadOnlyList<DashboardEventResponse> ActiveAlarms,
    IReadOnlyList<DashboardEventResponse> RecentIncidents,
    IReadOnlyList<OperatorActivityResponse> OperatorActivity);

public sealed record DashboardMetricsResponse(
    int TotalCameras,
    int OnlineCameras,
    int OfflineCameras,
    int DisabledCameras,
    int ActiveLiveStreams,
    int ActiveRecordings,
    int ActiveUsers,
    long SystemUptimeSeconds);

public sealed record DashboardCameraResponse(
    string Id,
    string Name,
    string Location,
    string? Group,
    CameraConnectionStatus ConnectionStatus,
    CameraRecordingStatus RecordingStatus,
    bool IsEnabled,
    string? Resolution,
    double? FramesPerSecond,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastCheckedAt,
    string? LastConnectionError);

public sealed record DashboardEventResponse(
    Guid Id,
    SystemEventType Type,
    DateTimeOffset Timestamp,
    string? CameraId,
    string? CameraName,
    EventSeverity Severity,
    string Description,
    EventStatus Status);

public sealed record OperatorActivityResponse(
    Guid Id,
    Guid UserId,
    string DisplayName,
    SystemEventType Type,
    DateTimeOffset Timestamp,
    string Description);

public sealed record StorageHealthResponse(
    string Path,
    StorageHealthStatus Status,
    long TotalBytes,
    long AvailableBytes,
    long UsedBytes,
    long RecordingBytes,
    double UsedPercent,
    string? Error);

public sealed record DashboardChangedMessage(
    long Revision,
    DateTimeOffset Timestamp,
    string Reason);

[JsonConverter(typeof(JsonStringEnumConverter<StorageHealthStatus>))]
public enum StorageHealthStatus
{
    Healthy,
    Warning,
    Critical,
    Unavailable
}

public sealed class RecordingStorageOptions
{
    public const string SectionName = "RecordingStorage";

    [Required]
    public string Path { get; set; } = "storage/recordings";

    [Range(1, 99)]
    public double WarningPercent { get; set; } = 80;

    [Range(2, 100)]
    public double CriticalPercent { get; set; } = 90;

    [Range(5, 3600)]
    public int MonitorIntervalSeconds { get; set; } = 30;
}
