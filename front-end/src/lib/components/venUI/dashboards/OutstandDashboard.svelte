<script lang="ts">
  import { onMount } from "svelte";
  import { slide, fade } from "svelte/transition";
  import * as Table from "$lib/components/ui/table";
  import { Icon } from "$lib/components/venUI/icon";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import { Button } from "$lib/components/ui/button";
  import { toast } from "$lib/components/venUI/toast";
  import { authStore } from "$lib/stores/auth";
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
  import { fetchOutstandingData } from "./outstand-dashboard/api";
  import type {
    OutstandingRow,
    OutstandingInvoice,
    AgingFilterOption,
    GroupingMode,
    TreeNode,
    TreeNodeAgg,
  } from "./outstand-dashboard/types";

  let loading = $state(false);
  let dateRange = $state<{ start: unknown; end: unknown }>({
    start: undefined,
    end: undefined,
  });

  let rows = $state<OutstandingRow[]>([]);
  let expanded = $state<Record<string, boolean>>({});
  let groupingMode = $state<GroupingMode>("region-dealer-customer");
  let agingFilter = $state<AgingFilterOption>("all");
  let selectedRegion = $state<string>("ALL");
  let selectedProduct = $state<string>("ALL");
  let selectedRespCenter = $state<string>("ALL");
  let searchQuery = $state<string>("");

  // Modal / Detail state
  let detailOpen = $state(false);
  let detailTitle = $state("");
  let detailSubtitle = $state("");
  let detailInvoices = $state<OutstandingInvoice[]>([]);
  let detailTotal = $state(0);

  let activeController: AbortController | null = null;
  let fetchId = 0;

  const workDate = $derived($authStore.user?.workDate);
  let sortField = $state<keyof TreeNodeAgg | "label">("totalBalance");
  let sortDir = $state<"asc" | "desc">("desc");

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
              fromDate(new Date(workDate), getLocalTimeZone())
            );
          }
        }
        try {
          return parseDate(workDate);
        } catch {
          return toCalendarDate(
            fromDate(new Date(workDate), getLocalTimeZone())
          );
        }
      }
      return today(getLocalTimeZone());
    } catch {
      return today(getLocalTimeZone());
    }
  });

  const availableRegions = $derived.by(() => {
    const set = new Set<string>();
    for (const r of rows) {
      if (r.region) set.add(r.region);
    }
    return Array.from(set).sort();
  });

  const availableProducts = $derived.by(() => {
    const set = new Set<string>();
    for (const r of rows) {
      if (r.product) set.add(r.product);
    }
    return Array.from(set).sort();
  });

  const availableRespCenters = $derived.by(() => {
    const set = new Set<string>();
    const locs = $authStore.locations ?? [];
    for (const l of locs) {
      if (l.code) set.add(l.code);
    }
    for (const r of rows) {
      if (r.respCenter) set.add(r.respCenter);
    }
    return Array.from(set).sort();
  });

  // Filter rows based on Aging Filter criteria
  const filteredRows = $derived.by(() => {
    let list = rows;

    if (selectedRegion !== "ALL") {
      list = list.filter((r) => r.region.toLowerCase() === selectedRegion.toLowerCase());
    }

    if (selectedProduct !== "ALL") {
      list = list.filter((r) => r.product.toLowerCase().includes(selectedProduct.toLowerCase()));
    }

    if (selectedRespCenter !== "ALL") {
      list = list.filter((r) => r.respCenter?.toLowerCase() === selectedRespCenter.toLowerCase());
    }

    if (searchQuery.trim()) {
      const q = searchQuery.trim().toLowerCase();
      list = list.filter(
        (r) =>
          r.customerName.toLowerCase().includes(q) ||
          r.dealerName.toLowerCase().includes(q) ||
          r.region.toLowerCase().includes(q) ||
          r.customerCode.toLowerCase().includes(q) ||
          r.dealerCode.toLowerCase().includes(q)
      );
    }

    if (agingFilter === "all") return list;

    return list.filter((r) => {
      if (agingFilter === "below30") return r.bucket0_30 > 0;
      if (agingFilter === "below60") return r.bucket0_30 + r.bucket31_60 > 0;
      if (agingFilter === "below90") return r.bucket0_30 + r.bucket31_60 + r.bucket61_90 > 0;
      if (agingFilter === "above30") return r.totalBalance - r.bucket0_30 > 0;
      if (agingFilter === "above60") return r.totalBalance - (r.bucket0_30 + r.bucket31_60) > 0;
      if (agingFilter === "above90") return r.bucket91_180 + r.bucket181_365 + r.bucketOver365 > 0;
      if (agingFilter === "above180") return r.bucket181_365 + r.bucketOver365 > 0;
      if (agingFilter === "above365") return r.bucketOver365 > 0;
      return true;
    });
  });

  function emptyAgg(): TreeNodeAgg {
    return {
      bucket0_30: 0,
      bucket31_60: 0,
      bucket61_90: 0,
      bucket91_180: 0,
      bucket181_365: 0,
      bucketOver365: 0,
      totalBalance: 0,
      invoicesCount: 0,
      customerCount: 0,
    };
  }

  function sumAgg(list: OutstandingRow[]): TreeNodeAgg {
    const custSet = new Set<string>();
    const res = emptyAgg();

    for (const r of list) {
      res.bucket0_30 += r.bucket0_30;
      res.bucket31_60 += r.bucket31_60;
      res.bucket61_90 += r.bucket61_90;
      res.bucket91_180 += r.bucket91_180;
      res.bucket181_365 += r.bucket181_365;
      res.bucketOver365 += r.bucketOver365;
      res.totalBalance += r.totalBalance;
      res.invoicesCount += r.invoicesCount;
      if (r.customerCode) custSet.add(r.customerCode);
    }
    res.customerCount = custSet.size;
    return res;
  }

  // Tree Builder logic
  const treeNodes = $derived.by((): TreeNode[] => {
    if (!filteredRows.length) return [];

    const regionMap = new Map<string, OutstandingRow[]>();
    for (const r of filteredRows) {
      const reg = r.region?.trim() || "Unassigned Region";
      if (!regionMap.has(reg)) regionMap.set(reg, []);
      regionMap.get(reg)!.push(r);
    }

    const result: TreeNode[] = [];

    for (const [regName, regRows] of regionMap.entries()) {
      const dealerMap = new Map<string, OutstandingRow[]>();
      for (const r of regRows) {
        const dKey = `${r.dealerCode}_${r.dealerName}`;
        if (!dealerMap.has(dKey)) dealerMap.set(dKey, []);
        dealerMap.get(dKey)!.push(r);
      }

      const dealerNodes: TreeNode[] = [];

      for (const [dKey, dRows] of dealerMap.entries()) {
        const firstD = dRows[0];
        const dealerLabel = `${firstD.dealerName} (${firstD.dealerCode})`;

        if (groupingMode === "region-dealer-customer") {
          // Region -> Dealer -> Customer
          const custNodes: TreeNode[] = dRows.map((r) => ({
            key: `reg_${regName}_dlr_${firstD.dealerCode}_cust_${r.customerCode}`,
            label: `${r.customerName} [${r.product}]`,
            code: r.customerCode,
            level: 2,
            nodeType: "customer" as const,
            agg: {
              bucket0_30: r.bucket0_30,
              bucket31_60: r.bucket31_60,
              bucket61_90: r.bucket61_90,
              bucket91_180: r.bucket91_180,
              bucket181_365: r.bucket181_365,
              bucketOver365: r.bucketOver365,
              totalBalance: r.totalBalance,
              invoicesCount: r.invoicesCount,
              customerCount: 1,
            },
            children: [],
            isLeaf: true,
            invoices: r.invoices,
            rawRow: r,
          }));

          dealerNodes.push({
            key: `reg_${regName}_dlr_${firstD.dealerCode}`,
            label: dealerLabel,
            code: firstD.dealerCode,
            level: 1,
            nodeType: "dealer" as const,
            agg: sumAgg(dRows),
            children: custNodes,
            isLeaf: custNodes.length === 0,
          });
        } else {
          // Region -> Dealer -> Product -> Customer
          const prodMap = new Map<string, OutstandingRow[]>();
          for (const r of dRows) {
            const p = r.product?.trim() || "General Product";
            if (!prodMap.has(p)) prodMap.set(p, []);
            prodMap.get(p)!.push(r);
          }

          const prodNodes: TreeNode[] = [];
          for (const [pName, pRows] of prodMap.entries()) {
            const custNodes: TreeNode[] = pRows.map((r) => ({
              key: `reg_${regName}_dlr_${firstD.dealerCode}_prod_${pName}_cust_${r.customerCode}`,
              label: r.customerName,
              code: r.customerCode,
              level: 3,
              nodeType: "customer" as const,
              agg: {
                bucket0_30: r.bucket0_30,
                bucket31_60: r.bucket31_60,
                bucket61_90: r.bucket61_90,
                bucket91_180: r.bucket91_180,
                bucket181_365: r.bucket181_365,
                bucketOver365: r.bucketOver365,
                totalBalance: r.totalBalance,
                invoicesCount: r.invoicesCount,
                customerCount: 1,
              },
              children: [],
              isLeaf: true,
              invoices: r.invoices,
              rawRow: r,
            }));

            prodNodes.push({
              key: `reg_${regName}_dlr_${firstD.dealerCode}_prod_${pName}`,
              label: `Product: ${pName}`,
              level: 2,
              nodeType: "product" as const,
              agg: sumAgg(pRows),
              children: custNodes,
              isLeaf: custNodes.length === 0,
            });
          }

          dealerNodes.push({
            key: `reg_${regName}_dlr_${firstD.dealerCode}`,
            label: dealerLabel,
            code: firstD.dealerCode,
            level: 1,
            nodeType: "dealer" as const,
            agg: sumAgg(dRows),
            children: prodNodes,
            isLeaf: prodNodes.length === 0,
          });
        }
      }

      result.push({
        key: `reg_${regName}`,
        label: `${regName} Region`,
        level: 0,
        nodeType: "region" as const,
        agg: sumAgg(regRows),
        children: dealerNodes,
        isLeaf: dealerNodes.length === 0,
      });
    }

    // Sort function for nodes at each level
    const sortNodes = (nodes: TreeNode[]) => {
      nodes.sort((a, b) => {
        let vA, vB;
        if (sortField === "label") {
          vA = a.label.toLowerCase();
          vB = b.label.toLowerCase();
        } else {
          vA = a.agg[sortField] ?? 0;
          vB = b.agg[sortField] ?? 0;
        }
        const mod = sortDir === "asc" ? 1 : -1;
        if (vA < vB) return -1 * mod;
        if (vA > vB) return 1 * mod;
        return 0;
      });

      for (const node of nodes) {
        if (node.children && node.children.length > 0) {
          sortNodes(node.children);
        }
      }
    };

    sortNodes(result);
    return result;
  });

  const grandTotal = $derived.by(() => {
    if (filteredRows.length === 0) return null;
    return sumAgg(filteredRows);
  });

  // Key KPI metrics derived
  const metrics = $derived.by(() => {
    if (rows.length === 0) return null;
    const totals = sumAgg(filteredRows);
    const criticalOver365 = totals.bucketOver365;
    const highRiskOver90 = totals.bucket91_180 + totals.bucket181_365 + totals.bucketOver365;

    // Find top customer balance
    let topCustName = "N/A";
    let topCustBal = 0;
    for (const r of filteredRows) {
      if (r.totalBalance > topCustBal) {
        topCustBal = r.totalBalance;
        topCustName = r.customerName;
      }
    }

    return {
      totalBalance: totals.totalBalance,
      highRiskOver90,
      criticalOver365,
      customerCount: totals.customerCount,
      topCustName,
      topCustBal,
    };
  });

  function fmtMoney(n: number | undefined) {
    if (n == null) return "₹0";
    return "₹" + Number(n).toLocaleString("en-IN", {
      maximumFractionDigits: 0,
    });
  }

  function toggleExpand(key: string) {
    expanded = { ...expanded, [key]: !expanded[key] };
  }

  function expandAll() {
    const next: Record<string, boolean> = {};
    const traverse = (nodes: TreeNode[]) => {
      for (const n of nodes) {
        if (!n.isLeaf) {
          next[n.key] = true;
          traverse(n.children);
        }
      }
    };
    traverse(treeNodes);
    expanded = next;
  }

  function collapseAll() {
    expanded = {};
  }

  function openDetails(node: TreeNode) {
    detailTitle = node.label;
    detailSubtitle = node.code ? `Code: ${node.code}` : `Level: ${node.nodeType.toUpperCase()}`;
    detailInvoices = node.invoices || [];
    detailTotal = node.agg.totalBalance;
    detailOpen = true;
  }

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
        getLocalTimeZone()
      ).toISOString();
    }
    return "";
  }

  async function loadData() {
    if (activeController) activeController.abort();
    activeController = new AbortController();
    const { signal } = activeController;
    const id = ++fetchId;

    loading = true;
    rows = [];
    expanded = {};

    const asOfDate = dateRange.end ? toIso(dateRange.end) : dateRange.start ? toIso(dateRange.start) : undefined;

    const res = await fetchOutstandingData(
      {
        asOfDate,
        region: selectedRegion,
        product: selectedProduct,
        respCenters: selectedRespCenter !== "ALL" ? [selectedRespCenter] : undefined,
        agingFilter,
        search: searchQuery,
      },
      signal
    );

    if (signal.aborted || id !== fetchId) return;

    loading = false;
    if (!res.success) {
      if (res.error) toast.error(res.error);
      return;
    }
    rows = res.rows;
    if (rows.length === 0) {
      toast.info("No outstanding balance records found.");
    } else {
      // Auto-expand region level by default
      const defaultExpanded: Record<string, boolean> = {};
      for (const r of rows) {
        if (r.region) defaultExpanded[`reg_${r.region}`] = true;
      }
      expanded = defaultExpanded;
    }
  }

  onMount(() => {
    const start = startOfMonth(ref);
    const end = endOfMonth(ref);
    dateRange = { start, end };
    loadData();
  });
