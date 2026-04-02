namespace Tyresoles.Data.Features.Admin.Auth;

/// <summary>Password policy: expiry and optional strength. Bound from configuration (e.g. PasswordPolicy section).</summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";

    /// <summary>Password must be changed after this many days. 0 = disabled.</summary>
    public int ExpiryDays { get; set; }
}
