# Tyresoles Back-End Improvement Plan

> **Audit Date**: 2026-04-01  
> **Scope**: All projects under `back-end/` — Tyresoles.Web, Tyresoles.Data, Tyresoles.Sql, Tyresoles.Protean, Tyresoles.Easebuzz, Tyresoles.Logger, Tyresoles.Reporting, Tyresoles.Sql.Generators, Tyresoles.Sql.Cli  
> **Goal**: Optimal performance, memory & thread safety, eliminate redundancy, enforce clean architecture

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Critical Issues — Thread & Memory Safety](#2-critical-issues--thread--memory-safety)
3. [Performance Improvements](#3-performance-improvements)
4. [Repetitive / Duplicated Code](#4-repetitive--duplicated-code)
5. [Security Vulnerabilities](#5-security-vulnerabilities)
6. [Code Quality & Maintainability](#6-code-quality--maintainability)
7. [Proposed Solutions — Detailed](#7-proposed-solutions--detailed)
8. [Priority Matrix](#8-priority-matrix)

---

## 1. Architecture Overview

```
Tyresoles.Web (ASP.NET Core Host)
├── GraphQL (HotChocolate: Query.cs, Mutation.cs, Subscription)
├── Controllers (REST: ProteanController, ReportsController, DashboardController, etc.)
├── Auth (JWT token service)
└── Services (ReminderBackgroundService)

Tyresoles.Data (Business Logic / Data Access)
├── Features/
│   ├── Sales (SalesService, ProteanService, ProteanDataService, SalesReportService)
│   ├── Production (ProductionService, ProductionReportService)
│   ├── Procurement (ProcurementService)
│   ├── Purchase (PurchaseService)
│   ├── Protean (ProteanDataService — E-Invoice/E-Waybill)
│   ├── Calendar (CalendarService, CalendarDbContext — EF Core)
│   ├── Admin (UserService, SessionStore, Auth)
│   ├── Common (Connector — NAV SOAP, NavisionModels)
│   ├── Payroll (PayrollReportService)
│   └── Accounts (AccountsReportService)
├── Infrastructure (GlobalQueryCache, ScopedQueryCache)
└── DataverseDataService (tenant scope factory)

Tyresoles.Sql (Custom ORM / Query Engine)
├── Core (TenantScope, Query builder, Materialization)
├── Abstractions (ITenantScope, IDataverse, IQuery)
├── SqlServer (SQL Server dialect)
└── GraphQL (IQueryable bridge)

Tyresoles.Protean (GSP integration — E-Invoice/E-Waybill)
├── Services (EInvoiceService, EWaybillService)
├── Http (ProteanHttpClient)
├── Session (token management)
└── Models (request/response DTOs)

Tyresoles.Easebuzz (Payment gateway)
Tyresoles.Reporting (RDLC report engine)
Tyresoles.Logger (File-based logging provider)
Tyresoles.Sql.Generators (Source generators)
Tyresoles.Sql.Cli (CLI tooling)
```

---

## 2. Critical Issues — Thread & Memory Safety

### 2.1 `Parallel.ForEachAsync` with Shared Mutable Counter Variables

**Files**: `Tyresoles.Data/Features/Sales/ProteanDataService.cs` (lines 356, 518)

Both `RunEInvProcessAsync` and `RunEWBProcessAsync` use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism = 5` and share `int processed` / `int errors` counters. While `Interlocked.Increment` is used (correct), the overarching issue is:

- **ITenantScope lifetime**: Each parallel iteration creates `_dataverse.ForTenant(scope.TenantKey)` which is good, but some error-handler branches reuse the **outer** `scope` instead of `workScope` (line 558: `GetEwbByDocNoAsync(scope, ...)` passes the shared outer scope).
- **Connector is Scoped**: `Connector` is registered `AddScoped<Connector>()` but used inside `Parallel.ForEachAsync` — if the Scoped DI container is shared across parallel tasks, the **WCF client created by `GetClient()`** may experience concurrent access issues since `BasicHttpBinding` state is not thread-safe.

**Risk**: Data corruption on concurrent SOAP calls; `DbConnection` sharing across tasks.

### 2.2 Blocking `.Result` Call on Async Method

**File**: `Tyresoles.Data/Features/Sales/SalesService.cs` (line 458)

```csharp
var row = query.FirstOrDefaultAsync(cancellationToken).Result;  // ← BLOCKS THREAD
```

The `GetDealerQuery` method synchronously blocks on an async database call. Under ASP.NET Core's thread pool this can cause **thread-pool starvation** under load and potential deadlocks.

### 2.3 `System.Drawing.Bitmap` on Non-Windows Platforms

**File**: `Tyresoles.Data/Features/Sales/ProteanDataService.cs` (lines 758-790)

`GenerateQRCode` uses `System.Drawing.Bitmap` + `Marshal.Copy`. `System.Drawing` is **Windows-only** in .NET 6+; if the server ever migrates to Linux containers, this will throw `PlatformNotSupportedException`.

### 2.4 WCF Client Not Disposed Properly in All Paths

**File**: `Tyresoles.Data/Features/Common/Connector.cs` (lines 65-82, 84-102)

`ExecuteWithRetryAsync` uses `using var client = GetClient()` inside the retry loop. On each retry, a **new client is created and the old one is disposed**, but if `Dispose()` throws (rare but possible with WCF), the retry loop exits with a non-transient exception. The `using` ensures cleanup only for the current iteration.

### 2.5 `Console.WriteLine` Statements Left in Production Code

**Files**:
- `ProteanDataService.cs` lines 74, 289, 377, 706, 715, 726
- Scattered `System.Console.WriteLine` calls that should be `_logger.Log*` — these are not thread-safe for structured output and pollute stdout in production.

---

## 3. Performance Improvements

### 3.1 No `CancellationToken` Propagation in WCF SOAP Calls

**File**: `Tyresoles.Data/Features/Common/Connector.cs`

None of the `ExecuteWithRetryAsync` calls propagate `CancellationToken`. If a user cancels a GraphQL request, the SOAP call continues running server-side until it completes or times out. With the 15-minute GraphQL execution timeout, abandoned SOAP calls can accumulate.

**Fix**: Thread `CancellationToken` through `ExecuteWithRetryAsync` and use `WaitAsync(ct)` on WCF calls.

### 3.2 `ForEach` Sequential SOAP Insert in Hot Paths

**Files**:
- `ProductionService.cs` lines 92-96 (`InsertCasingItemsAsync`): Sequential `INSERT` in a loop
- `ProductionService.cs` lines 651-662 (`UpdateProcOrdLineDispatchAsync`): Sequential SOAP calls per line
- `ProductionService.cs` lines 686-692, 703-708, 720-725: Same pattern

For batch operations (dispatch 20+ lines), each item makes an **individual SOAP call** sequentially. This is O(n) round-trips.

**Fix**: Where the NAV codeunit supports it, batch the calls. Where it doesn't, use `Task.WhenAll` with a bounded concurrency (e.g., `SemaphoreSlim`).

### 3.3 Large SQL Queries Without `TOP` / Pagination

**File**: `ProductionService.cs` — `GetProcurementOrderLinesDispatchAsync` (lines 373-402)

This query fetches **all** purchase lines matching the filter criteria without pagination. For companies with thousands of procurement lines, this can return massive datasets.

**Fix**: Add server-side pagination or `TOP` limits with warning thresholds.

### 3.4 Missing Indexes and Query Optimization Signals

Multiple raw SQL queries in `ProductionService` use:
- `FORMAT(Header.[Posting Date], 'dd-MMM-yy')` — this prevents index usage on `Posting Date`
- `LTRIM(STR(CAST(RIGHT(Line.[Document No_], 5) AS INT)))` — computed in SELECT, not indexed
- String manipulation in WHERE: `AND [New Serial No_] LIKE @like` with `B0300%` — may not hit indexes

**Fix**: Return raw `DateTime` from SQL, format client-side. Add computed columns or indexed views for frequently filtered patterns.

### 3.5 `toList()` Everywhere After `QueryAsync`

**Pattern found in**: ProductionService, SalesService

```csharp
var result = await scope.QueryAsync<T>(sql, params, ct);
return result.ToList();  // materializes entire result set
```

If `QueryAsync` already returns `IEnumerable<T>` backed by a buffered collection, the `.ToList()` creates a **duplicate list allocation**. If it streams, the `.ToList()` is necessary but we should consider returning `IReadOnlyList<T>` to avoid downstream mutations.

### 3.6 GraphQL Execution Timeout Set to 15 Minutes

**File**: `Program.cs` (line 211)

```csharp
o.ExecutionTimeout = TimeSpan.FromMinutes(15);
```

This is extremely generous. A single slow NAV SOAP mutation can hold server resources for 15 minutes. Combined with no per-query complexity analysis, this is an easy DoS vector.

**Fix**: Reduce to 2-5 minutes for queries; use per-operation timeout overrides for known-slow mutations.

### 3.7 EF Core Calendar Queries Not Optimized

**File**: `CalendarService.cs` (41KB, not fully inspected)

The Calendar feature uses EF Core with `CalendarDbContext`. The `ReminderBackgroundService` runs every 1 minute, querying `Include(r => r.Event)` on all unsent reminders. Without an index on `(IsSent, RemindAtUtc)`, this becomes a table scan as reminders grow.

### 3.8 `GlobalQueryCache` — No Stampede Protection

**File**: `Infrastructure/GlobalQueryCache.cs`

`GetOrAddAsync` has a classic **cache stampede** problem: if the cache key expires and 100 requests arrive simultaneously, all 100 will call `factory()` before any writes back. This is expensive for queries like balance calculations.

**Fix**: Use a `SemaphoreSlim` or `Lazy<Task<T>>` per key to ensure only one factory call per cache miss.

### 3.9 E-Invoice/E-Waybill Candidate Fetching Loads Entire Tables

**File**: `Tyresoles.Data/Features/Protean/ProteanDataService.cs`

`GetPendingEWaybillsAsync` (lines 341-343) loads **all** SalesInvoiceHeaders with `EWbillStatus == 1 || 3` into memory, then all related Customers, all DC lines, all DC headers, all Sales Invoice Lines for those headers. For a company with thousands of invoices in flight, this is a massive memory allocation.

**Fix**: Use SQL JOINs to project only needed columns; batch processing with pagination.

---

## 4. Repetitive / Duplicated Code

### 4.1 Duplicate Procurement Logic Across `ProductionService` and `PurchaseService`

| Method | `ProductionService` | `PurchaseService` |
|---|---|---|
| `ItemNos` / `GetItemNosAsync` | Lines 33-57 (raw SQL) | Lines 54-105 (IQuery fluent) |
| `Makes` / `GetMakesAsync` | Lines 111-126 (raw SQL) | Lines 114-133 (IQuery fluent) |
| `MakeSubMake` / `GetMakeSubMakeAsync` | Lines 128-134 (raw SQL) | Lines 140-148 (IQuery fluent) |
| `InspectorCodeNames` | Lines 149-165 (raw SQL) | Lines 159-174 (raw SQL, identical logic) |
| `ProcurementInspection` | Lines 167-179 (static list) | Lines 181-190 (static list, identical) |
| `ProcurementMarkets` | Lines 181-187 (raw SQL) | Lines 197-205 (IQuery fluent) |

**Impact**: Every bug fix must be applied in two places. The `PurchaseService` versions are the "new" IQuery-based implementations; the `ProductionService` versions are the legacy raw SQL copies that haven't been removed.

**Fix**: Consolidate to `PurchaseService` as the single owner; remove duplicates from `ProductionService`; update `Query.cs` to call `PurchaseService`.

### 4.2 Duplicate `ProcurementOrderLinesDispatch` / `NewNumbering`

`ProductionService.GetProcurementOrderLinesDispatchAsync` and `ProcurementService.ProcurementOrderLinesNewNumbering` contain nearly identical SQL SELECT column lists:

```sql
-- Both contain this same 25-column SELECT with CASE expressions for Casing Condition, Order Status, Remark
-- Both join Purchase Line + Vendor + Employee (×3)
-- Both map to OrderLineDispatch / ProcurementNewNumberingDto (same shape)
```

This is **~150 lines of duplicated SQL template**.

### 4.3 Repetitive `userId` Extraction Pattern in Query.cs / Mutation.cs

**Pattern repeated 20+ times**:
```csharp
var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? httpContextAccessor.HttpContext?.User?.FindFirstValue("sub") ?? "";
if (string.IsNullOrEmpty(userId)) return ...;
```

This exact code block appears in every Calendar query/mutation and every Notification query/mutation.

### 4.4 Repetitive Scope Creation Pattern in Query.cs

**Pattern repeated 35+ times**:
```csharp
var scope = dataService.ForTenant("NavLive");
httpContextAccessor.HttpContext?.Response.RegisterForDispose(scope);
```

### 4.5 Repetitive `CASE` Expressions for Casing Condition & Order Status

The SQL CASE expressions for `Casing Condition` (7 values) and `Order Status` (9 values) are duplicated in at least **4 methods** across `ProductionService` and `ProcurementService`.

### 4.6 Duplicate Report Controller Pattern

**File**: `ReportsController.cs`

The `SalesReports`, `PayrollReports`, `ProductionReports`, and `AccountsReports` methods are **structurally identical** (~40 lines each):
1. Validate parameters
2. Check ReportName
3. Validate output format
4. Call service.RenderReportAsync
5. Return file or error

This is 160 lines of copy-paste with only the service variable changing.

### 4.7 Duplicate Error Handling in Mutations

**File**: `Mutation.cs`

Most mutations follow this pattern:
```csharp
try {
    using var scope = dataService.ForTenant("NavLive");
    await service.DoSomethingAsync(scope, ...);
    return new MutationResult { Success = true, Message = "..." };
} catch (Exception ex) {
    return new MutationResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
}
```

This try-catch-return pattern is repeated 10+ times.

### 4.8 Two Separate E-Invoice Candidate Builders

| Builder | File | Purpose |
|---|---|---|
| `ProteanService.GetSalesLinesForEInvoiceAsync` | `Sales/ProteanDataService.cs` L90-161 | Builds via raw SQL with 25+ column SELECT |
| `ProteanDataService.GetPendingEInvoicesAsync` | `Protean/ProteanDataService.cs` L20-27 | Builds via IQuery fluent API |

Both produce the same `EInvoiceCandidate` output from the same NAV tables. The raw SQL version in `ProteanService` (Sales) was the original; the IQuery version in `ProteanDataService` (Protean) is the newer clean version. **Both are still active** and called by different endpoints.

---

## 5. Security Vulnerabilities

### 5.1 CORS Allows Any Origin

**File**: `Program.cs` (lines 184-192)

```csharp
policy.AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader();
```

This is a **critical security issue** for production. Any website can make authenticated requests to the API if it has a user's JWT token.

**Fix**: Restrict to known frontend origins (e.g., `https://tyresoles.app`, `http://localhost:5173`).

### 5.2 Protean Controller is `[AllowAnonymous]`

**File**: `Controllers/ProteanController.cs` (line 21)

The entire Protean controller, including `run-einv`, `run-ewb`, `gstin/save`, and `test-auth`, is marked `[AllowAnonymous]`. Anyone can trigger E-Invoice/E-Waybill processing or modify GSTIN master data without authentication.

**Fix**: Remove `[AllowAnonymous]`; add `[Authorize]` or use API key authentication for machine-to-machine calls.

### 5.3 SQL Injection via String Interpolation in Table Names

Throughout `ProductionService` and `ProteanDataService`, table names from `scope.GetQualifiedTableName(...)` are interpolated directly into SQL strings:
```csharp
var sql = $"SELECT ... FROM {lineT} AS Line ...";
```

While `GetQualifiedTableName` is a controlled internal function (not user-supplied), this pattern is fragile. If any code path allows user input to influence table name resolution, it becomes an injection vector.

### 5.4 Login Mutation Has No Rate Limiting

**File**: `Mutation.cs` (lines 22-38)

The `Login` mutation has no rate limiting, lockout, or brute-force protection. An attacker can enumerate passwords at machine speed.

### 5.5 Default Credentials in Protean Constants

**File**: `Tyresoles.Protean/Constants.cs`

Default GSTIN/username/password from `Constants.DefaultGstin`, `Constants.DefaultEInvUsername`, `Constants.DefaultEInvPassword` are used in the test-auth endpoint. If committed to source control, these credentials are exposed.

---

## 6. Code Quality & Maintainability

### 6.1 Monolithic `Query.cs` and `Mutation.cs`

- `Query.cs`: 860 lines, 40+ query methods
- `Mutation.cs`: 718 lines, 25+ mutation methods

These God-classes violate SRP and make any change risky (merge conflicts, discoverability).

**Fix**: Use HotChocolate `[ExtendObjectType]` to split into domain-specific extensions: `SalesQueries.cs`, `CalendarQueries.cs`, `ProductionQueries.cs`, etc.

### 6.2 `Program.cs` is 397 Lines

The application startup file handles Redis, EF Core migration SQL, GraphQL config, JWT, CORS, DI, service registration, middleware, and endpoint mapping all in a single file.

**Fix**: Extract `IServiceCollection` extension methods: `AddTyresolesAuth()`, `AddTyresolesCalendar()`, `AddTyresolesGraphQL()`, etc.

### 6.3 Inline SQL Schema in `Program.cs`

Lines 241-303 contain raw `CREATE TABLE` / `CREATE INDEX` SQL for the Calendar schema. This should be in a proper migration or schema initializer.

### 6.4 Models / DTOs Defined in Service Files

Multiple result types (`KillSessionResult`, `KillSessionsByUserResult`, `ResetPasswordResult`, `ChangePasswordResult`, `SetProfileResult`, `MutationResult`) are defined at the bottom of `Mutation.cs` Instead of in separate model files.

Similarly, `UpdateGstinRequest` and `ProteanTestAuthResponse` are defined at the bottom of `ProteanController.cs`.

### 6.5 Mixed Async Patterns

Some methods use `ConfigureAwait(false)` consistently; others don't use it at all. In ASP.NET Core, `ConfigureAwait(false)` is not strictly necessary (no synchronization context), but inconsistency suggests different developers/migration waves. Pick one convention and enforce it.

### 6.6 `FetchParams` Used as Universal Parameter Object

`Tyresoles.Data.Features.Production.Models.FetchParams` is a Swiss-army-knife DTO:
```
RespCenters, UserCode, UserDepartment, UserType, UserSpecialToken, 
Regions, Areas, Nos, Type, View, From, To, ReportName
```

Most methods use only 2-3 of these 13 properties. This makes API contracts unclear and encourages passing irrelevant data.

### 6.7 Missing Interface for `Connector`

`Connector` (NAV SOAP gateway) has no interface. It's registered directly as `AddScoped<Connector>()`. This makes testing impossible without a real NAV endpoint and prevents mocking.

---

## 7. Proposed Solutions — Detailed

### Solution 1: Fix Thread Safety in Parallel E-Invoice/E-Waybill Processing

```diff
// ProteanDataService.cs — RunEInvProcessAsync
- await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 5 }, ...)
+ await Parallel.ForEachAsync(candidates, new ParallelOptions 
+ {
+     MaxDegreeOfParallelism = 5,
+     CancellationToken = ct  // ← propagate cancellation
+ }, async (candidate, token) =>
  {
-     await using var workScope = _dataverse.ForTenant(scope.TenantKey);
+     // Each parallel task gets its own scope AND its own Connector clone
+     using var taskServiceScope = _serviceProvider.CreateScope();
+     var workScope = taskServiceScope.ServiceProvider.GetRequiredService<IDataverseDataService>().ForTenant(scope.TenantKey);
+     var taskConnector = taskServiceScope.ServiceProvider.GetRequiredService<Connector>();
      ...
  });
```

### Solution 2: Fix Blocking `.Result` Call

```diff
// SalesService.cs — GetDealerQuery
- public SalespersonPurchaser GetDealerQuery(ITenantScope scope, string code, CancellationToken ct)
+ public async Task<SalespersonPurchaser> GetDealerQuery(ITenantScope scope, string code, CancellationToken ct)
  {
      var query = scope.Query<SalespersonPurchaser>().Where(l => l.Code == code);
-     var row = query.FirstOrDefaultAsync(ct).Result;
+     var row = await query.FirstOrDefaultAsync(ct).ConfigureAwait(false);
      return row ?? new SalespersonPurchaser();
  }
```

### Solution 3: Consolidate Duplicated Master Data Methods

Create a single `ICasingMasterService` that both `ProductionService` and `PurchaseService` delegate to:

```csharp
// New: ICasingMasterService.cs
public interface ICasingMasterService
{
    IQueryable<CasingItem> ItemNos(ITenantScope scope, FetchParams param);
    IQueryable<CodeName> Makes(ITenantScope scope, FetchParams param);
    IQueryable<CodeName> MakeSubMake(ITenantScope scope, FetchParams param);
    Task<List<CodeName>> InspectorCodeNamesAsync(ITenantScope scope, FetchParams param, CancellationToken ct);
    List<CodeName> ProcurementInspection();
    IQueryable<CodeName> ProcurementMarkets(ITenantScope scope);
}
```

Remove the duplicate implementations from `ProductionService`; redirect the existing GraphQL query resolvers.

### Solution 4: Extract User ID Helper

```csharp
// New: UserContextExtensions.cs in Tyresoles.Web
internal static class UserContextExtensions
{
    public static string GetAuthenticatedUserId(this IHttpContextAccessor accessor)
    {
        var ctx = accessor.HttpContext;
        return ctx?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? ctx?.User?.FindFirstValue("sub") 
            ?? "";
    }
    
    public static ITenantScope CreateNavLiveScope(this IDataverseDataService dataService, IHttpContextAccessor accessor)
    {
        var scope = dataService.ForTenant("NavLive");
        accessor.HttpContext?.Response.RegisterForDispose(scope);
        return scope;
    }
}
```

### Solution 5: Extract SQL CASE Expressions to Constants

```csharp
// New: SqlFragments.cs in Production/Models or Common
internal static class SqlFragments
{
    public const string CasingConditionCase = @"
        CASE t0.[Casing Condition] 
            WHEN 0 THEN '' WHEN 1 THEN 'OK' WHEN 2 THEN 'Superficial Lug damages' 
            WHEN 3 THEN 'Minor Ply damages' WHEN 4 THEN 'Minor one cut upto BP5' 
            WHEN 5 THEN 'Minor two cuts upto BP5' WHEN 6 THEN 'Minor three cuts upto BP5' 
            ELSE '' END";

    public const string OrderStatusCase = @"
        CASE t0.[Order Status] 
            WHEN 0 THEN '' WHEN 1 THEN 'Posted' WHEN 2 THEN 'Dispatched' 
            WHEN 3 THEN 'Received At Factory' WHEN 4 THEN 'Purchased' 
            WHEN 5 THEN 'Seconds' WHEN 6 THEN 'Rejected' WHEN 7 THEN 'Dropped' 
            WHEN 8 THEN 'Returned' ELSE '' END";
}
```

### Solution 6: Generic Report Controller Pattern

```csharp
// Extract shared logic to a base method
private async Task<IActionResult> RenderReportAsync<TService>(
    TService service,
    SalesReportParams? parameters,
    Func<TService, ITenantScope, string, SalesReportParams, CancellationToken, Task<byte[]?>> renderFunc,
    string category,
    CancellationToken cancellationToken)
    where TService : notnull
{
    // Shared validation + rendering logic (40 lines → 1 method)
}

[HttpPost("sales")]
public Task<IActionResult> SalesReports([FromBody] SalesReportParams? p, CancellationToken ct)
    => RenderReportAsync(_salesReportService, p, (s, sc, rn, pp, c) => s.RenderReportAsync(sc, rn, pp, c), "Sales", ct);
```

### Solution 7: Add Stampede Protection to GlobalQueryCache

```csharp
private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
{
    var cacheKey = GlobalPrefix + key;
    
    // Fast path: check cache first
    var json = await _cache.GetStringAsync(cacheKey);
    if (!string.IsNullOrEmpty(json))
        return JsonSerializer.Deserialize<T>(json)!;
    
    // Slow path: acquire per-key lock
    var semaphore = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
    await semaphore.WaitAsync();
    try
    {
        // Double-check after acquiring lock
        json = await _cache.GetStringAsync(cacheKey);
        if (!string.IsNullOrEmpty(json))
            return JsonSerializer.Deserialize<T>(json)!;
        
        var result = await factory();
        // ... set cache
        return result!;
    }
    finally
    {
        semaphore.Release();
    }
}
```

### Solution 8: Split Query.cs / Mutation.cs Using HotChocolate Extensions

```csharp
// New: GraphQL/Queries/SalesQueries.cs
[ExtendObjectType(typeof(Query))]
public class SalesQueries
{
    [Authorize]
    public async Task<IReadOnlyList<EntityBalance>> GetMyBalance(...) { ... }
    
    [Authorize]
    [UsePaging] [UseProjection] [UseFiltering] [UseSorting]
    public IQueryable<AccountTransaction> GetMyTransactions(...) { ... }
    
    // ... all sales queries moved here
}

// New: GraphQL/Queries/CalendarQueries.cs
[ExtendObjectType(typeof(Query))]
public class CalendarQueries { ... }

// New: GraphQL/Queries/ProductionQueries.cs
[ExtendObjectType(typeof(Query))]
public class ProductionQueries { ... }
```

### Solution 9: Replace `System.Drawing` with Cross-Platform QR Generation

```diff
// Use SkiaSharp or QRCoder (cross-platform)
- using System.Drawing;
- using System.Drawing.Imaging;
+ using SkiaSharp;
+ using QRCoder;

private static byte[] GenerateQRCode(string data)
{
    using var qrGenerator = new QRCodeGenerator();
    var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M);
    using var qrCode = new PngByteQRCode(qrCodeData);
    return qrCode.GetGraphic(5);
}
```

### Solution 10: Add IConnector Interface

```csharp
// New: IConnector.cs
public interface IConnector
{
    Task<string> TestConnectionAsync();
    Task InsertEInvoiceAsync(EInvoice eInvoice);
    Task InsertGSTINAsync(PartyGstin gstRec);
    Task InsertGstApiLogAsync(GSTApiLog log);
    Task CreateDealerAsync(CreateDealer dealer);
    Task<string> NewProcurementOrderAsync(FetchParams param);
    // ... all public methods
}

// DI registration:
builder.Services.AddScoped<IConnector, Connector>();
```

---

## 8. Priority Matrix

| # | Issue | Severity | Effort | Priority |
|---|---|---|---|---|
| 2.1 | Thread safety in Parallel.ForEachAsync | 🔴 Critical | Medium | **P0** |
| 5.2 | ProteanController [AllowAnonymous] | 🔴 Critical | Low | **P0** |
| 5.1 | CORS AllowAnyOrigin | 🔴 Critical | Low | **P0** |
| 2.2 | Blocking .Result call | 🟠 High | Low | **P1** |
| 5.4 | No login rate limiting | 🟠 High | Medium | **P1** |
| 3.8 | Cache stampede (GlobalQueryCache) | 🟠 High | Low | **P1** |
| 3.1 | No CancellationToken in WCF calls | 🟠 High | Medium | **P1** |
| 3.6 | 15-min GraphQL timeout | 🟠 High | Low | **P1** |
| 4.1 | Duplicate master data methods | 🟡 Medium | Medium | **P2** |
| 4.2 | Duplicate SQL templates | 🟡 Medium | Medium | **P2** |
| 4.6 | Duplicate report controller pattern | 🟡 Medium | Low | **P2** |
| 4.3 | Repetitive userId extraction | 🟡 Medium | Low | **P2** |
| 4.4 | Repetitive scope creation | 🟡 Medium | Low | **P2** |
| 4.8 | Two E-Invoice candidate builders | 🟡 Medium | High | **P2** |
| 6.1 | Monolithic Query.cs / Mutation.cs | 🟡 Medium | Medium | **P2** |
| 3.9 | E-Invoice loads entire tables | 🟡 Medium | High | **P2** |
| 2.3 | System.Drawing Windows-only | 🟢 Low | Low | **P3** |
| 2.5 | Console.WriteLine in prod code | 🟢 Low | Low | **P3** |
| 3.2 | Sequential SOAP in loops | 🟢 Low | High | **P3** |
| 3.4 | FORMAT() preventing index use | 🟢 Low | Medium | **P3** |
| 6.2 | Program.cs 397 lines | 🟢 Low | Medium | **P3** |
| 6.3 | Inline SQL schema in Program.cs | 🟢 Low | Medium | **P3** |
| 6.4 | DTOs defined in service files | 🟢 Low | Low | **P3** |
| 6.5 | Mixed ConfigureAwait usage | 🟢 Low | Low | **P3** |
| 6.6 | God-object FetchParams | 🟢 Low | High | **P3** |
| 6.7 | Missing IConnector interface | 🟢 Low | Low | **P3** |

---

## Execution Recommendations

### Phase 1 — Critical Security & Safety (1-2 days)
- Fix CORS policy (`AllowAnyOrigin` → whitelist)
- Add `[Authorize]` to `ProteanController` (or API key auth)
- Fix `Parallel.ForEachAsync` to use proper scoped DI
- Fix `.Result` blocking call in `GetDealerQuery`

### Phase 2 — Performance & Stability (3-5 days)
- Add cache stampede protection to `GlobalQueryCache`
- Reduce GraphQL execution timeout (15min → 3min)
- Propagate `CancellationToken` through `Connector`
- Add login rate limiting
- Replace `Console.WriteLine` with `ILogger`

### Phase 3 — Code Consolidation (5-7 days)
- Consolidate duplicate master data methods (Production ↔ Purchase)
- Extract `userId` helper and scope creation helper
- Extract SQL CASE constants
- Refactor `ReportsController` to use generic method
- Split `Query.cs` / `Mutation.cs` into domain extensions

### Phase 4 — Architecture Polish (ongoing)
- Extract `IConnector` interface
- Move DTOs to proper model files
- Refactor `Program.cs` into extension methods
- Migrate Calendar schema SQL to EF migrations
- Replace `System.Drawing` with cross-platform alternative
- Unify E-Invoice candidate builders
