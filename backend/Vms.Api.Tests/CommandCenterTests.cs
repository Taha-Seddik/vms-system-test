using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Models;
using Xunit;

namespace Vms.Api.Tests;

public sealed class CommandCenterTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public CommandCenterTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Operator_receives_complete_command_center_snapshot()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");
        await SeedOperationalStateAsync();

        var response = await client.GetAsync("/api/command-center");
        var dashboard = await response.Content
            .ReadFromJsonAsync<CommandCenterResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(4, dashboard.Metrics.TotalCameras);
        Assert.Equal(1, dashboard.Metrics.OnlineCameras);
        Assert.Equal(1, dashboard.Metrics.OfflineCameras);
        Assert.Equal(1, dashboard.Metrics.DisabledCameras);
        Assert.Equal(1, dashboard.Metrics.ActiveLiveStreams);
        Assert.Equal(1, dashboard.Metrics.ActiveRecordings);
        Assert.True(dashboard.Metrics.ActiveUsers >= 1);
        Assert.Equal(StorageHealthStatus.Healthy, dashboard.Storage.Status);
        Assert.Equal(40, dashboard.Storage.UsedPercent);
        Assert.Single(dashboard.OfflineCameras);
        Assert.Contains(dashboard.RecordingFailures, item =>
            item.Type == SystemEventType.RecordingFailure);
        Assert.Contains(dashboard.ActiveAlarms, item =>
            item.Status == EventStatus.Open
            && item.Severity == EventSeverity.Critical);
        Assert.Contains(dashboard.RecentIncidents, item =>
            item.Type == SystemEventType.CameraOffline);
        Assert.Contains(dashboard.OperatorActivity, item =>
            item.Type == SystemEventType.UserLogin);
    }

    [Fact]
    public async Task Viewer_cannot_access_command_center()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");

        var response = await client.GetAsync("/api/command-center");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SignalR_negotiate_accepts_operator_query_token_and_rejects_anonymous()
    {
        using var authenticatedClient = _factory.CreateClient();
        var login = await LoginAsync(
            authenticatedClient,
            "operator",
            "Operator123!");

        var authenticatedResponse = await authenticatedClient.PostAsync(
            $"/hubs/command-center/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(login.AccessToken)}",
            null);

        using var anonymousClient = _factory.CreateClient();
        var anonymousResponse = await anonymousClient.PostAsync(
            "/hubs/command-center/negotiate?negotiateVersion=1",
            null);

        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    private async Task SeedOperationalStateAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var cameras = await database.Cameras.OrderBy(item => item.Id).ToListAsync();

        cameras[0].ConnectionStatus = CameraConnectionStatus.Online;
        cameras[0].RecordingStatus = CameraRecordingStatus.Recording;
        cameras[0].LastHeartbeatAt = DateTimeOffset.UtcNow;
        cameras[1].ConnectionStatus = CameraConnectionStatus.Offline;
        cameras[1].LastConnectionError = "Connection refused.";
        cameras[2].ConnectionStatus = CameraConnectionStatus.Disabled;
        cameras[2].IsEnabled = false;
        cameras[3].ConnectionStatus = CameraConnectionStatus.Unknown;

        var timestamp = DateTimeOffset.UtcNow;
        database.SystemEvents.AddRange(
            new Vms.Api.Domain.Entities.SystemEvent
            {
                Id = Guid.NewGuid(),
                Type = SystemEventType.CameraOffline,
                Timestamp = timestamp,
                CameraId = cameras[1].Id,
                Severity = EventSeverity.Warning,
                Description = "Loading Bay is offline.",
                Status = EventStatus.Open
            },
            new Vms.Api.Domain.Entities.SystemEvent
            {
                Id = Guid.NewGuid(),
                Type = SystemEventType.RecordingFailure,
                Timestamp = timestamp.AddSeconds(1),
                CameraId = cameras[0].Id,
                Severity = EventSeverity.Critical,
                Description = "Entrance recording failed.",
                Status = EventStatus.Open
            });
        await database.SaveChangesAsync();
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string username,
        string password)
    {
        var client = _factory.CreateClient();
        var login = await LoginAsync(client, username, password);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }

    private static async Task<LoginResponse> LoginAsync(
        HttpClient client,
        string username,
        string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
