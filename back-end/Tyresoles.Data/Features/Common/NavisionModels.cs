using System.Text.Json;

namespace Tyresoles.Data.Features.Common
{

public class GSTApiLog
{
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string Source { get; set; } = "GST System";
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class EInvoice
{
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentNo { get; set; } = string.Empty;
    public string AckNo { get; set; } = string.Empty;
    public DateTime AckDate { get; set; }
    public string IRN { get; set; } = string.Empty;
    public string QRImage { get; set; } = string.Empty;
    public string JsonText { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class PartyGstin
{
    public string Type { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Gstin { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string BlockStatus { get; set; } = string.Empty;
    public string TradeName { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class CreateDealer
{
    public string CustomerNo { get; set; } = string.Empty;
    public string DealerCode { get; set; } = string.Empty;
    public int Product { get; set; }
    public int BusModel { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DlrshipName { get; set; } = string.Empty;
    public decimal InvAmt { get; set; }
    public DateTime DoB { get; set; }
    public DateTime DoA { get; set; }
    public DateTime DoE { get; set; }
    public DateTime DoJ { get; set; }
    public int BrdShop { get; set; }
    public string Pan { get; set; } = string.Empty;
    public string Gst { get; set; } = string.Empty;
    public string Adhar { get; set; } = string.Empty;
    public string BkName { get; set; } = string.Empty;
    public string BkAcNo { get; set; } = string.Empty;
    public string BkBrch { get; set; } = string.Empty;
    public string BkIfsc { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class MergerEntity
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OldCode { get; set; } = string.Empty;
    public string OldCompany { get; set; } = string.Empty;
    public string OldRespCenter { get; set; } = string.Empty;
    public string NewRespCenter { get; set; } = string.Empty;
    public string SerRespCenter { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string PostCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string AreaCode { get; set; } = string.Empty;
    public string DealerCode { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string PrimaryNo { get; set; } = string.Empty;
    public bool Primary { get; set; }
    public string GSTIN { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string PriceType { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class ClaimRequest
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string CustomerNo { get; set; } = string.Empty;
    public string Tyre { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string SerialNo { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal TWDAmount { get; set; }
    public decimal DealerDisc { get; set; }
    public decimal LineDisc { get; set; }
    public string Variant { get; set; } = string.Empty;
    public string InspReport { get; set; } = string.Empty;
    public bool OwnerRisk { get; set; }
    public string OldCompany { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class Vendor
{
    public string No { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Address2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string StateCode { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string EcoMgrCode { get; set; } = string.Empty;
    public bool SelfInvoice { get; set; }
    public string NameOnInvoice { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccNo { get; set; } = string.Empty;
    public string BankIFSC { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string PanNo { get; set; } = string.Empty;
    public string AdhaarNo { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class FixedAsset
{
    public string No { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Description2 { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public string Employee { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string MainAssetNo { get; set; } = string.Empty;
    public bool Inactive { get; set; }
    public bool Blocked { get; set; }
    public string Class { get; set; } = string.Empty;
    public string SubClass { get; set; } = string.Empty;
    public string SerialNo { get; set; } = string.Empty;
    public string VendorNo { get; set; } = string.Empty;
    public decimal PurchaseCost { get; set; }
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class EntityBalance
{
    public string Type { get; set; } = string.Empty;
    public string No { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class Vehicle
{
    public string No { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string GSTIN { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string RespCenter { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class DocumentImage
{
    public string DocumentNo { get; set; } = string.Empty;
    public int Type { get; set; }
    public int LineNo { get; set; }
    public string Image { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class SupportRequest
{
    public string UserID { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string RequestDataJson { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

public class FetchParams
{
    public string ReportName { get; set; } = string.Empty;
    public string ReportOutput { get; set; } = "PDF";
    public string Type { get; set; } = string.Empty;
    public string View { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public List<string> Customers { get; set; } = new();
    public List<string> Dealers { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> Regions { get; set; } = new();
    public List<string> RespCenters { get; set; } = new();
    public List<string> Nos { get; set; } = new();
    public string UserCode { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string UserDepartment { get; set; } = string.Empty;
    public string UserSpecialToken { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

/// <summary>Input model for updating a dealer's master data. Mirrors the front-end FormData shape.</summary>
public class SaveDealerInput
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DealershipName { get; set; } = string.Empty;
    public string EMail { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public int BusinessModel { get; set; }
    public int Product { get; set; }
    public int Status { get; set; }
    public decimal InvestmentAmount { get; set; }
    public DateTime DealershipStartDate { get; set; }
    public DateTime DealershipExpDate { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DateTime DateOfAniversary { get; set; }
    public int BrandedShop { get; set; }
    public string PanNo { get; set; } = string.Empty;
    public string GstNo { get; set; } = string.Empty;
    public string AadharNo { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankACNo { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string BankIFSC { get; set; } = string.Empty;
    public override string ToString() => JsonSerializer.Serialize(this);
}

/// <summary>Result of creating a dealer from a customer (see <c>CreateDealerAsync</c>).</summary>
public sealed class CreateDealerResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? DealerCode { get; init; }
}

}

namespace Procurement
{
    public class OrderInfo
    {
        public string OrderNo { get; set; } = string.Empty;
        public string SupplierCode { get; set; } = string.Empty;
        public string ManagerCode { get; set; } = string.Empty;
        public int Status { get; set; }
    }

    public class OrderLine
    {
        public string No { get; set; } = string.Empty;
        public int LineNo { get; set; }
        public string VendorNo { get; set; } = string.Empty;
        public string ItemNo { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string SubMake { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string Inspection { get; set; } = string.Empty;
        public string InspectorCode { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class OrderLineDispatch
    {
        public string OrderNo { get; set; } = string.Empty;
        public int LineNo { get; set; }
        public string No { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string SerialNo { get; set; } = string.Empty;
        public string Inspector { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
        public string DispatchOrderNo { get; set; } = string.Empty;
        public string DispatchDate { get; set; } = string.Empty;
        public string DispatchDestination { get; set; } = string.Empty;
        public string DispatchVehicleNo { get; set; } = string.Empty;
        public string DispatchMobileNo { get; set; } = string.Empty;
        public string DispatchTransporter { get; set; } = string.Empty;
        public string Button { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string NewSerialNo { get; set; } = string.Empty;
        public string FactInspector { get; set; } = string.Empty;
        public string FactInspection { get; set; } = string.Empty;
        public string FactInspectorFinal { get; set; } = string.Empty;
        public string RejectionReason { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
    }
}
