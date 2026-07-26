using System.ComponentModel.DataAnnotations;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed record CameraGroupSummaryResponse(Guid Id, string Name);

public sealed record CameraGroupResponse(
    Guid Id,
    string Name,
    string? Description,
    int CameraCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AccessibleCameraResponse(
    string Id,
    string Name,
    string Location,
    string HlsUrl,
    CameraGroupSummaryResponse? Group,
    string? Resolution,
    double? FramesPerSecond,
    CameraRecordingStatus RecordingStatus,
    CameraConnectionStatus ConnectionStatus,
    bool IsEnabled,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastCheckedAt);

public sealed record ManagedCameraResponse(
    string Id,
    string Name,
    string Location,
    string RtspUrl,
    string HlsUrl,
    CameraGroupSummaryResponse? Group,
    string? Resolution,
    double? FramesPerSecond,
    CameraRecordingStatus RecordingStatus,
    CameraConnectionStatus ConnectionStatus,
    bool IsEnabled,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastCheckedAt,
    string? LastConnectionError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateCameraRequest(
    [param: Required]
    [param: RegularExpression(
        "^[a-z0-9][a-z0-9-]{0,99}$",
        ErrorMessage = "Id must contain lowercase letters, numbers, and hyphens only.")]
    string Id,
    [param: Required, StringLength(160, MinimumLength = 2)]
    string Name,
    [param: Required, StringLength(240, MinimumLength = 2)]
    string Location,
    [param: Required, StringLength(1000)]
    string RtspUrl,
    [param: Required, StringLength(300)]
    string HlsPath,
    Guid? GroupId,
    bool IsEnabled = true);

public sealed record UpdateCameraRequest(
    [param: Required, StringLength(160, MinimumLength = 2)]
    string Name,
    [param: Required, StringLength(240, MinimumLength = 2)]
    string Location,
    [param: Required, StringLength(1000)]
    string RtspUrl,
    [param: Required, StringLength(300)]
    string HlsPath,
    Guid? GroupId);

public sealed record SetCameraEnabledRequest(bool IsEnabled);

public sealed record CreateCameraGroupRequest(
    [param: Required, StringLength(120, MinimumLength = 2)]
    string Name,
    [param: StringLength(500)]
    string? Description);

public sealed record UpdateCameraGroupRequest(
    [param: Required, StringLength(120, MinimumLength = 2)]
    string Name,
    [param: StringLength(500)]
    string? Description);

public sealed record CameraConnectionTestResponse(
    string CameraId,
    bool Succeeded,
    CameraConnectionStatus Status,
    DateTimeOffset CheckedAt,
    long ElapsedMilliseconds,
    string? Codec,
    string? Resolution,
    double? FramesPerSecond,
    string? Error);

public sealed class CameraMonitoringOptions
{
    public const string SectionName = "CameraMonitoring";

    public bool Enabled { get; set; } = true;

    [Range(5, 3600)]
    public int IntervalSeconds { get; set; } = 15;

    [Range(0, 300)]
    public int InitialDelaySeconds { get; set; } = 5;

    [Range(1, 60)]
    public int ProbeTimeoutSeconds { get; set; } = 5;

    [Required]
    public string FfprobeExecutable { get; set; } = "ffprobe";
}
