using ApiForge.Core;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ApiForge.Infrastructure;

public sealed class PostgresUserStore(IConfiguration configuration) : IUserStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"] 
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static UserRecord ReadUser(NpgsqlDataReader reader)
    {
        var id = reader.GetInt64(0);
        var username = reader.GetString(1);
        var email = reader.GetString(2);
        var passwordHash = reader.GetString(3);
        var firstname = reader.IsDBNull(4) ? null : reader.GetString(4);
        var lastname = reader.IsDBNull(5) ? null : reader.GetString(5);
        var roles = reader.IsDBNull(6) ? [] : (string[])reader.GetValue(6);
        var enabled = reader.GetBoolean(7);

        return new UserRecord(
            id: id,
            username: username,
            email: email,
            password: passwordHash,
            first: firstname,
            last: lastname,
            roles: roles,
            enabled: enabled
        );
    }

    private const string BaseUserSelectSql = """
        SELECT u.id,
               u.username,
               u.email,
               u.password,
               u.firstname,
               u.lastname,
               COALESCE(array_agg(r.name) FILTER (WHERE r.name IS NOT NULL), ARRAY[]::text[]) AS roles,
               u.enabled
        FROM users u
        LEFT JOIN user_roles ur ON ur.user_id = u.id
        LEFT JOIN roles r ON r.id = ur.role_id
        """;

    public async Task<UserRecord?> Find(string identifier, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();

        var sql = $"""
            {BaseUserSelectSql}
            WHERE u.username = @v OR u.email = @v
            GROUP BY u.id
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("v", identifier);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadUser(reader) : null;
    }

    public async Task<UserRecord?> ById(long id, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();

        var sql = $"""
            {BaseUserSelectSql}
            WHERE u.id = @v
            GROUP BY u.id
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("v", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadUser(reader) : null;
    }

    public async Task<IReadOnlyList<UserRecord>> All(CancellationToken ct = default)
    {
        await using var connection = OpenConnection();

        var sql = $"""
            {BaseUserSelectSql}
            GROUP BY u.id
            ORDER BY u.id
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var users = new List<UserRecord>();
        while (await reader.ReadAsync(ct))
        {
            users.Add(ReadUser(reader));
        }

        return users;
    }

    public async Task<UserRecord> Add(
        string username,
        string email,
        string password,
        string? first,
        string? last,
        IReadOnlyList<string> roles,
        CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insertUserSql = """
            INSERT INTO users (username, email, password, firstname, lastname, enabled)
            VALUES (@u, @e, @p, @f, @l, TRUE)
            RETURNING id
            """;

        await using var command = new NpgsqlCommand(insertUserSql, connection, transaction);
        command.Parameters.AddWithValue("u", username);
        command.Parameters.AddWithValue("e", email);
        command.Parameters.AddWithValue("p", BCrypt.Net.BCrypt.HashPassword(password));
        command.Parameters.AddWithValue("f", (object?)first ?? DBNull.Value);
        command.Parameters.AddWithValue("l", (object?)last ?? DBNull.Value);

        var userId = (long)(await command.ExecuteScalarAsync(ct))!;

        await SetUserRolesInternal(connection, transaction, userId, roles, ct);
        await transaction.CommitAsync(ct);

        return (await ById(userId, ct))!;
    }

    private static async Task SetUserRolesInternal(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long userId,
        IReadOnlyList<string> roles,
        CancellationToken ct)
    {
        const string clearRolesSql = "DELETE FROM user_roles WHERE user_id = @id";
        await using var clearCommand = new NpgsqlCommand(clearRolesSql, connection, transaction);
        clearCommand.Parameters.AddWithValue("id", userId);
        await clearCommand.ExecuteNonQueryAsync(ct);

        foreach (var role in roles)
        {
            const string ensureRoleSql = "INSERT INTO roles (name) VALUES (@n) ON CONFLICT (name) DO NOTHING";
            await using var ensureRoleCommand = new NpgsqlCommand(ensureRoleSql, connection, transaction);
            ensureRoleCommand.Parameters.AddWithValue("n", role);
            await ensureRoleCommand.ExecuteNonQueryAsync(ct);

            const string assignRoleSql = """
                INSERT INTO user_roles (user_id, role_id)
                SELECT @userId, id
                FROM roles
                WHERE name = @n
                ON CONFLICT DO NOTHING
                """;

            await using var assignRoleCommand = new NpgsqlCommand(assignRoleSql, connection, transaction);
            assignRoleCommand.Parameters.AddWithValue("userId", userId);
            assignRoleCommand.Parameters.AddWithValue("n", role);
            await assignRoleCommand.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<UserRecord?> SetRoles(long id, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync(ct);

        if (await ById(id, ct) is null)
        {
            return null;
        }

        await SetUserRolesInternal(connection, transaction, id, roles, ct);
        await transaction.CommitAsync(ct);

        return await ById(id, ct);
    }

    public async Task<bool> Remove(long id, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        const string sql = "DELETE FROM users WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }
}

public sealed class PostgresPermissionStore(IConfiguration configuration) : IPermissionStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"] 
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<ApiPermissionDto> Add(ApiPermissionDto dto, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insertSql = """
            INSERT INTO api_permissions (content_type_api_id, endpoint, method)
            VALUES (@c, @e, @m)
            RETURNING id, created_at
            """;

        await using var command = new NpgsqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("c", dto.ContentTypeApiId);
        command.Parameters.AddWithValue("e", dto.Endpoint);
        command.Parameters.AddWithValue("m", dto.Method);

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var id = reader.GetInt64(0);
        var createdAt = reader.GetDateTime(1);
        await reader.CloseAsync();

        await AssignRolesToPermission(connection, transaction, "api_permission_roles", id, dto.AllowedRoles ?? [], ct);
        await transaction.CommitAsync(ct);

        return dto with { Id = id, CreatedAt = createdAt };
    }

    public async Task<ContentPermissionDto> Add(ContentPermissionDto dto, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insertSql = """
            INSERT INTO content_permissions (content_type_api_id, action)
            VALUES (@c, @a)
            RETURNING id, created_at
            """;

        await using var command = new NpgsqlCommand(insertSql, connection, transaction);
        command.Parameters.AddWithValue("c", dto.ContentTypeApiId);
        command.Parameters.AddWithValue("a", dto.Action);

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var id = reader.GetInt64(0);
        var createdAt = reader.GetDateTime(1);
        await reader.CloseAsync();

        await AssignRolesToPermission(connection, transaction, "content_permission_roles", id, dto.AllowedRoles ?? [], ct);
        await transaction.CommitAsync(ct);

        return dto with { Id = id, CreatedAt = createdAt };
    }

    private static async Task AssignRolesToPermission(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        long permissionId,
        IEnumerable<string> roles,
        CancellationToken ct)
    {
        foreach (var role in roles)
        {
            var sql = $"INSERT INTO {tableName} (permission_id, role_name) VALUES (@id, @r) ON CONFLICT DO NOTHING";
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", permissionId);
            command.Parameters.AddWithValue("r", role);

            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ApiPermissionDto>> ApiAll(CancellationToken ct = default)
    {
        await using var connection = OpenConnection();

        const string sql = """
            SELECT p.id,
                   p.content_type_api_id,
                   p.endpoint,
                   p.method,
                   p.created_at,
                   COALESCE(array_agg(r.role_name) FILTER (WHERE r.role_name IS NOT NULL), ARRAY[]::text[]) AS allowed_roles
            FROM api_permissions p
            LEFT JOIN api_permission_roles r ON r.permission_id = p.id
            GROUP BY p.id
            ORDER BY p.id
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var permissions = new List<ApiPermissionDto>();
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var contentTypeApiId = reader.GetString(1);
            var endpoint = reader.GetString(2);
            var method = reader.GetString(3);
            var createdAt = reader.GetDateTime(4);
            var roleArray = (string[])reader.GetValue(5);

            permissions.Add(new ApiPermissionDto(
                Id: id,
                ContentTypeApiId: contentTypeApiId,
                Endpoint: endpoint,
                Method: method,
                AllowedRoles: new HashSet<string>(roleArray),
                CreatedAt: createdAt
            ));
        }

        return permissions;
    }

    public async Task<IReadOnlyList<ContentPermissionDto>> ContentAll(CancellationToken ct = default)
    {
        await using var connection = OpenConnection();

        const string sql = """
            SELECT p.id,
                   p.content_type_api_id,
                   p.action,
                   p.created_at,
                   COALESCE(array_agg(r.role_name) FILTER (WHERE r.role_name IS NOT NULL), ARRAY[]::text[]) AS allowed_roles
            FROM content_permissions p
            LEFT JOIN content_permission_roles r ON r.permission_id = p.id
            GROUP BY p.id
            ORDER BY p.id
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var permissions = new List<ContentPermissionDto>();
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var contentTypeApiId = reader.GetString(1);
            var action = reader.GetString(2);
            var createdAt = reader.GetDateTime(3);
            var roleArray = (string[])reader.GetValue(4);

            permissions.Add(new ContentPermissionDto(
                Id: id,
                ContentTypeApiId: contentTypeApiId,
                Action: action,
                AllowedRoles: new HashSet<string>(roleArray),
                CreatedAt: createdAt
            ));
        }

        return permissions;
    }

    public async Task<ApiPermissionDto?> ApiBy(long id, CancellationToken ct = default) =>
        (await ApiAll(ct)).FirstOrDefault(x => x.Id == id);

    public async Task<ContentPermissionDto?> ContentBy(long id, CancellationToken ct = default) =>
        (await ContentAll(ct)).FirstOrDefault(x => x.Id == id);

    public async Task<ApiPermissionDto?> Update(ApiPermissionDto dto, CancellationToken ct = default)
    {
        if (dto.Id is null || await ApiBy(dto.Id.Value, ct) is null)
        {
            return null;
        }

        await using var connection = OpenConnection();

        const string updateSql = """
            UPDATE api_permissions
            SET content_type_api_id = @c,
                endpoint = @e,
                method = @m
            WHERE id = @id
            """;

        await using var command = new NpgsqlCommand(updateSql, connection);
        command.Parameters.AddWithValue("c", dto.ContentTypeApiId);
        command.Parameters.AddWithValue("e", dto.Endpoint);
        command.Parameters.AddWithValue("m", dto.Method);
        command.Parameters.AddWithValue("id", dto.Id.Value);
        await command.ExecuteNonQueryAsync(ct);

        const string deleteRolesSql = "DELETE FROM api_permission_roles WHERE permission_id = @id";
        await using var deleteRolesCommand = new NpgsqlCommand(deleteRolesSql, connection);
        deleteRolesCommand.Parameters.AddWithValue("id", dto.Id.Value);
        await deleteRolesCommand.ExecuteNonQueryAsync(ct);

        foreach (var role in dto.AllowedRoles ?? [])
        {
            const string insertRoleSql = "INSERT INTO api_permission_roles (permission_id, role_name) VALUES (@id, @r)";
            await using var insertRoleCommand = new NpgsqlCommand(insertRoleSql, connection);
            insertRoleCommand.Parameters.AddWithValue("id", dto.Id.Value);
            insertRoleCommand.Parameters.AddWithValue("r", role);
            await insertRoleCommand.ExecuteNonQueryAsync(ct);
        }

        return await ApiBy(dto.Id.Value, ct);
    }

    public async Task<ContentPermissionDto?> Update(ContentPermissionDto dto, CancellationToken ct = default)
    {
        if (dto.Id is null || await ContentBy(dto.Id.Value, ct) is null)
        {
            return null;
        }

        await using var connection = OpenConnection();

        const string updateSql = """
            UPDATE content_permissions
            SET content_type_api_id = @c,
                action = @a
            WHERE id = @id
            """;

        await using var command = new NpgsqlCommand(updateSql, connection);
        command.Parameters.AddWithValue("c", dto.ContentTypeApiId);
        command.Parameters.AddWithValue("a", dto.Action);
        command.Parameters.AddWithValue("id", dto.Id.Value);
        await command.ExecuteNonQueryAsync(ct);

        const string deleteRolesSql = "DELETE FROM content_permission_roles WHERE permission_id = @id";
        await using var deleteRolesCommand = new NpgsqlCommand(deleteRolesSql, connection);
        deleteRolesCommand.Parameters.AddWithValue("id", dto.Id.Value);
        await deleteRolesCommand.ExecuteNonQueryAsync(ct);

        foreach (var role in dto.AllowedRoles ?? [])
        {
            const string insertRoleSql = "INSERT INTO content_permission_roles (permission_id, role_name) VALUES (@id, @r)";
            await using var insertRoleCommand = new NpgsqlCommand(insertRoleSql, connection);
            insertRoleCommand.Parameters.AddWithValue("id", dto.Id.Value);
            insertRoleCommand.Parameters.AddWithValue("r", role);
            await insertRoleCommand.ExecuteNonQueryAsync(ct);
        }

        return await ContentBy(dto.Id.Value, ct);
    }

    public async Task<bool> RemoveApi(long id, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        const string sql = "DELETE FROM api_permissions WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }

    public async Task<bool> RemoveContent(long id, CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
        const string sql = "DELETE FROM content_permissions WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        return rowsAffected > 0;
    }
}
