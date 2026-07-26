using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class StorageEventService(
    VmsDbContext database,
    IStorageMetricsProvider storageMetrics,
    DashboardUpdatePublisher dashboardUpdates,
    TimeProvider timeProvider)
{
    public async Task EvaluateAsync(CancellationToken cancellationToken)
    {
        var storage = await storageMetrics.GetAsync(cancellationToken);
        var openEvents = await database.SystemEvents
            .Where(item =>
                item.Type == SystemEventType.StorageFull
                && item.Status == EventStatus.Open)
            .ToListAsync(cancellationToken);

        if (storage.Status == StorageHealthStatus.Critical)
        {
            if (openEvents.Count > 0)
            {
                return;
            }

            database.SystemEvents.Add(new SystemEvent
            {
                Id = Guid.NewGuid(),
                Type = SystemEventType.StorageFull,
                Timestamp = timeProvider.GetUtcNow(),
                Severity = EventSeverity.Critical,
                Description =
                    $"Recording storage reached critical capacity at {storage.UsedPercent:F1}% used.",
                Status = EventStatus.Open
            });
            await database.SaveChangesAsync(cancellationToken);
            await dashboardUpdates.PublishAsync(
                "storage-full",
                cancellationToken);
            return;
        }

        if ((storage.Status is StorageHealthStatus.Healthy
                or StorageHealthStatus.Warning)
            && openEvents.Count > 0)
        {
            foreach (var item in openEvents)
            {
                item.Status = EventStatus.Closed;
            }

            await database.SaveChangesAsync(cancellationToken);
            await dashboardUpdates.PublishAsync(
                "storage-recovered",
                cancellationToken);
        }
    }
}
