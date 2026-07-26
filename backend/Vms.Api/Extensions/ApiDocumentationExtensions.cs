using Microsoft.OpenApi;

namespace Vms.Api.Extensions;

public static class ApiDocumentationExtensions
{
    public static IServiceCollection AddVmsApiDocumentation(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(
                "v1",
                new OpenApiInfo
                {
                    Title = "Video Management System API",
                    Version = "v1",
                    Description =
                        "Assessment API for cameras, live monitoring, recording, "
                        + "playback, events, users, search, and audit activity."
                });
            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the JWT returned by POST /api/auth/login."
                });
            options.AddSecurityRequirement(document =>
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
        });
        return services;
    }

    public static WebApplication UseVmsApiDocumentation(
        this WebApplication app)
    {
        app.UseSwagger(options =>
            options.RouteTemplate = "openapi/{documentName}.json");
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint(
                "/openapi/v1.json",
                "Video Management System API v1");
            options.DocumentTitle = "VMS API documentation";
            options.DisplayRequestDuration();
        });
        return app;
    }
}
