<script lang="ts">
	import { Icon } from "$lib/components/venUI/icon";
	import { Input } from "$lib/components/ui/input";
	import { Dropdown } from "$lib/components/venUI/dropdowns";
	import { Button } from "$lib/components/ui/button";
	import type { Table } from "@tanstack/table-core";

	let {
		title,
		description,
		table,
		searchQuery = $bindable(),
		viewMode = $bindable(),
		showViewToggle,
		showColumnToggle,
		showDensity,
		showFilters,
		loading,
		onFilterClick,
		actions
	}: { 
		title?: string, 
		description?: string, 
		table: Table<any>, 
		searchQuery: string, 
		viewMode: "grid"|"table",
		showViewToggle: boolean,
		showColumnToggle: boolean,
		showDensity: boolean,
		showFilters?: boolean,
		loading: boolean,
		onFilterClick?: () => void,
		actions?: import("svelte").Snippet
	} = $props();

	// Create dropdown items for Column Visibility
	const columnVisibilityItems = $derived(() => {
		const items: any[] = [{ type: 'label', label: 'Toggle Columns' }, { type: 'separator' }];
		
		table.getAllLeafColumns().forEach(col => {
			if (col.getCanHide()) {
				items.push({
					type: 'checkbox',
					label: typeof col.columnDef.header === 'string' ? col.columnDef.header : col.id,
					checked: col.getIsVisible(),
					onCheckedChange: (checked: boolean) => col.toggleVisibility(checked)
				});
			}
		});

		return items;
	});

</script>

<div class="flex flex-col md:flex-row items-center justify-between gap-4 p-4 border-b bg-muted/20 rounded-t-xl">
	<!-- Left Side: Title & Search -->
	<div class="flex flex-col sm:flex-row items-start sm:items-center w-full md:w-auto gap-4 flex-1">
		{#if title}
			<div class="mr-4">
				<h2 class="text-lg font-semibold tracking-tight leading-none mb-1">{title}</h2>
				{#if description}
					<p class="text-xs text-muted-foreground">{description}</p>
				{/if}
			</div>
		{/if}

		<div class="relative w-full sm:max-w-xs md:max-w-md ml-auto sm:ml-0 group">
			<Icon name="search" class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground group-focus-within:text-primary transition-colors" />
			<Input
				type="text"
				placeholder="Filter items..."
				bind:value={searchQuery}
				class="pl-9 bg-background focus:ring-1 transition-shadow"
			/>
			{#if loading}
				<div class="absolute right-3 top-1/2 -translate-y-1/2 h-full flex items-center">
					<Icon name="loader-2" class="size-3 text-muted-foreground animate-spin" />
				</div>
			{/if}
		</div>
	</div>

	<!-- Right Side: Actions & Settings -->
	<div class="flex items-center w-full md:w-auto justify-end gap-2 shrinks-0">
		{#if actions}
			{@render actions()}
		{/if}

		<div class="w-px h-6 bg-border mx-1"></div>

		<!-- View Toggle -->
		{#if showViewToggle}
			<div class="flex items-center shrink-0 rounded-md border bg-background p-1 space-x-1">
				<button 
					type="button" 
					onclick={() => viewMode = 'table'}
					class="p-1 rounded text-muted-foreground hover:text-foreground data-[active=true]:bg-muted data-[active=true]:text-primary transition-colors"
					data-active={viewMode === 'table'}
					title="Table View"
				>
					<Icon name="list" class="size-4" />
				</button>
				<button 
					type="button" 
					onclick={() => viewMode = 'grid'}
					class="p-1 rounded text-muted-foreground hover:text-foreground data-[active=true]:bg-muted data-[active=true]:text-primary transition-colors"
					data-active={viewMode === 'grid'}
					title="Grid View"
				>
					<Icon name="layout-grid" class="size-4" />
				</button>
			</div>
		{/if}

		<!-- Filters Toggle -->
		{#if showFilters}
			<Button 
				variant="outline" 
				size="sm" 
				class="h-9 w-9 px-0 py-0 bg-background border-border shrink-0 text-muted-foreground hover:text-foreground"
				onclick={onFilterClick}
				title="Filters"
			>
				<Icon name="filter" class="size-4" />
			</Button>
		{/if}

		<!-- Column Toggle -->
		{#if showColumnToggle}
			<!-- Function evaluating at runtime due to reactivity -->
			<Dropdown 
				items={columnVisibilityItems()} 
				trigger={{
					iconOnly: true,
					variant: "outline",
					icon: "columns-3-cog",
					label:"",					
					class: "h-9 w-9 bg-background px-0 py-0 flex items-center justify-center shrink-0 border-border"
				}}
				align="end"
			/>
		{/if}
	</div>
</div>
