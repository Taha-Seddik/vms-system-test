namespace Vms.Api.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public Guid UserId { get; set; }

    public required string ActorUsername { get; set; }

    public required string Action { get; set; }

    public required string ResourceType { get; set; }

    public string? ResourceId { get; set; }

    public required string Description { get; set; }
}
