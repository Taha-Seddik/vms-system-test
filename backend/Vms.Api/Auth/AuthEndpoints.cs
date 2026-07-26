using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vms.Api.Authorization;
using Vms.Api.Data;
using Vms.Api.Data.Entities;
using Vms.Api.Domain;

namespace Vms.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();
        group.MapGet("/activity", GetActivityAsync)
            .RequireAuthorization(AppPolicies.AdministratorOnly);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        VmsDbContext database,
        IPasswordHasher<AppUser> hasher,
        JwtTokenService tokens,
        IOptions<JwtOptions> jwtOptions,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["credentials"] = ["Username and password are required."]
            });
        }

        var normalizedUsername = request.Username.Trim().ToUpperInvariant();
        var user = await database.Users
            .Include(item => item.CameraAssignments)
            .SingleOrDefaultAsync(
                item => item.NormalizedUsername == normalizedUsername,
                cancellationToken);

        if (user is null || !user.IsEnabled)
        {
            return Results.Unauthorized();
        }

        var verification = hasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return Results.Unauthorized();
        }

        if (user.Role == AppRole.Viewer && user.CameraAssignments.Count == 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Viewer has no camera assignments.",
                detail: "An administrator must assign at least one camera before this Viewer can sign in.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
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
        database.SystemEvents.Add(new SystemEvent
        {
            Id = Guid.NewGuid(),
            Type = SystemEventType.UserLogin,
            Timestamp = now,
            UserId = user.Id,
            Severity = EventSeverity.Information,
            Description = $"{user.DisplayName} signed in.",
            Status = EventStatus.Closed
        });
        await database.SaveChangesAsync(cancellationToken);

        return Results.Ok(new LoginResponse(
            tokens.CreateToken(user, session),
            session.ExpiresAt,
            ToUserResponse(user)));
    }

    private static async Task<IResult> LogoutAsync(
        ClaimsPrincipal principal,
        VmsDbContext database,
        CancellationToken cancellationToken)
    {
        var sessionId = principal.GetRequiredSessionId();
        var userId = principal.GetRequiredUserId();
        var session = await database.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId,
                cancellationToken);

        if (session is null || session.RevokedAt is not null)
        {
            return Results.NoContent();
        }

        var now = DateTimeOffset.UtcNow;
        session.RevokedAt = now;
        session.RevokedReason = "User logout";
        session.User.LastActivityAt = now;
        database.SystemEvents.Add(new SystemEvent
        {
            Id = Guid.NewGuid(),
            Type = SystemEventType.UserLogout,
            Timestamp = now,
            UserId = userId,
            Severity = EventSeverity.Information,
            Description = $"{session.User.DisplayName} signed out.",
            Status = EventStatus.Closed
        });
        await database.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        VmsDbContext database,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredUserId();
        var user = await database.Users
            .AsNoTracking()
            .Include(item => item.CameraAssignments)
            .SingleAsync(item => item.Id == userId, cancellationToken);

        return Results.Ok(ToUserResponse(user));
    }

    private static async Task<IResult> GetActivityAsync(
        VmsDbContext database,
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

        return Results.Ok(new AuthActivityResponse(activeSessions, events));
    }

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

