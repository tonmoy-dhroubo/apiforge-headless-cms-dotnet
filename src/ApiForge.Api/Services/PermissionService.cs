using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class PermissionService(IPermissionStore store) : IPermissionService
{
    public async Task<ApiPermissionDto> CreateApiPermissionAsync(ApiPermissionDto dto, CancellationToken ct = default)
    {
        return await store.Add(dto, ct);
    }

    public async Task<IReadOnlyList<ApiPermissionDto>> GetAllApiPermissionsAsync(CancellationToken ct = default)
    {
        return await store.ApiAll(ct);
    }

    public async Task<IReadOnlyList<ApiPermissionDto>> GetApiPermissionsByContentTypeAsync(string contentTypeApiId, CancellationToken ct = default)
    {
        var all = await store.ApiAll(ct);
        return all.Where(x => x.ContentTypeApiId == contentTypeApiId).ToList();
    }

    public async Task<ApiPermissionDto> GetApiPermissionByIdAsync(long id, CancellationToken ct = default)
    {
        var item = await store.ApiBy(id, ct);
        if (item is null)
        {
            throw new ApiForgeException("API permission not found", 404);
        }
        return item;
    }

    public async Task<ApiPermissionDto> UpdateApiPermissionAsync(long id, ApiPermissionDto dto, CancellationToken ct = default)
    {
        var updated = await store.Update(dto with { Id = id }, ct);
        if (updated is null)
        {
            throw new ApiForgeException("API permission not found", 404);
        }
        return updated;
    }

    public async Task DeleteApiPermissionAsync(long id, CancellationToken ct = default)
    {
        var removed = await store.RemoveApi(id, ct);
        if (!removed)
        {
            throw new ApiForgeException("API permission not found", 404);
        }
    }

    public async Task<bool> CheckApiPermissionAsync(PermissionCheck check, CancellationToken ct = default)
    {
        var all = await store.ApiAll(ct);
        return all.Any(p =>
            p.ContentTypeApiId == check.ContentTypeApiId &&
            p.Endpoint == check.Endpoint &&
            p.Method == check.Method &&
            (p.AllowedRoles ?? []).Intersect(check.UserRoles ?? []).Any());
    }

    public async Task<ContentPermissionDto> CreateContentPermissionAsync(ContentPermissionDto dto, CancellationToken ct = default)
    {
        return await store.Add(dto, ct);
    }

    public async Task<IReadOnlyList<ContentPermissionDto>> GetAllContentPermissionsAsync(CancellationToken ct = default)
    {
        return await store.ContentAll(ct);
    }

    public async Task<IReadOnlyList<ContentPermissionDto>> GetContentPermissionsByContentTypeAsync(string contentTypeApiId, CancellationToken ct = default)
    {
        var all = await store.ContentAll(ct);
        return all.Where(x => x.ContentTypeApiId == contentTypeApiId).ToList();
    }

    public async Task<ContentPermissionDto> GetContentPermissionByIdAsync(long id, CancellationToken ct = default)
    {
        var item = await store.ContentBy(id, ct);
        if (item is null)
        {
            throw new ApiForgeException("Content permission not found", 404);
        }
        return item;
    }

    public async Task<ContentPermissionDto> UpdateContentPermissionAsync(long id, ContentPermissionDto dto, CancellationToken ct = default)
    {
        var updated = await store.Update(dto with { Id = id }, ct);
        if (updated is null)
        {
            throw new ApiForgeException("Content permission not found", 404);
        }
        return updated;
    }

    public async Task DeleteContentPermissionAsync(long id, CancellationToken ct = default)
    {
        var removed = await store.RemoveContent(id, ct);
        if (!removed)
        {
            throw new ApiForgeException("Content permission not found", 404);
        }
    }

    public async Task<bool> CheckContentPermissionAsync(PermissionCheck check, CancellationToken ct = default)
    {
        var all = await store.ContentAll(ct);
        return all.Any(p =>
            p.ContentTypeApiId == check.ContentTypeApiId &&
            p.Action == check.Action &&
            (p.AllowedRoles ?? []).Intersect(check.UserRoles ?? []).Any());
    }
}
