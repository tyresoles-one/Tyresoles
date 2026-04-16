<script lang="ts">
  import { onMount } from "svelte";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { DataGrid, type DataGridColumn } from "$lib/components/venUI/datagrid";
  import { usePaginatedList } from "$lib/composables";
  import { Tabs, TabsContent, TabsList, TabsTrigger } from "$lib/components/ui/tabs";
  import { cn } from "$lib/utils";

  const GET_FA_CLASSES = `
    query GetFAClasses($first: Int, $after: String, $where: FAClassFilterInput, $order: [FAClassSortInput!]) {
      faClasses(first: $first, after: $after, where: $where, order: $order) {
        nodes { code name }
        pageInfo { hasNextPage endCursor }
        totalCount
      }
    }
  `;

  const GET_FA_SUBCLASSES = `
    query GetFASubclasses($first: Int, $after: String, $where: FASubclassFilterInput, $order: [FASubclassSortInput!]) {
      faSubclasses(first: $first, after: $after, where: $where, order: $order) {
        nodes { code name faClassCode }
        pageInfo { hasNextPage endCursor }
        totalCount
      }
    }
  `;

  let activeTab = $state("classes");

  const classesList = usePaginatedList<any>({
    query: GET_FA_CLASSES,
    dataPath: "faClasses",
    pageSize: 50,
    serverVariableAllowlist: ["where", "order"],
    paginationMode: "cursor",
    pageInfoPath: "faClasses.pageInfo"
  });

  const subclassesList = usePaginatedList<any>({
    query: GET_FA_SUBCLASSES,
    dataPath: "faSubclasses",
    pageSize: 50,
    serverVariableAllowlist: ["where", "order"],
    paginationMode: "cursor",
    pageInfoPath: "faSubclasses.pageInfo"
  });

  const classCols: DataGridColumn<any>[] = [
    { accessorKey: "code", header: "Class Code" },
    { accessorKey: "name", header: "Class Name" }
  ];

  const subclassCols: DataGridColumn<any>[] = [
    { accessorKey: "code", header: "Subclass Code" },
    { accessorKey: "name", header: "Subclass Name" },
    { accessorKey: "faClassCode", header: "Parent Class" }
  ];
</script>

<svelte:head>
  <title>Fixed Assets Configuration</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/fixed-assets" icon="settings">
    {#snippet title()}Fixed Asset Configuration{/snippet}
  </PageHeading>

  <main class="flex-1 space-y-6 pb-20 pt-4">
    <Tabs bind:value={activeTab} class="w-full">
      <TabsList class="mx-4 md:mx-6">
        <TabsTrigger value="classes">Asset Classes</TabsTrigger>
        <TabsTrigger value="subclasses">Sub Classes</TabsTrigger>
      </TabsList>
      
      <TabsContent value="classes" class="mt-4">
        <DataGrid
          title="Asset Classes"
          description="View classification codes for fixed assets"
          items={classesList.items}
          columns={classCols}
          pagination={classesList.pagination}
          loading={classesList.loading}
          loadingMore={classesList.loadingMore}
          bind:searchQuery={classesList.searchQuery.value}
          mobileCardTitleKey="name"
          mobileCardSubtitleKey="code"
        />
      </TabsContent>
      
      <TabsContent value="subclasses" class="mt-4">
        <DataGrid
          title="Asset Subclasses"
          description="View secondary classification for fixed assets"
          items={subclassesList.items}
          columns={subclassCols}
          pagination={subclassesList.pagination}
          loading={subclassesList.loading}
          loadingMore={subclassesList.loadingMore}
          bind:searchQuery={subclassesList.searchQuery.value}
          mobileCardTitleKey="name"
          mobileCardSubtitleKey="code"
        />
      </TabsContent>
    </Tabs>
  </main>
</div>
