using System.ComponentModel.DataAnnotations;

namespace ApiForge.Core;

public sealed record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 3)] string Username,
    [Required, EmailAddress, StringLength(255)] string Email,
    [Required, StringLength(100, MinimumLength = 6)] string Password,
    [StringLength(100)] string? Firstname,
    [StringLength(100)] string? Lastname
);

public sealed record LoginRequest(
    [StringLength(100)] string? Username,
    [StringLength(255)] string? Email,
    [Required] string Password
);

public sealed record RefreshRequest(
    [Required] string RefreshToken
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
