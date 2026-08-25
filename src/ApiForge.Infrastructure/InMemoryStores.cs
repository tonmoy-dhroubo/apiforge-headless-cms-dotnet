using System.Collections.Concurrent;
using System.Security.Cryptography;
using ApiForge.Core;

namespace ApiForge.Infrastructure;

public sealed class ApiForgeException(string message, int status) : Exception(message) { public int Status { get; } = status; }

public sealed class InMemoryContentTypeStore : IContentTypeStore
{
    private readonly ConcurrentDictionary<long, ContentTypeDto> _types = new();
    private long _next;
    public Task<IReadOnlyList<ContentTypeDto>> All(CancellationToken ct) => Task.FromResult<IReadOnlyList<ContentTypeDto>>(_types.Values.OrderBy(x => x.Id).ToList());
    public Task<ContentTypeDto?> ById(long id, CancellationToken ct) => Task.FromResult(_types.GetValueOrDefault(id));
    public Task<ContentTypeDto?> ByApiId(string apiId, CancellationToken ct) => Task.FromResult(_types.Values.FirstOrDefault(x => string.Equals(x.ApiId, apiId, StringComparison.Ordinal)));
    public Task<ContentTypeDto> Create(ContentTypeDto dto, CancellationToken ct)
    {
        if (_types.Values.Any(x => x.ApiId == dto.ApiId)) throw new ApiForgeException("Content type with this API ID already exists", 409);
        var now = DateTime.UtcNow;
        var fields = (dto.Fields ?? []).Select(f => f with { Id = f.Id ?? Random.Shared.NextInt64(1, long.MaxValue) }).ToList();
        var result = dto with { Id = Interlocked.Increment(ref _next), PluralName = dto.PluralName ?? dto.Name + "s", Fields = fields, CreatedAt = now, UpdatedAt = now };
        _types[result.Id!.Value] = result; return Task.FromResult(result);
    }
    public Task<ContentTypeDto> Update(long id, ContentTypeDto dto, CancellationToken ct)
    {
        if (!_types.TryGetValue(id, out var old)) throw new ApiForgeException("Content type not found", 404);
        var result = old with { Name = dto.Name ?? old.Name, PluralName = dto.PluralName ?? old.PluralName, Description = dto.Description ?? old.Description, Fields = dto.Fields ?? old.Fields, UpdatedAt = DateTime.UtcNow };
        _types[id] = result; return Task.FromResult(result);
    }
    public Task Delete(long id, CancellationToken ct) { if (!_types.TryRemove(id, out _)) throw new ApiForgeException("Content type not found", 404); return Task.CompletedTask; }
}

