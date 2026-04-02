<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { ensureFetchParams } from "$lib/managers/stores";
  import { fetchStatesAndMarkets } from "./logic"; // Adjusted from "../logic" to "./logic"
  import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { TableCell, TableHead } from "$lib/components/ui/table";
  import { Button } from "$lib/components/ui/button";
  import { cn } from "$lib/utils";
  import { graphqlQuery } from "$lib/services/graphql";
  import {
    GetMyVendorsDocument,
    type GetMyVendorsQuery,
    type VendorFilterInput,
  } from "$lib/services/graphql/generated/graphql";

  /** Matches Tyresoles.Web Query.GetMyVendors — casing procurement scope for this route */
  const ECOPROC_VENDOR_CATEGORIES = ["CASING PROCUREMENT"] as const;

  type VendorNode = NonNullable<
    NonNullable<GetMyVendorsQuery["myVendors"]>["items"]
  >[number];

  let viewMode = $state<"grid" | "table">("table");
  let items = $state<VendorNode[]>([]);
  let totalCount = $state(0);
  let cursor = $state<string | null>(null);
  let hasMore = $state(false);
  let loading = $state(false);
  let loadingMore = $state(false);
  let error = $state<string | undefined>(undefined);
  let searchQuery = $state("");

  /** Normalized search for API + effect dedup (case-insensitive). */
  function searchKey(q: string): string {
    return q.trim().toLowerCase();
  }

  function vendorListKey(): string {
    return searchKey(searchQuery);
  }

  let prevVendorListKey: string | null = null;
  let filterDebounce: ReturnType<typeof setTimeout> | null = null;

  function buildVendorWhere(): VendorFilterInput | undefined {
    const term = searchKey(searchQuery);
    if (!term) return undefined;
    return { or: [{ name: { contains: term } }] };
  }

  async function fetchPage(afterCursor: string | null, append: boolean) {
    if (append) loadingMore = true;
    else {
      loading = true;
      error = undefined;
    }

    try {
      const res = await graphqlQuery<GetMyVendorsQuery>(GetMyVendorsDocument, {
        variables: {
          first: 20,
          after: afterCursor ?? undefined,
          where: buildVendorWhere(),
          categories: [...ECOPROC_VENDOR_CATEGORIES],
        },
        skipLoading: true,
        skipCache: true,
      });

      if (res.success && res.data?.myVendors) {
        const data = res.data.myVendors;
        const newItems = data.items ?? [];
        if (append) {
          items = [...items, ...newItems];
        } else {
          items = newItems;
          totalCount = data.totalCount ?? 0;
        }
        cursor = data.pageInfo?.endCursor ?? null;
        hasMore = data.pageInfo?.hasNextPage ?? false;
      } else {
        error = res.error || "Failed to load suppliers.";
      }
    } catch {
      error = "An unexpected error occurred.";
    } finally {
      loading = false;
      loadingMore = false;
    }
  }

  function reload() {
    fetchPage(null, false);
  }

  function loadMore() {
    if (loadingMore || !hasMore || !cursor) return;
    fetchPage(cursor, true);
  }

  $effect(() => {
    searchQuery;
    const key = vendorListKey();
    filterDebounce && clearTimeout(filterDebounce);
    if (prevVendorListKey === null) {
      prevVendorListKey = key;
      reload();
      return () => {
        if (filterDebounce) clearTimeout(filterDebounce);
      };
    }
    if (prevVendorListKey === key) {
      return () => {
        if (filterDebounce) clearTimeout(filterDebounce);
      };
    }
    prevVendorListKey = key;
    filterDebounce = setTimeout(() => {
      filterDebounce = null;
      reload();
    }, 400);
    return () => {
      if (filterDebounce) clearTimeout(filterDebounce);
    };
  });

  function toNum(v: unknown): number {
    if (v == null) return 0;
    if (typeof v === "number" && !Number.isNaN(v)) return v;
    const n = parseFloat(String(v));
    return Number.isNaN(n) ? 0 : n;
  }

  function fmt(n: number) {
    return n.toLocaleString("en-IN", {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    });
  }

  onMount(() => {
    ensureFetchParams();
    void fetchStatesAndMarkets();
  });
</script>

