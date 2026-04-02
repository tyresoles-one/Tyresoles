<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import { fade, slide } from "svelte/transition";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import {
    GetMyCustomersDocument,
    type GetMyCustomersQuery,
    type CustomerSortInput,
  } from "$lib/services/graphql/generated/types.js";
  import { getUser } from "$lib/stores/auth";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { cn } from "$lib/utils";

  // ── Entity context ─────────────────────────────────────────
  const user = getUser();
  const entityType = user?.entityType ?? null;
  const entityCode = user?.entityCode ?? null;
  const respCenter = user?.respCenter ?? null;
  const department = user?.department ?? null;

  // ── Types ──────────────────────────────────────────────────
  type CustomerNode = NonNullable<
    NonNullable<GetMyCustomersQuery["myCustomers"]>["nodes"]
  >[number];

  type SortField = "no" | "name" | "balance";
  type SortDir = "ASC" | "DESC";

  // ── Fetch state ────────────────────────────────────────────
  const PAGE_SIZE = 20;
  let allRows = $state<CustomerNode[]>([]);
  let totalCount = $state(0);
  let cursor = $state<string | null>(null);
  let hasMore = $state(false);
  let loading = $state(false);
  let loadingMore = $state(false);
  let error = $state<string | null>(null);
  let initialLoaded = $state(false);

  // ── Selection / Card state ─────────────────────────────────
  let selectedNo = $state<string | null>(null);
  let selectedCustomer = $derived(allRows.find(c => c.no === selectedNo) ?? null);

  // ── Sort ───────────────────────────────────────────────────
  let sortField = $state<SortField>("name");
  let sortDir = $state<SortDir>("ASC");

  function buildOrder(): CustomerSortInput[] {
    return [{ [sortField]: sortDir }];
  }

  async function fetchPage(afterCursor: string | null, append: boolean) {
    if (append) loadingMore = true;
    else { loading = true; error = null; }
    try {
      const res = await graphqlQuery<GetMyCustomersQuery>(
        GetMyCustomersDocument,
        {
          variables: {
            entityType,
            entityCode,
            respCenter,
            department,
            first: PAGE_SIZE,
            after: afterCursor ?? undefined,
            order: buildOrder(),
          },
          skipCache: true,
        }
      );
      if (res.success && res.data?.myCustomers) {
        const { nodes, pageInfo, totalCount: tc } = res.data.myCustomers;
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
        error = res.error ?? "Failed to load customers.";
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
      { threshold: 0.1 }
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
  let filterName = $state("");
  let filterNo = $state("");
  let filterCity = $state("");
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

  // ── Filtered derived rows ──────────────────────────────────
  let filteredRows = $derived((() => {
    let rows = allRows;
    if (filterName.trim()) {
      const q = filterName.trim().toLowerCase();
      rows = rows.filter(r => r.name?.toLowerCase().includes(q));
    }
    if (filterNo.trim()) {
      const q = filterNo.trim().toLowerCase();
      rows = rows.filter(r => r.no?.toLowerCase().includes(q));
    }
    if (filterCity.trim()) {
      const q = filterCity.trim().toLowerCase();
      rows = rows.filter(r => r.city?.toLowerCase().includes(q));
    }
    return rows;
  })());

  // ── Sort toggle ────────────────────────────────────────────
  function setSort(field: SortField) {
    if (sortField === field) {
      sortDir = sortDir === "ASC" ? "DESC" : "ASC";
    } else {
      sortField = field;
      sortDir = field === "balance" ? "DESC" : "ASC";
    }
    reload();
  }

  function resetFilters() {
    filterName = "";
    filterNo = "";
    filterCity = "";
  }

  let hasActiveFilters = $derived(filterName || filterNo || filterCity);

  function amtClass(amount: unknown) {
    const n = toNum(amount);
    if (n < 0) return "text-emerald-600 dark:text-emerald-400";
    if (n > 0) return "text-rose-600 dark:text-rose-400";
    return "text-muted-foreground";
  }

  function getGSTStatusLabel(status: number) {
    switch(status) {
      case 1: return "Active";
      case 2: return "Cancelled";
      case 3: return "Suspended";
      default: return "Unknown";
    }
  }
</script>

<svelte:head>
  <title>My Customers</title>
</svelte:head>

<div class="cust-page">
  <PageHeading backHref="/" icon="users">
    {#snippet title()}My Customers{/snippet}
  </PageHeading>

  <!-- ── Toolbar ───────────────────────────────────────────── -->
  <div class="cust-toolbar">
    <div class="cust-summary-chip">
      {#if loading}
        <span class="cust-chip-label">Loading…</span>
      {:else if initialLoaded}
        <span class="cust-chip-label">{totalCount.toLocaleString("en-IN")} customers</span>
      {/if}
    </div>

    <div class="cust-toolbar-right">
      <button
        class="cust-icon-btn {showFilters ? 'cust-icon-btn--active' : ''}"
        onclick={() => (showFilters = !showFilters)}
        title="Filters"
      >
        <Icon name="sliders-horizontal" class="cust-toolbar-icon" />
        {#if hasActiveFilters}
          <span class="cust-filter-dot"></span>
        {/if}
      </button>

      <div class="cust-sort-group">
        {#each ([['no', 'ID'], ['name', 'Name'], ['balance', 'Balance']] as const) as [field, label]}
          <button
            class="cust-sort-btn {sortField === field ? 'cust-sort-btn--active' : ''}"
            onclick={() => setSort(field)}
          >
            {label}
            {#if sortField === field}
              <Icon name={sortDir === 'DESC' ? 'arrow-down' : 'arrow-up'} class="cust-sort-arrow" />
            {/if}
          </button>
        {/each}
      </div>

      <button class="cust-icon-btn" onclick={reload} disabled={loading}>
        <Icon name="refresh-cw" class="cust-toolbar-icon {loading ? 'spinning' : ''}" />
      </button>
    </div>
  </div>

  <!-- ── Filter panel ───────────────────────────────────────── -->
  {#if showFilters}
    <div class="cust-filter-panel" in:slide={{ duration: 200 }} out:slide={{ duration: 150 }}>
      <div class="cust-filter-grid">
        <div class="cust-filter-field">
          <label class="cust-filter-label">Customer Name</label>
          <div class="cust-input-wrap">
            <Icon name="search" class="cust-input-icon" />
            <input class="cust-input" type="text" placeholder="Search name..." bind:value={filterName} />
          </div>
        </div>
        <div class="cust-filter-field">
          <label class="cust-filter-label">Customer ID</label>
          <div class="cust-input-wrap">
            <Icon name="hash" class="cust-input-icon" />
            <input class="cust-input" type="text" placeholder="Search ID..." bind:value={filterNo} />
          </div>
        </div>
        <div class="cust-filter-field">
          <label class="cust-filter-label">City</label>
          <div class="cust-input-wrap">
            <Icon name="map-pin" class="cust-input-icon" />
            <input class="cust-input" type="text" placeholder="Search city..." bind:value={filterCity} />
          </div>
        </div>
      </div>
      {#if hasActiveFilters}
        <button class="cust-reset-btn" onclick={resetFilters}>
          <Icon name="x" class="w-2.5 h-2.5" /> Clear filters
        </button>
      {/if}
    </div>
  {/if}

  <!-- ── Content ────────────────────────────────────────────── -->
  <div class="cust-main">
    <div class="cust-list-wrap">
      {#if loading && !loadingMore}
        <div class="space-y-4">
          {#each { length: 5 } as _}
            <div class="h-16 w-full animate-pulse rounded-lg bg-muted/40"></div>
          {/each}
        </div>
      {:else if error}
        <div class="flex flex-col items-center justify-center py-12 text-center">
          <Icon name="alert-circle" class="h-10 w-10 text-destructive mb-4" />
          <p class="text-sm font-medium text-destructive">{error}</p>
          <button class="mt-4 text-xs font-semibold underline" onclick={reload}>Try Again</button>
        </div>
      {:else if filteredRows.length === 0}
        <div class="flex flex-col items-center justify-center py-12 text-center">
          <Icon name="inbox" class="h-10 w-10 text-muted-foreground mb-4 opacity-20" />
          <p class="text-sm font-medium text-muted-foreground">No customers found</p>
        </div>
      {:else}
        <div class="cust-list">
          {#each filteredRows as cust (cust.no)}
            <button
              class="cust-row {selectedNo === cust.no ? 'cust-row--selected' : ''}"
              onclick={() => selectedNo = selectedNo === cust.no ? null : cust.no}
            >
              <div class="cust-row-info">
                <span class="cust-row-no">{cust.no}</span>
                <span class="cust-row-name">{cust.name}</span>
                <span class="cust-row-city">{cust.city || "—"}</span>
              </div>
              <div class="cust-row-balance {amtClass(cust.balance)}">
                ₹{fmt(toNum(cust.balance))}
              </div>
            </button>

            {#if selectedNo === cust.no}
              <div class="cust-detail-card" in:slide={{ duration: 250 }}>
                <div class="cust-detail-grid">
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">Address</span>
                    <span class="cust-detail-value">
                      {cust.address || "—"}<br/>
                      {cust.address2 || ""}
                    </span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">City / Pincode</span>
                    <span class="cust-detail-value">{cust.city || "—"} {cust.postCode || ""}</span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">State</span>
                    <span class="cust-detail-value">{cust.stateCode || "—"}</span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">Area / Dealer</span>
                    <span class="cust-detail-value">{cust.areaCode || "—"} / {cust.dealerCode || "—"}</span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">GST Details</span>
                    <span class="cust-detail-value">
                      <span class="font-mono text-[0.7rem]">{cust.gstRegistrationNo || "UNREGISTERED"}</span>
                      <span class="ml-2 text-[0.6rem] px-1.5 py-0.5 rounded-full bg-muted font-bold">
                        {getGSTStatusLabel(toNum(cust.gstStatus))}
                      </span>
                    </span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">PAN / Price Group</span>
                    <span class="cust-detail-value">{cust.panNo || "—"} / {cust.customerPriceGroup || "—"}</span>
                  </div>
                  <div class="cust-detail-item">
                    <span class="cust-detail-label">Current Balance</span>
                    <span class="cust-detail-value font-bold {amtClass(cust.balance)}">₹{fmt(toNum(cust.balance))}</span>
                  </div>
                </div>
              </div>
            {/if}
          {/each}
        </div>
      {/if}

      <div bind:this={sentinel} class="cust-sentinel">
        {#if loadingMore}
          <div class="flex items-center justify-center p-4">
            <Icon name="loader-2" class="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        {/if}
      </div>
    </div>
  </div>
</div>

<style>
  .cust-page {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    display: flex;
    flex-direction: column;
  }

  .cust-toolbar {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.75rem 1rem;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    position: sticky;
    top: 0;
    z-index: 20;
  }

  .cust-chip-label {
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--muted-foreground);
  }

  .cust-toolbar-right {
    display: flex;
    align-items: center;
    gap: 0.5rem;
  }

  .cust-icon-btn {
    position: relative;
    width: 2rem;
    height: 2rem;
    border-radius: 50%;
    border: 1px solid var(--border);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--muted-foreground);
    transition: all 0.2s;
  }

  .cust-icon-btn:hover { background: var(--muted); color: var(--foreground); }
  .cust-icon-btn--active { border-color: var(--primary); color: var(--primary); }

  .cust-filter-dot {
    position: absolute;
    top: 0.25rem;
    right: 0.25rem;
    width: 0.4rem;
    height: 0.4rem;
    background: var(--primary);
    border-radius: 50%;
  }

  .cust-sort-group {
    display: flex;
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    overflow: hidden;
  }

  .cust-sort-btn {
    padding: 0.25rem 0.6rem;
    font-size: 0.65rem;
    font-weight: 600;
    color: var(--muted-foreground);
    border-right: 1px solid var(--border);
    display: flex;
    align-items: center;
    gap: 0.25rem;
  }

  .cust-sort-btn:last-child { border-right: none; }
  .cust-sort-btn--active { background: var(--muted); color: var(--foreground); }

  .cust-filter-panel {
    padding: 1rem;
    background: var(--card);
    border-bottom: 1px solid var(--border);
  }

  .cust-filter-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
    gap: 1rem;
    margin-bottom: 1rem;
  }

  .cust-filter-field {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

  .cust-filter-label {
    font-size: 0.6rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    color: var(--muted-foreground);
  }

  .cust-input-wrap {
    position: relative;
  }

  :global(.cust-input-icon) {
    position: absolute;
    left: 0.5rem;
    top: 50%;
    transform: translateY(-50%);
    width: 0.8rem;
    height: 0.8rem;
    color: var(--muted-foreground);
  }

  .cust-input {
    width: 100%;
    padding: 0.4rem 0.6rem 0.4rem 1.75rem;
    font-size: 0.75rem;
    border: 1px solid var(--border);
    border-radius: 0.375rem;
    background: var(--background);
    outline: none;
  }

  .cust-reset-btn {
    font-size: 0.65rem;
    font-weight: 600;
    display: flex;
    align-items: center;
    gap: 0.35rem;
    color: var(--muted-foreground);
    padding: 0.25rem 0.75rem;
    border-radius: 999px;
    border: 1px solid var(--border);
  }

  .cust-main {
    flex: 1;
    overflow-y: auto;
    padding: 1rem;
  }

  .cust-list-wrap {
    max-width: 600px;
    margin: 0 auto;
    width: 100%;
  }

  .cust-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .cust-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0.875rem 1rem;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: 0.75rem;
    transition: all 0.2s;
    text-align: left;
    width: 100%;
  }

  .cust-row:hover { border-color: var(--ring); transform: translateY(-1px); box-shadow: 0 4px 12px -4px rgba(0,0,0,0.1); }
  .cust-row--selected { border-color: var(--primary); background: var(--muted)/30; border-bottom-left-radius: 0; border-bottom-right-radius: 0; }

  .cust-row-info {
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
  }

  .cust-row-no {
    font-size: 0.625rem;
    font-weight: 700;
    color: var(--primary);
    opacity: 0.8;
  }

  .cust-row-name {
    font-size: 0.875rem;
    font-weight: 600;
  }

  .cust-row-city {
    font-size: 0.7rem;
    color: var(--muted-foreground);
  }

  .cust-row-balance {
    font-size: 0.9rem;
    font-weight: 700;
    font-variant-numeric: tabular-nums;
  }

  .cust-detail-card {
    background: var(--card);
    border: 1px solid var(--primary);
    border-top: none;
    border-bottom-left-radius: 0.75rem;
    border-bottom-right-radius: 0.75rem;
    padding: 1rem;
    margin-top: -0.5rem;
    margin-bottom: 0.5rem;
    box-shadow: inset 0 2px 4px rgba(0,0,0,0.02);
  }

  .cust-detail-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 1rem;
  }

  .cust-detail-item {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .cust-detail-label {
    font-size: 0.55rem;
    font-weight: 700;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }

  .cust-detail-value {
    font-size: 0.7rem;
    font-weight: 500;
    line-height: 1.4;
  }

  :global(.spinning) { animation: spin 1s linear infinite; }
  @keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>
