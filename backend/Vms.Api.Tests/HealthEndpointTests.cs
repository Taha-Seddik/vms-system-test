using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Vms.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<VmsApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(VmsApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_returns_healthy_status()
    {
        var response = await _client.GetAsync("/health");
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("Healthy", payload.Status);
        Assert.Equal("vms-api", payload.Service);
    }

    private sealed record HealthResponse(string Status, string Service, DateTimeOffset Timestamp);
}
