<script lang="ts">
  import { onMount } from "svelte";
  import { slide, fade } from "svelte/transition";
  import * as Table from "$lib/components/ui/table";
  import { Icon } from "$lib/components/venUI/icon";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import { Button } from "$lib/components/ui/button";
  import { toast } from "$lib/components/venUI/toast";
  import { authStore, getUser } from "$lib/stores/auth";
  import {
    today,
    getLocalTimeZone,
    parseDate,
    parseDateTime,
    toCalendarDate,
    fromDate,
  } from "@internationalized/date";
  import { fetchClaimRatios } from "./claims-dashboard/api";
  import type { ClaimRatioRow } from "./claims-dashboard/types";

  let loading = $state(false);
  let dateRange = $state<{ start: unknown; end: unknown }>({
    start: undefined,
    end: undefined,
  });
  let selectedRc = $state<string[]>([]);
  let currentView = $state<"Summary" | "Pattern" | "Defect" | "Procurement" | "Make" | "Dealer">("Summary");
  let rows = $state<ClaimRatioRow[]>([]);
  let expanded = $state<Record<string, boolean>>({});

  let activeController: AbortController | null = null;
  let fetchId = 0;

  const workDate = $derived($authStore.user?.workDate);
  const views = ['Summary','Defect','Procurement','Make','Pattern','Dealer'];

  let viewFilteredRc = $state<string[]>([]);
  let sortField = $state<keyof TreeNode["agg"] | "label">("label");
  let sortDir = $state<"asc" | "desc">("asc");

  function toggleSort(field: typeof sortField) {
    if (sortField === field) {
      sortDir = sortDir === "asc" ? "desc" : "asc";
    } else {
      sortField = field;
      sortDir = field === "label" ? "asc" : "desc";
    }
  }

  const productionLocations = $derived.by(() => {
    const locs = $authStore.locations ?? [];
    return locs.filter(
      (l: { production?: unknown }) => l.production === 1,
    ) as { code: string; name: string }[];
  });

  const availableLocations = $derived.by(() => {
    const set = new Set<string>();
    for (const r of rows) {
      if (r.respCenter) set.add(r.respCenter);
    }
    return Array.from(set).sort();
  });

  const filteredRows = $derived.by(() => {
    const allowedMakes = ["APOLLO", "MRF", "JK", "CEAT", "BRIDGESTON", "BRIDGESTONE", "MICHELIN", "BKT", "MODI CONTINENTAL"];
    return rows
      .filter(r => r.respCenter && viewFilteredRc.includes(r.respCenter))
      .map(r => {
        if (currentView === "Make" && r.level01) {
          const makeUpper = r.level01.toUpperCase().trim();
          return {
            ...r,
            level01: allowedMakes.includes(makeUpper) ? makeUpper : "OTHERS"
          };
        }
        return r;
      });
  });


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

  const filterSummary = $derived.by(() => {
    const s = dateRange.start;
    const e = dateRange.end;
    if (!s || !e) return "";
    try {
      const d1 =
        s && typeof s === "object" && "toDate" in (s as object)
          ? (s as { toDate: (tz: unknown) => Date }).toDate(
              getLocalTimeZone(),
            )
          : new Date(String(s));
      const d2 =
        e && typeof e === "object" && "toDate" in (e as object)
          ? (e as { toDate: (tz: unknown) => Date }).toDate(
              getLocalTimeZone(),
            )
          : new Date(String(e));
      const o = { day: "2-digit", month: "short", year: "numeric" } as const;
      return `${d1.toLocaleDateString("en-IN", o)} — ${d2.toLocaleDateString("en-IN", o)}`;
    } catch {
      return "";
    }
  });

  const summaryMeta = $derived.by(() => {
    if (rows.length === 0) return null;
    
    // Calculate based on viewFilteredRc (frontend filter)
    const rcMap = new Map<string, { sale: number; cn: number }>();
    const seen = new Set<string>();
    
    // Initialize with 0
    for (const rc of viewFilteredRc) {
      rcMap.set(rc, { sale: 0, cn: 0 });
    }

    for (const r of rows) {
      const rc = r.respCenter || "default";
      if (viewFilteredRc.includes(rc) && !seen.has(rc)) {
        rcMap.set(rc, { sale: r.saleValue ?? 0, cn: r.creditNoteValue ?? 0 });
        seen.add(rc);
      }
    }
    
    const totals = Array.from(rcMap.values());
    const totalSale = totals.reduce((a, b) => a + b.sale, 0);
    const totalCN = totals.reduce((a, b) => a + b.cn, 0);
    const totalRatio = totalSale > 0 ? (totalCN * 100) / totalSale : 0;
    
    return {
      period: filterSummary,
      locations: viewFilteredRc.length > 0 ? viewFilteredRc.join(", ") : "None Selected",
      saleValue: totalSale,
      creditNoteValue: totalCN,
      creditNotePercent: totalRatio,
      view: "Claim Ratios",
    };
  });

  type TreeNode = {
    key: string;
    label: string;
    level: number;
    agg: {
      sold: number;
      purchase: number;
      claims: number;
      pass: number;
      reject: number;
      unsettled: number;
      specialCase: number;
      claimPercent: number;
      passPercent: number;
    };
    children: TreeNode[];
    isLeaf: boolean;
  };

  const grandTotalClaims = $derived(filteredRows.reduce((a, r) => a + (r.claims ?? 0), 0));
  const grandTotalPass = $derived(filteredRows.reduce((a, r) => a + (r.pass ?? 0), 0));

  function sumAgg(list: ClaimRatioRow[], totalClaims: number, totalPass: number) {
    const sold = list.reduce((a, r) => a + (r.sold ?? 0), 0);
    const purchase = list.reduce((a, r) => a + (r.purchase ?? 0), 0);
    const claims = list.reduce((a, r) => a + (r.claims ?? 0), 0);
    const pass = list.reduce((a, r) => a + (r.pass ?? 0), 0);
    const reject = list.reduce((a, r) => a + (r.reject ?? 0), 0);
    const unsettled = list.reduce((a, r) => a + (r.unsettled ?? 0), 0);
    const specialCase = list.reduce((a, r) => a + (r.specialCase ?? 0), 0);
    
    let baseValue = sold;
    if (currentView === "Defect") baseValue = totalClaims;
    else if (currentView === "Procurement") baseValue = purchase;

    const claimPercent = baseValue > 0 ? (claims * 100) / baseValue : 0;
    
    let passBaseValue = baseValue;
    if (currentView === "Defect") {
      passBaseValue = totalPass;
    }
    const passPercent = passBaseValue > 0 ? (pass * 100) / passBaseValue : 0;
    return {
      sold,
      purchase,
      claims,
      pass,
      reject,
      unsettled,
      specialCase,
      claimPercent,
      passPercent,
    };
  }

  const claimTree = $derived.by((): TreeNode[] => {
    if (!filteredRows.length) return [];
    const levels: (keyof ClaimRatioRow)[] = ["level01", "level02", "level03", "level04"];

    function buildNodes(data: ClaimRatioRow[], depth: number, parentKey: string): TreeNode[] {
      if (depth >= levels.length) return [];
      const currentLevelField = levels[depth];
      const groups = new Map<string, ClaimRatioRow[]>();

      for (const r of data) {
        const val = (r[currentLevelField] as string)?.trim() || "";
        if (!groups.has(val)) groups.set(val, []);
        groups.get(val)!.push(r);
      }

      const groupedData = Array.from(groups.entries()).map(([label, children]) => ({
        label,
        children,
        agg: sumAgg(children, grandTotalClaims, grandTotalPass)
      }));

      // Apply Sorting
      groupedData.sort((a, b) => {
        let vA, vB;
        if (sortField === "label") {
          vA = a.label.toLowerCase();
          vB = b.label.toLowerCase();
        } else {
          vA = a.agg[sortField] ?? 0;
          vB = b.agg[sortField] ?? 0;
        }

        const modifier = sortDir === "asc" ? 1 : -1;
        if (vA < vB) return -1 * modifier;
        if (vA > vB) return 1 * modifier;
        return 0;
      });

      return groupedData.flatMap(({ label, children, agg }) => {
        if (!label) {
          return buildNodes(children, depth + 1, parentKey);
        }
        const key = `${parentKey}|${depth}|${label}`;
        const subNodes = buildNodes(children, depth + 1, key);
        return [{
          key,
          label,
          level: depth,
          agg,
          children: subNodes,
          isLeaf: subNodes.length === 0,
        }];
      });
    }
    return buildNodes(filteredRows, 0, "root");
  });

  const grandTotal = $derived.by(() => {
    if (filteredRows.length === 0) return null;
    return sumAgg(filteredRows, grandTotalClaims, grandTotalPass);
  });

  function toIso(date: unknown): string {
    if (!date) return "";
    if (typeof date === "string") return new Date(date).toISOString();
    if (
      date &&
      typeof date === "object" &&
      "toDate" in date &&
      typeof (date as { toDate: unknown }).toDate === "function"
    ) {
      return (date as { toDate: (tz: unknown) => Date }).toDate(
        getLocalTimeZone(),
      ).toISOString();
    }
    return "";
  }

  function handleDateChange(val: unknown) {
    if (!val) return;
    dateRange = val as { start: unknown; end: unknown };
  }

  function toggleViewRc(code: string) {
    if (viewFilteredRc.includes(code)) {
      viewFilteredRc = viewFilteredRc.filter((c) => c !== code);
    } else {
      viewFilteredRc = [...viewFilteredRc, code];
    }
  }

  function toggleRc(code: string) {
    if (selectedRc.includes(code)) {
      selectedRc = selectedRc.filter((c) => c !== code);
    } else {
      selectedRc = [...selectedRc, code];
    }
  }

  function fmtInt(n: number | undefined) {
    return (n ?? 0).toLocaleString("en-IN");
  }

  function fmtPct(n: number | undefined) {
    if (n == null || Number.isNaN(n)) return "—";
    return `${Number(n).toFixed(2)}%`;
  }

  function fmtMoney(n: number | undefined) {
    if (n == null) return "—";
    return `₹${Number(n).toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })}`;
  }

  function toggleExpand(key: string) {
    expanded = { ...expanded, [key]: !expanded[key] };
  }

  async function onSubmit() {
    const from = dateRange.start ? toIso(dateRange.start) : "";
    const to = dateRange.end ? toIso(dateRange.end) : "";
    if (!from || !to) {
      toast.error("Please select a date range");
      return;
    }
    if (selectedRc.length === 0) {
      toast.error("Select at least one responsibility center");
      return;
    }

    if (activeController) activeController.abort();
    activeController = new AbortController();
    const { signal } = activeController;
    const id = ++fetchId;

    loading = true;
    rows = [];
    expanded = {};

    const res = await fetchClaimRatios(
      {
        from,
        to,
        respCenters: selectedRc.length > 0 ? selectedRc : undefined,
        view: currentView,
      },
      signal,
    );

    if (signal.aborted || id !== fetchId) return;

    loading = false;
    if (!res.success) {
      if (res.error) toast.error(res.error);
      return;
    }
    rows = res.rows;
    // Initialize view filter to show all fetched locations by default
    viewFilteredRc = Array.from(new Set(rows.map(r => r.respCenter).filter(Boolean))) as string[];
    
    if (rows.length === 0) {
      toast.info("No data found for this period.");
    }
  }

  onMount(() => {
    const start = ref.set({ day: 1 });
    dateRange = { start, end: ref };

    const pl =
      authStore.get().locations?.filter((l) => l.production === 1) ?? [];
    if (pl.length > 0) {
      const u = getUser();
      selectedRc =
        u?.respCenter && pl.some((x) => x.code === u.respCenter)
          ? [u.respCenter]
          : pl.map((x) => x.code);
    }
    
    setTimeout(() => {
        if (dateRange.start && dateRange.end && selectedRc.length > 0) {
            onSubmit();
        }
    }, 100);
  });
