using Vms.Api.Models;

namespace Vms.Api.Services;

public interface IStorageMetricsProvider
{
    Task<StorageHealthResponse> GetAsync(CancellationToken cancellationToken);
}
