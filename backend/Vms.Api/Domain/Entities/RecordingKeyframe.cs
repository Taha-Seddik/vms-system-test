namespace Vms.Api.Domain.Entities;

public sealed class RecordingKeyframe
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording Recording { get; set; } = null!;

    public int TimestampSeconds { get; set; }

    public required string FileName { get; set; }
}
