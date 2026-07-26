using Vms.Api.Domain;
using Vms.Api.Domain.Entities;

namespace Vms.Api.Data;

public static class DemoCameraData
{
    public static readonly Guid PerimeterGroupId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");

    public static readonly Guid OperationsGroupId =
        Guid.Parse("20000000-0000-0000-0000-000000000002");

    public static IReadOnlyList<CameraGroup> CreateGroups(DateTimeOffset now) =>
    [
        new CameraGroup
        {
            Id = PerimeterGroupId,
            Name = "Perimeter",
            Description = "Public entrances and parking areas",
            CreatedAt = now,
            UpdatedAt = now
        },
        new CameraGroup
        {
            Id = OperationsGroupId,
            Name = "Operations",
            Description = "Loading and warehouse operations",
            CreatedAt = now,
            UpdatedAt = now
        }
    ];

    public static IReadOnlyList<Camera> CreateCameras(DateTimeOffset now) =>
    [
        CreateCamera(
            "camera-1",
            "Entrance",
            "Main entrance",
            PerimeterGroupId,
            now),
        CreateCamera(
            "camera-2",
            "Loading Bay",
            "Logistics area",
            OperationsGroupId,
            now),
        CreateCamera(
            "camera-3",
            "Parking",
            "Visitor parking",
            PerimeterGroupId,
            now),
        CreateCamera(
            "camera-4",
            "Warehouse",
            "Storage floor",
            OperationsGroupId,
            now)
    ];

    private static Camera CreateCamera(
        string id,
        string name,
        string location,
        Guid groupId,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            Name = name,
            Location = location,
            RtspUrl = $"rtsp://mediamtx:8554/{id}",
            HlsPath = $"/{id}/index.m3u8",
            GroupId = groupId,
            IsEnabled = true,
            ConnectionStatus = CameraConnectionStatus.Unknown,
            RecordingStatus = CameraRecordingStatus.NotRecording,
            CreatedAt = now,
            UpdatedAt = now
        };
}
