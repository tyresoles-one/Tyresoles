<script lang="ts">
  import { get } from "svelte/store";
  import { page } from "$app/stores";
  import { afterNavigate, goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import { onMount } from "svelte";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { toast } from "$lib/components/venUI/toast";
  import { graphqlQuery } from "$lib/services/graphql";
  import { ensureFetchParams, fetchParamsStore } from "$lib/managers/stores";
  import { toFetchParamsInput } from "../../logic";
  import { TableCell, TableHead, TableRow, TableBody, TableHeader, Table } from "$lib/components/ui/table";

  import {
    GetProductionProcurementOrdersInfoDocument,
    GetProductionProcurementOrderLinesDocument,
    type GetProductionProcurementOrdersInfoQuery,
    type GetProductionProcurementOrderLinesQuery,
    type GetProductionProcurementOrderLinesQueryVariables,
    type OrderInfoInput,
  } from "$lib/services/graphql/generated/graphql";

  const orderID = $derived(decodeURIComponent($page.params.orderID ?? ""));

  type ProductionOrderInfo = NonNullable<
    GetProductionProcurementOrdersInfoQuery["productionProcurementOrdersInfo"]
  >[number];

  type ProductionOrderLine = NonNullable<
    GetProductionProcurementOrderLinesQuery["productionProcurementOrderLines"]
  >[number];

  /** Same shape as open order line query; resolver keys on `orderNo`. */
  function orderInfoInputForLines(orderNo: string): OrderInfoInput {
    return {
      orderNo,
      amount: 0,
      date: "",
      location: "",
      manager: "",
      managerCode: "",
      qty: 0,
      respCenter: "",
      status: 0,
      supplier: "",
      supplierCode: "",
    };
  }

  const ORDER_HEADER_VIEWS = ["", "Posted", "Dispatched", "Received At Factory", "Purchased"] as const;

  type OrderHeader = {
    no: string;
    postingDate: string | null;
    orderDate: string | null;
    buyFromVendorNo: string;
    buyFromVendorName: string | null;
    responsibilityCenter: string;
    orderStatus: number;
    ecomileProcMgr: string | null;
    date: string;
    location: string;
    manager: string;
    qty: number;
    amount: unknown;
  };

  let header = $state<OrderHeader | null>(null);
  let lines = $state<ProductionOrderLine[]>([]);
  let loadingHeader = $state(true);
  let loadingLines = $state(true);

  let orderDetailsOpen = $state(false);

  async function loadOrderData() {
    await Promise.all([fetchHeader(), fetchLines()]);
  }

  onMount(() => {
    orderDetailsOpen = window.matchMedia("(min-width: 768px)").matches;
    void loadOrderData();
  });

  afterNavigate(({ from, to }) => {
    if (!browser) return;
    const next = to?.params?.orderID;
    if (!next) return;
    const prev = from?.params?.orderID;
    if (from && prev != null && decodeURIComponent(prev) !== decodeURIComponent(next)) {
      void loadOrderData();
    }
  });

  function mapOrderInfoToHeader(o: ProductionOrderInfo): OrderHeader {
    return {
      no: o.orderNo,
      orderDate: o.date || null,
      postingDate: o.date || null,
      buyFromVendorNo: o.supplierCode,
      buyFromVendorName: o.supplier,
      responsibilityCenter: o.respCenter,
      orderStatus: o.status,
      ecomileProcMgr: o.managerCode,
      date: o.date ?? "",
      location: o.location ?? "",
      manager: o.manager ?? "",
      qty: o.qty ?? 0,
      amount: o.amount,
    };
  }

  async function fetchHeader() {
    ensureFetchParams();
    const fetchParams = get(fetchParamsStore);
    if (!fetchParams) {
      toast.error("Please re-login. [Empty Fetch Params]");
      loadingHeader = false;
      return;
    }

    loadingHeader = true;
    try {
      const results = await Promise.all(
        ORDER_HEADER_VIEWS.map((view) =>
          graphqlQuery<GetProductionProcurementOrdersInfoQuery>(GetProductionProcurementOrdersInfoDocument, {
            variables: { param: toFetchParamsInput({ ...fetchParams, view }) },
            skipLoading: true,
            skipCache: true,
          }),
        ),
      );

      for (const res of results) {
        if (!res.success || !res.data?.productionProcurementOrdersInfo?.length) continue;
        const hit = res.data.productionProcurementOrdersInfo.find((o) => o.orderNo === orderID);
        if (hit) {
          header = mapOrderInfoToHeader(hit);
          return;
        }
      }

      toast.error("Posted order not found.");
      goto("/ecoproc/posted");
    } catch {
      toast.error("Failed to load order.");
    } finally {
      loadingHeader = false;
    }
  }

  async function fetchLines() {
    if (!orderID) {
      loadingLines = false;
      return;
    }

    ensureFetchParams();
    if (!get(fetchParamsStore)) {
      loadingLines = false;
      return;
    }

    loadingLines = true;
    try {
      const res = await graphqlQuery<
        GetProductionProcurementOrderLinesQuery,
        GetProductionProcurementOrderLinesQueryVariables
      >(GetProductionProcurementOrderLinesDocument, {
        variables: { param: orderInfoInputForLines(orderID) },
        skipLoading: true,
        skipCache: true,
      });
      if (res.success && res.data?.productionProcurementOrderLines) {
        lines = res.data.productionProcurementOrderLines;
      } else {
        lines = [];
        if (res.error) toast.error(res.error);
      }
    } catch {
      toast.error("Failed to load order lines.");
      lines = [];
    } finally {
      loadingLines = false;
    }
  }

  function reload() {
    void loadOrderData();
  }

  function formatDate(iso: string | null | undefined) {
    if (!iso) return "—";
    const d = new Date(iso);
    return isNaN(d.getTime()) ? "—" : d.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
  }

  function fmt(n: number) {
    return n.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  function formatAmount(val: unknown) {
    if (val == null || val === "") return "—";
    const n = Number(val);
    return Number.isNaN(n) ? "—" : n.toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  const headerStatusLabel: Record<number, string> = {
    0: "Open",
    1: "Posted",
    2: "Dispatched",
    3: "Received At Factory",
    4: "Purchased",
  };

  function getStatusVariant(s: number) {
    if (s === 1) return "bg-green-100 text-green-700 border-green-200";
    if (s >= 2) return "bg-blue-100 text-blue-800 border-blue-200";
    return "bg-amber-100 text-amber-700 border-amber-200";
  }

  function inspectionConditionClass(inspection: string) {
    const t = (inspection || "").toLowerCase();
    if (t === "ok" || t === "") return "bg-green-50 text-green-700 border-green-200";
    if (t.includes("reject") || t.includes("damage") || t.includes("cut")) return "bg-red-50 text-red-700 border-red-200";
    return "bg-muted text-muted-foreground border-border";
  }

  const totalAmount = $derived(lines.reduce((s, l) => s + (Number(l.amount) || 0), 0));
</script>

<svelte:head>
  <title>Posted {orderID} | Ecomile Procurement</title>
</svelte:head>

<div class="min-h-screen bg-muted/30 pb-20">
  <PageHeading
    backHref="/ecoproc/posted"
    backLabel="Tyres Booked"
    icon="clipboard-check"
    class="border-b bg-background"
  >
    {#snippet title()}
      {#if loadingHeader}
        <Skeleton class="h-6 w-40" />
      {:else}
        Posted Order — {header?.no ?? orderID}
      {/if}
    {/snippet}
    {#snippet description()}
      {#if header}
        <span class="inline-flex items-center gap-2">
          <span class="inline-flex items-center rounded border px-2 py-0.5 text-xs font-semibold {getStatusVariant(header.orderStatus)}">
            {headerStatusLabel[header.orderStatus] ?? "Unknown"}
          </span>
          <span class="text-muted-foreground text-xs">{header.buyFromVendorName || "—"}</span>
        </span>
      {/if}
    {/snippet}
    {#snippet actions()}
      <div class="flex flex-wrap items-center gap-2">
        <Button variant="outline" size="sm" onclick={reload} disabled={loadingHeader || loadingLines}>
          <Icon name="refresh-cw" class="mr-2 size-4 {(loadingHeader || loadingLines) ? 'animate-spin' : ''}" />
          Refresh
        </Button>
        <Button
          variant="outline"
          size="sm"
          onclick={() => goto(`/ecoproc/posted/${encodeURIComponent(orderID)}/print`)}
          disabled={!orderID}
        >
          <Icon name="printer" class="mr-2 size-4" />
          Print
        </Button>
      </div>
    {/snippet}
  </PageHeading>

  <div class="container mx-auto max-w-6xl px-4 py-4 space-y-4 sm:py-6 md:px-6">
    <section class="rounded-xl border bg-card shadow-sm overflow-hidden" aria-labelledby="posted-order-details-toggle">
      <button
        type="button"
        id="posted-order-details-toggle"
        class="flex w-full items-start gap-3 border-b border-border/80 bg-card px-4 py-3.5 text-left transition-colors hover:bg-muted/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 sm:items-center sm:px-5 sm:py-4"
        aria-expanded={orderDetailsOpen}
        aria-controls="posted-order-details-panel"
        onclick={() => {
          orderDetailsOpen = !orderDetailsOpen;
        }}
      >
        <div class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <Icon name="package" class="size-4" />
        </div>
        <div class="min-w-0 flex-1">
          <div class="flex flex-col gap-1 sm:flex-row sm:flex-wrap sm:items-baseline sm:gap-x-2">
            <span class="font-semibold text-sm tracking-tight text-foreground">Order details</span>
            {#if header && !orderDetailsOpen && !loadingHeader}
              <span class="text-xs text-muted-foreground sm:truncate sm:max-w-[min(100%,28rem)]">
                <span class="font-medium text-foreground/90">{header.no}</span>
                <span class="mx-1.5 text-border">·</span>
                <span>{header.buyFromVendorName || header.buyFromVendorNo || "—"}</span>
                <span class="mx-1.5 text-border">·</span>
                <span class="font-semibold text-primary">₹{fmt(totalAmount)}</span>
              </span>
            {:else if loadingHeader}
              <span class="text-xs text-muted-foreground">Loading…</span>
            {/if}
          </div>
        </div>
        <Icon
          name="chevron-down"
          class="size-5 shrink-0 text-muted-foreground transition-transform duration-200 {orderDetailsOpen ? 'rotate-180' : ''}"
          aria-hidden="true"
        />
      </button>

      {#if orderDetailsOpen}
        <div id="posted-order-details-panel">
          {#if loadingHeader}
            <div class="grid grid-cols-1 gap-3 p-4 sm:grid-cols-2 sm:gap-4 sm:p-5 md:grid-cols-4">
              {#each { length: 6 } as _}
                <div class="space-y-1.5">
                  <Skeleton class="h-3 w-20" />
                  <Skeleton class="h-5 w-28" />
                </div>
              {/each}
            </div>
          {:else if header}
            <div
              class="grid grid-cols-1 gap-3 p-4 text-sm sm:grid-cols-2 sm:gap-4 sm:p-5 md:grid-cols-3 lg:grid-cols-5"
            >
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Order No</p>
                <p class="mt-0.5 font-semibold wrap-break-word">{header.no}</p>
              </div>
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Order Date</p>
                <p class="mt-0.5 wrap-break-word">{formatDate(header.orderDate)}</p>
              </div>
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Posting Date</p>
                <p class="mt-0.5 wrap-break-word">{formatDate(header.postingDate)}</p>
              </div>
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Location</p>
                <p class="mt-0.5 wrap-break-word">{header.responsibilityCenter || "—"}</p>
              </div>
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Proc. Manager</p>
                <p class="mt-0.5 wrap-break-word">{header.ecomileProcMgr || "—"}</p>
              </div>
              <div class="min-w-0 sm:col-span-2 lg:col-span-2">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Supplier</p>
                <p class="mt-0.5 font-semibold wrap-break-word">{header.buyFromVendorName || header.buyFromVendorNo || "—"}</p>
              </div>
              <div class="min-w-0">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Qty (header)</p>
                <p class="mt-0.5">{header.qty ?? "—"}</p>
              </div>
              <div class="min-w-0 sm:col-span-2 md:col-span-1">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Total Amount</p>
                <p class="mt-0.5 font-bold text-primary">₹{fmt(totalAmount)}</p>
              </div>
            </div>
          {/if}
        </div>
      {/if}
    </section>

    <div class="rounded-xl border bg-card shadow-sm overflow-hidden">
      <div class="flex items-center justify-between gap-3 px-5 py-4 border-b">
        <div class="flex items-center gap-3">
          <div class="flex size-8 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
            <Icon name="list" class="size-4" />
          </div>
          <h2 class="font-semibold text-sm tracking-tight">
            Order lines
            {#if !loadingLines}
              <span class="ml-2 inline-flex items-center rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                {lines.length}
              </span>
            {/if}
          </h2>
        </div>
      </div>

      {#if loadingLines}
        <div class="p-4 space-y-2">
          {#each { length: 5 } as _}
            <div class="flex items-center gap-4 py-2 border-b last:border-0">
              <Skeleton class="h-4 w-24" />
              <Skeleton class="h-4 w-32 flex-1" />
              <Skeleton class="h-4 w-20" />
              <Skeleton class="h-4 w-16" />
            </div>
          {/each}
        </div>
      {:else if lines.length === 0}
        <div class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground">
          <Icon name="inbox" class="size-10 mb-3 opacity-40" />
          <p class="font-medium">No lines for this order</p>
        </div>
      {:else}
        <div class="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow class="bg-muted/30 hover:bg-muted/30">
                <TableHead class="w-[80px]">Line</TableHead>
                <TableHead class="w-[120px]">Item</TableHead>
                <TableHead>Serial No</TableHead>
                <TableHead>Make / Model</TableHead>
                <TableHead>Inspection</TableHead>
                <TableHead>Inspector</TableHead>
                <TableHead class="w-[100px]">Condition</TableHead>
                <TableHead class="w-[100px] text-right">Amount</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {#each lines as line}
                <TableRow
                  class="cursor-pointer hover:bg-muted/40 transition-colors"
                  onclick={() => goto(`/ecoproc/orders/${encodeURIComponent(orderID)}/${line.lineNo}`)}
                >
                  <TableCell class="text-muted-foreground text-xs">{line.lineNo}</TableCell>
                  <TableCell class="font-medium">{line.itemNo || "—"}</TableCell>
                  <TableCell class="font-mono text-xs">{line.serialNo || "—"}</TableCell>
                  <TableCell>{[line.make, line.subMake].filter(Boolean).join(" / ") || "—"}</TableCell>
                  <TableCell>{line.inspection || "—"}</TableCell>
                  <TableCell>{line.inspector || "—"}</TableCell>
                  <TableCell>
                    <span
                      class="inline-flex items-center rounded-full border px-2 py-0.5 text-xs font-medium max-w-[180px] truncate {inspectionConditionClass(line.inspection)}"
                      title={line.inspection || ""}
                    >
                      {line.inspection || "—"}
                    </span>
                  </TableCell>
                  <TableCell class="text-right font-semibold">₹{formatAmount(line.amount)}</TableCell>
                </TableRow>
              {/each}
            </TableBody>
          </Table>
        </div>
        <div class="flex items-center justify-end px-4 py-3 border-t">
          <span class="text-sm font-semibold">Total: <span class="text-primary">₹{fmt(totalAmount)}</span></span>
        </div>
      {/if}
    </div>
  </div>
</div>
