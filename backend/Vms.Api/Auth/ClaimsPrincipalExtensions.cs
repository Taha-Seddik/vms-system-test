using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Vms.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new InvalidOperationException("Authenticated user ID claim is missing.");
    }

    public static Guid GetRequiredSessionId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

        return Guid.TryParse(value, out var sessionId)
            ? sessionId
            : throw new InvalidOperationException("Authenticated session claim is missing.");
    }
}

