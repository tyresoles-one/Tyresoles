<script lang="ts">
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";
  import { browser } from "$app/environment";
  import { ensureFetchParams, fetchParamsStore, orderStore } from "$lib/managers/stores";
  import { get } from "svelte/store";
  import { ensureOrderLinesStoreForOrder } from "../order-lines-sync";
  import { newOrderLine } from "../logic.svelte.ts";
  import { Icon } from "$lib/components/venUI/icon";

  const orderID = $derived($page.params.orderID ?? "");

  let err = $state<string | null>(null);

  onMount(async () => {
    if (!browser || !orderID) return;
    ensureFetchParams();
    if (!get(fetchParamsStore)) {
      err = "Please re-login. [Empty Fetch Params]";
      return;
    }

    const ensured = await ensureOrderLinesStoreForOrder(orderID);
    if (!ensured.success) {
      err = ensured.error || "Failed to load order lines.";
      return;
    }

    if (!String(get(orderStore)?.supplierCode ?? "").trim()) {
      err = "Set a supplier on the order before adding a tyre.";
      return;
    }

    newOrderLine();
  });
</script>

<svelte:head>
  <title>Add Tyre — {orderID} | Ecomile Procurement</title>
</svelte:head>

<div class="flex min-h-[40vh] flex-col items-center justify-center gap-3 px-4 py-12 text-center">
  {#if err}
    <p class="text-sm text-destructive">{err}</p>
    <button
      type="button"
      class="text-sm text-primary underline"
      onclick={() => goto(`/ecoproc/orders/${orderID}`)}
    >
      Back to order
    </button>
  {:else}
    <Icon name="loader-2" class="size-8 animate-spin text-muted-foreground" />
    <p class="text-sm text-muted-foreground">Preparing new tyre line…</p>
  {/if}
</div>
