using Tyresoles.Protean.Models.EWaybill;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Protean;

// ──────────────────────────────────────────────────────────────────────────────
// Domain result records (data-layer view of an invoice ready for EInv / EWB)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A posted sales invoice enriched with Responsibility-Center GST credentials,
/// ready to be submitted to the IRP for E-Invoice generation.
/// </summary>
public sealed record EInvoiceCandidate
{
    // Document identity
    public string  DocumentNo   { get; init; } = "";
    public string  DocumentType { get; init; } = "INV";
    public string  PostingDate  { get; init; } = ""; // dd/MM/yyyy
    public string  RespCenter   { get; init; } = "";

    // Responsibility-Centre / GSTIN credentials
    public string  Gstin        { get; init; } = "";
    public string  UserName     { get; init; } = "";
    public string  Password     { get; init; } = "";
    public string  GSTTradeName { get; init; } = "";
    public string  Address1     { get; init; } = "";
    public string? Address2     { get; init; }
    public string  City         { get; init; } = "";
    public string  Pincode      { get; init; } = "";
    public string  StateCode    { get; init; } = "";

    // Buyer info
    public string  BuyerGstin   { get; init; } = "URP";
    public string  BuyerName    { get; init; } = "";
    public string  BuyerAddr1   { get; init; } = "";
    public string? BuyerAddr2   { get; init; }
    public string  BuyerCity    { get; init; } = "";
    public string  BuyerPincode { get; init; } = "";
    public string  BuyerState   { get; init; } = "";

    // Financials (aggregated at header level)
    public decimal TotalValue   { get; init; }
    public decimal CgstValue    { get; init; }
    public decimal SgstValue    { get; init; }
    public decimal IgstValue    { get; init; }
    public decimal TotInvValue  { get; init; }
    public decimal OthChrg      { get; init; }
    public decimal RndOffAmt    { get; init; }

    // Line items (required for IRN generation)
    public IReadOnlyList<Tyresoles.Protean.Models.EInvoice.InvoiceItem> Lines { get; init; } = Array.Empty<Tyresoles.Protean.Models.EInvoice.InvoiceItem>();

    // E-Invoice status
    public string? ExistingIrn  { get; init; }
    public bool    EInvSkip     { get; init; }

    /// <summary>
    /// Maps this candidate's flat properties and lines into a schema-compliant IRP payload.
    /// </summary>
    public Tyresoles.Protean.Models.EInvoice.EInvoicePayload ToPayload()
    {
        return new Tyresoles.Protean.Models.EInvoice.EInvoicePayload
        {
            DocDtls = new Tyresoles.Protean.Models.EInvoice.DocDetail
            {
                Typ = DocumentType,
                No  = DocumentNo,
                Dt  = PostingDate
            },
            TranDtls = new Tyresoles.Protean.Models.EInvoice.TransDetail
            {
                SupTyp = "B2B" // Standard for these documents
            },
            SellerDtls = new Tyresoles.Protean.Models.EInvoice.SellerDetail
            {
                Gstin = Gstin,
                LglNm = GSTTradeName, // NIC often wants Legal Name here
                TrdNm = GSTTradeName,
                Addr1 = Address1,
                Addr2 = Address2,
                Loc   = City,
                Pin   = int.TryParse(Pincode, out var p) ? p : 0,
                Stcd  = StateCode
            },
            BuyerDtls = new Tyresoles.Protean.Models.EInvoice.BuyerDetail
            {
                Gstin = BuyerGstin,
                LglNm = BuyerName,
                TrdNm = BuyerName,
                Addr1 = BuyerAddr1,
                Addr2 = BuyerAddr2,
                Loc   = BuyerCity,
                Pin   = int.TryParse(BuyerPincode, out var bp) ? bp : 0,
                Stcd  = BuyerState,
                Pos   = BuyerState
            },
            ValDtls = new Tyresoles.Protean.Models.EInvoice.ValueDetails
            {
                AssVal    = TotalValue,
                CgstVal   = CgstValue,
                SgstVal   = SgstValue,
                IgstVal   = IgstValue,
                TotInvVal = TotInvValue,
                OthChrg   = OthChrg,
                RndOffAmt = RndOffAmt
            },
            ItemList = Lines.ToList()
        };
    }
}

/// <summary>
/// A posted sales invoice enriched with transport / DC-sheet data,
/// ready to be submitted to the EWaybill portal.
/// </summary>
public sealed record EWaybillCandidate
{
    // Document identity
    public string  DocumentNo      { get; init; } = "";
    public string  PostingDate     { get; init; } = ""; // dd/MM/yyyy
    public int     EWbillStatus    { get; init; }  // 1=Generate, 2=Consolidate, 3=Cancel
    public string  RequestType     { get; init; } = "Generate";

