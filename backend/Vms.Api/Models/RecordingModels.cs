using System.ComponentModel.DataAnnotations;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed record RecordingResponse(
    Guid Id,
    string CameraId,
    string CameraName,
    RecordingMode Mode,
    RecordingState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    double? DurationSeconds,
    long? FileSizeBytes,
    string? FailureReason,
    Guid? TriggerEventId);

public sealed record RecordingCommandResponse(
    string Message,
    RecordingResponse Recording);

public sealed record RecordingKeyframeResponse(
    Guid Id,
    int TimestampSeconds);

public sealed record RecordingDetailsResponse(
    RecordingResponse Recording,
    IReadOnlyList<RecordingKeyframeResponse> Keyframes);

public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

    [Required]
    public string FfmpegExecutable { get; set; } = "ffmpeg";

    [Range(3, 3600)]
    public int ContinuousSegmentSeconds { get; set; } = 10;

    [Range(3, 300)]
    public int EventDurationSeconds { get; set; } = 8;

    [Range(1, 30)]
    public int StopTimeoutSeconds { get; set; } = 5;

    [Range(0, 30)]
    public int MinimumCaptureSeconds { get; set; } = 6;

    [Range(30, 60)]
    public int KeyframeIntervalSeconds { get; set; } = 30;
}
