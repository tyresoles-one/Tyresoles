<script lang="ts">
  import { get } from "svelte/store";
  import { goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { TableCell, TableHead } from "$lib/components/ui/table";
  import { Button } from "$lib/components/ui/button";
  import { graphqlQuery, graphqlMutation } from "$lib/services/graphql";
  import { toast } from "$lib/components";
  import { ensureFetchParams, fetchParamsStore } from "$lib/managers/stores";
  import { authStore } from "$lib/stores/auth";
  import { toFetchParamsInput } from "../logic";

  import {
    GetProductionProcurementOrdersInfoDocument,
    NewProductionProcurementOrderDocument,
    type GetProductionProcurementOrdersInfoQuery,
    type GetProductionProcurementOrdersInfoQueryVariables,
    type NewProductionProcurementOrderMutation,
    type NewProductionProcurementOrderMutationVariables,
  } from "$lib/services/graphql/generated/graphql";

  type OrderInfo = NonNullable<
    GetProductionProcurementOrdersInfoQuery["productionProcurementOrdersInfo"]
  >[number];

  let viewMode = $state<"grid" | "table">("table");
  let items = $state<OrderInfo[]>([]);
  let totalCount = $state(0);
  let loading = $state(false);
  let error = $state<string | undefined>(undefined);
  let searchQuery = $state("");
  let creatingOrder = $state(false);

  async function createNewOrder() {
    ensureFetchParams();
    const fetchParams = get(fetchParamsStore);
    if (!fetchParams) {
      toast.error("Please re-login. [Empty Fetch Params]");
      return;
    }

    creatingOrder = true;
    try {
      const res = await graphqlMutation<
        NewProductionProcurementOrderMutation,
        NewProductionProcurementOrderMutationVariables
      >(NewProductionProcurementOrderDocument, {
        variables: {
          param: toFetchParamsInput({ ...fetchParams, view: "" }),
        },
        skipLoading: true,
      });

      if (res.success && res.data?.newProductionProcurementOrder) {
        const docNo = res.data.newProductionProcurementOrder.trim();
        if (docNo) {
          toast.success(`Order ${docNo} created.`);
          await goto(`/ecoproc/orders/${encodeURIComponent(docNo)}`);
        } else {
          toast.error("Order was created but no document number was returned.");
        }
      } else {
        toast.error(res.error || "Failed to create order.");
      }
    } catch {
      toast.error("An unexpected error occurred.");
    } finally {
      creatingOrder = false;
    }
  }

  async function fetchOrders() {
    ensureFetchParams();
    const fetchParams = get(fetchParamsStore);
    if (!fetchParams) {
      error = "Please re-login. [Empty Fetch Params]";
      return;
    }

    loading = true;
    error = undefined;

    try {
      const res = await graphqlQuery<
        GetProductionProcurementOrdersInfoQuery,
        GetProductionProcurementOrdersInfoQueryVariables
      >(GetProductionProcurementOrdersInfoDocument, {
        variables: {
          param: toFetchParamsInput({ ...fetchParams, view: "" }), // view: "" = Open Orders (status 0)
        },
        skipLoading: true,
        // Default graphqlQuery cache is 5m; without this, the list stays stale after creating an order or returning from detail.
        skipCache: true,
      });

      if (res.success && res.data?.productionProcurementOrdersInfo) {
        items = res.data.productionProcurementOrdersInfo;
        totalCount = items.length;
      } else {
        error = res.error || "Failed to load orders.";
      }
    } catch {
      error = "An unexpected error occurred.";
    } finally {
      loading = false;
    }
  }

  function reload() {
    fetchOrders();
  }

  function formatAmount(val: unknown) {
    if (val == null || val === "") return "—";
    const n = Number(val);
    return isNaN(n) ? "—" : n.toLocaleString("en-IN");
  }

  /** Same as ecoproc home / newnumber: hydrate fetch params from auth, then load orders (handles direct navigation to this route). */
  $effect(() => {
    if (!browser) return;
    void $authStore.user?.userId;
    ensureFetchParams();
    if (!get(fetchParamsStore)) return;
    void fetchOrders();
  });
</script>

<MasterList
  title="Tyres Booking"
  description="Open Ecomile Procurement Orders"
  {items}
  {loading}
  loadingMore={false}
  {error}
  hasMore={false}
  {totalCount}
  bind:searchQuery
  bind:viewMode
  onRefresh={reload}
  onLoadMore={() => {}}
>
  {#snippet actions()}
    <Button
      variant="default"
      size="sm"
      class="gap-2 shrink-0"
      onclick={() => createNewOrder()}
      disabled={creatingOrder || loading}
    >
      <Icon name="plus" class="size-4 {creatingOrder ? 'animate-pulse' : ''}" />
      {creatingOrder ? "Creating…" : "New Order"}
    </Button>
  {/snippet}

  {#snippet tableHeader()}
    <TableHead class="w-[110px]">Date</TableHead>
    <TableHead class="w-[150px]">Order No</TableHead>
    <TableHead>Supplier</TableHead>
    <TableHead class="w-[120px]">Location</TableHead>
    <TableHead class="w-[110px]">Manager</TableHead>
    <TableHead class="w-[70px] text-right">Qty</TableHead>
    <TableHead class="w-[110px] text-right">Amount</TableHead>
  {/snippet}

  {#snippet tableRow(item)}
    <TableCell class="font-medium text-muted-foreground">{item.date || "—"}</TableCell>
    <TableCell>
      <button
        class="text-primary hover:underline font-semibold text-left focus:outline-none"
        onclick={() => goto(`/ecoproc/orders/${item.orderNo}`)}
      >
        {item.orderNo}
      </button>
    </TableCell>
    <TableCell>{item.supplier || "—"}</TableCell>
    <TableCell>{item.location || item.respCenter || "—"}</TableCell>
    <TableCell>{item.manager || "—"}</TableCell>
    <TableCell class="text-right tabular-nums">{item.qty ?? 0}</TableCell>
    <TableCell class="text-right tabular-nums">{formatAmount(item.amount)}</TableCell>
  {/snippet}

  {#snippet gridItem(item)}
    <div
      class="flex flex-col gap-3 rounded-xl border bg-card p-5 shadow-sm transition-all hover:border-primary/50 hover:shadow-md h-full cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      tabindex="0"
      role="button"
      onclick={() => goto(`/ecoproc/orders/${item.orderNo}`)}
      onkeydown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          goto(`/ecoproc/orders/${item.orderNo}`);
        }
      }}
    >
      <div class="flex items-center justify-between">
        <span class="inline-flex items-center rounded-md bg-secondary px-2 py-1 text-xs font-medium text-secondary-foreground">
          {item.date || "—"}
        </span>
        <span class="text-sm font-semibold text-primary">{item.orderNo}</span>
      </div>
      <div>
        <h3 class="font-semibold text-card-foreground line-clamp-2">
          {item.supplier || "Unknown Supplier"}
        </h3>
        <p class="text-xs text-muted-foreground mt-1 flex items-center gap-1">
          <Icon name="map-pin" class="size-3" />
          {item.location || item.respCenter || "—"}
        </p>
      </div>
      <div class="mt-auto pt-3 border-t flex items-center justify-between text-xs text-muted-foreground">
        <span class="flex items-center gap-1">
          <Icon name="user" class="size-3" />
          {item.manager || "—"}
        </span>
        <span class="flex items-center gap-1 font-medium">
          <Icon name="package" class="size-3" />
          {item.qty ?? 0} tyres
        </span>
      </div>
    </div>
  {/snippet}
</MasterList>
