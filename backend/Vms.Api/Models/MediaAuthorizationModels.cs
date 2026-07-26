namespace Vms.Api.Models;

public sealed record MediaAuthorizationRequest(
    string? User,
    string? Password,
    string? Token,
    string? Ip,
    string? Action,
    string? Path,
    string? Protocol,
    string? Id,
    string? Query,
    string? UserAgent);
