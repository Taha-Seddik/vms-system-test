using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class AuthenticationService(
    VmsDbContext database,
    IPasswordHasher<AppUser> passwordHasher,
    JwtTokenService tokenService,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        var normalizedUsername = request.Username.Trim().ToUpperInvariant();
        var user = await database.Users
            .Include(item => item.CameraAssignments)
            .SingleOrDefaultAsync(
                item => item.NormalizedUsername == normalizedUsername,
                cancellationToken);

        if (user is null || !user.IsEnabled)
        {
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        if (user.Role == AppRole.Viewer && user.CameraAssignments.Count == 0)
        {
            return new LoginResult(null, LoginFailure.ViewerHasNoAssignments);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        }

        var now = DateTimeOffset.UtcNow;
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(jwtOptions.Value.AccessTokenMinutes),
            LastActivityAt = now
        };

        user.LastLoginAt = now;
        user.LastActivityAt = now;
        database.UserSessions.Add(session);
        database.SystemEvents.Add(CreateActivityEvent(
            user,
            SystemEventType.UserLogin,
            $"{user.DisplayName} signed in.",
            now));
        await database.SaveChangesAsync(cancellationToken);

        var response = new LoginResponse(
            tokenService.CreateToken(user, session),
            session.ExpiresAt,
            ToUserResponse(user));

        return new LoginResult(response, LoginFailure.None);
    }

    public async Task LogoutAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await database.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId,
                cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        session.RevokedAt = now;
        session.RevokedReason = "User logout";
        session.User.LastActivityAt = now;
        database.SystemEvents.Add(CreateActivityEvent(
            session.User,
            SystemEventType.UserLogout,
            $"{session.User.DisplayName} signed out.",
            now));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .Include(item => item.CameraAssignments)
            .SingleAsync(item => item.Id == userId, cancellationToken);

        return ToUserResponse(user);
    }

    public async Task<AuthActivityResponse> GetActivityAsync(
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var activeSessions = await database.UserSessions.CountAsync(
            item => item.RevokedAt == null &&
                    item.ExpiresAt > now &&
                    item.LastActivityAt > now.AddMinutes(-5),
            cancellationToken);
        var events = await database.SystemEvents
            .AsNoTracking()
            .Where(item =>
                item.Type == SystemEventType.UserLogin ||
                item.Type == SystemEventType.UserLogout)
            .OrderByDescending(item => item.Timestamp)
            .Take(25)
            .Select(item => new ActivityEventResponse(
                item.Id,
                item.Type,
                item.Timestamp,
                item.Description))
            .ToListAsync(cancellationToken);

        return new AuthActivityResponse(activeSessions, events);
    }

    private static SystemEvent CreateActivityEvent(
        AppUser user,
        SystemEventType type,
        string description,
        DateTimeOffset timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Timestamp = timestamp,
            UserId = user.Id,
            Severity = EventSeverity.Information,
            Description = description,
            Status = EventStatus.Closed
        };

    private static AuthenticatedUserResponse ToUserResponse(AppUser user) =>
        new(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Role,
            user.CameraAssignments
                .OrderBy(item => item.CameraId)
                .Select(item => item.CameraId)
                .ToArray(),
            user.LastLoginAt,
            user.LastActivityAt);
}
