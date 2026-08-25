using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace ApiForge.Infrastructure;

public sealed class JwtTokenService(IConfiguration configuration)
{
    private readonly string _secret = configuration["Jwt:Secret"] ?? "apiforge-headless-cms-secret-key-minimum-256-bits-required-for-hs256";
    private readonly string _refreshSecret = configuration["Jwt:RefreshSecret"] ?? "apiforge-headless-cms-refresh-secret-key-minimum-256-bits-required";
    private string Create(long id, string username, IEnumerable<string> roles, bool refresh)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, username), new("userId", id.ToString()), new("username", username) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(refresh ? _refreshSecret : _secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.Add(refresh ? TimeSpan.FromDays(7) : TimeSpan.FromHours(1)), signingCredentials: credentials));
    }
    public string Access(long id, string username, IEnumerable<string> roles) => Create(id, username, roles, false);
    public string Refresh(long id, string username, IEnumerable<string> roles) => Create(id, username, roles, true);
    public bool Validate(string? token, bool refresh = false)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        try { new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(refresh ? _refreshSecret : _secret)), ValidateIssuer = false, ValidateAudience = false, ClockSkew = TimeSpan.Zero }, out _); return true; } catch { return false; }
    }
    public long? UserId(string token, bool refresh = false) { try { var p = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(refresh ? _refreshSecret : _secret)), ValidateIssuer = false, ValidateAudience = false }, out _); return long.TryParse(p.FindFirst("userId")?.Value, out var id) ? id : null; } catch { return null; } }
}
