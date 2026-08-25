using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace ApiForge.Infrastructure;

public sealed class MediaStore(IWebHostEnvironment environment)
{
    private readonly ConcurrentDictionary<long, MediaRecord> _items = new(); private long _next;
    private string Root => Path.Combine(environment.ContentRootPath, "uploads");
    public async Task<MediaRecord> Save(IFormFile file, CancellationToken ct)
    {
        Directory.CreateDirectory(Root); var original = Path.GetFileName(file.FileName); var ext = Path.GetExtension(original); var hash = Guid.NewGuid().ToString(); var stored = hash + ext; var path = Path.Combine(Root, stored);
        await using (var output = File.Create(path)) await file.CopyToAsync(output, ct);
        var item = new MediaRecord(Interlocked.Increment(ref _next), original, hash, ext, file.ContentType, file.Length / 1024d, "/api/upload/files/" + stored, "local", path); _items[item.Id] = item; return item;
    }
    public IReadOnlyList<MediaRecord> All() => _items.Values.OrderBy(x => x.Id).ToList(); public MediaRecord? ById(long id) => _items.GetValueOrDefault(id); public MediaRecord? ByFile(string file) => _items.Values.FirstOrDefault(x => x.Hash + x.Ext == file);
    public bool Remove(long id) { if (!_items.TryRemove(id, out var x)) return false; try { File.Delete(x.Path); } catch { } return true; }
    public sealed record MediaRecord(long Id, string Name, string Hash, string Ext, string? Mime, double Size, string Url, string Provider, [property: JsonIgnore] string Path);
}
