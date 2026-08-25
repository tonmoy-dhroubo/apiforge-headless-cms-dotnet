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
public sealed class UserRecord(long id, string username, string email, string password, string? first, string? last, IEnumerable<string> roles, bool enabled)
{
    public long Id { get; } = id;
    public string Username { get; } = username;
    public string Email { get; } = email;
    public string Password { get; } = password;
    public string? Firstname { get; } = first;
    public string? Lastname { get; } = last;
    public List<string> Roles { get; } = roles.ToList();
    public bool Enabled { get; set; } = enabled;
}
public sealed record MediaRecord(long Id, string Name, string? AlternativeText, string? Caption, int? Width, int? Height, string Hash, string Ext, string? Mime, double Size, string Url, string Provider, [property: JsonIgnore] string Path);
public interface IUserStore
{
    Task<UserRecord?> Find(string identifier, CancellationToken ct = default);
    Task<UserRecord?> ById(long id, CancellationToken ct = default);
    Task<IReadOnlyList<UserRecord>> All(CancellationToken ct = default);
    Task<UserRecord> Add(string username, string email, string password, string? first, string? last, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task<UserRecord?> SetRoles(long id, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task<bool> Remove(long id, CancellationToken ct = default);
}
public interface IPermissionStore
{
    Task<ApiPermissionDto> Add(ApiPermissionDto value, CancellationToken ct = default);
    Task<ContentPermissionDto> Add(ContentPermissionDto value, CancellationToken ct = default);
    Task<IReadOnlyList<ApiPermissionDto>> ApiAll(CancellationToken ct = default);
    Task<IReadOnlyList<ContentPermissionDto>> ContentAll(CancellationToken ct = default);
    Task<ApiPermissionDto?> ApiBy(long id, CancellationToken ct = default);
    Task<ContentPermissionDto?> ContentBy(long id, CancellationToken ct = default);
    Task<ApiPermissionDto?> Update(ApiPermissionDto value, CancellationToken ct = default);
    Task<ContentPermissionDto?> Update(ContentPermissionDto value, CancellationToken ct = default);
    Task<bool> RemoveApi(long id, CancellationToken ct = default);
    Task<bool> RemoveContent(long id, CancellationToken ct = default);
}
public interface IMediaStore
{
    Task<MediaRecord> Save(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default);
    Task<MediaRecord?> ById(long id, CancellationToken ct = default);
    Task<MediaRecord?> ByFile(string filename, CancellationToken ct = default);
    Task<bool> Remove(long id, CancellationToken ct = default);
}

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
