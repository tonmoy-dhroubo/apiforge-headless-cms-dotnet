namespace ApiForge.Core;

public interface IContentStore
{
    Task<IDictionary<string, object?>?> Create(string apiId, IDictionary<string, object?> values, CancellationToken ct = default);
    Task<IReadOnlyList<IDictionary<string, object?>>> All(string apiId, CancellationToken ct = default);
    Task<IReadOnlyList<IDictionary<string, object?>>> Search(string apiId, IDictionary<string, object?> filters, CancellationToken ct = default);
    Task<IDictionary<string, object?>?> ById(string apiId, long id, CancellationToken ct = default);
    Task<IDictionary<string, object?>?> Update(string apiId, long id, IDictionary<string, object?> values, CancellationToken ct = default);
    Task Delete(string apiId, long id, CancellationToken ct = default);
}
