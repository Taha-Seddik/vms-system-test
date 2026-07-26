namespace Vms.Api.Models;

public sealed record AccessibleCameraResponse(
    string Id,
    string Name,
    string Location,
    string HlsUrl);

