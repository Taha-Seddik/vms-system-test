using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class AuditService(
    VmsDbContext database,
    TimeProvider timeProvider)
{
    public async Task RecordAsync(
        Guid userId,
        string actorUsername,
        string action,
        string resourceType,
        string? resourceId,
        string description,
        CancellationToken cancellationToken)
    {
        database.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = timeProvider.GetUtcNow(),
            UserId = userId,
            ActorUsername = actorUsername,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Description = description
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditLogSearchResponse> SearchAsync(
        AuditLogQuery request,
        CancellationToken cancellationToken)
    {
        var query = database.AuditLogs.AsNoTracking();

        if (request.From.HasValue)
        {
            query = query.Where(item => item.Timestamp >= request.From.Value);
        }

        if (request.To.HasValue)
        {
            query = query.Where(item => item.Timestamp <= request.To.Value);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(item => item.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(item => item.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
        {
            var resourceType = request.ResourceType.Trim();
            query = query.Where(item => item.ResourceType == resourceType);
        }

        var count = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.Timestamp)
            .Take(request.Take)
            .Select(item => new AuditLogResponse(
                item.Id,
                item.Timestamp,
                item.UserId,
                item.ActorUsername,
                item.Action,
                item.ResourceType,
                item.ResourceId,
                item.Description))
            .ToListAsync(cancellationToken);

        return new AuditLogSearchResponse(count, items);
    }
}
