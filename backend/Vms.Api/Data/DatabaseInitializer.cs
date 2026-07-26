using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vms.Api.Domain.Entities;
using Vms.Api.Domain;
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

        if (await database.Users.AnyAsync())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var hasher = new PasswordHasher<AppUser>();

        var administrator = CreateUser(
            DemoIdentityData.AdministratorId,
            "admin",
            "System Administrator",
            AppRole.Administrator,
            "Admin123!",
            now,
            hasher);

        var operatorUser = CreateUser(
            DemoIdentityData.OperatorId,
            "operator",
            "Security Operator",
            AppRole.Operator,
            "Operator123!",
            now,
            hasher);

        var viewer = CreateUser(
            DemoIdentityData.ViewerId,
            "viewer",
            "Assigned Camera Viewer",
            AppRole.Viewer,
            "Viewer123!",
            now,
            hasher);

        viewer.CameraAssignments.Add(new UserCameraAssignment
        {
            UserId = viewer.Id,
            CameraId = "camera-1",
            AssignedAt = now
        });
        viewer.CameraAssignments.Add(new UserCameraAssignment
        {
            UserId = viewer.Id,
            CameraId = "camera-2",
            AssignedAt = now
        });

        database.Users.AddRange(administrator, operatorUser, viewer);
        await database.SaveChangesAsync();
    }

    private static AppUser CreateUser(
        Guid id,
        string username,
        string displayName,
        AppRole role,
        string password,
        DateTimeOffset createdAt,
        IPasswordHasher<AppUser> hasher)
    {
        var user = new AppUser
        {
            Id = id,
            Username = username,
            NormalizedUsername = UsernameNormalizer.Normalize(username),
            DisplayName = displayName,
            PasswordHash = string.Empty,
            Role = role,
            IsEnabled = true,
            CreatedAt = createdAt
        };

        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }
}
