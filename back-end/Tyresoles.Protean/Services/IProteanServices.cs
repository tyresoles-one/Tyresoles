using Tyresoles.Protean.Models.EInvoice;
using Tyresoles.Protean.Models.EWaybill;
using Tyresoles.Protean.Models.Gstin;

namespace Tyresoles.Protean.Services;

/// <summary>
/// High-level EInvoice service (IRP v1.04).
/// All methods are fully async and cancellation-aware.
/// </summary>
public interface IEInvoiceService
{
    /// <summary>Generate a new IRN for the given invoice.</summary>
    Task<EInvoiceResult> GenerateIrnAsync(EInvoiceRequest request, CancellationToken ct = default);

    /// <summary>Retrieve an existing IRN by IRN hash.</summary>
    Task<EInvoiceResult> GetIrnAsync(EInvoiceRequest request, CancellationToken ct = default);

    /// <summary>Retrieve an existing IRN by document type/number/date (last 2 days only).</summary>
    Task<EInvoiceResult> GetIrnByDocumentAsync(EInvoiceRequest request, CancellationToken ct = default);

    /// <summary>Fetch live GSTIN details from the IRP master.</summary>
    Task<GstinDetails?> GetGstinAsync(string searchGstin, CancellationToken ct = default);

    /// <summary>Force-sync GSTIN details from the IRP master.</summary>
    Task<GstinDetails?> SyncGstinAsync(string searchGstin, CancellationToken ct = default);
}

/// <summary>
/// High-level EWaybill service (v1.03).
/// All methods are fully async and cancellation-aware.
/// </summary>
public interface IEWaybillService
{
    Task<EWaybillGenResult>         GenerateAsync(EWaybillRequest request, CancellationToken ct = default);
    Task<EWaybillPartBResult>       UpdatePartBAsync(EWaybillRequest request, CancellationToken ct = default);
    Task<EWaybillConsolidateResult> GenerateConsolidateAsync(EWaybillRequest request, CancellationToken ct = default);
    Task<EWaybillCancelResult>      CancelAsync(EWaybillRequest request, CancellationToken ct = default);
    Task<EWaybillGenResult>         GetByDocumentAsync(EWaybillRequest request, CancellationToken ct = default);
}
