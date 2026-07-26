using System.Text.Json.Serialization;
using Vms.Api.Auth;
using Vms.Api.Authorization;
using Vms.Api.Cameras;
using Vms.Api.Data;

if (args.Contains("--health-check", StringComparer.Ordinal))
{
    var healthUrl = Environment.GetEnvironmentVariable("HEALTH_CHECK_URL")
        ?? "http://127.0.0.1:8080/health";

    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync(healthUrl);
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .OfType<string>()
    .ToArray();

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddCors(options =>
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
builder.Services.AddVmsData(builder.Configuration);
builder.Services.AddVmsAuthentication(builder.Configuration);
builder.Services.AddVmsAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

await DatabaseInitializer.InitializeAsync(app.Services);

app.MapGet("/", () => Results.Ok(new
{
    service = "VMS API",
    status = "ready",
    step = 2
}));

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "vms-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "Video Management System",
    foundation = "ASP.NET Core, React, PostgreSQL, MediaMTX, FFmpeg",
    implementedStep = 2
}));

app.MapAuthEndpoints();
app.MapAccessibleCameraEndpoints();
app.MapGet("/api/access/admin", () => Results.Ok(new
    {
        message = "Administrator access granted."
    }))
    .RequireAuthorization(AppPolicies.AdministratorOnly);
app.MapGet("/api/access/operator", () => Results.Ok(new
    {
        message = "Operator access granted."
    }))
    .RequireAuthorization(AppPolicies.OperatorOrAdministrator);

app.Run();
return 0;

public partial class Program;
