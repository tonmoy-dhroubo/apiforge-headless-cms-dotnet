using ApiForge.Core;
using ApiForge.Core.Services;
using ApiForge.Infrastructure;

namespace ApiForge.Api.Services;

public class AuthService(IUserStore users, JwtTokenService jwt) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (await users.Find(request.Username, ct) is not null || await users.Find(request.Email, ct) is not null)
        {
            throw new ApiForgeException("Username or email already exists", 409);
        }

        var user = await users.Add(request.Username, request.Email, request.Password, request.Firstname, request.Lastname, ["REGISTERED"], ct);
        return BuildAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var identifier = string.IsNullOrWhiteSpace(request.Username) ? request.Email : request.Username;
        var user = identifier is null ? null : await users.Find(identifier, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new ApiForgeException("Invalid credentials", 401);
        }

        if (!user.Enabled)
        {
            throw new ApiForgeException("Account is disabled", 403);
        }

        return BuildAuthResponse(user);
    }

    public bool ValidateToken(string? token)
    {
        return !string.IsNullOrWhiteSpace(token) && jwt.Validate(token);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var id = jwt.UserId(refreshToken, true);
        var user = id is null ? null : await users.ById(id.Value, ct);

        if (user is null || !jwt.Validate(refreshToken, true))
        {
            throw new ApiForgeException("Invalid refresh token", 401);
        }

        return BuildAuthResponse(user);
    }

    private AuthResponse BuildAuthResponse(UserRecord u) =>
        new(jwt.Access(u.Id, u.Username, u.Roles), jwt.Refresh(u.Id, u.Username, u.Roles), "Bearer", u.Id, u.Username, u.Email, u.Roles);
}
