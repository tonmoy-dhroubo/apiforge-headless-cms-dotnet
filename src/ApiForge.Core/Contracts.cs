using System.Text.Json.Serialization;

namespace ApiForge.Core;

public sealed record ApiResponse<T>(bool Success, string? Message, T? Data, string? Error)
{
    public static ApiResponse<T> Ok(T? data, string? message = null) => new(true, message, data, null);
    public static ApiResponse<T> Fail(string error) => new(false, null, default, error);
}

public sealed record AuthResponse(string Token, string RefreshToken, string Type, long UserId, string Username, string Email, IReadOnlyList<string> Roles);
public sealed record RegisterRequest(string Username, string Email, string Password, string? Firstname, string? Lastname);
public sealed record LoginRequest(string? Username, string? Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UserDto(long Id, string Username, string Email, string? Firstname, string? Lastname, IReadOnlyList<string> Roles, bool Enabled);
public enum FieldType { SHORT_TEXT, LONG_TEXT, RICH_TEXT, NUMBER, BOOLEAN, DATETIME, MEDIA, RELATION }
public sealed record FieldDto(long? Id, string Name, string FieldName, FieldType Type, bool? Required, bool? Unique, string? TargetContentType, string? RelationType);
public sealed record ContentTypeDto(long? Id, string Name, string? PluralName, string ApiId, string? Description, IReadOnlyList<FieldDto>? Fields, DateTime? CreatedAt, DateTime? UpdatedAt);
public sealed record ApiPermissionDto(long? Id, string ContentTypeApiId, string Endpoint, string Method, HashSet<string>? AllowedRoles, DateTime? CreatedAt);
public sealed record ContentPermissionDto(long? Id, string ContentTypeApiId, string Action, HashSet<string>? AllowedRoles, DateTime? CreatedAt);
public sealed record PermissionCheck(string? ContentTypeApiId, string? Endpoint, string? Method, string? Action, IReadOnlyList<string>? UserRoles);

public interface IContentTypeStore
{
    Task<IReadOnlyList<ContentTypeDto>> All(CancellationToken ct);
    Task<ContentTypeDto?> ById(long id, CancellationToken ct);
    Task<ContentTypeDto?> ByApiId(string apiId, CancellationToken ct);
    Task<ContentTypeDto> Create(ContentTypeDto dto, CancellationToken ct);
    Task<ContentTypeDto> Update(long id, ContentTypeDto dto, CancellationToken ct);
    Task Delete(long id, CancellationToken ct);
}

public interface IContentStore
{
    Task<IDictionary<string, object?>?> Create(string apiId, IDictionary<string, object?> values, CancellationToken ct);
    Task<IReadOnlyList<IDictionary<string, object?>>> All(string apiId, CancellationToken ct);
    Task<IReadOnlyList<IDictionary<string, object?>>> Search(string apiId, IDictionary<string, object?> filters, CancellationToken ct);
    Task<IDictionary<string, object?>?> ById(string apiId, long id, CancellationToken ct);
    Task<IDictionary<string, object?>?> Update(string apiId, long id, IDictionary<string, object?> values, CancellationToken ct);
    Task Delete(string apiId, long id, CancellationToken ct);
}
