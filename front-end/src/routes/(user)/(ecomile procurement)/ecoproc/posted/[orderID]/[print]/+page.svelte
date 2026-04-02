<script lang="ts">
  import { get } from "svelte/store";
  import { page } from "$app/stores";
  import { onMount } from "svelte";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Button } from "$lib/components/ui/button";
  import PdfViewer from "$lib/components/venUI/pdf-viewer/PdfViewer.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { updateGoBackPath } from "$lib/components";
  import { fetchParamsStore, ensureFetchParams } from "$lib/managers/stores";
  import { exportProductionReport } from "$lib/services/productionReports";
  import type { ReportFetchParams } from "$lib/services/salesReports";
  import { getUser } from "$lib/stores/auth";
  import { toast } from "$lib/components/venUI/toast";
  import { cn } from "$lib/utils";

  /** Matches Live: Posted Proc Order + Vendor Bill (type Posted for procurement). */
  type PostedPrintReport = "Posted Proc Order" | "Vendor Bill";

  const orderID = $derived(decodeURIComponent($page.params.orderID ?? ""));
  const closePath = $derived(
    orderID ? `/ecoproc/posted/${encodeURIComponent(orderID)}` : "/ecoproc/posted",
  );

  let reportName = $state<PostedPrintReport>("Posted Proc Order");
  let pdfData = $state<Uint8Array | null>(null);
  let pdfFileName = $state("report.pdf");
  let loading = $state(false);
  let error = $state<string | null>(null);

  onMount(() => {
    ensureFetchParams();
  });

  $effect(() => {
    updateGoBackPath(closePath);
  });

  function buildReportParams(): ReportFetchParams {
    const user = getUser();
    const fp = get(fetchParamsStore) ?? {};
    const respCenters: string[] =
      Array.isArray(fp.respCenters) && fp.respCenters.length
        ? fp.respCenters
        : user?.respCenter
          ? [user.respCenter]
          : [];
    const now = new Date();
    const from = new Date(
      now.getFullYear(),
      now.getMonth(),
      now.getDate(),
    ).toISOString();
    const to = now.toISOString();

    return {
      from,
      to,
      reportOutput: "PDF",
      view: "All",
      type: reportName === "Vendor Bill" ? "Posted" : "",
      respCenters,
      customers: [],
      dealers: [],
      areas: [],
      regions: [],
      nos: orderID ? [orderID] : [],
      entityCode: user?.entityCode ?? undefined,
      entityType: user?.entityType ?? undefined,
      entityDepartment: user?.department ?? undefined,
      userSpecialToken:
        typeof fp.userSpecialToken === "string" ? fp.userSpecialToken : undefined,
    };
  }

  async function loadReport() {
    if (!orderID) return;
    ensureFetchParams();
    error = null;
    pdfData = null;
    loading = true;
    try {
      const params = buildReportParams();
      const { blob, fileName } = await exportProductionReport(reportName, params);
      const buffer = await blob.arrayBuffer();
      pdfData = new Uint8Array(buffer);
      pdfFileName = fileName;
    } catch (e: unknown) {
      const msg =
        e instanceof Error ? e.message : "Failed to generate report";
      error = msg;
      toast.error(msg);
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    const id = orderID;
    const name = reportName;
    if (!id) return;
    void loadReport();
  });
</script>

<div class="min-h-screen bg-background pb-safe">
  <PageHeading
    backHref={closePath}
    icon="printer"
    pageTitle="Posted order print"
  >
    {#snippet title()}
      <span class="truncate">Print — {orderID || "—"}</span>
    {/snippet}
    {#snippet description()}
      <span class="text-muted-foreground text-sm">Posted procurement reports</span>
    {/snippet}
    {#snippet actions()}
      <div class="flex flex-wrap items-center gap-2">
        <Button
          variant={reportName === "Posted Proc Order" ? "default" : "outline"}
          size="sm"
          class="gap-1.5"
          onclick={() => {
            reportName = "Posted Proc Order";
          }}
        >
          <Icon name="list" class="size-4" />
          Posted Order
        </Button>
        <Button
          variant={reportName === "Vendor Bill" ? "default" : "outline"}
          size="sm"
          class="gap-1.5"
          onclick={() => {
            reportName = "Vendor Bill";
          }}
        >
          <Icon name="file-text" class="size-4" />
          Vendor Bill
        </Button>
        <Button
          variant="outline"
          size="sm"
          class="gap-1.5"
          disabled={loading || !orderID}
          onclick={() => void loadReport()}
        >
          <Icon name="refresh-cw" class={cn("size-4", loading && "animate-spin")} />
          Refresh
        </Button>
      </div>
    {/snippet}
  </PageHeading>

  <div class="container mx-auto px-4 py-4">
    {#if !orderID}
      <p class="text-sm text-muted-foreground">Missing order in URL.</p>
    {:else if error}
      <div
        class="flex items-start gap-2 rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-destructive text-sm"
      >
        <Icon name="alert-circle" class="size-5 shrink-0 mt-0.5" />
        <span>{error}</span>
      </div>
    {/if}

    {#if orderID}
      <div
        class={cn(
          "relative min-h-[70vh] rounded-xl border bg-muted/20 overflow-hidden",
          loading && "opacity-70 pointer-events-none",
        )}
      >
        {#if loading && !pdfData}
          <div
            class="absolute inset-0 z-10 flex flex-col items-center justify-center gap-3 bg-background/80"
          >
            <Icon name="loader-2" class="size-10 animate-spin text-primary" />
            <p class="text-sm text-muted-foreground">Generating report…</p>
          </div>
        {/if}
        {#if pdfData}
          <PdfViewer data={pdfData} fileName={pdfFileName} class="min-h-[70vh]" />
        {:else if !loading && !error}
          <div
            class="flex min-h-[50vh] flex-col items-center justify-center gap-2 p-8 text-center text-muted-foreground"
          >
            <Icon name="file-search" class="size-12 opacity-40" />
            <p class="text-sm">No preview yet.</p>
          </div>
        {/if}
      </div>
    {/if}
  </div>
</div>
