using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class StorageHealthMonitor(
    IServiceScopeFactory scopeFactory,
    IOptions<RecordingStorageOptions> options,
    ILogger<StorageHealthMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EvaluateAsync(stoppingToken);
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(options.Value.MonitorIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateAsync(stoppingToken);
        }
    }

    private async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<StorageEventService>()
                .EvaluateAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Storage event evaluation failed.");
        }
    }
}
