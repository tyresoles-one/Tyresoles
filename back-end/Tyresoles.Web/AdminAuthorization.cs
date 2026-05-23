using System;
using System.Security.Claims;

namespace Tyresoles.Web;

/// <summary>Lightweight checks aligned with JWT claims from <see cref="Auth.JwtTokenService"/>.</summary>
public static class AdminAuthorization
{
    public static bool IsAdministrator(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        var t = user.FindFirst("userType")?.Value ?? "";
        return t.Equals("Admin", StringComparison.OrdinalIgnoreCase)
               || t.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)
               || t.Equals("SUPER", StringComparison.OrdinalIgnoreCase);
    }
}
