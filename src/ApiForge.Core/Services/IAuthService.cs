using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    bool ValidateToken(string? token);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);
}

public interface IUserService
{
    Task<IReadOnlyList<UserDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserDto> AssignRolesAsync(long id, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
