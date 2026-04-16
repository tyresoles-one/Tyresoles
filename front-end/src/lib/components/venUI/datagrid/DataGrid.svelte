<script lang="ts" generics="TData extends any">
	import { 
		createTable, 
		getCoreRowModel,
		getSortedRowModel,
		type TableState, 
		type Updater 
	} from "@tanstack/table-core";
	import type { DataGridProps } from "./types";
	import { Icon } from "$lib/components/venUI/icon";
	import { Input } from "$lib/components/ui/input";
	import { cn } from "$lib/utils";
	import DataTable from "./DataTable.svelte";
	import DataGridToolbar from "./DataGridToolbar.svelte";
	import DataCardGrid from "./DataCardGrid.svelte";
	import PaginationBar from "./PaginationBar.svelte";
	import DataGridFilters from "./DataGridFilters.svelte";

	let {
		title = "Data Grid",
		description,
		items = [],
		columns = [],
		pagination,
		loading = false,
		loadingMore = false,
		searchQuery = $bindable(""),
		showDensity = true,
		showViewToggle = true,
		showColumnToggle = true,
		mobileCardFallback = true,
		mobileCardTitleKey,
		mobileCardSubtitleKey,
		onRowClick,
		showFilters = false,
		filterRules = $bindable([]),
		onFilterRulesChange,
		actions
	}: DataGridProps<TData> = $props();

	let viewMode = $state<"grid" | "table">("table");
	let filterSheetOpen = $state(false);

	// Default internal table state mapped to svelte reactivity
	let tableState = $state<Partial<TableState>>({
		sorting: [],
		columnVisibility: {},
		columnOrder: [],
		columnPinning: {}
	});

	// Connect our local state updates to the headless table logic
	function createUpdater<K extends keyof TableState>(key: K) {
		return (updater: Updater<any>) => {
			if (typeof updater === 'function') {
				tableState[key] = updater(tableState[key]);
			} else {
				tableState[key] = updater;
			}

			// Hook sorting changes into GraphQL Backend IF SmartPagination provided
			if (key === 'sorting' && pagination) {
				const sortState = tableState.sorting;
				if (sortState && sortState.length > 0) {
					// We pass standard `sort` variable for graphql: [{ field: "DESC" }]
					// Adjust mapping here based on backend graph expectations.
					const sortMap = sortState.map(s => ({
						[s.id]: s.desc ? "DESC" : "ASC"
					}));
					pagination.setVariables({ order: sortMap });
				} else {
					pagination.setVariables({ order: null });
				}
			}
		};
	}

	const baseTableOptions = {
		onSortingChange: createUpdater("sorting"),
		onColumnVisibilityChange: createUpdater("columnVisibility"),
		onColumnOrderChange: createUpdater("columnOrder"),
		onColumnPinningChange: createUpdater("columnPinning"),
		getCoreRowModel: getCoreRowModel(),
		getSortedRowModel: getSortedRowModel(),
		onStateChange() {},
		renderFallbackValue: ''
	};

	const table = createTable({
		...baseTableOptions,
		get data() { return items; },
		get columns() { return columns; },
		get state() { return tableState as TableState; }
	});

	$effect(() => {
		table.setOptions({
			...baseTableOptions,
			data: items,
			columns,
			state: tableState as TableState
		});
	});
</script>

<div class="flex flex-col w-full h-full bg-background rounded-xl">
	<DataGridToolbar
		{title}
		{description}
		{table}
		bind:searchQuery
		bind:viewMode
		{showViewToggle}
		{showColumnToggle}
		{showDensity}
		{showFilters}
		onFilterClick={() => filterSheetOpen = true}
		{loading}
		{actions}
	/>

	<div class="flex-1 w-full min-h-0 bg-card rounded-b-xl border border-t-0 p-4 relative">
		{#if loading && !loadingMore && items.length === 0}
			<div class="flex items-center justify-center p-12 h-[200px]">
				<Icon name="loader-2" class="size-8 animate-spin text-primary opacity-50" />
			</div>
		{:else if items.length === 0}
			<div class="flex flex-col items-center justify-center p-12 h-[200px] text-muted-foreground">
				<Icon name="inbox" class="size-10 mb-4 opacity-30" />
				<h3 class="text-sm font-semibold">No results found</h3>
				<p class="text-xs">There are no items matching your criteria.</p>
			</div>
		{:else}
			{#if viewMode === "table"}
				<DataTable 
					{table}
					{items}
					{loadingMore} 
					{onRowClick} 
				/>
			{:else}
				<DataCardGrid 
					{table}
					{items}
					{onRowClick} 
					titleKey={mobileCardTitleKey as string} 
					subtitleKey={mobileCardSubtitleKey as string} 
				/>
			{/if}
		{/if}
	</div>

	<!-- Pagination Bar matches existing structure bottom line -->
	{#if pagination}
		<PaginationBar {pagination} />
	{/if}

	{#if showFilters}
		<DataGridFilters
			bind:open={filterSheetOpen}
			{table}
			bind:rules={filterRules}
			onApply={(rules) => {
				if (onFilterRulesChange) onFilterRulesChange(rules);
			}}
		/>
	{/if}
</div>
