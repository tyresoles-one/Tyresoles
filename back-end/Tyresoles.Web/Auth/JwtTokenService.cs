using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tyresoles.Data.Features.Admin.Auth;

namespace Tyresoles.Web.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _keyBytes;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var secret = _options.Secret ?? "";
        _keyBytes = TryDecodeBase64(secret, out var decoded) ? decoded : Encoding.UTF8.GetBytes(secret);
        if (_keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:Secret must be at least 32 bytes (or a base64 string that decodes to at least 32 bytes).");
    }

    public string GenerateToken(JwtTokenRequest request)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(request.ExpiresIn);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("userSecurityId", request.UserSecurityId.ToString()),
            new("userType", request.UserType ?? ""),
            new("entityType", request.EntityType ?? ""),
            new("entityCode", request.EntityCode ?? ""),
            new("department", request.Department ?? ""),
            new("sessionId", request.SessionId ?? "")
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(_keyBytes),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool TryDecodeBase64(string value, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value) || value.Length % 4 != 0)
            return false;
        try
        {
            decoded = Convert.FromBase64String(value.Trim());
            return true;
        }
        catch
        {
            return false;
        }
    }
}