    // From (Responsibility Centre)
    public string  Gstin           { get; init; } = "";
    public string  UserName        { get; init; } = "";
    public string  Password        { get; init; } = "";

    // Transport
    public string? TransporterGstin { get; init; }
    public string? TransporterName  { get; init; }
    public string? TransDocNo       { get; init; }
    public string? TransDocDate     { get; init; }
    public string? VehicleNo        { get; init; }

    // Line-level aggregates (computed per-invoice after query)
    public decimal TotalValue      { get; init; }
    public decimal CgstValue       { get; init; }
    public decimal SgstValue       { get; init; }
    public decimal IgstValue       { get; init; }
    public decimal TotInvValue     { get; init; }

    // Full generate payload (assembled by ProteanDataService from query rows)
    public EWaybillGeneratePayload? Payload { get; init; }
}

/// <summary>Result of writing an EWaybill response back to Sales Invoice Header.</summary>
public enum EWaybillWritebackType { Generate, Consolidate, Cancel, Error }

public sealed record EWaybillWriteback
{
    public string InvoiceNo { get; init; } = "";
    public EWaybillWritebackType Type { get; init; }
    public string? EwbNo     { get; init; }
    public string? EwbDate   { get; init; }
    public string? ValidUpto { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Service interface
// ──────────────────────────────────────────────────────────────────────────────
public interface IProteanDataService
{
    /// <summary>
    /// Returns posted sales invoices that are pending E-Invoice generation
    /// (EInvSkip = 0 and no IRN assigned yet).
    /// </summary>
    Task<IReadOnlyList<EInvoiceCandidate>> GetPendingEInvoicesAsync(
        ITenantScope scope,
        CancellationToken ct = default);

    /// <summary>
    /// Writes IRN, AckNo, AckDate, QR image bytes and JSON back to the posted document header
    /// (Sales Invoice or Sales Cr. Memo), mirroring Nav Connector.InsertEInvoiceDetails.
    /// </summary>
    /// <param name="documentType"><c>INV</c> or <c>CRN</c>.</param>
    Task WriteEInvoiceResultAsync(
        ITenantScope scope,
        string documentType,
        string documentNo,
        string irn,
        string ackNo,
        DateTime ackDate,
        byte[] qrImageBytes,
        string jsonText,
        CancellationToken ct = default);

    /// <summary>
    /// Logs a failed E-Invoice or E-Waybill API call to the GST Api Log table (Connector.InsertGstApiLog).
    /// </summary>
    /// <param name="documentType"><c>INV</c>, <c>CRN</c>, <c>EWB</c>, etc. — mapped to Nav <c>Document Type</c>.</param>
    Task WriteGstApiLogAsync(
        ITenantScope scope,
        string documentType,
        string documentNo,
        string errorCode,
        string errorMessage,
        string source,
        string respCenter = "",
        CancellationToken ct = default);

    /// <summary>
    /// Returns outward sales invoices pending E-Waybill generation / consolidation / cancellation.
    /// EWbillStatus: 1=Generate, 2=Consolidate, 3=Cancel.
    /// </summary>
    Task<IReadOnlyList<EWaybillCandidate>> GetPendingEWaybillsAsync(
        ITenantScope scope,
        CancellationToken ct = default);

    /// <summary>
    /// Writes EWaybill response (number, date, expiry) back to the Sales Invoice Header
    /// and updates its E-WBill Status field.
    /// </summary>
    Task WriteEWaybillResultAsync(
        ITenantScope scope,
        EWaybillWriteback writeback,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves Responsibility Centre credentials for a given invoice (for GetByDoc retries).
    /// </summary>
    Task<(string Gstin, string UserName, string Password)?> GetCredentialsForInvoiceAsync(
        ITenantScope scope,
        string invoiceNo,
        CancellationToken ct = default);

    /// <summary>
    /// Fetches the name of a Customer, Vendor, or Transporter by their code.
    /// </summary>
    Task<string?> GetPartyNameAsync(
        ITenantScope scope,
        string type,
        string code,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the GST-related fields for a Customer or Vendor in the database.
    /// </summary>
    Task UpdateGstinMasterAsync(
        ITenantScope scope,
        string type,
        string code,
        string gstin,
        string tradeName,
        string legalName,
        string status,
        string blockStatus,
        CancellationToken ct = default);
}
