<script lang="ts">
  import { onMount } from "svelte";
  import { fade, slide } from "svelte/transition";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import { authStore, getUser } from "$lib/stores/auth";
  import { toast } from "$lib/components/venUI/toast";
  
  // UI Components
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import * as Select from "$lib/components/ui/select";
  import * as Dialog from "$lib/components/ui/dialog";
  import PdfViewer from "$lib/components/venUI/pdf-viewer/PdfViewer.svelte";
  
  // Types
  interface DocumentDto {
    no: string;
    date: string;
    customerNo: string;
    name: string;
    amount: number;
  }

  // GraphQL Query
  const GetMyDocumentsQuery = `
    query GetMyDocuments($input: SalesReportParamsInput!) {
      getMyDocuments(parameters: $input) {
        no
        date
        customerNo
        name
        amount
      }
    }
  `;

  const PrintDocumentsMutation = `
    mutation PrintDocuments($input: SalesReportParamsInput!) {
      printDocuments(parameters: $input)
    }
  `;

  // --- State ---
  const PAGE_SIZE = 50;
  let allRows = $state<DocumentDto[]>([]);
  let hasMore = $state(true);
  let skip = $state(0);
  let loading = $state(false);       // initial / sort-reset load
  let loadingMore = $state(false);   // infinite-scroll load
  let error = $state<string | null>(null);
  let initialLoaded = $state(false);
  let selectedNos = $state<Set<string>>(new Set());
  let showFilters = $state(false);
  
  // Printing state
  let printing = $state(false);
  let showPdfViewer = $state(false);
  let pdfData = $state<Uint8Array | null>(null);
  let pdfFileName = $state("");

  // --- Filters ---
  const user = getUser();
  const workDate = $authStore.user?.workDate;

  /** Show RC MasterSelect only when user can choose among multiple locations; otherwise default to profile respCenter. */
  let showRespCenterMaster = $derived(
    ($authStore.locations?.length ?? 0) > 1
  );
  
  let dateRange = $state<{ start: any; end: any }>({ start: undefined, end: undefined });
  let view = $state("Invoice"); // "Invoice", "CrNote", "Claim"
  let respCenters = $state("");
  let customers = $state("");
  
  let filterForm = $state({
    values: {
      respCenters: "",
      customers: ""
    },
    setTouched: (name: string) => {}
  });

  $effect(() => {
    respCenters = String(filterForm.values.respCenters || "");
    customers = String(filterForm.values.customers || "");
  });

  /** Single-location users: lock filter to login respCenter (API may send ISO; we store code string). */
  $effect(() => {
    const locCount = $authStore.locations?.length ?? 0;
    const rc = $authStore.user?.respCenter ?? "";
    if (locCount <= 1 && rc) {
      filterForm.values.respCenters = rc;
    }
  });

  // --- Sorting (Client-side over loaded rows) ---
  type SortField = "date" | "amount" | "no";
  let sortField = $state<SortField>("date");
  let sortOrder = $state<"ASC" | "DESC">("DESC");

  // --- Helpers ---
  function splitCSV(val: string): string[] {
    return val ? val.split(/[,;\s]+/).map((s) => s.trim()).filter(Boolean) : [];
  }

  function toIso(date: any): string | null {
    if (!date) return null;
    if (typeof date === "string") return date;
    if (date.toDate) return date.toDate().toISOString();
    return new Date(date).toISOString();
  }

  function fmtDate(iso: string) {
    if (!iso) return "—";
    return new Date(iso).toLocaleDateString("en-IN", {
      day: "2-digit",
      month: "short",
      year: "numeric",
    });
  }

  function fmtAmt(n: number) {
    return new Intl.NumberFormat("en-IN", {
      style: "currency",
      currency: "INR",
      minimumFractionDigits: 2,
    }).format(n);
  }

  // --- Actions ---
  async function fetchPage(append: boolean) {
    if (append) {
      if (!hasMore || loadingMore) return;
      loadingMore = true;
    } else {
      loading = true;
      error = null;
      skip = 0;
      allRows = [];
      selectedNos = new Set();
    }
    
    try {
      const input = {
        view,
        from: toIso(dateRange?.start),
        to: toIso(dateRange?.end),
        respCenters: splitCSV(respCenters),
        customers: customers.trim() || undefined,
        entityType: user?.entityType,
        entityCode: user?.entityCode,
        entityDepartment: user?.department,
        workDate: workDate,
        skip,
        take: PAGE_SIZE
      };

      const res = await graphqlQuery<{ getMyDocuments: DocumentDto[] }>(GetMyDocumentsQuery, {
        variables: { input },
        skipCache: true
      });

      if (res.success && res.data) {
        const newRows = res.data.getMyDocuments || [];
        if (append) {
          allRows = [...allRows, ...newRows];
        } else {
          allRows = newRows;
          initialLoaded = true;
        }
        hasMore = newRows.length === PAGE_SIZE;
        skip += newRows.length;
      } else {
        error = res.error || "Failed to fetch documents";
      }
    } catch (e: any) {
      error = e.message || "An unexpected error occurred";
    } finally {
      loading = false;
      loadingMore = false;
    }
  }

  async function reload() {
    await fetchPage(false);
  }

  async function loadMore() {
    await fetchPage(true);
  }

  function toggleAll() {
    if (selectedNos.size === sortedDocs().length) {
      selectedNos = new Set();
    } else {
      selectedNos = new Set(sortedDocs().map(d => d.no));
    }
  }

  function toggleSelect(no: string) {
    if (selectedNos.has(no)) {
      selectedNos.delete(no);
    } else {
      selectedNos.add(no);
    }
    selectedNos = new Set(selectedNos);
  }

  function handleSort(field: SortField) {
    if (sortField === field) {
      sortOrder = sortOrder === "ASC" ? "DESC" : "ASC";
    } else {
      sortField = field;
      sortOrder = "DESC";
    }
  }

  // --- Derived ---
  let sortedDocs = $derived(() => {
    return [...allRows].sort((a, b) => {
      let valA: any = a[sortField];
      let valB: any = b[sortField];
      
      if (sortField === "date") {
        valA = new Date(a.date).getTime();
        valB = new Date(b.date).getTime();
      }
      
      if (valA < valB) return sortOrder === "ASC" ? -1 : 1;
      if (valA > valB) return sortOrder === "ASC" ? 1 : -1;
      return 0;
    });
  });

  // --- Infinite scroll sentinel ---
  let sentinel = $state<HTMLElement | null>(null);
  let observer: IntersectionObserver | null = null;

  $effect(() => {
    if (sentinel && typeof IntersectionObserver !== "undefined") {
      observer?.disconnect();
      observer = new IntersectionObserver((entries) => {
        if (entries[0].isIntersecting && hasMore && !loading && !loadingMore) {
          loadMore();
        }
      }, { threshold: 0.1 });
      observer.observe(sentinel);
    }
    return () => observer?.disconnect();
  });

  async function handlePrint() {
    if (selectedNos.size === 0 || printing) return;
    
    printing = true;
    try {
      const input = {
        view,
        nos: Array.from(selectedNos),
        respCenters: splitCSV(respCenters),
        customers: customers.trim() || undefined,
        entityType: user?.entityType,
        entityCode: user?.entityCode,
        entityDepartment: user?.department,
        workDate: workDate,
        reportOutput: "PDF"
      };

      const res = await graphqlQuery<{ printDocuments: string }>(PrintDocumentsMutation, {
        variables: { input },
        skipCache: true
      });

      if (res.success && res.data?.printDocuments) {
        const base64 = res.data.printDocuments;
        const binaryString = window.atob(base64);
        const bytes = new Uint8Array(binaryString.length);
        for (let i = 0; i < binaryString.length; i++) {
          bytes[i] = binaryString.charCodeAt(i);
        }
        pdfData = bytes;
        pdfFileName = `${view}_Batch_${new Date().getTime()}.pdf`;
        showPdfViewer = true;
      } else {
        toast.error(res.error || "Failed to generate report");
      }
    } catch (e: any) {
      toast.error(e.message || "Error printing documents");
    } finally {
      printing = false;
    }
  }

  onMount(() => {
    reload();
  });
