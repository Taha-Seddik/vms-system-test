using System.Security.Claims;
using Vms.Api.Extensions;
using Vms.Api.Services;

namespace Vms.Api.Middleware;

public sealed class AuditWriteMiddleware(
    RequestDelegate next,
    ILogger<AuditWriteMiddleware> logger)
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase)
        {
            HttpMethods.Post,
            HttpMethods.Put,
            HttpMethods.Patch,
            HttpMethods.Delete
        };

    public async Task InvokeAsync(
        HttpContext context,
        AuditService audit)
    {
        await next(context);

        if (!WriteMethods.Contains(context.Request.Method)
            || context.Response.StatusCode is < 200 or >= 300
            || context.User.Identity?.IsAuthenticated != true
            || context.Request.Path.StartsWithSegments("/api/auth"))
        {
            return;
        }

        try
        {
            var userId = context.User.GetRequiredUserId();
            var username = context.User.FindFirstValue(ClaimTypes.Name)
                ?? "unknown";
            var resourceType = GetResourceType(context.Request.Path);
            var resourceId = GetResourceId(context);
            var action = GetAction(context.Request.Method, context.Request.Path);

            await audit.RecordAsync(
                userId,
                username,
                action,
                resourceType,
                resourceId,
                $"{username} {action.ToLowerInvariant()} {resourceType}"
                    + (resourceId is null ? "." : $" '{resourceId}'."),
                context.RequestAborted);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Successful write request {Method} {Path} could not be audited.",
                context.Request.Method,
                context.Request.Path);
        }
    }

    private static string GetResourceType(PathString path)
    {
        var segments = path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            ?? [];
        return segments.Length > 1
            ? segments[1] switch
            {
                "camera-groups" => "CameraGroup",
                "cameras" => "Camera",
                "recordings" => "Recording",
                "events" => "Event",
                "users" => "User",
                _ => segments[1]
            }
            : "System";
    }

    private static string? GetResourceId(HttpContext context)
    {
        string[] names =
        [
            "id",
            "cameraId",
            "groupId",
            "recordingId",
            "userId"
        ];
        foreach (var name in names)
        {
            if (context.Request.RouteValues.TryGetValue(name, out var value)
                && value is not null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static string GetAction(string method, PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (value.EndsWith("/close", StringComparison.OrdinalIgnoreCase))
        {
            return "Closed";
        }

        if (value.Contains("/recordings/", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/motion/simulate", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/test-connection", StringComparison.OrdinalIgnoreCase))
        {
            return "Executed";
        }

        return method.ToUpperInvariant() switch
        {
            "POST" => "Created",
            "PUT" or "PATCH" => "Updated",
            "DELETE" => "Deleted",
            _ => "Changed"
        };
    }
}
