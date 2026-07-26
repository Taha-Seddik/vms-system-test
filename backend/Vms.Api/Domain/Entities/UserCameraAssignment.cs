namespace Vms.Api.Domain.Entities;

public sealed class UserCameraAssignment
{
    public Guid UserId { get; set; }

    public required string CameraId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public AppUser User { get; set; } = null!;
}
