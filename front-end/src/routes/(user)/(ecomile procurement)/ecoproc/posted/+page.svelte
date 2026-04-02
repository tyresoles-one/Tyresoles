<script lang="ts">
  import { get } from "svelte/store";
  import { goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { TableCell, TableHead } from "$lib/components/ui/table";
  import { graphqlQuery } from "$lib/services/graphql";
  import { ensureFetchParams, fetchParamsStore, postedOrderStore, settingsStore } from "$lib/managers/stores";
  import { authStore } from "$lib/stores/auth";
  import { toFetchParamsInput } from "../logic";

  import {
    GetProductionProcurementOrdersInfoDocument,
    type GetProductionProcurementOrdersInfoQuery,
    type GetProductionProcurementOrdersInfoQueryVariables,
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

  async function fetchPostedOrders() {
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
          param: toFetchParamsInput({ ...fetchParams, view: "Posted" }),
        },
        skipLoading: true,
        skipCache: true,
      });

      if (res.success && res.data?.productionProcurementOrdersInfo) {
        items = res.data.productionProcurementOrdersInfo;
        totalCount = items.length;
      } else {
        error = res.error || "Failed to load posted orders.";
      }
    } catch {
      error = "An unexpected error occurred.";
    } finally {
      loading = false;
    }
  }

  function reload() {
    fetchPostedOrders();
  }

  function formatAmount(val: unknown) {
    if (val == null || val === "") return "—";
    const n = Number(val);
    return isNaN(n) ? "—" : n.toLocaleString("en-IN");
  }

  /**
   * `postedOrderStore.set(order)` then `goto` — keeps posted detail + REST lines in sync.
   */
  function openPostedOrder(item: OrderInfo) {
    const no = String(item.orderNo ?? "").trim();
    if (!no) return;
    postedOrderStore.set({
      orderNo: no,
      supplierCode: item.supplierCode ?? "",
      supplier: item.supplier ?? "",
      respCenter: item.respCenter ?? "",
    });
    settingsStore.update((s) => ({
      ...s,
      activePage: `Posted Order No. ${no}`,
    }));
    goto(`/ecoproc/posted/${encodeURIComponent(no)}`);
  }

  $effect(() => {
    if (!browser) return;
    void $authStore.user?.userId;
    ensureFetchParams();
    if (!get(fetchParamsStore)) return;
    void fetchPostedOrders();
  });
</script>

<MasterList
  title="Tyres Booked"
  description="Posted Ecomile Procurement Orders"
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
  onRowClick={openPostedOrder}
  gridKeyboardNav={true}
>
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
        onclick={() => openPostedOrder(item)}
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
      onclick={() => openPostedOrder(item)}
      onkeydown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          openPostedOrder(item);
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
