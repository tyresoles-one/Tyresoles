<script lang="ts">
  import { get } from "svelte/store";
  import { goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';
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

  const columns: DataGridColumn<OrderInfo>[] = [
    { accessorKey: 'date', header: 'Date' },
    { accessorKey: 'orderNo', header: 'Order No' },
    { accessorKey: 'supplier', header: 'Supplier' },
    { accessorKey: 'location', header: 'Location' },
    { accessorKey: 'manager', header: 'Manager' },
    { accessorKey: 'qty', header: 'Qty' },
    { accessorKey: 'amount', header: 'Amount' }
  ];
</script>


<DataGrid
  title="Tyres Booking"
  description="Open Ecomile Procurement Orders"
  {items}
  {columns}
  {loading}
  bind:searchQuery
  mobileCardTitleKey="supplier"
  mobileCardSubtitleKey="orderNo"
  onRowClick={(item) => goto('/ecoproc/orders/' + item.orderNo)}
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
</DataGrid>
