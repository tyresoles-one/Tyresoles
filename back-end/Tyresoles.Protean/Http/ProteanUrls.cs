namespace Tyresoles.Protean.Http;

/// <summary>Protean IRP (EInvoice v1.04) URL constants.</summary>
public static class EInvoiceUrls
{
    private const string Production = "https://api.proteangsp.co.in/gus/irp/nic";
    private const string Sandbox    = "https://test.proteangsp.co.in/gus/irp/nic";

    public static string Base(bool sandbox) => sandbox ? Sandbox : Production;

    public static string Authentication(bool sandbox)        => $"{Base(sandbox)}/eivital/v1.04/auth";
    public static string GenerateEInvoice(bool sandbox)      => $"{Base(sandbox)}/eicore/v1.03/Invoice";
    public static string CancelEInvoice(bool sandbox)        => $"{Base(sandbox)}/eicore/v1.03/Invoice/Cancel";
    public static string GetEInvoice(bool sandbox, string irn) => $"{Base(sandbox)}/eicore/v1.03/Invoice/irn/{irn}";
    public static string GetIRNByDocument(bool sandbox)      => $"{Base(sandbox)}/eicore/v1.03/Invoice/irnbydocdetails";
    public static string GenerateEWaybillByIRN(bool sandbox) => $"{Base(sandbox)}/eiewb/v1.03/ewaybill";
    public static string GetEWaybillByIRN(bool sandbox, string irn) => $"{Base(sandbox)}/eiewb/v1.03/ewaybill/irn/{irn}";
    public static string GetGSTIN(bool sandbox, string gstin)  => $"{Base(sandbox)}/eivital/v1.04/Master/gstin/{gstin}";
    public static string SyncGSTIN(bool sandbox, string gstin) => $"{Base(sandbox)}/eivital/v1.04/Master/syncgstin/{gstin}";
}

/// <summary>Protean EWaybill (v1.03) URL constants.</summary>
public static class EWaybillUrls
{
    private const string Production = "https://api.proteangsp.co.in/gus/ewb";
    private const string Sandbox    = "https://test.proteangsp.co.in/gus/ewb/ewaybillapi";

    public static string Base(bool sandbox) => sandbox ? Sandbox : Production;

    public static string Authentication(bool sandbox)   => $"{Base(sandbox)}/v1.03/auth";
    public static string Main(bool sandbox)             => $"{Base(sandbox)}/v1.03/ewayapi/";
    public static string GetEwbByDoc(bool sandbox)      => $"{Main(sandbox)}GetEwayBillGeneratedByConsigner";
}

/// <summary>EWaybill action codes.</summary>
public static class EWaybillActions
{
    public const string Generate          = "GENEWAYBILL";
    public const string GenPartB          = "VEHEWB";
    public const string GenConsolidate    = "GENCEWB";
    public const string Cancel            = "CANEWB";
    public const string Reject            = "REJEWB";
    public const string UpdateTransporter = "UPDATETRANSPORTER";
    public const string ExtendValidity    = "EXTENDVALIDITY";
}
