<script lang="ts">
	import type { Column, Table } from "@tanstack/table-core";
	import DetailItem from "$lib/components/venUI/detail-item/DetailItem.svelte";
	import { Icon } from "$lib/components/venUI/icon";

	let { 
		table,
		items,
		onRowClick,
		titleKey,
		subtitleKey
	}: { 
		table: Table<any>,
		items: any[],
		onRowClick?: (item: any) => void,
		titleKey?: string,
		subtitleKey?: string
	} = $props();

	function getPlainCellValue(col: Column<any>, original: any): unknown {
		const def = col.columnDef;
		if ("accessorFn" in def && typeof def.accessorFn === "function") {
			try {
				return def.accessorFn(original, 0);
			} catch {
				return undefined;
			}
		}
		if ("accessorKey" in def && def.accessorKey) {
			return (original as Record<string, unknown>)[def.accessorKey as string];
		}
		return undefined;
	}

	function renderCardCell(col: Column<any>, original: any, rowIndex: number): string {
		const def = col.columnDef;
		const v = getPlainCellValue(col, original);
		if (typeof def.cell === "function") {
			try {
				const out = def.cell({
					column: col,
					row: { original, index: rowIndex, id: String(rowIndex) },
					getValue: () => v,
					cell: { getValue: () => v },
					renderValue: () => v
				} as any);
				if (typeof out === "object" && out?.render) return "";
				return out == null ? "" : String(out);
			} catch {
				return v == null ? "" : String(v);
			}
		}
		return v == null ? "" : String(v);
	}
</script>

<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
	{#each items as original, rowIndex}
		<div 
			role="button"
			tabindex="0"
			class="flex flex-col group p-4 rounded-xl border bg-card transition-all {onRowClick ? 'cursor-pointer hover:border-primary/50 hover:shadow-md' : ''}"
			onclick={() => onRowClick?.(original)}
			onkeydown={(e) => {
				if (onRowClick && (e.key === "Enter" || e.key === " ")) {
					onRowClick(original);
				}
			}}
		>
			{#if titleKey || subtitleKey}
				<div class="flex items-center gap-3 mb-4 pb-3 border-b border-border/50">
					<div class="flex size-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
						<Icon name="layout-list" class="size-5" />
					</div>
					<div class="flex flex-col min-w-0 flex-1">
						{#if titleKey}
							<div class="font-semibold text-foreground truncate">{original[titleKey] || '—'}</div>
						{/if}
						{#if subtitleKey}
							<code class="text-[10px] font-mono text-muted-foreground truncate">{original[subtitleKey] || ''}</code>
						{/if}
					</div>
				</div>
			{/if}
			
			<div class="flex flex-col gap-2 flex-1">
				{#each table.getVisibleLeafColumns() as col}
					{#if col.id !== titleKey && col.id !== subtitleKey}
						<DetailItem 
							label={typeof col.columnDef.header === 'string' ? col.columnDef.header : col.id}
						>
							<span class="text-xs sm:text-sm">{renderCardCell(col, original, rowIndex) || '—'}</span>
						</DetailItem>
					{/if}
				{/each}
			</div>
		</div>
	{/each}
</div>
