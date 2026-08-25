using System.Text.Json;
using ApiForge.Core;
using ApiForge.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiForge.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, IUserService userService) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var response = await authService.RegisterAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(response, "User registered successfully"));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var response = await authService.LoginAsync(request, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(response, "Login successful"));
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<bool>> ValidateToken([FromBody] JsonElement body)
    {
        string? token = body.TryGetProperty("token", out var t) ? t.GetString() : null;
        var isValid = authService.ValidateToken(token);
        return Ok(ApiResponse<bool>.Ok(isValid));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var response = await authService.RefreshAsync(request.RefreshToken, ct);
        return Ok(ApiResponse<AuthResponse>.Ok(response, "Token refreshed"));
    }

    [HttpGet("oauth2/google")]
    [AllowAnonymous]
    public IActionResult OAuth2Google()
    {
        return Redirect("/api/auth/oauth2/authorize/google");
    }

    [HttpGet("users")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UserDto>>>> GetAllUsers(CancellationToken ct)
    {
        var users = await userService.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<UserDto>>.Ok(users));
    }

    [HttpGet("users/{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(long id, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct);
        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    [HttpPut("users/{id:long}/roles")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> AssignRoles(long id, [FromBody] JsonElement body, CancellationToken ct)
    {
        if (!body.TryGetProperty("roles", out var rolesElement))
        {
            return BadRequest(ApiResponse<object>.Fail("roles is required"));
        }

        var roles = rolesElement.EnumerateArray()
            .Select(x => x.GetString()!)
            .Where(x => x is not null)
            .ToList();

        var updatedUser = await userService.AssignRolesAsync(id, roles, ct);
        return Ok(ApiResponse<UserDto>.Ok(updatedUser, "Roles assigned successfully"));
    }

    [HttpDelete("users/{id:long}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object?>>> DeleteUser(long id, CancellationToken ct)
    {
        await userService.DeleteAsync(id, ct);
        return Ok(ApiResponse<object?>.Ok(null, "User deleted successfully"));
    }
}
