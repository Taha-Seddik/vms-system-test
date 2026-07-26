using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Extensions;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CameraAccessService(VmsDbContext database)
{
    public async Task<IReadOnlyList<AccessibleCameraResponse>> GetAccessibleAsync(
        Guid userId,
        AppRole role,
        CancellationToken cancellationToken)
    {
        var query = database.Cameras
            .AsNoTracking()
            .Include(camera => camera.Group)
            .AsQueryable();

        if (role == AppRole.Viewer)
        {
            query = query.Where(camera =>
                camera.UserAssignments.Any(item => item.UserId == userId));
        }

        var cameras = await query
            .OrderBy(camera => camera.Name)
            .ToListAsync(cancellationToken);
        return cameras.Select(camera => camera.ToAccessibleResponse()).ToArray();
    }
}
