using Microsoft.AspNetCore.Identity;

namespace Vms.Api.Utils;

public static class IdentityResultExtensions
{
    public static void EnsureSucceeded(
        this IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException($"{operation} failed. {errors}");
    }
}
