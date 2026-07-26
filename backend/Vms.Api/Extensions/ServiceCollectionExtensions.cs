using System.Text.Json.Serialization;
using Vms.Api.Services;

namespace Vms.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVmsApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .GetChildren()
            .Select(origin => origin.Value)
            .OfType<string>()
            .ToArray();

        services.AddProblemDetails();
        services
            .AddControllers()
            .AddJsonOptions(options =>
                options.JsonSerializerOptions.Converters.Add(
                    new JsonStringEnumConverter()));
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length == 0)
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(allowedOrigins);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        services.AddVmsPersistence(configuration);
        services.AddVmsAuthentication(configuration);
        services.AddVmsAuthorization();

        services.AddScoped<AuthenticationService>();
        services.AddScoped<CameraAccessService>();
        services.AddScoped<SessionValidationService>();
        services.AddSingleton<JwtTokenService>();

        return services;
    }
}
