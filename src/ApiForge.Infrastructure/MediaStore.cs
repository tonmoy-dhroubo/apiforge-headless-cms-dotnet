using System.Collections.Concurrent;
using ApiForge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ApiForge.Infrastructure;

public sealed class MediaStore(IWebHostEnvironment environment) : IMediaStore
{
    private readonly ConcurrentDictionary<long, MediaRecord> _items = new();
    private long _nextId;

    private string UploadRootDirectory => Path.Combine(environment.ContentRootPath, "uploads");

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

        var id = Interlocked.Increment(ref _nextId);
        var sizeInKb = file.Length / 1024d;
        var publicUrl = "/api/upload/files/" + storedFileName;

        var record = new MediaRecord(
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

        _items[record.Id] = record;
        return record;
    }

    public Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default)
    {
        var records = _items.Values
            .OrderBy(item => item.Id)
            .ToList();

        return Task.FromResult<IReadOnlyList<MediaRecord>>(records);
    }

    public Task<MediaRecord?> ById(long id, CancellationToken ct = default)
    {
        _items.TryGetValue(id, out var record);
        return Task.FromResult(record);
    }

    public Task<MediaRecord?> ByFile(string filename, CancellationToken ct = default)
    {
        var record = _items.Values.FirstOrDefault(item => (item.Hash + item.Ext) == filename);
        return Task.FromResult(record);
    }

    public Task<bool> Remove(long id, CancellationToken ct = default)
    {
        if (!_items.TryRemove(id, out var record))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (File.Exists(record.Path))
            {
                File.Delete(record.Path);
            }
        }
        catch
        {
            // Suppress file deletion errors if file is locked or missing
        }

        return Task.FromResult(true);
    }
}
