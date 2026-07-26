using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vms.Api.Data;
using Vms.Api.Data.Entities;

namespace Vms.Api.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddVmsAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetRequiredSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        Validate(jwtOptions);

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetRequiredSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddSingleton<JwtTokenService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateSessionAsync
                };
            });

        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Token principal is unavailable.");
            return;
        }

        var sessionValue = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        var userValue = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sessionValue, out var sessionId) ||
            !Guid.TryParse(userValue, out var userId))
        {
            context.Fail("Required token claims are invalid.");
            return;
        }

        var database = context.HttpContext.RequestServices
            .GetRequiredService<VmsDbContext>();
        var session = await database.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId,
                context.HttpContext.RequestAborted);
        var now = DateTimeOffset.UtcNow;

        if (session is null ||
            session.RevokedAt is not null ||
            session.ExpiresAt <= now ||
            !session.User.IsEnabled)
        {
            context.Fail("Session is no longer active.");
            return;
        }

        if (session.LastActivityAt <= now.AddSeconds(-30))
        {
            session.LastActivityAt = now;
            session.User.LastActivityAt = now;
            await database.SaveChangesAsync(context.HttpContext.RequestAborted);
        }
    }

    private static void Validate(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer) ||
            string.IsNullOrWhiteSpace(options.Audience) ||
            options.SigningKey.Length < 32 ||
            options.AccessTokenMinutes is < 5 or > 1440)
        {
            throw new OptionsValidationException(
                JwtOptions.SectionName,
                typeof(JwtOptions),
                ["JWT issuer, audience, a 32+ character signing key, and a 5–1440 minute lifetime are required."]);
        }
    }
}
