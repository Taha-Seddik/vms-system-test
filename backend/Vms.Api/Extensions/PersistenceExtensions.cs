using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;

namespace Vms.Api.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddVmsPersistence(
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
