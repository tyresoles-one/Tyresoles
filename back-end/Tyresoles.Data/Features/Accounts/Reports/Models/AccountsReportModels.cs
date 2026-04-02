using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Tyresoles.Data.Features.Accounts.Reports.Models;

public class ReportGST01
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal TaxableAmt { get; set; }
    public decimal TaxPerCent { get; set; }
    public decimal IGST { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
    public decimal TaxAmt { get; set; }
}

public class ReportGST02
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal TaxableAmt { get; set; }
    public decimal TaxPerCent { get; set; }
    public decimal TaxAmt { get; set; }
    public bool Interstate { get; set; }
}

public class ReportGST03
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GSTNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal TaxableAmt { get; set; }
    public decimal TaxPerCent { get; set; }
    public decimal TaxAmt { get; set; }
    public decimal IGST { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
    public decimal TotalAmt { get; set; }
    public string SupplyType { get; set; } = string.Empty;
    public string ApplyDocNo { get; set; } = string.Empty;
    public string ApplyDate { get; set; } = string.Empty;
    public string GstType { get; set; } = string.Empty;
    public bool IsCreditMemo { get; set; }
}

public class ReportGST04
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string SupplyType { get; set; } = string.Empty;
    public decimal TaxableAmt { get; set; }
    public decimal TaxPerCent { get; set; }
    public decimal TaxAmt { get; set; }
    public decimal IGST { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
}

public class ReportGST05
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string HSN { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UQC { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal TotalTaxableValue { get; set; }
    public decimal TotalValue { get; set; }
    public decimal IntegratedTax { get; set; }
    public decimal CenteralTax { get; set; }
    public decimal StateUtTax { get; set; }
    public decimal TaxAmt { get; set; }
    public bool IsInterstate { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public class ReportGST06
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string GSTIN { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string InvDate { get; set; } = string.Empty;
    public string PlaceOfSupply { get; set; } = string.Empty;
    public decimal TotalInvoiceValue { get; set; }
    public string SupplyType { get; set; } = string.Empty;
    public string InvoiceType { get; set; } = string.Empty;
    public string RevCharge { get; set; } = string.Empty;
    public decimal IGST { get; set; }
    public decimal CGST { get; set; }
    public decimal SGST { get; set; }
    public decimal Taxable { get; set; }
}

public class EInvoiceErrors
{
    public string ReportName { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
