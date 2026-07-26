namespace Vms.Api.Services;

public sealed record RecordingProcessRequest(
    string SourceUrl,
    string OutputPath,
    TimeSpan? MaximumDuration);

public interface IRecordingProcessHandle : IAsyncDisposable
{
    Task<int> Completion { get; }

    Task<string> GetErrorAsync();

    Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IRecordingProcessRunner
{
    IRecordingProcessHandle Start(RecordingProcessRequest request);
}
