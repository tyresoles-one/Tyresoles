<script lang="ts">
  import { onMount, untrack } from "svelte";
  import { fade } from "svelte/transition";
  import { authStore, getUser } from "$lib/stores/auth";
  import { toast } from "$lib/components/venUI/toast";
  import { scaleBand } from "d3-scale";
  import { BarChart } from "layerchart";
  import TrendingUpIcon from "@lucide/svelte/icons/trending-up";
  import TrendingDownIcon from "@lucide/svelte/icons/trending-down";
  import * as Card from "$lib/components/ui/card/index.js";
  import { fetchSalesChart } from "./sales-chart/api";
  import type { MonthlySalesRow } from "./sales-chart/types";
  import { fetchDashboard } from "./classic-dashboard/api";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { Icon } from "$lib/components/venUI/icon";
  import { Select } from "$lib/components/venUI/select";
  import { cn } from "$lib/utils";
  import Tile from "./Tile.svelte";

  let { class: className = "" }: { class?: string } = $props();

  const user = getUser();
  
  // Unified state
  let selectedRespCenters = $state<string[]>([]);
  let loading = $state(false);
  let error = $state<string | null>(null);
  
  // Data states
  let summaryData = $state<any>(null);
  let chartRows = $state<MonthlySalesRow[]>([]);
  
  let fetchController: AbortController | null = null;

  const availableLocations = $derived.by(() => {
    const locs = $authStore.locations;
    if (locs && locs.length > 0) {
      return locs.filter(l => (l as any).sale === 1);
    }
    return [];
  });

  const activeRespCenters = $derived.by(() => {
    if (selectedRespCenters && selectedRespCenters.length > 0) return selectedRespCenters;
    
    if (availableLocations.length > 0) {
      return availableLocations.map(l => l.code).filter(Boolean);
    }
    return user?.respCenter ? [user.respCenter] : [];
  });

  const chartConfig = {
    sale: { label: "Sales", color: "oklch(0.86 0.17 92)" },
  };

  async function load() {
    if (fetchController) fetchController.abort();
    fetchController = new AbortController();
    loading = true;
    error = null;
    
    const apiParams = {
        entityType: user?.entityType ?? undefined,
        entityCode: user?.entityCode ?? undefined,
        entityDepartment: user?.department ?? undefined,
        workDate: user?.workDate,
        respCenters: activeRespCenters.length > 0 ? activeRespCenters : undefined
    } as any;

    try {
        const [chartRes, summaryRes] = await Promise.all([
            fetchSalesChart(apiParams, fetchController.signal),
            fetchDashboard({ ...apiParams, reportName: 'summary' }, fetchController.signal)
        ]);

        if (chartRes.success && chartRes.data) {
            chartRows = chartRes.data;
        } else if (chartRes.error && chartRes.error !== "AbortError") {
            throw new Error(chartRes.error);
        }

        if (summaryRes.success && summaryRes.data) {
            summaryData = summaryRes.data;
        } else if (summaryRes.error && summaryRes.error !== "AbortError") {
            throw new Error(summaryRes.error);
        }
    } catch (e: any) {
        if (e.name !== "AbortError") {
            error = e.message || "Failed to load dashboard data";
            toast.error(error!);
        }
    } finally {
        loading = false;
    }
  }

  $effect(() => {
    // React to filter changes
    const _len1 = selectedRespCenters?.length ?? 0;
    const _len2 = availableLocations?.length ?? 0;
    untrack(() => {
      load();
    });
  });

  const stats = $derived(summaryData?.tiles || summaryData?.Tiles || []);

  const trend = $derived.by(() => {
    if (chartRows.length < 2) return null;
    const last = chartRows[chartRows.length - 1].sale;
    const prev = chartRows[chartRows.length - 2].sale;
    if (prev <= 0) return null;
    const diff = ((last - prev) / prev) * 100;
    return {
      percentage: Math.abs(diff).toFixed(1),
      isUp: diff >= 0
    };
  });

  const dateDescription = $derived.by(() => {
    if (chartRows.length === 0) return "Loading...";
    return `${chartRows[0].month} - ${chartRows[chartRows.length - 1].month}`;
  });

  function formatValue(val: any, unit?: string, showUnit: boolean = true) {
    if (val === undefined || val === null || isNaN(val)) return "";
    if (typeof val !== "number") return String(val);
    if (val === 0) return "0";
    const u = (unit || "").trim().toLowerCase();
    const suffix = showUnit ? (u === "cr" ? "Cr" : "L") : "";
    if (u === "cr") return val.toFixed(2) + suffix;
    return val.toFixed(1) + suffix;
  }

  const currentUnit = $derived(chartRows.length > 0 ? (chartRows[0].unit || "L") : "L");
