using ApiForge.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ApiForge.Infrastructure;

public sealed class PostgresMediaStore(IConfiguration configuration, IBlobStorage blobStorage) : IMediaStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"] 
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct = default)
    {
        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);

        await using var stream = file.OpenReadStream();
        var blob = await blobStorage.UploadAsync(originalFileName, stream, file.ContentType, ct);
        var hash = Path.GetFileNameWithoutExtension(blob.StorageKey);
        var sizeInKb = blob.SizeBytes / 1024d;

        try
        {
            await using var connection = await OpenConnectionAsync(ct);

            const string insertSql = """
                INSERT INTO media (name, hash, ext, mime, size, url, provider)
                VALUES (@name, @hash, @ext, @mime, @size, @url, @provider)
                RETURNING id, created_at
                """;

            await using var command = new NpgsqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("name", originalFileName);
            command.Parameters.AddWithValue("hash", hash);
            command.Parameters.AddWithValue("ext", extension);
            command.Parameters.AddWithValue("mime", (object?)file.ContentType ?? DBNull.Value);
            command.Parameters.AddWithValue("size", sizeInKb);
            command.Parameters.AddWithValue("url", blob.Url);
            command.Parameters.AddWithValue("provider", blobStorage.ProviderName);

            await using var reader = await command.ExecuteReaderAsync(ct);
            await reader.ReadAsync(ct);

            var id = reader.GetInt64(0);

            return new MediaRecord(
                Id: id,
                Name: originalFileName,
                AlternativeText: null,
                Caption: null,
                Width: null,
                Height: null,
                Hash: hash,
                Ext: extension,
                Mime: file.ContentType,
                Size: sizeInKb,
                Url: blob.Url,
                Provider: blobStorage.ProviderName,
                Path: blob.StorageKey
            );
        }
        catch
        {
            // Clean up blob on database transaction failure
            try
            {
                await blobStorage.DeleteAsync(blob.StorageKey, ct);
            }
            catch
            {
                // Suppress rollback cleanup failure
            }

            throw;
        }
    }

    private MediaRecord ReadMediaRecord(NpgsqlDataReader reader)
    {
        var id = reader.GetInt64(0);
        var name = reader.IsDBNull(1) ? "upload" : reader.GetString(1);
        var altText = reader.IsDBNull(2) ? null : reader.GetString(2);
        var caption = reader.IsDBNull(3) ? null : reader.GetString(3);
        var width = reader.IsDBNull(4) ? (int?)null : reader.GetInt32(4);
        var height = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5);
        var hash = reader.GetString(6);
        var ext = reader.GetString(7);
        var mime = reader.IsDBNull(8) ? null : reader.GetString(8);
        var size = reader.IsDBNull(9) ? 0d : Convert.ToDouble(reader.GetValue(9));
        var url = reader.IsDBNull(10) ? "/api/upload/files/" + hash + ext : reader.GetString(10);
        var provider = reader.IsDBNull(11) ? blobStorage.ProviderName : reader.GetString(11);
        var storageKey = hash + ext;

        return new MediaRecord(
            Id: id,
            Name: name,
            AlternativeText: altText,
            Caption: caption,
            Width: width,
            Height: height,
            Hash: hash,
            Ext: ext,
            Mime: mime,
            Size: size,
            Url: url,
            Provider: provider,
            Path: storageKey
        );
    }

    private const string BaseMediaSelectSql = """
        SELECT id, name, alternative_text, caption, width, height, hash, ext, mime, size, url, provider
        FROM media
        """;

    public async Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var sql = $"{BaseMediaSelectSql} ORDER BY id";

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var records = new List<MediaRecord>();
        while (await reader.ReadAsync(ct))
        {
            records.Add(ReadMediaRecord(reader));
        }

        return records;
    }

    public async Task<MediaRecord?> ById(long id, CancellationToken ct = default)
    {
        await using var connection = await OpenConnectionAsync(ct);
        var sql = $"{BaseMediaSelectSql} WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadMediaRecord(reader) : null;
    }

    public async Task<MediaRecord?> ByFile(string filename, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(filename);
        var hash = Path.GetFileNameWithoutExtension(filename);

        await using var connection = await OpenConnectionAsync(ct);
        var sql = $"{BaseMediaSelectSql} WHERE hash = @h AND ext = @e";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("h", hash);
        command.Parameters.AddWithValue("e", extension);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadMediaRecord(reader) : null;
    }

    public async Task<bool> Remove(long id, CancellationToken ct = default)
    {
        var existing = await ById(id, ct);
        if (existing is null)
        {
            return false;
        }

        await using var connection = await OpenConnectionAsync(ct);
        const string sql = "DELETE FROM media WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        var success = rowsAffected > 0;

        if (success)
        {
            await blobStorage.DeleteAsync(existing.Path, ct);
        }

        return success;
    }
}
