using Microsoft.AspNetCore.Identity;

namespace Vms.Api.Domain.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset? LastActivityAt { get; set; }

    public ICollection<UserCameraAssignment> CameraAssignments { get; set; } = [];

    public ICollection<UserSession> Sessions { get; set; } = [];
}
