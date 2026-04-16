<script lang="ts">
  import { onMount } from "svelte";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { DataGrid, type DataGridColumn, type FilterRule } from "$lib/components/venUI/datagrid";
  import { usePaginatedList } from "$lib/composables";
  import { Button } from "$lib/components/ui/button";
  import { cn } from "$lib/utils";

  const GET_FIXED_ASSET_SERVICE_LOGS = `
    query GetFixedAssetServiceLogs($first: Int, $after: String, $where: FixedAssetServiceLogFilterInput, $order: [FixedAssetServiceLogSortInput!]) {
      fixedAssetServiceLogs(first: $first, after: $after, where: $where, order: $order) {
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
    mapSearchToVariables: (term) => ({ where: logsSearchToWhere(term, filterRules) }),
    serverVariableAllowlist: ["where", "order"],
    paginationMode: "cursor",
    pageInfoPath: "fixedAssetServiceLogs.pageInfo"
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
  <PageHeading backHref="/fixed-assets" icon="wrench">
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
      {filterRules}
      {onFilterRulesChange}
    />
  </main>
</div>
