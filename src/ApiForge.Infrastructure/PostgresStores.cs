using ApiForge.Core;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ApiForge.Infrastructure;

public sealed class PostgresContentTypeStore(IConfiguration configuration) : IContentTypeStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"] 
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            throw new ApiForgeException("Invalid identifier", 400);
        }

        return value;
    }

    private static FieldDto ReadField(NpgsqlDataReader reader) =>
        new(
            Id: reader.GetInt64(0),
            Name: reader.GetString(1),
            FieldName: reader.GetString(2),
            Type: Enum.Parse<FieldType>(reader.GetString(3)),
            Required: reader.IsDBNull(4) ? null : reader.GetBoolean(4),
            Unique: reader.IsDBNull(5) ? null : reader.GetBoolean(5),
            TargetContentType: reader.IsDBNull(6) ? null : reader.GetString(6),
            RelationType: reader.IsDBNull(7) ? null : reader.GetString(7)
        );

    private static async Task<List<FieldDto>> LoadFieldsForTypeAsync(NpgsqlConnection connection, long contentTypeId, CancellationToken ct)
    {
        const string fieldsSql = """
            SELECT id, name, field_name, type, required, "unique", target_content_type, relation_type
            FROM fields
            WHERE content_type_id = @id
            ORDER BY id
            """;

        await using var fieldsCommand = new NpgsqlCommand(fieldsSql, connection);
        fieldsCommand.Parameters.AddWithValue("id", contentTypeId);

        await using var fieldsReader = await fieldsCommand.ExecuteReaderAsync(ct);
        var fields = new List<FieldDto>();
        while (await fieldsReader.ReadAsync(ct))
        {
            fields.Add(ReadField(fieldsReader));
        }

        return fields;
    }

    public async Task<IReadOnlyList<ContentTypeDto>> All(CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);

        const string sql = """
            SELECT id, name, plural_name, api_id, description, created_at, updated_at
            FROM content_types
            ORDER BY id
            """;

        var types = new List<(long Id, string Name, string PluralName, string ApiId, string? Description, DateTime? CreatedAt, DateTime? UpdatedAt)>();

        await using (var command = new NpgsqlCommand(sql, connection))
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                types.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    reader.IsDBNull(6) ? null : reader.GetDateTime(6)
                ));
            }
        }

        if (types.Count == 0)
        {
            return [];
        }

        const string allFieldsSql = """
            SELECT id, name, field_name, type, required, "unique", target_content_type, relation_type, content_type_id
            FROM fields
            ORDER BY id
            """;

        var fieldsByType = new Dictionary<long, List<FieldDto>>();
        await using (var fieldsCommand = new NpgsqlCommand(allFieldsSql, connection))
        await using (var fieldsReader = await fieldsCommand.ExecuteReaderAsync(ct))
        {
            while (await fieldsReader.ReadAsync(ct))
            {
                var fieldDto = ReadField(fieldsReader);
                var typeId = fieldsReader.GetInt64(8);
                if (!fieldsByType.TryGetValue(typeId, out var fieldList))
                {
                    fieldList = [];
                    fieldsByType[typeId] = fieldList;
                }
                fieldList.Add(fieldDto);
            }
        }

        return types.Select(t => new ContentTypeDto(
            Id: t.Id,
            Name: t.Name,
            PluralName: t.PluralName,
            ApiId: t.ApiId,
            Description: t.Description,
            Fields: fieldsByType.TryGetValue(t.Id, out var fList) ? fList : [],
            CreatedAt: t.CreatedAt,
            UpdatedAt: t.UpdatedAt
        )).ToList();
    }

    private async Task<ContentTypeDto?> FindOne(string sql, object value, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        long? id = null;
        string? name = null;
        string? pluralName = null;
        string? apiId = null;
        string? description = null;
        DateTime? createdAt = null;
        DateTime? updatedAt = null;

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.AddWithValue("v", value);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                id = reader.GetInt64(0);
                name = reader.GetString(1);
                pluralName = reader.GetString(2);
                apiId = reader.GetString(3);
                description = reader.IsDBNull(4) ? null : reader.GetString(4);
                createdAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                updatedAt = reader.IsDBNull(6) ? null : reader.GetDateTime(6);
            }
        }

        if (id is null)
        {
            return null;
        }

        var fields = await LoadFieldsForTypeAsync(connection, id.Value, ct);

        return new ContentTypeDto(
            Id: id,
            Name: name!,
            PluralName: pluralName!,
            ApiId: apiId!,
            Description: description,
            Fields: fields,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt
        );
    }

    public Task<ContentTypeDto?> ById(long id, CancellationToken ct)
    {
        const string sql = """
            SELECT id, name, plural_name, api_id, description, created_at, updated_at
            FROM content_types
            WHERE id = @v
            """;

        return FindOne(sql, id, ct);
    }

    public Task<ContentTypeDto?> ByApiId(string apiId, CancellationToken ct)
    {
        const string sql = """
            SELECT id, name, plural_name, api_id, description, created_at, updated_at
            FROM content_types
            WHERE api_id = @v
            """;

        return FindOne(sql, apiId, ct);
    }

    private static string FieldToSqlColumnDefinition(FieldDto field)
    {
        var sqlType = field.Type switch
        {
            FieldType.SHORT_TEXT => "VARCHAR(255)",
            FieldType.LONG_TEXT or FieldType.RICH_TEXT => "TEXT",
            FieldType.NUMBER => "NUMERIC",
            FieldType.BOOLEAN => "BOOLEAN",
            FieldType.DATETIME => "TIMESTAMP",
            FieldType.MEDIA or FieldType.RELATION => "BIGINT",
            _ => "TEXT"
        };

        var safeFieldName = SanitizeIdentifier(field.FieldName);
        var requiredConstraint = field.Required == true ? " NOT NULL" : "";
        var uniqueConstraint = field.Unique == true ? " UNIQUE" : "";

        return $"{safeFieldName} {sqlType}{requiredConstraint}{uniqueConstraint}";
    }

    public async Task<ContentTypeDto> Create(ContentTypeDto dto, CancellationToken ct)
    {
        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        const string insertTypeSql = """
            INSERT INTO content_types (name, plural_name, api_id, description)
            VALUES (@name, @pluralName, @apiId, @description)
            RETURNING id, created_at, updated_at
            """;

        await using var typeCommand = new NpgsqlCommand(insertTypeSql, connection, transaction);
        typeCommand.Parameters.AddWithValue("name", dto.Name);
        typeCommand.Parameters.AddWithValue("pluralName", dto.PluralName ?? dto.Name + "s");
        typeCommand.Parameters.AddWithValue("apiId", SanitizeIdentifier(dto.ApiId));
        typeCommand.Parameters.AddWithValue("description", (object?)dto.Description ?? DBNull.Value);

        await using var reader = await typeCommand.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        var id = reader.GetInt64(0);
        var createdAt = reader.GetDateTime(1);
        var updatedAt = reader.GetDateTime(2);
        await reader.CloseAsync();

        // Insert field definitions
        foreach (var field in dto.Fields ?? [])
        {
            const string insertFieldSql = """
                INSERT INTO fields (name, field_name, type, required, "unique", target_content_type, relation_type, content_type_id)
                VALUES (@name, @fieldName, @type, @required, @unique, @targetContentType, @relationType, @contentTypeId)
                """;

            await using var fieldCommand = new NpgsqlCommand(insertFieldSql, connection, transaction);
            fieldCommand.Parameters.AddWithValue("name", field.Name);
            fieldCommand.Parameters.AddWithValue("fieldName", SanitizeIdentifier(field.FieldName));
            fieldCommand.Parameters.AddWithValue("type", field.Type.ToString());
            fieldCommand.Parameters.AddWithValue("required", (object?)field.Required ?? DBNull.Value);
            fieldCommand.Parameters.AddWithValue("unique", (object?)field.Unique ?? DBNull.Value);
            fieldCommand.Parameters.AddWithValue("targetContentType", (object?)field.TargetContentType ?? DBNull.Value);
            fieldCommand.Parameters.AddWithValue("relationType", (object?)field.RelationType ?? DBNull.Value);
            fieldCommand.Parameters.AddWithValue("contentTypeId", id);

            await fieldCommand.ExecuteNonQueryAsync(ct);
        }

        // Dynamically create physical table ct_{apiId}
        var columnDefinitions = (dto.Fields ?? []).Select(FieldToSqlColumnDefinition).ToList();
        var columnsSql = columnDefinitions.Count > 0 ? ", " + string.Join(", ", columnDefinitions) : "";

        var safeApiId = SanitizeIdentifier(dto.ApiId);
        var createTableSql = $"""
            CREATE TABLE IF NOT EXISTS ct_{safeApiId} (
                id BIGSERIAL PRIMARY KEY,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                {columnsSql}
            )
            """;

        await using var createTableCommand = new NpgsqlCommand(createTableSql, connection, transaction);
        await createTableCommand.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);

        return new ContentTypeDto(
            Id: id,
            Name: dto.Name,
            PluralName: dto.PluralName ?? dto.Name + "s",
            ApiId: dto.ApiId,
            Description: dto.Description,
            Fields: dto.Fields,
            CreatedAt: createdAt,
            UpdatedAt: updatedAt
        );
    }

    public async Task<ContentTypeDto> Update(long id, ContentTypeDto dto, CancellationToken ct)
    {
        var existing = await ById(id, ct) 
            ?? throw new ApiForgeException("Content type not found", 404);

        await using var connection = await OpenConnectionAsync(ct);

        const string updateSql = """
            UPDATE content_types
            SET name = COALESCE(@name, name),
                plural_name = COALESCE(@pluralName, plural_name),
                description = COALESCE(@description, description),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = @id
            """;

        await using var command = new NpgsqlCommand(updateSql, connection);
        command.Parameters.AddWithValue("name", (object?)dto.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("pluralName", (object?)dto.PluralName ?? DBNull.Value);
        command.Parameters.AddWithValue("description", (object?)dto.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(ct);

        return (await ById(id, ct))!;
    }

    public async Task Delete(long id, CancellationToken ct)
    {
        var existing = await ById(id, ct) 
            ?? throw new ApiForgeException("Content type not found", 404);

        await using var connection = await OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var safeApiId = SanitizeIdentifier(existing.ApiId);
        var dropTableSql = $"DROP TABLE IF EXISTS ct_{safeApiId}; DELETE FROM content_types WHERE id = @id";

        await using var deleteCommand = new NpgsqlCommand(dropTableSql, connection, transaction);
        deleteCommand.Parameters.AddWithValue("id", id);

        await deleteCommand.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
    }
}

public sealed class PostgresContentStore(IContentTypeStore types, IConfiguration configuration) : IContentStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"]
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (value.Any(c => !(char.IsLetterOrDigit(c) || c == '_')))
        {
            throw new ApiForgeException("Invalid identifier", 400);
        }

        return value;
    }

    private async Task EnsureContentTypeExists(string apiId, CancellationToken ct)
    {
        var contentType = await types.ByApiId(apiId, ct);
        if (contentType is null)
        {
            throw new ApiForgeException("Content type not found", 404);
        }
    }

    private static IDictionary<string, object?> ReadRow(NpgsqlDataReader reader)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < reader.FieldCount; i++)
        {
            var columnName = reader.GetName(i);
            row[columnName] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }

        return row;
    }

    public async Task<IDictionary<string, object?>?> Create(string apiId, IDictionary<string, object?> values, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var keys = values.Keys.Select(SanitizeIdentifier).ToList();

        var columnsClause = string.Join(", ", keys);
        var parametersClause = string.Join(", ", keys.Select((_, i) => "@p" + i));

        var safeTableName = SanitizeIdentifier(apiId);
        var insertSql = $"INSERT INTO ct_{safeTableName} ({columnsClause}) VALUES ({parametersClause}) RETURNING *";

        await using var command = new NpgsqlCommand(insertSql, connection);

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            command.Parameters.AddWithValue("p" + i, values[key] ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> All(string apiId, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var safeTableName = SanitizeIdentifier(apiId);
        var sql = $"SELECT * FROM ct_{safeTableName} ORDER BY id";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<IDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> Search(string apiId, IDictionary<string, object?> filters, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var keys = filters.Keys.Select(SanitizeIdentifier).ToList();

        var whereClause = keys.Count == 0 
            ? "TRUE" 
            : string.Join(" AND ", keys.Select((key, i) => $"{key} = @p{i}"));

        var safeTableName = SanitizeIdentifier(apiId);
        var sql = $"SELECT * FROM ct_{safeTableName} WHERE {whereClause}";

        await using var command = new NpgsqlCommand(sql, connection);

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            command.Parameters.AddWithValue("p" + i, filters[key] ?? DBNull.Value);
        }

        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<IDictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add(ReadRow(reader));
        }

        return rows;
    }

    public async Task<IDictionary<string, object?>?> ById(string apiId, long id, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var safeTableName = SanitizeIdentifier(apiId);
        var sql = $"SELECT * FROM ct_{safeTableName} WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    public async Task<IDictionary<string, object?>?> Update(string apiId, long id, IDictionary<string, object?> values, CancellationToken ct)
    {
        await EnsureContentTypeExists(apiId, ct);

        await using var connection = await OpenConnectionAsync(ct);
        var keys = values.Keys.Select(SanitizeIdentifier).ToList();

        var setClause = string.Join(", ", keys.Select((key, i) => $"{key} = @p{i}"));
        var safeTableName = SanitizeIdentifier(apiId);
        var sql = $"UPDATE ct_{safeTableName} SET {setClause}, updated_at = CURRENT_TIMESTAMP WHERE id = @id RETURNING *";

        await using var command = new NpgsqlCommand(sql, connection);

        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            command.Parameters.AddWithValue("p" + i, values[key] ?? DBNull.Value);
        }

        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadRow(reader) : null;
    }

    public async Task Delete(string apiId, long id, CancellationToken ct)
    {
        var existing = await ById(apiId, id, ct);
        if (existing is null)
        {
            throw new ApiForgeException("Content not found", 404);
        }

        await using var connection = await OpenConnectionAsync(ct);
        var safeTableName = SanitizeIdentifier(apiId);
        var sql = $"DELETE FROM ct_{safeTableName} WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await command.ExecuteNonQueryAsync(ct);
    }
}
