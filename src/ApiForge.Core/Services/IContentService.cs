namespace ApiForge.Core.Services;

public interface IContentService
{
    Task<IDictionary<string, object?>> CreateAsync(string apiId, IDictionary<string, object?> data, CancellationToken ct = default);
    Task<IReadOnlyList<IDictionary<string, object?>>> GetAllAsync(string apiId, CancellationToken ct = default);
    Task<IReadOnlyList<IDictionary<string, object?>>> SearchAsync(string apiId, IDictionary<string, object?> filters, CancellationToken ct = default);
    Task<IDictionary<string, object?>> GetByIdAsync(string apiId, long id, CancellationToken ct = default);
    Task<IDictionary<string, object?>> UpdateAsync(string apiId, long id, IDictionary<string, object?> data, CancellationToken ct = default);
    Task DeleteAsync(string apiId, long id, CancellationToken ct = default);
}
