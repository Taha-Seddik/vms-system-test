using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Vms.Api.Data;

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
            services.AddDbContext<VmsDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
