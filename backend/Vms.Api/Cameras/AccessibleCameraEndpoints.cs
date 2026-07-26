using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vms.Api.Auth;
using Vms.Api.Data;
using Vms.Api.Domain;

namespace Vms.Api.Cameras;

public static class AccessibleCameraEndpoints
{
    public static IEndpointRouteBuilder MapAccessibleCameraEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/cameras/accessible", GetAccessibleCamerasAsync)
            .RequireAuthorization()
            .WithTags("Cameras");

        return endpoints;
    }

    private static async Task<IResult> GetAccessibleCamerasAsync(
        ClaimsPrincipal principal,
        VmsDbContext database,
        CancellationToken cancellationToken)
    {
        if (!principal.IsInRole(nameof(AppRole.Viewer)))
        {
            return Results.Ok(DemoCameraCatalog.All);
        }

        var userId = principal.GetRequiredUserId();
        var assignedIds = await database.UserCameraAssignments
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.CameraId)
            .ToListAsync(cancellationToken);
        var assignedSet = assignedIds.ToHashSet(StringComparer.Ordinal);
        var accessible = DemoCameraCatalog.All
            .Where(camera => assignedSet.Contains(camera.Id))
            .ToArray();

        return Results.Ok(accessible);
    }
}

