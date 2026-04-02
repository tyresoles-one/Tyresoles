namespace Tyresoles.Web.Auth;

/// <summary>JWT signing and validation options. Bind from configuration (e.g. "Jwt").</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Signing key (base64-encoded or raw string). Must be at least 32 bytes for HMAC-SHA256.</summary>
    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "Tyresoles";
    public string Audience { get; set; } = "Tyresoles";

    /// <summary>Token lifetime in hours for DEALER and SALES user types.</summary>
    public int DealerSalesExpiryHours { get; set; } = 6;

    /// <summary>Token lifetime in hours for all other user types.</summary>
    public int DefaultExpiryHours { get; set; } = 168; // 7 days
}
