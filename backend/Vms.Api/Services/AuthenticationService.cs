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
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    JwtTokenService tokenService,
    DashboardUpdatePublisher dashboardUpdates,
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

        var user = await userManager.FindByNameAsync(request.Username.Trim());
        if (user is null || !user.IsEnabled)
        {
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return new LoginResult(null, LoginFailure.InvalidCredentials);
        }

        await database.Entry(user)
            .Collection(item => item.CameraAssignments)
            .LoadAsync(cancellationToken);
        var role = await GetRequiredRoleAsync(user);

        if (role == AppRole.Viewer && user.CameraAssignments.Count == 0)
        {
            return new LoginResult(null, LoginFailure.ViewerHasNoAssignments);
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
        database.AuditLogs.Add(CreateAuthenticationAudit(
            user,
            session.Id,
            "Login",
            $"{user.DisplayName} signed in.",
            now));
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync("user-login", cancellationToken);

        var response = new LoginResponse(
            tokenService.CreateToken(user, role, session),
            session.ExpiresAt,
            ToUserResponse(user, role));

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
        database.AuditLogs.Add(CreateAuthenticationAudit(
            session.User,
            session.Id,
            "Logout",
            $"{session.User.DisplayName} signed out.",
            now));
        await database.SaveChangesAsync(cancellationToken);
        await dashboardUpdates.PublishAsync("user-logout", cancellationToken);
    }

    public async Task<AuthenticatedUserResponse> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await database.Users
            .AsNoTracking()
            .Include(item => item.CameraAssignments)
            .SingleAsync(item => item.Id == userId, cancellationToken);
        var role = await GetRequiredRoleAsync(user);

        return ToUserResponse(user, role);
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

    private async Task<AppRole> GetRequiredRoleAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count != 1 || !Enum.TryParse<AppRole>(roles[0], out var role))
        {
            throw new InvalidOperationException(
                $"User '{user.UserName}' must have exactly one valid VMS role.");
        }

        return role;
    }

    private static SystemEvent CreateActivityEvent(
        ApplicationUser user,
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

    private static AuthenticatedUserResponse ToUserResponse(
        ApplicationUser user,
        AppRole role) =>
        new(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            role,
            user.CameraAssignments
                .OrderBy(item => item.CameraId)
                .Select(item => item.CameraId)
                .ToArray(),
            user.LastLoginAt,
            user.LastActivityAt);

    private static AuditLog CreateAuthenticationAudit(
        ApplicationUser user,
        Guid sessionId,
        string action,
        string description,
        DateTimeOffset timestamp) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp,
            UserId = user.Id,
            ActorUsername = user.UserName ?? string.Empty,
            Action = action,
            ResourceType = "Session",
            ResourceId = sessionId.ToString(),
            Description = description
        };
}
