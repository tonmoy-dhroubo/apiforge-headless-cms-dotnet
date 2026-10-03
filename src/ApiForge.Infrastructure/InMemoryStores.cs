using System.Collections.Concurrent;
using ApiForge.Core;

namespace ApiForge.Infrastructure;

public sealed class ApiForgeException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class InMemoryContentTypeStore : IContentTypeStore
{
    private readonly ConcurrentDictionary<long, ContentTypeDto> _types = new();
    private long _nextId;

    public Task<IReadOnlyList<ContentTypeDto>> All(CancellationToken ct)
    {
        var result = _types.Values
            .OrderBy(item => item.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ContentTypeDto>>(result);
    }

    public Task<ContentTypeDto?> ById(long id, CancellationToken ct)
    {
        _types.TryGetValue(id, out var contentType);
        return Task.FromResult(contentType);
    }

    public Task<ContentTypeDto?> ByApiId(string apiId, CancellationToken ct)
    {
        var contentType = _types.Values
            .FirstOrDefault(x => string.Equals(x.ApiId, apiId, StringComparison.Ordinal));

        return Task.FromResult(contentType);
    }

    public Task<ContentTypeDto> Create(ContentTypeDto dto, CancellationToken ct)
    {
        if (_types.Values.Any(x => x.ApiId == dto.ApiId))
        {
            throw new ApiForgeException("Content type with this API ID already exists", 409);
        }

        var now = DateTime.UtcNow;
        var fields = (dto.Fields ?? [])
            .Select(f => f with { Id = f.Id ?? Random.Shared.NextInt64(1, long.MaxValue) })
            .ToList();

        var id = Interlocked.Increment(ref _nextId);
        var pluralName = dto.PluralName ?? dto.Name + "s";

        var result = dto with
        {
            Id = id,
            PluralName = pluralName,
            Fields = fields,
            CreatedAt = now,
            UpdatedAt = now
        };

        _types[id] = result;
        return Task.FromResult(result);
    }

    public Task<ContentTypeDto> Update(long id, ContentTypeDto dto, CancellationToken ct)
    {
        if (!_types.TryGetValue(id, out var existing))
        {
            throw new ApiForgeException("Content type not found", 404);
        }

        var updated = existing with
        {
            Name = dto.Name ?? existing.Name,
            PluralName = dto.PluralName ?? existing.PluralName,
            Description = dto.Description ?? existing.Description,
            Fields = dto.Fields ?? existing.Fields,
            UpdatedAt = DateTime.UtcNow
        };

        _types[id] = updated;
        return Task.FromResult(updated);
    }

    public Task Delete(long id, CancellationToken ct)
    {
        if (!_types.TryRemove(id, out _))
        {
            throw new ApiForgeException("Content type not found", 404);
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryContentStore(IContentTypeStore types) : IContentStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<long, IDictionary<string, object?>>> _rows = new();
    private long _nextId;

    private async Task EnsureContentTypeExists(string apiId, CancellationToken ct)
    {
        var contentType = await types.ByApiId(apiId, ct);
        if (contentType is null)
        {
            throw new ApiForgeException("Content type not found", 404);
        }
    }

    public async Task<IDictionary<string, object?>?> Create(string apiId, IDictionary<string, object?> values, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        var id = Interlocked.Increment(ref _nextId);
        var now = DateTime.UtcNow;

        var row = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = id,
            ["created_at"] = now,
            ["updated_at"] = now
        };

        var table = _rows.GetOrAdd(apiId, _ => new ConcurrentDictionary<long, IDictionary<string, object?>>());
        table.TryAdd(id, row);

        return row;
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> All(string apiId, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        if (!_rows.TryGetValue(apiId, out var table))
        {
            return [];
        }

        return table.Values
            .OrderBy(x => Convert.ToInt64(x["id"]))
            .ToList();
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> Search(string apiId, IDictionary<string, object?> filters, CancellationToken ct)
    {
        var rows = await All(apiId, ct);

        return rows.Where(row =>
            filters.All(filter =>
                row.TryGetValue(filter.Key, out var val) &&
                string.Equals(Convert.ToString(val), Convert.ToString(filter.Value), StringComparison.OrdinalIgnoreCase)
            )
        ).ToList();
    }

    public async Task<IDictionary<string, object?>?> ById(string apiId, long id, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        if (_rows.TryGetValue(apiId, out var table) && table.TryGetValue(id, out var row))
        {
            return row;
        }

        return null;
    }

    public async Task<IDictionary<string, object?>?> Update(string apiId, long id, IDictionary<string, object?> values, CancellationToken ct)
    {
        var row = await ById(apiId, id, ct);
        if (row is null)
        {
            return null;
        }

        foreach (var (key, value) in values)
        {
            row[key] = value;
        }

        row["updated_at"] = DateTime.UtcNow;
        return row;
    }

    public async Task Delete(string apiId, long id, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        if (!_rows.TryGetValue(apiId, out var table) || !table.TryRemove(id, out _))
        {
            throw new ApiForgeException("Content not found", 404);
        }
    }
}

public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<long, UserRecord> _users = new();
    private long _nextId;

    public InMemoryUserStore()
    {
        Add(
            username: "admin",
            email: "admin@apiforge.com",
            password: "password123",
            first: "System",
            last: "Admin",
            roles: ["SUPER_ADMIN", "ADMIN"]
        );
    }

    public UserRecord Add(string username, string email, string password, string? first, string? last, IReadOnlyList<string> roles)
    {
        var id = Interlocked.Increment(ref _nextId);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

        var user = new UserRecord(
            id: id,
            username: username,
            email: email,
            password: passwordHash,
            first: first,
            last: last,
            roles: roles,
            enabled: true
        );

        _users[user.Id] = user;
        return user;
    }

    public Task<UserRecord?> Find(string identifier, CancellationToken ct = default)
    {
        var user = _users.Values.FirstOrDefault(x =>
            string.Equals(x.Username, identifier, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Email, identifier, StringComparison.OrdinalIgnoreCase)
        );

        return Task.FromResult(user);
    }

    public Task<UserRecord?> ById(long id, CancellationToken ct = default)
    {
        _users.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<UserRecord>> All(CancellationToken ct = default)
    {
        var users = _users.Values
            .OrderBy(x => x.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<UserRecord>>(users);
    }

    public Task<UserRecord> Add(string username, string email, string password, string? first, string? last, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        return Task.FromResult(Add(username, email, password, first, last, roles));
    }

    public Task<UserRecord?> SetRoles(long id, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        if (!_users.TryGetValue(id, out var user))
        {
            return Task.FromResult<UserRecord?>(null);
        }

        user.Roles.Clear();
        user.Roles.AddRange(roles);

        return Task.FromResult<UserRecord?>(user);
    }

    public Task<bool> Remove(long id, CancellationToken ct = default)
    {
        var removed = _users.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}

public sealed class InMemoryPermissionStore : IPermissionStore
{
    private readonly ConcurrentDictionary<long, ApiPermissionDto> _apiPermissions = new();
    private readonly ConcurrentDictionary<long, ContentPermissionDto> _contentPermissions = new();
    private long _nextId;

    public Task<ApiPermissionDto> Add(ApiPermissionDto dto, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var created = dto with
        {
            Id = id,
            CreatedAt = DateTime.UtcNow
        };

        _apiPermissions[id] = created;
        return Task.FromResult(created);
    }

    public Task<ContentPermissionDto> Add(ContentPermissionDto dto, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var created = dto with
        {
            Id = id,
            CreatedAt = DateTime.UtcNow
        };

        _contentPermissions[id] = created;
        return Task.FromResult(created);
    }

    public Task<IReadOnlyList<ApiPermissionDto>> ApiAll(CancellationToken ct = default)
    {
        var permissions = _apiPermissions.Values
            .OrderBy(x => x.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ApiPermissionDto>>(permissions);
    }

    public Task<IReadOnlyList<ContentPermissionDto>> ContentAll(CancellationToken ct = default)
    {
        var permissions = _contentPermissions.Values
            .OrderBy(x => x.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<ContentPermissionDto>>(permissions);
    }

    public Task<ApiPermissionDto?> ApiBy(long id, CancellationToken ct = default)
    {
        _apiPermissions.TryGetValue(id, out var permission);
        return Task.FromResult(permission);
    }

    public Task<ContentPermissionDto?> ContentBy(long id, CancellationToken ct = default)
    {
        _contentPermissions.TryGetValue(id, out var permission);
        return Task.FromResult(permission);
    }

    public Task<ApiPermissionDto?> Update(ApiPermissionDto dto, CancellationToken ct = default)
    {
        if (dto.Id is null || !_apiPermissions.TryGetValue(dto.Id.Value, out var existing))
        {
            return Task.FromResult<ApiPermissionDto?>(null);
        }

        var updated = dto with { CreatedAt = existing.CreatedAt };
        _apiPermissions[dto.Id.Value] = updated;

        return Task.FromResult<ApiPermissionDto?>(updated);
    }

    public Task<ContentPermissionDto?> Update(ContentPermissionDto dto, CancellationToken ct = default)
    {
        if (dto.Id is null || !_contentPermissions.TryGetValue(dto.Id.Value, out var existing))
        {
            return Task.FromResult<ContentPermissionDto?>(null);
        }

        var updated = dto with { CreatedAt = existing.CreatedAt };
        _contentPermissions[dto.Id.Value] = updated;

        return Task.FromResult<ContentPermissionDto?>(updated);
    }

    public Task<bool> RemoveApi(long id, CancellationToken ct = default)
    {
        var removed = _apiPermissions.TryRemove(id, out _);
        return Task.FromResult(removed);
    }

    public Task<bool> RemoveContent(long id, CancellationToken ct = default)
    {
        var removed = _contentPermissions.TryRemove(id, out _);
        return Task.FromResult(removed);
    }
}
