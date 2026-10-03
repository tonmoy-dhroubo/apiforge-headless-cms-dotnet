using ApiForge.Core;

namespace ApiForge.Core.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    bool ValidateToken(string? token);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default);
}
