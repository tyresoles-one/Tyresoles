namespace Tyresoles.Data.Features.Admin.Auth;

/// <summary>Generates JWT access tokens at login. Implementation lives in the Web layer (config + signing).</summary>
public interface IJwtTokenService
{
    string GenerateToken(JwtTokenRequest request);
}
