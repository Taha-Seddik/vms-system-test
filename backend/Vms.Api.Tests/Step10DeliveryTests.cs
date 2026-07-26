using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Models;
using Xunit;

namespace Vms.Api.Tests;

public sealed class Step10DeliveryTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public Step10DeliveryTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OpenApi_document_and_Swagger_UI_are_available()
    {
        using var client = _factory.CreateClient();

        var document = await client.GetAsync("/openapi/v1.json");
        var contents = await document.Content.ReadAsStringAsync();
        var ui = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
        Assert.Contains("/api/cameras", contents);
        Assert.Contains("Bearer", contents);
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
    }

    [Fact]
    public async Task Media_authorization_enforces_active_session_and_assignment()
    {
        using var anonymousClient = _factory.CreateClient();
        var missingToken = await AuthorizeMediaAsync(
            anonymousClient,
            null,
            "camera-1",
            "hls");
        Assert.Equal(HttpStatusCode.Unauthorized, missingToken.StatusCode);

        var (viewerClient, viewerToken) = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");
        using (viewerClient)
        {
            var assigned = await AuthorizeMediaAsync(
                viewerClient,
                viewerToken,
                "camera-1",
                "hls");
            var unassigned = await AuthorizeMediaAsync(
                viewerClient,
                viewerToken,
                "camera-3",
                "hls");

            Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, unassigned.StatusCode);

            var logout = await viewerClient.PostAsync("/api/auth/logout", null);
            logout.EnsureSuccessStatusCode();
            var revoked = await AuthorizeMediaAsync(
                viewerClient,
                viewerToken,
                "camera-1",
                "hls");
            Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        }

        var (operatorClient, operatorToken) = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");
        using (operatorClient)
        {
            var allowed = await AuthorizeMediaAsync(
                operatorClient,
                operatorToken,
                "camera-4",
                "hls");
            Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        }

        var internalRtsp = await AuthorizeMediaAsync(
            anonymousClient,
            null,
            "camera-1",
            "rtsp");
        Assert.Equal(HttpStatusCode.NoContent, internalRtsp.StatusCode);
    }

    [Fact]
    public async Task Camera_credentials_are_redacted_and_redacted_update_is_safe()
    {
        var (client, _) = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        using (client)
        {
            const string source =
                "rtsp://camera-user:camera-secret@mediamtx:8554/camera-1";
            var create = await client.PostAsJsonAsync(
                "/api/cameras",
                new CreateCameraRequest(
                    "camera-redaction-test",
                    "Credential Test",
                    "Secure lab",
                    source,
                    "/camera-redaction-test/index.m3u8",
                    null,
                    false));
            create.EnsureSuccessStatusCode();
            var created =
                (await create.Content.ReadFromJsonAsync<ManagedCameraResponse>())!;

            Assert.DoesNotContain("camera-user", created.RtspUrl);
            Assert.DoesNotContain("camera-secret", created.RtspUrl);
            Assert.Contains("***", created.RtspUrl);

            var update = await client.PutAsJsonAsync(
                "/api/cameras/camera-redaction-test",
                new UpdateCameraRequest(
                    "Credential Test Updated",
                    "Secure lab",
                    created.RtspUrl,
                    created.HlsUrl,
                    null));
            update.EnsureSuccessStatusCode();

            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            var stored = await database.Cameras
                .AsNoTracking()
                .SingleAsync(item => item.Id == "camera-redaction-test");
            Assert.Equal(source, stored.RtspUrl);

            var delete = await client.DeleteAsync(
                "/api/cameras/camera-redaction-test");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }
    }

    [Fact]
    public async Task Unsafe_HLS_paths_are_rejected()
    {
        var (client, _) = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        using (client)
        {
            var response = await client.PostAsJsonAsync(
                "/api/cameras",
                new CreateCameraRequest(
                    "unsafe-hls-test",
                    "Unsafe HLS Test",
                    "Secure lab",
                    "rtsp://mediamtx:8554/camera-1",
                    "/../escape/index.m3u8",
                    null,
                    false));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Command_center_exposes_live_wall_HLS_paths()
    {
        var (client, _) = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        using (client)
        {
            var dashboard = await client.GetFromJsonAsync<CommandCenterResponse>(
                "/api/command-center");

            Assert.NotNull(dashboard);
            Assert.Equal(4, dashboard.CameraHealth.Count);
            Assert.All(
                dashboard.CameraHealth,
                camera => Assert.EndsWith(
                    "/index.m3u8",
                    camera.HlsUrl,
                    StringComparison.Ordinal));
        }
    }

    private static Task<HttpResponseMessage> AuthorizeMediaAsync(
        HttpClient client,
        string? token,
        string path,
        string protocol) =>
        client.PostAsJsonAsync(
            "/api/media/authorize",
            new MediaAuthorizationRequest(
                null,
                null,
                token,
                "127.0.0.1",
                "read",
                path,
                protocol,
                "test",
                null,
                "integration-test"));

    private async Task<(HttpClient Client, string Token)>
        CreateAuthenticatedClientAsync(
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
        return (client, login.AccessToken);
    }
}
