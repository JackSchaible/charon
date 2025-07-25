using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace API.Services;

public class JwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _tokenLifetime;

    public JwtService(string secretKey, string issuer, string audience, TimeSpan? tokenLifetime = null)
    {
        _secretKey = secretKey ?? throw new ArgumentNullException(nameof(secretKey));
        _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        _audience = audience ?? throw new ArgumentNullException(nameof(audience));
        _tokenLifetime = tokenLifetime ?? TimeSpan.FromHours(24);
    }

    public JwtService()
    {
        _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET") ??
            throw new InvalidOperationException("JWT_SECRET environment variable is not set.");
        _issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "charon-api";
        _audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "charon-client";
        _tokenLifetime = TimeSpan.FromHours(24);
    }

    public string GenerateToken(string userId, string steamId, string username)
    {
        SymmetricSecurityKey? key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        SigningCredentials? credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        Claim[]? claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("steam_id", steamId),
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        JwtSecurityToken? token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_tokenLifetime),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            SymmetricSecurityKey? key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
            TokenValidationParameters? parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            };

            JwtSecurityTokenHandler? handler = new JwtSecurityTokenHandler();
            ClaimsPrincipal? principal = handler.ValidateToken(token, parameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    public int? GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        if (principal == null)
            return null;

        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            return null;

        return userId;
    }
}
