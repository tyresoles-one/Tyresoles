// DTOs for production reports. Property names align with RDLC DataSet_Result and legacy models.

namespace Tyresoles.Data.Features.Production.Models;

/// <summary>Ecomile Aging report row.</summary>
public sealed class EcomileAgingRow
{
    public string ReportName { get; set; } = "";
    public string Period { get; set; } = "";
    public string Tyre { get; set; } = "";
    public int Age30 { get; set; }
    public int Age60 { get; set; }
    public int Age90 { get; set; }
    public int Age180 { get; set; }
    public int AgeAbove { get; set; }
    public int Total { get; set; }
}

/// <summary>Tyre Age helper class for Ecomile Aging.</summary>
public sealed class TyreAgeRow
{
    public string No { get; set; } = "";
    public int Age { get; set; }
}

/// <summary>Exchange Tyres report row (single summary row).</summary>
public sealed class ExchangeTyresRow
{
    public string ReportName { get; set; } = "";
    public string Period { get; set; } = "";
    public int TotG { get; set; }
    public int TotR { get; set; }
    public int TotL { get; set; }
    public int TotP { get; set; }
    public int TotT { get; set; }
    public int TotO { get; set; }
    public int TotO1 { get; set; }
    public int TotTotal => TotG + TotR + TotL + TotP + TotT + TotO + TotO1;

    public int SoldG { get; set; }
    public int SoldR { get; set; }
    public int SoldL { get; set; }
    public int SoldP { get; set; }
    public int SoldT { get; set; }
    public int SoldO { get; set; }
    public int SoldO1 { get; set; }
    public int SoldTotal => SoldG + SoldR + SoldL + SoldP + SoldT + SoldO + SoldO1;

    public int G { get; set; }
    public int R { get; set; }
    public int L { get; set; }
    public int P { get; set; }
    public int T { get; set; }
    public int O { get; set; }
    public int O1 { get; set; }
    public int Total => G + R + L + P + T + O + O1;
}

public enum EcomileStatusRow
{
    None,
    Reject,
    Produce,
    Invoice,
    CreditMemo,
    Replace
}

public sealed class EcomileItemStatusRow
{
    public string No { get; set; } = "";
    public string Group { get; set; } = "";
    public EcomileStatusRow Status { get; set; }
}

