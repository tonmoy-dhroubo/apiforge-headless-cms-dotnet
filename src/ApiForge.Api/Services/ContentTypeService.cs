using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class ContentTypeService(IContentTypeStore store) : IContentTypeService
{
    public async Task<IReadOnlyList<ContentTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await store.All(ct);
    }

    public async Task<ContentTypeDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var result = await store.ById(id, ct);
        if (result is null)
        {
            throw new ApiForgeException("Content type not found", 404);
        }

        return result;
    }

    public async Task<ContentTypeDto> GetByApiIdAsync(string apiId, CancellationToken ct = default)
    {
        var result = await store.ByApiId(apiId, ct);
        if (result is null)
        {
            throw new ApiForgeException("Content type not found", 404);
        }

        return result;
    }

    public async Task<ContentTypeDto> CreateAsync(ContentTypeDto dto, CancellationToken ct = default)
    {
        return await store.Create(dto, ct);
    }

    public async Task<ContentTypeDto> UpdateAsync(long id, ContentTypeDto dto, CancellationToken ct = default)
    {
        return await store.Update(id, dto, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await store.Delete(id, ct);
    }
}
