using Microsoft.EntityFrameworkCore;

namespace Vms.Api.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddVmsData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("VmsDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'VmsDatabase' is not configured.");

        services.AddDbContext<VmsDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}

