namespace Vms.Api.Cameras;

public sealed record AccessibleCameraResponse(
    string Id,
    string Name,
    string Location,
    string HlsUrl);

public static class DemoCameraCatalog
{
    public static readonly IReadOnlyList<AccessibleCameraResponse> All =
    [
        new("camera-1", "Entrance", "Main entrance", "/camera-1/index.m3u8"),
        new("camera-2", "Loading Bay", "Logistics area", "/camera-2/index.m3u8"),
        new("camera-3", "Parking", "Visitor parking", "/camera-3/index.m3u8"),
        new("camera-4", "Warehouse", "Storage floor", "/camera-4/index.m3u8")
    ];
}

