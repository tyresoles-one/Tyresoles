namespace Tyresoles.Data.Features.Admin.User;

public interface IUserService
{
    /// <param name="platform">Optional: "web" (filter by Web App), "win" (filter by ERP App). If null/empty, no platform filter is applied.</param>
    Task<LoginResult> LoginAsync(string username, string password, string? platform = null, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);

    /// <summary>Refreshes an access token using a refresh token if the session is still valid.</summary>
    Task<LoginResult> RefreshTokenAsync(string token, string refreshToken, string? ipAddress = null, string? userAgent = null, CancellationToken cancellationToken = default);

    /// <summary>Get profile for a user by userId (UserName or MobileNo). Returns null if not found.</summary>
    Task<ProfileResult?> GetProfileAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Update profile fields for a user. Only provided fields are updated. Returns false if user not found.</summary>
    Task<bool> SetProfileAsync(string userId, ProfileUpdateInput input, CancellationToken cancellationToken = default);

    Task<string?> ResetPasswordAsync(string userId, CancellationToken cancellationToken = default);
    /// <summary>Change password. Provide either oldPassword (when user knows it) or securityPin (first login / forgot password). At least one must be valid.</summary>
    Task<bool> ChangePasswordAsync(string userId, string newPassword, string? oldPassword = null, int? securityPin = null, CancellationToken cancellationToken = default);
    /// <summary>Forgot password: reset by username + Security PIN (no auth required).</summary>
    Task<bool> ForgotPasswordAsync(string username, int securityPin, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Search users by name or username. Returns up to <paramref name="take"/> results. Used for attendee selection.</summary>
    Task<IReadOnlyList<UserSearchResult>> SearchUsersAsync(string? search, int take = 20, CancellationToken cancellationToken = default);

    /// <summary>Get user details including RDP password and NAV config name by username.</summary>
    Task<UserDetail?> GetUserAsync(string username, CancellationToken cancellationToken = default);
}
