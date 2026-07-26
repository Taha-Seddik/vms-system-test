using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CameraAccessService(VmsDbContext database)
{
    private static readonly IReadOnlyList<AccessibleCameraResponse> DemoCameras =
    [
        new("camera-1", "Entrance", "Main entrance", "/camera-1/index.m3u8"),
        new("camera-2", "Loading Bay", "Logistics area", "/camera-2/index.m3u8"),
        new("camera-3", "Parking", "Visitor parking", "/camera-3/index.m3u8"),
        new("camera-4", "Warehouse", "Storage floor", "/camera-4/index.m3u8")
    ];

    public async Task<IReadOnlyList<AccessibleCameraResponse>> GetAccessibleAsync(
        Guid userId,
        AppRole role,
        CancellationToken cancellationToken)
    {
        if (role != AppRole.Viewer)
        {
            return DemoCameras;
        }

        var assignedIds = await database.UserCameraAssignments
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.CameraId)
            .ToListAsync(cancellationToken);
        var assignedSet = assignedIds.ToHashSet(StringComparer.Ordinal);

        return DemoCameras
            .Where(camera => assignedSet.Contains(camera.Id))
            .ToArray();
    }
}
