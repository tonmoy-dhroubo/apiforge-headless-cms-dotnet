using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ApiForge.Infrastructure;

public sealed class JwtTokenService(IConfiguration configuration)
{
    private readonly string _secret = configuration["Jwt:Secret"] 
        ?? "apiforge-headless-cms-secret-key-minimum-256-bits-required-for-hs256";

    private readonly string _refreshSecret = configuration["Jwt:RefreshSecret"] 
        ?? "apiforge-headless-cms-refresh-secret-key-minimum-256-bits-required";

    private string CreateToken(long id, string username, IEnumerable<string> roles, bool isRefreshToken)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, username),
            new("userId", id.ToString()),
            new("username", username)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim("role", role));
        }

        var secretKey = isRefreshToken ? _refreshSecret : _secret;
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expires = isRefreshToken 
            ? DateTime.UtcNow.AddDays(7) 
            : DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
    }

    public string Access(long id, string username, IEnumerable<string> roles) =>
        CreateToken(id, username, roles, isRefreshToken: false);

    public string Refresh(long id, string username, IEnumerable<string> roles) =>
        CreateToken(id, username, roles, isRefreshToken: true);

    public bool Validate(string? token, bool refresh = false)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var secretKey = refresh ? _refreshSecret : _secret;
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public long? UserId(string token, bool refresh = false)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var secretKey = refresh ? _refreshSecret : _secret;
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false
        };

        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst("userId")?.Value;

            return long.TryParse(userIdClaim, out var parsedId) ? parsedId : null;
        }
        catch
        {
            return null;
        }
    }
}
