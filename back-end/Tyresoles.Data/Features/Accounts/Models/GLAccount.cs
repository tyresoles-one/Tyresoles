using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Accounts.Models
{
    /// <summary>Physical SQL table name uses underscores; NAV UI shows "G/L Account".</summary>
    [NavTable("G_L Account", IsShared = false)]
    public class GLAccount
    {
        [NavColumn("No_")] public string No { get; set; } = string.Empty;
        [NavColumn("Name")] public string Name { get; set; } = string.Empty;
        [NavColumn("Income_Balance")] public int IncomeBalance { get; set; }
        [NavColumn("Account Category")] public int AccountCategory { get; set; }
        [NavColumn("Account Type")] public int AccountType { get; set; }
        [NavColumn("Gen_ Bus_ Posting Group")] public string GenBusPostingGroup { get; set; } = string.Empty;
        [NavColumn("Gen_ Prod_ Posting Group")] public string GenProdPostingGroup { get; set; } = string.Empty;
        [NavColumn("VAT Bus_ Posting Group")] public string VATBusPostingGroup { get; set; } = string.Empty;
        [NavColumn("VAT Prod_ Posting Group")] public string VATProdPostingGroup { get; set; } = string.Empty;
        [NavColumn("Blocked")] public byte Blocked { get; set; }
        [NavColumn("Direct Posting")] public byte DirectPosting { get; set; }
    }
}
