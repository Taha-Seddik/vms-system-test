namespace Vms.Api.Domain.Entities;

public sealed class CameraGroup
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Camera> Cameras { get; set; } = [];
}
