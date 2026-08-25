using ApiForge.Core;
using ApiForge.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
[Route("api/upload")]
public class MediaController(IMediaService service) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MediaRecord>>> Upload([FromForm(Name = "files")] IFormFile? file, CancellationToken ct)
    {
        file ??= Request.Form.Files.GetFile("files");
        var result = await service.UploadAsync(file!, ct);
        return Ok(ApiResponse<MediaRecord>.Ok(result, "File uploaded successfully"));
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MediaRecord>>>> GetAll(CancellationToken ct)
    {
        var result = await service.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<MediaRecord>>.Ok(result));
    }

    [HttpGet("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<MediaRecord>>> GetById(long id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, ct);
        return Ok(ApiResponse<MediaRecord>.Ok(result));
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object?>>> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "File deleted successfully"));
    }

    [HttpGet("files/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> ServeFile(string fileName, CancellationToken ct)
    {
        var (path, mime, name) = await service.GetFileByNameAsync(fileName, ct);
        return PhysicalFile(path, mime, name);
    }
}
