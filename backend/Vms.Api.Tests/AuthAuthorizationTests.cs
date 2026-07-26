using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vms.Api.Data;
using Vms.Api.Domain;
using Vms.Api.Domain.Entities;
using Vms.Api.Models;
using Xunit;

namespace Vms.Api.Tests;

public sealed class AuthAuthorizationTests : IClassFixture<VmsApiFactory>
{
    private readonly VmsApiFactory _factory;

    public AuthAuthorizationTests(VmsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Protected_endpoint_rejects_anonymous_requests()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_receives_all_cameras_and_admin_access()
    {
        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "admin", "Admin123!");
        SetBearerToken(client, login.AccessToken);

        var cameras = await client.GetFromJsonAsync<AccessibleCamera[]>(
            "/api/cameras/accessible");
        var adminResponse = await client.GetAsync("/api/access/admin");

        Assert.NotNull(cameras);
        Assert.Equal(4, cameras.Length);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        Assert.Equal(AppRole.Administrator, login.User.Role);
    }

    [Fact]
    public async Task Operator_cannot_use_administrator_endpoint()
    {
        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "operator", "Operator123!");
        SetBearerToken(client, login.AccessToken);

        var adminResponse = await client.GetAsync("/api/access/admin");
        var operatorResponse = await client.GetAsync("/api/access/operator");
        var cameras = await client.GetFromJsonAsync<AccessibleCamera[]>(
            "/api/cameras/accessible");

        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, operatorResponse.StatusCode);
        Assert.Equal(4, cameras?.Length);
    }

    [Fact]
    public async Task Viewer_receives_only_assigned_cameras()
    {
        using var client = _factory.CreateClient();
        var login = await LoginAsync(client, "viewer", "Viewer123!");
        SetBearerToken(client, login.AccessToken);

        var cameras = await client.GetFromJsonAsync<AccessibleCamera[]>(
            "/api/cameras/accessible");
        var operatorResponse = await client.GetAsync("/api/access/operator");

        Assert.NotNull(cameras);
        Assert.Equal(["camera-1", "camera-2"], cameras.Select(item => item.Id));
        Assert.Equal(HttpStatusCode.Forbidden, operatorResponse.StatusCode);
        Assert.Equal(["camera-1", "camera-2"], login.User.AssignedCameraIds);
    }

    [Fact]
    public async Task Logout_revokes_token_and_creates_activity_events()
    {
        using var viewerClient = _factory.CreateClient();
        var viewerLogin = await LoginAsync(viewerClient, "viewer", "Viewer123!");
        SetBearerToken(viewerClient, viewerLogin.AccessToken);

        var logoutResponse = await viewerClient.PostAsync("/api/auth/logout", null);
        var afterLogoutResponse = await viewerClient.GetAsync("/api/auth/me");

        using var adminClient = _factory.CreateClient();
        var adminLogin = await LoginAsync(adminClient, "admin", "Admin123!");
        SetBearerToken(adminClient, adminLogin.AccessToken);
        var activity = await adminClient.GetFromJsonAsync<AuthActivityResponse>(
            "/api/auth/activity");

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogoutResponse.StatusCode);
        Assert.NotNull(activity);
        Assert.Contains(activity.RecentEvents, item => item.Type == SystemEventType.UserLogin);
        Assert.Contains(activity.RecentEvents, item => item.Type == SystemEventType.UserLogout);
    }

    [Fact]
    public async Task Viewer_without_assignments_cannot_sign_in()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            var assignments = await database.UserCameraAssignments
                .Where(item => item.UserId == DemoIdentityData.ViewerId)
                .ToListAsync();
            database.UserCameraAssignments.RemoveRange(assignments);
            await database.SaveChangesAsync();
        }

        try
        {
            using var client = _factory.CreateClient();
            var response = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("viewer", "Viewer123!"));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<VmsDbContext>();
            database.UserCameraAssignments.AddRange(
                new UserCameraAssignment
                {
                    UserId = DemoIdentityData.ViewerId,
                    CameraId = "camera-1",
                    AssignedAt = DateTimeOffset.UtcNow
                },
                new UserCameraAssignment
                {
                    UserId = DemoIdentityData.ViewerId,
                    CameraId = "camera-2",
                    AssignedAt = DateTimeOffset.UtcNow
                });
            await database.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Invalid_password_is_rejected()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("admin", "not-the-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Blank_credentials_are_rejected_as_invalid_input()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(" ", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_roles_are_managed_by_aspnet_core_identity()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var viewer = await userManager.FindByNameAsync("viewer");

        Assert.NotNull(viewer);
        Assert.True(await roleManager.RoleExistsAsync(nameof(AppRole.Viewer)));
        Assert.True(await userManager.IsInRoleAsync(viewer, nameof(AppRole.Viewer)));
    }

    [Fact]
    public async Task Identity_locks_an_account_after_five_failed_password_attempts()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "lockout-test",
                DisplayName = "Lockout Test",
                IsEnabled = true,
                LockoutEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            Assert.True((await userManager.CreateAsync(user, "Lockout123!")).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(
                user,
                nameof(AppRole.Operator))).Succeeded);
        }

        using var client = _factory.CreateClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failedResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest("lockout-test", "incorrect"));
            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        var correctPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("lockout-test", "Lockout123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, correctPasswordResponse.StatusCode);
        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var lockedUser = await verificationManager.FindByNameAsync("lockout-test");
        Assert.NotNull(lockedUser);
        Assert.True(await verificationManager.IsLockedOutAsync(lockedUser));
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

    private static void SetBearerToken(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record AccessibleCamera(
        string Id,
        string Name,
        string Location,
        string HlsUrl);
}
