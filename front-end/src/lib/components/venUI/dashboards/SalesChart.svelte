<script lang="ts">
  import { onMount } from "svelte";
  import { fade } from "svelte/transition";
  import { authStore, getUser } from "$lib/stores/auth";
  import { toast } from "$lib/components/venUI/toast";
  import { scaleBand } from "d3-scale";
  import { BarChart, type ChartContextValue } from "layerchart";
  import TrendingUpIcon from "@lucide/svelte/icons/trending-up";
  import TrendingDownIcon from "@lucide/svelte/icons/trending-down";
  import * as Chart from "$lib/components/ui/chart/index.js";
  import * as Card from "$lib/components/ui/card/index.js";
  import { cubicInOut } from "svelte/easing";
  import { fetchSalesChart } from "./sales-chart/api";
  import type { MonthlySalesRow } from "./sales-chart/types";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { Icon } from "$lib/components/venUI/icon";
  import { cn } from "$lib/utils";

  let { class: className = "" }: { class?: string } = $props();

  const user = getUser();
  let loading = $state(false);
  let error = $state<string | null>(null);
  let rows = $state<MonthlySalesRow[]>([]);
  let fetchController: AbortController | null = null;
  let context = $state<ChartContextValue>();

  const respCenters = $derived.by(() => {
    const locs = $authStore.locations;
    if (locs && locs.length > 0) {
      return locs.filter(l => (l as any).sale === 1).map(l => l.code).filter(Boolean);
    }
    return user?.respCenter ? [user.respCenter] : [];
  });

  const chartConfig = {
    sale: { label: "Sales", color: "oklch(0.86 0.17 92)" },
  } satisfies Chart.ChartConfig;

  async function load() {
    if (fetchController) fetchController.abort();
    fetchController = new AbortController();
    loading = true;
    error = null;
    
    const res = await fetchSalesChart(
      {
        respCenters: respCenters.length > 0 ? respCenters : undefined,
        entityType: user?.entityType ?? undefined,
        entityCode: user?.entityCode ?? undefined,
        entityDepartment: user?.department ?? undefined,
        workDate: user?.workDate
      } as any,
      fetchController.signal,
    );
    
    loading = false;
    if (res.success && res.data) {
      rows = res.data;
    } else if (res.error) {
       error = res.error;
       toast.error(res.error);
    }
  }

  onMount(() => {
    load();
  });

  const trend = $derived.by(() => {
    if (rows.length < 2) return null;
    const last = rows[rows.length - 1].sale;
    const prev = rows[rows.length - 2].sale;
    if (prev <= 0) return null;
    const diff = ((last - prev) / prev) * 100;
    return {
      percentage: Math.abs(diff).toFixed(1),
      isUp: diff >= 0
    };
  });

  const dateDescription = $derived.by(() => {
    if (rows.length === 0) return "Loading...";
    return `${rows[0].month} - ${rows[rows.length - 1].month}`;
  });

  function formatValue(val: any, unit?: string) {
    if (val === undefined || val === null || isNaN(val)) return "";
    if (typeof val !== "number") return String(val);
    if (val === 0) return "0";
    const u = (unit || "").trim().toLowerCase();
    if (u === "cr") return val.toFixed(2) + "Cr";
    return val.toFixed(1) + "L";
  }

  const currentUnit = $derived(rows.length > 0 ? (rows[0].unit || "L") : "L");
</script>

<div class="w-full h-full min-h-[450px] {className}">
  {#if error}
    <Card.Root class="h-full flex flex-col">
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
  {:else if loading && rows.length === 0}
    <Card.Root class="h-full flex flex-col">
      <Card.Header class="px-4 py-3">
        <div class="space-y-2">
          <Skeleton class="h-5 w-40" />
          <Skeleton class="h-4 w-32" />
        </div>
      </Card.Header>
      <Card.Content class="flex-1 p-4 pt-0">
        <Skeleton class="h-full min-h-[180px] w-full rounded-lg" />
      </Card.Content>
    </Card.Root>
  {:else if rows.length === 0 && !loading}
    <Card.Root class="h-full flex flex-col">
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
    <Card.Root class="overflow-hidden gap-3 border-none shadow-sm sm:border sm:shadow-md h-full min-h-[450px] flex flex-col py-2">
      <Card.Header class="flex flex-row items-center justify-between px-4 py-3 shrink-0">
        <div>
          <Card.Title class="text-sm font-bold">Sales Performance History</Card.Title>
          <Card.Description class="text-xs">{dateDescription}</Card.Description>
        </div>
        <button 
          onclick={load} 
          class="inline-flex size-8 items-center justify-center rounded-lg bg-muted/30 hover:bg-primary/10 transition-all duration-200 active:scale-95 group/btn" 
          disabled={loading}
        >
          <Icon name="refresh-cw" class={cn("size-4 text-muted-foreground group-hover/btn:text-primary", loading && "animate-spin")} />
        </button>
      </Card.Header>
      <Card.Content class="p-4 pt-0 flex-1 min-h-[300px] relative">
        <div class="h-[300px] w-full">
            <BarChart
                data={rows}
                x="month"
                y="sale"
                series={[
                    { key: "sale", label: "Monthly Sale", color: chartConfig.sale.color },
                ]}
                padding={{ top: 40, bottom: 25, left: 50, right: 10 }}
                xScale={scaleBand().padding(0.4)}
                labels={true}
                props={{
                    bars: { 
                        radius: 4, 
                        rounded: "top",
                        fill: chartConfig.sale.color,
                        stroke: chartConfig.sale.color,
                        strokeWidth: 2,
                        class: "transition-all duration-500"
                    },
                    labels: {
                        format: (v) => formatValue(v, currentUnit),
                        class: "text-[10px] font-bold fill-foreground",
                        offset: 4
                    },
                    xAxis: {
                        format: (d) => d,
                        class: "text-[10px] fill-muted-foreground"
                    },
                    yAxis: {
                        ticks: 5,
                        format: (v) => formatValue(v, currentUnit),
                        class: "text-[10px] fill-muted-foreground"
                    },
                    grid: {
                        y: { class: "stroke-muted/20" }
                    }
                }}
            >
                {#snippet tooltip(props)}
                    {#if props?.data}
                        <div class="p-2 bg-background border rounded-lg shadow-xl text-xs">
                            <div class="font-bold border-bottom pb-1 mb-1">{props.data.month}</div>
                            <div class="flex items-center gap-2">
                                <div class="size-2 rounded-full" style="background: {chartConfig.sale.color}"></div>
                                <span>Sales: <b>{formatValue(props.data.sale, currentUnit)}</b></span>
                            </div>
                        </div>
                    {/if}
                {/snippet}
            </BarChart>
        </div>
      </Card.Content>
      <Card.Footer class="p-4 pt-0">
        <div class="flex w-full items-start gap-2 text-sm">
          <div class="grid gap-1">
            {#if trend}
              <div class="flex items-center gap-2 leading-none font-bold text-foreground">
                {trend.isUp ? 'Trending up' : 'Trending down'} by {trend.percentage}% this month 
                {#if trend.isUp}
                  <TrendingUpIcon class="size-4 text-emerald-500" />
                {:else}
                  <TrendingDownIcon class="size-4 text-destructive" />
                {/if}
              </div>
            {/if}
            <div class="text-muted-foreground flex items-center gap-2 leading-none text-xs">
              Showing total revenue in {currentUnit.trim().toLowerCase() === 'cr' ? 'crores' : 'lakhs'} for the last 6 months
            </div>
          </div>
        </div>
      </Card.Footer>
    </Card.Root>
  {/if}
</div>
