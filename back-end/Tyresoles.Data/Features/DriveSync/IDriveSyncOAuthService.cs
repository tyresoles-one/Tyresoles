using Tyresoles.Data.Features.DriveSync.Entities;

namespace Tyresoles.Data.Features.DriveSync;

public interface IDriveSyncOAuthService
{
    Task<string> GetAuthorizationUrlAsync(string adminUserId, CancellationToken ct = default);
    Task HandleOAuthCallbackAsync(string code, string state, CancellationToken ct = default);
    Task<DriveSyncOAuthStatus> GetStatusAsync(CancellationToken ct = default);
    Task<string> GetValidAccessTokenAsync(CancellationToken ct = default);
}
