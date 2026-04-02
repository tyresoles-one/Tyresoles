# Back-End Unused Code / Dead Code Audit Plan

> **Audit Date**: 2026-04-01  
> **Scope**: `Tyresoles.Web` (GraphQL Resolvers & REST Controllers), `Tyresoles.Data` (Service Layer Mapping) vs `front-end` usage.
> **Goal**: Identify and remove endpoints, GraphQL fields, and backend services that are completely detached or unreachable from the front-end application to reduce footprint and attack surface.

---

## 1. Executive Summary

A comprehensive search across the `.svelte`, `.ts`, and auto-generated `.graphql`/`.gql` definitions in the `front-end` workspace correctly matched **>95%** of the existing backend API surface. Almost everything defined in `Query.cs`, `Mutation.cs`, and `Controllers/*` is wired to the UI. 

However, there are several "orphan" methods, legacy redundant functions from incomplete migrations, and development-only test endpoints that can be safely removed.

---

## 2. Unreachable / Unused Endpoints (Safe to Delete)

### 2.1 Temporary Auth Testing (REST)
**File**: `Tyresoles.Web/Controllers/ProteanController.cs`
**Endpoint**: `GET /api/protean/test-auth`
- **Why it's unused**: This was built to test Protean session tokens with default credentials (defined in `Constants.cs`). The front-end triggers E-Invoicing via the `run-einv` POST command automatically. This endpoint is unreachable from the SPA UI.
- **Action**: Delete immediately. It poses a security risk if left in production.

### 2.2 Replaced / Dead GraphQL Queries
**File**: `Tyresoles.Web/Query.cs`
**Query**: `getSalesInvLinesForInvoiceAsync` (Returns `List<EInvoiceCandidate>`)
- **Why it's unused**: Early in development, this was used to expose E-Invoice candidates. The current production workflow relies on the backend processor (`RunEInvProcessAsync`) scanning the database and generating IRNs automatically without piping the data down to the browser.
- **Action**: Remove from `Query.cs` and subsequently remove `ProteanService.GetSalesLinesForEInvoiceAsync` from `Tyresoles.Data`.

---

## 3. Partially Unused / Redundant Service Code (Legacy Debt)

The following backend services are technically reachable if explicitly queried, but are architecturally redundant because the frontend has (or is) migrating to vastly improved abstractions.

### 3.1 Legacy Production Service Master Data 
**File**: `Tyresoles.Data/Features/Production/ProductionService.cs`
**Methods**: `GetItemNosAsync`, `GetMakesAsync`, `GetMakeSubMakeAsync`, `GetInspectorCodeNamesAsync`, `GetProcurementMarketsAsync`.
- **Why they are unused/redundant**: These were ported to `Tyresoles.Data/Features/Purchase/PurchaseService.cs` and are exposed via new, strongly-typed `IQueryable` endpoints (e.g., `GetPurchaseItemNos`). 
- **Action**: Update the remaining legacy GraphQL queries in `Query.cs` (e.g. `GetProductionItemNos`) to point to `PurchaseService`, or update the Svelte S2 components (`ecoproc` route) to point to the new queries and delete the old endpoints/methods entirely.

### 3.2 Dual E-Invoice Builders
**Files**: `ProteanDataService.cs` (Protean folder) vs `ProteanDataService.cs` (Sales folder)
- **Why it's redundant**: The method `GetSalesLinesForEInvoiceAsync` (raw SQL) builds candidates exactly how `GetPendingEInvoicesAsync` (Fluent IQuery API) does it. 
- **Action**: Since `getSalesInvLinesForInvoiceAsync` is unused from the front-end, delete the raw SQL version of the candidate builder and standardize on the `IQuery` implementation in the Protean feature folder.

---

## 4. Execution Plan (Cleanup Steps)

If you are ready to execute this cleanup, proceed with the following commits/steps:

1. **Delete Test Protean Endpoint**: Open `ProteanController.cs` and delete lines 52-99 (`TestAuth` method).
2. **Remove Orphan Query**: Open `Query.cs` and remove `GetSalesInvLinesForInvoiceAsync`.
3. **Delete Dead Candidate Builder**: Open `Tyresoles.Data/Features/Sales/ProteanDataService.cs` and remove `GetSalesLinesForEInvoiceAsync` method. Remove its interface definition in `ISalesService`.
4. **Complete Purchase Service Migration**: Cut over the UI usages of `GetProductionItemNos` to `GetPurchaseItemNos`, and definitively delete the raw-SQL variants from `ProductionService.cs`.
5. **Codegen Sync**: Run `npm run codegen` in the `front-end` repository to verify schema integrity after backend cleanup.
