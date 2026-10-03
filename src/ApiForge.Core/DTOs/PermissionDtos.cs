using System.ComponentModel.DataAnnotations;

namespace ApiForge.Core;

public sealed record ApiPermissionDto(
    long? Id,
    [Required, StringLength(100)] string ContentTypeApiId,
    [Required, StringLength(255)] string Endpoint,
    [Required, StringLength(10)] string Method,
    HashSet<string>? AllowedRoles,
    DateTime? CreatedAt
);

public sealed record ContentPermissionDto(
    long? Id,
    [Required, StringLength(100)] string ContentTypeApiId,
    [Required, StringLength(50)] string Action,
    HashSet<string>? AllowedRoles,
    DateTime? CreatedAt
);

public sealed record PermissionCheck(
    string? ContentTypeApiId,
    string? Endpoint,
    string? Method,
    string? Action,
    IReadOnlyList<string>? UserRoles
);
