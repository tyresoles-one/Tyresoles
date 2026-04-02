<script lang="ts" generics="T">
  import { tick, onMount } from "svelte";
  import { fade, fly } from "svelte/transition";
  import { quintOut } from "svelte/easing";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { Icon } from "$lib/components/venUI/icon";
  import {
    Collapsible,
    CollapsibleContent,
    CollapsibleTrigger,
  } from "$lib/components/ui/collapsible";
  import {
    Table,
    TableBody,
    TableHead,
    TableHeader,
    TableRow,
  } from "$lib/components/ui/table";
  import { Dropdown } from "$lib/components/venUI/dropdowns";
  import type { DropdownItem } from "$lib/components/venUI/dropdowns/types";

  interface Props {
    title: string;
    description?: string;
    items: T[];
    loading?: boolean;
    /** True when fetching next page (append). Keeps list visible and shows bottom loader instead of skeletons. */
    loadingMore?: boolean;
    error?: string | undefined;
    searchQuery?: string;
    viewMode?: "grid" | "table";
    // Snippets
    filters?: import("svelte").Snippet;
    actions?: import("svelte").Snippet; // For "Add" button etc
    emptyState?: import("svelte").Snippet;
    /** Rendered at top of main (e.g. section headings) before list / skeleton / error. */
    beforeList?: import("svelte").Snippet;
    gridItem: import("svelte").Snippet<[T]>;
    tableHeader: import("svelte").Snippet;
    tableRow: import("svelte").Snippet<[T]>;
    // Events
    onRefresh?: () => void;
    onSearchClear?: () => void;
    onLoadMore?: () => void;
    hasMore?: boolean;
    /** Total count from API (optional). When set, header shows "visible of total". */
    totalCount?: number;
    /** Placeholder for the main search field (e.g. directory-specific hint). */
    searchPlaceholder?: string;

    // Sorting
    sortOptions?: { label: string; value: string }[];
    currentSort?: string; // value of the currently selected sort option
    onSortChange?: (value: string) => void;
    /** When set, table rows are clickable (e.g. open detail / edit). */
    onRowClick?: (item: T) => void;
    /** When true, filters are wrapped in a collapsible panel (use with filters snippet). */
    filtersCollapsible?: boolean;
    /** When filtersCollapsible, controls expanded state (default open). */
    filtersOpen?: boolean; // bindable from parent when collapsible
    /** On large viewports, Arrow keys move focus between grid cards (requires onRowClick / focusable grid items). */
    gridKeyboardNav?: boolean;
  }

  let {
    title,
    description,
    items,
    loading = false,
    loadingMore = false,
    error = undefined,
    searchQuery = $bindable(""),
    viewMode = $bindable("grid"),
    filters,
    actions,
    emptyState,
    beforeList,
    gridItem,
    tableHeader,
    tableRow,
    onRefresh,
    onSearchClear,
    onLoadMore,
    hasMore,
    totalCount,
    searchPlaceholder = "Search...",
    sortOptions,
    currentSort = $bindable(""),
    onSortChange,
    onRowClick,
    filtersCollapsible = false,
    filtersOpen = $bindable(true),
    gridKeyboardNav = false,
  }: Props = $props();

  let gridNavEl: HTMLDivElement | null = $state(null);
  let searchInputRef: HTMLInputElement | null = $state(null);
  /** Tracks loading to focus first grid card when a fetch that started with no rows completes. */
  let prevLoading = $state<boolean | undefined>(undefined);
  let itemCountWhenFetchStarted = $state(0);

  onMount(() => {
    if (typeof window === "undefined") return;

    function onGlobalKeydown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "f") {
        const t = e.target;
        if (
          t instanceof Element &&
          (t.closest('[role="dialog"]') ||
            t.closest('[data-slot="dialog-content"]') ||
            t.closest('[data-slot="alert-dialog-content"]'))
        ) {
          return;
        }
        e.preventDefault();
        searchInputRef?.focus();
        searchInputRef?.select();
      }
    }

    window.addEventListener("keydown", onGlobalKeydown, true);
    return () => window.removeEventListener("keydown", onGlobalKeydown, true);
  });

  function gridColumnCount(grid: HTMLElement): number {
    const children = Array.from(grid.children) as HTMLElement[];
    if (children.length === 0) return 1;
    const top0 = children[0].offsetTop;
    let count = 0;
    for (const c of children) {
      if (c.offsetTop === top0) count++;
      else break;
    }
    return Math.max(1, count);
  }

  function focusablesInGrid(grid: HTMLElement): HTMLElement[] {
    const sel =
      'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    return Array.from(grid.querySelectorAll<HTMLElement>(sel)).filter(
      (el) => !el.closest("[data-grid-nav-ignore]"),
    );
  }

  function handleGridKeydown(e: KeyboardEvent) {
    if (!gridKeyboardNav || !onRowClick || !gridNavEl || viewMode !== "grid")
      return;
    const keys = [
      "ArrowDown",
      "ArrowUp",
      "ArrowLeft",
      "ArrowRight",
      "Home",
      "End",
    ];
    if (!keys.includes(e.key)) return;

    const focusable = focusablesInGrid(gridNavEl);
    if (focusable.length === 0) return;
    const active = document.activeElement as HTMLElement | null;
    let idx = active ? focusable.indexOf(active) : -1;
    if (idx < 0 && gridNavEl && active === gridNavEl) {
      if (
        e.key === "ArrowDown" ||
        e.key === "ArrowRight" ||
        e.key === "Home"
      ) {
        e.preventDefault();
        focusable[0]?.focus();
      } else if (e.key === "End") {
        e.preventDefault();
        focusable[focusable.length - 1]?.focus();
      }
      return;
    }
    if (idx < 0 && e.key !== "Home" && e.key !== "End") return;

    const cols = gridColumnCount(gridNavEl);
    const n = focusable.length;
    let next = idx;

    if (e.key === "Home") next = 0;
    else if (e.key === "End") next = n - 1;
    else if (e.key === "ArrowRight") next = Math.min(n - 1, idx + 1);
    else if (e.key === "ArrowLeft") next = Math.max(0, idx - 1);
    else if (e.key === "ArrowDown") next = Math.min(n - 1, idx + cols);
    else if (e.key === "ArrowUp") next = Math.max(0, idx - cols);

    if (next !== idx && next >= 0 && next < n) {
      e.preventDefault();
      focusable[next]?.focus();
    }
  }

  $effect(() => {
    const was = prevLoading;
    prevLoading = loading;
    if (was === undefined) {
      if (loading) itemCountWhenFetchStarted = items.length;
      return;
    }
    if (loading && !was) {
      itemCountWhenFetchStarted = items.length;
    }
    if (
      was &&
      !loading &&
      !loadingMore &&
      items.length > 0 &&
      itemCountWhenFetchStarted === 0 &&
      gridKeyboardNav &&
      onRowClick &&
      viewMode === "grid"
    ) {
      void tick().then(() => {
        const ae = document.activeElement;
        if (
          ae instanceof HTMLInputElement ||
          ae instanceof HTMLTextAreaElement ||
          ae instanceof HTMLSelectElement ||
          ae?.getAttribute?.("contenteditable") === "true"
        ) {
          return;
        }
        if (!gridNavEl) return;
        const f = focusablesInGrid(gridNavEl);
        f[0]?.focus();
      });
    }
  });

  // Internal State for Infinite Scroll
  const BATCH_SIZE = 20;

  /** Match sort value to option case-insensitively so radio state and callbacks stay consistent. */
  function canonicalSortValue(val: string): string {
    if (!sortOptions?.length) return val;
    const hit = sortOptions.find(
      (o) => o.value.toLowerCase() === val.trim().toLowerCase(),
    );
    return hit?.value ?? val;
  }

  let sortValueForUi = $derived(canonicalSortValue(currentSort));

  // Compute Dropdown Items for Sort
  let sortDropdownItems: DropdownItem[] = $derived(
    sortOptions
      ? [
          { type: "label", label: "Sort By" },
          { type: "separator" },
          {
            type: "radio-group",
            value: sortValueForUi,
            onValueChange: (val: string) => {
              const canonical = canonicalSortValue(val);
              currentSort = canonical;
              onSortChange?.(canonical);
            },
            options: sortOptions.map((o: { label: string; value: string }) => ({
              label: o.label,
              value: o.value,
            })),
          },
        ]
      : [],
  );
  let displayedCount = $state(BATCH_SIZE);
  let savedScrollY = $state<number | null>(null);
  let prevLoadingMore = $state(false);

  // Reset displayed count only in client-side mode when items change (server append must not trigger)
  $effect(() => {
    if (!onLoadMore) {
      items;
      displayedCount = BATCH_SIZE;
    }
  });

  // Restore scroll position after load-more completes (prevents "jump back to top")
  $effect(() => {
    const was = prevLoadingMore;
    prevLoadingMore = loadingMore;
    if (
      was &&
      !loadingMore &&
      savedScrollY != null &&
      typeof window !== "undefined"
    ) {
      const y = savedScrollY;
      savedScrollY = null;
      tick().then(() => {
        requestAnimationFrame(() => window.scrollTo(0, y));
      });
    }
  });

  // If onLoadMore is provided, we show ALL items passed to us (server paging),
  // otherwise we slice locally.
  let visibleItems = $derived(
    onLoadMore ? items : items.slice(0, displayedCount),
  );

  // Determine if we should show loader
  let showLoader = $derived(
    hasMore !== undefined ? hasMore : displayedCount < items.length,
  );

  // Show skeletons only on initial load, not when loading more (avoids flicker at bottom)
  let showSkeletons = $derived(loading && !loadingMore);
  let branch = $derived(
    showSkeletons
      ? "skeletons"
      : error
        ? "error"
        : items.length === 0
          ? "empty"
          : "list",
  );

  // Intersection Observer
  function intersectionObserver(node: HTMLElement) {
    const observer = new IntersectionObserver(
      (entries) => {
        if (!entries[0].isIntersecting) return;
        if (onLoadMore && hasMore && !loadingMore) {
          if (typeof window !== "undefined") savedScrollY = window.scrollY;
          onLoadMore();
        } else if (!onLoadMore && displayedCount < items.length) {
          displayedCount += BATCH_SIZE;
        }
      },
      { rootMargin: "200px" },
    );

    observer.observe(node);

    return {
      destroy() {
        observer.disconnect();
      },
    };
  }
