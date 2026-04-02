# Improvement Plan: GetCustomerTrialBalanceAsync

**Goal:** Make Customer Trial Balance report **near-native speed** with **minimal DB round trips** and **efficient memory use**.

**Owner:** Back-end / Tyresoles.Data  
**Target:** `SalesReportService.GetCustomerTrialBalanceAsync`

---

## 1. Current State

| Metric | Current |
|--------|--------|
| **DB round trips** | Up to **7** (sequential) |
| **Ledger table scans** | **4** (same table, different date/aggregates) |
| **Memory** | Multiple full arrays + 4 dictionaries; `ToDataTable` allocates per row |

### Round-trip breakdown

1. **Customers** – filtered list  
2. **Areas** – lookup by area codes from (1)  
3. **Dealers** – lookup by dealer codes from (1)  
4. **Period opening balance** – `Detailed Cust. Ledg. Entry`, `PostingDate < fromDt`, `GROUP BY CustomerNo`, `SUM(Amount)`  
5. **FY opening balance** – same table, `PostingDate < fyStart`  
6. **Period ledger** – same table, `fromDt..toDt`, `SUM(Debit)`, `SUM(Credit)`  
7. **FY ledger** – same table, `fyStart..toDt`, same aggregates  

Merge is in-memory: build 4 dictionaries from aggregation results, then loop over customers and fill `CustomerTrialBalanceRow`.

---

## 2. Target State (Near-Native Speed)

| Metric | Target |
|--------|--------|
| **DB round trips** | **1–2** (see phases below) |
| **Ledger table scans** | **1** (single aggregated query) |
| **Memory** | Single-pass where possible; pre-sized collections; minimal temp allocations |

---

## 3. Phased Plan

### Phase 1 – Single-pass ledger (biggest win)

**Objective:** One round trip for all ledger data; one scan of `Detailed Cust. Ledg. Entry`.

**Idea:** Replace the four separate grouped queries with **one** SQL query that does conditional aggregation per customer:

- `GROUP BY [Customer No_]`
- In the SELECT:
  - Period OB: `SUM(CASE WHEN [Posting Date] < @fromDt AND [Entry Type] <> 2 THEN [Amount] ELSE 0 END)`
  - FY OB: `SUM(CASE WHEN [Posting Date] < @fyStart AND [Entry Type] <> 2 THEN [Amount] ELSE 0 END)`
  - Period Debit/Credit: `SUM(CASE WHEN [Posting Date] >= @fromDt AND [Posting Date] <= @toDt AND [Entry Type] <> 2 THEN [Debit Amount] ELSE 0 END)` (and same for Credit)
  - YTD Debit/Credit: same with `@fyStart` and `@toDt`

**Implementation options:**

- **A. Raw SQL + DTO:** Use `scope.QueryAsync<CustomerTrialBalanceLedgerRow>(sql, new { fromDt, toDt, fyStart })` (or equivalent `RawQueryToArrayAsync` / `ExecuteScalar` path). Define a small DTO with `CustomerNo`, `PeriodOB`, `FYOB`, `PeriodDebit`, `PeriodCredit`, `YTDDebit`, `YTDCredit`. Table name must respect company (e.g. `Tyresoles (India) Pvt_ Ltd_$Detailed Cust_ Ledg_ Entry` or use existing table resolution from `scope.Query<DetailedCustLedgEntry>()` and dialect).
- **B. Extend fluent API:** Add a "conditional aggregate" or "single-pass group by with expressions" builder that emits the above SQL. More reusable but more work.

**Filter by customer list:** When `p.Customers` (or derived list from step 1) is available, add:

```sql
AND [Customer No_] IN (@customerNos)
```

so the engine only reads rows for reportable customers.

**Deliverables:**

- One method/query that returns all ledger aggregates in one result set.
- `GetCustomerTrialBalanceAsync` updated to call it once and build a single lookup structure (e.g. `Dictionary<string, CustomerTrialBalanceLedgerRow>`) for the merge loop.
- **Result:** 4 ledger round trips → 1; 4 scans → 1.

