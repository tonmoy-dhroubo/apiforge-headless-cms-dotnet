using System.Collections.Concurrent;
using ApiForge.Core;

namespace ApiForge.Infrastructure;

public sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderName => "memory";

    public async Task<BlobMetadata> UploadAsync(string fileName, Stream content, string? contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var hash = Guid.NewGuid().ToString();
        var storageKey = hash + extension;

        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        _blobs[storageKey] = bytes;

        var url = "/api/upload/files/" + storageKey;
        return new BlobMetadata(storageKey, url, bytes.Length);
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        if (_blobs.TryGetValue(storageKey, out var bytes))
        {
            Stream stream = new MemoryStream(bytes, writable: false);
            return Task.FromResult<Stream?>(stream);
        }

        return Task.FromResult<Stream?>(null);
    }

    public Task<bool> DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        return Task.FromResult(_blobs.TryRemove(storageKey, out _));
    }

    public Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        return Task.FromResult(_blobs.ContainsKey(storageKey));
    }
}
