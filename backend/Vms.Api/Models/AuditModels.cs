using System.ComponentModel.DataAnnotations;

namespace Vms.Api.Models;

public sealed class AuditLogQuery
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public Guid? UserId { get; init; }

    [StringLength(80)]
    public string? Action { get; init; }

    [StringLength(80)]
    public string? ResourceType { get; init; }

    [Range(1, 200)]
    public int Take { get; init; } = 100;
}

public sealed record AuditLogResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid UserId,
    string ActorUsername,
    string Action,
    string ResourceType,
    string? ResourceId,
    string Description);

public sealed record AuditLogSearchResponse(
    int MatchingCount,
    IReadOnlyList<AuditLogResponse> Items);
