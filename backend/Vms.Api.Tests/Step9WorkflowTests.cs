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

public sealed class Step9WorkflowTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public Step9WorkflowTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Administrator_manages_user_role_password_and_assignments()
    {
        using var admin = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        var username = $"step9-{Guid.NewGuid():N}"[..18];

        var createResponse = await admin.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                username,
                "Step 9 Viewer",
                "Viewer123!",
                AppRole.Viewer,
                ["camera-1"]));
        var created = await createResponse.Content
            .ReadFromJsonAsync<ManagedUserResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal(AppRole.Viewer, created.Role);
        Assert.Single(created.AssignedCameras);

        using var viewer = await CreateAuthenticatedClientAsync(
            username,
            "Viewer123!");
        var cameras = await viewer.GetFromJsonAsync<AccessibleCameraResponse[]>(
            "/api/cameras/accessible");
        var camera = Assert.Single(cameras!);
        Assert.Equal("camera-1", camera.Id);

        var updateResponse = await admin.PutAsJsonAsync(
            $"/api/users/{created.Id}",
            new UpdateUserRequest(
                "Step 9 Operator",
                AppRole.Operator,
                true,
                [],
                "Changed123!"));
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<ManagedUserResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(AppRole.Operator, updated.Role);
        Assert.Empty(updated.AssignedCameras);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await viewer.GetAsync("/api/auth/me")).StatusCode);

        using var changed = await CreateAuthenticatedClientAsync(
            username,
            "Changed123!");
        var current = await changed.GetFromJsonAsync<AuthenticatedUserResponse>(
            "/api/auth/me");
        Assert.Equal(AppRole.Operator, current!.Role);

        var deleteResponse = await admin.DeleteAsync(
            $"/api/users/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Viewer_requires_assignment_and_cannot_manage_users()
    {
        using var admin = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        var invalid = await admin.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                $"unassigned-{Guid.NewGuid():N}"[..20],
                "Unassigned Viewer",
                "Viewer123!",
                AppRole.Viewer,
                []));

        using var viewer = await CreateAuthenticatedClientAsync(
            "viewer",
            "Viewer123!");
        var users = await viewer.GetAsync("/api/users");
        var audit = await viewer.GetAsync("/api/audit-logs");
        var search = await viewer.GetAsync("/api/search");

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, audit.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, search.StatusCode);
    }

    [Fact]
    public async Task Search_applies_group_status_event_and_role_visibility()
    {
        Guid groupId;
        Guid eventId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            groupId = await database.Cameras
                .Where(item => item.Id == "camera-1")
                .Select(item => item.GroupId!.Value)
                .SingleAsync();
            eventId = Guid.NewGuid();
            database.SystemEvents.Add(new SystemEvent
            {
                Id = eventId,
                Type = SystemEventType.MotionDetected,
                Timestamp = DateTimeOffset.UtcNow,
                CameraId = "camera-1",
                Severity = EventSeverity.Warning,
                Description = "Step 9 entrance search event.",
                Status = EventStatus.Open
            });
            await database.SaveChangesAsync();
        }

        using var admin = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        var result = await admin.GetFromJsonAsync<GlobalSearchResponse>(
            $"/api/search?q=entrance&cameraGroupId={groupId}&status=Open&eventType=MotionDetected");

        Assert.NotNull(result);
        Assert.Empty(result.Cameras);
        Assert.Empty(result.Recordings);
        Assert.Contains(result.Events, item => item.Id == eventId);
        Assert.Empty(result.Users);

        using var operatorClient = await CreateAuthenticatedClientAsync(
            "operator",
            "Operator123!");
        var operatorResult = await operatorClient
            .GetFromJsonAsync<GlobalSearchResponse>("/api/search?q=admin");
        Assert.NotNull(operatorResult);
        Assert.Empty(operatorResult.Users);
    }

    [Fact]
    public async Task Successful_writes_and_authentication_are_audited()
    {
        using var admin = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");

        var probe = await admin.PostAsync(
            "/api/cameras/camera-1/test-connection",
            null);
        probe.EnsureSuccessStatusCode();

        var logs = await admin.GetFromJsonAsync<AuditLogSearchResponse>(
            "/api/audit-logs?take=200");

        Assert.NotNull(logs);
        Assert.Contains(logs.Items, item =>
            item.ActorUsername == "admin"
            && item.Action == "Login"
            && item.ResourceType == "Session");
        Assert.Contains(logs.Items, item =>
            item.ActorUsername == "admin"
            && item.Action == "Executed"
            && item.ResourceType == "Camera"
            && item.ResourceId == "camera-1");
    }

    [Fact]
    public async Task Administrator_cannot_remove_own_authority()
    {
        using var admin = await CreateAuthenticatedClientAsync(
            "admin",
            "Admin123!");
        var current = await admin.GetFromJsonAsync<AuthenticatedUserResponse>(
            "/api/auth/me");

        var update = await admin.PutAsJsonAsync(
            $"/api/users/{current!.Id}",
            new UpdateUserRequest(
                current.DisplayName,
                AppRole.Viewer,
                false,
                ["camera-1"],
                null));
        var delete = await admin.DeleteAsync($"/api/users/{current.Id}");

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
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
