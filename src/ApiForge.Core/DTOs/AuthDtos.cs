namespace ApiForge.Core;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? Firstname,
    string? Lastname
);

public sealed record LoginRequest(
    string? Username,
    string? Email,
    string Password
);

public sealed record RefreshRequest(
    string RefreshToken
);

public sealed record AuthResponse(
    string Token,
    string RefreshToken,
    string Type,
    long UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles
);

public sealed record UserDto(
    long Id,
    string Username,
    string Email,
    string? Firstname,
    string? Lastname,
    IReadOnlyList<string> Roles,
    bool Enabled
);
