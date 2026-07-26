using System.Text.Json.Serialization;
using Vms.Api.Models;
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
        services.AddSignalR();
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

        services
            .AddOptions<CameraMonitoringOptions>()
            .Bind(configuration.GetSection(CameraMonitoringOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services
            .AddOptions<RecordingStorageOptions>()
            .Bind(configuration.GetSection(RecordingStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                options => options.CriticalPercent > options.WarningPercent,
                "Critical storage percentage must be greater than the warning percentage.")
            .ValidateOnStart();
        services
            .AddOptions<RecordingOptions>()
            .Bind(configuration.GetSection(RecordingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<AuthenticationService>();
        services.AddScoped<CameraAccessService>();
        services.AddScoped<CameraGroupService>();
        services.AddScoped<CameraHealthService>();
        services.AddScoped<CameraManagementService>();
        services.AddScoped<CommandCenterService>();
        services.AddScoped<EventService>();
        services.AddScoped<RecordingService>();
        services.AddScoped<SessionValidationService>();
        services.AddScoped<StorageEventService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ApplicationUptime>();
        services.AddSingleton<CameraHealthCheckCoordinator>();
        services.AddSingleton<DashboardUpdatePublisher>();
        services.AddSingleton<IStorageMetricsProvider, FileSystemStorageMetricsProvider>();
        services.AddSingleton<ICameraProbe, FfprobeCameraProbe>();
        services.AddSingleton<IRecordingMediaInspector, FfprobeRecordingMediaInspector>();
        services.AddSingleton<IRecordingKeyframeGenerator, FfmpegRecordingKeyframeGenerator>();
        services.AddSingleton<IRecordingProcessRunner, FfmpegRecordingProcessRunner>();
        services.AddSingleton<RecordingCoordinator>();
        services.AddSingleton<RecordingKeyframeService>();
        services.AddSingleton<RecordingStoragePathResolver>();
        services.AddSingleton<JwtTokenService>();
        services.AddHostedService<CameraHealthMonitor>();
        services.AddHostedService(provider =>
            provider.GetRequiredService<RecordingCoordinator>());
        services.AddHostedService<RecordingKeyframeBackfillService>();
        services.AddHostedService<StorageHealthMonitor>();

        return services;
    }
}
