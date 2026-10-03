namespace ApiForge.Core;

public interface IContentTypeStore
{
    Task<IReadOnlyList<ContentTypeDto>> All(CancellationToken ct = default);
    Task<ContentTypeDto?> ById(long id, CancellationToken ct = default);
    Task<ContentTypeDto?> ByApiId(string apiId, CancellationToken ct = default);
    Task<ContentTypeDto> Create(ContentTypeDto dto, CancellationToken ct = default);
    Task<ContentTypeDto> Update(long id, ContentTypeDto dto, CancellationToken ct = default);
    Task Delete(long id, CancellationToken ct = default);
}
