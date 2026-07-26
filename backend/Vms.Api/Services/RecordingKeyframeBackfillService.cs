using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;

namespace Vms.Api.Services;

public sealed class RecordingKeyframeBackfillService(
    IServiceScopeFactory scopeFactory,
    RecordingKeyframeService keyframes,
    ILogger<RecordingKeyframeBackfillService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        do
        {
            await GenerateMissingAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task GenerateMissingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var recordingIds = await database.Recordings
            .AsNoTracking()
            .Where(item =>
                item.State == RecordingState.Completed
                && !item.Keyframes.Any())
            .OrderByDescending(item => item.StartedAt)
            .Select(item => item.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        foreach (var recordingId in recordingIds)
        {
            try
            {
                await keyframes.EnsureAsync(recordingId, cancellationToken);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                logger.LogWarning(
                    exception,
                    "Background keyframe generation failed for {RecordingId}.",
                    recordingId);
            }
        }
    }
}
