namespace ApiForge.Core;

public sealed record ApiPermissionDto(
    long? Id,
    string ContentTypeApiId,
    string Endpoint,
    string Method,
    HashSet<string>? AllowedRoles,
    DateTime? CreatedAt
);

public sealed record ContentPermissionDto(
    long? Id,
    string ContentTypeApiId,
    string Action,
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
