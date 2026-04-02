namespace Tyresoles.Data.Features.Sales;

/// <summary>
/// Entity code and its balance from Detailed Cust. Ledger Entry (grouped by entity as appropriate).
/// </summary>
public sealed class EntityBalance
{
    public string Code { get; set; } = "";
    public decimal Balance { get; set; }
    public string Product { get; set; } = "";
}