{#snippet beforeList()}
  <div class="mb-6 space-y-8">
    <section class="space-y-3" aria-labelledby="ecoproc-vendors-scope-heading">
      <div class="flex items-center gap-2">
        <div class="h-px flex-1 bg-linear-to-r from-primary/20 via-primary/40 to-transparent"></div>
        <h2 id="ecoproc-vendors-scope-heading" class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2">
          <Icon name="package" class="size-3.5 shrink-0" />
          Procurement scope
        </h2>
        <div class="h-px flex-1 bg-linear-to-l from-primary/20 via-primary/40 to-transparent"></div>
      </div>
      <p class="text-sm text-muted-foreground max-w-2xl leading-relaxed">
        Suppliers listed here are limited to
        <span class="font-medium text-foreground">casing procurement</span>
        for Ecomile. Use search to filter by name; open a row to edit full supplier details.
      </p>
    </section>

    <section class="space-y-3" aria-labelledby="ecoproc-vendors-directory-heading">
      <div class="flex items-center gap-2">
        <div class="h-px flex-1 bg-linear-to-r from-primary/20 via-primary/40 to-transparent"></div>
        <h2 id="ecoproc-vendors-directory-heading" class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2">
          <Icon name="users" class="size-3.5 shrink-0" />
          Supplier directory
        </h2>
        <div class="h-px flex-1 bg-linear-to-l from-primary/20 via-primary/40 to-transparent"></div>
      </div>
      <p class="text-xs text-muted-foreground sm:text-sm">
        Switch between grid and table using the toolbar above. Balances reflect the current ledger total.
      </p>
    </section>
  </div>
{/snippet}

<MasterList
  title="Suppliers"
  description="Manage Casing Procurement Vendors"
  {items}
  {loading}
  {loadingMore}
  {error}
  {hasMore}
  {totalCount}
  bind:searchQuery
  bind:viewMode
  searchPlaceholder="Search by supplier name…"
  onRefresh={reload}
  onLoadMore={loadMore}
  {beforeList}
>
  {#snippet actions()}
    <Button
      variant="default"
      size="sm"
      class="gap-2 shrink-0"
      onclick={() => console.log("New Vendor logic...")}
    >
      <Icon name="plus" class="size-4" />
      New Supplier
    </Button>
  {/snippet}

  {#snippet tableHeader()}
    <TableHead class="w-[120px]">Code</TableHead>
    <TableHead>Name</TableHead>
    <TableHead class="w-[180px]">City</TableHead>
    <TableHead class="w-[140px]">Phone</TableHead>
    <TableHead class="w-[140px] text-right">Balance</TableHead>
  {/snippet}

  {#snippet tableRow(item)}
    <TableCell>
      <button
        class="text-primary hover:underline font-semibold text-left focus:outline-none"
        onclick={() => goto(`./vendors/${item.no}`)}
      >
        {item.no}
      </button>
    </TableCell>
    <TableCell class="font-medium">{item.name || "—"}</TableCell>
    <TableCell>{item.city || "—"}</TableCell>
    <TableCell>{item.phoneNo || "—"}</TableCell>
    <TableCell
      class="text-right font-semibold {toNum(item.balance) < 0
        ? 'text-destructive'
        : 'text-green-600'}"
    >
      ₹{fmt(toNum(item.balance))}
    </TableCell>
  {/snippet}

  {#snippet gridItem(item)}
    <div
      class="flex flex-col gap-3 rounded-xl border bg-card p-5 shadow-sm transition-all hover:border-primary/50 hover:shadow-md h-full cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      tabindex="0"
      role="button"
      onclick={() => goto(`./vendors/${item.no}`)}
      onkeydown={(e) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          goto(`./vendors/${item.no}`);
        }
      }}
    >
      <div class="flex items-center justify-between">
        <span class="inline-flex items-center rounded-md bg-secondary px-2 py-1 text-xs font-medium text-secondary-foreground">
          {item.no}
        </span>
        <span class="text-xs font-medium text-muted-foreground">{item.city || "—"}</span>
      </div>
      <div>
        <h3 class="font-semibold text-card-foreground line-clamp-2">
          {item.name || "Unknown Supplier"}
        </h3>
        <p class="text-xs text-muted-foreground mt-1 flex items-center gap-1">
          <Icon name="phone" class="size-3" />
          {item.phoneNo || "—"}
        </p>
      </div>
      <div class="mt-auto pt-3 border-t flex flex-col items-end text-xs">
        <span class="text-muted-foreground">Balance</span>
        <span
          class={cn(
            "font-bold text-sm",
            toNum(item.balance) > 0 ? "text-destructive" : "text-green-600",
          )}
        >
          ₹{fmt(toNum(item.balance))}
        </span>
      </div>
    </div>
  {/snippet}
</MasterList>
