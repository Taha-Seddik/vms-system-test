using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class FileSystemStorageMetricsProvider(
    IOptions<RecordingStorageOptions> options,
    ILogger<FileSystemStorageMetricsProvider> logger) : IStorageMetricsProvider
{
    public Task<StorageHealthResponse> GetAsync(
        CancellationToken cancellationToken)
    {
        var configuredPath = options.Value.Path;

        try
        {
            var fullPath = Path.GetFullPath(configuredPath);
            Directory.CreateDirectory(fullPath);
            cancellationToken.ThrowIfCancellationRequested();

            var drive = FindDrive(fullPath);
            var recordingBytes = Directory
                .EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path).Length)
                .Sum();
            var totalBytes = drive.TotalSize;
            var availableBytes = drive.AvailableFreeSpace;
            var usedBytes = Math.Max(0, totalBytes - availableBytes);
            var usedPercent = totalBytes == 0
                ? 0
                : Math.Round(usedBytes * 100d / totalBytes, 2);

            return Task.FromResult(new StorageHealthResponse(
                fullPath,
                GetStatus(usedPercent),
                totalBytes,
                availableBytes,
                usedBytes,
                recordingBytes,
                usedPercent,
                null));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "Recording storage metrics are unavailable for {StoragePath}",
                configuredPath);
            return Task.FromResult(new StorageHealthResponse(
                configuredPath,
                StorageHealthStatus.Unavailable,
                0,
                0,
                0,
                0,
                0,
                exception.Message));
        }
    }

    private DriveInfo FindDrive(string fullPath) =>
        DriveInfo.GetDrives()
            .Where(drive => fullPath.StartsWith(
                drive.RootDirectory.FullName,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(drive => drive.RootDirectory.FullName.Length)
            .First();

    private StorageHealthStatus GetStatus(double usedPercent)
    {
        if (usedPercent >= options.Value.CriticalPercent)
        {
            return StorageHealthStatus.Critical;
        }

        return usedPercent >= options.Value.WarningPercent
            ? StorageHealthStatus.Warning
            : StorageHealthStatus.Healthy;
    }
}
