using System.Collections.Generic;
using System.Security.Claims;
using Dataverse.NavLive;
using HotChocolate;
using HotChocolate.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Tyresoles.Data;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Data.Features.Admin.User;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Calendar.Entities;
using Tyresoles.Data.Features.Sales;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Sales.Reports.Models;
using Tyresoles.Data.Features.Purchase;
using Tyresoles.Data.Features.Procurement;
using Tyresoles.Data.Features.Production;
using Tyresoles.Data.Features.Production.Models;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Features.Accounts;
using Tyresoles.Data.Features.Accounts.Models;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;
using Tyresoles.Web.GraphQL;
using Tyresoles.Web.Features.VpnInstaller;
using ProductionFetchParams = Tyresoles.Data.Features.Production.Models.FetchParams;
using Vendor = Dataverse.NavLive.Vendor;


namespace Tyresoles.Web;

public class Query
{
    public string Version => "1.0";

    /// <summary>Get Drive Sync user configuration for the specified user or the caller.</summary>
    [Authorize]
    [GraphQLName("getDriveSyncConfig")]
    public async Task<Tyresoles.Data.Features.DriveSync.Entities.DriveSyncUserConfig?> GetDriveSyncConfig(
        string? targetUserId,
        [Service] Tyresoles.Data.Features.DriveSync.IDriveSyncService syncService,
        [Service] Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var callerUserId = httpContextAccessor.HttpContext?.User?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        
        // Admins pass targetUserId, otherwise fetch own config
        var uid = string.IsNullOrEmpty(targetUserId) ? callerUserId : targetUserId;
        if (string.IsNullOrEmpty(uid)) return null;

        return await syncService.GetUserConfigAsync(uid, cancellationToken);
    }

    /// <summary>Get profile for a user by userId (UserName or MobileNo). Returns null if not found. Requires authentication.</summary>
    [Authorize]
    public async Task<ProfileResult?> GetProfile(
        string userId,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        return await userService.GetProfileAsync(userId, cancellationToken);
    }

    public async Task<List<Tyresoles.Data.Features.Protean.EInvoiceCandidate>> GetSalesInvLinesForInvoiceAsync([Service] IProteanService proteanService, [Service] IDataverseDataService dataService, CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        return await proteanService.GetSalesLinesForEInvoiceAsync(scope, cancellationToken);
    }
     /// <summary>Search app users by name or username for the attendee picker. Returns userId, fullName, userType, avatar. Requires authentication.</summary>
    [GraphQLName("searchUsers")]
    [Authorize]
    public async Task<IReadOnlyList<UserSearchResult>> SearchUsers(
        string? search,
        int? take,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        return await userService.SearchUsersAsync(search, take ?? 20, cancellationToken);
    }

    /// <summary>List active sessions. Optional filter by userId. Requires authentication.</summary>
    [Authorize]
    public async Task<IReadOnlyList<SessionInfo>> GetSessions(
        string? userId,
        [Service] ISessionStore sessionStore,
        CancellationToken cancellationToken = default)
    {
        return await sessionStore.ListAsync(userId, cancellationToken);
    }

    /// <summary>
    /// List of entity balances (Code, Balance) from Detailed Cust. Ledger Entry for the caller's scope.
    /// Customer/Partner: one item keyed by entityCode. PartnerGroup: one item per dealer in the group.
    /// </summary>
    [Authorize]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Sales.EntityBalance>> GetMyBalance(
        string? entityType,
        string? entityCode,
        string? respCenter,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await salesService.GetMyBalanceAsync(scope, entityType, entityCode, respCenter, cancellationToken);
    }

