<script lang="ts">
  import { onMount } from "svelte";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { DataGrid, type DataGridColumn } from "$lib/components/venUI/datagrid";
  import { usePaginatedList } from "$lib/composables";
  import { Tabs, TabsContent, TabsList, TabsTrigger } from "$lib/components/ui/tabs";
  import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "$lib/components/ui/dialog";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Button } from "$lib/components/ui/button";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import { Toast } from "$lib/components/venUI/toast";
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

  // Class State
  let isEditingClass = $state(false);
  let editingClass = $state<any>({});
  let submittingClass = $state(false);

  function openNewClass() {
    editingClass = {};
    isEditingClass = true;
  }
  function editClass(item: any) {
    editingClass = { ...item };
    isEditingClass = true;
  }
  async function saveClass() {
    submittingClass = true;
    try {
      await new Promise(r => setTimeout(r, 800));
      Toast.success("Class saved successfully (Mocked)");
      isEditingClass = false;
    } catch (e) {
      Toast.error("Failed to save class");
    } finally {
      submittingClass = false;
    }
  }

  // Subclass State
  let isEditingSubclass = $state(false);
  let editingSubclass = $state<any>({});
  let submittingSubclass = $state(false);

  function openNewSubclass() {
    editingSubclass = {};
    isEditingSubclass = true;
  }
  function editSubclass(item: any) {
    editingSubclass = { ...item };
    isEditingSubclass = true;
  }
  async function saveSubclass() {
    submittingSubclass = true;
    try {
      await new Promise(r => setTimeout(r, 800));
      Toast.success("Subclass saved successfully (Mocked)");
      isEditingSubclass = false;
    } catch (e) {
      Toast.error("Failed to save subclass");
    } finally {
      submittingSubclass = false;
    }
  }
</script>

<svelte:head>
  <title>Fixed Assets Configuration</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/fixedasset" icon="settings">
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
          onRowClick={editClass}
        >
          {#snippet actions()}
            <Button size="sm" class="gap-2" onclick={openNewClass}>
              <Icon name="plus" class="size-4" />
              Add Class
            </Button>
          {/snippet}
        </DataGrid>
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
          onRowClick={editSubclass}
        >
          {#snippet actions()}
            <Button size="sm" class="gap-2" onclick={openNewSubclass}>
              <Icon name="plus" class="size-4" />
              Add Subclass
            </Button>
          {/snippet}
        </DataGrid>
      </TabsContent>
    </Tabs>
  </main>
</div>

<!-- Class Dialog -->
<Dialog open={isEditingClass} onOpenChange={(o) => isEditingClass = o}>
  <DialogContent class="sm:max-w-md">
    <DialogHeader>
      <DialogTitle>{editingClass.code ? "Edit Asset Class" : "New Asset Class"}</DialogTitle>
    </DialogHeader>

    <div class="grid gap-6 py-4">
      <div class="space-y-2">
        <Label for="classCode">Class Code</Label>
        <Input id="classCode" bind:value={editingClass.code} disabled={!!editingClass.code} />
      </div>
      <div class="space-y-2">
        <Label for="className">Name</Label>
        <Input id="className" bind:value={editingClass.name} />
      </div>
    </div>

    <DialogFooter>
      <Button variant="outline" onclick={() => isEditingClass = false}>Cancel</Button>
      <Button disabled={submittingClass} onclick={saveClass}>
        {#if submittingClass}
          <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
        {/if}
        {editingClass.code ? "Update Class" : "Save Class"}
      </Button>
    </DialogFooter>
  </DialogContent>
</Dialog>

<!-- Subclass Dialog -->
<Dialog open={isEditingSubclass} onOpenChange={(o) => isEditingSubclass = o}>
  <DialogContent class="sm:max-w-md">
    <DialogHeader>
      <DialogTitle>{editingSubclass.code ? "Edit Asset Subclass" : "New Asset Subclass"}</DialogTitle>
    </DialogHeader>

    <div class="grid gap-6 py-4">
      <div class="space-y-2">
        <Label for="subclassCode">Subclass Code</Label>
        <Input id="subclassCode" bind:value={editingSubclass.code} disabled={!!editingSubclass.code} />
      </div>
      <div class="space-y-2">
        <Label for="subclassName">Name</Label>
        <Input id="subclassName" bind:value={editingSubclass.name} />
      </div>
      <div class="space-y-2">
        <Label>Parent Class</Label>
        <MasterSelect type="faClasses" bind:value={editingSubclass.faClassCode} />
      </div>
    </div>

    <DialogFooter>
      <Button variant="outline" onclick={() => isEditingSubclass = false}>Cancel</Button>
      <Button disabled={submittingSubclass} onclick={saveSubclass}>
        {#if submittingSubclass}
          <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
        {/if}
        {editingSubclass.code ? "Update Subclass" : "Save Subclass"}
      </Button>
    </DialogFooter>
  </DialogContent>
</Dialog>
