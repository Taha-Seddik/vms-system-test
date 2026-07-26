using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;
using Xunit;

namespace Vms.Api.Tests;

public sealed class PlaybackWorkflowTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public PlaybackWorkflowTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Viewer_cannot_browse_or_play_recordings()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");

        var list = await client.GetAsync("/api/recordings");
        var media = await client.GetAsync(
            $"/api/recordings/{Guid.NewGuid()}/media");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, media.StatusCode);
    }

    [Fact]
    public async Task Operator_can_filter_play_download_and_open_keyframes()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");
        var recordingId = await SeedCompletedRecordingAsync(durationSeconds: 65);

        var filtered = await client.GetFromJsonAsync<RecordingResponse[]>(
            "/api/recordings?cameraId=camera-1"
            + "&mode=Manual&state=Completed"
            + "&from=2026-07-26T00%3A00%3A00Z"
            + "&to=2026-07-27T00%3A00%3A00Z");
        var details = await client.GetFromJsonAsync<RecordingDetailsResponse>(
            $"/api/recordings/{recordingId}");

        Assert.NotNull(filtered);
        Assert.Contains(filtered, item => item.Id == recordingId);
        Assert.NotNull(details);
        Assert.Equal([0, 30, 60], details.Keyframes
            .Select(item => item.TimestampSeconds)
            .ToArray());

        using var mediaRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/recordings/{recordingId}/media");
        mediaRequest.Headers.Range = new RangeHeaderValue(0, 3);
        using var media = await client.SendAsync(mediaRequest);
        Assert.Equal(HttpStatusCode.PartialContent, media.StatusCode);
        Assert.Equal("video/mp4", media.Content.Headers.ContentType?.MediaType);

        using var download = await client.GetAsync(
            $"/api/recordings/{recordingId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(
            "attachment",
            download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(
            ".mp4",
            download.Content.Headers.ContentDisposition?.FileNameStar);

        foreach (var keyframe in details.Keyframes)
        {
            using var image = await client.GetAsync(
                $"/api/recordings/{recordingId}/keyframes/{keyframe.Id}");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal(
                "image/jpeg",
                image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(
                [0xff, 0xd8, 0xff, 0xd9],
                await image.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task Invalid_date_range_and_incomplete_media_are_rejected()
    {
        using var client = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");
        var failedId = await SeedFailedRecordingAsync();

        var dates = await client.GetAsync(
            "/api/recordings"
            + "?from=2026-07-27T00%3A00%3A00Z"
            + "&to=2026-07-26T00%3A00%3A00Z");
        var media = await client.GetAsync(
            $"/api/recordings/{failedId}/media");

        Assert.Equal(HttpStatusCode.BadRequest, dates.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, media.StatusCode);
    }

    private async Task<Guid> SeedCompletedRecordingAsync(double durationSeconds)
    {
        var id = Guid.NewGuid();
        var fileName = $"{id:N}.mp4";
        Directory.CreateDirectory(_factory.RecordingPath);
        await File.WriteAllBytesAsync(
            Path.Combine(_factory.RecordingPath, fileName),
            [0, 0, 0, 24, 102, 116, 121, 112, 1, 2, 3, 4]);

        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        database.Recordings.Add(new Recording
        {
            Id = id,
            CameraId = "camera-1",
            Mode = RecordingMode.Manual,
            State = RecordingState.Completed,
            FileName = fileName,
            StartedAt = new DateTimeOffset(2026, 7, 26, 12, 0, 0, TimeSpan.Zero),
            EndedAt = new DateTimeOffset(2026, 7, 26, 12, 1, 5, TimeSpan.Zero),
            DurationSeconds = durationSeconds,
            FileSizeBytes = 12,
            StartedByUserId = Guid.NewGuid()
        });
        await database.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedFailedRecordingAsync()
    {
        var id = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
        database.Recordings.Add(new Recording
        {
            Id = id,
            CameraId = "camera-1",
            Mode = RecordingMode.Manual,
            State = RecordingState.Failed,
            FileName = $"{id:N}.mp4",
            StartedAt = DateTimeOffset.UtcNow,
            EndedAt = DateTimeOffset.UtcNow,
            DurationSeconds = 0,
            FileSizeBytes = 0,
            FailureReason = "Test failure.",
            StartedByUserId = Guid.NewGuid()
        });
        await database.SaveChangesAsync();
        return id;
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
