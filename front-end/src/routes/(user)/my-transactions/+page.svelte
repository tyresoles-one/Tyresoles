<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import { fade, slide } from "svelte/transition";
  import { goto } from "$app/navigation";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import {
    GetTransactionsPageDocument,
    type GetTransactionsPageQuery,
    type AccountTransactionSortInput,
  } from "$lib/services/graphql/generated/types.js";
  import { getUser } from "$lib/stores/auth";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Skeleton } from "$lib/components/ui/skeleton";

  // ── Entity context ─────────────────────────────────────────
  const user = getUser();
  const entityType = user?.entityType ?? null;
  const entityCode = user?.entityCode ?? null;
  const respCenter = user?.respCenter ?? null;

  // ── Types ──────────────────────────────────────────────────
  type TxNode = NonNullable<
    NonNullable<GetTransactionsPageQuery["myTransactions"]>["nodes"]
  >[number];

  type SortField = "date" | "amount" | "documentNo";
  type SortDir = "ASC" | "DESC";
  type TxTypeFilter = "all" | "credit" | "debit";
  type DocTypeFilter = number | "all";

  // ── Fetch state ────────────────────────────────────────────
  const PAGE_SIZE = 20;
  let allRows = $state<TxNode[]>([]);
  let totalCount = $state(0);
  let cursor = $state<string | null>(null);
  let hasMore = $state(false);
  let loading = $state(false); // initial / sort-reset load
  let loadingMore = $state(false); // infinite-scroll load
  let error = $state<string | null>(null);
  let initialLoaded = $state(false);

  // ── Sort ───────────────────────────────────────────────────
  let sortField = $state<SortField>("date");
  let sortDir = $state<SortDir>("DESC");

  function buildOrder(): AccountTransactionSortInput[] {
    return [{ [sortField]: sortDir }];
  }

  async function fetchPage(afterCursor: string | null, append: boolean) {
    if (append) loadingMore = true;
    else {
      loading = true;
      error = null;
    }
    try {
      const res = await graphqlQuery<GetTransactionsPageQuery>(
        GetTransactionsPageDocument,
        {
          variables: {
            entityType,
            entityCode,
            respCenter,
            first: PAGE_SIZE,
            after: afterCursor ?? undefined,
            order: buildOrder(),
          },
          skipCache: true,
        },
      );
      if (res.success && res.data?.myTransactions) {
        const { nodes, pageInfo, totalCount: tc } = res.data.myTransactions;
        const newRows = nodes ?? [];
        if (append) {
          allRows = [...allRows, ...newRows];
        } else {
          allRows = newRows;
          totalCount = tc;
          initialLoaded = true;
        }
        cursor = pageInfo.endCursor ?? null;
        hasMore = pageInfo.hasNextPage;
      } else {
        error = res.error ?? "Failed to load transactions.";
      }
    } catch {
      error = "An unexpected error occurred.";
    } finally {
      loading = false;
      loadingMore = false;
    }
  }

  async function reload() {
    cursor = null;
    hasMore = false;
    await fetchPage(null, false);
  }

  async function loadMore() {
    if (loadingMore || !hasMore || !cursor) return;
    await fetchPage(cursor, true);
  }

  // ── Infinite scroll sentinel ───────────────────────────────
  let sentinel: HTMLDivElement | null = null;
  let observer: IntersectionObserver | null = null;

  function attachObserver() {
    if (!sentinel || typeof IntersectionObserver === "undefined") return;
    observer?.disconnect();
    observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && hasMore && !loadingMore) {
          loadMore();
        }
      },
      { threshold: 0.1 },
    );
    observer.observe(sentinel);
  }

  $effect(() => {
    if (sentinel) attachObserver();
  });

  onMount(() => {
    reload();
  });

  onDestroy(() => {
    observer?.disconnect();
  });

  // ── Filters (client-side on allRows) ──────────────────────
  let filterDocNo = $state("");
  let filterCustNo = $state("");
  let filterType = $state<TxTypeFilter>("all");
  let filterDocType = $state<DocTypeFilter>("all");
  let filterDateFrom = $state("");
  let filterDateTo = $state("");
  let showFilters = $state(false);

  // ── Helpers ────────────────────────────────────────────────
  function toNum(v: unknown): number {
    return typeof v === "number" ? v : parseFloat(String(v)) || 0;
  }

  function fmt(n: number) {
    return n.toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }

  const DOC_TYPE_LABELS: Record<number, string> = {
    0: "Credit Memo",
    1: "Payment",
    2: "Invoice",
    3: "Reminder",
    4: "Finance Charge",
  };
  function typeLabel(t: number) {
    return DOC_TYPE_LABELS[t] ?? `Type ${t}`;
  }

  function fmtDate(iso: string | null) {
    if (!iso) return "—";
    return new Date(iso).toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    });
  }

  // ── Filtered derived rows ──────────────────────────────────
  let filteredRows = $derived(
    (() => {
      let rows = allRows;

      // Doc no
      if (filterDocNo.trim()) {
        const q = filterDocNo.trim().toLowerCase();
        rows = rows.filter((r) => r.documentNo?.toLowerCase().includes(q));
      }
      // Customer no
      if (filterCustNo.trim()) {
        const q = filterCustNo.trim().toLowerCase();
        rows = rows.filter((r) => r.customerNo?.toLowerCase().includes(q));
      }
      // Credit / debit
      if (filterType === "credit")
        rows = rows.filter((r) => toNum(r.amount) < 0);
      else if (filterType === "debit")
        rows = rows.filter((r) => toNum(r.amount) > 0);

      // Doc type
      if (filterDocType !== "all") {
        rows = rows.filter((r) => r.type === filterDocType);
      }
      // Date from
      if (filterDateFrom) {
        const from = new Date(filterDateFrom).getTime();
        rows = rows.filter((r) => r.date && new Date(r.date).getTime() >= from);
      }
      // Date to
      if (filterDateTo) {
        const to = new Date(filterDateTo).getTime() + 86_399_000;
        rows = rows.filter((r) => r.date && new Date(r.date).getTime() <= to);
      }
      return rows;
    })(),
  );

  // ── Sort toggle ────────────────────────────────────────────
  function setSort(field: SortField) {
    if (sortField === field) {
      sortDir = sortDir === "ASC" ? "DESC" : "ASC";
    } else {
      sortField = field;
      sortDir = field === "amount" ? "DESC" : "DESC";
    }
    reload();
  }

  // ── Filter reset ───────────────────────────────────────────
  function resetFilters() {
    filterDocNo = "";
    filterCustNo = "";
    filterType = "all";
    filterDocType = "all";
    filterDateFrom = "";
    filterDateTo = "";
  }

  let hasActiveFilters = $derived(
    filterDocNo ||
      filterCustNo ||
      filterType !== "all" ||
      filterDocType !== "all" ||
      filterDateFrom ||
      filterDateTo,
  );

  // ── Amount colour ──────────────────────────────────────────
  function amtClass(amount: unknown) {
    const n = toNum(amount);
    if (n < 0) return "tx-credit";
    if (n > 0) return "tx-debit";
    return "tx-zero";
  }

  // Unique doc types from loaded rows (for filter chips)
  let availableDocTypes = $derived(
    [...new Set(allRows.map((r) => r.type))].sort((a, b) => a - b),
  );
