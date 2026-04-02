using System;

namespace Tyresoles.Data.Features.Sales.Reports.Models;

public enum SaleType
{
    Retread,
    Ecomile,
    ExchangeTyre,
    IcTyre,
    Ecoflex,
    IcEcoflex,
    Scrap,
    FlapTube,
    TreadRubber
}

public class ItemCategoryProductGroup
{
    public int Id { get; set; }
    public SaleType SaleType { get; set; }
    public string? Product { get; set; }
    public System.Collections.Generic.List<string>? ItemCategories { get; set; }
    public string? ProductGroup { get; set; }
    public System.Collections.Generic.List<string>? Items { get; set; }
    public string? Name { get; set; }
    public double Unit { get; set; }
}

public class DocumentDto
{
    public string? No { get; set; }
    public DateTime? Date { get; set; }
    public string? CustomerNo { get; set; }
    public string? Name { get; set; }
    public decimal Amount { get; set; }
}

public class PostedSalesCreditMemoRow
{
    public string? CreditMemoNo { get; set; }
    public string? CreditMemoDate { get; set; }
    public string? CustomerNo { get; set; }
    public string? SellToCustomerName { get; set; }
    public int LineNo { get; set; }
    public string? Description { get; set; }
    public string? No { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public string? ExternalDocumentNo { get; set; }
    public string? ResponsibilityCenter { get; set; }
    public string? UserID { get; set; }
    public string? Make { get; set; }
    public string? SerialNo { get; set; }
    public string? Pattern { get; set; }
    public decimal TotalGSTAmount { get; set; }
    public DateTime? PostingDate { get; set; }

    // Legacy fields for cloning
    public byte[]? Logo { get; set; }
    public string? COMPANYNAME { get; set; }
    public string? CompAddress { get; set; }
    public string? CompAddress2 { get; set; }
    public string? RegOffAddress { get; set; }
    public string? CINText { get; set; }
    public string? GSTText { get; set; }
    public string? BillToAddress1 { get; set; }
    public string? BillToAddress2 { get; set; }
    public string? BillToAddress3 { get; set; }
    public string? BillToAddress4 { get; set; }
    public string? BillToAddress5 { get; set; }
    public string? BillToAddress6 { get; set; }
    public string? SellToAddress1 { get; set; }
    public string? SellToAddress2 { get; set; }
    public string? SellToAddress3 { get; set; }
    public string? SellToAddress4 { get; set; }
    public string? SellToAddress5 { get; set; }
    public string? SellToAddress6 { get; set; }
    public byte[] QRCode { get; set; }
    public string SelltoCustomerNo_SalesCrMemoHeader { get; set; }
    public string No_SalesCrMemoHeader { get; set; }
    public string PostingDate_SalesCrMemoHeader { get; set; }
    public decimal GrTotal { get; set; }
    public decimal discAmt { get; set; }
    public string? HSNText { get; set; }
    public string? PANText { get; set; }
    public string? JuriText { get; set; }
    public string? UserName { get; set; }
    public decimal LineAmount { get; set; }
    public decimal gstBaseAmount { get; set; }
    public decimal iGstAmt { get; set; }
    public decimal cGstAmt { get; set; }
    public decimal sGstAmt { get; set; }
    public decimal iGstPer { get; set; }
    public decimal cGstPer { get; set; }
    public decimal sGstPer { get; set; }
    public bool showIGST { get; set; }
    public bool showCGST { get; set; }
    public bool showSGST { get; set; }
    public decimal tcsAmt { get; set; }
    public decimal roundUp { get; set; }
    public decimal Line_TotalGstAmount { get; set; }
    public int Line_GstJurisdictionType { get; set; }
    public decimal Line_GstPercent { get; set; }
    public string? InvoiceText { get; set; }
    public string? DocumentCaption { get; set; }
    public string? IGSTTxt { get; set; }
    public string? CGSTTxt { get; set; }
    public string? SGSTTxt { get; set; }
    public string? GrandTotalText { get; set; }
    public decimal FinalAmount { get; set; }
    public string? CustomerGenBusPostingGroup { get; set; }
    public decimal Line_Frieght { get; set; }
    public decimal Line_TDS { get; set; }
}

public class PostedClaimFormRow
{
    public string ReportName { set; get; }
    public string CompanyName { set; get; }
    public string RespName { set; get; }
    public byte[] Logo { set; get; }
    public string CompAddress { set; get; }
    public string CompAddress2 { set; get; }
    public string Type_ClaimFailurePosted { set; get; }
    public string No_ClaimFailurePosted { set; get; }
    public string CustomerNo_ClaimFailurePosted { set; get; }
    public string PostingDate_ClaimFailurePosted { set; get; }
    public string ItemNo_ClaimFailurePosted { set; get; }
    public string SerialNo_ClaimFailurePosted { set; get; }
    public string Make_ClaimFailurePosted { set; get; }
    public string InvoiceNo_ClaimFailurePosted { set; get; }
    public string InvoiceDate_ClaimFailurePosted { set; get; }
    public string Variant_ClaimFailurePosted { set; get; }
    public string Name_ClaimFailurePosted { set; get; }
    public string InspectionReport_ClaimFailurePosted { set; get; }
    public bool OwnerRisk_ClaimFailurePosted { set; get; }
    public int RunPeriod_ClaimFailurePosted { set; get; }
    public string AreaName { set; get; }
    public string Decision_ClaimFailureSettlement { set; get; }
    public string SanctionType_ClaimFailureSettlement { set; get; }
    public decimal Percentage_ClaimFailureSettlement { set; get; }
    public string Percentage_ClaimFailureSettlementFormat { set; get; }
    public decimal CompensationAmount_ClaimFailureSettlement { set; get; }
    public string CompensationAmount_ClaimFailureSettlementFormat { set; get; }
    public string Date_ClaimFailureSettlement { set; get; }
    public decimal SalesUnitPrice_ClaimFailureSettlement { set; get; }
    public string SalesUnitPrice_ClaimFailureSettlementFormat { set; get; }
    public decimal NSD_ClaimFailureSettlement { set; get; }
    public string NSD_ClaimFailureSettlementFormat { set; get; }
    public int RunPeriod_ClaimFailureSettlement { set; get; }
    public string FaultDescription_ClaimFailureSettlement { set; get; }
    //Extra
    public string RespCenter { get; set; }
}

public class SalesAndBalanceRow
{
    public string? ReportName { get; set; }
    public string? Period { get; set; }
    public string? CustomerNo { get; set; }
    public string? CustomerName { get; set; }
    public string? DealerNo { get; set; }
    public string? DealerName { get; set; }
    public string? AreaName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public string? NetSaleName { get; set; }
    public string? NetSaleName2 { get; set; }
    public decimal NetSale { get; set; }
    public decimal NetSale2 { get; set; }
    public decimal TotalSale { get; set; }
    public decimal Balance { get; set; }
    public bool HideCustomer { get; set; }
    public bool HideDealer { get; set; }
    public bool HideRegion { get; set; }
    public string? Filter { get; set; }
}

public class PaymentCollectionRow
{
    public string? ReportName { get; set; }
    public string? Period { get; set; }
    public string? CustomerNo { get; set; }
    public string? CustomerName { get; set; }
    public string? DealerNo { get; set; }
    public string? DealerName { get; set; }
    public string? AreaName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public decimal Collection { get; set; }
    public bool HideCustomer { get; set; }
    public bool HideDealer { get; set; }
    public bool HideRegion { get; set; }
    public bool HideArea { get; set; }
    public string? Filter { get; set; }
}

public class TyreSalesRow
{
    public string? ReportName { get; set; }
    public string? Product01 { get; set; }
    public string? Product02 { get; set; }
    public string? Period { get; set; }
    public string? CustomerNo { get; set; }
    public string? CustomerName { get; set; }
    public string? DealerNo { get; set; }
    public string? DealerName { get; set; }
    public string? AreaName { get; set; }
    public string? RegionCode { get; set; }
    public string? RegionName { get; set; }
    public decimal NetSale { get; set; }
    public decimal NetSale2 { get; set; }
    public decimal TotalSale { get; set; }
    public bool HideCustomer { get; set; }
    public bool HideDealer { get; set; }
    public bool HideArea { get; set; }
    public bool HideRegion { get; set; }
    public string? Filter { get; set; }
    public decimal RG { get; set; }
    public decimal RR { get; set; }
    public decimal RP { get; set; }
    public decimal RL { get; set; }
    public decimal RT { get; set; }
    public decimal RO { get; set; }
    public decimal RO1 { get; set; }
    public decimal RTotal { get; set; }
    public decimal EG { get; set; }
    public decimal ER { get; set; }
    public decimal EP { get; set; }
    public decimal EL { get; set; }
    public decimal ET { get; set; }
    public decimal EO { get; set; }
    public decimal EO1 { get; set; }
    public decimal ETotal { get; set; }
}

public class BelowBasePriceSalesRow
{
    public string? ReportName { get; set; }
    public string? Period { get; set; }
    public string? CustomerNo { get; set; }
    public string? CustomerName { get; set; }
    public string? DealerNo { get; set; }
    public string? DealerName { get; set; }
    public string? AreaName { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Date { get; set; }
    public string? Tyre { get; set; }
    public string? Category { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal BaseUnitPrice { get; set; }
    public decimal Difference { get; set; }
}

public class EcomileTyreStockRow
{
    public string? ReportName { get; set; }
    public string? Period { get; set; }
    public string? PurchDate { get; set; }
    public string? ProdDate { get; set; }
    public int PurchAge { get; set; }
    public int ProdAge { get; set; }
    public string? Tyre { get; set; }
    public string? SerialNo { get; set; }
    public string? Make { get; set; }
    public int Qty { get; set; }
    public string? CasingCondition { get; set; }
    public string? ProdCondition { get; set; }
}

public class CustomerTrialBalanceRow
{
    public string? CompanyName { get; set; }
    public string? PeriodFilter { get; set; }
    public string? CustFieldCaptPostingGroup { get; set; }
    public string?   CustTableCaptioncustFilter { get; set; }

    public string? CustFilter { get; set; }

    public string? EmptyString { get; set; }

    public DateTime PeriodStartDate { get; set; }

    public string? PeriodFilter1 { get; set; }

    public DateTime FiscalYearStartDate { get; set; }

    public string? FiscalYearFilter { get; set; }

    public DateTime PeriodEndDate { get; set; }

    public string? PostingGroup_Customer { get; set; }

    public decimal YTDTotal { get; set; }

    public string? YTDTotalFormat { get; set; }

    public decimal YTDCreditAmt { get; set; }

    public string? YTDCreditAmtFormat { get; set; }

    public decimal YTDDebitAmt { get; set; }

    public string? YTDDebitAmtFormat { get; set; }

    public decimal YTDBeginBalance { get; set; }

    public string? YTDBeginBalanceFormat { get; set; }

    public decimal PeriodCreditAmt { get; set; }

    public string? PeriodCreditAmtFormat { get; set; }

    public decimal PeriodDebitAmt { get; set; }

    public string? PeriodDebitAmtFormat { get; set; }

    public decimal PeriodBeginBalance { get; set; }

    public string? PeriodBeginBalanceFormat { get; set; }

    public string? Name_Customer { get; set; }

    public string? No_Customer { get; set; }

    public string? TotalPostGroup_Customer { get; set; }

    public string? CustTrialBalanceCaption { get; set; }

    public string? CurrReportPageNoCaption { get; set; }

    public string? AmtsinLCYCaption { get; set; }

    public string? inclcustentriesinperiodCaption { get; set; }

    public string? YTDTotalCaption { get; set; }

    public string? PeriodCaption { get; set; }

    public string? FiscalYearToDateCaption { get; set; }

    public string? NetChangeCaption { get; set; }

    public string? TotalinLCYCaption { get; set; }

    public string? DealerCode_Customer { get; set; }

    public string? AreaCode_Customer { get; set; }

    public bool ShowDealer { get; set; }

    public bool ShowArea { get; set; }

    public string? AreaName { get; set; }

    public string? DealerName { get; set; }

    public bool bSorting { get; set; }
    public string? UserName { get; set; }
    public string? No_CustomerCaption { get; set; }
    public string? Name_CustomerCaption { get; set; }
    public string? PeriodBeginBalanceCaption { get; set; }
    public string? PeriodDebitAmtCaption { get; set; }
    public string? PeriodCreditAmtCaption { get; set; }
    public string? PeriodStartDateStr { get; set; }
    public string? PeriodEndDateStr { get; set; }
    public string? FiscalYearStartDateStr { get; set; }
    public bool Active { get; set; }
    public bool HasBalance { get; set; }
}

public class StatementOfAccount
{
    public string? ExternalDocumentNo_CustLedgerEntry { get; set; }
    public string? CustomerNo_CustLedgerEntry { get; set; }
    public DateTime PostingDate_CustLedgerEntry { get; set; }
    public string? DocumentType_CustLedgerEntry { get; set; }
    public string? DocumentNo_CustLedgerEntry { get; set; }
    public decimal CustBalance { get; set; }
    public decimal OpnBalance { get; set; }
    public decimal Amount_CustLedgerEntry { get; set; }
    public decimal DebitAmount_CustLedgerEntry { get; set; }
    public decimal CreditAmount_CustLedgerEntry { get; set; }
    public string? SellToName { get; set; }
    public string? SourceCode_CustLedgerEntry { get; set; }
    public string? DocTypeColor { get; set; }
    public string? Add1 { get; set; }
    public string? Add2 { get; set; }
    public string? Add3 { get; set; }
    public string? Add4 { get; set; }
    public string? Add5 { get; set; }
    public string? TotalText { get; set; }
    public string? BalanceText { get; set; }
    public string? COMPANYNAME { get; set; }
    public string? PeriodText { get; set; }
    public string? CompAdd1 { get; set; }
    public string? CompAdd2 { get; set; }
    public string? CompAdd3 { get; set; }
    public string? CompAdd4 { get; set; }
    public string? AreaName { get; set; }
    public string? DealerName { get; set; }
    public string? UserName { get; set; }
    public bool bHideDealer { get; set; }
    public bool bSorting { get; set; }
    public override string ToString()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}

public class CustomerRecord
{
    public string? No { get; set; }
    public string? Name { get; set; }
    public string? Name2 { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? StateName { get; set; }
    public string? PostalCode { get; set; }
    public string? PhoneNo { get; set; }

    public List<string> GetAddress()
    {
        var address = new List<string>();
        address.Add((Name ?? "") + (Name2 ?? ""));

        if (!string.IsNullOrWhiteSpace(Address))
            address.Add(Address);
        else if (!string.IsNullOrWhiteSpace(Address2))
            address.Add(Address2);

        if (!string.IsNullOrWhiteSpace(City) && !string.IsNullOrWhiteSpace(PostalCode))
            address.Add($"{City} - {PostalCode}");
        else
        {
            if (!string.IsNullOrWhiteSpace(City))
                address.Add(City);
            if (!string.IsNullOrWhiteSpace(PostalCode))
                address.Add(PostalCode);
        }
        
        if (!string.IsNullOrWhiteSpace(StateName))
            address.Add(StateName);
            
        if (!string.IsNullOrWhiteSpace(PhoneNo))
            address.Add($"Phone No. {PhoneNo}");
            
        return address;
    }
}

public enum NatureOfBusiness
{    
    TyreRetreading = 0,
    RubberManufacturing = 1,
    TilesManufacturing = 2,    
    Administration = 3,
    SalesDepot = 4,
    CustomerSite = 5,
    OldCompany = 6
}

public class RespCenter
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Company { get; set; }
    public NatureOfBusiness NatureOfBusiness { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? PostCode { get; set; }
    public string? State { get; set; }
    public string? StateName { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? GSTRegNo { get; set; }
    public string? PANNo { get; set; }
    public string? HomePage { get; set; }
    public string? Juridiction { get; set; }
    public string? CINNo { get; set; }
    public byte[]? Logo { get; set; }

    public string GetAddress1()
    {
        var a1 = string.IsNullOrWhiteSpace(Address) ? "" : Address + ",";
        var a2 = string.IsNullOrWhiteSpace(Address2) ? "" : Address2;
        return (a1 + " " + a2).Trim();
    }

    public string GetAddress2()
    {
        var city = string.IsNullOrWhiteSpace(City) ? "" : City + " -";
        var post = string.IsNullOrWhiteSpace(PostCode) ? "" : PostCode;
        if (!string.IsNullOrWhiteSpace(State))
        {
            post = string.IsNullOrWhiteSpace(post) ? "" : post + ",";
            var stateInfo = string.IsNullOrWhiteSpace(StateName) ? "" : StateName;
            return (city + " " + post + " " + stateInfo).Trim();
        }
        return (city + " " + post).Trim();
    }

    public string GetContactLine()
    {            
        if (!string.IsNullOrWhiteSpace(Phone))
            return $"Contact No: {Phone}";
        return string.Empty;
    }

    public string GetContactWebLine()
    {
        string value = string.Empty;
        if (!string.IsNullOrWhiteSpace(Email))
            value = $"E-Mail: {Email}";
        if (!string.IsNullOrWhiteSpace(HomePage))
        {
            if (!string.IsNullOrWhiteSpace(value))
                value += ", ";
            value += $"Web site: {HomePage}";
        }
        return value;
    }

    public string GetAddressInSingleLine(bool label)
    {
        string labelTxt = string.Empty;
        if (label)
        {
            switch (NatureOfBusiness)
            {
                case NatureOfBusiness.TyreRetreading:
                case NatureOfBusiness.TilesManufacturing:
                case NatureOfBusiness.RubberManufacturing:
                    labelTxt = "Factory Address : ";
                    break;
                case NatureOfBusiness.SalesDepot:
                    labelTxt = "Depot Address : ";
                    break;
            }
        }
        return labelTxt + GetAddress1() + " " + GetAddress2();
    }

    public string GetRegOfficeAddressInSingleLine(bool label, RespCenter respCenterRegOffice)
    {
        if (label)
            return "Reg. Office Address : " + respCenterRegOffice.GetAddressInSingleLine(false);
        return respCenterRegOffice.GetAddressInSingleLine(false);
    }

    public string GetPANNo()
    {
        if (!string.IsNullOrWhiteSpace(PANNo))
            return $"P.A.N No. : {PANNo}";
        return string.Empty;
    }

    public string GetGSTNo()
    {
        if (!string.IsNullOrWhiteSpace(GSTRegNo))
            return $"GSTIN : {GSTRegNo}";
        return string.Empty;
    }

    public string GetCINNo()
    {
        if (!string.IsNullOrWhiteSpace(CINNo))
            return $"C.I.N. No. : {CINNo}";
        return string.Empty;
    }

    public string GetJurisdiction()
    {
        if (!string.IsNullOrWhiteSpace(Juridiction))
            return $"Subject to {Juridiction} Jurisdiction";
        return string.Empty;
    }

    public override string ToString()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}

public class DayTransactionRow
{
    public string? CustomerNo { get; set; }
    public string? Name { get; set; }
    public DateTime PostingDate { get; set; }
    public string? DocumentNo { get; set; }
    public string? ExtDocNo { get; set; }
    public string? Description { get; set; }
    public decimal OpeningBal { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public string? UserName { get; set; }
    public string? PeriodText { get; set; }
}

public class EcomileItemSoldRow
{
    public string? Location { get; set; }
    public string? ReportName { get; set; }
    public string? DateRange { get; set; }
    public string? Tyre { get; set; }
    public string? Pattern { get; set; }
    public int Quantity { get; set; }
}

public class EcomileSizeMakeRow
{
    public string? Location { get; set; }
    public string? ReportName { get; set; }
    public string? DateRange { get; set; }
    public string? Tyre { get; set; }
    public string? Make { get; set; }
    public int Quantity { get; set; }
}
public class BankAccount
{
    public string? Code { get; set; }
    public string? AccountNo { get; set; }
    public string? RealBankAccountNo { get; set; }
    public string? IFSCCode { get; set; }
}

public class DocumentAddressRecord
{
    public string? No { get; set; }
    public string? ShipToCode { get; set; }
    public string? Name { get; set; }
    public string? Name2 { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? GSTNo { get; set; }
    public string? ShipGSTNo { get; set; }
    public string? State { get; set; }
    public string? ShipName { get; set; }
    public string? ShipName2 { get; set; }
    public string? ShipAddress { get; set; }
    public string? ShipAddress2 { get; set; }
    public string? ShipCity { get; set; }
    public string? ShipPostalCode { get; set; }
    public string? ShipState { get; set; }
}

public class DocumentAddress
{
    public string? No { get; set; }
    public string[] BillToAddress { get; set; } = new string[7];
    public string[] ShipToAddress { get; set; } = new string[7];
}

public class GstComponent
{
    public string? Code { get; set; }
    public string? Value { get; set; }
}

public class ProductMixEcomileRow
{
    public string? ReportName { get; set; }
    public string? PeriodText { get; set; }
    public string? CompanyName { get; set; }
    public string? ItemNo { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Qty { get; set; }
    public decimal SalesAmount { get; set; }
    public string? ItemGroup { get; set; }
    public string? Location { get; set; }
}

public class LocationRespCenter
{
    public string? LocationCode { get; set; }
    public string? RespCenterCode { get; set; }
    public string? RespCenterName { get; set; }
}
