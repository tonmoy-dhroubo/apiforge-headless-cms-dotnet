using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IPermissionService
{
    Task<ApiPermissionDto> CreateApiPermissionAsync(ApiPermissionDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ApiPermissionDto>> GetAllApiPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApiPermissionDto>> GetApiPermissionsByContentTypeAsync(string contentTypeApiId, CancellationToken ct = default);
    Task<ApiPermissionDto> GetApiPermissionByIdAsync(long id, CancellationToken ct = default);
    Task<ApiPermissionDto> UpdateApiPermissionAsync(long id, ApiPermissionDto dto, CancellationToken ct = default);
    Task DeleteApiPermissionAsync(long id, CancellationToken ct = default);
    Task<bool> CheckApiPermissionAsync(PermissionCheck check, CancellationToken ct = default);

    Task<ContentPermissionDto> CreateContentPermissionAsync(ContentPermissionDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ContentPermissionDto>> GetAllContentPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ContentPermissionDto>> GetContentPermissionsByContentTypeAsync(string contentTypeApiId, CancellationToken ct = default);
    Task<ContentPermissionDto> GetContentPermissionByIdAsync(long id, CancellationToken ct = default);
    Task<ContentPermissionDto> UpdateContentPermissionAsync(long id, ContentPermissionDto dto, CancellationToken ct = default);
    Task DeleteContentPermissionAsync(long id, CancellationToken ct = default);
    Task<bool> CheckContentPermissionAsync(PermissionCheck check, CancellationToken ct = default);
}
