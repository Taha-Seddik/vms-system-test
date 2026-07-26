using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;
using Vms.Api.Services;
using Xunit;

namespace Vms.Api.Tests;

public sealed class EventManagementTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public EventManagementTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Operator_can_filter_and_open_event_details()
    {
        var eventId = await SeedEventAsync(
            SystemEventType.MotionDetected,
            EventSeverity.Warning,
            EventStatus.Open,
            "camera-1");
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var search = await client.GetFromJsonAsync<EventSearchResponse>(
            "/api/events?cameraId=camera-1&type=MotionDetected&severity=Warning&status=Open");
        var details = await client.GetFromJsonAsync<EventResponse>(
            $"/api/events/{eventId}");

        Assert.NotNull(search);
        var item = Assert.Single(search.Items, item => item.Id == eventId);
        Assert.True(item.IsActiveAlarm);
        Assert.True(item.IsIncident);
        Assert.Equal("Entrance", item.CameraName);
        Assert.NotNull(details);
        Assert.Equal(eventId, details.Id);
        Assert.Equal(SystemEventType.MotionDetected, details.Type);
        Assert.False(string.IsNullOrWhiteSpace(details.Description));
    }

    [Fact]
    public async Task Closing_event_removes_it_from_active_alarms()
    {
        var eventId = await SeedEventAsync(
            SystemEventType.StorageFull,
            EventSeverity.Critical,
            EventStatus.Open);
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");

        var before = await client.GetFromJsonAsync<EventSearchResponse>(
            "/api/events");
        var response = await client.PostAsync(
            $"/api/events/{eventId}/close",
            null);
        var closed = await response.Content.ReadFromJsonAsync<EventResponse>();
        var after = await client.GetFromJsonAsync<EventSearchResponse>(
            "/api/events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(before);
        Assert.NotNull(closed);
        Assert.NotNull(after);
        Assert.Equal(EventStatus.Closed, closed.Status);
        Assert.False(closed.IsActiveAlarm);
        Assert.Equal(before.ActiveAlarmCount - 1, after.ActiveAlarmCount);

        var secondClose = await client.PostAsync(
            $"/api/events/{eventId}/close",
            null);
        Assert.Equal(HttpStatusCode.OK, secondClose.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_read_or_close_events()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");

        var search = await client.GetAsync("/api/events");
        var close = await client.PostAsync(
            $"/api/events/{Guid.NewGuid()}/close",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, close.StatusCode);
    }

    [Fact]
    public async Task Reversed_event_date_filter_is_rejected()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");

        var response = await client.GetAsync(
            "/api/events?from=2026-07-27T00:00:00Z&to=2026-07-26T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public void Assessment_event_types_are_supported()
    {
        SystemEventType[] required =
        [
            SystemEventType.CameraOffline,
            SystemEventType.MotionDetected,
            SystemEventType.RecordingStarted,
            SystemEventType.RecordingStopped,
            SystemEventType.StorageFull,
            SystemEventType.CameraReconnected,
            SystemEventType.UserLogin,
            SystemEventType.UserLogout
        ];

        Assert.All(required, item =>
            Assert.True(Enum.IsDefined(typeof(SystemEventType), item)));
    }

    [Fact]
    public async Task Critical_storage_creates_one_alarm_and_recovery_closes_it()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var metrics = scope.ServiceProvider
            .GetRequiredService<FakeStorageMetricsProvider>();
        var storageEvents = scope.ServiceProvider
            .GetRequiredService<StorageEventService>();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();

        try
        {
            metrics.NextResponse = new StorageHealthResponse(
                "/test/recordings",
                StorageHealthStatus.Critical,
                1_000_000,
                50_000,
                950_000,
                100_000,
                95,
                null);

            await storageEvents.EvaluateAsync(CancellationToken.None);
            await storageEvents.EvaluateAsync(CancellationToken.None);

            var open = await database.SystemEvents
                .Where(item =>
                    item.Type == SystemEventType.StorageFull
                    && item.Status == EventStatus.Open)
                .ToListAsync();
            Assert.Single(open);
            Assert.Equal(EventSeverity.Critical, open[0].Severity);

            metrics.NextResponse = FakeStorageMetricsProvider.Healthy();
            await storageEvents.EvaluateAsync(CancellationToken.None);

            Assert.False(await database.SystemEvents.AnyAsync(item =>
                item.Type == SystemEventType.StorageFull
                && item.Status == EventStatus.Open));
        }
        finally
        {
            metrics.NextResponse = FakeStorageMetricsProvider.Healthy();
        }
    }

    private async Task<Guid> SeedEventAsync(
        SystemEventType type,
        EventSeverity severity,
        EventStatus status,
        string? cameraId = null)
    {
        var id = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        database.SystemEvents.Add(new SystemEvent
        {
            Id = id,
            Type = type,
            Timestamp = DateTimeOffset.UtcNow,
            CameraId = cameraId,
            Severity = severity,
            Description = $"Step 8 verification event {id}.",
            Status = status
        });
        await database.SaveChangesAsync();
        return id;
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