---

### Phase 2 – One round trip for customers + lookups

**Objective:** Cut 3 round trips (customers, areas, dealers) to 1.

**Option A – Single query with joins**

- One query: `Customer` **LEFT JOIN** `Area` ON `Customer.AreaCode = Area.Code` **LEFT JOIN** `SalespersonPurchaser` (dealers) ON `Customer.DealerCode = SalespersonPurchaser.Code`.
- Apply existing filters (Customers, Dealers, Areas, RespCenters) on `Customer`.
- Project to a DTO: `CustomerNo`, `CustomerName`, `AreaName`, `DealerName` (and any other fields needed for `CustomerTrialBalanceRow`).
- Single `ToArrayAsync` → one round trip. Then build `CustomerTrialBalanceRow[]` from that (no separate area/dealer dictionaries).

**Option B – MultipleQuery (no join changes)**

- Use `scope.CreateMultipleQuery().Add(custQuery).Add(areaQuery).Add(dealerQuery)`.
- Extend `IMultipleQuery` with `ExecuteAsync<T1, T2, T3>` (already exists in codebase). Add exactly 3 queries, then execute once and get three arrays.
- Keep current in-memory merge (areas/dealers to dictionaries, then project customers). Still 1 round trip instead of 3.

**Recommendation:** Prefer **Option A** (single joined query) for fewer round trips and simpler code. Use Option B if you want to avoid changing the Customer query shape.

**Deliverables:**

- Either one joined query returning customer + area + dealer names, or one `ExecuteAsync<T1, T2, T3>` for customers, areas, dealers.
- **Result:** 3 round trips → 1.

---

### Phase 3 – Minimal round trips end-to-end

**Objective:** After Phase 1 and 2, only **1 or 2** round trips total.

- **2-round-trip design:**  
  1) Customers + areas + dealers (joined or multi-query).  
  2) Single-pass ledger query (with optional `IN (customer list)`).  
  Then merge in memory.

- **1-round-trip design (advanced):**  
  Use `IMultipleQuery` with 2 queries: (1) customers + areas + dealers, (2) single-pass ledger.  
  Requires `ExecuteAsync<T1, T2>` and two result sets in one `QueryMultipleAsync`.  
  Same as today's `GetSalesAndBalanceAsync` pattern but with the new ledger query and joined (or multi) customer query.

**Deliverables:**

- Document which variant is implemented (1 vs 2 round trips).
- **Result:** Total round trips = 1 or 2 (down from 7).

---

### Phase 4 – Memory efficiency

**Objective:** Use memory efficiently; avoid unnecessary allocations and large Gen2 pressure.

1. **Pre-size collections**
   - When building dictionaries from aggregation results, use constructor capacity: `new Dictionary<string, decimal>(periodOBal.Length)` (and similar for other dicts). After Phase 1 you have one result set; one dictionary with `capacity = customerCount` or `ledgerRowCount`.

2. **Single merge pass**
   - After Phase 1, merge loop only needs one lookup structure (e.g. `Dictionary<string, CustomerTrialBalanceLedgerRow>`) keyed by `CustomerNo`. No need for four separate dictionaries.

3. **Avoid redundant allocations**
   - Reuse the same `PeriodFilter` string for all rows: `var periodFilter = $"{fromDt:dd-MM-yy}..{toDt:dd-MM-yy}";` once, then assign `row.PeriodFilter = periodFilter;`.
   - Reuse formatted customer number logic without extra substrings where possible (e.g. cache in a small pool or single pass).

4. **ToDataTable**
   - Current `ToDataTable` allocates per row and uses reflection per type once (column definitions). To reduce allocations:
     - Pre-allocate `DataTable` with correct column count and, if available, row count: `dt.BeginLoadData();` then add rows in a loop; `dt.EndLoadData();` to avoid event overhead.
     - Consider a single reflection pass to get `PropertyInfo[]` and reuse for all rows (already the case for column definitions; ensure row loop does not re-get properties).
   - If the report layer can consume a list or array instead of `DataTable`, that would avoid DataTable overhead; only change if the RDLC contract allows it.

