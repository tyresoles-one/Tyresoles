<script lang="ts">
  import { onMount, untrack } from "svelte";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { DataGrid, type DataGridColumn, type FilterRule } from "$lib/components/venUI/datagrid";
  import { usePaginatedList } from "$lib/composables";
  import { Button } from "$lib/components/ui/button";
  import { cn } from "$lib/utils";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import { authStore } from "$lib/stores/auth";
  import {
    today,
    getLocalTimeZone,
    toCalendarDate,
    parseDateTime,
    parseDate,
    fromDate as fromJsDate
  } from "@internationalized/date";

  const GET_FIXED_ASSET_SERVICE_LOGS = `
    query GetFixedAssetServiceLogs($first: Int, $after: String, $fromDate: DateTime, $toDate: DateTime, $where: FixedAssetServiceLogFilterInput, $order: [FixedAssetServiceLogSortInput!]) {
      fixedAssetServiceLogs(first: $first, after: $after, fromDate: $fromDate, toDate: $toDate, where: $where, order: $order) {
        nodes {
          date
          description
          location
          employee
          subClass
          amount
          vendorNo
          vendorName
        }
        pageInfo { hasNextPage endCursor }
        totalCount
      }
    }
  `;

  // Filter Rules
  let filterRules = $state<FilterRule[]>([]);
  
  let dateRange = $state<{ start: unknown; end: unknown }>({
    start: undefined,
    end: undefined
  });

  const workDateRef = $derived.by(() => {
    const workDate = $authStore.user?.workDate;
    if (!workDate) return today(getLocalTimeZone());
    try {
      if (typeof workDate === "string") {
        if (workDate.includes("T")) {
          try {
            return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
          } catch {
            return toCalendarDate(fromJsDate(new Date(workDate), getLocalTimeZone()));
          }
        }
        try {
          return parseDate(workDate);
        } catch {
          return toCalendarDate(fromJsDate(new Date(workDate), getLocalTimeZone()));
        }
      }
      return today(getLocalTimeZone());
    } catch {
      return today(getLocalTimeZone());
    }
  });

  function calendarToYmd(d: unknown): string {
    if (d == null) return "";
    if (typeof d === "object" && d !== null && "toString" in d)
      return String((d as { toString: () => string }).toString()).slice(0, 10);
    return "";
  }

  const fromDateYmd = $derived(calendarToYmd(dateRange.start));
  const toDateYmd = $derived(calendarToYmd(dateRange.end));

  function toStartOfDayIso(dateStr: string): string {
    return new Date(`${dateStr}T00:00:00`).toISOString();
  }

  function toEndOfDayIso(dateStr: string): string {
    return new Date(`${dateStr}T23:59:59.999`).toISOString();
  }

  function logsSearchToWhere(term: string, rules: FilterRule[] = filterRules) {
    const q = term.trim();
    const conds: any[] = [];
    if (q) {
      conds.push({
        or: [
          { description: { contains: q } },
          { vendorNo: { contains: q } },
          { vendorName: { contains: q } }
        ]
      });
    }
    rules.forEach(r => {
      conds.push({ [r.columnId]: { [r.operator]: r.value } });
    });
    return conds.length === 0 ? {} : (conds.length === 1 ? conds[0] : { and: conds });
  }

  const list = usePaginatedList<any>({
    query: GET_FIXED_ASSET_SERVICE_LOGS,
    dataPath: "fixedAssetServiceLogs",
    pageSize: 50,
    mapSearchToVariables: (term) => ({ 
      where: logsSearchToWhere(term, filterRules),
      fromDate: fromDateYmd ? toStartOfDayIso(fromDateYmd) : undefined,
      toDate: toDateYmd ? toEndOfDayIso(toDateYmd) : undefined,
    }),
    serverVariableAllowlist: ["where", "order", "fromDate", "toDate"],
    paginationMode: "cursor",
    pageInfoPath: "fixedAssetServiceLogs.pageInfo",
    itemsPath: "fixedAssetServiceLogs.nodes"
  });

  let dateFilterReady = false;
  $effect(() => {
    const from = fromDateYmd ? toStartOfDayIso(fromDateYmd) : null;
    const to = toDateYmd ? toEndOfDayIso(toDateYmd) : null;
    untrack(() => {
      if (!dateFilterReady) {
        list.pagination.setVariables({ fromDate: from, toDate: to });
        dateFilterReady = true;
        return;
      }
      list.pagination.setVariables({ fromDate: from, toDate: to });
      list.onRefresh();
    });
  });

  const columns: DataGridColumn<any>[] = [
    { 
      accessorKey: "date", 
      header: "Date",
      cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString("en-IN", { day: '2-digit', month: 'short', year: 'numeric' })
    },
    { accessorKey: "description", header: "Description" },
    { accessorKey: "vendorName", header: "Technician / Vendor" },
    { 
      accessorKey: "amount", 
      header: "Amount",
      meta: { align: "right" },
      cell: ({ getValue }) => (getValue() as number).toLocaleString("en-IN", { style: 'currency', currency: 'INR', maximumFractionDigits: 0 })
    },
    { accessorKey: "location", header: "Location" },
    { accessorKey: "employee", header: "Reported By" }
  ];

  function onFilterRulesChange(rules: FilterRule[]) {
    filterRules = rules;
    list.pagination.setVariables({ where: logsSearchToWhere(list.searchQuery.value, rules) });
    list.onRefresh();
  }
</script>

<svelte:head>
  <title>Fixed Asset Service Logs</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/fixedasset" icon="wrench">
    {#snippet title()}Fixed Asset Service Logs{/snippet}
  </PageHeading>

  <main class="flex-1 pb-20 pt-4">
    <DataGrid
      title="Maintenance Records"
      description="Track repair and service expenses across all fixed assets"
      items={list.items}
      {columns}
      pagination={list.pagination}
      loading={list.loading}
      loadingMore={list.loadingMore}
      bind:searchQuery={list.searchQuery.value}
      mobileCardTitleKey="description"
      mobileCardSubtitleKey="date"
      showFilters={true}
      bind:filterRules
      {onFilterRulesChange}
    >
      {#snippet actions()}
        <div class="flex items-center gap-2">
          <div class="w-[260px]">
            <DatePicker
              bind:value={dateRange}
              mode="range"
              valueType="calendar"
              placeholder="Select date range"
              presetKeys="thisMonth,lastMonth,thisQuarter,lastQuarter,thisFinYear,lastFinYear"
              fiscal
              workdate={workDateRef}
            />
          </div>
          {#if dateRange.start || dateRange.end}
            <Button
              type="button"
              variant="ghost"
              size="sm"
              class="h-9 px-2 text-muted-foreground hover:text-foreground"
              onclick={() => {
                dateRange = { start: undefined, end: undefined };
              }}
            >
              Clear
            </Button>
          {/if}
        </div>
      {/snippet}
    </DataGrid>
  </main>
</div>
