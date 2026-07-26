using Vms.Api.Domain;

namespace Vms.Api.Authorization;

public static class AppPolicies
{
    public const string AdministratorOnly = nameof(AdministratorOnly);

    public const string OperatorOrAdministrator = nameof(OperatorOrAdministrator);

    public static IServiceCollection AddVmsAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AdministratorOnly, policy =>
                policy.RequireRole(nameof(AppRole.Administrator)))
            .AddPolicy(OperatorOrAdministrator, policy =>
                policy.RequireRole(
                    nameof(AppRole.Administrator),
                    nameof(AppRole.Operator)));

        return services;
    }
}

