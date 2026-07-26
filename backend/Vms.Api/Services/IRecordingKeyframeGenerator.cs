namespace Vms.Api.Services;

public sealed record GeneratedRecordingKeyframe(
    int TimestampSeconds,
    string FileName);

public interface IRecordingKeyframeGenerator
{
    Task<IReadOnlyList<GeneratedRecordingKeyframe>> GenerateAsync(
        Guid recordingId,
        string recordingPath,
        double durationSeconds,
        CancellationToken cancellationToken);
}
