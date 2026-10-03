namespace ApiForge.Core;

public interface IPermissionStore
{
    Task<ApiPermissionDto> Add(ApiPermissionDto value, CancellationToken ct = default);
    Task<ContentPermissionDto> Add(ContentPermissionDto value, CancellationToken ct = default);
    Task<IReadOnlyList<ApiPermissionDto>> ApiAll(CancellationToken ct = default);
    Task<IReadOnlyList<ContentPermissionDto>> ContentAll(CancellationToken ct = default);
    Task<ApiPermissionDto?> ApiBy(long id, CancellationToken ct = default);
    Task<ContentPermissionDto?> ContentBy(long id, CancellationToken ct = default);
    Task<ApiPermissionDto?> Update(ApiPermissionDto value, CancellationToken ct = default);
    Task<ContentPermissionDto?> Update(ContentPermissionDto value, CancellationToken ct = default);
    Task<bool> RemoveApi(long id, CancellationToken ct = default);
    Task<bool> RemoveContent(long id, CancellationToken ct = default);
}
