using Microsoft.AspNetCore.Http;
using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class MediaService(IMediaStore store) : IMediaService
{
    private const long MaxFileSizeInBytes = 25 * 1024 * 1024; // 25 MB
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".bat", ".cmd", ".sh", ".dll", ".so", ".dylib", ".com", ".msi", ".vbs", ".ps1", ".jar"
    };

    public async Task<MediaRecord> UploadAsync(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new ApiForgeException("files is required", 400);
        }

        if (file.Length > MaxFileSizeInBytes)
        {
            throw new ApiForgeException($"File size exceeds the maximum allowed limit of {MaxFileSizeInBytes / (1024 * 1024)} MB", 400);
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.IsNullOrEmpty(extension) && BlockedExtensions.Contains(extension))
        {
            throw new ApiForgeException($"Files with extension '{extension}' are not allowed", 400);
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
