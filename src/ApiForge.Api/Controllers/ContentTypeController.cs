using ApiForge.Core;
using ApiForge.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
[Route("api/content-types")]
[Authorize]
public class ContentTypeController(IContentTypeService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ContentTypeDto>>> Create([FromBody] ContentTypeDto dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(dto, ct);
        return Ok(ApiResponse<ContentTypeDto>.Ok(result, "Content type created successfully"));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContentTypeDto>>>> GetAll(CancellationToken ct)
    {
        var result = await service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<ContentTypeDto>>.Ok(result));
    }

    [HttpGet("api-id/{apiId}")]
    public async Task<ActionResult<ApiResponse<ContentTypeDto>>> GetByApiId(string apiId, CancellationToken ct)
    {
        var result = await service.GetByApiIdAsync(apiId, ct);
        return Ok(ApiResponse<ContentTypeDto>.Ok(result));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<ContentTypeDto>>> GetById(long id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return Ok(ApiResponse<ContentTypeDto>.Ok(result));
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<ContentTypeDto>>> Update(long id, [FromBody] ContentTypeDto dto, CancellationToken ct)
    {
        var result = await service.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<ContentTypeDto>.Ok(result, "Content type updated successfully"));
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "Content type deleted successfully"));
    }
}
