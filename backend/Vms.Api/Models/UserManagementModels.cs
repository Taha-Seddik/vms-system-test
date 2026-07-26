using System.ComponentModel.DataAnnotations;
using Vms.Api.Domain;

namespace Vms.Api.Models;

public sealed record UserCameraResponse(string Id, string Name);

public sealed record ManagedUserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    AppRole Role,
    bool IsEnabled,
    IReadOnlyList<UserCameraResponse> AssignedCameras,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? LastActivityAt);

public sealed record CreateUserRequest(
    [param: Required]
    [param: RegularExpression(
        "^[A-Za-z0-9._-]{3,100}$",
        ErrorMessage = "Username must be 3-100 letters, numbers, dots, underscores, or hyphens.")]
    string Username,
    [param: Required, StringLength(160, MinimumLength = 2)]
    string DisplayName,
    [param: Required, StringLength(200, MinimumLength = 8)]
    string Password,
    AppRole Role,
    IReadOnlyList<string>? AssignedCameraIds);

public sealed record UpdateUserRequest(
    [param: Required, StringLength(160, MinimumLength = 2)]
    string DisplayName,
    AppRole Role,
    bool IsEnabled,
    IReadOnlyList<string>? AssignedCameraIds,
    [param: StringLength(200, MinimumLength = 8)]
    string? NewPassword);

public enum UserMutationError
{
    None,
    NotFound,
    Validation,
    Conflict
}

public sealed record UserMutationResult(
    ManagedUserResponse? User,
    UserMutationError ErrorType,
    string? Error);
