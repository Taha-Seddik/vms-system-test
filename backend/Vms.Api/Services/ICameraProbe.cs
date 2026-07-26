namespace Vms.Api.Services;

public interface ICameraProbe
{
    Task<CameraProbeResult> ProbeAsync(
        string rtspUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed record CameraProbeResult(
    bool Succeeded,
    TimeSpan Elapsed,
    string? Codec,
    int? ResolutionWidth,
    int? ResolutionHeight,
    double? FramesPerSecond,
    string? Error);
