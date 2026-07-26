using Vms.Api.Domain;

namespace Vms.Api.Domain.Entities;

public sealed class Camera
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string Location { get; set; }

    public required string RtspUrl { get; set; }

    public required string HlsPath { get; set; }

    public Guid? GroupId { get; set; }

    public CameraGroup? Group { get; set; }

    public bool IsEnabled { get; set; }

    public CameraConnectionStatus ConnectionStatus { get; set; }

    public CameraRecordingStatus RecordingStatus { get; set; }

    public int? ResolutionWidth { get; set; }

    public int? ResolutionHeight { get; set; }

    public double? FramesPerSecond { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public string? LastConnectionError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserCameraAssignment> UserAssignments { get; set; } = [];

    public ICollection<Recording> Recordings { get; set; } = [];
}
