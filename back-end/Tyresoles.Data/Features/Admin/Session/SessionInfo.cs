namespace Tyresoles.Data.Features.Admin.Session;

/// <summary>Represents an active app session created at login.</summary>
public sealed class SessionInfo
{
    public string SessionId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public Guid UserSecurityId { get; init; }
    public string UserType { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public string EntityCode { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? RefreshTokenExpiresAtUtc { get; init; }
}
