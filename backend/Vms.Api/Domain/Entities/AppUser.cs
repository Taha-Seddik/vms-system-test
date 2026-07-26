using Vms.Api.Domain;

namespace Vms.Api.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string DisplayName { get; set; }

    public required string PasswordHash { get; set; }

    public AppRole Role { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    public ICollection<UserCameraAssignment> CameraAssignments { get; set; } = [];

    public ICollection<UserSession> Sessions { get; set; } = [];
}
