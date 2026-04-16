<script lang="ts">
  import { onMount } from "svelte";
  import { fade, slide, fly } from "svelte/transition";
  import { flip } from "svelte/animate";
  import { cn } from "$lib/utils";
  import { authStore, getUser } from "$lib/stores/auth";
  import { toast } from "$lib/components/venUI/toast";
  import {
    today,
    getLocalTimeZone,
    toCalendarDate,
    parseDate,
    parseDateTime,
    fromDate,
  } from "@internationalized/date";

  import * as Tabs from "$lib/components/ui/tabs";
  import * as Accordion from "$lib/components/ui/accordion";
  import * as Popover from "$lib/components/ui/popover";
  import * as Table from "$lib/components/ui/table";
  import { Button } from "$lib/components/ui/button";
  import { Toggle } from "$lib/components/ui/toggle";
  import { Badge } from "$lib/components/ui/badge";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { Icon } from "$lib/components/venUI/icon";
  import { DatePicker } from "$lib/components/venUI/date-picker";

  import { fetchDashboard } from "./classic-dashboard/api";
  import {
    prepareBusinessLocations,
    prepareData,
  } from "./classic-dashboard/logic";
  import {
    VIEWS,
    type DashboardData,
    type FetchParams,
    type BusinessLocation,
    type ActiveCustomer,
    type ProductSale,
    type Sales,
    type CustomerSales,
    type CollectionData,
    type SalesmanSale,
    type ViewName,
  } from "./classic-dashboard/types";

  const user = getUser();
  const restrictedProducts = [
    "Scrap Sale",
    "Intercompany (Ecomile/Casing/Retd)",
    "Exchange Tyres",
  ];
  const isRestrictedUser = $derived.by(() => {
    const u = $authStore.user;
    if (!u) return false;
    const ut = u.userType?.toUpperCase();
    const uid = u.userId?.toUpperCase();
    return (
      ut === "DEALER" || ut === "SALES" || uid === "DEALER" || uid === "SALES"
    );
  });

  let loading = $state(false);
  let filterOpen = $state(false);
  let activeView = $state<ViewName>("Product Sale");
  let activeTab = $state<string>("view");
  let rawData = $state<DashboardData>();
  let locations = $state<BusinessLocation[]>([]);
  let dbrdData = $state<DashboardData>();
  let popoverRecords = $state<any[]>([]);
  /** Inner Salesperson/Dealer tab value per business (avoids duplicate product keys). */
  let salesInnerTabByBiz = $state<Record<string, string>>({});
  let activeController: AbortController | null = null;
  let fetchId = $state(0);

  const workDate = $derived($authStore.user?.workDate);
  const ref = $derived.by(() => {
    if (!workDate) return today(getLocalTimeZone());
    try {
      if (typeof workDate === "string") {
        if (workDate.includes("T")) {
          try {
            return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
          } catch {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        }
        try {
          return parseDate(workDate);
        } catch {
          return toCalendarDate(
            fromDate(new Date(workDate), getLocalTimeZone()),
          );
        }
      }
      return today(getLocalTimeZone());
    } catch {
      return today(getLocalTimeZone());
    }
  });

  const showWorkDateBadge = $derived(
    workDate && ref.toString() !== today(getLocalTimeZone()).toString(),
  );

  let dateRange = $state<{ start: any; end: any }>({
    start: undefined,
    end: undefined,
  });

  const currentViewDef = $derived(VIEWS.find((v) => v.label === activeView)!);
  const filterSummary = $derived.by(() => {
    if (!dateRange.start || !dateRange.end) return "";
    const from = fmtDate(dateRange.start);
    const to = fmtDate(dateRange.end);
    if (!from || !to) return "";
    return `${from} .. ${to}`;
  });

  $effect(() => {
    if (!rawData) return;
    const data = rawData;
    const view = activeView;
    let cancelled = false;

    // Using setTimeout to defer initial heavy processing so the UI remains responsive
    const timer = setTimeout(() => {
      if (cancelled) return;
      try {
        const locs = prepareBusinessLocations(data, view);
        if (cancelled) return;
        locations = locs;
      } finally {
        if (!cancelled) loading = false;
      }
    }, 0);
    return () => {
      cancelled = true;
      clearTimeout(timer);
    };
  });

  $effect(() => {
    // Whenever rawData or locations change, recompute the dbrdData
    if (rawData && locations.length > 0) {
      dbrdData = prepareData(rawData, locations);
    }
  });

  // When user opens the View tab: show loading immediately and clear current data; refetch if date range is set
  let prevActiveTab = $state<string>("view");
  $effect(() => {
    const current = activeTab;
    const justOpenedView = prevActiveTab !== "view" && current === "view";
    prevActiveTab = current;
    if (!justOpenedView) return;
    loading = true;
    rawData = undefined;
    dbrdData = undefined;
    const from = dateRange.start ? toIso(dateRange.start) : "";
    const to = dateRange.end ? toIso(dateRange.end) : "";
    if (from && to) {
      onSubmit();
    } else {
      loading = false;
    }
  });

  function toIso(date: unknown): string {
    if (!date) return "";
    if (typeof date === "string") return new Date(date).toISOString();
    if (
      date &&
      typeof date === "object" &&
      "toDate" in date &&
      typeof (date as any).toDate === "function"
    ) {
      return (date as any).toDate(getLocalTimeZone()).toISOString();
    }
    return "";
  }

  function fmtDate(val: any): string {
    if (!val) return "";
    let d: Date;
    if (
      val &&
      typeof val === "object" &&
      "toDate" in val &&
      typeof (val as any).toDate === "function"
    ) {
      d = (val as any).toDate(getLocalTimeZone());
    } else {
      d = new Date(val.toString());
    }
    const day = String(d.getDate()).padStart(2, "0");
    const month = d
      .toLocaleDateString("en-US", { month: "short" })
      .toUpperCase();
    const year = String(d.getFullYear()).slice(-2);
    return `${day}-${month}-${year}`;
  }

  /** Build dashboard params: date range + respCenters from auth (locations or user.respCenter) + user context. */
  function getParams(): Omit<FetchParams, "reportName"> {
    const from = dateRange.start ? toIso(dateRange.start) : "";
    const to = dateRange.end ? toIso(dateRange.end) : "";
    const locs = $authStore.locations;
    const respCenters =
      locs && locs.length > 0
        ? locs
            .filter((l) => (l as any).sale === 1)
            .map((l) => l.code)
            .filter(Boolean)
        : user?.respCenter
          ? [user.respCenter]
          : [];
    return {
      from,
      to,
      respCenters,
      customers: [],
      dealers: [],
      areas: [],
      regions: [],
      entityType: user?.entityType ?? undefined,
      entityCode: user?.entityCode ?? undefined,
      entityDepartment: user?.department ?? undefined,
    };
  }

  function handleDateChange(val: any) {
    if (!val) return;
    dateRange = val;
  }

  async function onSubmit() {
    const from = dateRange.start ? toIso(dateRange.start) : "";
    const to = dateRange.end ? toIso(dateRange.end) : "";
    if (!from || !to) {
      toast.error("Please select a date range");
      return;
    }

    // Cancel any previous in-flight request
    if (activeController) activeController.abort();
    activeController = new AbortController();
    const { signal } = activeController;
    const currentFetchId = ++fetchId;

    loading = true;
    activeTab = "view";
    const viewValue =
      VIEWS.find((v) => v.label === activeView)?.value ?? "ProductSale";
    const params = getParams();

    const res = await fetchDashboard(
      {
        ...params,
        reportName: viewValue,
      },
      signal,
    );

    // Ignore stale responses
    if (signal.aborted || currentFetchId !== fetchId) return;

    if (res.success && res.data) {
      if (isRestrictedUser && res.data.data) {
        res.data.data = res.data.data.filter(
          (d: any) => !restrictedProducts.includes(d.product),
        );
      }
      rawData = res.data;
      // loading is cleared when deferred preparation finishes in $effect
    } else {
      if (res.error) toast.error(res.error || "Failed to load dashboard");
      loading = false;
    }
  }

  async function switchView(view: ViewName) {
    activeView = view;
    // Clear stale data immediately so UI never shows wrong tab's data
    rawData = undefined;
    dbrdData = undefined;
    locations = [];
    const from = dateRange.start ? toIso(dateRange.start) : "";
    const to = dateRange.end ? toIso(dateRange.end) : "";
    if (from && to) {
      await onSubmit();
    }
  }

  onMount(() => {
    // Initialize date range to this month based on ref
    const start = ref.set({ day: 1 });
    dateRange = { start, end: ref };

    // Auto-fetch after a short delay
    setTimeout(() => {
      if (dateRange.start && dateRange.end && !rawData) {
        onSubmit();
      }
    }, 100);
  });

  function fmtLakhs(text: string): { whole: string; decimal: string } {
    if (text === "-") return { whole: "-", decimal: "" };
    const parts = text.split(".");
    return { whole: parts[0] ?? "-", decimal: parts[1] ?? "00" };
  }

  function fmtNumber(n: number): string {
    if (n === 0) return "-";
    return n.toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }
</script>

<!-- ─── HEADER + COLLAPSIBLE FILTER ─────────────────────── -->
<div class="cd-root h-full w-full">
  <Accordion.Root
    type="single"
    class="w-full bg-card rounded-xl border border-border shadow-sm overflow-hidden cd-accordion-wrap"
    bind:value={activeTab}
  >
    <Accordion.Item value="filter" class="border-b-0">
      <Accordion.Trigger
        class="cd-header px-4 py-3 hover:no-underline hover:bg-muted/50 transition-colors group w-full"
      >
        <span class="flex items-center gap-3 flex-1 min-w-0 w-full">
          <div
            class="cd-header-icon w-8 h-8 rounded-lg bg-muted flex items-center justify-center group-hover:bg-primary/10 transition-colors flex-shrink-0"
          >
            <Icon
              name="layout-dashboard"
              class="cd-icon-lg group-hover:text-primary transition-colors"
            />
          </div>
          <div class="w-full min-w-0 flex justify-between items-center">
            <div class="flex flex-col items-center gap-1.5">
              <span class="cd-title">Sales Dashboard</span>
              {#if filterSummary}
                <span class="cd-filter-badge">{filterSummary}</span>
              {/if}
            </div>
          </div>
        </span>
      </Accordion.Trigger>
      <Accordion.Content
        class="px-2 pb-3 pt-2 sm:px-4 sm:pb-4 border-t border-border/50 bg-muted/10"
      >
        <div class="w-full">
          <div
            class="cd-filter-panel"
            in:slide={{ duration: 200 }}
            out:slide={{ duration: 150 }}
          >
            <div class="cd-filter-row">
              <div class="cd-filter-field">
                <span class="cd-label mb-1">Period</span>
                <DatePicker
                  value={dateRange}
                  mode="range"
                  valueType="calendar"
                  placeholder="Select date range"
                  presetKeys="thisMonth,lastMonth,thisQuarter,lastQuarter,thisFinYear,lastFinYear"
                  onValueChange={handleDateChange}
                  workdate={workDate}
                  fiscal
                />
              </div>
              <div class="cd-filter-actions">
                <Button
                  variant="default"
                  class="cd-submit-btn min-w-[120px]"
                  onclick={onSubmit}
                  disabled={loading}
                >
                  {#if loading}
                    <Icon
                      name="loader-circle"
                      class="mr-2 h-4 w-4 animate-spin"
                    />
                  {:else}
                    <Icon name="search" class="mr-2 h-4 w-4" />
                  {/if}
                  <span>Submit</span>
                </Button>
              </div>
            </div>
          </div>
        </div>
      </Accordion.Content>
    </Accordion.Item>
    <!-- VIEW ACCORDION -->
    <Accordion.Item value="view" class="border-b-0">
      <Accordion.Trigger
        class="cd-accordion-trigger px-4 py-3 hover:no-underline hover:bg-muted/50 transition-colors group {activeTab ===
        'view'
          ? 'cd-accordion-trigger--active'
          : ''}"
      >
        <span class="flex items-center gap-3">
          <div
            class="w-8 h-8 rounded-lg bg-muted flex items-center justify-center group-hover:bg-primary/10 transition-colors"
          >
            <Icon
              name="chart-column"
              class="cd-icon-sm group-hover:text-primary transition-colors"
            />
          </div>
          <span class="font-semibold text-sm">View Dashboard</span>
        </span>
      </Accordion.Trigger>
      <Accordion.Content
        class="px-2 pb-3 pt-2 sm:px-4 sm:pb-4 border-t border-border/50 bg-muted/10"
      >
        <div class="w-full">
          <Tabs.Root
            class="w-full"
            value={activeView}
            onValueChange={(val) => {
              switchView(val as ViewName);
            }}
          >
            <Tabs.List
              class="w-full justify-start overflow-x-auto h-11 p-1 bg-background border border-border/50 rounded-lg"
            >
              {#each VIEWS as view (view.value)}
                <Tabs.Trigger
                  value={view.label}
                  class="cd-inner-tab flex items-center gap-2 whitespace-nowrap data-[state=active]:shadow-md rounded-md px-3 py-1.5 text-sm font-medium transition-all"
                >
                  <Icon name={view.icon} class="w-3.5 h-3.5 opacity-70" />
                  {view.label}
                </Tabs.Trigger>
              {/each}
            </Tabs.List>

            <div class="mt-1">
              {#if loading}
                <div class="cd-loading-container" in:fade={{ duration: 200 }}>
                  <div class="cd-loading-text">
                    Loading data
                    <div class="cd-loading-dots">
                      <span></span><span></span><span></span>
                    </div>
                  </div>
                  <div class="cd-loading-shimmer"></div>
                </div>
                <div class="cd-skeleton-grid" in:fade={{ duration: 150 }}>
                  {#each { length: 6 } as _}
                    <div class="cd-skeleton-card">
                      <Skeleton class="cd-skel-title" />
                      <Skeleton class="cd-skel-value" />
                      <Skeleton class="cd-skel-bar" />
                      <Skeleton class="cd-skel-bar short" />
                    </div>
                  {/each}
                </div>
              {:else if !dbrdData?.data?.length}
                <div class="cd-empty" in:fade={{ duration: 250 }}>
                  <div class="cd-empty-icon-wrap">
                    <Icon name="chart-column" class="cd-empty-icon" />
                  </div>
                  <h3 class="cd-empty-title">Select Parameters</h3>
                  <p class="cd-empty-desc">
                    Configure your filter parameters and click <strong
                      >Submit</strong
                    > to view sales data insights.
                  </p>
                </div>
              {:else}
                <div in:fade={{ duration: 300 }}>
                  {#if dbrdData.name === "ProductSale"}
                    {@render productSaleView(dbrdData)}
                  {:else if dbrdData.name === "ActiveCustomer"}
                    {@render activeCustomerView(dbrdData)}
                  {:else if dbrdData.name === "Collection"}
                    {@render collectionView(dbrdData)}
                  {:else if dbrdData.name === "SalesmanSale" || dbrdData.name === "DealerSale"}
                    {@render salesPersonView(dbrdData)}
                  {/if}
                </div>
              {/if}
            </div>
          </Tabs.Root>
        </div>
      </Accordion.Content>
    </Accordion.Item>
  </Accordion.Root>
</div>

<!-- ══════════════════════════════════════════════════════ -->
<!-- SNIPPETS                                              -->
<!-- ══════════════════════════════════════════════════════ -->

<!-- ─── BUSINESS LOCATION HEADER ────────────────────── -->
{#snippet businessHeader(loc: BusinessLocation)}
  <div class="cd-biz-header">
    <div class="cd-biz-name">
      <div class="cd-biz-avatar">{loc.business.charAt(0)}</div>
      <span>{loc.business}</span>
    </div>
    {#if loc.locations.length > 1}
      <div class="cd-biz-locs">
        {#each loc.locations as l (l)}
          <Toggle
            variant="outline"
            size="sm"
            class={cn(
              "cd-loc-toggle",
              loc.selections.includes(l) && "cd-loc-toggle--active",
            )}
            pressed={loc.selections.includes(l)}
            onPressedChange={(pressed) => {
              if (pressed) {
                loc.selections = [...loc.selections, l];
              } else {
                loc.selections = loc.selections.filter((s) => s !== l);
              }
            }}
          >
            {l}
          </Toggle>
        {/each}
      </div>
    {/if}
  </div>
{/snippet}

<!-- ─── PRODUCT SALE VIEW ───────────────────────────── -->
{#snippet productSaleView(source: DashboardData)}
  <div class="cd-view-content">
    {#each locations as location, locIdx (`cd-loc-${locIdx}`)}
      {@const prodSales = source.data.filter(
        (d) =>
          d.business === location.business && d.location === location.default,
      ) as ProductSale[]}
      <section class="cd-biz-section" in:fly={{ y: 20, duration: 300 }}>
        {@render businessHeader(location)}
        <div class="cd-products">
          {#each prodSales as prodSale, pi (`cd-ps-${pi}`)}
            <div
              class={cn(
                "cd-product-group",
                prodSale.product === "Grand Total" && "cd-product-group--total",
              )}
            >
              <div class="cd-product-label">
                {#if prodSale.product === "Grand Total"}
                  <Icon name="sigma" class="cd-icon-xs" />
                {/if}
                <span>{prodSale.product}</span>
              </div>
              <div class="cd-sale-cards">
                {#each prodSale.data as sale, si (`cd-sale-${si}`)}
                  {@render saleCard(sale, prodSale.product === "Grand Total")}
                {/each}
              </div>
            </div>
          {/each}
        </div>
      </section>
    {/each}
  </div>
{/snippet}

<!-- ─── SALE METRIC CARD ────────────────────────────── -->
{#snippet saleCard(source: Sales, isTotal: boolean)}
  {@const fmt = fmtLakhs(source.saleText)}
  <div class={cn("cd-metric-card", isTotal && "cd-metric-card--total")}>
    {#if isTotal}
      <div class="cd-metric-period">
        <span class="cd-metric-period-label">{source.label}</span>
        <span class="cd-metric-period-range">{source.dateRange}</span>
      </div>
    {/if}
    <div class="cd-metric-value-wrap">
      {#if fmt.whole === "-"}
        <span class="cd-metric-value cd-metric-value--nil">—</span>
      {:else}
        <span class="cd-metric-value">{fmt.whole}</span>
        <span class="cd-metric-decimal">.{fmt.decimal}</span>
        <span class="cd-metric-unit">{source.saleUnit}</span>
      {/if}
    </div>
    {#if source.items?.length}
      <div class="cd-metric-items">
        {#each source.items as item, i (`cd-item-${i}`)}
          <div
            class={cn(
              "cd-metric-item",
              i === source.items.length - 1 && "cd-metric-item--last",
            )}
          >
            <span class="cd-metric-item-label">{item.label}</span>
            <div class="cd-metric-item-data">
              <span
                class={cn(
                  "cd-metric-item-value",
                  item.value === 0 && "cd-muted-dash",
                )}>{item.value === 0 ? "—" : item.value}</span
              >
              <span class="cd-metric-item-unit">{item.unit}</span>
            </div>
          </div>
        {/each}
      </div>
    {/if}
  </div>
{/snippet}

<!-- ─── ACTIVE CUSTOMER VIEW ────────────────────────── -->
{#snippet activeCustomerView(source: DashboardData)}
  <div class="cd-view-content">
    {#each locations as location, locIdx (`cd-loc-${locIdx}`)}
      {@const custData = source.data.filter(
        (d: any) =>
          d.business === location.business && d.location === location.default,
      ) as ActiveCustomer[]}
      <section class="cd-biz-section" in:fly={{ y: 20, duration: 300 }}>
        {@render businessHeader(location)}
        <div class="cd-products">
          {#each custData as group, gi (`cd-ac-${gi}`)}
            <div class="cd-product-group">
              <div class="cd-product-label"><span>{group.product}</span></div>
              <div class="cd-sale-cards">
                {#each group.data as cs, csi (`cd-cs-${csi}`)}
                  {@render customerCard(cs)}
                {/each}
              </div>
            </div>
          {/each}
        </div>
      </section>
    {/each}
  </div>
{/snippet}

<!-- ─── CUSTOMER CARD ───────────────────────────────── -->
{#snippet customerCard(source: CustomerSales)}
  <div class="cd-metric-card cd-customer-card">
    <div class="cd-metric-period">
      <span class="cd-metric-period-range">{source.dateRange}</span>
    </div>

    <Popover.Root>
      <Popover.Trigger
        class="cd-customer-count"
        onclick={() => (popoverRecords = source.records)}
      >
        {source.records.length}
        <Icon name="external-link" class="cd-icon-xs" />
      </Popover.Trigger>
      <Popover.Content class="cd-popover-content" align="start">
        <div class="cd-popover-header">
          <h4>Customer Details</h4>
          <Badge variant="secondary">{popoverRecords.length} records</Badge>
        </div>
        {#if popoverRecords.length}
          <div class="cd-popover-table-wrap">
            <Table.Root>
              <Table.Header>
                <Table.Row>
                  <Table.Head>Code</Table.Head>
                  <Table.Head>Name</Table.Head>
                  <Table.Head class="text-right">Sale</Table.Head>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {#each popoverRecords as rec, pri (`cd-pop-${pri}`)}
                  <Table.Row>
                    <Table.Cell class="font-medium">{rec.code}</Table.Cell>
                    <Table.Cell>{rec.name}</Table.Cell>
                    <Table.Cell class="text-right tabular-nums">
                      <span class={!rec.sale ? "cd-muted-dash" : ""}>
                        {rec.sale
                          ? Number(rec.sale).toLocaleString("en-IN")
                          : "—"}
                      </span>
                    </Table.Cell>
                  </Table.Row>
                {/each}
              </Table.Body>
            </Table.Root>
          </div>
        {:else}
          <p class="cd-popover-empty">No records</p>
        {/if}
      </Popover.Content>
    </Popover.Root>

    {#if source.lines?.length}
      <div class="cd-metric-items">
        {#each source.lines as line, li (`cd-line-${li}`)}
          <div class="cd-metric-item">
            <span class="cd-metric-item-label">{line.description}</span>
            <span
              class={cn(
                "cd-metric-item-value",
                !line.amount && "cd-muted-dash",
              )}>{line.amount ? line.amount.toFixed(2) : "—"}</span
            >
            <span class="cd-metric-item-unit">{line.unit}</span>
          </div>
        {/each}
      </div>
    {/if}
  </div>
{/snippet}

<!-- ─── COLLECTION VIEW ─────────────────────────────── -->
{#snippet collectionView(source: DashboardData)}
  <div class="cd-view-content">
    {#each locations as location, locIdx (`cd-loc-${locIdx}`)}
      {@const collections = source.data.filter(
        (d) =>
          d.business === location.business && d.location === location.default,
      ) as CollectionData[]}
      <section class="cd-biz-section" in:fly={{ y: 20, duration: 300 }}>
        {@render businessHeader(location)}
        <div class="cd-sale-cards cd-collection-cards">
          {#each collections as col, ci (`cd-col-${ci}`)}
            {@render collectionCard(col)}
          {/each}
        </div>
      </section>
    {/each}
  </div>
{/snippet}

<!-- ─── COLLECTION CARD ─────────────────────────────── -->
{#snippet collectionCard(col: CollectionData)}
  <div class="cd-metric-card cd-collection-card">
    <div class="cd-metric-period">
      <span class="cd-metric-period-range">{col.period}</span>
    </div>

    <Popover.Root>
      <Popover.Trigger
        class="cd-collection-value"
        onclick={() => (popoverRecords = col.data)}
      >
        <span class={col.collection === 0 ? "cd-muted-dash" : ""}>
          {col.collection === 0 ? "—" : col.collection.toLocaleString("en-IN")}
        </span>
        {#if col.data?.length}
          <Icon name="external-link" class="cd-icon-xs" />
        {/if}
      </Popover.Trigger>
      <Popover.Content class="cd-popover-content" align="center" sideOffset={8}>
        <div class="cd-popover-inner">
          <div class="cd-popover-header">
            <div class="flex flex-col gap-1">
              <h4 class="text-sm font-bold flex items-center gap-2">
                <Icon name="banknote" class="w-4 h-4 text-primary" />
                Collection Details
              </h4>
              <p
                class="text-[0.7rem] text-muted-foreground font-medium opacity-80"
              >
                {col.period}
              </p>
            </div>
            <Badge
              variant="outline"
              class="h-6 bg-primary/5 text-primary border-primary/20"
            >
              {popoverRecords.length} records
            </Badge>
          </div>

          {#if popoverRecords.length}
            <div class="cd-popover-scroll-area">
              <Table.Root>
                <Table.Header class="z-30">
                  <Table.Row class="hover:bg-transparent border-b">
                    <Table.Head
                      class="sticky top-0 bg-background/95 backdrop-blur-md z-30 h-9 py-2 text-[0.7rem] uppercase tracking-wider font-bold shadow-[0_1px_0_var(--border)]"
                      >Date</Table.Head
                    >
                    <Table.Head
                      class="sticky top-0 bg-background/95 backdrop-blur-md z-30 h-9 py-2 text-[0.7rem] uppercase tracking-wider font-bold shadow-[0_1px_0_var(--border)]"
                      >Customer</Table.Head
                    >
                    <Table.Head
                      class="sticky top-0 bg-background/95 backdrop-blur-md z-30 h-9 py-2 text-[0.7rem] uppercase tracking-wider font-bold text-right shadow-[0_1px_0_var(--border)]"
                      >Amount</Table.Head
                    >
                  </Table.Row>
                </Table.Header>
                <Table.Body>
                  {#each popoverRecords as rec, ri (ri)}
                    <Table.Row
                      class="group transition-colors odd:bg-muted/30 hover:bg-muted/60 border-none"
                    >
                      <Table.Cell class="py-2 align-top">
                        <div class="flex flex-col gap-0.5">
                          <span
                            class="text-[0.75rem] font-bold tabular-nums whitespace-nowrap"
                            >{rec.date || "—"}</span
                          >
                          <span
                            class="text-[0.65rem] text-muted-foreground font-medium flex items-center gap-1"
                          >
                            <span
                              class="w-1.5 h-1.5 rounded-full bg-emerald-500/60 shadow-[0_0_4px_rgba(16,185,129,0.3)]"
                            ></span>
                            {rec.type || "Payment"}
                          </span>
                        </div>
                      </Table.Cell>
                      <Table.Cell class="py-2 max-w-[200px] align-top">
                        <div class="flex flex-col gap-0.5">
                          <span
                            class="text-[0.75rem] font-semibold leading-tight line-clamp-2"
                            >{rec.name || "—"}</span
                          >
                          <span
                            class="text-[0.65rem] text-muted-foreground font-mono opacity-70 tracking-tighter"
                            >{rec.customerNo || ""}</span
                          >
                        </div>
                      </Table.Cell>
                      <Table.Cell class="py-2 text-right align-top">
                        <span
                          class="text-[0.85rem] font-extrabold text-primary tabular-nums tracking-tight"
                        >
                          {rec.amount ||
                            (rec.amt ? rec.amt.toLocaleString("en-IN") : "—")}
                        </span>
                      </Table.Cell>
                    </Table.Row>
                  {/each}
                </Table.Body>
              </Table.Root>
            </div>
          {:else}
            <div
              class="py-12 flex flex-col items-center justify-center gap-2 grayscale opacity-40"
            >
              <Icon name="search-x" class="w-8 h-8" />
              <p class="text-xs font-medium italic">No detail records found</p>
            </div>
          {/if}
        </div>
      </Popover.Content>
    </Popover.Root>
  </div>
{/snippet}

<!-- ─── SALESPERSON / DEALER VIEW ───────────────────── -->
{#snippet salesPersonView(source: DashboardData)}
  <div class="cd-view-content">
    {#each locations as location, locIdx (`cd-loc-${locIdx}`)}
      {@const salesData = source.data.filter(
        (d) =>
          d.business === location.business && d.location === location.default,
      ) as SalesmanSale[]}
      <section class="cd-biz-section" in:fly={{ y: 20, duration: 300 }}>
        {@render businessHeader(location)}
        {#if salesData.length}
          <div class="cd-sales-tabs-wrap">
            <Tabs.Root
              value={salesInnerTabByBiz[location.business] ?? "tab-0"}
              onValueChange={(v) => {
                salesInnerTabByBiz = {
                  ...salesInnerTabByBiz,
                  [location.business]: v,
                };
              }}
              class="w-full"
            >
              <Tabs.List class="cd-inner-tab-list">
                {#each salesData as sd, i (`sp-${i}`)}
                  <Tabs.Trigger value={`tab-${i}`}
                    >{sd.product ?? "—"}</Tabs.Trigger
                  >
                {/each}
              </Tabs.List>
              {#each salesData as sd, i (`sp-${i}`)}
                <Tabs.Content value={`tab-${i}`} class="cd-inner-tab-content">
                  {#if !sd.data?.length}
                    <div class="cd-tab-empty">
                      <Icon name="inbox" class="cd-icon-lg" />
                      <p>No data found</p>
                    </div>
                  {:else}
                    <div class="cd-data-table-wrap">
                      <Table.Root>
                        <Table.Header>
                          <Table.Row>
                            {#each Object.keys(sd.data[0] ?? {}) as key (key)}
                              <Table.Head
                                class={typeof sd.data[0]?.[key] === "number"
                                  ? "text-right"
                                  : ""}
                              >
                                {key
                                  .replace(/([a-z])([A-Z])/g, "$1 $2")
                                  .replace(/^./, (s) => s.toUpperCase())}
                              </Table.Head>
                            {/each}
                          </Table.Row>
                        </Table.Header>
                        <Table.Body>
                          {#each sd.data as row, ri (ri)}
                            <Table.Row>
                              {#each Object.entries(row) as [key, val], vi (vi)}
                                <Table.Cell
                                  class={cn(
                                    typeof val === "number" &&
                                      "text-right tabular-nums",
                                  )}
                                >
                                  {#if typeof val === "number"}
                                    <span
                                      class={val === 0 ? "cd-muted-dash" : ""}
                                    >
                                      {val === 0
                                        ? "—"
                                        : val.toLocaleString("en-IN")}
                                    </span>
                                  {:else}
                                    <span class={!val ? "cd-muted-dash" : ""}
                                      >{val ?? "—"}</span
                                    >
                                  {/if}
                                </Table.Cell>
                              {/each}
                            </Table.Row>
                          {/each}
                        </Table.Body>
                      </Table.Root>
                    </div>
                  {/if}
                </Tabs.Content>
              {/each}
            </Tabs.Root>
          </div>
        {:else}
          <div class="cd-tab-empty">
            <Icon name="inbox" class="cd-icon-lg" />
            <p>No data available</p>
          </div>
        {/if}
      </section>
    {/each}
  </div>
{/snippet}

<!-- ══════════════════════════════════════════════════════ -->
<!-- STYLES                                                -->
<!-- ══════════════════════════════════════════════════════ -->
<style>
  /* ── Root ────────────────────────────────────────────── */
  .cd-root {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    min-height: 100%;
    width: 100%;
  }

  /* ── Header ─────────────────────────────────────────── */
  .cd-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    padding: 0.75rem 1rem;
    border-bottom: 1px solid var(--border);
    background: var(--card);
    border-radius: var(--radius-xl) var(--radius-xl) 0 0;
    position: sticky;
    top: 0;
    z-index: 20;
    backdrop-filter: blur(12px);
    background: color-mix(in oklch, var(--card) 85%, transparent);
  }

  .cd-header-left {
    display: flex;
    align-items: center;
    gap: 0.625rem;
    min-width: 0;
  }

  .cd-header-icon {
    flex-shrink: 0;
    width: 2.25rem;
    height: 2.25rem;
    border-radius: var(--radius-lg);
    background: var(--primary);
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.cd-icon-lg) {
    width: 1.125rem;
    height: 1.125rem;
  }

  .cd-header-icon :global(.cd-icon-lg) {
    color: var(--primary-foreground);
  }

  .cd-title {
    font-size: 1rem;
    font-weight: 700;
    letter-spacing: -0.02em;
    color: var(--foreground);
    line-height: 1.2;
  }

  .cd-subtitle {
    font-size: 0.7rem;
    color: var(--muted-foreground);
    display: flex;
    align-items: center;
    gap: 0.375rem;
    flex-wrap: wrap;
  }

  .cd-filter-badge {
    display: inline-flex;
    align-items: center;
    background: var(--primary);
    color: var(--primary-foreground);
    font-size: 0.575rem;
    font-weight: 600;
    padding: 0.1rem 0.4rem;
    border-radius: 999px;
    letter-spacing: 0.02em;
  }

  .cd-filter-toggle {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    gap: 0.3rem;
    background: var(--muted);
    border: 1px solid var(--border);
    border-radius: var(--radius-lg);
    padding: 0.35rem 0.6rem;
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--foreground);
    cursor: pointer;
    transition: all 0.15s ease;
  }

  .cd-filter-toggle:hover {
    background: var(--accent);
    border-color: var(--ring);
  }

  :global(.cd-icon-sm) {
    width: 0.875rem;
    height: 0.875rem;
  }
  :global(.cd-icon-xs) {
    width: 0.625rem;
    height: 0.625rem;
  }

  .cd-filter-toggle-label {
    display: none;
  }

  @media (min-width: 480px) {
    .cd-filter-toggle-label {
      display: inline;
    }
  }

  /* ── Filter Panel ───────────────────────────────────── */
  .cd-filter-panel {
    padding: 0.75rem 1rem;
    border-bottom: 1px solid var(--border);
    /*background: color-mix(in oklch, var(--muted) 30%, var(--card));*/
    background: var(--card);
  }

  .cd-filter-row {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  @media (min-width: 640px) {
    .cd-filter-row {
      flex-direction: row;
      align-items: flex-end;
    }
  }

  .cd-filter-field {
    flex: 1;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
    min-width: 0;
  }

  .cd-label {
    font-size: 0.625rem;
    font-weight: 700;
    letter-spacing: 0.08em;
    text-transform: uppercase;
    color: var(--muted-foreground);
  }

  .cd-filter-actions {
    display: flex;
    align-items: flex-end;
    flex-shrink: 0;
  }

  :global(.cd-submit-btn) {
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    white-space: nowrap;
  }

  /* ── View Nav ───────────────────────────────────────── */
  .cd-view-nav {
    border-bottom: 1px solid var(--border);
    background: var(--card);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
  }

  .cd-view-nav::-webkit-scrollbar {
    display: none;
  }

  .cd-view-tabs {
    display: flex;
    gap: 0;
    padding: 0 0.5rem;
    min-width: max-content;
  }

  .cd-view-tab {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    padding: 0.625rem 0.875rem;
    font-size: 0.95rem;
    font-weight: 500;
    color: var(--muted-foreground);
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    white-space: nowrap;
    transition: all 0.2s ease;
    position: relative;
  }

  .cd-view-tab:hover {
    color: var(--foreground);
    background: var(--muted);
  }

  .cd-view-tab--active {
    color: var(--primary);
    border-bottom-color: var(--primary);
    font-weight: 600;
  }

  .cd-view-tab:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  /* View accordion trigger: dark when active (open) */
  .cd-accordion-trigger--active {
    background: var(--muted);
    color: var(--foreground);
    font-weight: 600;
  }
  .cd-accordion-trigger--active .cd-icon-sm {
    color: var(--primary);
  }

  /* Inner view tabs (Product Sale, etc.): dark active tab */
  :global(.cd-inner-tab[data-state="active"]) {
    background: var(--muted);
    color: var(--foreground);
    font-weight: 600;
  }

  /* ── Content ────────────────────────────────────────── */
  .cd-content {
    flex: 1;
    padding: 0.75rem;
  }

  @media (min-width: 640px) {
    .cd-content {
      padding: 1rem;
    }
  }

  /* ── Skeleton Loading ───────────────────────────────── */
  .cd-skeleton-grid {
    display: flex;
    flex-wrap: wrap;
    gap: 0.75rem;
  }

  .cd-skeleton-card {
    flex: 1 1 min(100%, 280px);
    border-radius: var(--radius-lg);
    border: 1px solid var(--border);
    background: var(--card);
    padding: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  :global(.cd-skel-title) {
    height: 0.75rem;
    width: 60%;
    border-radius: 0.25rem;
  }
  :global(.cd-skel-value) {
    height: 1.5rem;
    width: 45%;
    border-radius: 0.375rem;
  }
  :global(.cd-skel-bar) {
    height: 0.5rem;
    width: 80%;
    border-radius: 0.25rem;
  }
  :global(.cd-skel-bar.short) {
    width: 50%;
  }

  /* ── Empty State ────────────────────────────────────── */
  .cd-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    padding: 3rem 1.5rem;
    gap: 0.75rem;
  }

  .cd-empty-icon-wrap {
    width: 3.5rem;
    height: 3.5rem;
    border-radius: 50%;
    background: var(--muted);
    display: flex;
    align-items: center;
    justify-content: center;
  }

  :global(.cd-empty-icon) {
    width: 1.5rem;
    height: 1.5rem;
    color: var(--muted-foreground);
  }

  .cd-empty-title {
    font-size: 1rem;
    font-weight: 700;
    color: var(--foreground);
  }

  .cd-empty-desc {
    font-size: 0.8rem;
    color: var(--muted-foreground);
    max-width: 24rem;
    line-height: 1.5;
  }

  /* ── View Content ───────────────────────────────────── */
  .cd-view-content {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  /* ── Business Section ───────────────────────────────── */
  .cd-biz-section {
    border-radius: var(--radius-xl);
    border: 1px solid var(--border);
    background: var(--card);
    overflow: hidden;
  }

  .cd-biz-header {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    padding: 0.75rem 1rem;
    background: color-mix(in oklch, var(--primary) 6%, var(--card));
    border-bottom: 1px solid var(--border);
  }

  @media (min-width: 640px) {
    .cd-biz-header {
      flex-direction: row;
      align-items: center;
      justify-content: space-between;
    }
  }

  .cd-biz-name {
    display: flex;
    align-items: center;
    gap: 0.65rem;
    font-size: 1rem;
    font-weight: 700;
    color: var(--foreground);
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }

  .cd-biz-avatar {
    width: 1.75rem;
    height: 1.75rem;
    border-radius: var(--radius-md);
    background: var(--primary);
    color: var(--primary-foreground);
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.7rem;
    font-weight: 800;
    flex-shrink: 0;
  }

  .cd-biz-locs {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
  }

  :global(.cd-loc-toggle) {
    font-size: 0.65rem !important;
    padding: 0.2rem 0.5rem !important;
    height: auto !important;
    border-radius: 999px !important;
  }

  :global(.cd-loc-toggle--active) {
    background: var(--primary) !important;
    color: var(--primary-foreground) !important;
    border-color: var(--primary) !important;
  }

  /* ── Product Groups ─────────────────────────────────── */
  .cd-products {
    display: flex;
    flex-direction: column;
    gap: 0;
  }

  .cd-product-group {
    padding: 1.25rem 1.5rem;
    border-bottom: 1px solid color-mix(in oklch, var(--border) 50%, transparent);
  }

  .cd-product-group:last-child {
    border-bottom: none;
  }

  .cd-product-group--total {
    background: color-mix(in oklch, var(--primary) 4%, var(--card));
  }

  .cd-product-label {
    display: flex;
    align-items: center;
    gap: 0.45rem;
    font-size: 0.85rem;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: var(--muted-foreground);
    padding-bottom: 0.375rem;
  }

  .cd-product-group--total .cd-product-label {
    color: var(--primary);
  }

  /* ── Sale Cards Grid ────────────────────────────────── */
  .cd-sale-cards {
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
    padding-top: 0.5rem;
    padding-bottom: 0.75rem;
  }

  .cd-sale-cards > * {
    flex: 1 1 min(100%, 280px);
  }

  .cd-collection-cards {
    padding: 0.75rem;
  }

  /* ── Metric Card ────────────────────────────────────── */
  .cd-metric-card {
    border-radius: var(--radius-xl);
    border: 1px solid var(--border);
    background: var(--background);
    padding: 1.25rem;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
    transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    position: relative;
    overflow: hidden;
    text-align: center;
    box-shadow: 0 2px 8px -2px rgba(0, 0, 0, 0.02);
  }

  .cd-metric-card:hover {
    border-color: color-mix(in oklch, var(--primary) 40%, transparent);
    box-shadow: 0 12px 24px -6px
      color-mix(in oklch, var(--primary) 12%, transparent);
    transform: translateY(-3px);
  }

  .cd-metric-card--total {
    border-color: color-mix(in oklch, var(--primary) 30%, var(--border));
    background: color-mix(in oklch, var(--primary) 2%, var(--background));
  }

  .cd-metric-period {
    display: flex;
    flex-direction: column;
    gap: 0.05rem;
  }

  .cd-metric-period-label {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--foreground);
    text-transform: capitalize;
  }

  .cd-metric-period-range {
    font-size: 0.75rem;
    color: var(--muted-foreground);
    font-weight: bold;
  }

  .cd-metric-value-wrap {
    display: flex;
    align-items: baseline;
    justify-content: center;
    gap: 0.1rem;
    padding: 0.25rem 0;
  }

  .cd-metric-value {
    font-size: 2rem;
    font-weight: 800;
    color: var(--primary);
    letter-spacing: -0.03em;
    line-height: 1;
  }

  .cd-metric-value--nil {
    color: var(--muted-foreground);
    font-size: 1.25rem;
    opacity: 0.35;
    font-weight: 400;
  }

  .cd-muted-dash {
    color: var(--muted-foreground);
    opacity: 0.35;
    font-weight: 400;
  }

  .cd-metric-decimal {
    font-size: 1.15rem;
    font-weight: 600;
    color: var(--primary);
    opacity: 0.6;
  }

  .cd-metric-unit {
    font-size: 0.85rem;
    font-weight: 600;
    color: var(--muted-foreground);
    margin-left: 0.15rem;
  }

  /* ── Metric Item Rows ───────────────────────────────── */
  .cd-metric-items {
    display: flex;
    flex-direction: column;
    gap: 0;
    border-top: 1px solid var(--border);
    padding-top: 0.25rem;
    margin-top: 0.1rem;
  }

  .cd-metric-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    padding: 0.35rem 0.25rem;
    font-size: 0.8rem;
    color: var(--muted-foreground);
    border-bottom: 1px solid color-mix(in oklch, var(--border) 15%, transparent);
    transition: background 0.2s ease;
  }

  .cd-metric-item:hover {
    background: color-mix(in oklch, var(--primary) 4%, transparent);
  }

  .cd-metric-item:last-child {
    border-bottom: none;
  }

  .cd-metric-item--last {
    font-weight: 700;
    color: var(--foreground);
    background: color-mix(in oklch, var(--primary) 8%, transparent) !important;
    border-radius: var(--radius-md);
    padding: 0.4rem 0.5rem;
    margin-top: 0.25rem;
    display: flex;
    justify-content: space-between;
    gap: 0.5rem;
  }

  .cd-metric-item-label {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    text-align: left;
  }

  .cd-metric-item-data {
    display: flex;
    align-items: baseline;
    gap: 0.15rem;
    flex-shrink: 0;
  }

  .cd-metric-item-value {
    font-weight: 600;
    color: var(--foreground);
    font-variant-numeric: tabular-nums;
    flex-shrink: 0;
  }

  .cd-metric-item-unit {
    font-size: 0.75rem;
    color: var(--muted-foreground);
    flex-shrink: 0;
    min-width: 1.5rem;
  }

  /* ── Customer Card ──────────────────────────────────── */
  .cd-customer-card {
    align-items: center;
    text-align: center;
  }

  :global(.cd-customer-count) {
    font-size: 1.375rem !important;
    font-weight: 800 !important;
    color: var(--primary) !important;
    display: inline-flex !important;
    align-items: center !important;
    gap: 0.3rem !important;
    cursor: pointer !important;
    transition: opacity 0.15s !important;
    border: none !important;
    background: none !important;
    padding: 0.25rem !important;
  }

  :global(.cd-customer-count:hover) {
    opacity: 0.7 !important;
  }

  /* ── Collection Card ────────────────────────────────── */
  .cd-collection-card {
    align-items: center;
    text-align: center;
    padding: 0.75rem;
  }

  :global(.cd-collection-value) {
    font-size: 1.25rem !important;
    font-weight: 800 !important;
    color: var(--primary) !important;
    display: inline-flex !important;
    align-items: center !important;
    gap: 0.3rem !important;
    cursor: pointer !important;
    transition: opacity 0.15s !important;
    border: none !important;
    background: none !important;
    padding: 0.25rem !important;
  }

  :global(.cd-collection-value:hover) {
    opacity: 0.7 !important;
  }

  /* ── Popover ────────────────────────────────────────── */
  :global(.cd-popover-content) {
    max-height: 85vh !important;
    min-height: 12rem !important;
    width: calc(100vw - 2rem) !important;
    padding: 0 !important;
    overflow: hidden !important;
    display: flex !important;
    flex-direction: column !important;
    border: 1px solid var(--border) !important;
    box-shadow:
      0 20px 50px -12px rgb(0 0 0 / 0.15),
      0 0 30px -10px color-mix(in oklch, var(--primary) 10%, transparent) !important;
    border-radius: var(--radius-xl) !important;
    background: var(--background) !important;
  }

  .cd-popover-inner {
    display: flex;
    flex-direction: column;
    min-height: 0;
    height: 100%;
    max-height: inherit;
  }

  .cd-popover-header {
    flex-shrink: 0;
    position: sticky;
    top: 0;
    min-height: 3.5rem;
    padding: 1rem;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    border-bottom: 2px solid var(--border);
    background: var(--muted);
    color: var(--foreground);
    z-index: 40;
    user-select: none;
  }

  .cd-popover-scroll-area {
    overflow-y: auto;
    flex: 1;
    min-height: 0;
    scrollbar-width: thin;
    scrollbar-color: var(--border) transparent;
  }

  .cd-popover-scroll-area::-webkit-scrollbar {
    width: 6px;
  }

  .cd-popover-scroll-area::-webkit-scrollbar-thumb {
    background: var(--border);
    border-radius: 999px;
  }

  @media (min-width: 640px) {
    :global(.cd-popover-content) {
      width: 80vw !important;
      max-width: 550px !important;
    }
  }

  @media (min-width: 1024px) {
    :global(.cd-popover-content) {
      width: 45vw !important;
      max-width: 600px !important;
      max-height: 65vh !important;
    }
  }

  /* (Duplicate removed) */

  .cd-popover-header h4 {
    font-size: 0.875rem;
    font-weight: 700;
    color: var(--foreground);
  }

  .cd-popover-table-wrap {
    overflow-x: auto;
  }

  .cd-popover-empty {
    padding: 2rem;
    text-align: center;
    font-size: 0.75rem;
    color: var(--muted-foreground);
  }

  /* ── Salesperson Tabs ───────────────────────────────── */
  .cd-sales-tabs-wrap {
    padding: 0.75rem;
  }

  :global(.cd-inner-tab-list) {
    overflow-x: auto !important;
    -webkit-overflow-scrolling: touch;
    scrollbar-width: none;
  }

  :global(.cd-inner-tab-list::-webkit-scrollbar) {
    display: none;
  }

  :global(.cd-inner-tab-content) {
    margin-top: 0.5rem !important;
  }

  .cd-data-table-wrap {
    overflow-x: auto;
    border-radius: var(--radius-lg);
    border: 1px solid var(--border);
  }

  .cd-tab-empty {
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.5rem;
    padding: 2rem;
    color: var(--muted-foreground);
    font-size: 0.8rem;
    font-style: italic;
  }

  /* ── Loading State ───────────────────────────────────── */
  .cd-loading-container {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 3rem 1.5rem;
    gap: 1.25rem;
    background: color-mix(in oklch, var(--muted) 15%, transparent);
    border-radius: var(--radius-xl);
    border: 1px dashed var(--border);
    margin: 1.5rem 0;
    text-align: center;
  }

  .cd-loading-text {
    font-size: 1.125rem;
    font-weight: 600;
    color: var(--primary);
    display: flex;
    align-items: center;
    gap: 0.5rem;
    letter-spacing: 0.02em;
  }

  .cd-loading-dots {
    display: flex;
    gap: 0.2rem;
    align-items: center;
    height: 1rem;
    margin-top: 0.2rem;
  }

  .cd-loading-dots span {
    width: 0.35rem;
    height: 0.35rem;
    background-color: var(--primary);
    border-radius: 50%;
    display: inline-block;
    animation: cd-dot-bounce 1.4s infinite ease-in-out both;
  }

  .cd-loading-dots span:nth-child(2) {
    animation-delay: 0.2s;
  }
  .cd-loading-dots span:nth-child(3) {
    animation-delay: 0.4s;
  }

  @keyframes cd-dot-bounce {
    0%,
    80%,
    100% {
      transform: scale(0);
      opacity: 0.4;
    }
    40% {
      transform: scale(1);
      opacity: 1;
    }
  }

  .cd-loading-shimmer {
    width: 140px;
    height: 3px;
    background: var(--muted);
    border-radius: 999px;
    position: relative;
    overflow: hidden;
  }

  .cd-loading-shimmer::after {
    content: "";
    position: absolute;
    top: 0;
    left: 0;
    width: 60%;
    height: 100%;
    background: linear-gradient(
      90deg,
      transparent,
      var(--primary),
      transparent
    );
    animation: cd-shimmer-slide 1.5s infinite ease-in-out;
  }

  @keyframes cd-shimmer-slide {
    0% {
      transform: translateX(-150%);
    }
    100% {
      transform: translateX(250%);
    }
  }

  /* ── Responsive Tweaks ──────────────────────────────── */
  @media (min-width: 768px) {
    .cd-title {
      font-size: 1.125rem;
    }

    .cd-header {
      padding: 0.875rem 1.25rem;
    }

    .cd-content {
      padding: 1.25rem;
    }
  }

  @media (min-width: 1024px) {
    .cd-content {
      padding: 1.5rem;
    }
  }
</style>