</script>

{#snippet treeRow(node: TreeNode)}
  <Table.Row
    class="group/row relative transition-all duration-200 {node.isLeaf
      ? 'hover:bg-muted/30'
      : 'cursor-pointer bg-muted/5 hover:bg-muted/15 font-semibold'}"
    onclick={() => {
      if (!node.isLeaf) toggleExpand(node.key);
      else openDetails(node);
    }}
  >
    <Table.Cell
      class="cd-table-cell-sticky sticky left-0 z-10 min-w-[140px] sm:min-w-[220px] max-w-[180px] sm:max-w-[260px] bg-background/90 backdrop-blur-md p-2 sm:p-3.5 whitespace-normal break-words"
      style="padding-left: calc({node.level} * var(--indent-step, 1rem) + 0.5rem)"
    >
      <div class="flex items-center gap-1.5 sm:gap-2">
        {#if !node.isLeaf}
          <div
            class="flex size-4 sm:size-5 shrink-0 items-center justify-center rounded-md border border-border bg-background transition-transform duration-200 {expanded[
              node.key
            ]
              ? 'rotate-90'
              : ''}"
          >
            <Icon name="chevron-right" class="size-2.5 sm:size-3" />
          </div>
        {:else}
          <div class="flex size-4 sm:size-5 shrink-0 items-center justify-center text-primary/70">
            <Icon name="file-text" class="size-3" />
          </div>
        {/if}

        <div class="flex flex-col min-w-0">
          <span
            class="whitespace-normal break-words leading-tight text-xs sm:text-sm {node.level === 0
              ? 'font-black text-foreground text-sm sm:text-base'
              : node.level === 1
                ? 'font-bold text-foreground'
                : node.isLeaf
                  ? 'text-muted-foreground hover:text-foreground'
                  : 'font-medium text-foreground'}"
          >
            {node.label}
          </span>
          {#if node.nodeType === "customer" && node.code}
            <span class="text-[10px] text-muted-foreground/70 font-mono">Code: {node.code}</span>
          {/if}
        </div>
      </div>
      {#if !node.isLeaf && expanded[node.key]}
        <div
          class="absolute left-0 top-0 h-full w-0.5 sm:w-1 bg-primary/40"
          style="margin-left: calc({node.level} * var(--indent-step, 1rem) + 0.25rem)"
        ></div>
      {/if}
    </Table.Cell>

    <!-- 0-30 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-foreground/80">
      {fmtMoney(node.agg.bucket0_30)}
    </Table.Cell>

    <!-- 31-60 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-foreground/80">
      {fmtMoney(node.agg.bucket31_60)}
    </Table.Cell>

    <!-- 61-90 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-amber-600 dark:text-amber-400 font-medium">
      {fmtMoney(node.agg.bucket61_90)}
    </Table.Cell>

    <!-- 91-180 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-orange-600 dark:text-orange-400 font-semibold">
      {fmtMoney(node.agg.bucket91_180)}
    </Table.Cell>

    <!-- 181-365 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-rose-600 dark:text-rose-400 font-bold">
      {fmtMoney(node.agg.bucket181_365)}
    </Table.Cell>

    <!-- >365 Days -->
    <Table.Cell class="text-right tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-red-600 dark:text-red-400 font-black">
      {fmtMoney(node.agg.bucketOver365)}
    </Table.Cell>

    <!-- Total Balance -->
    <Table.Cell class="text-right tabular-nums font-black text-xs sm:text-sm p-2 sm:p-3.5 text-primary">
      {fmtMoney(node.agg.totalBalance)}
    </Table.Cell>

    <!-- Invoices Count -->
    <Table.Cell class="text-center tabular-nums text-xs sm:text-sm p-2 sm:p-3.5 text-muted-foreground">
      {node.agg.invoicesCount}
    </Table.Cell>

    <!-- Action -->
    <Table.Cell class="text-center p-2 sm:p-3.5">
      <Button
        variant="ghost"
        size="icon"
        class="size-7 rounded-full hover:bg-primary/10 hover:text-primary transition-colors"
        onclick={(e) => {
          e.stopPropagation();
          openDetails(node);
        }}
        title="View Line-Item Details"
      >
        <Icon name="external-link" class="size-3.5" />
      </Button>
    </Table.Cell>
  </Table.Row>

  {#if !node.isLeaf && expanded[node.key]}
    {#each node.children as child (child.key)}
      {@render treeRow(child)}
    {/each}
  {/if}
{/snippet}

{#snippet sortIcon(field: typeof sortField)}
  <div class="ml-1 inline-flex flex-col opacity-30 group-hover/head:opacity-100 transition-opacity {sortField === field ? 'opacity-100' : ''}">
    <Icon 
      name={sortDir === 'asc' && sortField === field ? 'chevron-up' : 'chevron-down'} 
      class="size-3 {sortField === field ? 'text-primary' : 'text-muted-foreground'}" 
    />
  </div>
{/snippet}

<div class="cd-root min-h-full w-full space-y-4 sm:space-y-6 p-3 sm:p-4 md:p-6" in:fade={{ duration: 300 }}>
  <!-- Top Header & Filter Card -->
  <div class="cd-header-card rounded-2xl border border-border bg-card/40 p-4 sm:p-6 shadow-xl backdrop-blur-xl transition-all">
    <div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between w-full">
      <div class="flex items-center gap-3.5">
        <div class="flex size-11 items-center justify-center rounded-2xl bg-primary/10 text-primary shrink-0 shadow-inner">
          <Icon name="wallet" class="size-6" />
        </div>
        <div>
          <h1 class="text-lg sm:text-xl font-black tracking-tight text-foreground flex items-center gap-2">
            Outstanding Customer Balance
          </h1>
          <p class="text-xs text-muted-foreground">Aging receivables intelligence & credit risk control</p>
        </div>
      </div>

      <!-- Quick Action Controls -->
      <div class="flex flex-wrap items-center gap-2 w-full lg:w-auto">
        <div class="w-full sm:w-[220px]">
          <DatePicker
            value={dateRange}
            mode="range"
            valueType="calendar"
            placeholder="As of Period"
            workdate={workDate}
            presetKeys="thisMonth,lastMonth,thisQuarter,lastQuarter,thisFinYear"
            fiscal
          />
        </div>

        <Button
          variant="default"
          class="h-9 w-full sm:w-auto bg-primary px-6 font-bold text-primary-foreground shadow-lg transition-all hover:scale-[1.02] active:scale-[0.98]"
          onclick={loadData}
          disabled={loading}
        >
          {#if loading}
            <Icon name="loader-circle" class="mr-2 size-4 animate-spin" />
          {:else}
            <Icon name="search" class="mr-2 size-4" />
          {/if}
          Generate
        </Button>
      </div>
    </div>

    <!-- Secondary Filters Bar -->
    <div class="mt-4 pt-4 border-t border-border/40 grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-3">
      <!-- Search Input -->
      <div class="relative flex items-center">
        <Icon name="search" class="absolute left-3 size-4 text-muted-foreground" />
        <input
          type="text"
          placeholder="Search Customer / Dealer..."
          bind:value={searchQuery}
          class="h-9 w-full rounded-xl border border-input bg-background/50 pl-9 pr-3 text-xs sm:text-sm font-medium placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-primary/40"
        />
        {#if searchQuery}
          <button
            class="absolute right-3 text-muted-foreground hover:text-foreground"
            onclick={() => (searchQuery = "")}
          >
            <Icon name="x" class="size-3.5" />
          </button>
        {/if}
      </div>

      <!-- Responsibility Center Filter -->
      <div class="flex items-center gap-2 bg-background/50 border border-input rounded-xl px-3 h-9">
        <Icon name="building" class="size-4 text-muted-foreground shrink-0" />
        <select
          bind:value={selectedRespCenter}
          class="w-full bg-transparent text-xs sm:text-sm font-medium focus:outline-none cursor-pointer"
        >
          <option value="ALL">All Resp Centers</option>
          {#each availableRespCenters as rc}
            <option value={rc}>{rc}</option>
          {/each}
        </select>
      </div>

      <!-- Region Filter -->
      <div class="flex items-center gap-2 bg-background/50 border border-input rounded-xl px-3 h-9">
        <Icon name="map-pin" class="size-4 text-muted-foreground shrink-0" />
        <select
          bind:value={selectedRegion}
          class="w-full bg-transparent text-xs sm:text-sm font-medium focus:outline-none cursor-pointer"
        >
          <option value="ALL">All Regions</option>
          {#each availableRegions as reg}
            <option value={reg}>{reg} Region</option>
          {/each}
        </select>
      </div>

      <!-- Product Filter -->
      <div class="flex items-center gap-2 bg-background/50 border border-input rounded-xl px-3 h-9">
        <Icon name="package" class="size-4 text-muted-foreground shrink-0" />
        <select
          bind:value={selectedProduct}
          class="w-full bg-transparent text-xs sm:text-sm font-medium focus:outline-none cursor-pointer"
        >
          <option value="ALL">All Products</option>
          {#each availableProducts as prod}
            <option value={prod}>{prod}</option>
          {/each}
        </select>
      </div>

      <!-- Aging Bucket Filter -->
      <div class="flex items-center gap-2 bg-background/50 border border-input rounded-xl px-3 h-9">
        <Icon name="clock" class="size-4 text-muted-foreground shrink-0" />
        <select
          bind:value={agingFilter}
          class="w-full bg-transparent text-xs sm:text-sm font-medium focus:outline-none cursor-pointer text-primary"
        >
          <option value="all">All Aging Buckets</option>
          <option value="below30">Below 30 Days (Current)</option>
          <option value="below60">Below 60 Days</option>
          <option value="below90">Below 90 Days</option>
          <option value="above30">Above 30 Days Overdue</option>
          <option value="above60">Above 60 Days Overdue</option>
          <option value="above90">Above 90 Days Overdue</option>
          <option value="above180">Above 180 Days Overdue</option>
          <option value="above365">Above 365 Days (Critical)</option>
        </select>
      </div>
    </div>
  </div>

  <!-- KPI Summary Cards -->
  {#if metrics}
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-5" in:slide={{ duration: 300 }}>
      <!-- Total Outstanding -->
      <div class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 shadow-md backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:shadow-xl">
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Total Receivables</p>
        <p class="mt-1.5 text-xl sm:text-2xl font-black tabular-nums tracking-tight text-primary">
          {fmtMoney(metrics.totalBalance)}
        </p>
        <p class="mt-2 text-[10px] text-muted-foreground flex items-center gap-1">
          <Icon name="users" class="size-3" />
          <span>{metrics.customerCount} Active Accounts</span>
        </p>
      </div>

      <!-- High Risk (>90 Days) -->
      <div class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 shadow-md backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:shadow-xl">
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Overdue &gt; 90 Days</p>
        <p class="mt-1.5 text-xl sm:text-2xl font-black tabular-nums tracking-tight text-amber-500">
          {fmtMoney(metrics.highRiskOver90)}
        </p>
        <p class="mt-2 text-[10px] text-amber-600/90 font-medium">Require Payment Follow-up</p>
      </div>

      <!-- Critical (>365 Days) -->
      <div class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 shadow-md backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:shadow-xl">
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Critical &gt; 365 Days</p>
        <p class="mt-1.5 text-xl sm:text-2xl font-black tabular-nums tracking-tight text-red-500">
          {fmtMoney(metrics.criticalOver365)}
        </p>
        <p class="mt-2 text-[10px] text-red-500 font-bold flex items-center gap-1">
          <Icon name="alert-circle" class="size-3" />
          <span>Action Required</span>
        </p>
      </div>

      <!-- Top Customer Balance -->
      <div class="group relative overflow-hidden rounded-2xl border border-border bg-card/40 p-4 shadow-md backdrop-blur-xl transition-all hover:-translate-y-0.5 hover:shadow-xl col-span-1 sm:col-span-2 lg:col-span-2">
        <p class="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Highest Outstanding Customer</p>
        <div class="mt-1 flex items-baseline justify-between gap-2">
          <p class="text-sm sm:text-base font-bold text-foreground truncate max-w-[200px] sm:max-w-[280px]">
            {metrics.topCustName}
          </p>
          <p class="text-base sm:text-lg font-black text-rose-500 tabular-nums">
            {fmtMoney(metrics.topCustBal)}
          </p>
        </div>
        <p class="mt-2 text-[10px] text-muted-foreground">Top exposure account in current filter selection</p>
      </div>
    </div>
  {/if}

  <!-- View Modes & Expand / Collapse Controls -->
  <div class="flex flex-wrap items-center justify-between gap-3">
    <div class="flex items-center gap-2">
      <span class="text-xs font-bold text-muted-foreground uppercase tracking-wider">Grouping:</span>
      <button
        class="rounded-full px-3.5 py-1.5 text-xs font-bold tracking-wide transition-all {groupingMode ===
        'region-dealer-customer'
          ? 'bg-foreground text-background shadow-md'
          : 'bg-muted/50 text-muted-foreground hover:bg-muted/80'}"
        onclick={() => (groupingMode = "region-dealer-customer")}
      >
        Region → Dealer → Customer
      </button>
      <button
        class="rounded-full px-3.5 py-1.5 text-xs font-bold tracking-wide transition-all {groupingMode ===
        'region-dealer-product-customer'
          ? 'bg-foreground text-background shadow-md'
          : 'bg-muted/50 text-muted-foreground hover:bg-muted/80'}"
        onclick={() => (groupingMode = "region-dealer-product-customer")}
      >
        Dealer.Product Separation
      </button>
    </div>

    <div class="flex items-center gap-2">
      <Button variant="outline" size="sm" class="h-8 text-xs font-semibold" onclick={expandAll}>
        <Icon name="chevrons-down" class="mr-1.5 size-3.5" />
        Expand All
      </Button>
      <Button variant="outline" size="sm" class="h-8 text-xs font-semibold" onclick={collapseAll}>
        <Icon name="chevrons-up" class="mr-1.5 size-3.5" />
        Collapse All
      </Button>
    </div>
  </div>

  <!-- Main Tree Grid Table -->
  {#if loading}
    <div class="flex min-h-[350px] flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed border-border/60 bg-muted/5">
      <Icon name="loader-circle" class="size-10 animate-spin text-primary" />
      <p class="animate-pulse text-xs font-bold uppercase tracking-widest text-muted-foreground">
        Synthesizing Outstanding Receivables...
      </p>
    </div>
  {:else if treeNodes.length > 0}
    <div class="overflow-hidden rounded-xl border border-border/60 bg-card/30 shadow-xl backdrop-blur-xl">
      <div class="overflow-x-auto">
        <Table.Root class="w-full border-collapse text-sm">
          <Table.Header>
            <Table.Row class="bg-muted/50 hover:bg-muted/50">
              <Table.Head
                class="sticky left-0 z-20 min-w-[140px] sm:min-w-[220px] max-w-[180px] sm:max-w-[260px] bg-muted/90 py-3 px-3 text-[10px] font-black uppercase tracking-widest cursor-pointer group/head whitespace-normal leading-tight backdrop-blur-md"
                onclick={() => toggleSort("label")}
              >
                <div class="flex items-center gap-1">
                  <span>Entity / Account</span>
                  {@render sortIcon("label")}
                </div>
              </Table.Head>

              <!-- 0-30 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest cursor-pointer group/head min-w-[90px]"
                onclick={() => toggleSort("bucket0_30")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>0-30 Days</span>
                  {@render sortIcon("bucket0_30")}
                </div>
              </Table.Head>

              <!-- 31-60 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest cursor-pointer group/head min-w-[90px]"
                onclick={() => toggleSort("bucket31_60")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>31-60 Days</span>
                  {@render sortIcon("bucket31_60")}
                </div>
              </Table.Head>

              <!-- 61-90 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest text-amber-600 cursor-pointer group/head min-w-[90px]"
                onclick={() => toggleSort("bucket61_90")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>61-90 Days</span>
                  {@render sortIcon("bucket61_90")}
                </div>
              </Table.Head>

              <!-- 91-180 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest text-orange-600 cursor-pointer group/head min-w-[95px]"
                onclick={() => toggleSort("bucket91_180")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>91-180 Days</span>
                  {@render sortIcon("bucket91_180")}
                </div>
              </Table.Head>

              <!-- 181-365 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest text-rose-600 cursor-pointer group/head min-w-[100px]"
                onclick={() => toggleSort("bucket181_365")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>181-365 Days</span>
                  {@render sortIcon("bucket181_365")}
                </div>
              </Table.Head>

              <!-- >365 Days -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest text-red-600 cursor-pointer group/head min-w-[95px]"
                onclick={() => toggleSort("bucketOver365")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>&gt;365 Days</span>
                  {@render sortIcon("bucketOver365")}
                </div>
              </Table.Head>

              <!-- Total Balance -->
              <Table.Head
                class="text-right py-3 px-3 text-[10px] font-black uppercase tracking-widest text-primary cursor-pointer group/head min-w-[110px]"
                onclick={() => toggleSort("totalBalance")}
              >
                <div class="flex items-center justify-end gap-1">
                  <span>Total Balance</span>
                  {@render sortIcon("totalBalance")}
                </div>
              </Table.Head>

              <!-- Invoices Count -->
              <Table.Head class="text-center py-3 px-3 text-[10px] font-black uppercase tracking-widest min-w-[70px]">
                Invoices
              </Table.Head>

              <!-- Details -->
              <Table.Head class="text-center py-3 px-3 text-[10px] font-black uppercase tracking-widest min-w-[60px]">
                Detail
              </Table.Head>
            </Table.Row>
          </Table.Header>

          <Table.Body>
            {#each treeNodes as node (node.key)}
              {@render treeRow(node)}
            {/each}
          </Table.Body>

          {#if grandTotal}
            <Table.Footer class="bg-muted/60 border-t-2 border-border/80">
              <Table.Row class="hover:bg-muted/70">
                <Table.Cell class="sticky left-0 z-10 bg-muted/95 backdrop-blur-md font-black text-[10px] uppercase tracking-widest px-3 py-3.5">
                  Grand Total
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucket0_30)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucket31_60)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-amber-600 dark:text-amber-400 text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucket61_90)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-orange-600 dark:text-orange-400 text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucket91_180)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-rose-600 dark:text-rose-400 text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucket181_365)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-bold text-red-600 dark:text-red-400 text-xs sm:text-sm p-3">
                  {fmtMoney(grandTotal.bucketOver365)}
                </Table.Cell>
                <Table.Cell class="text-right tabular-nums font-black text-primary text-sm sm:text-base p-3">
                  {fmtMoney(grandTotal.totalBalance)}
                </Table.Cell>
                <Table.Cell class="text-center font-bold text-xs sm:text-sm p-3">
                  {grandTotal.invoicesCount}
                </Table.Cell>
                <Table.Cell class="text-center p-3" />
              </Table.Row>
            </Table.Footer>
          {/if}
        </Table.Root>
      </div>
    </div>
  {:else if !loading && treeNodes.length === 0}
    <div class="flex min-h-[250px] flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-border/60 bg-muted/5 text-muted-foreground">
      <Icon name="search-x" class="size-10 opacity-30" />
      <p class="font-bold uppercase tracking-widest text-xs opacity-60">No outstanding customer accounts found</p>
    </div>
  {/if}
</div>

<!-- Invoice Details Drawer / Dialog Modal -->
{#if detailOpen}
  <div
    class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-6 bg-background/80 backdrop-blur-md"
    in:fade={{ duration: 200 }}
    out:fade={{ duration: 150 }}
  >
    <div
      class="relative w-full max-w-4xl max-h-[90vh] flex flex-col rounded-2xl border border-border bg-card shadow-2xl overflow-hidden"
    >
      <!-- Modal Header -->
      <div class="flex items-center justify-between border-b border-border p-4 sm:p-5 bg-muted/20">
        <div class="flex items-center gap-3">
          <div class="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary shrink-0">
            <Icon name="receipt" class="size-5" />
          </div>
          <div>
            <h2 class="text-base sm:text-lg font-black text-foreground">{detailTitle}</h2>
            <p class="text-xs text-muted-foreground">{detailSubtitle} • Total Bal: <span class="font-bold text-primary">{fmtMoney(detailTotal)}</span></p>
          </div>
        </div>

        <Button
          variant="ghost"
          size="icon"
          class="rounded-full size-8 hover:bg-muted"
          onclick={() => (detailOpen = false)}
        >
          <Icon name="x" class="size-5" />
        </Button>
      </div>

      <!-- Modal Content / Invoice List -->
      <div class="p-4 sm:p-6 overflow-y-auto flex-1 space-y-4">
        {#if detailInvoices.length > 0}
          <div class="overflow-x-auto rounded-xl border border-border/60">
            <Table.Root class="w-full text-xs sm:text-sm">
              <Table.Header class="bg-muted/40">
                <Table.Row>
                  <Table.Head class="font-bold">Invoice #</Table.Head>
                  <Table.Head class="font-bold">Inv Date</Table.Head>
                  <Table.Head class="font-bold">Due Date</Table.Head>
                  <Table.Head class="font-bold">Product</Table.Head>
                  <Table.Head class="text-right font-bold">Total Bill</Table.Head>
                  <Table.Head class="text-right font-bold text-primary">Outstanding</Table.Head>
                  <Table.Head class="text-center font-bold">Overdue</Table.Head>
                  <Table.Head class="text-center font-bold">Risk Status</Table.Head>
                </Table.Row>
              </Table.Header>
              <Table.Body>
                {#each detailInvoices as inv}
                  <Table.Row class="hover:bg-muted/20">
                    <Table.Cell class="font-mono font-bold text-foreground">{inv.invoiceNo}</Table.Cell>
                    <Table.Cell class="text-muted-foreground">{inv.invoiceDate}</Table.Cell>
                    <Table.Cell class="text-muted-foreground">{inv.dueDate}</Table.Cell>
                    <Table.Cell class="font-medium text-foreground">{inv.product}</Table.Cell>
                    <Table.Cell class="text-right tabular-nums text-muted-foreground">{fmtMoney(inv.totalAmount)}</Table.Cell>
                    <Table.Cell class="text-right tabular-nums font-bold text-primary">{fmtMoney(inv.outstandingAmount)}</Table.Cell>
                    <Table.Cell class="text-center tabular-nums font-medium">
                      {inv.daysOverdue} days
                    </Table.Cell>
                    <Table.Cell class="text-center">
                      <span
                        class="inline-flex items-center rounded-full px-2.5 py-0.5 text-[10px] font-extrabold uppercase tracking-wider {inv.status ===
                        'Critical'
                          ? 'bg-red-500/10 text-red-600 border border-red-500/20'
                          : inv.status === 'High Risk'
                            ? 'bg-rose-500/10 text-rose-600 border border-rose-500/20'
                            : inv.status === 'Watch'
                              ? 'bg-amber-500/10 text-amber-600 border border-amber-500/20'
                              : 'bg-green-500/10 text-green-600 border border-green-500/20'}"
                      >
                        {inv.status}
                      </span>
                    </Table.Cell>
                  </Table.Row>
                {/each}
              </Table.Body>
            </Table.Root>
          </div>
        {:else}
          <div class="flex flex-col items-center justify-center p-8 text-center text-muted-foreground gap-2">
            <Icon name="info" class="size-8 opacity-40" />
            <p class="text-sm font-medium">No individual invoice line items available for this selection.</p>
          </div>
        {/if}
      </div>

      <!-- Modal Footer -->
      <div class="flex items-center justify-end border-t border-border p-4 bg-muted/10">
        <Button variant="outline" onclick={() => (detailOpen = false)}>Close</Button>
      </div>
    </div>
  </div>
{/if}

<style>
  .cd-root {
    background: radial-gradient(
      circle at top right,
      hsl(var(--primary) / 0.03),
      transparent 40%
    );
  }

  .cd-header-card {
    background-image: linear-gradient(
        to bottom right,
        hsl(var(--card) / 0.8),
        hsl(var(--card) / 0.4)
      ),
      url("data:image/svg+xml,%3Csvg width='20' height='20' viewBox='0 0 20 20' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='%239C92AC' fill-opacity='0.05' fill-rule='evenodd'%3E%3Ccircle cx='3' cy='3' r='3'/%3E%3Ccircle cx='13' cy='13' r='3'/%3E%3C/g%3E%3C/svg%3E");
  }

  :global(.cd-table-cell-sticky) {
    box-shadow: 4px 0 8px -4px rgba(0, 0, 0, 0.05);
  }
</style>