    /// <summary>Latest account transactions (Date, Type, DocumentNo, Amount, CustomerNo) from Detailed Cust. Ledger Entry. EntityType: Partner or PartnerGroup only. Fully paged, ordered by date descending.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<AccountTransaction> GetMyTransactions(
        string? entityType,
        string? entityCode,
        string? respCenter,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyTransactionsQuery(scope, entityType, entityCode, respCenter);
    }

    /// <summary>My customers scoped by entity type/code/department. Partner/PartnerGroup filtered by dealer code.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Customer> GetMyCustomers(
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter,
        string? dealerCode,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyCustomersQuery(scope, entityType, entityCode, department, respCenter, dealerCode);
    }

    /// <summary>My dealers (SalespersonPurchaser) scoped by entity type/code/department.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<SalespersonPurchaser> GetMyDealers(
        string? entityType,
        string? entityCode,
        string? department,
        IReadOnlyList<string>? respCenters,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyDealersQuery(scope, entityType, entityCode, department, respCenters);
    }

    /// <summary>My areas. Optional respCenters filter by Responsibility Center (union). Employee: areas by Team (Team Salesperson). Partner: areas from customers' Area Code (Dealer Code).</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Area> GetMyAreas(
        string? entityType,
        string? entityCode,
        string? department,
        IReadOnlyList<string>? respCenters,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyAreasQuery(scope, entityType, entityCode, department, respCenters);
    }
    /// <summary>My regions (Territery) scoped by entity type/code/department.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Territory> GetMyRegions(
        string? entityType,
        string? entityCode,
        string? department,
        IReadOnlyList<string>? respCenters,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyRegionsQuery(scope, entityType, entityCode, department, respCenters);
    }

    /// <summary>Get a single dealer by code.</summary>
    [Authorize]
    [GraphQLName("dealerByCode")]
    public SalespersonPurchaser GetDealerByCode(
        string code,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetDealerQuery(scope, code, cancellationToken);
    }

    /// <summary>Get a single dealer by code.</summary>
    [Authorize]
    [GraphQLName("vendorByCode")]
    public Vendor GetVendorByCode(
        string code,
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.VendorByCode(scope,code, cancellationToken);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ResponsibilityCenter> GetMyRespCenters(
        string type,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Query> logger)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);

        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";

        logger.LogInformation("Fetch myRespCenters for user: {UserId}, type: {Type}", userId, type);

        return salesService.GetMyRespCentersQuery(scope, userId, type);
    }

    /// <summary>Get report metadata for sales category. Optional comma-separated report codes filter.</summary>
    [Authorize]
    public async Task<List<ReportMeta>> GetReportSalesMeta(
        string? reports,
        [Service] IDataverseDataService dataService,
        [Service] ISalesReportService salesReportService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        var list = await salesReportService.GetReportMetaAsync(scope, reports, cancellationToken);
        return list ?? new List<ReportMeta>();
    }

    /// <summary>Vehicles (transporters) scoped by entity type/code/department. Active/inactive status included as raw int.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 200)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.Vehicles> GetMyVehicles(
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter,
        [Service] IDataverseDataService dataService,
        [Service] ISalesService salesService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return salesService.GetMyVehiclesQuery(scope, entityType, entityCode, department, respCenter);
    }

    /// <summary>
    /// Admin fetch for all users in the system.
    /// Hot Chocolate: projection, filtering, sorting, and offset paging.
    /// </summary>
    /// <summary>
    /// Admin fetch for all users in the system.
    /// Hot Chocolate: projection, filtering, sorting, and offset paging.
    /// </summary>
    [Authorize]
    [UseOffsetPaging(IncludeTotalCount = true, MaxPageSize = 200)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.User> GetUsers(
        bool? duplicateMobileOnly,
        [Service] IDataverseDataService dataService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        var query = scope.Query<Dataverse.NavLive.User>();
        
        if (duplicateMobileOnly == true)
        {
            // Filter users who share a mobile number with at least one other user.
            // Using a raw SQL exists condition for performance on non-indexed mobile columns if needed,
            // but IQuery.WhereRaw is cleaner here.
            query = query.Where("EXISTS (SELECT 1 FROM " + scope.GetQualifiedTableName("User", isShared: true) + " u2 WHERE u2.[Mobile No_] = t0.[Mobile No_] AND u2.[User Security ID] <> t0.[User Security ID])");
        }
        
        return query.AsQueryable(scope);
    }

    /// <summary>Fetch a single user profile with extended info.</summary>
    [Authorize]
    public async Task<ProfileResult?> GetUserDetail(
        string userId,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        return await userService.GetProfileAsync(userId, cancellationToken);
    }

    /// <summary>Paged responsibility centers for lookups.</summary>
    [Authorize]
    [UseOffsetPaging(IncludeTotalCount = true, MaxPageSize = 2000)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.ResponsibilityCenter> GetResponsibilityCenters(
        [Service] IDataverseDataService dataService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForNavLive();
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return scope.Query<Dataverse.NavLive.ResponsibilityCenter>().AsQueryable(scope);
    }

    /// <summary>Paged permission sets for assignment.</summary>
    [Authorize]
    [UseOffsetPaging(IncludeTotalCount = true, MaxPageSize = 2000)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.PermissionSet> GetPermissionSets(
        [Service] IDataverseDataService dataService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForNavLive();
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return scope.Query<Dataverse.NavLive.PermissionSet>().AsQueryable(scope);
    }

    /// <summary>Paged employee list for user linking.</summary>
    [Authorize]
    [UseOffsetPaging(IncludeTotalCount = true, MaxPageSize = 2000)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.Employee> GetEmployees(
        [Service] IDataverseDataService dataService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForNavLive();
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return scope.Query<Dataverse.NavLive.Employee>().AsQueryable(scope);
    }

    /// <summary>Fetch reports by category for permission mapping.</summary>
    [Authorize]
    public async Task<List<ReportMeta>> GetReportsByCategory(
        string category,
        [Service] IDataverseDataService dataService,
        [Service] ISalesReportService salesReportService,
        [Service] IProductionReportService productionReportService,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForNavLive();
        if (category == "sales")
            return await salesReportService.GetReportMetaAsync(scope, null, cancellationToken);
        if (category == "production")
            return await productionReportService.GetReportMetaAsync(scope, null, cancellationToken);
        
        return new List<ReportMeta>();
    }

   
    /// <summary>
    /// NAV Post Code master (PIN, city, state, country). Source: <see cref="ICommonDataService.GetPostCodesQuery"/>.
    /// Hot Chocolate: projection, filtering, sorting, and cursor paging (Relay connection).
    /// </summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 200)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.PostCode> GetPostCodes(
        [Service] IDataverseDataService dataService,
        [Service] ICommonDataService commonService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return commonService.GetPostCodesQuery(scope);
    }

    /// <summary>
    /// NAV State master (code, description). Source: <see cref="ICommonDataService.GetStatesQuery"/> (same data as <c>GetStateAsync</c>).
    /// Hot Chocolate: projection, filtering, sorting, and cursor paging.
    /// </summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 200)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.State> GetStates(
        [Service] IDataverseDataService dataService,
        [Service] ICommonDataService commonService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return commonService.GetStatesQuery(scope);
    }

    /// <summary>My vendors scoped by responsibility centers and group categories. Fully compatible with GraphQL projection, filters, sorting, and paging.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.Vendor> GetMyVendors(
        string? respCenter,
        string[]? categories,
        string? ecoMgr,
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.MyVendors(scope, respCenter, categories, ecoMgr);
    }

    /// <summary>Item / casing numbers for procurement (legacy Db.Production.ItemNos). Paged, filterable, sortable.</summary>
    /// <remarks>Do not add Hot Chocolate <c>[UseProjection]</c> here: it re-projects <c>CasingItem</c> using property names as SQL columns,
    /// so <c>MinRate</c>/<c>MaxRate</c> become invalid on Group Details; the provider must keep the Nav mapping (<c>Value</c>, <c>Extra Value</c>).</remarks>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 200)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<CasingItem> GetPurchaseItemNos(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.ItemNos(scope, param);
    }

    /// <summary>My Procurement Orders. Fully compatible with GraphQL projection, filters, sorting, and paging.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.PurchaseHeader> GetProcurementOrders(
        string? respCenters,
        string? userCode,
        string? userDepartment,
        string? userSpecialToken,
        int? statusFilter,
        [Service] IDataverseDataService dataService,
        [Service] IProcurementService procurementService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return procurementService.ProcurementOrders(scope, respCenters, userCode, userDepartment, userSpecialToken, statusFilter);
    }

    /// <summary>My Procurement Order Lines. Fully compatible with GraphQL projection, filters, sorting, and paging.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Dataverse.NavLive.PurchaseLine> GetProcurementOrderLines(
        string? respCenters,
        string? userCode,
        string? userDepartment,
        string? userSpecialToken,
        int? statusFilter,
        [Service] IDataverseDataService dataService,
        [Service] IProcurementService procurementService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return procurementService.ProcurementOrderLines(scope, respCenters, userCode, userDepartment, userSpecialToken, statusFilter);
    }

    /// <summary>My calendar events in date range. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getMyCalendarEvents")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>> GetMyCalendarEvents(
        DateTime fromUtc,
        DateTime toUtc,
        EventTagType? tagType = null,
        string? tagKey = null,
        int? skip = null,
        int? take = null,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>();
        return await calendarService.GetMyEventsAsync(userId, fromUtc, toUtc, tagType, tagKey, true, skip, take, cancellationToken);
    }

    /// <summary>Get a single calendar event by id. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getCalendarEventById")]
    public async Task<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto?> GetCalendarEventById(
        Guid eventId,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return null;
        return await calendarService.GetEventByIdAsync(eventId, userId, cancellationToken);
    }

    /// <summary>Event types for calendar (Meeting, Call, Visit, etc.). Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getEventTypes")]
    public async Task<IReadOnlyList<EventTypeDto>> GetEventTypes(
        [Service] ICalendarService calendarService = null!,
        CancellationToken cancellationToken = default)
    {
        return await calendarService.GetEventTypesAsync(cancellationToken);
    }

    /// <summary>Upcoming reminders for the current user. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getUpcomingReminders")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>> GetUpcomingReminders(
        DateTime? untilUtc,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>();
        var until = untilUtc ?? DateTime.UtcNow.AddHours(24);
        return await calendarService.GetUpcomingRemindersAsync(userId, until, cancellationToken);
    }

    /// <summary>Calendars I have shared with others. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getSharedCalendars")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.CalendarShareDto>> GetSharedCalendars(
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.Calendar.Dto.CalendarShareDto>();
        return await calendarService.GetSharedCalendarsAsync(userId, cancellationToken);
    }

    /// <summary>My notification preference for reminders. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getNotificationPreference")]
    public async Task<Tyresoles.Data.Features.Calendar.NotificationPreferenceDto?> GetNotificationPreference(
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return null;
        return await calendarService.GetNotificationPreferenceAsync(userId, cancellationToken);
    }

    /// <summary>Free/busy slots for given user IDs in date range. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getFreeBusy")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.FreeBusyDto>> GetFreeBusy(
        IReadOnlyList<string> userIds,
        DateTime fromUtc,
        DateTime toUtc,
        [Service] ICalendarService calendarService = null!,
        CancellationToken cancellationToken = default)
    {
        return await calendarService.GetFreeBusyAsync(userIds, fromUtc, toUtc, cancellationToken);
    }

    /// <summary>Audit log for calendar events. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getCalendarAuditLog")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.CalendarAuditLogEntryDto>> GetCalendarAuditLog(
        Guid? eventId,
        string? userId,
        int limit = 50,
        [Service] ICalendarService calendarService = null!,
        CancellationToken cancellationToken = default)
    {
        return await calendarService.GetCalendarAuditLogAsync(eventId, userId, limit, cancellationToken);
    }

    /// <summary>Events that conflict with the given time range for the current user. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getCalendarConflicts")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>> GetCalendarConflicts(
        DateTime startUtc,
        DateTime endUtc,
        Guid? excludeEventId,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.Calendar.Dto.CalendarEventDto>();
        return await calendarService.GetConflictsAsync(userId, startUtc, endUtc, excludeEventId, cancellationToken);
    }

    /// <summary>Export calendar as ICS (iCalendar) for the given range. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("exportCalendarIcs")]
    public async Task<string> ExportCalendarIcs(
        DateTime fromUtc,
        DateTime toUtc,
        [Service] ICalendarService calendarService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
        if (string.IsNullOrEmpty(userId)) return "";
        return await calendarService.ExportIcsAsync(userId, fromUtc, toUtc, cancellationToken);
    }

    /// <summary>Get recent notifications for the current user. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getNotifications")]
    public async Task<NotificationFeedResult> GetNotifications(
        int limit = 50,
        [Service] INotificationService notificationService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        [Service] ILogger<Query> logger = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
            if (string.IsNullOrEmpty(userId))
                return new NotificationFeedResult { Notifications = Array.Empty<Notification>(), ServerTimeUtc = DateTime.UtcNow };
            var items = await notificationService.GetMyNotificationsAsync(userId, limit, cancellationToken);
            return new NotificationFeedResult
            {
                Notifications = items,
                ServerTimeUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetNotifications query");
            throw;
        }
    }

    /// <summary>Get count of unread notifications for the current user. Requires authentication.</summary>
    [Authorize]
    [GraphQLName("getUnreadNotificationCount")]
    public async Task<int> GetUnreadNotificationCount(
        [Service] INotificationService notificationService = null!,
        [Service] IHttpContextAccessor httpContextAccessor = null!,
        [Service] ILogger<Query> logger = null!,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
            if (string.IsNullOrEmpty(userId)) return 0;
            return await notificationService.GetUnreadCountAsync(userId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetUnreadNotificationCount query");
            throw;
        }
    }

    /// <summary>
    /// NAV G/L Account master. Source: <see cref="IAccountService.GetGLAccounts"/>.
    /// Hot Chocolate: projection, filtering, sorting, and cursor paging.
    /// </summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<GLAccount> GetGLAccounts(
        [Service] IDataverseDataService dataService,
        [Service] IAccountService accountService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return accountService.GetGLAccounts(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.UnitOfMeasure> GetUnitOfMeasures(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetUnitOfMeasures(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.ItemCategory> GetItemCategories(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetItemCategories(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.ProductGroup> GetProductGroups(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetProductGroups(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.GenProductPostingGroup> GetGenProductPostingGroups(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetGenProductPostingGroups(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.GSTGroup> GetGSTGroups(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetGSTGroups(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.HsnSac> GetHsnSacs(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetHsnSacs(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.InventoryPostingGroup> GetInventoryPostingGroups(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetInventoryPostingGroups(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.Item> GetItems(
        [Service] IDataverseDataService dataService,
        [Service] IPurchaseService purchaseService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return purchaseService.GetItems(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.FAClass> GetFAClasses(
        [Service] IDataverseDataService dataService,
        [Service] IFixedAssetService fixedAssetService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return fixedAssetService.GetFAClasses(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.FASubclass> GetFASubclasses(
        [Service] IDataverseDataService dataService,
        [Service] IFixedAssetService fixedAssetService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return fixedAssetService.GetFASubclasses(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.FixedAsset> GetFixedAssets(
        [Service] IDataverseDataService dataService,
        [Service] IFixedAssetService fixedAssetService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return fixedAssetService.GetFixedAssets(scope);
    }

    [Authorize]
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 250)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Tyresoles.Data.Features.Purchase.Models.FixedAssetServiceLog> GetFixedAssetServiceLogs(
        [Service] IDataverseDataService dataService,
        [Service] IFixedAssetService fixedAssetService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return fixedAssetService.GetFixedAssetServiceLogs(scope);
    }
    
    /// <summary>
    /// Fetch documents (Invoices, Credit Notes, Claims) based on parameters.
    /// Partially integrated with comprehensive user context filtering.
    /// </summary>
    [Authorize]
    [GraphQLName("getMyDocuments")]
    public async Task<DocumentDto[]> GetMyDocuments(
        SalesReportParams parameters,
        [Service] IDataverseDataService dataService,
        [Service] ISalesReportService salesService,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ILogger<Query> logger,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        try
        {
            return await salesService.GetMyDocuments(scope, parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in getMyDocuments query");
            throw;
        }
    }

    /// <summary>Get group categories by type and optional comma-separated resp centers. Requires authentication.</summary>
    [Authorize]
    public async Task<IReadOnlyList<GroupCategory>> GetGroupCategories(
        int type,
        string? respCenters,
        [Service] ICommonDataService commonService,
        CancellationToken cancellationToken = default)
    {
        return await commonService.GetGroupCategoriesAsync(type, respCenters, cancellationToken);
    }

    /// <summary>Get group details by category and optional comma-separated codes. Requires authentication.</summary>
    [Authorize]
    public async Task<IReadOnlyList<GroupDetails>> GetGroupDetails(
        string category,
        string? codes,
        [Service] ICommonDataService commonService,
        CancellationToken cancellationToken = default)
    {
        return await commonService.GetGroupDetailsAsync(category, codes, cancellationToken);
    }

    /// <summary>VPN installer download metadata for the Tauri desktop client (URL from server config). Requires authentication.</summary>
    [Authorize]
    [GraphQLName("vpnInstallerConfig")]
    public VpnInstallerConfig GetVpnInstallerConfig(
        [Service] IOptionsSnapshot<VpnInstallerOptions> options)
    {
        var o = options.Value;
        return new VpnInstallerConfig
        {
            DownloadUrl = o.DownloadUrl ?? "",
            Sha256Hex = o.Sha256Hex,
            FileName = o.FileName,
            IsZipArchive = o.IsZipArchive,
            ZipEntryName = o.ZipEntryName,
        };
    }

    /// <summary>Get user details including RDP password and NAV config name by username. Requires authentication.</summary>
    [Authorize]
    public async Task<UserDetail?> GetUser(
        string username,
        [Service] IUserService userService,
        CancellationToken cancellationToken = default)
    {
        return await userService.GetUserAsync(username, cancellationToken);
    }

    /// <summary>Get casing items. Ported from Db.Production.ItemNos.</summary>
    [Authorize]
    public async Task<List<CasingItem>> GetProductionItemNos(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetItemNosAsync(scope, param, cancellationToken);
    }


    /// <summary>Get makes. Ported from Db.Production.Makes.</summary>
    [Authorize]
    public async Task<List<CodeName>> GetProductionMakes(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetMakesAsync(scope, param, cancellationToken);
    }

    /// <summary>Get sub-makes. Ported from Db.Production.MakeSubMake.</summary>
    [Authorize]
    public async Task<List<CodeName>> GetProductionMakeSubMake(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetMakeSubMakeAsync(scope, param, cancellationToken);
    }

    /// <summary>Get vendors code names. Ported from Db.Production.VendorsCodeNames.</summary>
    [Authorize]
    public async Task<List<CodeName>> GetProductionVendorsCodeNames(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetVendorsCodeNamesAsync(scope, param, cancellationToken);
    }

    /// <summary>Get inspector code names. Ported from Db.Production.InspectorCodeNames.</summary>
    [Authorize]
    public async Task<List<CodeName>> GetProductionInspectorCodeNames(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetInspectorCodeNamesAsync(scope, param, cancellationToken);
    }

    /// <summary>Get procurement inspection options. Ported from Db.Production.ProcurementInspection.</summary>
    public List<CodeName> GetProductionProcurementInspection(Tyresoles.Data.Features.Production.Models.FetchParams param, [Service] IProductionService productionService)
    {
        return productionService.GetProcurementInspection(param);
    }

    /// <summary>Get procurement markets. Ported from Db.Production.ProcurementMarkets.</summary>
    [Authorize]
    public async Task<List<CodeName>> GetProductionProcurementMarkets(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementMarketsAsync(scope, param, cancellationToken);
    }

    /// <summary>Get vendors with balance. Ported from Db.Production.Vendors.</summary>
    [Authorize]
    public async Task<List<VendorModel>> GetProductionVendors(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetVendorsAsync(scope, param, cancellationToken);
    }

    /// <summary>Get ecomile last numbering. Ported from Db.Production.EcomileLastNewNumber.</summary>
    [Authorize]
    public async Task<string> GetProductionEcomileLastNewNumber(
        string respCenter,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetEcomileLastNewNumberAsync(scope, respCenter, cancellationToken);
    }

    /// <summary>Get procurement orders info. Ported from Db.Production.ProcurementOrdersInfo.</summary>
    [Authorize]
    public async Task<List<OrderInfo>> GetProductionProcurementOrdersInfo(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementOrdersInfoAsync(scope, param, cancellationToken);
    }

    /// <summary>
    /// Get procurement order lines for dispatch workflows. Ported from Live <c>Db.Production.ProcurementOrderLinesDispatch(FetchParams)</c>.
    /// GraphQL: <c>productionProcurementOrderLinesDispatch(param: FetchParamsInput!)</c>.
    /// </summary>
    [Authorize]
    public async Task<List<OrderLineDispatch>> GetProductionProcurementOrderLinesDispatch(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementOrderLinesDispatchAsync(scope, param, cancellationToken);
    }

    /// <summary>Get procurement order lines for numbering. Ported from Db.Production.ProcurementOrderLinesNewNumbering.</summary>
    [Authorize]
    public async Task<List<OrderLineDispatch>> GetProductionProcurementOrderLinesNewNumbering(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementOrderLinesNewNumberingAsync(scope, param, cancellationToken);
    }

    /// <summary>Get procurement order lines. Ported from <c>Tyresoles.One.Data.Navision.Db.Production.ProcurementOrderLines</c> (<c>Db.Production.cs</c>).</summary>
    [Authorize]
    public async Task<List<OrderLine>> GetProductionProcurementOrderLines(
        OrderInfo param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementOrderLinesAsync(scope, param, cancellationToken);
    }

    /// <summary>Get dispatch orders. Ported from Db.Production.ProcurementDispatchOrders.</summary>
    [Authorize]
    public async Task<List<DispatchOrder>> GetProductionProcurementDispatchOrders(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcurementDispatchOrdersAsync(scope, param, cancellationToken);
    }

    /// <summary>Get proc markets. Ported from Db.Production.ProcMarkets.</summary>
    [Authorize]
    public async Task<List<string>> GetProductionProcMarkets(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetProcMarketsAsync(scope, param, cancellationToken);
    }

    /// <summary>Get ecomile procurement tiles. Ported from Db.Production.GetEcomileProcurementTiles.</summary>
    [Authorize]
    public async Task<List<Tile>> GetProductionEcomileProcurementTiles(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetEcomileProcurementTilesAsync(scope, param, cancellationToken);
    }

    /// <summary>Get shipment order for merger. Ported from Db.Production.ShipmentOrderForMerger.</summary>
    [Authorize]
    public async Task<List<ShipmentInfo>> GetProductionShipmentOrderForMerger(
        ProductionFetchParams param,
        [Service] IDataverseDataService dataService,
        [Service] IProductionService productionService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var scope = dataService.ForTenant("NavLive");
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return await productionService.GetShipmentOrderForMergerAsync(scope, param, cancellationToken);
    }
    [Authorize]
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    [GraphQLName("procurementNewNumberingPaged")]
    public IQueryable<ProcurementNewNumberingDto> GetProcurementNewNumberingPaged(
        DateTime? fromDate,
        DateTime? toDate,
        string? respCenters,
        string? view,
        string? type,
        string[]? nos,
        [Service] IDataverseDataService dataService,
        [Service] IProcurementService procurementService,
        [Service] IHttpContextAccessor httpContextAccessor,
        ClaimsPrincipal claimsPrincipal)
    {
        var scope = dataService.ForNavLive();
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);

        var userCode = claimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userSpecialToken = claimsPrincipal.FindFirst("SpecialToken")?.Value;

        return procurementService.ProcurementOrderLinesNewNumbering(
            scope, fromDate, toDate, respCenters, view, type, nos, userCode, userSpecialToken);
    }

    // ── Payroll ────────────────────────────────────────────────

    /// <summary>Paged, filterable, sortable list of NAV Employees for payroll views.</summary>
    [Authorize]
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    [GraphQLName("payrollEmployees")]
    public IQueryable<Dataverse.NavLive.Employee> GetPayrollEmployees(
        Tyresoles.Data.Features.Payroll.ReportFetchParam param,
        [Service] IDataverseDataService dataService,
        [Service] Tyresoles.Data.Features.Payroll.IPayrollService payrollService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var scope = dataService.ForNavLive();
        httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
        return payrollService.GetEmployees(scope, param);
    }

    // ── Navision Edit Requests ─────────────────────────────────

    /// <summary>Get all request types (templates). Admin sees all, users see active only.</summary>
    [Authorize]
    [GraphQLName("navEditRequestTypes")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.NavisionEdits.Entities.NavEditRequestType>> GetNavEditRequestTypes(
        bool? activeOnly,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        return await navEditService.GetRequestTypesAsync(activeOnly ?? true, cancellationToken);
    }

    /// <summary>Get a single request type by ID.</summary>
    [Authorize]
    [GraphQLName("navEditRequestTypeById")]
    public async Task<Tyresoles.Data.Features.NavisionEdits.Entities.NavEditRequestType?> GetNavEditRequestTypeById(
        int id,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        return await navEditService.GetRequestTypeByIdAsync(id, cancellationToken);
    }

    /// <summary>Dynamic record lookup from Nav. Returns dictionaries of column→value.</summary>
    [Authorize]
    [GraphQLName("navEditLookupRecords")]
    public async Task<List<List<Tyresoles.Data.Features.NavisionEdits.KeyValueItem>>> NavEditLookupRecords(
        int requestTypeId,
        string? search,
        int? take,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        var rows = await navEditService.LookupRecordsAsync(requestTypeId, search, take ?? 20, cancellationToken);
        return rows.Select(r => r.Select(kv => new Tyresoles.Data.Features.NavisionEdits.KeyValueItem { Key = kv.Key, Value = kv.Value?.ToString() }).ToList()).ToList();
    }

    /// <summary>Fetch a single Nav record by its primary key.</summary>
    [Authorize]
    [GraphQLName("navEditGetRecord")]
    public async Task<List<Tyresoles.Data.Features.NavisionEdits.KeyValueItem>?> NavEditGetRecord(
        int requestTypeId,
        string recordKey,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        var row = await navEditService.GetRecordByKeyAsync(requestTypeId, recordKey, cancellationToken);
        return row?.Select(kv => new Tyresoles.Data.Features.NavisionEdits.KeyValueItem { Key = kv.Key, Value = kv.Value?.ToString() }).ToList();
    }

    /// <summary>Get my edit requests (for current user).</summary>
    [Authorize]
    [GraphQLName("navEditMyRequests")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto>> GetNavEditMyRequests(
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto>();
        return await navEditService.GetMyRequestsAsync(userId, cancellationToken);
    }

    /// <summary>Get all edit requests (admin only). Optional status filter.</summary>
    [Authorize]
    [GraphQLName("navEditAllRequests")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto>> GetNavEditAllRequests(
        int? statusFilter,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        Tyresoles.Data.Features.NavisionEdits.Entities.NavEditStatus? filter = statusFilter.HasValue
            ? (Tyresoles.Data.Features.NavisionEdits.Entities.NavEditStatus)statusFilter.Value
            : null;
        return await navEditService.GetAllRequestsAsync(filter, cancellationToken);
    }

    /// <summary>Get pending approvals for the current user.</summary>
    [Authorize]
    [GraphQLName("navEditPendingApprovals")]
    public async Task<IReadOnlyList<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto>> GetNavEditPendingApprovals(
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrEmpty(userId)) return Array.Empty<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto>();
        return await navEditService.GetPendingApprovalsAsync(userId, cancellationToken);
    }

    /// <summary>Get a single edit request by ID.</summary>
    [Authorize]
    [GraphQLName("navEditRequestById")]
    public async Task<Tyresoles.Data.Features.NavisionEdits.NavEditRequestDto?> GetNavEditRequestById(
        Guid requestId,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        return await navEditService.GetRequestByIdAsync(requestId, cancellationToken);
    }

    /// <summary>Nav Live table names (INFORMATION_SCHEMA) for Navision Edits template designer.</summary>
    [Authorize]
    [GraphQLName("navEditNavTables")]
    public async Task<IReadOnlyList<string>> GetNavEditNavTables(
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        return await navEditService.GetNavLiveTableNamesAsync(cancellationToken);
    }

    /// <summary>Column names for a Nav Live table (INFORMATION_SCHEMA).</summary>
    [Authorize]
    [GraphQLName("navEditNavTableColumns")]
    public async Task<IReadOnlyList<string>> GetNavEditNavTableColumns(
        string tableName,
        [Service] Tyresoles.Data.Features.NavisionEdits.INavEditService navEditService,
        CancellationToken cancellationToken = default)
    {
        return await navEditService.GetNavLiveColumnNamesForTableAsync(tableName, cancellationToken);
    }
}