</script>

<svelte:head>
  <title>My Transactions</title>
  <meta
    name="description"
    content="View and search your recent account transactions."
  />
</svelte:head>

<div class="tx-page">
  <!-- ── Page heading ──────────────────────────────────────── -->
  <PageHeading backHref="/" icon="receipt">
    {#snippet title()}
      My Transactions
    {/snippet}
  </PageHeading>

  <!-- ── Toolbar ───────────────────────────────────────────── -->
  <div class="tx-toolbar">
    <!-- Summary chip -->
    <div class="tx-summary-chip">
      {#if loading}
        <span class="tx-chip-label">Loading…</span>
      {:else if initialLoaded}
        <span class="tx-chip-label"
          >{totalCount.toLocaleString("en-IN")} total</span
        >
      {/if}
    </div>

    <div class="tx-toolbar-right">
      <!-- Filter toggle -->
      <button
        class="tx-icon-btn {hasActiveFilters ? 'tx-icon-btn--active' : ''}"
        onclick={() => (showFilters = !showFilters)}
        title="Filters"
        aria-expanded={showFilters}
      >
        <Icon name="sliders-horizontal" class="tx-toolbar-icon" />
        {#if hasActiveFilters}
          <span class="tx-filter-dot"></span>
        {/if}
      </button>

      <!-- Sort buttons -->
      <div class="tx-sort-group" role="group" aria-label="Sort">
        {#each [["date", "Date"], ["amount", "₹"], ["documentNo", "Doc"]] as const as [field, label]}
          <button
            class="tx-sort-btn {sortField === field
              ? 'tx-sort-btn--active'
              : ''}"
            onclick={() => setSort(field)}
            aria-pressed={sortField === field}
          >
            {label}
            {#if sortField === field}
              <Icon
                name={sortDir === "DESC" ? "arrow-down" : "arrow-up"}
                class="tx-sort-arrow"
              />
            {/if}
          </button>
        {/each}
      </div>

      <!-- Refresh -->
      <button
        class="tx-icon-btn"
        onclick={reload}
        disabled={loading}
        title="Refresh"
        aria-label="Refresh"
      >
        <Icon
          name="refresh-cw"
          class="tx-toolbar-icon {loading ? 'spinning' : ''}"
        />
      </button>
    </div>
  </div>

  <!-- ── Filter panel ───────────────────────────────────────── -->
  {#if showFilters}
    <div
      class="tx-filter-panel"
      in:slide={{ duration: 220 }}
      out:slide={{ duration: 180 }}
    >
      <div class="tx-filter-grid">
        <!-- Doc No -->
        <div class="tx-filter-field">
          <label class="tx-filter-label" for="filter-docno">Document No</label>
          <div class="tx-input-wrap">
            <Icon name="search" class="tx-input-icon" />
            <input
              id="filter-docno"
              class="tx-input"
              type="text"
              placeholder="e.g. SI-00123"
              bind:value={filterDocNo}
            />
          </div>
        </div>
        <!-- Customer No -->
        <div class="tx-filter-field">
          <label class="tx-filter-label" for="filter-custno">Customer No</label>
          <div class="tx-input-wrap">
            <Icon name="user" class="tx-input-icon" />
            <input
              id="filter-custno"
              class="tx-input"
              type="text"
              placeholder="e.g. CUST0001"
              bind:value={filterCustNo}
            />
          </div>
        </div>
        <!-- Date from -->
        <div class="tx-filter-field">
          <label class="tx-filter-label" for="filter-from">Date From</label>
          <input
            id="filter-from"
            class="tx-input tx-input--date"
            type="date"
            bind:value={filterDateFrom}
          />
        </div>
        <!-- Date to -->
        <div class="tx-filter-field">
          <label class="tx-filter-label" for="filter-to">Date To</label>
          <input
            id="filter-to"
            class="tx-input tx-input--date"
            type="date"
            bind:value={filterDateTo}
          />
        </div>
        <!-- Amount sign -->
        <div class="tx-filter-field tx-filter-field--full">
          <span class="tx-filter-label">Amount</span>
          <div class="tx-chip-group" role="group">
            {#each ["all", "credit", "debit"] as TxTypeFilter[] as opt}
              <button
                class="tx-chip {filterType === opt ? 'tx-chip--active' : ''}"
                onclick={() => (filterType = opt)}
                aria-pressed={filterType === opt}
              >
                {opt === "all"
                  ? "All"
                  : opt === "credit"
                    ? "🟢 Credits"
                    : "🔴 Debits"}
              </button>
            {/each}
          </div>
        </div>
        <!-- Doc type chips -->
        {#if availableDocTypes.length > 0}
          <div class="tx-filter-field tx-filter-field--full">
            <span class="tx-filter-label">Type</span>
            <div class="tx-chip-group" role="group">
              <button
                class="tx-chip {filterDocType === 'all'
                  ? 'tx-chip--active'
                  : ''}"
                onclick={() => (filterDocType = "all")}
                aria-pressed={filterDocType === "all"}>All</button
              >
              {#each availableDocTypes as dt}
                <button
                  class="tx-chip {filterDocType === dt
                    ? 'tx-chip--active'
                    : ''}"
                  onclick={() => (filterDocType = dt)}
                  aria-pressed={filterDocType === dt}
                >
                  {typeLabel(dt)}
                </button>
              {/each}
            </div>
          </div>
        {/if}
      </div>
      {#if hasActiveFilters}
        <button class="tx-reset-btn" onclick={resetFilters}>
          <Icon name="x" class="tx-reset-icon" /> Clear filters
        </button>
      {/if}
    </div>
  {/if}

  <!-- ── Error state ────────────────────────────────────────── -->
  {#if error}
    <div class="tx-error-banner" in:fade>
      <Icon name="alert-circle" class="tx-error-icon" />
      <span>{error}</span>
      <button class="tx-error-retry" onclick={reload}>Retry</button>
    </div>
  {/if}

  <!-- ── Content ────────────────────────────────────────────── -->
  <div class="tx-list-wrap">
    {#if loading}
      <!-- Skeleton rows -->
      <div class="tx-skeleton-list" in:fade={{ duration: 150 }}>
        {#each { length: 8 } as _, i}
          <div class="tx-skeleton-row" style="animation-delay: {i * 40}ms">
            <div class="tx-skel-left">
              <Skeleton class="tx-skel-doc" />
              <Skeleton class="tx-skel-meta" />
            </div>
            <Skeleton class="tx-skel-amt" />
          </div>
        {/each}
      </div>
    {:else if !error}
      {#if filteredRows.length === 0}
        <div class="tx-empty" in:fade>
          <Icon name="inbox" class="tx-empty-icon" />
          <p class="tx-empty-title">No transactions found</p>
          {#if hasActiveFilters}
            <p class="tx-empty-sub">Try adjusting your filters</p>
            <button class="tx-reset-btn" onclick={resetFilters}
              >Clear filters</button
            >
          {:else}
            <p class="tx-empty-sub">
              No transactions available for your account.
            </p>
          {/if}
        </div>
      {:else}
        <div class="tx-list" in:fade={{ duration: 200 }}>
          <!-- desktop column header (hidden on mobile) -->
          <div class="tx-col-header" aria-hidden="true">
            <span class="tx-col tx-col-date">Date</span>
            <span class="tx-col tx-col-doc">Document No</span>
            <span class="tx-col tx-col-type">Type</span>
            <span class="tx-col tx-col-cust">Customer</span>
            <span class="tx-col tx-col-name">Name</span>
            <span class="tx-col tx-col-amt">Amount</span>
          </div>
          {#each filteredRows as tx, i (tx.documentNo + String(tx.date) + i)}
            <div
              class="tx-card"
              in:fade={{ duration: 180, delay: Math.min(i, 5) * 30 }}
            >
              <!-- mobile badge (hidden on desktop) -->
              <div class="tx-card-badge tx-mobile-only">
                <Icon name="file-text" class="tx-card-badge-icon" />
              </div>
              <!-- mobile: stacked info -->
              <div class="tx-card-info tx-mobile-only">
                <span class="tx-card-doc">{tx.documentNo || "—"}</span>
                <span class="tx-card-meta"
                  >{typeLabel(tx.type)} · {fmtDate(tx.date)}</span
                >
                {#if tx.customerNo}
                  <span class="tx-card-cust">{tx.customerNo}</span>
                {/if}
                {#if tx.customerName}
                  <span class="tx-card-name">{tx.customerName}</span>
                {/if}
              </div>
              <!-- desktop: individual cells -->
              <span class="tx-cell tx-cell-date tx-desktop-only"
                >{fmtDate(tx.date)}</span
              >
              <span class="tx-cell tx-cell-doc tx-desktop-only"
                >{tx.documentNo || "—"}</span
              >
              <span class="tx-cell tx-cell-type tx-desktop-only"
                >{typeLabel(tx.type)}</span
              >
              <span class="tx-cell tx-cell-cust tx-desktop-only"
                >{tx.customerNo || "—"}</span
              >
              <span class="tx-cell tx-cell-name tx-desktop-only"
                >{tx.customerName || "—"}</span
              >
              <!-- amount: shared, positions itself in mobile flex or desktop grid -->
              <span class="tx-card-amount {amtClass(tx.amount)}">
                {toNum(tx.amount) < 0
                  ? ""
                  : toNum(tx.amount) > 0
                    ? "+"
                    : ""}₹{fmt(Math.abs(toNum(tx.amount)))}
              </span>
            </div>
          {/each}
        </div>

        <!-- Filtered count hint -->
        {#if hasActiveFilters && filteredRows.length < allRows.length}
          <p class="tx-filter-hint">
            Showing {filteredRows.length} of {allRows.length} loaded ·
            <button class="tx-link" onclick={resetFilters}>Clear filters</button
            >
          </p>
        {/if}
      {/if}
    {/if}

    <!-- ── Sentinel / load-more spinner ──────────────────── -->
    <div class="tx-sentinel" bind:this={sentinel}>
      {#if loadingMore}
        <div class="tx-loader" in:fade={{ duration: 100 }}>
          <Icon name="loader-2" class="tx-loader-icon spinning" />
          <span>Loading more…</span>
        </div>
      {:else if !hasMore && initialLoaded && allRows.length > 0 && !loading}
        <p class="tx-end-hint">All {allRows.length} transactions loaded</p>
      {/if}
    </div>
  </div>
</div>

<style>
  /* ── Page shell ──────────────────────────────────────────── */
  .tx-page {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    display: flex;
    flex-direction: column;
    padding-bottom: 5rem;
  }

  /* ── Toolbar ─────────────────────────────────────────────── */
  .tx-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.5rem;
    padding: 0.625rem 1rem;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    position: sticky;
    top: 0;
    z-index: 20;
  }

  .tx-summary-chip {
    flex-shrink: 0;
  }

  .tx-chip-label {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--muted-foreground);
  }

  .tx-toolbar-right {
    display: flex;
    align-items: center;
    gap: 0.375rem;
  }

  .tx-icon-btn {
    position: relative;
    width: 1.875rem;
    height: 1.875rem;
    border-radius: 50%;
    border: 1px solid var(--border);
    background: transparent;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    color: var(--muted-foreground);
    transition:
      background 0.15s,
      color 0.15s,
      border-color 0.15s;
    flex-shrink: 0;
  }

  .tx-icon-btn:hover {
    background: var(--muted);
    color: var(--foreground);
  }
  .tx-icon-btn:active {
    transform: scale(0.9);
  }
  .tx-icon-btn:disabled {
    opacity: 0.4;
    cursor: not-allowed;
  }
  .tx-icon-btn--active {
    border-color: var(--primary);
    color: var(--primary);
  }

  .tx-filter-dot {
    position: absolute;
    top: 0.2rem;
    right: 0.2rem;
    width: 0.375rem;
    height: 0.375rem;
    border-radius: 50%;
    background: var(--primary);
  }

  :global(.tx-toolbar-icon) {
    width: 0.85rem;
    height: 0.85rem;
    color: inherit;
  }

  /* Sort group */
  .tx-sort-group {
    display: flex;
    align-items: center;
    gap: 0;
    border: 1px solid var(--border);
    border-radius: var(--radius-lg, 0.5rem);
    overflow: hidden;
  }

  .tx-sort-btn {
    display: flex;
    align-items: center;
    gap: 0.2rem;
    padding: 0.25rem 0.55rem;
    background: transparent;
    border: none;
    border-right: 1px solid var(--border);
    font-size: 0.625rem;
    font-weight: 600;
    cursor: pointer;
    color: var(--muted-foreground);
    transition:
      background 0.12s,
      color 0.12s;
    white-space: nowrap;
  }

  .tx-sort-btn:last-child {
    border-right: none;
  }
  .tx-sort-btn:hover {
    background: var(--muted);
    color: var(--foreground);
  }
  .tx-sort-btn--active {
    background: var(--muted);
    color: var(--foreground);
  }

  :global(.tx-sort-arrow) {
    width: 0.55rem;
    height: 0.55rem;
    color: inherit;
  }

  /* ── Filter panel ────────────────────────────────────────── */
  .tx-filter-panel {
    padding: 0.75rem 1rem;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .tx-filter-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0.625rem;
  }

  @media (min-width: 640px) {
    .tx-filter-grid {
      grid-template-columns: repeat(4, 1fr);
    }
  }

  .tx-filter-field {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .tx-filter-field--full {
    grid-column: 1 / -1;
  }

  .tx-filter-label {
    font-size: 0.575rem;
    font-weight: 700;
    letter-spacing: 0.07em;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }

  .tx-input-wrap {
    position: relative;
    display: flex;
    align-items: center;
  }

  :global(.tx-input-icon) {
    position: absolute;
    left: 0.5rem;
    width: 0.7rem;
    height: 0.7rem;
    color: var(--muted-foreground);
    pointer-events: none;
  }

  .tx-input {
    width: 100%;
    padding: 0.35rem 0.5rem 0.35rem 1.5rem;
    font-size: 0.725rem;
    border: 1px solid var(--border);
    border-radius: calc(var(--radius-lg) - 2px);
    background: var(--background);
    color: var(--foreground);
    transition: border-color 0.15s;
    outline: none;
  }

  .tx-input--date {
    padding-left: 0.5rem;
  }

  .tx-input:focus {
    border-color: var(--ring);
  }

  /* Chip group */
  .tx-chip-group {
    display: flex;
    flex-wrap: wrap;
    gap: 0.3rem;
  }

  .tx-chip {
    padding: 0.2rem 0.6rem;
    font-size: 0.625rem;
    font-weight: 600;
    border: 1px solid var(--border);
    border-radius: 9999px;
    background: transparent;
    cursor: pointer;
    color: var(--muted-foreground);
    transition: all 0.12s;
    white-space: nowrap;
  }

  .tx-chip:hover {
    background: var(--muted);
    color: var(--foreground);
  }
  .tx-chip--active {
    background: var(--primary);
    color: oklch(from var(--primary) 1 0 h);
    border-color: var(--primary);
  }

  .tx-reset-btn {
    align-self: flex-start;
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    font-size: 0.625rem;
    font-weight: 600;
    color: var(--muted-foreground);
    background: none;
    border: 1px solid var(--border);
    border-radius: 9999px;
    padding: 0.2rem 0.6rem;
    cursor: pointer;
    transition: all 0.12s;
  }

  .tx-reset-btn:hover {
    color: var(--foreground);
    border-color: var(--ring);
  }

  :global(.tx-reset-icon) {
    width: 0.6rem;
    height: 0.6rem;
    color: inherit;
  }

  /* ── Error banner ────────────────────────────────────────── */
  .tx-error-banner {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    margin: 0.75rem 1rem;
    padding: 0.6rem 0.875rem;
    border-radius: var(--radius-lg);
    background: color-mix(in oklch, var(--destructive) 10%, transparent);
    border: 1px solid color-mix(in oklch, var(--destructive) 25%, transparent);
    font-size: 0.75rem;
    color: var(--destructive);
  }

  :global(.tx-error-icon) {
    width: 0.875rem;
    height: 0.875rem;
    flex-shrink: 0;
    color: var(--destructive);
  }

  .tx-error-retry {
    margin-left: auto;
    font-size: 0.625rem;
    font-weight: 700;
    color: var(--destructive);
    background: none;
    border: 1px solid currentColor;
    border-radius: 9999px;
    padding: 0.15rem 0.5rem;
    cursor: pointer;
  }

  /* ── List wrapper ────────────────────────────────────────── */
  .tx-list-wrap {
    padding: 0.75rem 0.875rem;
    max-width: 52rem;
    margin: 0 auto;
    width: 100%;
  }

  /* ── Skeleton ────────────────────────────────────────────── */
  .tx-skeleton-list {
    display: flex;
    flex-direction: column;
    gap: 0;
  }

  .tx-skeleton-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    padding: 0.75rem 0;
    border-bottom: 1px solid var(--border);
    animation: pulse 1.6s ease-in-out infinite;
  }

  .tx-skel-left {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    flex: 1;
  }

  :global(.tx-skel-doc) {
    height: 0.75rem;
    width: min(10rem, 60%);
    border-radius: 0.25rem;
  }
  :global(.tx-skel-meta) {
    height: 0.55rem;
    width: min(7rem, 45%);
    border-radius: 0.2rem;
  }
  :global(.tx-skel-amt) {
    height: 0.75rem;
    width: 4.5rem;
    border-radius: 0.25rem;
    flex-shrink: 0;
  }

  @keyframes pulse {
    0%,
    100% {
      opacity: 1;
    }
    50% {
      opacity: 0.5;
    }
  }

  /* ── Responsive Utilities ────────────────────────────────── */
  .tx-desktop-only {
    display: none !important;
  }
  .tx-mobile-only {
    display: block;
  }

  @media (min-width: 640px) {
    .tx-desktop-only {
      display: block !important;
    }
    .tx-cell.tx-desktop-only {
      display: flex !important;
      align-items: center;
    } /* For grid cells */
    .tx-mobile-only {
      display: none !important;
    }
  }

  /* ── Transaction list & headers ────────────────────────────── */
  .tx-list {
    display: flex;
    flex-direction: column;
    gap: 0;
  }

  .tx-col-header {
    display: none;
    position: sticky;
    top: 3.125rem; /* Height of toolbar */
    z-index: 10;
    background: var(--card);
    border-bottom: 2px solid var(--border);
    padding: 0.75rem 0.5rem;
    margin: 0 -0.5rem;
  }

  @media (min-width: 640px) {
    .tx-col-header {
      display: grid;
      grid-template-columns: 1.2fr 1.5fr 1fr 1.2fr 1.5fr 1fr 1fr;
      gap: 1rem;
    }
  }

  .tx-col {
    font-size: 0.625rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--muted-foreground);
  }

  .tx-col-amt,
  .tx-col-bal {
    text-align: right;
  }

  .tx-card {
    display: flex;
    align-items: center;
    gap: 0.625rem;
    padding: 0.75rem 0;
    border-bottom: 1px solid var(--border);
    transition:
      background 0.12s,
      padding 0.12s;
  }

  @media (min-width: 640px) {
    .tx-card {
      display: grid;
      grid-template-columns: 1.2fr 1.5fr 1fr 1.2fr 1.5fr 1fr 1fr;
      gap: 1rem;
      padding: 0.875rem 0.5rem;
      margin: 0 -0.5rem;
      align-items: center;
    }
  }

  .tx-card:last-child {
    border-bottom: none;
  }
  .tx-card:hover {
    background: color-mix(in oklch, var(--muted) 60%, transparent);
    border-radius: var(--radius-lg);
    padding-left: 0.5rem;
    padding-right: 0.5rem;
  }

  /* ── Cells & Content ─────────────────────────────────────── */
  .tx-cell {
    font-size: 0.8125rem;
    color: var(--foreground);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .tx-cell-doc {
    font-weight: 600;
  }
  .tx-cell-type {
    color: var(--muted-foreground);
    font-size: 0.75rem;
  }
  .tx-cell-cust {
    font-weight: 500;
    opacity: 0.8;
  }
  .tx-cell-name {
    font-weight: 500;
  }
  .tx-cell-bal {
    font-weight: 700;
    text-align: right;
    font-variant-numeric: tabular-nums;
    letter-spacing: 0.02em;
    min-width: 6ch;
  }

  .tx-card-badge {
    flex-shrink: 0;
    width: 2rem;
    height: 2rem;
    border-radius: calc(var(--radius-lg) - 2px);
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.tx-card-badge-icon) {
    width: 0.85rem;
    height: 0.85rem;
    color: var(--muted-foreground);
  }

  .tx-card-info {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
  }

  .tx-card-doc {
    font-size: 0.825rem;
    font-weight: 600;
    color: var(--foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tx-card-meta {
    font-size: 0.675rem;
    color: var(--muted-foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tx-card-cust {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--foreground);
    opacity: 0.55;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tx-card-name {
    font-size: 0.7rem;
    color: var(--foreground);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tx-card-bal {
    font-size: 0.75rem;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
    letter-spacing: 0.02em;
  }

  .tx-card-amount {
    font-size: 0.85rem;
    font-weight: 700;
    flex-shrink: 0;
    text-align: right;
  }

  @media (min-width: 640px) {
    .tx-card-amount {
      font-size: 0.875rem;
    }
  }

  /* Amount colours */
  .tx-credit {
    color: oklch(0.52 0.16 160);
  }
  .tx-debit {
    color: oklch(0.52 0.2 25);
  }
  .tx-zero {
    color: var(--muted-foreground);
  }

  /* ── Hints ───────────────────────────────────────────────── */
  .tx-filter-hint {
    text-align: center;
    font-size: 0.675rem;
    color: var(--muted-foreground);
    padding: 0.5rem 0;
  }

  .tx-link {
    background: none;
    border: none;
    color: var(--primary);
    font-size: inherit;
    cursor: pointer;
    font-weight: 600;
    padding: 0;
  }

  /* ── Empty state ─────────────────────────────────────────── */
  .tx-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
    padding: 3rem 1rem;
    opacity: 0.6;
  }

  :global(.tx-empty-icon) {
    width: 2.5rem;
    height: 2.5rem;
    color: var(--muted-foreground);
  }

  .tx-empty-title {
    font-size: 0.925rem;
    font-weight: 700;
    color: var(--foreground);
  }

  .tx-empty-sub {
    font-size: 0.75rem;
    color: var(--muted-foreground);
    text-align: center;
  }

  /* ── Sentinel / infinite scroll ───────────────────────────── */
  .tx-sentinel {
    height: 3rem;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-top: 0.5rem;
  }

  .tx-loader {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    font-size: 0.7rem;
    color: var(--muted-foreground);
  }

  :global(.tx-loader-icon) {
    width: 1rem;
    height: 1rem;
    color: var(--muted-foreground);
  }

  .tx-end-hint {
    font-size: 0.625rem;
    color: var(--muted-foreground);
    text-align: center;
    letter-spacing: 0.04em;
  }

  /* ── Spinner ─────────────────────────────────────────────── */
  :global(.spinning) {
    animation: spin 0.9s linear infinite;
  }

  @keyframes spin {
    to {
      transform: rotate(360deg);
    }
  }
</style>
