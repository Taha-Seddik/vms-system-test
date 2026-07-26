using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vms.Api.Data;
using Vms.Api.Services;

namespace Vms.Api.Tests;

public sealed class VmsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"vms-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VmsDatabase"] =
                    "Host=unused;Database=vms_tests;Username=unused;Password=unused"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<VmsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VmsDbContext>>();
            services.RemoveAll<VmsDbContext>();
            services.RemoveAll<ICameraProbe>();
            services.AddDbContext<VmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
            services.AddSingleton<FakeCameraProbe>();
            services.AddSingleton<ICameraProbe>(
                provider => provider.GetRequiredService<FakeCameraProbe>());
        });
    }
}

public sealed class FakeCameraProbe : ICameraProbe
{
    public CameraProbeResult NextResult { get; set; } = Online();

    public Task<CameraProbeResult> ProbeAsync(
        string rtspUrl,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        Task.FromResult(NextResult);

    public static CameraProbeResult Online() =>
        new(
            true,
            TimeSpan.FromMilliseconds(12),
            "h264",
            640,
            360,
            10,
            null);

    public static CameraProbeResult Offline(string error = "Connection refused.") =>
        new(
            false,
            TimeSpan.FromMilliseconds(18),
            null,
            null,
            null,
            null,
            error);
}
