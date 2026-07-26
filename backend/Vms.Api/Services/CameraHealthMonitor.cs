using Microsoft.Extensions.Options;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class CameraHealthMonitor(
    IServiceScopeFactory scopeFactory,
    IOptions<CameraMonitoringOptions> options,
    ILogger<CameraHealthMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Camera health monitoring is disabled.");
            return;
        }

        if (options.Value.InitialDelaySeconds > 0)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.InitialDelaySeconds),
                stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var health = scope.ServiceProvider
                    .GetRequiredService<CameraHealthService>();
                await health.CheckAllEnabledAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "The camera health-monitor cycle failed.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(options.Value.IntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
