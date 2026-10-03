namespace ApiForge.Core;

public interface IMediaStore
{
    Task<MediaRecord> Save(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct = default);
    Task<IReadOnlyList<MediaRecord>> All(CancellationToken ct = default);
    Task<MediaRecord?> ById(long id, CancellationToken ct = default);
    Task<MediaRecord?> ByFile(string filename, CancellationToken ct = default);
    Task<bool> Remove(long id, CancellationToken ct = default);
}
