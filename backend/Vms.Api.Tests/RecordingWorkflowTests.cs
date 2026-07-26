using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Models;
using Vms.Api.Services;
using Xunit;

namespace Vms.Api.Tests;

public sealed class RecordingWorkflowTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public RecordingWorkflowTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Viewer_cannot_start_recordings()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");

        var response = await client.PostAsync(
            "/api/cameras/camera-1/recordings/manual/start",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manual_recording_prevents_conflicts_and_completes()
    {
        await SetCameraOnlineAsync("camera-1");
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var start = await client.PostAsync(
            "/api/cameras/camera-1/recordings/manual/start",
            null);
        var started = await start.Content
            .ReadFromJsonAsync<RecordingCommandResponse>();
        var duplicate = await client.PostAsync(
            "/api/cameras/camera-1/recordings/continuous/start",
            null);
        var stop = await client.PostAsync(
            "/api/cameras/camera-1/recordings/stop",
            null);
        var stopped = await stop.Content
            .ReadFromJsonAsync<RecordingCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        Assert.NotNull(started);
        Assert.Equal(RecordingMode.Manual, started.Recording.Mode);
        Assert.Equal(RecordingState.Recording, started.Recording.State);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        Assert.NotNull(stopped);
        Assert.Equal(RecordingState.Completed, stopped.Recording.State);
        Assert.True(stopped.Recording.FileSizeBytes > 0);
        await AssertCameraStoppedAndEventsAsync("camera-1");
    }

    [Fact]
    public async Task Simulated_motion_creates_event_and_real_recording_metadata()
    {
        await SetCameraOnlineAsync("camera-2");
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var response = await client.PostAsync(
            "/api/cameras/camera-2/motion/simulate",
            null);
        var started = await response.Content
            .ReadFromJsonAsync<RecordingCommandResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(started);
        Assert.Equal(RecordingMode.Event, started.Recording.Mode);
        Assert.NotNull(started.Recording.TriggerEventId);

        var completed = await WaitForRecordingAsync(started.Recording.Id);
        Assert.Equal(RecordingState.Completed, completed.State);
        Assert.True(completed.FileSizeBytes > 0);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        Assert.True(await database.SystemEvents.AnyAsync(item =>
            item.Id == started.Recording.TriggerEventId
            && item.Type == SystemEventType.MotionDetected));
    }

    [Fact]
    public async Task Continuous_recording_creates_multiple_finalized_segments()
    {
        await SetCameraOnlineAsync("camera-3");
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var start = await client.PostAsync(
            "/api/cameras/camera-3/recordings/continuous/start",
            null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        await WaitForCompletedCountAsync("camera-3", expected: 2);
        var stop = await client.PostAsync(
            "/api/cameras/camera-3/recordings/stop",
            null);

        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var segments = await database.Recordings
            .Where(item =>
                item.CameraId == "camera-3"
                && item.Mode == RecordingMode.Continuous)
            .ToListAsync();
        Assert.True(segments.Count >= 2);
        Assert.All(segments, segment =>
        {
            Assert.Equal(RecordingState.Completed, segment.State);
            Assert.True(segment.FileSizeBytes > 0);
        });
    }

    [Fact]
    public async Task Ffmpeg_start_failure_creates_failure_event()
    {
        await SetCameraOnlineAsync("camera-4");
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider
            .GetRequiredService<FakeRecordingProcessRunner>()
            .FailNextStart = true;
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var response = await client.PostAsync(
            "/api/cameras/camera-4/recordings/manual/start",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var assertionScope = _factory.Services.CreateAsyncScope();
        var database = assertionScope.ServiceProvider
            .GetRequiredService<VmsDbContext>();
        Assert.True(await database.Recordings.AnyAsync(item =>
            item.CameraId == "camera-4"
            && item.State == RecordingState.Failed));
        Assert.True(await database.SystemEvents.AnyAsync(item =>
            item.CameraId == "camera-4"
            && item.Type == SystemEventType.RecordingFailure
            && item.Status == EventStatus.Open));
    }

    private async Task SetCameraOnlineAsync(string cameraId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var camera = await database.Cameras.SingleAsync(item => item.Id == cameraId);
        camera.IsEnabled = true;
        camera.ConnectionStatus = CameraConnectionStatus.Online;
        camera.RecordingStatus = CameraRecordingStatus.NotRecording;
        await database.SaveChangesAsync();
    }

    private async Task AssertCameraStoppedAndEventsAsync(string cameraId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        var camera = await database.Cameras.SingleAsync(item => item.Id == cameraId);
        Assert.Equal(CameraRecordingStatus.NotRecording, camera.RecordingStatus);
        Assert.True(await database.SystemEvents.AnyAsync(item =>
            item.CameraId == cameraId
            && item.Type == SystemEventType.RecordingStarted));
        Assert.True(await database.SystemEvents.AnyAsync(item =>
            item.CameraId == cameraId
            && item.Type == SystemEventType.RecordingStopped));
    }

    private async Task<Vms.Api.Domain.Entities.Recording> WaitForRecordingAsync(
        Guid recordingId)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(50);
            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            var recording = await database.Recordings
                .AsNoTracking()
                .SingleAsync(item => item.Id == recordingId);
            if (recording.State != RecordingState.Recording)
            {
                return recording;
            }
        }

        throw new TimeoutException("Recording did not finish in time.");
    }

    private async Task WaitForCompletedCountAsync(
        string cameraId,
        int expected)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(50);
            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            var count = await database.Recordings.CountAsync(item =>
                item.CameraId == cameraId
                && item.Mode == RecordingMode.Continuous
                && item.State == RecordingState.Completed);
            if (count >= expected)
            {
                return;
            }
        }

        throw new TimeoutException("Continuous segments were not finalized.");
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(
        string username,
        string password)
    {
        var client = _factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}
