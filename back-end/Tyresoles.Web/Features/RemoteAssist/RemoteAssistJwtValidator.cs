using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Web.Auth;

namespace Tyresoles.Web.Features.RemoteAssist;

/// <summary>Validates the same JWT as API auth (session + Redis) for WebSocket query token.</summary>
public sealed class RemoteAssistJwtValidator
{
    private readonly JwtOptions _options;
    private readonly byte[] _keyBytes;

    public RemoteAssistJwtValidator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var secret = _options.Secret ?? "";
        _keyBytes = TryDecodeBase64(secret, out var decoded) ? decoded : Encoding.UTF8.GetBytes(secret);
    }

    public async Task<ClaimsPrincipal?> ValidateAsync(string accessToken, ISessionStore sessionStore, CancellationToken cancellationToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_keyBytes),
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(accessToken, parameters, out _);
        }
        catch
        {
            return null;
        }

        var sessionId = principal.FindFirst("sessionId")?.Value;
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        var session = await sessionStore.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
            return null;

        return principal;
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
