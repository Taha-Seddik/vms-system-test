namespace Vms.Api.Utils;

public static class UsernameNormalizer
{
    public static string Normalize(string username) =>
        username.Trim().ToUpperInvariant();
}
