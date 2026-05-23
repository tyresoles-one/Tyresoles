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
    startOfMonth,
    endOfMonth,
  } from "@internationalized/date";
  import { fetchProcurement } from "./procurement-dashboard/api";
  import type { ProcurementRow } from "./procurement-dashboard/types";

  let loading = $state(false);
  let dateRange = $state<{ start: unknown; end: unknown }>({
    start: undefined,
    end: undefined,
  });
  let rows = $state<ProcurementRow[]>([]);
  let expanded = $state<Record<string, boolean>>({});

  let activeController: AbortController | null = null;
  let fetchId = 0;

  const workDate = $derived($authStore.user?.workDate);
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

  type TreeNode = {
    key: string;
    label: string;
    level: number;
    agg: {
      target: number;
      purchased: number;
      purchasedLastMonth: number;
      avgCost: number;
      avgCostLastMonth: number;
      freight: number;
    };
    children: TreeNode[];
    isLeaf: boolean;
  };

  function sumAgg(list: ProcurementRow[]) {
    const target = list.reduce((a, r) => a + (r.target ?? 0), 0);
    const purchased = list.reduce((a, r) => a + (r.purchased ?? 0), 0);
    const purchasedLastMonth = list.reduce((a, r) => a + (r.purchasedLastMonth ?? 0), 0);

    const totalCostCurrent = list.reduce((a, r) => a + (r.purchased ?? 0) * (r.avgCost ?? 0), 0);
    const avgCost = purchased > 0 ? totalCostCurrent / purchased : 0;

    const totalCostLastMonth = list.reduce((a, r) => a + (r.purchasedLastMonth ?? 0) * (r.avgCostLastMonth ?? 0), 0);
    const avgCostLastMonth = purchasedLastMonth > 0 ? totalCostLastMonth / purchasedLastMonth : 0;

    const totalFreightCurrent = list.reduce((a, r) => a + (r.purchased ?? 0) * (r.freight ?? 0), 0);
    const freight = purchased > 0 ? totalFreightCurrent / purchased : 0;

    return {
      target,
      purchased,
      purchasedLastMonth,
      avgCost,
      avgCostLastMonth,
      freight,
    };
  }

  const procurementTree = $derived.by((): TreeNode[] => {
    if (!rows.length) return [];

    const sizeGroups = new Map<string, ProcurementRow[]>();
    for (const r of rows) {
      const sz = r.size?.trim() || "Unknown Size";
      if (!sizeGroups.has(sz)) {
        sizeGroups.set(sz, []);
      }
      sizeGroups.get(sz)!.push(r);
    }

    const sizeNodes = Array.from(sizeGroups.entries()).map(([sizeName, childRows]) => {
      const marketNodes: TreeNode[] = childRows.map((r) => {
        const mktName = r.market?.trim() || "Default Market";
        return {
          key: `size_${sizeName}_market_${mktName}`,
          label: mktName,
          level: 1,
          agg: {
            target: r.target ?? 0,
            purchased: r.purchased ?? 0,
            purchasedLastMonth: r.purchasedLastMonth ?? 0,
            avgCost: r.avgCost ?? 0,
            avgCostLastMonth: r.avgCostLastMonth ?? 0,
            freight: r.freight ?? 0,
          },
          children: [],
          isLeaf: true,
        };
      });

      const parentAgg = sumAgg(childRows);

      return {
        key: `size_${sizeName}`,
        label: sizeName,
        level: 0,
        agg: parentAgg,
        children: marketNodes,
        isLeaf: marketNodes.length === 0,
      };
    });

    sizeNodes.sort((a, b) => {
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

    for (const parent of sizeNodes) {
      parent.children.sort((a, b) => {
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
    }

    return sizeNodes;
  });

  const grandTotal = $derived.by(() => {
    if (rows.length === 0) return null;
    return sumAgg(rows);
  });

  const varianceMetrics = $derived.by(() => {
    if (rows.length === 0) return null;
    const totals = sumAgg(rows);
    const current = totals.purchased;
    const last = totals.purchasedLastMonth;
    const diff = current - last;
    const pct = last > 0 ? (diff * 100) / last : 0;
    return {
      current,
      last,
      diff,
      pct,
    };
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

  function fmtInt(n: number | undefined) {
    return (n ?? 0).toLocaleString("en-IN");
  }

  function fmtMoney(n: number | undefined) {
    if (n == null) return "—";
    return Number(n).toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
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

    if (activeController) activeController.abort();
    activeController = new AbortController();
    const { signal } = activeController;
    const id = ++fetchId;

    loading = true;
    rows = [];
    expanded = {};

    const res = await fetchProcurement(
      {
        from,
        to,
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

    if (rows.length === 0) {
      toast.info("No procurement data found for this period.");
    }
  }

  onMount(() => {
    /** Full calendar month containing workDate (or today), not 1st → ref only. */
    const start = startOfMonth(ref);
    const end = endOfMonth(ref);
    dateRange = { start, end };

    setTimeout(() => {
      if (dateRange.start && dateRange.end) {
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
      class="cd-table-cell-sticky sticky left-0 z-10 min-w-[110px] sm:min-w-[180px] max-w-[140px] sm:max-w-[220px] bg-background/80 backdrop-blur-md p-2 sm:p-4 whitespace-normal break-words"
      style="padding-left: calc({node.level} * var(--indent-step, 0.75rem) + 0.5rem)"
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
          class="whitespace-normal break-words leading-tight text-xs sm:text-sm {node.isLeaf
            ? 'text-muted-foreground'
            : 'font-semibold text-foreground'}"
        >
          {node.label}
        </span>
      </div>
      {#if !node.isLeaf && expanded[node.key]}
        <div
          class="absolute left-0 top-0 h-full w-0.5 sm:w-1 bg-primary/40"
          style="margin-left: calc({node.level} * var(--indent-step, 0.75rem) + var(--line-offset, 0.25rem))"
        ></div>
      {/if}
    </Table.Cell>
    <Table.Cell class="text-right tabular-nums font-medium text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.target)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-primary font-semibold text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.purchased)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-muted-foreground text-xs sm:text-sm p-2 sm:p-4"
      >{fmtInt(node.agg.purchasedLastMonth)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtMoney(node.agg.avgCost)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-amber-600 dark:text-amber-400 text-xs sm:text-sm p-2 sm:p-4"
      >{fmtMoney(node.agg.freight)}</Table.Cell
    >
    <Table.Cell class="text-right tabular-nums text-muted-foreground text-xs sm:text-sm p-2 sm:p-4"
      >{fmtMoney(node.agg.avgCostLastMonth)}</Table.Cell
    >
  </Table.Row>

  {#if !node.isLeaf && expanded[node.key]}
    {#each node.children as child (child.key)}
      {@render treeRow(child)}
    {/each}
  {/if}
{/snippet}

<div class="cd-root h-full w-full space-y-4 sm:space-y-6 p-3 sm:p-4 md:p-6" in:fade={{ duration: 400 }}>
  <!-- Procurement Header & Filter Card -->
  <div class="cd-header-card rounded-2xl border border-border bg-card/40 p-4 sm:p-6 shadow-xl backdrop-blur-xl transition-all hover:shadow-2xl">
    <div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between w-full">
      <div class="flex items-center gap-4">
        <div class="flex size-10 items-center justify-center rounded-2xl bg-primary/10 text-primary shrink-0">
          <Icon name="truck" class="size-6" />
        </div>
        <div>
          <h1 class="text-lg sm:text-xl font-black tracking-tight text-foreground">Procurement Intelligence</h1>          
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

  {#if varianceMetrics}
    <div
      class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3"
      in:slide={{ duration: 400 }}
    >
      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-primary/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Total Procurement Config Target</p
        >
        <p class="mt-2 text-xl sm:text-2xl font-black tabular-nums tracking-tight">
          {fmtInt(grandTotal?.target)}
        </p>
        <div class="mt-4 flex items-center gap-2 text-[10px] text-muted-foreground">
          <Icon name="clock" class="size-3" />
          <span>{filterSummary}</span>
        </div>
      </div>

      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-blue-500/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Current Period Purchased Volume</p
        >
        <p class="mt-2 text-xl sm:text-2xl font-black tabular-nums tracking-tight text-primary">
          {fmtInt(varianceMetrics.current)}
        </p>
        <div class="mt-4 flex items-center gap-2 text-[10px] text-muted-foreground">
          <Icon name="history" class="size-3" />
          <span>Last Month: {fmtInt(varianceMetrics.last)}</span>
        </div>
      </div>

      <div
        class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 sm:p-5 shadow-lg backdrop-blur-xl transition-all hover:-translate-y-1 hover:shadow-2xl"
      >
        <div class="absolute -right-4 -top-4 size-24 rounded-full bg-amber-500/5 transition-transform duration-500 group-hover:scale-150"></div>
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
          >Month-on-Month Volume Growth</p
        >
        <div class="mt-2 flex items-baseline gap-2">
          <p class="text-xl sm:text-2xl font-black tabular-nums tracking-tight {varianceMetrics.pct >= 0 ? 'text-green-500' : 'text-red-500'}">
            {varianceMetrics.pct >= 0 ? '+' : ''}{varianceMetrics.pct.toFixed(2)}%
          </p>
          <span class="text-xs text-muted-foreground">
            ({varianceMetrics.diff >= 0 ? '+' : ''}{fmtInt(varianceMetrics.diff)} pcs)
          </span>
        </div>
        <div class="mt-4 flex items-center gap-2 text-[10px] text-muted-foreground">
          <Icon name={varianceMetrics.pct >= 0 ? 'trending-up' : 'trending-down'} class="size-3.5 {varianceMetrics.pct >= 0 ? 'text-green-500' : 'text-red-500'}" />
          <span class="font-bold {varianceMetrics.pct >= 0 ? 'text-green-500' : 'text-red-500'}">
            {varianceMetrics.pct >= 0 ? "Growth Observed" : "Deficit Observed"}
          </span>
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
{:else if procurementTree.length > 0}
  <div
    class="overflow-hidden rounded-lg border border-border/60 bg-card/30 shadow-2xl backdrop-blur-xl"
  >
    <div class="overflow-x-auto">
      <Table.Root class="w-full border-collapse text-sm">
        <Table.Header>
          <Table.Row class="bg-muted/40 hover:bg-muted/40">
            <Table.Head
              class="sticky left-0 z-20 min-w-[110px] sm:min-w-[180px] max-w-[140px] sm:max-w-[220px] bg-muted/80 py-3 sm:py-4 px-2 sm:px-4 text-[9px] sm:text-[10px] font-black uppercase tracking-widest backdrop-blur-xl cursor-pointer group/head whitespace-normal leading-tight"
              onclick={() => toggleSort('label')}
            >
              <div class="flex items-center gap-1">
                <span class="whitespace-normal">Size / Market Structure</span>
                {@render sortIcon('label')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4 whitespace-normal leading-tight min-w-[60px] sm:min-w-[80px]"
              onclick={() => toggleSort('target')}
            >
              <div class="flex items-center justify-end text-right gap-1">
                <span>Target</span>
                {@render sortIcon('target')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-primary cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4 whitespace-normal leading-tight min-w-[60px] sm:min-w-[80px]"
              onclick={() => toggleSort('purchased')}
            >
              <div class="flex items-center justify-end text-right gap-1">
                <span>Purchased</span>
                {@render sortIcon('purchased')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-2 sm:py-3 px-1.5 sm:px-3 whitespace-normal leading-tight max-w-[80px] sm:max-w-[110px] min-w-[85px] sm:min-w-[110px]"
              onclick={() => toggleSort('purchasedLastMonth')}
            >
              <div class="flex items-center justify-end text-right gap-0.5 sm:gap-1">
                <span class="whitespace-normal text-right">Purchased Last Month</span>
                {@render sortIcon('purchasedLastMonth')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-green-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4 whitespace-normal leading-tight min-w-[70px] sm:min-w-[90px]"
              onclick={() => toggleSort('avgCost')}
            >
              <div class="flex items-center justify-end text-right gap-1">
                <span>Avg Cost</span>
                {@render sortIcon('avgCost')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest text-amber-600 cursor-pointer group/head py-3 sm:py-4 px-2 sm:px-4 whitespace-normal leading-tight min-w-[70px] sm:min-w-[90px]"
              onclick={() => toggleSort('freight')}
            >
              <div class="flex items-center justify-end text-right gap-1">
                <span>Freight</span>
                {@render sortIcon('freight')}
              </div>
            </Table.Head>
            <Table.Head 
              class="text-right text-[9px] sm:text-[10px] font-black uppercase tracking-widest cursor-pointer group/head py-2 sm:py-3 px-1.5 sm:px-3 whitespace-normal leading-tight max-w-[80px] sm:max-w-[110px] min-w-[85px] sm:min-w-[110px]"
              onclick={() => toggleSort('avgCostLastMonth')}
            >
              <div class="flex items-center justify-end text-right gap-0.5 sm:gap-1">
                <span class="whitespace-normal text-right">Avg Cost Last Month</span>
                {@render sortIcon('avgCostLastMonth')}
              </div>
            </Table.Head>
          </Table.Row>
        </Table.Header>
        <Table.Body>
          {#each procurementTree as node (node.key)}
            {@render treeRow(node)}
          {/each}
        </Table.Body>
        {#if grandTotal}
          <Table.Footer class="bg-muted/50 border-t-2 border-border/80">
            <Table.Row class="hover:bg-muted/60 transition-colors">
              <Table.Cell class="sticky left-0 z-10 bg-muted/90 backdrop-blur-md font-black text-[9px] sm:text-[10px] uppercase tracking-widest px-3 sm:px-6 py-3 sm:py-4 min-w-[110px] sm:min-w-[180px] max-w-[140px] sm:max-w-[220px]">
                Grand Total
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-foreground text-xs sm:text-sm p-2 sm:p-4">
                {fmtInt(grandTotal.target)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-primary text-xs sm:text-sm p-2 sm:p-4">
                {fmtInt(grandTotal.purchased)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-muted-foreground text-xs sm:text-sm p-2 sm:p-4">
                {fmtInt(grandTotal.purchasedLastMonth)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-green-600 dark:text-green-400 text-xs sm:text-sm p-2 sm:p-4">
                {fmtMoney(grandTotal.avgCost)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-amber-600 dark:text-amber-400 text-xs sm:text-sm p-2 sm:p-4">
                {fmtMoney(grandTotal.freight)}
              </Table.Cell>
              <Table.Cell class="text-right tabular-nums font-black text-muted-foreground text-xs sm:text-sm p-2 sm:p-4">
                {fmtMoney(grandTotal.avgCostLastMonth)}
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
    box-shadow: 4px 0 8px -4px rgba(0, 0, 0, 0.05);
  }
</style>
