using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MINMs.Server.Options;

namespace MINMs.Server.Services;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public AuthTokenResult CreateAccessToken(int userId, string login)
    {
        var expires = DateTime.UtcNow.AddMinutes(Math.Clamp(_options.ExpiresMinutes, 1, 10080));
        var keyBytes = Encoding.UTF8.GetBytes(_options.Key);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes (UTF-8) for HS256.");

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Name, login),
            new(ClaimTypes.NameIdentifier, userId.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, login),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return new AuthTokenResult(encoded, expires);
    }
}

public sealed record AuthTokenResult(string Token, DateTime ExpiresAtUtc);