</script>

{#snippet treeRow(node: TreeNode)}
  <Table.Row
    class="group/row relative transition-all duration-200 {node.isLeaf
      ? 'hover:bg-muted/30'
      : 'cursor-pointer bg-muted/5 hover:bg-muted/15'}"
    onclick={() => !node.isLeaf && toggleExpand(node.key)}
  >
    <Table.Cell
      class="cd-table-cell-sticky sticky left-0 z-10 min-w-[140px] sm:min-w-[200px] bg-background/80 backdrop-blur-md p-2 sm:p-4"
      style="padding-left: calc({node.level} * var(--indent-step, 0.5rem) + 0.5rem)"
    >
      <div class="flex items-center gap-1.5 sm:gap-2">
        {#if !node.isLeaf}
          <div
            class="flex size-4 sm:size-5 items-center justify-center rounded-md border border-border bg-background transition-transform duration-200 {expanded[
              node.key
            ]
              ? 'rotate-90'
              : ''}"
          >
            <Icon name="chevron-right" class="size-2.5 sm:size-3" />
          </div>
        {:else}
          <div class="size-4 sm:size-5" />
        {/if}
        <span
          class="truncate text-xs sm:text-sm {node.isLeaf
            ? 'text-muted-foreground'
            : 'font-semibold text-foreground'}"
        >
          {node.label}
        </span>
      </div>
      {#if !node.isLeaf && expanded[node.key]}
        <div
          class="absolute left-0 top-0 h-full w-0.5 sm:w-1 bg-primary/40"
          style="margin-left: calc({node.level} * var(--indent-step, 0.5rem) + var(--line-offset, 0.25rem))"
        ></div>
      {/if}
    </Table.Cell>
    {#if currentView !== "Defect"}
    <Table.Cell class="text-right tabular-nums font-medium text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(currentView === "Procurement" ? node.agg.purchase : node.agg.sold)}</Table.Cell
    >
    {/if}
    <Table.Cell class="text-right tabular-nums text-primary font-semibold text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.claims)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.pass)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-red-600 dark:text-red-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.reject)}</Table.Cell
    >
    {#if currentView !== "Defect"}
    <Table.Cell class="text-right tabular-nums text-amber-600 dark:text-amber-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.unsettled)}</Table.Cell
    >
    {/if}
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.specialCase)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums font-bold text-xs sm:text-sm p-2 sm:p-4"
      >{fmtPct(node.agg.claimPercent)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums font-bold text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtPct(node.agg.passPercent)}</Table.Cell
    >
  </Table.Row>

  {#if !node.isLeaf && expanded[node.key]}
    {#each node.children as child (child.key)}
      {@render treeRow(child)}
    {/each}
  {/if}
{/snippet}

<div class="cd-root h-full w-full space-y-4 sm:space-y-6 p-3 sm:p-4 md:p-6" in:fade={{ duration: 400 }}>
  <!-- Intelligence Header & Filter Card -->
  <div class="cd-header-card rounded-2xl border border-border bg-card/40 p-4 sm:p-6 shadow-xl backdrop-blur-xl transition-all hover:shadow-2xl">
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between w-full">
      <div class="flex items-center gap-4">
        <div class="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary shrink-0">
          <Icon name="file-box" class="size-6" />
        </div>
        <div>
          <h1 class="text-lg sm:text-xl font-black tracking-tight text-foreground">Claims Intelligence</h1>          
        </div>
      </div>

      <div class="flex flex-col sm:flex-row gap-3 w-full sm:w-auto">
        <div class="w-full sm:w-[240px]">
          <DatePicker
            value={dateRange}
            mode="range"
            valueType="calendar"
            placeholder="Select period"
            onValueChange={handleDateChange}
            workdate={workDate}
            presetKeys="thisMonth,lastMonth,thisQuarter,lastQuarter,thisFinYear,lastFinYear"
            fiscal
          />        
        </div>
        <div class="w-full sm:w-auto">
          <Button
            variant="default"
            class="h-8 w-full sm:w-auto bg-foreground px-8 font-black text-background shadow-2xl transition-all hover:scale-[1.02] active:scale-[0.98] disabled:opacity-50"
            onclick={onSubmit}
            disabled={loading}
          >
            {#if loading}
              <Icon name="loader-circle" class="mr-3 size-5 animate-spin" />
            {:else}
              <Icon name="search" class="mr-3 size-5" />
            {/if}
            Generate
          </Button>  
        </div>
      </div>        
    </div>
  </div>

  <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between w-full">  
  {#if views.length > 1}
    <div class="flex items-center gap-3 overflow-x-auto pb-2 scrollbar-hide" in:fade>
      <span class="text-[10px] font-black uppercase tracking-[0.2em] text-muted-foreground/40 whitespace-nowrap">View</span>
      <div class="flex gap-2">
        {#each views as v}
          <button
            type="button"
            class="h-8 rounded-full border px-4 text-[11px] font-bold transition-all active:scale-95 whitespace-nowrap {currentView === v 
              ? 'bg-foreground text-background border-foreground shadow-md' 
              : 'bg-background/40 text-muted-foreground border-border hover:border-foreground/40 hover:text-foreground'}"
            onclick={() => {
              currentView = v as any;
              onSubmit();
            }}
          >
            {v}
          </button>
        {/each}
      </div>
    </div>
  {/if}

  {#if availableLocations.length > 1}
    <div class="flex items-center gap-3 overflow-x-auto pb-2 scrollbar-hide" in:fade>
      <span class="text-[10px] font-black uppercase tracking-[0.2em] text-muted-foreground/40 whitespace-nowrap">Locations</span>
      <div class="flex gap-2">
        {#each availableLocations as loc}
          <button
            type="button"
            class="h-8 rounded-full border px-4 text-[11px] font-bold transition-all active:scale-95 whitespace-nowrap {viewFilteredRc.includes(loc) 
              ? 'bg-foreground text-background border-foreground shadow-md' 
              : 'bg-background/40 text-muted-foreground border-border hover:border-foreground/40 hover:text-foreground'}"
            onclick={() => toggleViewRc(loc)}
          >
            {loc}
          </button>
        {/each}
      </div>
    </div>
  {/if}
  </div>  
  {#if summaryMeta}
    <div
      class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
      in:slide={{ duration: 400 }}
    >
      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-primary/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Sales Performance</p
        >
        <p class="mt-2 text-xl sm:text-2xl font-black tabular-nums tracking-tight">
          {fmtMoney(summaryMeta.saleValue)}
        </p>
        <div class="mt-4 flex items-center gap-2 text-[10px] text-muted-foreground">
          <Icon name="clock" class="size-3" />
          <span>{summaryMeta.period}</span>
        </div>
      </div>

      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-red-500/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Claims Exposure</p
        >
        <p class="mt-2 text-xl sm:text-2xl font-black tabular-nums tracking-tight text-red-500">
          {fmtMoney(summaryMeta.creditNoteValue)}
        </p>
        <div class="mt-4 flex items-center gap-2 text-[10px] text-muted-foreground">
          <Icon name="map-pin" class="size-3" />
          <span class="truncate">{summaryMeta.locations}</span>
        </div>
      </div>

      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-amber-500/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Claim Ratio</p
        >
        <p class="mt-2 text-xl sm:text-2xl font-black tabular-nums tracking-tight">
          {fmtPct(summaryMeta.creditNotePercent)}
        </p>
        <div class="mt-4 h-1.5 w-full overflow-hidden rounded-full bg-muted">
          <div
            class="h-full bg-primary transition-all duration-1000"
            style="width: {Math.min(summaryMeta.creditNotePercent * 5, 100)}%"
          ></div>
        </div>
      </div>      
    </div>
  {/if}

{#snippet sortIcon(field: typeof sortField)}
  <div class="ml-1 inline-flex flex-col opacity-20 group-hover/head:opacity-100 transition-opacity {sortField === field ? 'opacity-100' : ''}">
    <Icon 
      name={sortDir === 'asc' && sortField === field ? 'chevron-up' : 'chevron-down'} 
      class="size-3 {sortField === field ? 'text-primary' : 'text-muted-foreground'}" 
    />
  </div>
{/snippet}

{#if loading}
  <div
    class="flex min-h-[400px] flex-col items-center justify-center gap-4 rounded-3xl border-2 border-dashed border-border/60 bg-muted/5"
  >
    <div class="relative">
      <div class="absolute inset-0 animate-ping rounded-full bg-primary/20"></div>
      <Icon name="loader-circle" class="relative size-12 animate-spin text-primary" />
    </div>
    <p class="animate-pulse text-sm font-bold uppercase tracking-widest text-muted-foreground"
      >Synthesizing Data...</p
    >
  </div>
{:else if claimTree.length > 0}
  <div
    class="overflow-hidden rounded-lg border border-border/60 bg-card/30 shadow-2xl backdrop-blur-xl"
  >
    <div class="overflow-x-auto">
      <Table.Root class="w-full border-collapse text-sm">
        <Table.Header>
          <Table.Row class="bg-muted/40 hover:bg-muted/40">
            <Table.Head
              class="sticky left-0 z-20 min-w-[140px] sm:min-w-[280px] bg-muted/80 py-3 sm:py-4 px-2 sm:px-4 text-[9px] sm:text-[10px] font-black uppercase tracking-widest backdrop-blur-xl cursor-pointer group/head"
              onclick={() => toggleSort('label')}
            >
              <div class="flex items-center">
                Structure
                {@render sortIcon('label')}
              </div>
            </Table.Head>
            {#if currentView !== "Defect"}
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort(currentView === "Procurement" ? 'purchase' : 'sold')}
            >
              <div class="flex items-center justify-end">
                {currentView === "Procurement" ? "Purchase" : "Sold"}
                {@render sortIcon(currentView === "Procurement" ? 'purchase' : 'sold')}
              </div>
            </Table.Head>
            {/if}
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-primary cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('claims')}
            >
              <div class="flex items-center justify-end">
                Claims
                {@render sortIcon('claims')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-green-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('pass')}
            >
              <div class="flex items-center justify-end">
                Pass
                {@render sortIcon('pass')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-red-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('reject')}
            >
              <div class="flex items-center justify-end">
                Reject
                {@render sortIcon('reject')}
              </div>
            </Table.Head>
            {#if currentView !== "Defect"}
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-amber-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('unsettled')}
            >
              <div class="flex items-center justify-end">
                Unsettled
                {@render sortIcon('unsettled')}
              </div>
            </Table.Head>
            {/if}
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('specialCase')}
            >
              <div class="flex items-center justify-end">
                Sp. Case
                {@render sortIcon('specialCase')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('claimPercent')}
            >
              <div class="flex items-center justify-end">
                Claim %
                {@render sortIcon('claimPercent')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-green-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4"
              onclick={() => toggleSort('passPercent')}
            >
              <div class="flex items-center justify-end">
                Pass %
                {@render sortIcon('passPercent')}
              </div>
            </Table.Head>
          </Table.Row>
        </Table.Header>
          <Table.Body>
            {#each claimTree as node (node.key)}
              {@render treeRow(node)}
            {/each}
          </Table.Body>
          {#if grandTotal}
            <Table.Footer class="bg-muted/50 border-t-2 border-border/80">
              <Table.Row class="hover:bg-muted/60 transition-colors">
                <Table.Cell class="sticky left-0 z-10 bg-muted/90 backdrop-blur-md font-black text-[9px] sm:text-[10px] uppercase tracking-widest px-3 sm:px-6 py-3 sm:py-4">
                  Grand Total
                </Table.Cell>
                {#if currentView !== "Defect"}
                <Table.Cell class="text-right tabular-nums font-black text-foreground text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(currentView === "Procurement" ? grandTotal.purchase : grandTotal.sold)}
                </Table.Cell>
                {/if}
                <Table.Cell class="text-right tabular-nums font-black text-primary text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(grandTotal.claims)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-black text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(grandTotal.pass)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-black text-red-600 dark:text-red-400 text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(grandTotal.reject)}
                </Table.Cell>
                {#if currentView !== "Defect"}
                <Table.Cell class="text-right tabular-nums font-black text-amber-600 dark:text-amber-400 text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(grandTotal.unsettled)}
                </Table.Cell>
                {/if}
                <Table.Cell class="text-right tabular-nums font-black text-xs sm:text-sm p-2 sm:p-4">
                  {fmtInt(grandTotal.specialCase)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-black text-foreground text-xs sm:text-sm p-2 sm:p-4">
                  {fmtPct(grandTotal.claimPercent)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-black text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4">
                  {fmtPct(grandTotal.passPercent)}
                </Table.Cell>
              </Table.Row>
            </Table.Footer>
          {/if}
        </Table.Root>
      </div>
    </div>
  {:else if !loading && rows.length === 0}
    <div
      class="flex min-h-[300px] flex-col items-center justify-center gap-4 rounded-3xl border border-dashed border-border/60 bg-muted/5 text-muted-foreground"
    >
      <Icon name="search-x" class="size-12 opacity-20" />
      <p class="font-bold uppercase tracking-widest opacity-40">No intelligence found</p>
    </div>
  {/if}
</div>

<style>
  .cd-root {
    background: radial-gradient(
      circle at top right,
      hsl(var(--primary) / 0.03),
      transparent 40%
    );
  }

  .cd-header-card {
    background-image: 
        linear-gradient(to bottom right, hsl(var(--card) / 0.8), hsl(var(--card) / 0.4)),
        url("data:image/svg+xml,%3Csvg width='20' height='20' viewBox='0 0 20 20' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='%239C92AC' fill-opacity='0.05' fill-rule='evenodd'%3E%3Ccircle cx='3' cy='3' r='3'/%3E%3Ccircle cx='13' cy='13' r='3'/%3E%3C/g%3E%3C/svg%3E");
  }

  :global(.cd-table-cell-sticky) {
    --indent-step: 0.5rem;
    --line-offset: 0.25rem;
  }
  @media (min-width: 640px) {
    :global(.cd-table-cell-sticky) {
      --indent-step: 1.5rem;
      --line-offset: 0.5rem;
    }
  }

  /* Custom Scrollbar for Tree Container */
  .overflow-x-auto {
    scrollbar-width: none; /* Firefox */
    -ms-overflow-style: none; /* IE and Edge */
  }
  .overflow-x-auto::-webkit-scrollbar {
    display: none; /* Chrome, Safari, Opera */
    height: 0px;
    width: 0px;
  }
  
  .scrollbar-hide {
    scrollbar-width: none;
    -ms-overflow-style: none;
  }
  .scrollbar-hide::-webkit-scrollbar {
    display: none;
  }

  :global(.cd-table tbody tr) {
    border-bottom: 1px solid var(--border);
  }
</style>
