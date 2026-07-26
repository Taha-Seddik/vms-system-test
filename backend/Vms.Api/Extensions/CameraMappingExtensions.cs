using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Extensions;

public static class CameraMappingExtensions
{
    public static AccessibleCameraResponse ToAccessibleResponse(this Camera camera) =>
        new(
            camera.Id,
            camera.Name,
            camera.Location,
            camera.HlsPath,
            camera.Group is null
                ? null
                : new CameraGroupSummaryResponse(
                    camera.Group.Id,
                    camera.Group.Name),
            FormatResolution(camera),
            camera.FramesPerSecond,
            camera.RecordingStatus,
            camera.ConnectionStatus,
            camera.IsEnabled,
            camera.LastHeartbeatAt,
            camera.LastCheckedAt);

    public static ManagedCameraResponse ToManagedResponse(this Camera camera) =>
        new(
            camera.Id,
            camera.Name,
            camera.Location,
            camera.RtspUrl,
            camera.HlsPath,
            camera.Group is null
                ? null
                : new CameraGroupSummaryResponse(
                    camera.Group.Id,
                    camera.Group.Name),
            FormatResolution(camera),
            camera.FramesPerSecond,
            camera.RecordingStatus,
            camera.ConnectionStatus,
            camera.IsEnabled,
            camera.LastHeartbeatAt,
            camera.LastCheckedAt,
            camera.LastConnectionError,
            camera.CreatedAt,
            camera.UpdatedAt);

    public static CameraGroupResponse ToResponse(this CameraGroup group) =>
        new(
            group.Id,
            group.Name,
            group.Description,
            group.Cameras.Count,
            group.CreatedAt,
            group.UpdatedAt);

    private static string? FormatResolution(Camera camera) =>
        camera.ResolutionWidth.HasValue && camera.ResolutionHeight.HasValue
            ? $"{camera.ResolutionWidth}x{camera.ResolutionHeight}"
            : null;
}
