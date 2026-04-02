namespace Tyresoles.Easebuzz;

/// <summary>
/// Configuration for Easebuzz payment gateway. Bind to "Easebuzz" in appsettings.json.
/// Key and Salt are obtained from the Easebuzz merchant dashboard (https://dashboard.easebuzz.in).
/// Do not commit Key/Salt; use User Secrets, environment variables, or Key Vault in production.
/// </summary>
public sealed class EasebuzzOptions
{
    /// <summary>Configuration section name for appsettings.json.</summary>
    public const string Section = "Easebuzz";

    /// <summary>Merchant key from Easebuzz dashboard.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Merchant salt from Easebuzz dashboard. Never send to client; used server-side only for hash generation.</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>Use sandbox/test environment when true; production when false.</summary>
    public bool Sandbox { get; set; } = true;

    /// <summary>Enable iframe payment flow when true.</summary>
    public bool EnableIframe { get; set; } = false;

    /// <summary>Optional override for API base URL. When null, derived from Sandbox (test vs production).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>HTTP timeout for Easebuzz API calls in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
