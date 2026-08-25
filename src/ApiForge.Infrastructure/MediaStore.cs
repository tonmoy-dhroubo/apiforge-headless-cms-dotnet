using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using ApiForge.Core;

namespace ApiForge.Infrastructure;

public sealed class MediaStore(IWebHostEnvironment environment) : IMediaStore
{
    private readonly ConcurrentDictionary<long, MediaRecord> _items = new(); private long _next;
    private string Root => Path.Combine(environment.ContentRootPath, "uploads");
    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Root); var original = Path.GetFileName(file.FileName); var ext = Path.GetExtension(original); var hash = Guid.NewGuid().ToString(); var stored = hash + ext; var path = Path.Combine(Root, stored);
        await using (var output = File.Create(path)) await file.CopyToAsync(output, ct);
        var item = new MediaRecord(Interlocked.Increment(ref _next), original, null, null, null, null, hash, ext, file.ContentType, file.Length / 1024d, "/api/upload/files/" + stored, "local", path); _items[item.Id] = item; return item;
    }
    public Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MediaRecord>>(_items.Values.OrderBy(x => x.Id).ToList()); public Task<MediaRecord?> ById(long id, CancellationToken ct = default) => Task.FromResult(_items.GetValueOrDefault(id)); public Task<MediaRecord?> ByFile(string file, CancellationToken ct = default) => Task.FromResult(_items.Values.FirstOrDefault(x => x.Hash + x.Ext == file));
    public Task<bool> Remove(long id, CancellationToken ct = default) { if (!_items.TryRemove(id, out var x)) return Task.FromResult(false); try { File.Delete(x.Path); } catch { } return Task.FromResult(true); }
}
