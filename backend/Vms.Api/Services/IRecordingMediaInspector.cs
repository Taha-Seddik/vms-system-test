namespace Vms.Api.Services;

public sealed record RecordedMediaInfo(
    double DurationSeconds,
    long FileSizeBytes);

public interface IRecordingMediaInspector
{
    Task<RecordedMediaInfo?> InspectAsync(
        string filePath,
        CancellationToken cancellationToken);
}
