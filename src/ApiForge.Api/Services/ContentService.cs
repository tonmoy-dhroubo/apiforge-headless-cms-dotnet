using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class ContentService(IContentStore store) : IContentService
{
    public async Task<IDictionary<string, object?>> CreateAsync(string apiId, IDictionary<string, object?> data, CancellationToken ct = default)
    {
        var result = await store.Create(apiId, data, ct);
        if (result is null)
        {
            throw new ApiForgeException("Failed to create content", 500);
        }
        return result;
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> GetAllAsync(string apiId, CancellationToken ct = default)
    {
        return await store.All(apiId, ct);
    }

    public async Task<IReadOnlyList<IDictionary<string, object?>>> SearchAsync(string apiId, IDictionary<string, object?> filters, CancellationToken ct = default)
    {
        return await store.Search(apiId, filters, ct);
    }

    public async Task<IDictionary<string, object?>> GetByIdAsync(string apiId, long id, CancellationToken ct = default)
    {
        var result = await store.ById(apiId, id, ct);
        if (result is null)
        {
            throw new ApiForgeException("Content not found", 404);
        }
        return result;
    }

    public async Task<IDictionary<string, object?>> UpdateAsync(string apiId, long id, IDictionary<string, object?> data, CancellationToken ct = default)
    {
        var result = await store.Update(apiId, id, data, ct);
        if (result is null)
        {
            throw new ApiForgeException("Content not found", 404);
        }
        return result;
    }

    public async Task DeleteAsync(string apiId, long id, CancellationToken ct = default)
    {
        await store.Delete(apiId, id, ct);
    }
}
