using Tyresoles.Sql.Abstractions;

namespace Dataverse.NavLive;

/// <summary>
/// Sales-scoped extensions for <see cref="Customer"/>.
/// </summary>
public partial class Customer
{
    /// <summary>
    /// Ledger balance from <see cref="Tyresoles.Data.Features.Sales.SalesService.GetMyCustomersQuery"/> via
    /// <c>SelectRaw</c> (correlated subquery on Detailed Cust. Ledg. Entry), not a duplicate NAV column projection.
    /// Without <see cref="SqlNotMappedAttribute"/>, the SQL layer maps this property to <c>t0.[Balance]</c> while
    /// <c>SelectRaw</c> also emits <c>AS [Balance]</c>, producing two Balance columns in the SELECT.
    /// </summary>
    [SqlNotMapped]
    public decimal Balance { get; set; }
}