using Vms.Api.Data;
using Vms.Api.Extensions;
using Vms.Api.Hubs;

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
builder.Services.AddVmsApplication(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

await DatabaseInitializer.InitializeAsync(app.Services);

app.MapControllers();
app.MapHub<CommandCenterHub>("/hubs/command-center");

app.Run();
return 0;

public partial class Program;
