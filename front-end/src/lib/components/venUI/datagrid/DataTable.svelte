<script lang="ts">
	import type { Column, Header, Table } from "@tanstack/table-core";
	import { Icon } from "$lib/components/venUI/icon";

	let { 
		table,
		items,
		loadingMore, 
		onRowClick 
	}: { 
		table: Table<any>,
		/** Current page rows from the parent list (SmartPagination, etc.). */
		items: any[],
		loadingMore: boolean, 
		onRowClick?: (item: any) => void 
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

	function flexRender(cellRenderValue: any, props: any): string {
		if (!cellRenderValue) return "";
		if (typeof cellRenderValue === 'function') {
			try {
				const res = cellRenderValue(props);
				// Check if it's a Svelte snippet or primitive
				if (typeof res === 'object' && res.render) {
					// Handling raw snippets returned might require specific rendering approaches in Svelte 5.
					// For string based representations:
					return ""; 
				}
				return String(res);
			} catch (e) {
				return "";
			}
		}
		return typeof cellRenderValue !== 'object' ? String(cellRenderValue) : "";
	}

	type ColumnMetaAlign = { align?: 'left' | 'right' | 'center' };

	function getColumnMeta(col: Column<any>): ColumnMetaAlign {
		return (col.columnDef.meta as ColumnMetaAlign) ?? {};
	}

	function headerCellClass(header: Header<any, unknown>): string {
		const meta = getColumnMeta(header.column);
		const base =
			'px-4 py-3 font-semibold text-xs tracking-wider text-muted-foreground uppercase whitespace-nowrap group';
		const sort = header.column.getCanSort()
			? 'cursor-pointer hover:text-foreground transition-colors'
			: '';
		const align = meta.align === 'right' ? 'text-right' : '';
		return [base, sort, align].filter(Boolean).join(' ');
	}

	function headerInnerClass(header: Header<any, unknown>): string {
		const meta = getColumnMeta(header.column);
		return meta.align === 'right'
			? 'flex items-center gap-2 justify-end w-full'
			: 'flex items-center gap-2';
	}

	function bodyCellClass(col: Column<any>): string {
		const meta = getColumnMeta(col);
		const base = 'px-4 py-3 align-middle';
		if (col.id === 'actions') return `${base} text-right`;
		if (meta.align === 'right') return `${base} text-right tabular-nums`;
		return base;
	}

	function renderBodyCell(col: Column<any>, original: any, rowIndex: number): string {
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

<div class="rounded-lg overflow-hidden border bg-background relative w-full h-full text-sm">
	<div class="overflow-auto w-full max-h-full">
		<table class="w-full text-left border-collapse">
			<thead class="bg-muted border-b sticky top-0 z-10 shadow-sm">
				{#each table.getHeaderGroups() as headerGroup}
					<tr>
						{#each headerGroup.headers as header}
							<th 
								class={headerCellClass(header)}
								onclick={header.column.getToggleSortingHandler()}
								colspan={header.colSpan}
							>
								<div class={headerInnerClass(header)}>
									{#if !header.isPlaceholder}
										<span>{flexRender(header.column.columnDef.header, header.getContext())}</span>
										
										<!-- Sort Icon -->
										{#if header.column.getCanSort()}
											<div class="inline-flex items-center opacity-0 group-hover:opacity-50 {header.column.getIsSorted() ? '!opacity-100' : ''} transition-opacity">
												{#if header.column.getIsSorted() === 'asc'}
													<Icon name="arrow-up" class="size-3 text-primary" />
												{:else if header.column.getIsSorted() === 'desc'}
													<Icon name="arrow-down" class="size-3 text-primary" />
												{:else}
													<Icon name="arrow-up-down" class="size-3" />
												{/if}
											</div>
										{/if}
									{/if}
								</div>
							</th>
						{/each}
					</tr>
				{/each}
			</thead>
			<tbody class="divide-y relative">
				{#each items as original, rowIndex}
					<tr 
						class="group hover:bg-muted/40 transition-colors bg-background {onRowClick ? 'cursor-pointer' : ''}"
						onclick={() => onRowClick?.(original)}
					>
						{#each table.getVisibleLeafColumns() as col}
							<td class={bodyCellClass(col)}>
								{renderBodyCell(col, original, rowIndex)}
							</td>
						{/each}
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
	
	{#if loadingMore}
		<div class="absolute bottom-0 left-0 right-0 p-2 bg-gradient-to-t from-background to-transparent pointer-events-none flex justify-center">
			<span class="inline-flex items-center gap-2 px-3 py-1 bg-muted/80 backdrop-blur rounded-full text-xs font-medium text-muted-foreground border shadow-sm">
				<Icon name="loader-2" class="size-3 animate-spin"/> Loading more...
			</span>
		</div>
	{/if}
</div>
