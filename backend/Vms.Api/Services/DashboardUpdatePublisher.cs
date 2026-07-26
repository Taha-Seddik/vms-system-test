using Microsoft.AspNetCore.SignalR;
using Vms.Api.Hubs;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class DashboardUpdatePublisher(
    IHubContext<CommandCenterHub> hubContext,
    TimeProvider timeProvider,
    ILogger<DashboardUpdatePublisher> logger)
{
    private long _revision;

    public async Task PublishAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        var message = new DashboardChangedMessage(
            Interlocked.Increment(ref _revision),
            timeProvider.GetUtcNow(),
            reason);

        try
        {
            await hubContext.Clients.All.SendAsync(
                "DashboardChanged",
                message,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Dashboard notification {Revision} could not be published.",
                message.Revision);
        }
    }
}