/// <summary>Claim & Failure report row.</summary>
public sealed class ClaimFailureRow
{
    public string Location { get; set; } = "";
    public string CustomerNo { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string DealerCode { get; set; } = "";
    public string DealerName { get; set; } = "";
    public string AreaCode { get; set; } = "";
    public string AreaName { get; set; } = "";
    public string RegionCode { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string TyreSize { get; set; } = "";
    public string ProductGroup { get; set; } = "";
    public string Decision { get; set; } = "";
    public string ReportTitle { get; set; } = "";
    public string FilterText { get; set; } = "";
    public string FaultText { get; set; } = "";
    public bool TyreDecision { get; set; }
    public bool FaultDecision { get; set; }
    public bool CustDecision { get; set; }
    public bool DealerDecision { get; set; }
    public bool AreaDecision { get; set; }
    public bool RegionDecision { get; set; }
}

/// <summary>Posted Proc Order / Dispatch Order / Dispatch Details / Casing Inspection / New Numbering report row.</summary>
public sealed class PostedProcOrderRow
{
    public string CompanyName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string SortNo { get; set; } = "";
    public string OrderNo { get; set; } = "";
    public string VendorName { get; set; } = "";
    public string VendorNo { get; set; } = "";
    public string Date { get; set; } = "";
    public string TyreSize { get; set; } = "";
    public string Make { get; set; } = "";
    public string SerialNo { get; set; } = "";
    public string Inspection { get; set; } = "";
    public string Inspector { get; set; } = "";
    public decimal Amount { get; set; }
    public string Transporter { get; set; } = "";
    public string Manager { get; set; } = "";
    public string VehicleNo { get; set; } = "";
    public string MobileNo { get; set; } = "";
    public string DispatchDate { get; set; } = "";
    public string DispatchOrderNo { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Location { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccNo { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public string BankIFSC { get; set; } = "";
}

/// <summary>Casing Average Cost report row.</summary>
public sealed class PostedShipmentAverageCostRow
{
    public string CompanyName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string DocumentNo { get; set; } = "";
    public string Date { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string TyreSize { get; set; } = "";
    public decimal AvgCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Qty { get; set; }
}

/// <summary>Vendor Bill report row.</summary>
public sealed class VendorBillRow
{
    public string No { get; set; } = "";
    public string Name { get; set; } = "";
    public string Date { get; set; } = "";
    public string BillNo { get; set; } = "";
    public string Address { get; set; } = "";
    public string MobileNo { get; set; } = "";
    public string BillToName { get; set; } = "";
    public string BillToAddress { get; set; } = "";
    public string BillToAddress2 { get; set; } = "";
    public string BillToAddress3 { get; set; } = "";
    public string BillToGSTNo { get; set; } = "";
    public string BillToState { get; set; } = "";
    public string Product { get; set; } = "";
    public string HSN { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal Rate { get; set; }
    public decimal Amount { get; set; }
    public string AmountInWords { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAccNo { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public string BankIFSC { get; set; } = "";
}

/// <summary>Casing Purchase Details and Analysis rows.</summary>
public sealed class CasingPurchaseDetailsRow
{
    public string Location { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string DateRange { get; set; } = "";
    public string Date { get; set; } = "";
    public string Market { get; set; } = "";
    public string Manager { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Segment { get; set; } = "";
    public decimal Quantity { get; set; }
}

public sealed class CasingPurchaseAnalysisRow
{
    public string Location { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string DateRange { get; set; } = "";
    public string Tyre { get; set; } = "";
    public string Segment { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Market { get; set; } = "";
    public string Manager { get; set; } = "";
    public string City { get; set; } = "";
    public string SupplierCode { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Ecomile Inv. Sales Statistics row.</summary>
public sealed class EcomileInvSalesStatisticsRow
{
    public string Location { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string DateRange { get; set; } = "";
    public string Tyre { get; set; } = "";
    public string Variant { get; set; } = "";
    public decimal Quantity { get; set; }
}

/// <summary>Claim Analysis report row.</summary>
public sealed class ClaimAnalysisRow
{
    public string ReportName { get; set; } = "";
    public string Period { get; set; } = "";
    public string Location { get; set; } = "";
    public string View { get; set; } = "";
    public string Segment { get; set; } = "";
    public string DateRange { get; set; } = "";
    public string Size { get; set; } = "";
    public string Type { get; set; } = "";
    public string Particular { get; set; } = "";
    public string ParticularLabel { get; set; } = "";
    public string Month1 { get; set; } = "";
    public string Month2 { get; set; } = "";
    public string Month3 { get; set; } = "";
    public string Month4 { get; set; } = "";
    public string Month5 { get; set; } = "";
    public string Month6 { get; set; } = "";
    public string Month7 { get; set; } = "";
    public string Month8 { get; set; } = "";
    public string Month9 { get; set; } = "";
    public string Month10 { get; set; } = "";
    public string Month11 { get; set; } = "";
    public string Month12 { get; set; } = "";
    public decimal TY1 { get; set; }
    public decimal TM1 { get; set; }
    public decimal A1 { get; set; }
    public decimal PY1 { get; set; }
    public decimal PM1 { get; set; }
    public decimal TY2 { get; set; }
    public decimal TM2 { get; set; }
    public decimal A2 { get; set; }
    public decimal PY2 { get; set; }
    public decimal PM2 { get; set; }
    public decimal TY3 { get; set; }
    public decimal TM3 { get; set; }
    public decimal A3 { get; set; }
    public decimal PY3 { get; set; }
    public decimal PM3 { get; set; }
    public decimal TY4 { get; set; }
    public decimal TM4 { get; set; }
    public decimal A4 { get; set; }
    public decimal PY4 { get; set; }
    public decimal PM4 { get; set; }
    public decimal TY5 { get; set; }
    public decimal TM5 { get; set; }
    public decimal A5 { get; set; }
    public decimal PY5 { get; set; }
    public decimal PM5 { get; set; }
    public decimal TY6 { get; set; }
    public decimal TM6 { get; set; }
    public decimal A6 { get; set; }
    public decimal PY6 { get; set; }
    public decimal PM6 { get; set; }
    public decimal TY7 { get; set; }
    public decimal TM7 { get; set; }
    public decimal A7 { get; set; }
    public decimal PY7 { get; set; }
    public decimal PM7 { get; set; }
    public decimal TY8 { get; set; }
    public decimal TM8 { get; set; }
    public decimal A8 { get; set; }
    public decimal PY8 { get; set; }
    public decimal PM8 { get; set; }
    public decimal TY9 { get; set; }
    public decimal TM9 { get; set; }
    public decimal A9 { get; set; }
    public decimal PY9 { get; set; }
    public decimal PM9 { get; set; }
    public decimal TY10 { get; set; }
    public decimal TM10 { get; set; }
    public decimal A10 { get; set; }
    public decimal PY10 { get; set; }
    public decimal PM10 { get; set; }
    public decimal TY11 { get; set; }
    public decimal TM11 { get; set; }
    public decimal A11 { get; set; }
    public decimal PY11 { get; set; }
    public decimal PM11 { get; set; }
    public decimal TY12 { get; set; }
    public decimal TM12 { get; set; }
    public decimal A12 { get; set; }
    public decimal PY12 { get; set; }
    public decimal PM12 { get; set; }
    public decimal TTY { get; set; }
    public decimal TTM { get; set; }
    public decimal TTA { get; set; }
    public decimal TTPY { get; set; }
    public decimal TTPM { get; set; }
    public decimal TTAVG { get; set; }
}

public sealed class ClaimAnalysisRecordRow
{
    public string Topic { get; set; } = "";
    public string Period { get; set; } = "";
    public string Type { get; set; } = "";
    public int Figure { get; set; }
}
