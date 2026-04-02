<script lang="ts">
	import { goto } from '$app/navigation';

	// Reusable composables
	import { usePaginatedList } from '$lib/composables';

	// Reusable components
	import { EntityCard } from '$lib/components/venUI/entityCard';
	import { TableActions } from '$lib/components/venUI/tableActions';

	// UI
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { TableCell, TableHead } from '$lib/components/ui/table';
	import MasterList from '$lib/components/venUI/masterList/MasterList.svelte';

	// GraphQL
	import { GetMyVendorsDocument } from '$lib/services/graphql/generated/types';
	import type { GetMyVendorsQuery } from '$lib/services/graphql/generated/types';

	type Vendor = NonNullable<GetMyVendorsQuery['myVendors']>['items'][number];
	type ViewMode = 'grid' | 'table';

	let viewMode = $state<ViewMode>('grid');

	const list = usePaginatedList<Vendor>({
		query: GetMyVendorsDocument,
		dataPath: 'myVendors',
		pageSize: 50
	});
</script>

<div class="min-h-screen bg-background pb-20">
	<MasterList
		title="Vendors"
		description="Manage your preferred vendors and partners"
		items={list.items}
		totalCount={list.totalCount}
		bind:searchQuery={list.searchQuery.value}
		bind:viewMode
		loading={list.loading}
		loadingMore={list.loadingMore}
		error={list.error}
		hasMore={list.hasMore}
		onLoadMore={list.onLoadMore}
		onRefresh={list.onRefresh}
	>
		{#snippet filters()}
			<div class="hidden sm:flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20">
				<span class="px-2.5 py-1 text-xs font-medium text-muted-foreground">All Categories</span>
			</div>
			<div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>
		{/snippet}

		{#snippet actions()}
			<Button
				size="sm"
				class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
			>
				<Icon name="plus" class="size-3.5" />
				<span class="hidden sm:inline">Add Vendor</span>
				<span class="sm:hidden">Add</span>
			</Button>
		{/snippet}

		{#snippet gridItem(vendor: Vendor)}
			<EntityCard
				icon="truck"
				title={vendor.name || '—'}
				subtitle={vendor.no}
				metadata={[
					...(vendor.city?.trim() ? [{ icon: 'map-pin', label: 'City', value: vendor.city }] : []),
					...(vendor.groupCategory?.trim() ? [{ icon: 'tag', label: 'Category', value: vendor.groupCategory }] : []),
					...(vendor.phoneNo?.trim() ? [{ icon: 'phone', label: 'Phone', value: vendor.phoneNo }] : []),
					...(vendor.gstRegistrationNo?.trim() ? [{ icon: 'file-text', label: 'GSTIN', value: vendor.gstRegistrationNo }] : [])
				]}
				onclick={() => {}}
			/>
		{/snippet}

		{#snippet tableHeader()}
			<TableHead class="w-[80px] text-center">No.</TableHead>
			<TableHead class="cursor-pointer hover:text-primary transition-colors">Vendor Name</TableHead>
			<TableHead class="hidden md:table-cell">City</TableHead>
			<TableHead class="hidden lg:table-cell">Contact & Info</TableHead>
			<TableHead class="text-right">Actions</TableHead>
		{/snippet}

		{#snippet tableRow(vendor: Vendor)}
			<TableCell class="text-center p-2">
				<div
					class="mx-auto flex size-10 items-center justify-center rounded-lg bg-orange-500/5 text-orange-600 ring-2 ring-transparent"
				>
					<Icon name="truck" class="size-5" />
				</div>
				<code class="mt-1 block text-[10px] font-mono text-muted-foreground truncate max-w-[70px] mx-auto">{vendor.no || '—'}</code>
			</TableCell>
			<TableCell>
				<div class="font-medium text-foreground">{vendor.name || 'N/A'}</div>
				<div class="text-xs text-muted-foreground md:hidden font-mono">{vendor.no}</div>
			</TableCell>
			<TableCell class="hidden md:table-cell">
				<span class="text-sm text-muted-foreground">{vendor.city?.trim() || '—'}</span>
			</TableCell>
			<TableCell class="hidden lg:table-cell">
				<div class="flex flex-col gap-1 text-xs">
					{#if vendor.contact?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="user" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.contact}</span>
						</div>
					{/if}
					{#if vendor.phoneNo?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="phone" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.phoneNo}</span>
						</div>
					{/if}
					{#if vendor.gstRegistrationNo?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground/80">
							<Icon name="file-text" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.gstRegistrationNo}</span>
						</div>
					{/if}
				</div>
			</TableCell>
			<TableCell class="text-right">
				<TableActions
					title={vendor.name}
					actions={[
						{
							label: 'View Details',
							icon: 'eye',
							onClick: () => {}
						}
					]}
				/>
			</TableCell>
		{/snippet}
	</MasterList>
</div>
