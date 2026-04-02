<script lang="ts">
  import { get } from "svelte/store";
  import { page } from "$app/stores";
  import { afterNavigate, goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import { onMount } from "svelte";
  import { z } from "zod";
  import { CreateForm } from "$lib/components/venUI/form";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { toast } from "$lib/components/venUI/toast";
  import { dialogShow } from "$lib/components";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql";
  import { ensureFetchParams, fetchParamsStore } from "$lib/managers/stores";
  import { toFetchParamsInput } from "../../logic";
  import { hydrateOrderStoresFromLines } from "./order-lines-sync";
  import { newOrderLine } from "./logic.svelte.ts";
  import { TableCell, TableHead, TableRow, TableBody, TableHeader, Table } from "$lib/components/ui/table";

  import {
    GetProductionProcurementOrdersInfoDocument,
    GetProductionProcurementOrderLinesDocument,
    UpdateProductionProcurementOrderDocument,
    DeleteProductionProcurementOrderDocument,
    type GetProductionProcurementOrdersInfoQuery,
    type GetProductionProcurementOrderLinesQuery,
    type GetProductionProcurementOrderLinesQueryVariables,
    type OrderInfoInput,
    type UpdateProductionProcurementOrderMutation,
    type DeleteProductionProcurementOrderMutation,
  } from "$lib/services/graphql/generated/graphql";

  /** Same scope as ecoproc supplier directory — casing procurement vendors only. */
  const ECOPROC_VENDOR_CATEGORIES = ["CASING PROCUREMENT"] as const;

  const orderID = $derived($page.params.orderID ?? "");

  type ProductionOrderInfo = NonNullable<
    GetProductionProcurementOrdersInfoQuery["productionProcurementOrdersInfo"]
  >[number];

  type ProductionOrderLine = NonNullable<
    GetProductionProcurementOrderLinesQuery["productionProcurementOrderLines"]
  >[number];

  /** Backend resolver only uses `orderNo`; other fields satisfy the GraphQL input shape. */
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

  // ── State ──────────────────────────────────────────────────────
  type OrderHeader = {
    no: string;
    postingDate: string | null;
    orderDate: string | null;
    buyFromVendorNo: string;
    buyFromVendorName: string | null;
    responsibilityCenter: string;
    orderStatus: number;
    ecomileProcMgr: string | null;
    /** Raw fields from API — required for `OrderInfoInput` on update. */
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
  let saving = $state(false);
  let posting = $state(false);
  let deleting = $state(false);

  /** Mobile-first: collapsed by default; `md` and up opens to show details without an extra tap. */
  let orderDetailsOpen = $state(false);

  /** Header + lines in parallel (full order payload for this page). */
  async function loadOrderData() {
    await Promise.all([fetchHeader(), fetchLines()]);
    syncOrderStoresForNewLineNav();
  }

  /**
   * Live parity: `newOrderLine()` reads `orderStore` + `orderLinesStore`.
   * Keep them aligned with GraphQL-loaded header/lines (does not replace fetch/save).
   */
  function syncOrderStoresForNewLineNav() {
    if (!orderID || !header) return;
    const supplierCode = String(form.values.buyFromVendorNo ?? header.buyFromVendorNo ?? "").trim();
    hydrateOrderStoresFromLines(orderID, supplierCode, lines);
  }

  onMount(() => {
    orderDetailsOpen = window.matchMedia("(min-width: 768px)").matches;
    void loadOrderData();
  });

  /**
   * Reload when:
   * - switching to a different order (`orderID` changes), or
   * - returning from a tyre line (`…/orders/[orderID]/[lineNo]`) to this order — same `orderID` but data changed (Save & Close).
   * Without this, `afterNavigate` skipped because `from.params.orderID === to.params.orderID`.
   */
  afterNavigate(({ from, to }) => {
    if (!browser) return;
    const next = to?.params?.orderID;
    if (!next) return;
    const prev = from?.params?.orderID;
    const fromLineNo = from?.params?.lineNo;
    const toLineNo = to?.params?.lineNo;
    const cameFromLineEditor =
      Boolean(from && fromLineNo != null && String(fromLineNo) !== "") &&
      (toLineNo == null || String(toLineNo) === "") &&
      String(from?.params?.orderID ?? "") === String(next);
    const switchedOrder = Boolean(from && next && prev != null && next !== prev);
    if (switchedOrder || cameFromLineEditor) void loadOrderData();
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

  /** Full `OrderInfoInput` for `updateProductionProcurementOrder` (Nav SOAP uses order no, vendor, manager, status). */
  function orderInfoInputForUpdate(
    h: OrderHeader,
    supplierCode: string,
    supplierName: string,
    opts?: { status?: number },
  ): OrderInfoInput {
    const amt = h.amount != null && h.amount !== "" ? Number(h.amount) : 0;
    return {
      orderNo: h.no,
      supplierCode,
      supplier: supplierName,
      managerCode: h.ecomileProcMgr ?? "",
      manager: h.manager,
      status: opts?.status ?? h.orderStatus,
      respCenter: h.responsibilityCenter,
      qty: h.qty,
      amount: Number.isFinite(amt) ? amt : 0,
      date: h.date,
      location: h.location,
    };
  }

  /** Live `handlePostOrder` — confirm, then `updateProductionProcurementOrder` with `status: 1` (Posted). */
  function handlePostOrder() {
    const h = header;
    if (!h) {
      toast.error("Invalid order for posting.");
      return;
    }
    if ((h.qty ?? 0) === 0 || lines.length === 0) {
      toast.error("Please add at least one tyre.");
      return;
    }
    dialogShow({
      title: "Confirm posting",
      description: "Are you sure you want to post this order?",
      actionLabel: "Post",
      onAction: async () => {
        posting = true;
        try {
          const supplierCode = String(form.values.buyFromVendorNo ?? h.buyFromVendorNo).trim();
          const supplierName =
            supplierCode === h.buyFromVendorNo ? (h.buyFromVendorName ?? "") : "";
          const res = await graphqlMutation<UpdateProductionProcurementOrderMutation>(
            UpdateProductionProcurementOrderDocument,
            {
              variables: {
                order: orderInfoInputForUpdate(h, supplierCode, supplierName, { status: 1 }),
              },
              skipLoading: true,
            },
          );
          if (!res.success || res.data == null) {
            toast.error(res.error ?? "Failed to post order.");
            return;
          }
          toast.success("Order posted successfully.");
          goto("/ecoproc/orders");
        } finally {
          posting = false;
        }
      },
    });
  }

  /** Live: delete order — uses same `OrderInfoInput` shape as update (GraphQL). */
  function handleDeleteOrder() {
    const h = header;
    if (!h) {
      toast.error("Order not loaded.");
      return;
    }
    dialogShow({
      title: "Confirm order deletion",
      description: "Are you sure you want to delete this order? This cannot be undone.",
      actionLabel: "Delete",
      onAction: async () => {
        deleting = true;
        try {
          const supplierCode = String(form.values.buyFromVendorNo ?? h.buyFromVendorNo).trim();
          const supplierName =
            supplierCode === h.buyFromVendorNo ? (h.buyFromVendorName ?? "") : "";
          const res = await graphqlMutation<DeleteProductionProcurementOrderMutation>(
            DeleteProductionProcurementOrderDocument,
            {
              variables: {
                order: orderInfoInputForUpdate(h, supplierCode, supplierName),
              },
              skipLoading: true,
            },
          );
          if (!res.success || res.data == null) {
            toast.error(res.error ?? "Failed to delete order.");
            return;
          }
          toast.success("Order deleted.");
          goto("/ecoproc/orders");
        } finally {
          deleting = false;
        }
      },
    });
  }

  // ── Form ───────────────────────────────────────────────────────
  const headerSchema = z.object({
    buyFromVendorNo: z.string().min(1, "Supplier is required"),
  });

  const form = CreateForm<{ buyFromVendorNo: string }>({
    schema: headerSchema,
    initialValues: { buyFromVendorNo: "" },
    onSubmit: async (values) => {
      if (!header) {
        toast.error("Order not loaded.");
        return;
      }
      saving = true;
      try {
        const supplierName =
          values.buyFromVendorNo === header.buyFromVendorNo
            ? (header.buyFromVendorName ?? "")
            : "";
        const res = await graphqlMutation<UpdateProductionProcurementOrderMutation>(
          UpdateProductionProcurementOrderDocument,
          {
            variables: {
              order: orderInfoInputForUpdate(header, values.buyFromVendorNo, supplierName),
            },
            skipLoading: true,
          },
        );
        if (!res.success || res.data == null) {
          toast.error(res.error ?? "Failed to update supplier.");
          return;
        }
        toast.success("Supplier updated.");
        await fetchHeader(values.buyFromVendorNo.trim());
        syncOrderStoresForNewLineNav();
      } finally {
        saving = false;
      }
    },
  });

  // ── Fetch ──────────────────────────────────────────────────────
  /** @param preferredSupplierCode After save, Nav may return blank `supplierCode` briefly — keep the submitted code. */
  async function fetchHeader(preferredSupplierCode?: string) {
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
          })
        )
      );

      for (const res of results) {
        if (!res.success || !res.data?.productionProcurementOrdersInfo?.length) continue;
        const hit = res.data.productionProcurementOrdersInfo.find((o) => o.orderNo === orderID);
        if (hit) {
          header = mapOrderInfoToHeader(hit);
          const fromApi = (hit.supplierCode ?? "").trim();
          const merged =
            fromApi ||
            (preferredSupplierCode ?? "").trim() ||
            String(form.values.buyFromVendorNo ?? "").trim();
          form.setValue("buyFromVendorNo", merged);
          return;
        }
      }

      toast.error("Order not found.");
      goto("/ecoproc/orders");
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

  // ── Helpers ────────────────────────────────────────────────────
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

  /** Header order status from `productionProcurementOrdersInfo` (Nav purchase header). */
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

  const totalAmount = $derived(
    lines.reduce((s, l) => s + (Number(l.amount) || 0), 0)
  );
</script>

<svelte:head>
  <title>Order {orderID} | Ecomile Procurement</title>
</svelte:head>

<div class="min-h-screen bg-muted/30 pb-20">
  <PageHeading
    backHref="/ecoproc/orders"
    backLabel="Tyres Booking"
    icon="file-text"
    class="border-b bg-background"
  >
    {#snippet title()}
      {#if loadingHeader}
        <Skeleton class="h-6 w-36" />
      {:else}
        Order — {header?.no ?? orderID}
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
        {#if header?.orderStatus === 0}
          <Button
            variant="outline"
            size="sm"
            class="border-2"
            onclick={() => newOrderLine()}
            disabled={loadingHeader || loadingLines || !header?.buyFromVendorNo}
            title={!header?.buyFromVendorNo ? "Select a supplier in order details first" : undefined}
          >
            <Icon name="plus" class="mr-2 size-4" />
            New Tyre
          </Button>
          <Button
            size="sm"
            variant="default"
            onclick={handlePostOrder}
            disabled={posting || saving || loadingHeader || loadingLines || form.isSubmitting}
          >
            {#if posting}
              <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
            {:else}
              <Icon name="send" class="mr-2 size-4" />
            {/if}
            Post Order
          </Button>
          <Button
            size="sm"
            variant="outline"
            class="border-destructive/30 text-destructive hover:bg-destructive/10"
            onclick={handleDeleteOrder}
            disabled={deleting || saving || loadingHeader || loadingLines || form.isSubmitting}
          >
            {#if deleting}
              <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
            {:else}
              <Icon name="trash-2" class="mr-2 size-4" />
            {/if}
            Delete Order
          </Button>
        {/if}
      </div>
    {/snippet}
  </PageHeading>

  <div class="container mx-auto max-w-6xl px-4 py-4 space-y-4 sm:py-6 md:px-6">

    <!-- Header Card (collapsible) -->
    <section class="rounded-xl border bg-card shadow-sm overflow-hidden" aria-labelledby="order-details-toggle">
      <button
        type="button"
        id="order-details-toggle"
        class="flex w-full items-start gap-3 border-b border-border/80 bg-card px-4 py-3.5 text-left transition-colors hover:bg-muted/35 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background sm:items-center sm:px-5 sm:py-4"
        aria-expanded={orderDetailsOpen}
        aria-controls="order-details-panel"
        onclick={() => {
          orderDetailsOpen = !orderDetailsOpen;
        }}
      >
        <div class="flex size-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <Icon name="package" class="size-4" />
        </div>
        <div class="min-w-0 flex-1">
          <div class="flex flex-col gap-1 sm:flex-row sm:flex-wrap sm:items-baseline sm:gap-x-2 sm:gap-y-0">
            <span class="font-semibold text-sm tracking-tight text-foreground">Order Details</span>
            {#if header && !orderDetailsOpen && !loadingHeader}
              <span class="text-xs text-muted-foreground sm:truncate sm:max-w-[min(100%,28rem)]">
                <span class="font-medium text-foreground/90">{header.no}</span>
                <span class="mx-1.5 text-border">·</span>
                <span>{header.buyFromVendorName || header.buyFromVendorNo || "—"}</span>
                <span class="mx-1.5 text-border">·</span>
                <span class="font-semibold text-primary">₹{fmt(totalAmount)}</span>
              </span>
            {:else if loadingHeader}
              <span class="text-xs text-muted-foreground">Loading order…</span>
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
        <div id="order-details-panel">
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
              class="grid grid-cols-1 gap-3 p-4 text-sm sm:grid-cols-2 sm:gap-4 sm:p-5 md:grid-cols-3 lg:grid-cols-5 lg:gap-x-4 lg:gap-y-3"
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
              <div class="min-w-0 sm:col-span-2 md:col-span-1 lg:col-span-1">
                <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Total Amount</p>
                <p class="mt-0.5 font-bold text-primary">₹{fmt(totalAmount)}</p>
              </div>
            </div>
            {#if header.orderStatus === 0}
              <div class="border-t px-4 py-4 sm:px-5">
                <div
                  class="flex w-full max-w-xl flex-col gap-4 md:max-w-3xl md:flex-row md:items-end md:gap-3 lg:max-w-4xl"
                >
                  <div class="min-w-0 flex-1">
                    <MasterSelect
                      {form}
                      fieldName="buyFromVendorNo"
                      masterType="vendors"
                      label="Supplier"
                      placeholder="Search supplier…"
                      singleSelect={true}
                      vendorCategories={[...ECOPROC_VENDOR_CATEGORIES]}
                      respCenterOverride={header.responsibilityCenter?.trim() || undefined}
                    />
                  </div>
                  <Button
                    class="w-full shrink-0 md:w-auto"
                    onclick={() => form.submit()}
                    disabled={saving || form.isSubmitting}
                    size="sm"
                  >
                    <Icon name="save" class="mr-2 size-4" />
                    Update Supplier
                  </Button>
                </div>
              </div>
            {/if}
          {/if}
        </div>
      {/if}
    </section>

    <!-- Lines Table -->
    <div class="rounded-xl border bg-card shadow-sm overflow-hidden">
      <div class="flex items-center justify-between gap-3 px-5 py-4 border-b">
        <div class="flex items-center gap-3">
          <div class="flex size-8 items-center justify-center rounded-lg bg-blue-50 text-blue-600">
            <Icon name="list" class="size-4" />
          </div>
          <h2 class="font-semibold text-sm tracking-tight">
            Order Lines
            {#if !loadingLines}
              <span class="ml-2 inline-flex items-center rounded-full bg-muted px-2 py-0.5 text-xs font-medium text-muted-foreground">
                {lines.length}
              </span>
            {/if}
          </h2>
        </div>
        {#if header?.orderStatus === 0}
          <Button
            size="sm"
            variant="outline"
            class="gap-1.5 border-2"
            onclick={() => newOrderLine()}
            disabled={loadingLines || !header?.buyFromVendorNo}
            title={!header?.buyFromVendorNo ? "Set supplier in order details first" : undefined}
          >
            <Icon name="plus" class="size-4" />
            New Tyre
          </Button>
        {/if}
      </div>

      {#if loadingLines}
        <div class="p-4 space-y-2">
          {#each { length: 5 } as _, i}
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
          <p class="font-medium">No tyre lines yet</p>
          <p class="text-sm mt-1">Click &quot;New Tyre&quot; to add the first line.</p>
        </div>
      {:else}
        <div class="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow class="bg-muted/30 hover:bg-muted/30">
                <TableHead class="w-[80px]">Line</TableHead>
                <TableHead class="w-[72px]">Sort</TableHead>
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
                  onclick={() => goto(`/ecoproc/orders/${orderID}/${line.lineNo}`)}
                >
                  <TableCell class="text-muted-foreground text-xs">{line.lineNo}</TableCell>
                  <TableCell class="text-muted-foreground text-xs">{line.sortNo || "—"}</TableCell>
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
