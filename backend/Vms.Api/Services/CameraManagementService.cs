using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Extensions;
using Vms.Api.Models;
using Vms.Api.Utils;

namespace Vms.Api.Services;

public sealed class CameraManagementService(
    VmsDbContext database,
    DashboardUpdatePublisher dashboardUpdates)
{
    public async Task<IReadOnlyList<ManagedCameraResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var cameras = await database.Cameras
            .AsNoTracking()
            .Include(camera => camera.Group)
            .OrderBy(camera => camera.Name)
            .ToListAsync(cancellationToken);
        return cameras.Select(camera => camera.ToManagedResponse()).ToArray();
    }

    public async Task<ManagedCameraResponse?> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var camera = await database.Cameras
            .AsNoTracking()
            .Include(item => item.Group)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return camera?.ToManagedResponse();
    }

    public async Task<CameraMutationResult> CreateAsync(
        CreateCameraRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateConfigurationAsync(
            request.RtspUrl,
            request.HlsPath,
            request.GroupId,
            cancellationToken);
        if (validationError is not null)
        {
            return CameraMutationResult.Invalid(validationError);
        }

        var id = request.Id.Trim();
        if (await database.Cameras.AnyAsync(
                item => item.Id == id,
                cancellationToken))
        {
            return CameraMutationResult.Conflict(
                $"A camera with id '{id}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var camera = new Camera
        {
            Id = id,
            Name = request.Name.Trim(),
            Location = request.Location.Trim(),
            RtspUrl = request.RtspUrl.Trim(),
            HlsPath = NormalizeHlsPath(request.HlsPath),
            GroupId = request.GroupId,
            IsEnabled = request.IsEnabled,
            ConnectionStatus = request.IsEnabled
                ? CameraConnectionStatus.Unknown
                : CameraConnectionStatus.Disabled,
            RecordingStatus = CameraRecordingStatus.NotRecording,
            CreatedAt = now,
            UpdatedAt = now
        };

        database.Cameras.Add(camera);
        await database.SaveChangesAsync(cancellationToken);
        await database.Entry(camera).Reference(item => item.Group)
            .LoadAsync(cancellationToken);
        await dashboardUpdates.PublishAsync("camera-created", cancellationToken);
        return CameraMutationResult.Success(camera.ToManagedResponse());
    }

    public async Task<CameraMutationResult> UpdateAsync(
        string id,
        UpdateCameraRequest request,
        CancellationToken cancellationToken)
    {
        var camera = await database.Cameras
            .Include(item => item.Group)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (camera is null)
        {
            return CameraMutationResult.NotFound(
                $"Camera '{id}' was not found.");
        }

        if (camera.RecordingStatus == CameraRecordingStatus.Recording)
        {
            return CameraMutationResult.Conflict(
                "Stop the active recording before editing this camera.");
        }

        var requestedRtspUrl = ResolveUpdatedRtspUrl(
            camera.RtspUrl,
            request.RtspUrl);
        if (requestedRtspUrl is null)
        {
            return CameraMutationResult.Invalid(
                "Replace the redacted RTSP URL completely when changing the camera source.");
        }

        var validationError = await ValidateConfigurationAsync(
            requestedRtspUrl,
            request.HlsPath,
            request.GroupId,
            cancellationToken);
        if (validationError is not null)
        {
            return CameraMutationResult.Invalid(validationError);
        }

        var sourceChanged = !string.Equals(
            camera.RtspUrl,
            requestedRtspUrl,
            StringComparison.Ordinal);

        camera.Name = request.Name.Trim();
        camera.Location = request.Location.Trim();
        camera.RtspUrl = requestedRtspUrl;
        camera.HlsPath = NormalizeHlsPath(request.HlsPath);
        camera.GroupId = request.GroupId;
        camera.UpdatedAt = DateTimeOffset.UtcNow;

        if (sourceChanged && camera.IsEnabled)
        {
            camera.LastConnectionError = null;
            camera.LastCheckedAt = null;
        }

        await database.SaveChangesAsync(cancellationToken);
        await database.Entry(camera).Reference(item => item.Group)
            .LoadAsync(cancellationToken);
        await dashboardUpdates.PublishAsync("camera-updated", cancellationToken);
        return CameraMutationResult.Success(camera.ToManagedResponse());
    }

    public async Task<CameraMutationResult> SetEnabledAsync(
        string id,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var camera = await database.Cameras
            .Include(item => item.Group)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (camera is null)
        {
            return CameraMutationResult.NotFound(
                $"Camera '{id}' was not found.");
        }

        if (!isEnabled
            && camera.RecordingStatus == CameraRecordingStatus.Recording)
        {
            return CameraMutationResult.Conflict(
                "Stop the active recording before disabling this camera.");
        }

        camera.IsEnabled = isEnabled;
        camera.ConnectionStatus = isEnabled
            ? CameraConnectionStatus.Unknown
            : CameraConnectionStatus.Disabled;
        camera.LastConnectionError = null;
        camera.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync(
            isEnabled ? "camera-enabled" : "camera-disabled",
            cancellationToken);

        return CameraMutationResult.Success(camera.ToManagedResponse());
    }

    public async Task<CameraMutationResult> DeleteAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var camera = await database.Cameras.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (camera is null)
        {
            return CameraMutationResult.NotFound(
                $"Camera '{id}' was not found.");
        }

        if (camera.RecordingStatus == CameraRecordingStatus.Recording
            || await database.Recordings.AnyAsync(
                recording => recording.CameraId == id,
                cancellationToken))
        {
            return CameraMutationResult.Conflict(
                "A camera with active or historical recordings cannot be deleted.");
        }

        database.Cameras.Remove(camera);
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync("camera-deleted", cancellationToken);
        return CameraMutationResult.Success();
    }

    private async Task<string?> ValidateConfigurationAsync(
        string rtspUrl,
        string hlsPath,
        Guid? groupId,
        CancellationToken cancellationToken)
    {
        if (!RtspUrlUtilities.IsSupported(rtspUrl.Trim()))
        {
            return "RTSP URL must be an absolute rtsp:// or rtsps:// address.";
        }

        var normalizedHlsPath = hlsPath.Trim();
        if (!normalizedHlsPath.StartsWith('/')
            || normalizedHlsPath.StartsWith("//", StringComparison.Ordinal)
            || normalizedHlsPath.Contains("..", StringComparison.Ordinal)
            || normalizedHlsPath.Contains('\\'))
        {
            return "HLS path must be a contained absolute path without traversal.";
        }

        if (groupId.HasValue
            && !await database.CameraGroups.AnyAsync(
                item => item.Id == groupId,
                cancellationToken))
        {
            return "The selected camera group does not exist.";
        }

        return null;
    }

    private static string NormalizeHlsPath(string hlsPath) => hlsPath.Trim();

    private static string? ResolveUpdatedRtspUrl(
        string existingUrl,
        string requestedUrl)
    {
        var normalized = requestedUrl.Trim();
        if (!normalized.Contains("***", StringComparison.Ordinal))
        {
            return normalized;
        }

        return string.Equals(
            normalized,
            RtspUrlUtilities.RedactCredentials(existingUrl),
            StringComparison.Ordinal)
            ? existingUrl
            : null;
    }
}

public enum CameraMutationError
{
    None,
    NotFound,
    Conflict,
    Invalid
}

public sealed record CameraMutationResult(
    ManagedCameraResponse? Camera,
    CameraMutationError ErrorType,
    string? Error)
{
    public static CameraMutationResult Success(ManagedCameraResponse? camera = null) =>
        new(camera, CameraMutationError.None, null);

    public static CameraMutationResult NotFound(string error) =>
        new(null, CameraMutationError.NotFound, error);

    public static CameraMutationResult Conflict(string error) =>
        new(null, CameraMutationError.Conflict, error);

    public static CameraMutationResult Invalid(string error) =>
        new(null, CameraMutationError.Invalid, error);
}
