<script lang="ts">
	import { Icon } from "$lib/components/venUI/icon";
	import { Button } from "$lib/components/ui/button";
	import { Input } from "$lib/components/ui/input";
	import * as Sheet from "$lib/components/ui/sheet";
	import * as Select from "$lib/components/ui/select";
	import type { Table } from "@tanstack/table-core";
	import type { FilterRule, FilterOperator } from "./types";

	let {
		open = $bindable(false),
		table,
		rules = $bindable([]),
		onApply
	}: {
		open: boolean;
		table: Table<any>;
		rules: FilterRule[];
		onApply?: (rules: FilterRule[]) => void;
	} = $props();

	// Derived list of filterable columns
	let columns = $derived(
		table.getAllLeafColumns()
			.filter(col => col.getCanFilter() !== false && col.id !== 'actions')
			.map(col => ({
				value: col.id,
				label: typeof col.columnDef.header === 'string' ? col.columnDef.header : col.id
			}))
	);

	const OPERATORS: { value: FilterOperator; label: string }[] = [
		{ value: 'contains', label: 'Contains' },
		{ value: 'eq', label: 'Equals' },
		{ value: 'neq', label: 'Not equals' },
		{ value: 'startsWith', label: 'Starts with' },
		{ value: 'endsWith', label: 'Ends with' },
		{ value: 'gt', label: 'Greater than' },
		{ value: 'gte', label: 'Greater than or eq' },
		{ value: 'lt', label: 'Less than' },
		{ value: 'lte', label: 'Less than or eq' },
	];

	function addRule() {
		const newRule: FilterRule = {
			id: crypto.randomUUID(),
			columnId: columns[0]?.value || '',
			operator: 'contains',
			value: ''
		};
		rules = [...rules, newRule];
	}

	function removeRule(id: string) {
		rules = rules.filter(r => r.id !== id);
	}

	function clearFilters() {
		rules = [];
		onApply?.([]);
		open = false;
	}

	function handleApply() {
		// Apply before closing — closing the sheet can sync props and make `rules` stale for this tick.
		onApply?.(rules);
		open = false;
	}
</script>

<Sheet.Root bind:open>
	<Sheet.Content side="right" class="w-full sm:max-w-md p-0 flex flex-col gap-0 border-l border-border bg-background sm:rounded-l-2xl shadow-2xl">
		<Sheet.Header class="p-6 border-b bg-muted/20 sm:text-left text-center">
			<Sheet.Title class="text-xl font-bold tracking-tight">Filters</Sheet.Title>
			<Sheet.Description class="text-sm">
				Create generic multi-condition filter logic for this datagrid.
			</Sheet.Description>
		</Sheet.Header>

		<div class="flex-1 overflow-y-auto p-6 flex flex-col gap-5">
			{#if rules.length === 0}
				<div class="flex flex-col items-center justify-center h-48 border-2 border-dashed border-muted rounded-xl bg-muted/10 text-muted-foreground p-6 text-center">
					<Icon name="filter" class="size-10 mb-3 opacity-20" />
					<p class="text-sm font-medium">No active filters</p>
					<p class="text-xs mb-4">Add a rule to easily filter data.</p>
					<Button variant="outline" size="sm" class="gap-2" onclick={addRule}>
						<Icon name="plus" class="size-3.5" />
						Add First Rule
					</Button>
				</div>
			{:else}
				<div class="flex items-center justify-between">
					<h3 class="text-sm font-semibold tracking-tight text-foreground/80 uppercase">Active Rules</h3>
					<Button variant="ghost" size="sm" class="h-8 text-xs text-destructive hover:bg-destructive/10 hover:text-destructive gap-1 px-2" onclick={clearFilters}>
						<Icon name="trash" class="size-3" />
						Clear All
					</Button>
				</div>
				
				<div class="space-y-4">
					{#each rules as rule (rule.id)}
						<div class="group relative flex flex-col gap-3 p-4 bg-muted/30 border rounded-xl transition-all hover:bg-muted/40 hover:shadow-sm">
							<button 
								class="absolute -right-2 -top-2 bg-background border text-muted-foreground hover:text-destructive hover:border-destructive hover:bg-destructive/10 rounded-full p-1 opacity-0 group-hover:opacity-100 transition-all shadow-sm focus:opacity-100"
								onclick={() => removeRule(rule.id)}
								title="Remove rule"
							>
								<Icon name="x" class="size-3.5" />
							</button>
							
							<!-- Desktop / Tablet Grid Row -->
							<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
								<Select.Root 
									type="single" 
									name="column-{rule.id}" 
									bind:value={rule.columnId}
								>
									<Select.Trigger class="w-full bg-background">
										{columns.find(c => c.value === rule.columnId)?.label || "Select Column"}
									</Select.Trigger>
									<Select.Content>
										{#each columns as col}
											<Select.Item value={col.value} label={col.label}>{col.label}</Select.Item>
										{/each}
									</Select.Content>
								</Select.Root>
								
								<Select.Root 
									type="single" 
									name="operator-{rule.id}" 
									bind:value={rule.operator}
								>
									<Select.Trigger class="w-full bg-background">
										{OPERATORS.find(o => o.value === rule.operator)?.label || "Select Operator"}
									</Select.Trigger>
									<Select.Content>
										{#each OPERATORS as op}
											<Select.Item value={op.value} label={op.label}>{op.label}</Select.Item>
										{/each}
									</Select.Content>
								</Select.Root>
							</div>
							
							<div class="w-full relative">
								<Input 
									type="text" 
									placeholder="Value..." 
									bind:value={rule.value} 
									class="w-full bg-background border-border/60 focus-visible:ring-primary shadow-xs"
								/>
							</div>
						</div>
					{/each}
				</div>
				
				<Button variant="outline" class="w-full gap-2 border-dashed border-2 bg-transparent hover:bg-muted/30 py-6" onclick={addRule}>
					<Icon name="plus" class="size-4" />
					<span class="font-medium">Add New Rule</span>
				</Button>
			{/if}
		</div>

		<Sheet.Footer class="p-6 border-t bg-muted/10 items-center justify-between gap-y-3 flex-row sm:flex-row shadow-[0_-4px_10px_rgba(0,0,0,0.02)]">
			<span class="text-xs text-muted-foreground hidden sm:inline-block">
				{rules.length} active rule{rules.length !== 1 ? 's' : ''}
			</span>
			<div class="flex gap-3 w-full sm:w-auto">
				<Button variant="outline" class="flex-1 sm:flex-none" onclick={() => open = false}>Cancel</Button>
				<Button class="flex-1 sm:flex-none gap-2 shadow-md hover:shadow-lg transition-shadow bg-primary text-primary-foreground" onclick={handleApply}>
					<Icon name="check" class="size-4" />
					Apply Filters
				</Button>
			</div>
		</Sheet.Footer>
	</Sheet.Content>
</Sheet.Root>