</script>

<div class="min-h-screen w-full bg-background relative selection:bg-primary/20">
  <!-- Background Mesh (Optional: could be passed as slot or kept here if consistent across all master lists) -->
  <div class="fixed inset-0 -z-10 pointer-events-none overflow-hidden">
    <div
      class="absolute top-[-10%] right-[-5%] h-[600px] w-[600px] rounded-full bg-primary/5 blur-[120px] animate-pulse"
    ></div>
    <div
      class="absolute bottom-[-10%] left-[-5%] h-[500px] w-[500px] rounded-full bg-blue-500/5 blur-[100px] animate-pulse"
      style="animation-delay: 2s;"
    ></div>
  </div>

  <!-- Header -->
  <header
    class="sticky top-0 z-40 w-full border-b bg-background/80 backdrop-blur-xl transition-all duration-300"
  >
    <div class="container mx-auto px-4 sm:px-6 lg:px-8 py-4">
      {#if filtersCollapsible && filters}
        <Collapsible bind:open={filtersOpen}>
          <div
            class="flex flex-col gap-4 md:flex-row md:items-center md:justify-between"
          >
            <CollapsibleTrigger
              class="group flex min-w-0 flex-1 cursor-pointer items-center gap-3 rounded-xl border border-transparent px-0 py-1.5 text-left transition-colors hover:border-border/40 hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring sm:-ml-2 sm:px-2"
              aria-label="Show or hide filters"
            >
              <div
                class="hidden shrink-0 rounded-xl bg-primary/10 p-2 text-primary sm:block"
              >
                <Icon name="layout-grid" class="size-6" />
              </div>
              <div class="min-w-0">
                <h1
                  class="flex flex-wrap items-center gap-2 text-2xl font-bold tracking-tight text-foreground"
                >
                  {title}
                  <Icon
                    name="chevron-down"
                    class="size-5 shrink-0 text-muted-foreground transition-transform duration-200 {filtersOpen
                      ? 'rotate-180'
                      : ''}"
                    aria-hidden="true"
                  />
                  {#if !loading && !loadingMore}
                    <span
                      class="inline-flex items-center justify-center rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground animate-in fade-in zoom-in"
                      title={totalCount != null
                        ? `${items.length} loaded of ${totalCount} total`
                        : `${items.length} items`}
                    >
                      {totalCount != null
                        ? `${items.length} of ${totalCount}`
                        : items.length}
                    </span>
                  {/if}
                </h1>
                {#if description}
                  <p class="hidden text-sm text-muted-foreground sm:block">
                    {description}
                  </p>
                {/if}
              </div>
            </CollapsibleTrigger>

            <div
              class="flex w-full flex-wrap items-center gap-2 sm:gap-3 md:w-auto md:justify-end"
            >
              <div class="group relative flex-1 md:w-64 lg:w-80">
                <Icon
                  name="search"
                  class="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground transition-colors group-focus-within:text-primary"
                />
                <Input
                  bind:ref={searchInputRef}
                  id="master-list-search"
                  data-master-list-search
                  bind:value={searchQuery}
                  placeholder={searchPlaceholder}
                  class="pl-9 bg-muted/50 border-muted-foreground/20 focus-visible:bg-background transition-all"
                />
                {#if searchQuery}
                  <button
                    type="button"
                    onclick={() => {
                      searchQuery = "";
                      onSearchClear?.();
                    }}
                    class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                  >
                    <Icon name="x" class="size-3" />
                  </button>
                {/if}
              </div>

              {#if sortOptions}
                <Dropdown
                  items={sortDropdownItems}
                  align="end"
                  trigger={{
                    label: "Sort",
                    icon: "arrow-up-down",
                    iconOnly: true,
                    variant: "outline",
                    class:
                      "h-10 w-10 bg-muted/50 border-muted-foreground/10 hover:bg-background hover:text-primary transition-all",
                  }}
                />
              {/if}

              <div
                class="flex shrink-0 rounded-lg border border-muted-foreground/10 bg-muted/50 p-1"
              >
                <button
                  type="button"
                  onclick={() => (viewMode = "grid")}
                  class="rounded-md p-2 transition-all duration-200 {viewMode ===
                  'grid'
                    ? 'bg-background text-primary shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'}"
                  title="Grid View"
                >
                  <Icon name="layoutPanelLeft" class="size-4" />
                </button>
                <button
                  type="button"
                  onclick={() => (viewMode = "table")}
                  class="rounded-md p-2 transition-all duration-200 {viewMode ===
                  'table'
                    ? 'bg-background text-primary shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'}"
                  title="Table View"
                >
                  <Icon name="list" class="size-4" />
                </button>
              </div>

              {#if onRefresh}
                <Button
                  variant="outline"
                  size="icon"
                  onclick={onRefresh}
                  disabled={loading || loadingMore}
                  class="shrink-0 hover:border-primary/50 hover:bg-primary/5 transition-colors"
                >
                  <Icon
                    name="refresh-cw"
                    class="size-4 {loading || loadingMore ? 'animate-spin' : ''}"
                  />
                </Button>
              {/if}

              {#if actions}
                {@render actions()}
              {/if}
            </div>
          </div>

          <div
            class="overflow-x-auto scrollbar-hide transition-[padding] {filtersOpen
              ? 'mt-4 border-t border-border/40 pt-4'
              : ''}"
          >
            <CollapsibleContent class="overflow-hidden">
              {@render filters()}
            </CollapsibleContent>
          </div>
        </Collapsible>
      {:else}
        <div
          class="flex flex-col gap-4 md:flex-row md:items-center md:justify-between"
        >
          <div class="flex items-center gap-3">
            <div
              class="hidden rounded-xl bg-primary/10 p-2 text-primary sm:block"
            >
              <Icon name="layout-grid" class="size-6" />
            </div>
            <div>
              <h1
                class="flex items-center gap-2 text-2xl font-bold tracking-tight text-foreground"
              >
                {title}
                {#if !loading && !loadingMore}
                  <span
                    class="inline-flex items-center justify-center rounded-full bg-muted px-2.5 py-0.5 text-xs font-medium text-muted-foreground animate-in fade-in zoom-in"
                    title={totalCount != null
                      ? `${items.length} loaded of ${totalCount} total`
                      : `${items.length} items`}
                  >
                    {totalCount != null
                      ? `${items.length} of ${totalCount}`
                      : items.length}
                  </span>
                {/if}
              </h1>
              {#if description}
                <p class="hidden text-sm text-muted-foreground sm:block">
                  {description}
                </p>
              {/if}
            </div>
          </div>

          <div class="flex w-full items-center gap-2 sm:gap-3 md:w-auto">
            <div class="group relative flex-1 md:w-64 lg:w-80">
              <Icon
                name="search"
                class="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground transition-colors group-focus-within:text-primary"
              />
              <Input
                bind:ref={searchInputRef}
                id="master-list-search"
                data-master-list-search
                bind:value={searchQuery}
                placeholder={searchPlaceholder}
                class="pl-9 bg-muted/50 border-muted-foreground/20 focus-visible:bg-background transition-all"
              />
              {#if searchQuery}
                <button
                  type="button"
                  onclick={() => {
                    searchQuery = "";
                    onSearchClear?.();
                  }}
                  class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
                >
                  <Icon name="x" class="size-3" />
                </button>
              {/if}
            </div>

            {#if sortOptions}
              <Dropdown
                items={sortDropdownItems}
                align="end"
                trigger={{
                  label: "Sort",
                  icon: "arrow-up-down",
                  iconOnly: true,
                  variant: "outline",
                  class:
                    "h-10 w-10 bg-muted/50 border-muted-foreground/10 hover:bg-background hover:text-primary transition-all",
                }}
              />
            {/if}

            <div
              class="flex shrink-0 rounded-lg border border-muted-foreground/10 bg-muted/50 p-1"
            >
              <button
                type="button"
                onclick={() => (viewMode = "grid")}
                class="rounded-md p-2 transition-all duration-200 {viewMode ===
                'grid'
                  ? 'bg-background text-primary shadow-sm'
                  : 'text-muted-foreground hover:text-foreground'}"
                title="Grid View"
              >
                <Icon name="layoutPanelLeft" class="size-4" />
              </button>
              <button
                type="button"
                onclick={() => (viewMode = "table")}
                class="rounded-md p-2 transition-all duration-200 {viewMode ===
                'table'
                  ? 'bg-background text-primary shadow-sm'
                  : 'text-muted-foreground hover:text-foreground'}"
                title="Table View"
              >
                <Icon name="list" class="size-4" />
              </button>
            </div>

            {#if onRefresh}
              <Button
                variant="outline"
                size="icon"
                onclick={onRefresh}
                disabled={loading || loadingMore}
                class="shrink-0 hover:border-primary/50 hover:bg-primary/5 transition-colors"
              >
                <Icon
                  name="refresh-cw"
                  class="size-4 {loading || loadingMore ? 'animate-spin' : ''}"
                />
              </Button>
            {/if}
          </div>
        </div>

        {#if filters || actions}
          <div
            class="mt-4 overflow-x-auto border-t border-border/40 pt-4 pb-1 sm:pb-0 scrollbar-hide"
          >
            <div
              class="flex flex-wrap items-center justify-between gap-4 overflow-x-auto pb-1 sm:pb-0 scrollbar-hide"
            >
              <div class="flex min-w-0 flex-wrap items-center gap-2">
                {#if filters}
                  <div class="mr-2 flex items-center gap-1.5">
                    <Icon name="filter" class="size-3.5 text-muted-foreground" />
                    <span class="text-xs font-medium text-muted-foreground"
                      >Filters:</span
                    >
                  </div>
                  {@render filters()}
                {/if}
              </div>

              {#if actions}
                {@render actions()}
              {/if}
            </div>
          </div>
        {/if}
      {/if}

      {#if searchQuery && items.length !== visibleItems.length}
        <!-- Note: logic for 'items.length !== visibleItems.length' above was original logic for checking filtering; 
				     but here 'items' IS likely the filtered list passed from parent. 
					 We might want a prop 'totalItems' vs 'filteredItems' count if we want to show this message accurately relative to backend DB. 
					 For now, we assume parent handles filtering feedback if needed, but we can show a simple message if filtered list is empty.
				-->
      {/if}
    </div>
  </header>

  <main class="container mx-auto px-4 sm:px-6 lg:px-8 py-6 sm:py-8">
    {#if beforeList}
      {@render beforeList()}
    {/if}
    {#if showSkeletons}
      <div
        class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6"
      >
        {#each Array(8) as _}
          <div class="rounded-xl border bg-card p-6 shadow-sm space-y-4">
            <div class="flex items-center gap-4">
              <Skeleton class="size-12 rounded-full" />
              <div class="space-y-2 flex-1">
                <Skeleton class="h-4 w-3/4" />
                <Skeleton class="h-3 w-1/2" />
              </div>
            </div>
            <Skeleton class="h-20 w-full rounded-lg" />
            <div class="flex justify-between pt-2">
              <Skeleton class="h-8 w-20" />
              <Skeleton class="h-8 w-8 rounded-full" />
            </div>
          </div>
        {/each}
      </div>
    {:else if error}
      <div
        class="flex flex-col items-center justify-center py-20 text-center animate-in fade-in zoom-in duration-300"
      >
        <div
          class="size-20 rounded-full bg-destructive/10 flex items-center justify-center mb-6"
        >
          <Icon name="triangle-alert" class="size-10 text-destructive" />
        </div>
        <h2 class="text-2xl font-bold mb-2">Failed to load data</h2>
        <p class="text-muted-foreground max-w-md mb-8">{error}</p>
        {#if onRefresh}
          <Button onclick={onRefresh} size="lg" class="gap-2">
            <Icon name="refresh-cw" class="size-4" />
            Try Again
          </Button>
        {/if}
      </div>
    {:else if items.length === 0}
      {#if emptyState}
        {@render emptyState()}
      {:else}
        <div
          class="flex flex-col items-center justify-center py-20 text-center animate-in fade-in zoom-in duration-300"
        >
          <div
            class="size-24 rounded-full bg-muted flex items-center justify-center mb-6"
          >
            <Icon name="search-x" class="size-12 text-muted-foreground/50" />
          </div>
          <h2 class="text-xl font-semibold mb-2">No items found</h2>
          <p class="text-muted-foreground max-w-md mb-8">
            We couldn't find any items matching your criteria.
          </p>
          {#if searchQuery}
            <Button
              variant="outline"
              onclick={() => {
                searchQuery = "";
                onSearchClear?.();
              }}
              class="gap-2"
            >
              <Icon name="x" class="size-4" />
              Clear Search
            </Button>
          {/if}
        </div>
      {/if}
    {:else}
      {#if viewMode === "grid"}
        <div
          bind:this={gridNavEl}
          role="grid"
          aria-label={title}
          tabindex={gridKeyboardNav ? 0 : undefined}
          onkeydown={handleGridKeydown}
          class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 sm:gap-6 list-scroll-anchor-fix outline-none focus-visible:outline-none {gridKeyboardNav
            ? 'focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 ring-offset-background rounded-lg'
            : ''}"
        >
          {#each visibleItems as item, i}
            <div
              role="gridcell"
              in:fly={{
                y: 20,
                duration: 300,
                delay: Math.min(i * 30, 300),
                easing: quintOut,
              }}
              class="group relative min-h-0 min-w-0"
            >
              {@render gridItem(item)}
            </div>
          {/each}
        </div>
      {:else}
        <div
          in:fade={{ duration: 300 }}
          class="rounded-xl border bg-card/60 backdrop-blur-sm shadow-sm overflow-hidden list-scroll-anchor-fix"
        >
          <div class="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow class="hover:bg-transparent border-b-primary/10">
                  {@render tableHeader()}
                </TableRow>
              </TableHeader>
              <TableBody>
                {#each visibleItems as item}
                  <TableRow
                    class="group hover:bg-muted/30 transition-colors {onRowClick
                      ? 'cursor-pointer'
                      : ''}"
                    onclick={() => onRowClick?.(item)}
                    onkeydown={(e) => {
                      if (
                        !onRowClick ||
                        (e.key !== "Enter" && e.key !== " ")
                      )
                        return;
                      e.preventDefault();
                      onRowClick(item);
                    }}
                    role={onRowClick ? "button" : undefined}
                    tabindex={onRowClick ? 0 : undefined}
                  >
                    {@render tableRow(item)}
                  </TableRow>
                {/each}
              </TableBody>
            </Table>
          </div>
        </div>
      {/if}

      <!-- Infinite Scroll Sentinel -->
      {#if showLoader}
        <div
          use:intersectionObserver
          class="w-full h-20 flex items-center justify-center p-4"
        >
          <Icon
            name="loader-circle"
            class="size-6 animate-spin text-muted-foreground"
          />
        </div>
      {:else if items.length > 0}
        <div
          class="w-full h-20 flex items-center justify-center p-4 text-xs text-muted-foreground"
        >
          No more items to load
        </div>
      {/if}
    {/if}
  </main>
</div>

<style>
  .list-scroll-anchor-fix {
    overflow-anchor: none;
  }
  .overflow-x-auto::-webkit-scrollbar {
    height: 8px;
  }
  .overflow-x-auto::-webkit-scrollbar-track {
    background: transparent;
  }
  .overflow-x-auto::-webkit-scrollbar-thumb {
    background-color: hsl(var(--muted-foreground) / 0.2);
    border-radius: 20px;
  }
  .overflow-x-auto::-webkit-scrollbar-thumb:hover {
    background-color: hsl(var(--muted-foreground) / 0.4);
  }
</style>
