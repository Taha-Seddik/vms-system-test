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

public sealed class CameraManagementTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public CameraManagementTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_can_manage_groups_and_complete_camera_crud()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");

        var groupResponse = await client.PostAsJsonAsync(
            "/api/camera-groups",
            new CreateCameraGroupRequest("Temporary", "Test group"));
        Assert.Equal(HttpStatusCode.Created, groupResponse.StatusCode);
        var group = await groupResponse.Content
            .ReadFromJsonAsync<CameraGroupResponse>();
        Assert.NotNull(group);

        var createResponse = await client.PostAsJsonAsync(
            "/api/cameras",
            new CreateCameraRequest(
                "camera-test",
                "Test Camera",
                "Test location",
                "rtsp://mediamtx:8554/camera-1",
                "/camera-test/index.m3u8",
                group.Id));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var updateResponse = await client.PutAsJsonAsync(
            "/api/cameras/camera-test",
            new UpdateCameraRequest(
                "Updated Camera",
                "Updated location",
                "rtsp://mediamtx:8554/camera-2",
                "/camera-test/index.m3u8",
                group.Id));
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<ManagedCameraResponse>();
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Updated Camera", updated?.Name);

        var disabledResponse = await client.PatchAsJsonAsync(
            "/api/cameras/camera-test/enabled",
            new SetCameraEnabledRequest(false));
        var disabled = await disabledResponse.Content
            .ReadFromJsonAsync<ManagedCameraResponse>();
        Assert.Equal(CameraConnectionStatus.Disabled, disabled?.ConnectionStatus);

        var deleteResponse = await client.DeleteAsync(
            "/api/cameras/camera-test");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var deleteGroupResponse = await client.DeleteAsync(
            $"/api/camera-groups/{group.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteGroupResponse.StatusCode);
    }

    [Fact]
    public async Task Operator_can_test_connection_but_cannot_mutate_cameras()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var testResponse = await client.PostAsync(
            "/api/cameras/camera-1/test-connection",
            null);
        var test = await testResponse.Content
            .ReadFromJsonAsync<CameraConnectionTestResponse>();
        var createResponse = await client.PostAsJsonAsync(
            "/api/cameras",
            new CreateCameraRequest(
                "not-allowed",
                "Not allowed",
                "Restricted",
                "rtsp://mediamtx:8554/camera-1",
                "/not-allowed/index.m3u8",
                null));

        Assert.Equal(HttpStatusCode.OK, testResponse.StatusCode);
        Assert.True(test?.Succeeded);
        Assert.Equal("640x360", test?.Resolution);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
    }

    [Fact]
    public async Task Health_transitions_persist_offline_and_reconnected_events()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        var probe = _factory.Services.GetRequiredService<FakeCameraProbe>();

        probe.NextResult = FakeCameraProbe.Offline();
        var offlineResponse = await client.PostAsync(
            "/api/cameras/camera-3/test-connection",
            null);
        offlineResponse.EnsureSuccessStatusCode();

        probe.NextResult = FakeCameraProbe.Online();
        var onlineResponse = await client.PostAsync(
            "/api/cameras/camera-3/test-connection",
            null);
        onlineResponse.EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var camera = await database.Cameras.SingleAsync(
            item => item.Id == "camera-3");
        var events = await database.SystemEvents
            .Where(item => item.CameraId == "camera-3")
            .OrderBy(item => item.Timestamp)
            .ToListAsync();

        Assert.Equal(CameraConnectionStatus.Online, camera.ConnectionStatus);
        Assert.NotNull(camera.LastHeartbeatAt);
        Assert.Contains(events, item =>
            item.Type == SystemEventType.CameraOffline
            && item.Status == EventStatus.Open);
        Assert.Contains(events, item =>
            item.Type == SystemEventType.CameraReconnected
            && item.Status == EventStatus.Closed);
    }

    [Fact]
    public async Task Invalid_rtsp_configuration_is_rejected()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");

        var response = await client.PostAsJsonAsync(
            "/api/cameras",
            new CreateCameraRequest(
                "unsafe-camera",
                "Unsafe Camera",
                "Unknown",
                "https://example.test/video",
                "/unsafe/index.m3u8",
                null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string username,
        string password)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login.AccessToken);
        return client;
    }
}
