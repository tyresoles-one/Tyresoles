<script lang="ts">
  import { onMount } from "svelte";
  import { slide } from "svelte/transition";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Toast } from "$lib/components/venUI/toast";
  import { DataGrid, type DataGridColumn, type FilterRule } from "$lib/components/venUI/datagrid";
  import { usePaginatedList } from "$lib/composables";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Card, CardContent, CardHeader, CardTitle } from "$lib/components/ui/card";
  import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "$lib/components/ui/dialog";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import { cn } from "$lib/utils";

  // GraphQL
  const GET_FIXED_ASSETS = `
    query GetFixedAssets($first: Int, $after: String, $where: FixedAssetFilterInput, $order: [FixedAssetSortInput!]) {
      fixedAssets(first: $first, after: $after, where: $where, order: $order) {
        nodes {
          no
          description
          description2
          serialNo
          vendorNo
          faClassCode
          faSubclassCode
          responsibilityCenter
          responsibleEmployee
          blocked
        }
        pageInfo { hasNextPage endCursor }
        totalCount
      }
    }
  `;

  const SAVE_FIXED_ASSET = `
    mutation SaveFixedAsset($input: FixedAssetInput!) {
      saveFixedAsset(input: $input) {
        success
        message
      }
    }
  `;

  // State
  let filterRules = $state<FilterRule[]>([]);
  let isEditing = $state(false);
  let editingAsset = $state<any>({});
  let submitting = $state(false);

  // Search/Filter helper
  function assetsSearchToWhere(term: string, rules: FilterRule[] = filterRules) {
    const q = term.trim();
    const conds: any[] = [];
    if (q) {
      conds.push({
        or: [
          { no: { contains: q } },
          { description: { contains: q } },
          { serialNo: { contains: q } }
        ]
      });
    }
    rules.forEach(r => {
      conds.push({ [r.columnId]: { [r.operator]: r.value } });
    });
    return conds.length === 0 ? {} : (conds.length === 1 ? conds[0] : { and: conds });
  }

  const list = usePaginatedList<any>({
    query: GET_FIXED_ASSETS,
    dataPath: "fixedAssets",
    pageSize: 50,
    mapSearchToVariables: (term) => ({ where: assetsSearchToWhere(term, filterRules) }),
    serverVariableAllowlist: ["where", "order"],
    paginationMode: "cursor",
    pageInfoPath: "fixedAssets.pageInfo"
  });

  const columns: DataGridColumn<any>[] = [
    { accessorKey: "no", header: "Asset No" },
    { accessorKey: "description", header: "Description" },
    { accessorKey: "faSubclassCode", header: "Type" },
    { accessorKey: "responsibilityCenter", header: "Location" },
    { accessorKey: "responsibleEmployee", header: "Assigned To" },
    { accessorKey: "serialNo", header: "Serial No" }
  ];

  function openNew() {
    editingAsset = { blocked: 0 };
    isEditing = true;
  }

  function editAsset(asset: any) {
    editingAsset = { ...asset };
    isEditing = true;
  }

  async function handleSave() {
    submitting = true;
    try {
      const res = await graphqlMutation<any>(SAVE_FIXED_ASSET, {
        variables: { input: editingAsset }
      });
      if (res.success && res.data?.saveFixedAsset.success) {
        Toast.success("Fixed asset saved successfully");
        isEditing = false;
        list.onRefresh();
      } else {
        Toast.error(res.data?.saveFixedAsset.message || res.error || "Failed to save asset");
      }
    } finally {
      submitting = false;
    }
  }

  function onFilterRulesChange(rules: FilterRule[]) {
    filterRules = rules;
    list.pagination.setVariables({ where: assetsSearchToWhere(list.searchQuery.value, rules) });
    list.onRefresh();
  }
</script>

<svelte:head>
  <title>Fixed Assets Directory</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/fixed-assets" icon="package">
    {#snippet title()}Fixed Assets Directory{/snippet}
  </PageHeading>

  <main class="flex-1 pb-20 pt-4">
    <DataGrid
      title="All Assets"
      description="View and manage corporate assets"
      items={list.items}
      {columns}
      pagination={list.pagination}
      loading={list.loading}
      loadingMore={list.loadingMore}
      bind:searchQuery={list.searchQuery.value}
      mobileCardTitleKey="description"
      mobileCardSubtitleKey="no"
      onRowClick={editAsset}
      showFilters={true}
      {filterRules}
      {onFilterRulesChange}
    >
      {#snippet actions()}
        <Button size="sm" class="gap-2" onclick={openNew}>
          <Icon name="plus" class="size-4" />
          Add Asset
        </Button>
      {/snippet}
    </DataGrid>
  </main>
</div>

<Dialog open={isEditing} onOpenChange={(o) => isEditing = o}>
  <DialogContent class="sm:max-w-2xl">
    <DialogHeader>
      <DialogTitle>{editingAsset.no ? "Edit Asset" : "New Fixed Asset"}</DialogTitle>
    </DialogHeader>

    <div class="grid gap-6 py-4 md:grid-cols-2">
      <div class="space-y-2">
        <Label for="no">Asset No</Label>
        <Input id="no" bind:value={editingAsset.no} disabled={!!editingAsset.no} placeholder="Auto-generated if empty" />
      </div>

      <div class="space-y-2">
        <Label>Asset Class</Label>
        <MasterSelect type="faClasses" bind:value={editingAsset.faClassCode} />
      </div>

      <div class="space-y-2">
        <Label>Sub Class</Label>
        <MasterSelect type="faSubclasses" bind:value={editingAsset.faSubclassCode} />
      </div>

      <div class="space-y-2">
        <Label for="serial">Serial No</Label>
        <Input id="serial" bind:value={editingAsset.serialNo} />
      </div>

      <div class="col-span-full space-y-2">
        <Label for="desc">Description</Label>
        <Input id="desc" bind:value={editingAsset.description} />
      </div>

      <div class="space-y-2">
        <Label>Location</Label>
        <MasterSelect type="respCenters" bind:value={editingAsset.responsibilityCenter} />
      </div>

      <div class="space-y-2">
        <Label>Assigned To</Label>
        <MasterSelect type="payrollEmployees" bind:value={editingAsset.responsibleEmployee} />
      </div>

      <div class="space-y-2">
        <Label>Vendor</Label>
        <MasterSelect type="vendors" bind:value={editingAsset.vendorNo} />
      </div>
    </div>

    <DialogFooter>
      <Button variant="outline" onclick={() => isEditing = false}>Cancel</Button>
      <Button disabled={submitting} onclick={handleSave}>
        {#if submitting}
          <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
        {/if}
        {editingAsset.no ? "Update Asset" : "Register Asset"}
      </Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
