using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain.Entities;
using Vms.Api.Extensions;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CameraGroupService(
    VmsDbContext database,
    DashboardUpdatePublisher dashboardUpdates)
{
    public async Task<IReadOnlyList<CameraGroupResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var groups = await database.CameraGroups
            .AsNoTracking()
            .Include(group => group.Cameras)
            .OrderBy(group => group.Name)
            .ToListAsync(cancellationToken);
        return groups.Select(group => group.ToResponse()).ToArray();
    }

    public async Task<CameraGroupMutationResult> CreateAsync(
        CreateCameraGroupRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await NameExistsAsync(name, null, cancellationToken))
        {
            return CameraGroupMutationResult.Conflict(
                $"A camera group named '{name}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var group = new CameraGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = NormalizeDescription(request.Description),
            CreatedAt = now,
            UpdatedAt = now
        };
        database.CameraGroups.Add(group);
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync(
            "camera-group-created",
            cancellationToken);
        return CameraGroupMutationResult.Success(group.ToResponse());
    }

    public async Task<CameraGroupMutationResult> UpdateAsync(
        Guid id,
        UpdateCameraGroupRequest request,
        CancellationToken cancellationToken)
    {
        var group = await database.CameraGroups
            .Include(item => item.Cameras)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (group is null)
        {
            return CameraGroupMutationResult.NotFound(
                "Camera group was not found.");
        }

        var name = request.Name.Trim();
        if (await NameExistsAsync(name, id, cancellationToken))
        {
            return CameraGroupMutationResult.Conflict(
                $"A camera group named '{name}' already exists.");
        }

        group.Name = name;
        group.Description = NormalizeDescription(request.Description);
        group.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync(
            "camera-group-updated",
            cancellationToken);
        return CameraGroupMutationResult.Success(group.ToResponse());
    }

    public async Task<CameraGroupMutationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var group = await database.CameraGroups.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (group is null)
        {
            return CameraGroupMutationResult.NotFound(
                "Camera group was not found.");
        }

        database.CameraGroups.Remove(group);
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync(
            "camera-group-deleted",
            cancellationToken);
        return CameraGroupMutationResult.Success();
    }

    private Task<bool> NameExistsAsync(
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        database.CameraGroups.AnyAsync(
            item => item.Name.ToLower() == name.ToLower()
                && (!excludedId.HasValue || item.Id != excludedId),
            cancellationToken);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}

public sealed record CameraGroupMutationResult(
    CameraGroupResponse? Group,
    CameraMutationError ErrorType,
    string? Error)
{
    public static CameraGroupMutationResult Success(
        CameraGroupResponse? group = null) =>
        new(group, CameraMutationError.None, null);

    public static CameraGroupMutationResult NotFound(string error) =>
        new(null, CameraMutationError.NotFound, error);

    public static CameraGroupMutationResult Conflict(string error) =>
        new(null, CameraMutationError.Conflict, error);
}
