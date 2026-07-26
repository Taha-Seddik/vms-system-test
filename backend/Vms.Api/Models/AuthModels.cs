using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthenticatedUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    AppRole Role,
    IReadOnlyList<string> AssignedCameraIds,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LastActivityAt);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    AuthenticatedUserResponse User);

public sealed record ActivityEventResponse(
    Guid Id,
    SystemEventType Type,
    DateTimeOffset Timestamp,
    string Description);

public sealed record AuthActivityResponse(
    int ActiveSessions,
    IReadOnlyList<ActivityEventResponse> RecentEvents);

public enum LoginFailure
{
    None,
    InvalidCredentials,
    ViewerHasNoAssignments
}

public sealed record LoginResult(LoginResponse? Response, LoginFailure Failure);
