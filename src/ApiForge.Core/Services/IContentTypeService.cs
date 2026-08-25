using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IContentTypeService
{
    Task<IReadOnlyList<ContentTypeDto>> GetAllAsync(CancellationToken ct = default);
    Task<ContentTypeDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ContentTypeDto> GetByApiIdAsync(string apiId, CancellationToken ct = default);
    Task<ContentTypeDto> CreateAsync(ContentTypeDto dto, CancellationToken ct = default);
    Task<ContentTypeDto> UpdateAsync(long id, ContentTypeDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
