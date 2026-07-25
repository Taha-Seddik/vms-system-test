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

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();

app.MapGet("/", () => Results.Ok(new
{
    service = "VMS API",
    status = "ready",
    step = 1
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
    implementedStep = 1
}));

app.Run();
return 0;

public partial class Program;
