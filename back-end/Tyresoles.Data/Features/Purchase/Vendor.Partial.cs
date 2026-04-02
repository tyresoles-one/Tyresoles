using Tyresoles.Sql.Abstractions;

namespace Dataverse.NavLive;

/// <summary>
/// Purchase-scoped extensions for <see cref="Vendor"/> (not part of NAV table mapping).
/// </summary>
public partial class Vendor
{
    /// <summary>
    /// When set by <see cref="Tyresoles.Data.Features.Purchase.PurchaseService.MyVendors"/>,
    /// sum of <see cref="DetailedVendorLedgEntry.Amount"/> for this vendor (same definition as
    /// <see cref="Tyresoles.Data.Features.Purchase.IPurchaseService.GetVendorBalanceAsync"/>).
    /// Supplied via <c>SelectRaw</c> as <c>[DetailVendorBalance]</c>, not a NAV column — must not be projected as <c>t0.[DetailVendorBalance]</c>.
    /// </summary>
    [SqlNotMapped]
    public decimal? Balance { get; set; }
}
