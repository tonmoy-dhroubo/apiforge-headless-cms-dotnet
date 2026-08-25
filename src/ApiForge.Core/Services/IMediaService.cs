using Microsoft.AspNetCore.Http;
using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IMediaService
{
    Task<MediaRecord> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<MediaRecord>> GetAllAsync(CancellationToken ct = default);
    Task<MediaRecord> GetByIdAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<(string Path, string Mime, string Name)> GetFileByNameAsync(string fileName, CancellationToken ct = default);
}
