using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Utils;

namespace Vms.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();

        if (database.Database.IsRelational())
        {
            await database.Database.MigrateAsync();
        }
        else
        {
            await database.Database.EnsureCreatedAsync();
        }

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        await EnsureDemoCamerasAsync(database);

        foreach (var role in Enum.GetValues<AppRole>())
        {
            if (!await roleManager.RoleExistsAsync(role.ToString()))
            {
                (await roleManager.CreateAsync(
                    new IdentityRole<Guid>(role.ToString())))
                    .EnsureSucceeded($"Create {role} role");
            }
        }

        var administrator = await EnsureUserAsync(
            userManager,
            DemoIdentityData.AdministratorId,
            "admin",
            "System Administrator",
            AppRole.Administrator,
            "Admin123!");
        var operatorUser = await EnsureUserAsync(
            userManager,
            DemoIdentityData.OperatorId,
            "operator",
            "Security Operator",
            AppRole.Operator,
            "Operator123!");
        var viewer = await EnsureUserAsync(
            userManager,
            DemoIdentityData.ViewerId,
            "viewer",
            "Assigned Camera Viewer",
            AppRole.Viewer,
            "Viewer123!");

        await EnsureRoleAsync(userManager, administrator, AppRole.Administrator);
        await EnsureRoleAsync(userManager, operatorUser, AppRole.Operator);
        await EnsureRoleAsync(userManager, viewer, AppRole.Viewer);

        await EnsureViewerAssignmentsAsync(database, viewer.Id);
    }

    private static async Task EnsureDemoCamerasAsync(VmsDbContext database)
    {
        var now = DateTimeOffset.UtcNow;
        var existingGroupIds = await database.CameraGroups
            .Select(item => item.Id)
            .ToListAsync();

        foreach (var group in DemoCameraData.CreateGroups(now)
                     .Where(group => !existingGroupIds.Contains(group.Id)))
        {
            database.CameraGroups.Add(group);
        }

        await database.SaveChangesAsync();

        var existingCameraIds = await database.Cameras
            .Select(item => item.Id)
            .ToListAsync();

        foreach (var camera in DemoCameraData.CreateCameras(now)
                     .Where(camera => !existingCameraIds.Contains(camera.Id)))
        {
            database.Cameras.Add(camera);
        }

        await database.SaveChangesAsync();
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        Guid id,
        string username,
        string displayName,
        AppRole role,
        string password)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = id,
                UserName = username,
                DisplayName = displayName,
                IsEnabled = true,
                LockoutEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            (await userManager.CreateAsync(user, password))
                .EnsureSucceeded($"Create {role} demo user");
            return user;
        }

        if (!user.LockoutEnabled)
        {
            user.LockoutEnabled = true;
            (await userManager.UpdateAsync(user))
                .EnsureSucceeded($"Enable lockout for {role} demo user");
        }

        if (string.IsNullOrWhiteSpace(user.SecurityStamp))
        {
            (await userManager.UpdateSecurityStampAsync(user))
                .EnsureSucceeded($"Set security stamp for {role} demo user");
        }

        return user;
    }

    private static async Task EnsureRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        AppRole requiredRole)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var unwantedRoles = currentRoles
            .Where(role => !string.Equals(
                role,
                requiredRole.ToString(),
                StringComparison.Ordinal))
            .ToArray();

        if (unwantedRoles.Length > 0)
        {
            (await userManager.RemoveFromRolesAsync(user, unwantedRoles))
                .EnsureSucceeded($"Remove obsolete roles from {user.UserName}");
        }

        if (!await userManager.IsInRoleAsync(user, requiredRole.ToString()))
        {
            (await userManager.AddToRoleAsync(user, requiredRole.ToString()))
                .EnsureSucceeded($"Assign {requiredRole} role to {user.UserName}");
        }
    }

    private static async Task EnsureViewerAssignmentsAsync(
        VmsDbContext database,
        Guid viewerId)
    {
        var assignedIds = await database.UserCameraAssignments
            .Where(item => item.UserId == viewerId)
            .Select(item => item.CameraId)
            .ToListAsync();
        var requiredIds = new[] { "camera-1", "camera-2" };
        var now = DateTimeOffset.UtcNow;

        foreach (var cameraId in requiredIds.Except(assignedIds))
        {
            database.UserCameraAssignments.Add(new UserCameraAssignment
            {
                UserId = viewerId,
                CameraId = cameraId,
                AssignedAt = now
            });
        }

        await database.SaveChangesAsync();
    }
}
