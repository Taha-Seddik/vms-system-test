using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Extensions;
using Vms.Api.Models;

namespace Vms.Api.Services;

public sealed class MediaAuthorizationService(
    VmsDbContext database,
    SessionValidationService sessions,
    IOptions<JwtOptions> jwtOptions)
{
    private readonly TokenValidationParameters _validationParameters =
        AuthenticationExtensions.CreateTokenValidationParameters(jwtOptions.Value);

    public async Task<bool> AuthorizeAsync(
        MediaAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Action, "read", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // RTSP is reachable only inside the Docker network. Camera probes and
        // recording workers use it as a trusted service-to-service transport.
        if (string.Equals(request.Protocol, "rtsp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.Equals(request.Protocol, "hls", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(request.Path)
            || string.IsNullOrWhiteSpace(request.Token))
        {
            return false;
        }

        var principal = ValidateToken(request.Token);
        if (principal is null
            || !TryGetGuidClaim(
                principal,
                JwtRegisteredClaimNames.Sub,
                ClaimTypes.NameIdentifier,
                out var userId)
            || !TryGetGuidClaim(
                principal,
                JwtRegisteredClaimNames.Jti,
                null,
                out var sessionId)
            || !await sessions.ValidateAsync(
                userId,
                sessionId,
                cancellationToken))
        {
            return false;
        }

        var roleName = await (
            from userRole in database.UserRoles.AsNoTracking()
            join role in database.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == userId
            select role.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (roleName is nameof(AppRole.Administrator) or nameof(AppRole.Operator))
        {
            return await database.Cameras
                .AsNoTracking()
                .AnyAsync(
                    camera => camera.Id == request.Path && camera.IsEnabled,
                    cancellationToken);
        }

        return roleName == nameof(AppRole.Viewer)
            && await database.UserCameraAssignments
                .AsNoTracking()
                .AnyAsync(
                    assignment =>
                        assignment.UserId == userId
                        && assignment.CameraId == request.Path
                        && assignment.Camera.IsEnabled,
                    cancellationToken);
    }

    private ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            return handler.ValidateToken(
                token,
                _validationParameters,
                out _);
        }
        catch (Exception exception)
            when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    private static bool TryGetGuidClaim(
        ClaimsPrincipal principal,
        string claimType,
        string? fallbackClaimType,
        out Guid value)
    {
        var claim = principal.FindFirstValue(claimType)
            ?? (fallbackClaimType is null
                ? null
                : principal.FindFirstValue(fallbackClaimType));
        return Guid.TryParse(claim, out value);
    }
}
