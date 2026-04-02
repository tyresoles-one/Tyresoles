namespace Tyresoles.Protean.Models.Gstin;

/// <summary>GSTIN details returned from the IRP master data API.</summary>
public sealed class GstinDetails
{
    public string? Gstin      { get; set; }
    public string? TradeName  { get; set; }
    public string? LegalName  { get; set; }
    public string? AddrBnm    { get; set; }
    public string? AddrBno    { get; set; }
    public string? AddrFlno   { get; set; }
    public string? AddrSt     { get; set; }
    public string? AddrLoc    { get; set; }
    public int?    StateCode  { get; set; }
    public int?    AddrPncd   { get; set; }
    public string? TxpType    { get; set; }
    public string? Status     { get; set; }
    public string? BlkStatus  { get; set; }
    public string? DtReg      { get; set; }
    public string? DtDReg     { get; set; }
}