</script>

<div class="w-full h-full {className}">
  {#if error}
    <Card.Root class="h-full flex flex-col min-h-[450px]">
      <Card.Content class="pt-6 flex-1 flex items-center">
        <div class="w-full rounded-xl border border-destructive/20 bg-destructive/5 p-4 text-sm text-destructive flex items-center justify-between" in:fade>
          <div class="flex items-center gap-2">
            <Icon name="circle-alert" class="size-6 text-destructive/50" />
            <span>{error}</span>
          </div>
          <button onclick={load} class="text-xs font-bold uppercase tracking-wider hover:underline px-3 py-1 bg-destructive/10 rounded-md transition-colors hover:bg-destructive/20">Retry</button>
        </div>
      </Card.Content>
    </Card.Root>
  {:else if loading && chartRows.length === 0 && stats.length === 0}
    <Card.Root class="h-full flex flex-col min-h-[450px]">
      <Card.Header class="px-4 py-3 border-b border-border/40">
        <div class="space-y-2">
          <Skeleton class="h-5 w-40" />
          <Skeleton class="h-4 w-32" />
        </div>
      </Card.Header>
      <Card.Content class="flex-1 p-4 flex flex-col gap-6">
        <div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
           <Skeleton class="h-24 w-full rounded-xl" />
           <Skeleton class="h-24 w-full rounded-xl" />
           <Skeleton class="h-24 w-full rounded-xl hidden lg:block" />
        </div>
        <Skeleton class="flex-1 min-h-[250px] w-full rounded-lg" />
      </Card.Content>
    </Card.Root>
  {:else if chartRows.length === 0 && stats.length === 0 && !loading}
    <Card.Root class="h-full flex flex-col min-h-[450px]">
      <Card.Content class="flex-1 flex flex-col items-center justify-center p-12 text-center text-muted-foreground gap-3">
        <div class="rounded-full bg-muted/50 p-4 transition-transform hover:scale-110">
          <Icon name="chart-column" class="size-8 opacity-30" />
        </div>
        <div class="space-y-1">
          <p class="text-sm font-semibold text-foreground">No recent sales data</p>
          <p class="text-xs">No sales activity tracked for the last 6 months.</p>
        </div>
        <button onclick={load} class="mt-2 text-xs font-medium text-primary hover:underline">Refresh Dashboard</button>
      </Card.Content>
    </Card.Root>
  {:else}
    <Card.Root class="overflow-hidden border-none shadow-sm sm:border sm:shadow-md h-full min-h-[450px] flex flex-col">
      <Card.Header class="flex flex-col sm:flex-row items-start sm:items-center justify-between px-3 py-3 sm:px-4 sm:py-3 shrink-0 gap-3 sm:gap-0">
        <div>
          <Card.Title class="text-base font-bold tracking-tight">Sales Overview</Card.Title>
          <Card.Description class="text-xs font-medium text-muted-foreground mt-0.5">
             {dateDescription}
          </Card.Description>
        </div>
        <div class="flex items-center gap-2 w-full sm:w-auto">
          {#if availableLocations.length > 1}
            <Select 
              options={availableLocations}
              bind:value={selectedRespCenters}
              valueKey="code"
              labelKey="name"
              multiple={true}
              placeholder="All Locations"
              class="w-full sm:w-[200px] h-8 text-xs font-medium bg-background"
            />
          {/if}
          <button 
            onclick={load} 
            class="inline-flex size-8 shrink-0 items-center justify-center rounded-md bg-background border shadow-sm hover:bg-muted/50 transition-all duration-200 active:scale-95 group/btn" 
            disabled={loading}
          >
            <Icon name="refresh-cw" class={cn("size-4 text-muted-foreground group-hover/btn:text-foreground", loading && "animate-spin")} />
          </button>
        </div>
      </Card.Header>
      
      <Card.Content class="p-3 pt-0 sm:p-4 sm:pt-0 flex-1 flex flex-col gap-4">
        <!-- Unified Tiles Grid -->
        {#if stats.length > 0}
            <div class="grid gap-3 grid-cols-1 sm:grid-cols-2 lg:grid-cols-3">
                {#each stats as stat, i}
                    <Tile {...stat} delay={i * 80} duration={400} y={10} />
                {/each}
            </div>
        {/if}

        <!-- Unified Chart -->
        {#if chartRows.length > 0}
            <div class="h-[280px] sm:h-[320px] w-full pt-2">
                <BarChart
                    data={chartRows}
                    x="month"
                    y="sale"
                    series={[
                        { key: "sale", label: "Monthly Sale", color: chartConfig.sale.color },
                    ]}
                    padding={{ top: 20, bottom: 25, left: 50, right: 10 }}
                    xScale={scaleBand().padding(0.4)}
                    labels={true}
                    props={{
                        bars: { 
                            radius: 4, 
                            rounded: "top",
                            fill: chartConfig.sale.color,
                            stroke: chartConfig.sale.color,
                            strokeWidth: 2,
                            class: "transition-all duration-500 hover:opacity-80"
                        },
                        labels: {
                            format: (v) => formatValue(v, currentUnit, false),
                            class: "text-[10px] font-bold fill-foreground",
                            offset: 4
                        },
                        xAxis: {
                            format: (d) => typeof d === 'string' ? d.split('-')[0] : d,
                            class: "text-[10px] font-medium fill-muted-foreground"
                        },
                        yAxis: {
                            ticks: 5,
                            format: (v) => formatValue(v, currentUnit, true),
                            class: "text-[10px] font-medium fill-muted-foreground"
                        },
                        grid: {
                            y: { class: "stroke-muted/20 stroke-dashed" }
                        }
                    }}
                >
                    {#snippet tooltip(props)}
                        {#if props?.data}
                            <div class="p-3 bg-background border rounded-lg shadow-xl text-sm min-w-[120px]">
                                <div class="font-bold border-b pb-1.5 mb-2 text-muted-foreground text-xs uppercase tracking-wider">{props.data.month}</div>
                                <div class="flex items-center gap-2">
                                    <div class="size-2.5 rounded-full" style="background: {chartConfig.sale.color}"></div>
                                    <span class="font-semibold text-foreground">{formatValue(props.data.sale, currentUnit)}</span>
                                </div>
                            </div>
                        {/if}
                    {/snippet}
                </BarChart>
            </div>
        {/if}
      </Card.Content>
      
      {#if chartRows.length > 0}
          <Card.Footer class="px-3 pb-3 pt-0 sm:px-4 sm:pb-4 sm:pt-0">
            <div class="flex w-full items-start gap-2 text-sm bg-muted/20 p-2 sm:p-3 rounded-md sm:rounded-lg border border-border/40">
              <div class="grid gap-1">
                {#if trend}
                  <div class="flex items-center gap-2 leading-none font-bold text-foreground">
                    {trend.isUp ? 'Trending up' : 'Trending down'} by {trend.percentage}% this month 
                    {#if trend.isUp}
                      <div class="bg-emerald-500/10 p-1 rounded text-emerald-500">
                          <TrendingUpIcon class="size-3.5" />
                      </div>
                    {:else}
                      <div class="bg-destructive/10 p-1 rounded text-destructive">
                          <TrendingDownIcon class="size-3.5" />
                      </div>
                    {/if}
                  </div>
                {/if}
                <div class="text-muted-foreground flex items-center gap-2 leading-none text-[11px] font-medium">
                  Showing total revenue in {currentUnit.trim().toLowerCase() === 'cr' ? 'crores' : 'lakhs'} for the last 6 months
                </div>
              </div>
            </div>
          </Card.Footer>
      {/if}
    </Card.Root>
  {/if}
</div>
