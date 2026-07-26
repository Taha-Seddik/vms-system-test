using Vms.Api.Domain;

namespace Vms.Api.Domain.Entities;

public sealed class Recording
{
    public Guid Id { get; set; }

    public required string CameraId { get; set; }

    public Camera Camera { get; set; } = null!;

    public RecordingMode Mode { get; set; }

    public RecordingState State { get; set; }

    public required string FileName { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public double? DurationSeconds { get; set; }

    public long? FileSizeBytes { get; set; }

    public string? FailureReason { get; set; }

    public Guid StartedByUserId { get; set; }

    public Guid? TriggerEventId { get; set; }

    public ICollection<RecordingKeyframe> Keyframes { get; set; } = [];
}
