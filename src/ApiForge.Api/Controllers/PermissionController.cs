using ApiForge.Core;
using ApiForge.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionController(IPermissionService service) : ControllerBase
{
    [HttpPost("api")]
    public async Task<ActionResult<ApiResponse<ApiPermissionDto>>> CreateApiPermission([FromBody] ApiPermissionDto dto, CancellationToken ct)
    {
        var result = await service.CreateApiPermissionAsync(dto, ct);
        return Ok(ApiResponse<ApiPermissionDto>.Ok(result, "API permission created successfully"));
    }

    [HttpGet("api")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiPermissionDto>>>> GetAllApiPermissions(CancellationToken ct)
    {
        var result = await service.GetAllApiPermissionsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<ApiPermissionDto>>.Ok(result));
    }

    [HttpGet("api/content-type/{contentTypeApiId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ApiPermissionDto>>>> GetApiPermissionsByContentType(string contentTypeApiId, CancellationToken ct)
    {
        var result = await service.GetApiPermissionsByContentTypeAsync(contentTypeApiId, ct);
        return Ok(ApiResponse<IReadOnlyList<ApiPermissionDto>>.Ok(result));
    }

    [HttpGet("api/{id:long}")]
    public async Task<ActionResult<ApiResponse<ApiPermissionDto>>> GetApiPermissionById(long id, CancellationToken ct)
    {
        var result = await service.GetApiPermissionByIdAsync(id, ct);
        return Ok(ApiResponse<ApiPermissionDto>.Ok(result));
    }

    [HttpPut("api/{id:long}")]
    public async Task<ActionResult<ApiResponse<ApiPermissionDto>>> UpdateApiPermission(long id, [FromBody] ApiPermissionDto dto, CancellationToken ct)
    {
        var result = await service.UpdateApiPermissionAsync(id, dto, ct);
        return Ok(ApiResponse<ApiPermissionDto>.Ok(result, "API permission updated successfully"));
    }

    [HttpDelete("api/{id:long}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteApiPermission(long id, CancellationToken ct)
    {
        await service.DeleteApiPermissionAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "API permission deleted successfully"));
    }

    [HttpPost("api/check")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckApiPermission([FromBody] PermissionCheck check, CancellationToken ct)
    {
        var result = await service.CheckApiPermissionAsync(check, ct);
        return Ok(ApiResponse<bool>.Ok(result));
    }

    [HttpPost("content")]
    public async Task<ActionResult<ApiResponse<ContentPermissionDto>>> CreateContentPermission([FromBody] ContentPermissionDto dto, CancellationToken ct)
    {
        var result = await service.CreateContentPermissionAsync(dto, ct);
        return Ok(ApiResponse<ContentPermissionDto>.Ok(result, "Content permission created successfully"));
    }

    [HttpGet("content")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContentPermissionDto>>>> GetAllContentPermissions(CancellationToken ct)
    {
        var result = await service.GetAllContentPermissionsAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<ContentPermissionDto>>.Ok(result));
    }

    [HttpGet("content/content-type/{contentTypeApiId}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ContentPermissionDto>>>> GetContentPermissionsByContentType(string contentTypeApiId, CancellationToken ct)
    {
        var result = await service.GetContentPermissionsByContentTypeAsync(contentTypeApiId, ct);
        return Ok(ApiResponse<IReadOnlyList<ContentPermissionDto>>.Ok(result));
    }

    [HttpGet("content/{id:long}")]
    public async Task<ActionResult<ApiResponse<ContentPermissionDto>>> GetContentPermissionById(long id, CancellationToken ct)
    {
        var result = await service.GetContentPermissionByIdAsync(id, ct);
        return Ok(ApiResponse<ContentPermissionDto>.Ok(result));
    }

    [HttpPut("content/{id:long}")]
    public async Task<ActionResult<ApiResponse<ContentPermissionDto>>> UpdateContentPermission(long id, [FromBody] ContentPermissionDto dto, CancellationToken ct)
    {
        var result = await service.UpdateContentPermissionAsync(id, dto, ct);
        return Ok(ApiResponse<ContentPermissionDto>.Ok(result, "Content permission updated successfully"));
    }

    [HttpDelete("content/{id:long}")]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteContentPermission(long id, CancellationToken ct)
    {
        await service.DeleteContentPermissionAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "Content permission deleted successfully"));
    }

    [HttpPost("content/check")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckContentPermission([FromBody] PermissionCheck check, CancellationToken ct)
    {
        var result = await service.CheckContentPermissionAsync(check, ct);
        return Ok(ApiResponse<bool>.Ok(result));
    }
}
