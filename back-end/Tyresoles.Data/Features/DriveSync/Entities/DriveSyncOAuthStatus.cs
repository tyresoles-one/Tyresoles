namespace Tyresoles.Data.Features.DriveSync.Entities;

public sealed class DriveSyncOAuthStatus
{
    public bool IsConfigured { get; init; }
    public bool HasRefreshToken { get; init; }
    public bool HasAccessToken { get; init; }
    public DateTime? AccessTokenExpiryUtc { get; init; }
    public bool IsAccessTokenExpired { get; init; }
    public string? GoogleAccountEmail { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string? UpdatedByUserId { get; init; }
}
