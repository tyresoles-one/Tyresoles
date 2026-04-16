using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Purchase.Models;

[NavTable("FA Class", IsShared = false)]
public class FAClass
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Name")]
    public string Name { get; set; } = string.Empty;
}

[NavTable("FA Subclass", IsShared = false)]
public class FASubclass
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Name")]
    public string Name { get; set; } = string.Empty;
    [NavColumn("FA Class Code")]
    public string FAClassCode { get; set; } = string.Empty;
}

[NavTable("Fixed Asset", IsShared = false)]
public class FixedAsset
{
    [NavColumn("No_")]
    public string No { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
    [NavColumn("Description 2")]
    public string Description2 { get; set; } = string.Empty;
    [NavColumn("Responsible Employee")]
    public string ResponsibleEmployee { get; set; } = string.Empty;
    [NavColumn("Responsibility Center")]
    public string ResponsibilityCenter { get; set; } = string.Empty;
    [NavColumn("FA Class Code")]
    public string FAClassCode { get; set; } = string.Empty;
    [NavColumn("FA Subclass Code")]
    public string FASubclassCode { get; set; } = string.Empty;
    [NavColumn("Serial No_")]
    public string SerialNo { get; set; } = string.Empty;
    [NavColumn("Vendor No_")]
    public string VendorNo { get; set; } = string.Empty;
    [NavColumn("Blocked")]
    public byte Blocked { get; set; }
}

[NavTable("G_L Entry", IsShared = false)]
public class GLEntry
{
    [NavColumn("Posting Date")]
    public DateTime PostingDate { get; set; }
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
    [NavColumn("Amount")]
    public decimal Amount { get; set; }
    [NavColumn("G_L Account No_")]
    public string GLAccountNo { get; set; } = string.Empty;
    [NavColumn("Source No_")]
    public string SourceNo { get; set; } = string.Empty;
    [NavColumn("FA No_")]
    public string FANo { get; set; } = string.Empty;
}

public class FixedAssetServiceLog
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Employee { get; set; } = string.Empty;
    public string SubClass { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string VendorNo { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
}
