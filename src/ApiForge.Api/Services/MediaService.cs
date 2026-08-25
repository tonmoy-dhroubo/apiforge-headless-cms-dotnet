using Microsoft.AspNetCore.Http;
using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class MediaService(IMediaStore store) : IMediaService
{
    public async Task<MediaRecord> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ApiForgeException("files is required", 400);
        }

        return await store.Save(file, ct);
    }

    public async Task<IReadOnlyList<MediaRecord>> GetAllAsync(CancellationToken ct = default)
    {
        return await store.All(ct);
    }

    public async Task<MediaRecord> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await store.ById(id, ct);
        if (result is null)
        {
            throw new ApiForgeException("Media not found", 404);
        }
        return result;
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        var removed = await store.Remove(id, ct);
        if (!removed)
        {
            throw new ApiForgeException("Media not found", 404);
        }
    }

    public async Task<(string Path, string Mime, string Name)> GetFileByNameAsync(string fileName, CancellationToken ct = default)
    {
        var media = await store.ByFile(fileName, ct);
        if (media is null || !File.Exists(media.Path))
        {
            throw new ApiForgeException("File not found", 404);
        }

        return (media.Path, media.Mime ?? "application/octet-stream", media.Name);
    }
}
