namespace Tyresoles.Data.Features.Admin.Auth;

/// <summary>Input for JWT generation at login.</summary>
public sealed class JwtTokenRequest
{
    public string UserId { get; init; } = string.Empty;
    public Guid UserSecurityId { get; init; }
    public string UserType { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityCode { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string UserSpecialToken { get; init;  } = string.Empty;
    public string SessionId { get; init; } = string.Empty;
    /// <summary>Token lifetime. DEALER/SALES typically 6h; others longer.</summary>
    public TimeSpan ExpiresIn { get; init; }
}
