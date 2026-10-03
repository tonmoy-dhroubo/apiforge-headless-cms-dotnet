using Microsoft.AspNetCore.Http;
using ApiForge.Core;

namespace ApiForge.Core.Services;

public sealed record MediaStreamResult(
    Stream Stream,
    string MimeType,
    string FileName
);

public interface IMediaService
{
    Task<MediaRecord> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<MediaRecord>> GetAllAsync(CancellationToken ct = default);
    Task<MediaRecord> GetByIdAsync(long id, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<MediaStreamResult> GetFileStreamByNameAsync(string fileName, CancellationToken ct = default);
}
