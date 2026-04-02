namespace Tyresoles.Data.Features.Admin.Auth;

/// <summary>Token lifetime in hours by user type. Populated from app configuration (e.g. JwtOptions in Web).</summary>
public sealed class JwtExpiryOptions
{
    public int DealerSalesExpiryHours { get; set; } = 6;
    public int DefaultExpiryHours { get; set; } = 168;
}
