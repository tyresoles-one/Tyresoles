<script lang="ts">
	import { Icon } from "$lib/components/venUI/icon";
	import { Button } from "$lib/components/ui/button";
	import type { SmartPagination } from "$lib/utils/pagination/store.svelte";

	let { pagination }: { pagination: SmartPagination<any> } = $props();

	let itemsCountText = $derived(() => {
		if (pagination.totalCount !== undefined && pagination.totalCount > 0) {
			return `${pagination.items.length} loaded of ${pagination.totalCount} total`;
		}
		return `${pagination.items.length} items loaded`;
	});

</script>

<div class="flex items-center justify-between p-4 border-t bg-card mt-auto rounded-b-xl">
	<span class="text-xs text-muted-foreground mr-4">
		{itemsCountText()}
	</span>

	<div class="flex items-center gap-2 ml-auto">
		<!-- Note: SmartPagination typically follows Infinite pagination (nextPage appending rows) -->
		<!-- Or standard offset pagination. If it supports page sizes etc, we render them here -->
		<Button 
			variant="outline" 
			size="sm" 
			class="h-8 shadow-sm transition-all text-xs border-dashed"
			onclick={() => {
				pagination.reset();
				pagination.load();
			}}
			disabled={pagination.loading}
		>
			<Icon name="refresh-cw" class="size-3 mr-1 {pagination.loading ? 'animate-spin' : ''}" />
			Refresh
		</Button>
		
		<Button 
			variant="secondary" 
			size="sm" 
			class="h-8 transition-all hover:bg-primary hover:text-primary-foreground gap-2"
			onclick={() => pagination.nextPage()}
			disabled={!pagination.hasMore || pagination.loadingMore}
		>
			{#if pagination.loadingMore}
				<Icon name="loader-2" class="size-3 animate-spin"/>
			{/if}
			Load More
			<Icon name="chevron-down" class="size-3" />
		</Button>
	</div>
</div>
