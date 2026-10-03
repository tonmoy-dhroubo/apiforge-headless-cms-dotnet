using System.Collections.Concurrent;
using ApiForge.Core;
using Microsoft.AspNetCore.Http;

namespace ApiForge.Infrastructure;

public sealed class MediaStore(IBlobStorage blobStorage) : IMediaStore
{
    private readonly ConcurrentDictionary<long, MediaRecord> _items = new();
    private long _nextId;

    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct = default)
    {
        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName);

        await using var stream = file.OpenReadStream();
        var blob = await blobStorage.UploadAsync(originalFileName, stream, file.ContentType, ct);

        var id = Interlocked.Increment(ref _nextId);
        var sizeInKb = blob.SizeBytes / 1024d;
        var hash = Path.GetFileNameWithoutExtension(blob.StorageKey);

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
            Url: blob.Url,
            Provider: blobStorage.ProviderName,
            Path: blob.StorageKey
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
        var record = _items.Values.FirstOrDefault(item => 
            string.Equals(item.Path, filename, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Hash + item.Ext, filename, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(record);
    }

    public async Task<bool> Remove(long id, CancellationToken ct = default)
    {
        if (!_items.TryRemove(id, out var record))
        {
            return false;
        }

        await blobStorage.DeleteAsync(record.Path, ct);
        return true;
    }
}
