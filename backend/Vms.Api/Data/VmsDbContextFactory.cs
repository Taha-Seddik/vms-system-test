using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vms.Api.Data;

public sealed class VmsDbContextFactory : IDesignTimeDbContextFactory<VmsDbContext>
{
    public VmsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VmsDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=vms;Username=vms;Password=vms_dev_password")
            .Options;

        return new VmsDbContext(options);
    }
}

