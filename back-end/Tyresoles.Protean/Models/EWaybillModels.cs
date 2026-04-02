using System.Text.Json;

namespace Tyresoles.Protean.Models.EWaybill;

// ──────────────────────────────────────────────────────────────────────────────
// Request models
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Caller-facing wrapper for all EWaybill operations.</summary>
public sealed class EWaybillRequest
{
    public required string UserName { get; init; }
    public required string Password { get; init; }
    public required string Gstin    { get; init; }

    /// <summary>Action to perform (e.g. "Generate").</summary>
    public string? Action { get; init; }

    /// <summary>Content payload for the specific action.</summary>
    public object? Request { get; init; }

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public sealed class EWaybillGeneratePayload
{
    public string  supplyType        { get; set; } = "O";
    public string? subSupplyType     { get; set; }
    public string? subSupplyDesc     { get; set; }
    public string  docType           { get; set; } = "INV";
    public string  docNo             { get; set; } = "";
    public string  docDate           { get; set; } = "";
    public string? fromGstin         { get; set; }
    public string? fromTrdName       { get; set; }
    public string? fromAddr1         { get; set; }
    public string? fromAddr2         { get; set; }
    public string? fromPlace         { get; set; }
    public string? actFromStateCode  { get; set; }
    public string? fromPincode       { get; set; }
    public string? fromStateCode     { get; set; }
    public string? toGstin           { get; set; }
    public string? toTrdName         { get; set; }
    public string? toAddr1           { get; set; }
    public string? toAddr2           { get; set; }
    public string? toPlace           { get; set; }
    public string? toPincode         { get; set; }
    public string? toStateCode       { get; set; }
    public string? actToStateCode    { get; set; }
    public string? transactionType   { get; set; }
    public decimal totalValue        { get; set; }
    public decimal cgstValue         { get; set; }
    public decimal sgstValue         { get; set; }
    public decimal igstValue         { get; set; }
    public decimal cessValue         { get; set; }
    public decimal cessNonAdvolValue { get; set; }
    public decimal otherValue        { get; set; }
    public decimal totInvValue       { get; set; }
    public string? transMode         { get; set; }
    public int     transDistance     { get; set; }
    public string? transporterName   { get; set; }
    public string? transporterId     { get; set; }
    public string? transDocNo        { get; set; }
    public string? transDocDate      { get; set; }
    public string? vehicleNo         { get; set; }
    public string? vehicleType       { get; set; }
    public List<EWaybillItem> itemList { get; set; } = [];
}

public sealed class EWaybillItem
{
    public string?  productName  { get; set; }
    public string?  productDesc  { get; set; }
    public int      hsnCode      { get; set; }
    public int      quantity     { get; set; }
    public string?  qtyUnit      { get; set; }
    public decimal  taxableAmount { get; set; }
    public decimal  sgstRate     { get; set; }
    public decimal  cgstRate     { get; set; }
    public decimal  igstRate     { get; set; }
    public decimal  cessRate     { get; set; }
    public decimal  cessNonadvol { get; set; }
}

public sealed class EWaybillPartBPayload
{
    public int     ewbNo       { get; set; }
    public string  vehicleNo   { get; set; } = "";
    public string  fromPlace   { get; set; } = "";
    public int     fromState   { get; set; }
    public string? reasonCode  { get; set; }
    public string? reasonRem   { get; set; }
    public string? transDocNo  { get; set; }
    public string? transDocDate { get; set; }
    public string? transMode   { get; set; }
    public string? vehicleType { get; set; }
}

public sealed class EWaybillConsolidatePayload
{
    public string  fromPlace  { get; set; } = "";
    public int     fromState  { get; set; }
    public string  vehicleNo  { get; set; } = "";
    public string? transMode  { get; set; }
    public string? transDocNo { get; set; }
    public string? transDocDate { get; set; }
    public List<EWbConsolidateEntry> tripSheetEwbBills { get; set; } = [];
}

public sealed class EWbConsolidateEntry
{
    public string ewbNo { get; set; } = "";
}

public sealed class EWaybillCancelPayload
{
    public int    ewbNo         { get; set; }
    public int    cancelRsnCode { get; set; }
    public string? cancelRmrk  { get; set; }
}

public sealed class EWaybillByDocPayload
{
    public string docType { get; set; } = "";
    public string docNo   { get; set; } = "";
}

// ──────────────────────────────────────────────────────────────────────────────
// Response models
// ──────────────────────────────────────────────────────────────────────────────

public sealed class EWaybillGenResult
{
    public string? ewayBillNo   { get; set; }
    public string? ewayBillDate { get; set; }
    public string? validUpto    { get; set; }
    public string? alert        { get; set; }
}

public sealed class EWaybillPartBResult
{
    public string? vehUpdDate { get; set; }
    public string? validUpto  { get; set; }
}

public sealed class EWaybillConsolidateResult
{
    public string? cEwbNo   { get; set; }
    public string? cEWBDate { get; set; }
}

public sealed class EWaybillCancelResult
{
    public int    ewayBillNo { get; set; }
    public string? cancelDate { get; set; }
}
