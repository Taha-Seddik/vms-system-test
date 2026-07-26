using System.Diagnostics;

namespace Vms.Api.Services;

public sealed class ApplicationUptime(TimeProvider timeProvider)
{
    private readonly DateTimeOffset _startedAt =
        Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public TimeSpan Elapsed =>
        timeProvider.GetUtcNow() - _startedAt;
}