</script>

<svelte:head>
  <title>Sales Documents</title>
</svelte:head>

<div class="page-container">
  <PageHeading backHref="/" icon="file-stack">
    {#snippet title()}
      <div class="flex items-center gap-3">
        <span>Sales Documents</span>
        {#if allRows.length > 0}
          <span class="count-badge">{allRows.length}{#if hasMore}+ {/if}</span>
        {/if}
      </div>
    {/snippet}

    {#snippet actions()}
      <div class="flex items-center gap-2">
        <Button 
          variant="outline" 
          size="sm" 
          class="rounded-full gap-2"
          onclick={() => showFilters = !showFilters}
        >
          <Icon name="sliders-horizontal" class="size-4" />
          <span class="hidden sm:inline">Filters</span>
          {#if (showRespCenterMaster && respCenters) || customers || dateRange?.start}
            <div class="size-2 rounded-full bg-primary animate-pulse"></div>
          {/if}
        </Button>
        
        <Button 
          variant="default" 
          size="sm" 
          class="rounded-full gap-2 shadow-lg shadow-primary/20"
          onclick={reload}
          disabled={loading}
        >
          <Icon name="refresh-cw" class="size-4 {loading ? 'animate-spin' : ''}" />
          <span class="hidden sm:inline">Refresh</span>
        </Button>
      </div>
    {/snippet}
  </PageHeading>

  <main class="content-area">
    {#if showFilters}
      <div class="filter-panel" transition:slide>
        <div class="filter-grid">
          <div class="filter-item">
            <label for="view-select">Document View</label>
            <Select.Root type="single" bind:value={view}>
              <Select.Trigger class="w-full bg-background border-input">
                {view === 'Invoice' ? 'Invoices' : view === 'CrNote' ? 'Credit Notes' : 'Claims'}
              </Select.Trigger>
              <Select.Content>
                <Select.Item value="Invoice">Invoices</Select.Item>
                <Select.Item value="CrNote">Credit Notes</Select.Item>
                <Select.Item value="Claim">Claims</Select.Item>
              </Select.Content>
            </Select.Root>
          </div>

          <div class="filter-item">
            <label for="date-range">Date Range</label>
            <DatePicker 
              mode="range" 
              bind:value={dateRange} 
              placeholder="Select date range"
              presetKeys="thisMonth,lastMonth,today"
              workdate={workDate}
            />
          </div>

          {#if showRespCenterMaster}
            <div class="filter-item">
              <MasterSelect 
                bind:form={filterForm} 
                fieldName="respCenters" 
                masterType="respCenters" 
                label="Resp. Center" 
                placeholder="All Centers"
              />
            </div>
          {/if}

          <div class="filter-item">
            <MasterSelect 
              bind:form={filterForm} 
              fieldName="customers" 
              masterType="customers" 
              label="Customers" 
              placeholder="Search customers..."
            />
          </div>
        </div>

        <div class="filter-actions">
          <Button variant="ghost" size="sm" onclick={() => {
            customers = "";
            dateRange = { start: undefined, end: undefined };
            filterForm.values.customers = "";
            if (showRespCenterMaster) {
              respCenters = "";
              filterForm.values.respCenters = "";
            } else {
              const rc = $authStore.user?.respCenter ?? "";
              filterForm.values.respCenters = rc;
              respCenters = rc;
            }
          }}>Reset Filters</Button>
          <Button variant="default" size="sm" onclick={() => { reload(); showFilters = false; }}>Apply Filters</Button>
        </div>
      </div>
    {/if}

    {#if selectedNos.size > 0}
      <div class="selection-bar" transition:fade>
        <div class="selection-info">
          <span class="selection-count">{selectedNos.size} selected</span>
          <Button variant="ghost" size="sm" class="text-xs" onclick={() => selectedNos = new Set()}>Clear</Button>
        </div>
        <div class="selection-actions">
          <Button 
            variant="default" 
            size="sm" 
            class="gap-2 h-8 px-4 font-bold bg-white text-primary hover:bg-white/90" 
            onclick={handlePrint}
            disabled={printing}
          >
            {#if printing}
              <Icon name="loader-2" class="size-3 animate-spin" />
              <span>Generating...</span>
            {:else}
              <Icon name="printer" class="size-3" />
              <span>Print PDF</span>
            {/if}
          </Button>
        </div>
      </div>
    {/if}

    <div class="list-container">
      {#if loading && !loadingMore}
        <div class="loading-state">
          {#each Array(6) as _}
            <div class="skeleton-row">
              <Skeleton class="h-12 w-full rounded-lg" />
            </div>
          {/each}
        </div>
      {:else if error}
        <div class="error-state">
          <Icon name="alert-circle" class="size-12 text-destructive mb-4" />
          <h3>Failed to load documents</h3>
          <p>{error}</p>
          <Button variant="outline" class="mt-4" onclick={reload}>Try Again</Button>
        </div>
      {:else if initialLoaded && sortedDocs().length === 0}
        <div class="empty-state">
          <div class="empty-icon-wrap">
            <Icon name="file-search" class="size-12 text-muted-foreground/50" />
          </div>
          <h3>No documents found</h3>
          <p>Try adjusting your filters or date range.</p>
          {#if !showFilters}
            <Button variant="outline" class="mt-4 rounded-full" onclick={() => showFilters = true}>Open Filters</Button>
          {/if}
        </div>
      {:else}
        <div class="list-wrapper" class:view-claim={view === 'Claim'}>
          <!-- Desktop Header -->
          <div class="list-header md:grid">
            <div class="cell-selection">
              <Checkbox 
                checked={selectedNos.size === sortedDocs().length && sortedDocs().length > 0} 
                onCheckedChange={toggleAll}
              />
            </div>
            <div onclick={() => handleSort('no')} class="cell-head cell-head-doc sortable">
              Document No
              {#if sortField === 'no'}
                <Icon name={sortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary" />
              {/if}
            </div>
            <div onclick={() => handleSort('date')} class="cell-head cell-head-date sortable">
              Date
              {#if sortField === 'date'}
                <Icon name={sortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary" />
              {/if}
            </div>
            <div class="cell-head cell-head-cust">Customer</div>
            <div class="cell-head cell-head-name">Name</div>
            {#if view !== 'Claim'}
              <div onclick={() => handleSort('amount')} class="cell-head cell-head-amount sortable">
                Amount
                {#if sortField === 'amount'}
                  <Icon name={sortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary" />
                {/if}
              </div>
            {/if}
          </div>

          <!-- List Rows -->
          <div class="list-body">
            {#each sortedDocs() as doc}
              <div 
                class="doc-row" 
                class:selected={selectedNos.has(doc.no)} 
                onclick={() => toggleSelect(doc.no)}
              >
                <!-- Selection Column -->
                <div class="cell-selection">
                  <Checkbox 
                    checked={selectedNos.has(doc.no)} 
                    onCheckedChange={() => toggleSelect(doc.no)}
                    onclick={(e) => e.stopPropagation()}
                  />
                </div>

                <!-- Mobile: Stacked Info | Desktop: Columns -->
                <div class="doc-info">
                  <div class="doc-main">
                    <span class="doc-id font-mono font-medium text-primary">{doc.no}</span>
                    <!-- Date, customer no, and name under doc no: only on mobile; hidden from md (768px) so Document No column shows only the number on big screens -->
                    <span class="doc-meta text-muted-foreground max-md:block md:hidden">{fmtDate(doc.date)} · {doc.customerNo}</span>
                    <span class="doc-name-stacked max-md:block md:hidden truncate-text" title={doc.name}>{doc.name}</span>
                  </div>
                  
                  <!-- Desktop Only Columns -->
                  <div class="doc-date hidden md:block text-muted-foreground">
                    {fmtDate(doc.date)}
                  </div>
                  <div class="doc-cust hidden md:block">
                    <span class="cust-code">{doc.customerNo}</span>
                  </div>
                  <div class="doc-name hidden md:block">
                    <span class="truncate-text" title={doc.name}>{doc.name}</span>
                  </div>
                </div>

                <!-- Amount Column -->
                {#if view !== 'Claim'}
                  <div class="cell-amount">
                    {fmtAmt(doc.amount)}
                  </div>
                {/if}
              </div>
            {/each}
          </div>
          
          <div bind:this={sentinel} class="h-10 w-full flex items-center justify-center">
            {#if loadingMore}
              <Icon name="loader-2" class="size-6 animate-spin text-muted-foreground" />
            {:else if !hasMore && allRows.length > 0}
              <span class="text-xs text-muted-foreground uppercase tracking-widest font-semibold py-8 text-center w-full">End of list</span>
            {/if}
          </div>
        </div>
      {/if}
    </div>
  </main>
</div>

<!-- PDF Viewer Modal -->
<Dialog.Root bind:open={showPdfViewer}>
  <Dialog.Content class="w-[96vw] max-w-[min(96vw,2400px)]! h-[95vh]! p-0 overflow-hidden flex flex-col rounded-lg">
    <Dialog.Header class="px-6 py-4 border-bottom flex-shrink-0">
      <Dialog.Title class="flex items-center gap-2">
        <Icon name="file-text" class="text-primary" />
        <span>Document Preview</span>
      </Dialog.Title>
    </Dialog.Header>
    
    <div class="flex-1 min-h-0 bg-muted/20">
      {#if pdfData}
        <PdfViewer 
          data={pdfData} 
          fileName={pdfFileName} 
          class="h-full w-full" 
        />
      {/if}
    </div>
    
    <Dialog.Footer class="px-6 py-3 border-top flex-shrink-0 bg-muted/5">
      <Button variant="outline" onclick={() => showPdfViewer = false}>Close Preview</Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<style>
  .page-container {
    min-height: 100vh;
    background: var(--background);
    display: flex;
    flex-direction: column;
  }

  .content-area {
    flex: 1;
    padding: 1rem;
    max-width: 1400px;
    margin: 0 auto;
    width: 100%;
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .count-badge {
    background: var(--primary);
    color: oklch(from var(--primary) 1 0 h);
    font-size: 0.75rem;
    padding: 0.1rem 0.6rem;
    border-radius: 999px;
    font-weight: 700;
  }

  .filter-panel {
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: var(--radius-xl, 1rem);
    padding: 1.25rem;
    box-shadow: 0 4px 20px -5px rgba(0,0,0,0.05);
  }

  .filter-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
    gap: 1rem;
  }

  .filter-item {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .filter-item label {
    font-size: 0.75rem;
    font-weight: 600;
    color: var(--muted-foreground);
    text-transform: uppercase;
    letter-spacing: 0.02em;
  }

  .filter-actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
    margin-top: 1.25rem;
    padding-top: 1rem;
    border-top: 1px solid var(--border);
  }

  .selection-bar {
    position: sticky;
    top: 0.5rem;
    z-index: 30;
    background: var(--primary);
    color: oklch(from var(--primary) 1 0 h);
    border-radius: 999px;
    padding: 0.5rem 1.25rem;
    display: flex;
    align-items: center;
    justify-content: space-between;
    box-shadow: 0 10px 25px -10px rgba(var(--primary-rgb), 0.5);
    margin-bottom: 0.5rem;
  }

  .selection-info {
    display: flex;
    align-items: center;
    gap: 1rem;
  }

  .selection-count {
    font-weight: 700;
    font-size: 0.875rem;
  }

  .list-container {
    flex: 1;
    background: var(--card);
    border: 1px solid var(--border);
    border-radius: var(--radius-xl, 1rem);
    overflow: hidden;
    display: flex;
    flex-direction: column;
  }

  .list-wrapper {
    display: flex;
    flex-direction: column;
    width: 100%;
    border-radius: 12px;
    border: 1px solid var(--border);
    background: var(--card);
    box-shadow: 0 1px 3px var(--border)/40;
    overflow: hidden;
  }

  .list-header {
    display: none;
    grid-template-columns: 48px 1.5fr 0.9fr 1fr 2fr 1.15fr;
    gap: 0 1.25rem;
    padding: 0.875rem 1.25rem;
    background: var(--muted)/8;
    border-bottom: 1px solid var(--border);
    position: sticky;
    top: 0;
    z-index: 10;
    align-items: center;
  }

  .cell-head {
    font-size: 0.6875rem;
    font-weight: 700;
    color: var(--muted-foreground);
    text-transform: uppercase;
    letter-spacing: 0.08em;
    display: flex;
    align-items: center;
    gap: 0.375rem;
  }

  .cell-head-doc,
  .cell-head-date,
  .cell-head-cust,
  .cell-head-name {
    text-align: left;
  }

  .cell-head-amount {
    text-align: right;
    justify-content: flex-end;
  }

  .cell-head.sortable {
    cursor: pointer;
    user-select: none;
    transition: color 0.2s;
  }
  .cell-head.sortable:hover { color: var(--primary); }

  .doc-row {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    padding: 1rem;
    border-bottom: 1px solid var(--border)/40;
    cursor: pointer;
    transition: background 0.2s;
  }

  .doc-row:hover { background: var(--muted)/8; }
  .doc-row.selected { background: var(--primary)/6; }

  .cell-selection {
    width: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
  }

  .doc-info {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.125rem;
  }

  .doc-main {
    display: flex;
    flex-direction: column;
    min-width: 0;
    text-align: left;
  }

  .doc-id { font-size: 0.875rem; }
  .doc-meta { font-size: 0.75rem; }
  .doc-name-stacked { 
    font-size: 0.75rem; 
    color: var(--foreground)/70;
  }

  .cell-amount {
    flex-shrink: 0;
    min-width: 80px;
    font-size: 0.875rem;
    font-variant-numeric: tabular-nums;
    font-weight: 600;
    color: var(--foreground);
    text-align: right;
  }

  /* Desktop Grid Overrides – Document No column shows only the number; hide stacked meta/name; align columns */
  @media (min-width: 768px) {
    .list-header {
      display: grid;
    }

    .doc-row {
      display: grid;
      grid-template-columns: 48px 1.5fr 0.9fr 1fr 2fr 1.15fr;
      gap: 0 1.25rem;
      padding: 0.875rem 1.25rem;
      align-items: center;
    }

    .doc-info {
      display: contents; /* Flatten for grid */
    }

    .doc-main {
      display: block;
      text-align: left;
    }

    .doc-date {
      text-align: left;
    }

    .doc-cust {
      text-align: left;
    }

    .doc-name {
      text-align: left;
      min-width: 0;
    }

    /* Hide date/customer/name under Document No on big screen; they appear in separate columns */
    .doc-meta,
    .doc-name-stacked {
      display: none !important;
    }

    .cell-amount {
      min-width: 0;
      text-align: right;
    }
  }

  /* Grid Layout Adjustments when view is Claim (no amount column) */
  :global(.view-claim) .list-header, 
  :global(.view-claim) .doc-row {
    grid-template-columns: 48px 1.5fr 0.9fr 1.5fr 3fr !important;
  }

  .cust-code {
    background: var(--primary)/10;
    color: var(--primary);
    padding: 0.2rem 0.6rem;
    border-radius: 6px;
    font-size: 0.75rem;
    font-weight: 700;
    font-family: var(--font-mono);
    border: 1px solid var(--primary)/20;
    display: inline-block;
  }

  .truncate-text {
    max-width: 100%;
    display: inline-block;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  @media (min-width: 1024px) {
    .truncate-text {
      max-width: 300px;
    }
  }

  .loading-state {
    padding: 1rem;
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .empty-state, .error-state {
    padding: 4rem 2rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    text-align: center;
    gap: 0.5rem;
  }

  .empty-icon-wrap {
    width: 4rem;
    height: 4rem;
    background: var(--muted)/30;
    border-radius: 2rem;
    display: flex;
    align-items: center;
    justify-content: center;
    margin-bottom: 1rem;
  }

  .empty-state h3, .error-state h3 {
    font-size: 1.25rem;
    font-weight: 700;
  }

  .empty-state p, .error-state p {
    color: var(--muted-foreground);
    max-width: 300px;
  }
</style>
