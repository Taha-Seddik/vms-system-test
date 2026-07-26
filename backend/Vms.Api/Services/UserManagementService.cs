using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class UserManagementService(
    VmsDbContext database,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ManagedUserResponse>> GetAllAsync(
        string? search,
        AppRole? role,
        bool? isEnabled,
        string? cameraId,
        CancellationToken cancellationToken)
    {
        var query = database.Users
            .AsNoTracking()
            .Include(item => item.CameraAssignments)
            .ThenInclude(item => item.Camera)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item =>
                item.UserName!.ToLower().Contains(term)
                || item.DisplayName.ToLower().Contains(term));
        }

        if (isEnabled.HasValue)
        {
            query = query.Where(item => item.IsEnabled == isEnabled.Value);
        }

        if (!string.IsNullOrWhiteSpace(cameraId))
        {
            var id = cameraId.Trim();
            query = query.Where(item =>
                item.CameraAssignments.Any(assignment =>
                    assignment.CameraId == id));
        }

        var users = await query
            .OrderBy(item => item.UserName)
            .ToListAsync(cancellationToken);
        var roles = await GetRoleMapAsync(
            users.Select(item => item.Id),
            cancellationToken);

        return users
            .Where(item =>
                roles.TryGetValue(item.Id, out var userRole)
                && (!role.HasValue || userRole == role.Value))
            .Select(item => ToResponse(item, roles[item.Id]))
            .ToArray();
    }

    public async Task<ManagedUserResponse?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .Include(item => item.CameraAssignments)
            .ThenInclude(item => item.Camera)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var role = await GetRequiredRoleAsync(user);
        return ToResponse(user, role);
    }

    public async Task<UserMutationResult> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var cameraIds = NormalizeAssignments(request.AssignedCameraIds);
        var validationError = await ValidateAssignmentsAsync(
            request.Role,
            cameraIds,
            cancellationToken);
        if (validationError is not null)
        {
            return Failed(UserMutationError.Validation, validationError);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.Username.Trim(),
            DisplayName = request.DisplayName.Trim(),
            IsEnabled = true,
            LockoutEnabled = true,
            CreatedAt = timeProvider.GetUtcNow()
        };
        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return Failed(
                UserMutationError.Validation,
                FormatErrors(created));
        }

        var roleAssigned = await userManager.AddToRoleAsync(
            user,
            request.Role.ToString());
        if (!roleAssigned.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return Failed(
                UserMutationError.Validation,
                FormatErrors(roleAssigned));
        }

        AddAssignments(user.Id, request.Role, cameraIds);
        await database.SaveChangesAsync(cancellationToken);
        return new UserMutationResult(
            await GetAsync(user.Id, cancellationToken),
            UserMutationError.None,
            null);
    }

    public async Task<UserMutationResult> UpdateAsync(
        Guid id,
        Guid actingUserId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .Include(item => item.CameraAssignments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (user is null)
        {
            return Failed(UserMutationError.NotFound, "User was not found.");
        }

        var currentRole = await GetRequiredRoleAsync(user);
        if (id == actingUserId
            && (!request.IsEnabled || request.Role != currentRole))
        {
            return Failed(
                UserMutationError.Conflict,
                "You cannot disable your own account or change your own role.");
        }

        var cameraIds = NormalizeAssignments(request.AssignedCameraIds);
        var validationError = await ValidateAssignmentsAsync(
            request.Role,
            cameraIds,
            cancellationToken);
        if (validationError is not null)
        {
            return Failed(UserMutationError.Validation, validationError);
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsEnabled = request.IsEnabled;

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
        {
            return Failed(UserMutationError.Validation, FormatErrors(updated));
        }

        var securityChanged = currentRole != request.Role
            || !request.IsEnabled
            || !string.IsNullOrWhiteSpace(request.NewPassword);
        if (currentRole != request.Role)
        {
            var removed = await userManager.RemoveFromRoleAsync(
                user,
                currentRole.ToString());
            if (!removed.Succeeded)
            {
                return Failed(
                    UserMutationError.Validation,
                    FormatErrors(removed));
            }

            var added = await userManager.AddToRoleAsync(
                user,
                request.Role.ToString());
            if (!added.Succeeded)
            {
                await userManager.AddToRoleAsync(user, currentRole.ToString());
                return Failed(
                    UserMutationError.Validation,
                    FormatErrors(added));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var passwordChanged = await userManager.ResetPasswordAsync(
                user,
                token,
                request.NewPassword);
            if (!passwordChanged.Succeeded)
            {
                return Failed(
                    UserMutationError.Validation,
                    FormatErrors(passwordChanged));
            }
        }

        database.UserCameraAssignments.RemoveRange(user.CameraAssignments);
        AddAssignments(user.Id, request.Role, cameraIds);
        if (securityChanged)
        {
            await RevokeSessionsAsync(
                user.Id,
                "Account changed by an administrator",
                cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
        return new UserMutationResult(
            await GetAsync(user.Id, cancellationToken),
            UserMutationError.None,
            null);
    }

    public async Task<UserMutationResult> DeleteAsync(
        Guid id,
        Guid actingUserId,
        CancellationToken cancellationToken)
    {
        if (id == actingUserId)
        {
            return Failed(
                UserMutationError.Conflict,
                "You cannot delete your own account.");
        }

        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            return Failed(UserMutationError.NotFound, "User was not found.");
        }

        var deleted = await userManager.DeleteAsync(user);
        return deleted.Succeeded
            ? new UserMutationResult(null, UserMutationError.None, null)
            : Failed(UserMutationError.Validation, FormatErrors(deleted));
    }

    private async Task<string?> ValidateAssignmentsAsync(
        AppRole role,
        IReadOnlyList<string> cameraIds,
        CancellationToken cancellationToken)
    {
        if (role == AppRole.Viewer && cameraIds.Count == 0)
        {
            return "A Viewer must be assigned at least one camera.";
        }

        if (role != AppRole.Viewer && cameraIds.Count > 0)
        {
            return "Camera assignments are only valid for Viewer accounts.";
        }

        var existingCount = await database.Cameras.CountAsync(
            camera => cameraIds.Contains(camera.Id),
            cancellationToken);
        return existingCount == cameraIds.Count
            ? null
            : "One or more assigned cameras do not exist.";
    }

    private void AddAssignments(
        Guid userId,
        AppRole role,
        IReadOnlyList<string> cameraIds)
    {
        if (role != AppRole.Viewer)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var cameraId in cameraIds)
        {
            database.UserCameraAssignments.Add(new UserCameraAssignment
            {
                UserId = userId,
                CameraId = cameraId,
                AssignedAt = now
            });
        }
    }

    private async Task RevokeSessionsAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sessions = await database.UserSessions
            .Where(item => item.UserId == userId && item.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }
    }

    private async Task<AppRole> GetRequiredRoleAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return roles.Count == 1
            && Enum.TryParse<AppRole>(roles[0], out var role)
                ? role
                : throw new InvalidOperationException(
                    $"User '{user.UserName}' must have exactly one valid VMS role.");
    }

    private async Task<IReadOnlyDictionary<Guid, AppRole>> GetRoleMapAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.ToArray();
        return await (
            from userRole in database.UserRoles.AsNoTracking()
            join identityRole in database.Roles.AsNoTracking()
                on userRole.RoleId equals identityRole.Id
            where ids.Contains(userRole.UserId)
            select new
            {
                userRole.UserId,
                identityRole.Name
            })
            .ToDictionaryAsync(
                item => item.UserId,
                item => Enum.Parse<AppRole>(item.Name!),
                cancellationToken);
    }

    private static ManagedUserResponse ToResponse(
        ApplicationUser user,
        AppRole role) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            role,
            user.IsEnabled,
            user.CameraAssignments
                .OrderBy(item => item.Camera.Name)
                .Select(item => new UserCameraResponse(
                    item.CameraId,
                    item.Camera.Name))
                .ToArray(),
            user.CreatedAt,
            user.LastLoginAt,
            user.LastActivityAt);

    private static string[] NormalizeAssignments(
        IReadOnlyList<string>? cameraIds) =>
        cameraIds?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray()
        ?? [];

    private static string FormatErrors(IdentityResult result) =>
        string.Join(
            " ",
            result.Errors.Select(item => item.Description));

    private static UserMutationResult Failed(
        UserMutationError error,
        string description) =>
        new(null, error, description);
}