public sealed class InMemoryContentStore(IContentTypeStore types) : IContentStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, IDictionary<string, object?>>> _rows = new();
    private long _next;
    private async Task Ensure(string apiId, CancellationToken ct) { if (await types.ByApiId(apiId, ct) is null) throw new ApiForgeException("Content type not found", 404); }
    public async Task<IDictionary<string, object?>?> Create(string apiId, IDictionary<string, object?> values, CancellationToken ct) { await Ensure(apiId, ct); var row = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase) { ["id"] = Interlocked.Increment(ref _next), ["created_at"] = DateTime.UtcNow, ["updated_at"] = DateTime.UtcNow }; _rows.GetOrAdd(apiId, _ => new()).TryAdd((long)row["id"]!, row); return row; }
    public async Task<IReadOnlyList<IDictionary<string, object?>>> All(string apiId, CancellationToken ct) { await Ensure(apiId, ct); return _rows.GetValueOrDefault(apiId)?.Values.OrderBy(x => Convert.ToInt64(x["id"])).ToList() ?? []; }
    public async Task<IReadOnlyList<IDictionary<string, object?>>> Search(string apiId, IDictionary<string, object?> filters, CancellationToken ct) { var rows = await All(apiId, ct); return rows.Where(row => filters.All(f => row.TryGetValue(f.Key, out var v) && string.Equals(Convert.ToString(v), Convert.ToString(f.Value), StringComparison.OrdinalIgnoreCase))).ToList(); }
    public async Task<IDictionary<string, object?>?> ById(string apiId, long id, CancellationToken ct) { await Ensure(apiId, ct); return _rows.GetValueOrDefault(apiId)?.GetValueOrDefault(id); }
    public async Task<IDictionary<string, object?>?> Update(string apiId, long id, IDictionary<string, object?> values, CancellationToken ct) { var row = await ById(apiId, id, ct); if (row is null) return null; foreach (var x in values) row[x.Key] = x.Value; row["updated_at"] = DateTime.UtcNow; return row; }
    public async Task Delete(string apiId, long id, CancellationToken ct) { await Ensure(apiId, ct); if (_rows.GetValueOrDefault(apiId)?.TryRemove(id, out _) != true) throw new ApiForgeException("Content not found", 404); }
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<long, UserRecord> _users = new(); private long _next;
    public InMemoryUserStore() { Add("admin", "admin@apiforge.com", "password123", "System", "Admin", ["SUPER_ADMIN", "ADMIN"]); }
    public UserRecord Add(string username, string email, string password, string? first, string? last, IReadOnlyList<string> roles) { var u = new UserRecord(Interlocked.Increment(ref _next), username, email, BCrypt.Net.BCrypt.HashPassword(password), first, last, roles, true); _users[u.Id] = u; return u; }
    public Task<UserRecord?> Find(string identifier, CancellationToken ct = default) => Task.FromResult(_users.Values.FirstOrDefault(x => x.Username == identifier || x.Email == identifier));
    public Task<UserRecord?> ById(long id, CancellationToken ct = default) => Task.FromResult(_users.GetValueOrDefault(id));
    public Task<IReadOnlyList<UserRecord>> All(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserRecord>>(_users.Values.OrderBy(x => x.Id).ToList());
    public Task<UserRecord> Add(string username, string email, string password, string? first, string? last, IReadOnlyList<string> roles, CancellationToken ct = default) => Task.FromResult(Add(username, email, password, first, last, roles));
    public Task<UserRecord?> SetRoles(long id, IReadOnlyList<string> roles, CancellationToken ct = default) { if (!_users.TryGetValue(id, out var u)) return Task.FromResult<UserRecord?>(null); u.Roles.Clear(); u.Roles.AddRange(roles); return Task.FromResult<UserRecord?>(u); }
    public Task<bool> Remove(long id, CancellationToken ct = default) => Task.FromResult(_users.TryRemove(id, out _));
}

public sealed class InMemoryPermissionStore : IPermissionStore
{
    private readonly ConcurrentDictionary<long, ApiPermissionDto> _api = new(); private readonly ConcurrentDictionary<long, ContentPermissionDto> _content = new(); private long _next;
    public Task<ApiPermissionDto> Add(ApiPermissionDto x, CancellationToken ct = default) { var y = x with { Id = Interlocked.Increment(ref _next), CreatedAt = DateTime.UtcNow }; _api[y.Id!.Value] = y; return Task.FromResult(y); }
    public Task<ContentPermissionDto> Add(ContentPermissionDto x, CancellationToken ct = default) { var y = x with { Id = Interlocked.Increment(ref _next), CreatedAt = DateTime.UtcNow }; _content[y.Id!.Value] = y; return Task.FromResult(y); }
    public Task<IReadOnlyList<ApiPermissionDto>> ApiAll(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ApiPermissionDto>>(_api.Values.OrderBy(x => x.Id).ToList());
    public Task<IReadOnlyList<ContentPermissionDto>> ContentAll(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ContentPermissionDto>>(_content.Values.OrderBy(x => x.Id).ToList());
    public Task<ApiPermissionDto?> ApiBy(long id, CancellationToken ct = default) => Task.FromResult(_api.GetValueOrDefault(id)); public Task<ContentPermissionDto?> ContentBy(long id, CancellationToken ct = default) => Task.FromResult(_content.GetValueOrDefault(id));
    public Task<ApiPermissionDto?> Update(ApiPermissionDto x, CancellationToken ct = default) { if (x.Id is null || !_api.ContainsKey(x.Id.Value)) return Task.FromResult<ApiPermissionDto?>(null); var y = x with { CreatedAt = _api[x.Id.Value].CreatedAt }; _api[x.Id.Value] = y; return Task.FromResult<ApiPermissionDto?>(y); }
    public Task<ContentPermissionDto?> Update(ContentPermissionDto x, CancellationToken ct = default) { if (x.Id is null || !_content.ContainsKey(x.Id.Value)) return Task.FromResult<ContentPermissionDto?>(null); var y = x with { CreatedAt = _content[x.Id.Value].CreatedAt }; _content[x.Id.Value] = y; return Task.FromResult<ContentPermissionDto?>(y); }
    public Task<bool> RemoveApi(long id, CancellationToken ct = default) => Task.FromResult(_api.TryRemove(id, out _)); public Task<bool> RemoveContent(long id, CancellationToken ct = default) => Task.FromResult(_content.TryRemove(id, out _));
}
