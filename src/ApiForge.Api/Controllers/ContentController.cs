using System.Text.Json;
using ApiForge.Api.Helpers;
using ApiForge.Core;
using ApiForge.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
[Route("api/content")]
[Authorize]
public class ContentController(IContentService service) : ControllerBase
{
    [HttpPost("{apiId}")]
    public async Task<ActionResult<ApiResponse<IDictionary<string, object?>>>> Create(string apiId, [FromBody] JsonElement body, CancellationToken ct)
    {
        var data = JsonHelper.ToDictionary(body);
        var result = await service.CreateAsync(apiId, data, ct);
        return Ok(ApiResponse<IDictionary<string, object?>>.Ok(result, "Content created successfully"));
    }

    [HttpGet("{apiId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IDictionary<string, object?>>>>> GetAll(string apiId, CancellationToken ct)
    {
        var result = await service.GetAllAsync(apiId, ct);
        return Ok(ApiResponse<IReadOnlyList<IDictionary<string, object?>>>.Ok(result));
    }

    [HttpPost("{apiId}/search")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<IDictionary<string, object?>>>>> Search(string apiId, [FromBody] JsonElement body, CancellationToken ct)
    {
        var filters = JsonHelper.ToDictionary(body);
        var result = await service.SearchAsync(apiId, filters, ct);
        return Ok(ApiResponse<IReadOnlyList<IDictionary<string, object?>>>.Ok(result));
    }

    [HttpGet("{apiId}/{id:long}")]
    public async Task<ActionResult<ApiResponse<IDictionary<string, object?>>>> GetById(string apiId, long id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(apiId, id, ct);
        return Ok(ApiResponse<IDictionary<string, object?>>.Ok(result));
    }

    [HttpPut("{apiId}/{id:long}")]
    public async Task<ActionResult<ApiResponse<IDictionary<string, object?>>>> Update(string apiId, long id, [FromBody] JsonElement body, CancellationToken ct)
    {
        var data = JsonHelper.ToDictionary(body);
        var result = await service.UpdateAsync(apiId, id, data, ct);
        return Ok(ApiResponse<IDictionary<string, object?>>.Ok(result, "Content updated successfully"));
    }

    [HttpDelete("{apiId}/{id:long}")]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(string apiId, long id, CancellationToken ct)
    {
        await service.DeleteAsync(apiId, id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "Content deleted successfully"));
    }
}
