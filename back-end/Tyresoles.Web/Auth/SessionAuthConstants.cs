namespace Tyresoles.Web.Auth;

/// <summary>
/// Marker included in JWT bearer failure messages when the token is valid but the server-side session is missing (killed or expired).
/// The SPA matches this substring to show an appropriate sign-out message.
/// </summary>
public static class SessionAuthConstants
{
    public const string RevokedMarker = "TY_SESSION_REVOKED";
}
