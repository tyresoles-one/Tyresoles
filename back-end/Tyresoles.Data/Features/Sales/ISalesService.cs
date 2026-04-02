using Dataverse.NavLive;
using Tyresoles.Data.Features.Common;
using Tyresoles.Sql.Abstractions;
using NavLiveVendor = Dataverse.NavLive.Vendor;
using NavLiveVehicles = Dataverse.NavLive.Vehicles;

namespace Tyresoles.Data.Features.Sales;

/// <summary>
/// Sales-scoped query service for my customers, dealers, areas, regions, and zones
/// with GraphQL-friendly IQueryable return types (projection, sorting, filtering, paging).
/// </summary>
public interface ISalesService
{
    /// <summary>
    /// Returns a list of entity balances (Code, Balance) from Detailed Cust. Ledger Entry.
    /// Customer / Partner: one item keyed by entityCode.
    /// PartnerGroup: one item per dealer in the group (grouped by DealerCode).
    /// </summary>
    Task<List<EntityBalance>> GetMyBalanceAsync(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? respCenter = null,
        CancellationToken ct = default);

    

    /// <summary>
    /// Returns a paged query of latest account transactions (Date, Type, DocumentNo, Amount, CustomerNo)
    /// from Detailed Cust. Ledger Entry. Supported entityType: Partner, PartnerGroup (same scoping as GetMyBalance).
    /// Ordered by posting date descending.
    /// </summary>
    IQueryable<AccountTransaction> GetMyTransactionsQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? respCenter = null);

    /// <summary>
    /// Returns a query for customers scoped by entity type/code/department.
    /// For Partner/PartnerGroup, filters by user dealer code (entityCode).
    /// </summary>
    IQueryable<Customer> GetMyCustomersQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null,
        string? dealerCode = null);

    /// <summary>
    /// Returns a query for dealers (SalespersonPurchaser) scoped by entity type/code/department.
    /// For Partner/PartnerGroup, filters by Code or Group.
    /// For Employee with a non-sales department (e.g. Administration), does not narrow by <paramref name="respCenter"/>
    /// so dealer search is not limited to that responsibility center before GraphQL text filters.
    /// </summary>
    IQueryable<SalespersonPurchaser> GetMyDealersQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null);

    /// <summary>
    /// Returns a query for areas. Optional responsibilityCenter filters Area by Responsibility Center.
    /// Partner/PartnerGroup: areas from customers' Area Code (Dealer Code = entityCode).
    /// Employee: areas whose Team is in TeamSalesperson.TeamCode for Code = entityCode.
    /// </summary>
    IQueryable<Area> GetMyAreasQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null);

    /// <summary>
    /// Returns a query for regions. Not implemented until Region entity exists (next phase).
    /// </summary>
    IQueryable<Territory> GetMyRegionsQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null);

    /// <summary>
    /// Returns a Dealer.
    /// </summary>
    SalespersonPurchaser GetDealerQuery(ITenantScope scope, string code, CancellationToken ct = default);

    /// <summary>
    /// Saves/updates dealer master: calls NAV Web Service <c>CreateDealer</c> (same as legacy
    /// <c>UpdateDealerRecord</c> → <c>Connector.CreateDealer</c>), then SQL-updates <c>Salesperson_Purchaser</c>
    /// for the full field set (including mobile and status, which are not on the SOAP signature).
    /// </summary>
    Task SaveDealerAsync(ITenantScope scope, SaveDealerInput input, CancellationToken ct = default);

    /// <summary>
    /// Creates a <see cref="SalespersonPurchaser"/> from a customer with no dealer code, using derived codes
    /// <c>LEFT(No,4)+RIGHT(No,5)</c> or <c>LEFT(No,4)+RIGHT(No,6)</c> when the preferred code is not already taken,
    /// then sets <see cref="Customer.DealerCode"/>.
    /// </summary>
    Task<CreateDealerResult> CreateDealerAsync(ITenantScope scope, string customerNo, CancellationToken ct = default);

    /// <summary>
    /// Returns a query for responsibility centers scoped by user and type.
    /// </summary>
    IQueryable<ResponsibilityCenter> GetMyRespCentersQuery(
        ITenantScope scope,
        string userid,
        string type);

    /// <summary>
    /// Returns a query for vehicles (transporters) scoped by entity type/code/department.
    /// Partner: vehicles linked through customers whose Dealer Code = entityCode.
    /// PartnerGroup: vehicles linked through dealers in the group.
    /// Employee (Sales): vehicles linked through team → area → customer chain.
    /// No entityType / null: returns all vehicles optionally filtered by respCenter.
    /// </summary>
    IQueryable<NavLiveVehicles> GetMyVehiclesQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null);

    /// <summary>
    /// Lists image rows for <paramref name="documentNo"/> (e.g. dealer code) and NAV <c>Doc_ Type</c>.
    /// </summary>
    Task<IReadOnlyList<DealerDocumentImageDto>> GetDealerDocumentImagesAsync(
        ITenantScope scope,
        string documentNo,
        int docType,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads one or more images via NAV SOAP <c>AddUpdateImage</c> (legacy <c>UpdateDocumentImage</c>).
    /// Line numbers are assigned after existing max line for the document/type.
    /// </summary>
    Task UploadDealerDocumentImagesAsync(
        ITenantScope scope,
        string documentNo,
        int docType,
        IReadOnlyList<string> imageBase64Payloads,
        CancellationToken ct = default);
}
