<script lang="ts">
	import * as Table from "$lib/components/ui/table";
	import { Button } from "$lib/components/ui/button";
	import { Icon } from "$lib/components/venUI/icon";
	import { SkeletonGrid } from "$lib/components/venUI/skeletonGrid";
	import { cn } from "$lib/utils";
	import type { TableColumn, ButtonProps } from "../custom/types";

	type Props = {
		data: any[];
		columns: TableColumn[];
		actions?: ButtonProps[];
		loading?: boolean;
		footer?: boolean;
		onRowClick?: (row: any) => void;
		enableSelection?: boolean;
		selectionType?: "single" | "multiple";
		dataKey?: string;
		onSelectionChange?: (selection: Map<string, any>) => void;
		selectedValues?: Set<string>;
		/** When true (default), clicking a row toggles selection like the checkbox. */
		selectOnRowClick?: boolean;
		/** Show "select all" in header for multiple selection (default true). */
		showSelectAll?: boolean;
	};

	let {
		data = [],
		columns = [],
		actions = [],
		loading = false,
		footer = false,
		onRowClick,
		enableSelection = false,
		selectionType = "single",
		dataKey = "id",
		onSelectionChange,
		selectedValues = $bindable(new Set()),
		selectOnRowClick = true,
		showSelectAll = true
	}: Props = $props();

	function rowKey(row: any) {
		return dataKey
			.split(",")
			.map((k) => row[k.trim()])
			.join(",");
	}

	function emitSelection(next: Set<string>) {
		selectedValues = next;
		const selectionMap = new Map<string, any>();
		data
			.filter((d) => {
				const dKey = rowKey(d);
				return next.has(dKey);
			})
			.forEach((d) => {
				const dKey = rowKey(d);
				selectionMap.set(dKey, d);
			});
		onSelectionChange?.(selectionMap);
	}

	function handleSelection(row: any) {
		if (!enableSelection) return;
		const key = rowKey(row);
		const next = new Set(selectedValues);
		if (next.has(key)) {
			next.delete(key);
		} else {
			if (selectionType === "single") next.clear();
			next.add(key);
		}
		emitSelection(next);
	}

	function toggleSelectAll() {
		if (!enableSelection || selectionType !== "multiple") return;
		const keys = data.map((row) => rowKey(row));
		const allSelected = keys.length > 0 && keys.every((k) => selectedValues.has(k));
		const next = new Set(selectedValues);
		if (allSelected) {
			for (const k of keys) next.delete(k);
		} else {
			for (const k of keys) next.add(k);
		}
		emitSelection(next);
	}

	let selectAllInputRef: HTMLInputElement | undefined = $state();
	let showSelectAllHeader = $derived(
		enableSelection && selectionType === "multiple" && showSelectAll
	);

	$effect(() => {
		const el = selectAllInputRef;
		if (!el || !showSelectAllHeader) return;
		const keys = data.map((row) => rowKey(row));
		const n = keys.filter((k) => selectedValues.has(k)).length;
		el.indeterminate = n > 0 && n < keys.length;
		el.checked = keys.length > 0 && n === keys.length;
	});

	function isSelected(row: any) {
		return selectedValues.has(rowKey(row));
	}
</script>

<div class="space-y-4">
	{#if actions.length}
		<div class="flex flex-wrap gap-2">
			{#each actions as action}
				<Button
					variant={action.variant || "outline"}
					size="sm"
					class={cn("gap-2", action.class)}
					onclick={action.onclick}
					disabled={loading || action.disabled}
					loading={action.loading}
				>
					{#if action.icon}
						<Icon name={action.icon as any} class="size-4" />
					{/if}
					{action.label}
				</Button>
			{/each}
		</div>
	{/if}

	<div class="rounded-md border bg-card">
		{#if loading}
			<SkeletonGrid count={5} columns={columns.length} />
		{:else}
			<Table.Root>
				<Table.Header>
					<Table.Row>
						{#if enableSelection}
							<Table.Head class="w-[40px]">
								{#if showSelectAllHeader}
									<input
										bind:this={selectAllInputRef}
										type="checkbox"
										class="size-4 accent-primary"
										title="Select all"
										aria-label="Select all rows"
										onclick={(e) => {
											e.stopPropagation();
											toggleSelectAll();
										}}
									/>
								{/if}
							</Table.Head>
						{/if}
						{#each columns as col}
							<Table.Head class={cn(col.textAlign === "right" && "text-right")}>
								{col.label}
							</Table.Head>
						{/each}
					</Table.Row>
				</Table.Header>
				<Table.Body>
					{#each data as row}
						<Table.Row
							class={cn(
								((enableSelection && selectOnRowClick) || onRowClick) && "cursor-pointer",
								isSelected(row) && "bg-muted/50"
							)}
							onclick={() => {
								if (enableSelection && selectOnRowClick) {
									handleSelection(row);
								}
								onRowClick?.(row);
							}}
						>
							{#if enableSelection}
								<Table.Cell>
									<input
										type={selectionType === "multiple" ? "checkbox" : "radio"}
										checked={isSelected(row)}
										onclick={(e) => {
											e.stopPropagation();
											handleSelection(row);
										}}
									/>
								</Table.Cell>
							{/if}
							{#each columns as col}
								<Table.Cell class={cn(col.textAlign === "right" && "text-right")}>
									{row[col.name] ?? ""}
								</Table.Cell>
							{/each}
						</Table.Row>
					{:else}
						<Table.Row>
							<Table.Cell colspan={columns.length + (enableSelection ? 1 : 0)} class="h-24 text-center">
								No results.
							</Table.Cell>
						</Table.Row>
					{/each}
				</Table.Body>
			</Table.Root>
		{/if}
	</div>
</div>
