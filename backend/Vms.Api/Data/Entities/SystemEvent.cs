using Vms.Api.Domain;

namespace Vms.Api.Data.Entities;

public sealed class SystemEvent
{
    public Guid Id { get; set; }

    public SystemEventType Type { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public Guid? UserId { get; set; }

    public string? CameraId { get; set; }

    public EventSeverity Severity { get; set; }

    public required string Description { get; set; }

    public EventStatus Status { get; set; }
}

