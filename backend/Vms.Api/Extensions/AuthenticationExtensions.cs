using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Vms.Api.Data;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;
using Vms.Api.Services;

namespace Vms.Api.Extensions;

public static class AuthenticationExtensions
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
            .ValidateOnStart();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<VmsDbContext>()
            .AddDefaultTokenProviders();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters =
                    CreateTokenValidationParameters(jwtOptions);

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrWhiteSpace(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments(
                                "/hubs/command-center"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = ValidateSessionAsync
                };
            });

        return services;
    }

    public static TokenValidationParameters CreateTokenValidationParameters(
        JwtOptions options) =>
        new()
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };

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

        var validator = context.HttpContext.RequestServices
            .GetRequiredService<SessionValidationService>();
        var isValid = await validator.ValidateAsync(
            userId,
            sessionId,
            context.HttpContext.RequestAborted);

        if (!isValid)
        {
            context.Fail("Session is no longer active.");
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
                ["JWT issuer, audience, a 32+ character signing key, and a 5-1440 minute lifetime are required."]);
        }
    }
}
