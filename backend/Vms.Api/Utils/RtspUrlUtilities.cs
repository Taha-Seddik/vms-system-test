namespace Vms.Api.Utils;

public static class RtspUrlUtilities
{
    public static bool IsSupported(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("rtsps", StringComparison.OrdinalIgnoreCase))
        && !string.IsNullOrWhiteSpace(uri.Host);

    public static string RedactCredentials(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.UserInfo))
        {
            return value;
        }

        var builder = new UriBuilder(uri)
        {
            UserName = "***",
            Password = "***"
        };
        return builder.Uri.ToString();
    }
}
