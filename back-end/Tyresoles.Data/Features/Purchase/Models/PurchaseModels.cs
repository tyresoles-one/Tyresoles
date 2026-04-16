using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Purchase.Models;

[NavTable("Unit of Measure", IsShared = false)]
public class UnitOfMeasure
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("Item Category", IsShared = false)]
public class ItemCategory
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("Product Group", IsShared = false)]
public class ProductGroup
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
    [NavColumn("Item Category Code")]
    public string ItemCategoryCode { get; set; } = string.Empty;
}

[NavTable("Gen_ Product Posting Group", IsShared = false)]
public class GenProductPostingGroup
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("GST Group", IsShared = false)]
public class GSTGroup
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("HSN_SAC", IsShared = false)]
public class HsnSac
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("Inventory Posting Group", IsShared = false)]
public class InventoryPostingGroup
{
    [NavColumn("Code")]
    public string Code { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
}

[NavTable("Item", IsShared = false)]
public class Item
{
    [NavColumn("No_")]
    public string No { get; set; } = string.Empty;
    [NavColumn("Description")]
    public string Description { get; set; } = string.Empty;
    [NavColumn("Base Unit of Measure")]
    public string BaseUnitOfMeasure { get; set; } = string.Empty;
    [NavColumn("Item Category Code")]
    public string ItemCategoryCode { get; set; } = string.Empty;
    [NavColumn("Product Group Code")]
    public string ProductGroupCode { get; set; } = string.Empty;
    [NavColumn("Inventory Posting Group")]
    public string InventoryPostingGroup { get; set; } = string.Empty;
    [NavColumn("Gen_ Prod_ Posting Group")]
    public string GenProdPostingGroup { get; set; } = string.Empty;
    [NavColumn("GST Group Code")]
    public string GstGroupCode { get; set; } = string.Empty;
    [NavColumn("HSN_SAC Code")]
    public string HsnSacCode { get; set; } = string.Empty;
}
