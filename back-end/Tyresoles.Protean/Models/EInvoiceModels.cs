using System.Text.Json;

namespace Tyresoles.Protean.Models.EInvoice;

// ──────────────────────────────────────────────────────────────────────────────
// Request models
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Top-level wrapper sent by callers. The inner <see cref="Request"/> is
/// serialised + AES-encrypted before being sent to the IRP.
/// </summary>
public sealed class EInvoiceRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string Gstin    { get; init; }

    /// <summary>EInvoice payload per GST schema v1.1.</summary>
    public required EInvoicePayload Request { get; init; }

    /// <summary>Optional: document reference for logging (type + number).</summary>
    public DocumentRef? Document { get; init; }

    /// <summary>Optional: fetch by existing IRN instead of generating new one.</summary>
    public string? Irn { get; init; }

    public string DocumentLabel() =>
        Document is not null ? $"{Document.Type} {Document.No}" : Irn ?? Request.DocDtls?.No ?? "";
}

public sealed class EInvoicePayload
{
    public string Version { get; set; } = "1.1";
    public TransDetail?   TranDtls  { get; set; }
    public DocDetail?     DocDtls   { get; set; }
    public SellerDetail?  SellerDtls { get; set; }
    public BuyerDetail?   BuyerDtls  { get; set; }
    public ShipDetail?    ShipDtls   { get; set; }
    public List<InvoiceItem> ItemList { get; set; } = [];
    public ValueDetails?  ValDtls   { get; set; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public sealed class TransDetail
{
    public string TaxSch     { get; set; } = "GST";
    public required string SupTyp { get; set; } // B2B, SEZWP, SEZWOP, EXPWP, EXPWOP, DEXP
    public string RegRev     { get; set; } = "N";
    public string? EcmGstin  { get; set; }
    public string IgstOnIntra { get; set; } = "N";
}

public sealed class DocDetail
{
    public required string Typ { get; set; } // INV, CRN, DBN
    public required string No  { get; set; }
    public required string Dt  { get; set; } // dd/mm/yyyy
}

public sealed class SellerDetail
{
    public required string Gstin  { get; set; }
    public required string LglNm  { get; set; }
    public string?  TrdNm  { get; set; }
    public required string Addr1  { get; set; }
    public string?  Addr2  { get; set; }
    public required string Loc    { get; set; }
    public required int    Pin    { get; set; }
    public required string Stcd   { get; set; }
    public string?  Ph     { get; set; }
    public string?  Em     { get; set; }
}

public sealed class BuyerDetail
{
    public required string Gstin  { get; set; }
    public required string LglNm  { get; set; }
    public string?  TrdNm  { get; set; }
    public required string Pos    { get; set; }
    public required string Addr1  { get; set; }
    public string?  Addr2  { get; set; }
    public required string Loc    { get; set; }
    public required int    Pin    { get; set; }
    public required string Stcd   { get; set; }
    public string?  Ph     { get; set; }
    public string?  Em     { get; set; }
}

public sealed class ShipDetail
{
    public string?  Gstin  { get; set; }
    public required string LglNm  { get; set; }
    public string?  TrdNm  { get; set; }
    public required string Addr1  { get; set; }
    public string?  Addr2  { get; set; }
    public string?  Loc    { get; set; }
    public required int    Pin    { get; set; }
    public required string Stcd   { get; set; }
}

public sealed class InvoiceItem
{
    public required string  SlNo       { get; set; }
    public string?  PrdDesc   { get; set; }
    public required string  IsServc    { get; set; } // Y / N
    public required string  HsnCd      { get; set; }
    public string?  Unit      { get; set; }
    public decimal  Qty       { get; set; }
    public decimal  UnitPrice { get; set; }
    public decimal  TotAmt    { get; set; }
    public decimal  Discount  { get; set; }
    public decimal  AssAmt    { get; set; }
    public decimal  GstRt     { get; set; }
    public decimal  IgstAmt   { get; set; }
    public decimal  CgstAmt   { get; set; }
    public decimal  SgstAmt   { get; set; }
    public decimal  Otherchrg { get; set; }
    public decimal  TotItemVal { get; set; }
}

public sealed class ValueDetails
{
    public decimal AssVal   { get; set; }
    public decimal CgstVal  { get; set; }
    public decimal SgstVal  { get; set; }
    public decimal IgstVal  { get; set; }
    public decimal CesVal   { get; set; }
    public decimal StCesVal { get; set; }
    public decimal Discount { get; set; }
    public decimal OthChrg  { get; set; }
    public decimal RndOffAmt { get; set; }
    public decimal TotInvVal { get; set; }
}

public sealed class DocumentRef
{
    public required string Type { get; init; }
    public required string No   { get; init; }
    public DateTime Date        { get; init; }
}

// ──────────────────────────────────────────────────────────────────────────────
// Response models
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Decrypted EInvoice IRN response from IRP.</summary>
public sealed class EInvoiceResult
{
    public string? AckNo         { get; set; }
    public string? AckDt         { get; set; }
    public string? Irn           { get; set; }
    public string? SignedInvoice { get; set; }
    public string? SignedQRCode  { get; set; }
    public string? Status        { get; set; }
    public string? EwbNo         { get; set; }
    public string? EwbDt         { get; set; }
    public string? EwbValidTill  { get; set; }
    public string? Remarks       { get; set; }
}
