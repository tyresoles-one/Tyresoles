using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Sales;

/// <summary>
/// A single account transaction projected from Detailed Cust. Ledger Entry.
/// NavColumn attributes mirror the source entity columns so that SqlBuilder
/// resolves the correct SQL column names even when HotChocolate's [UseProjection]
/// replaces the inner IQuery selector with an AccountTransaction → AccountTransaction
/// MemberInitExpression (losing the original DetailedCustLedgEntry member references).
/// </summary>
[NavTable("Detailed Cust_ Ledg_ Entry")]
public sealed class AccountTransaction
{
    [NavColumn("Posting Date")]
    public DateTime? Date { get; set; }

    [NavColumn("Document Type")]
    public int Type { get; set; }

    [NavColumn("Document No_")]
    public string DocumentNo { get; set; } = "";

    [NavColumn("Amount")]
    public decimal Amount { get; set; }

    [NavColumn("Customer No_")]
    public string CustomerNo { get; set; } = "";

    /// <summary>Customer name from joined Customer table. NavColumn maps to Customer.[Name] in join result.</summary>
    [NavColumn("Name")]
    public string CustomerName { get; set; } = "";

    /// <summary>Running total of Amount (cumulative balance). Filled by SQL window function or post-query.</summary>
    public decimal Balance { get; set; }
}
