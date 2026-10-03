using ApiForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ApiForge.Infrastructure;

public sealed class PostgresMediaStore(IWebHostEnvironment environment, IConfiguration configuration) : IMediaStore
{
    private readonly string _connectionString = configuration["Storage:ConnectionString"] 
        ?? throw new InvalidOperationException("Storage:ConnectionString is required");

    private string UploadRootDirectory => Path.Combine(environment.ContentRootPath, "uploads");

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct = default)
    {
        Directory.CreateDirectory(UploadRootDirectory);

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);
        var hash = Guid.NewGuid().ToString();
        var storedFileName = hash + extension;
        var physicalPath = Path.Combine(UploadRootDirectory, storedFileName);

        await using (var outputStream = File.Create(physicalPath))
        {
            await file.CopyToAsync(outputStream, ct);
        }

        try
        {
            await using var connection = OpenConnection();

            const string insertSql = """
                INSERT INTO media (name, hash, ext, mime, size, url, provider)
                VALUES (@name, @hash, @ext, @mime, @size, @url, 'local')
                RETURNING id, created_at
                """;

            var sizeInKb = file.Length / 1024d;
            var publicUrl = "/api/upload/files/" + storedFileName;

            await using var command = new NpgsqlCommand(insertSql, connection);
            command.Parameters.AddWithValue("name", originalFileName);
            command.Parameters.AddWithValue("hash", hash);
            command.Parameters.AddWithValue("ext", extension);
            command.Parameters.AddWithValue("mime", (object?)file.ContentType ?? DBNull.Value);
            command.Parameters.AddWithValue("size", sizeInKb);
            command.Parameters.AddWithValue("url", publicUrl);

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
                Url: publicUrl,
                Provider: "local",
                Path: physicalPath
            );
        }
        catch
        {
            try
            {
                if (File.Exists(physicalPath))
                {
                    File.Delete(physicalPath);
                }
            }
            catch
            {
                // Suppress disk cleanup failure during rollback
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
        var provider = reader.IsDBNull(11) ? "local" : reader.GetString(11);
        var physicalPath = Path.Combine(UploadRootDirectory, hash + ext);

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
            Path: physicalPath
        );
    }

    private const string BaseMediaSelectSql = """
        SELECT id, name, alternative_text, caption, width, height, hash, ext, mime, size, url, provider
        FROM media
        """;

    public async Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default)
    {
        await using var connection = OpenConnection();
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
        await using var connection = OpenConnection();
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

        await using var connection = OpenConnection();
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

        await using var connection = OpenConnection();
        const string sql = "DELETE FROM media WHERE id = @id";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        var rowsAffected = await command.ExecuteNonQueryAsync(ct);
        var success = rowsAffected > 0;

        if (success)
        {
            try
            {
                if (File.Exists(existing.Path))
                {
                    File.Delete(existing.Path);
                }
            }
            catch
            {
                // Suppress disk cleanup failure
            }
        }

        return success;
    }
}
