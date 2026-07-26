namespace Vms.Api.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] =
                "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy =
                "default-src 'self'; img-src 'self' data:; "
                + "style-src 'self' 'unsafe-inline'; "
                + "script-src 'self' 'unsafe-inline'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
