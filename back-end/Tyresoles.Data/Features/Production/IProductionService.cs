using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Data.Features.Production.Models;

namespace Tyresoles.Data.Features.Production;

/// <summary>
/// Production service for managing casing items, procurement operations, and ecomile processes.
/// Ported from legacy Db.Production.cs.
/// </summary>
public interface IProductionService
{
    // Masters
    Task<List<CasingItem>> GetItemNosAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task UpdateCasingAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task InsertCasingItemsAsync(ITenantScope scope, List<CasingItem> casingItems, CancellationToken ct = default);
    Task<List<CodeName>> GetMakesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<CodeName>> GetMakeSubMakeAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<CodeName>> GetVendorsCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<CodeName>> GetInspectorCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    List<CodeName> GetProcurementInspection(FetchParams param);
    Task<List<CodeName>> GetProcurementMarketsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<string> CreateVendorAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<bool> UpdateVendorAsync(ITenantScope scope, VendorModel param, CancellationToken ct = default);
    Task<List<VendorModel>> GetVendorsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);

    // Ecomile Operations
    Task<string> GetEcomileLastNewNumberAsync(ITenantScope scope, string respCenter, CancellationToken ct = default);
    Task<List<OrderInfo>> GetProcurementOrdersInfoAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    /// <summary>Ported from Live <c>Db.Production.ProcurementOrderLinesDispatch(FetchParams)</c> — single param bag.</summary>
    Task<List<OrderLineDispatch>> GetProcurementOrderLinesDispatchAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);

    Task<List<OrderLineDispatch>> GetProcurementOrderLinesDispatchAsync(ITenantScope scope,
        string? view,
        string? type,
        string? entityCode,
        string[] nos,
        string[] respCenters,
        string? userSpecialToken = null,
        CancellationToken ct = default);
    Task<List<OrderLineDispatch>> GetProcurementOrderLinesNewNumberingAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<OrderLine>> GetProcurementOrderLinesAsync(ITenantScope scope, OrderInfo param, CancellationToken ct = default);
    Task<List<DispatchOrder>> GetProcurementDispatchOrdersAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<string> NewProcurementOrderAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<string>> GetProcMarketsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<string> NewProcShipNoAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<int> UpdateProcurementOrderAsync(ITenantScope scope, OrderInfo order, CancellationToken ct = default);
    Task<string> GenerateGRAsAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<int> InsertProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default);
    Task<int> UpdateProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default);
    Task<int> UpdateProcOrdLineDispatchAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default);
    Task<int> UpdateProcOrdLineDispatchSingleAsync(ITenantScope scope, OrderLineDispatch line, CancellationToken ct = default);
    Task<int> UpdateProcOrdLineReceiptAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default);
    Task<int> UpdateProcOrdLineRemoveAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default);
    Task<int> UpdateProcOrdLineDropAsync(ITenantScope scope, List<OrderLineDispatch> lines, CancellationToken ct = default);
    Task<int> DeleteProcurementOrderLineAsync(ITenantScope scope, OrderLine order, CancellationToken ct = default);
    Task<int> DeleteProcurementOrderAsync(ITenantScope scope, OrderInfo order, CancellationToken ct = default);
    Task<List<Tile>> GetEcomileProcurementTilesAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
    Task<List<ShipmentInfo>> GetShipmentOrderForMergerAsync(ITenantScope scope, FetchParams param, CancellationToken ct = default);
}