5. **No streaming for this report**
   - Report needs full result set for RDLC; streaming is not required. Focus on fewer round trips and smaller, pre-sized in-memory structures rather than `StreamAsync` here.

**Deliverables:**

- Dictionaries and lists created with explicit capacity where size is known.
- Single shared `periodFilter` string; single lookup structure for ledger data after Phase 1.
- Optional: `ToDataTable` tuned (pre-size, `BeginLoadData`/`EndLoadData`) or replaced if API allows.

---

### Phase 5 – Database and runtime hygiene

**Objective:** Ensure the database and runtime do not become the bottleneck.

1. **Index (SQL Server)**
   - Recommend an index on the table underlying `DetailedCustLedgEntry` that supports the single-pass ledger query, e.g.:
     - `([Entry Type], [Posting Date], [Customer No_])`  
     or
     - `([Customer No_], [Entry Type], [Posting Date])`  
   - Align with actual table name (company-prefixed) and query plan. Goal: one index seek/range scan per customer set, no full table scan.

2. **Connection**
   - Use existing DI and `IDbConnectionFactory`; avoid creating extra scopes or connections for this report. Single scope, 1–2 round trips.

3. **Cancellation**
   - Keep passing `CancellationToken ct` through all async calls so long-running runs can be cancelled.

**Deliverables:**

- Index recommendation documented and applied if DBA agrees.
- No extra connections; `ct` passed through.

---

## 4. Implementation Checklist (summary)

- [ ] **Phase 1:** Implement single-pass ledger query (raw SQL + DTO or fluent extension). Add optional `IN (customer list)`. Replace 4 ledger calls with 1; single lookup for merge.
- [ ] **Phase 2:** Replace customers + areas + dealers with one joined query or one `ExecuteAsync<T1, T2, T3>`.
- [ ] **Phase 3:** Wire so total round trips = 1 or 2 (multi-query if going for 1).
- [ ] **Phase 4:** Pre-size dictionaries; single `periodFilter`; single ledger lookup; optional `ToDataTable` tuning.
- [ ] **Phase 5:** Index on ledger table; confirm `ct` and connection usage.

---

## 5. Success Criteria

| Criterion | Measure |
|-----------|--------|
| **Round trips** | 1 or 2 per report run (down from 7). |
| **Ledger scans** | 1 (down from 4). |
| **Latency** | End-to-end report time dominated by one heavy query + one light query (or one batched call), not 7 sequential round trips. |
| **Memory** | No unnecessary growth; dictionaries/list pre-sized; minimal duplicate strings. |
| **Correctness** | Output matches current report (same figures for Period OB, FY OB, Period/YTD debit/credit, YTD total). |

---

## 6. Risks and Mitigations

| Risk | Mitigation |
|------|------------|
| Raw SQL and table name/company | Use same table/company resolution as `scope.Query<DetailedCustLedgEntry>()` (e.g. dialect + company from scope). |
| Dapper vs existing materializer | For raw SQL, use a simple DTO and Dapper's `QueryAsync`; no need for NavColumn on this DTO if column names match. |
| Breaking RDLC contract | Keep returning same `DataTable` shape and report name; only change how the data is fetched and merged. |

---

## 7. References

- Existing batch pattern: `GetSalesAndBalanceAsync` uses `CreateMultipleQuery().Add(...).Add(...).ExecuteAsync<T1, T2>` (see `SalesReportService.cs` around line 354).
- `IMultipleQuery`: `Tyresoles.Sql\Abstractions\Interfaces.cs`; implementation in `Tyresoles.Sql\Core\MultipleQuery.cs` (supports 2 and 3 queries; extend to 4 if needed).
- Table: `Detailed Cust_ Ledg_ Entry` (company-specific), model `DetailedCustLedgEntry` in `Generated\NavModels.g.cs`.
- Performance guidance: `back-end\docs\guides\performance-tuning.md`.
