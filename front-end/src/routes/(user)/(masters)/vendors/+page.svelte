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

	function vendorDetailPath(d: Vendor) {
		console.log(d);
		const id = d.no?.trim();
		return id ? `/vendors/${encodeURIComponent(id)}` : '/vendors';
	}
</script>

<div class="min-h-screen bg-background pb-20">
	<MasterList
		title="Vendors"
		description="View and manage dealers"
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
			<!-- Inline filter bar (avoids SSR snippet scope issue with FilterChip/FilterSeparator) -->
			<div class="hidden sm:flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20">
				<span class="px-2.5 py-1 text-xs font-medium text-muted-foreground">All dealers</span>
			</div>
			<div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>
		{/snippet}

		{#snippet actions()}
			<Button
				size="sm"
				class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
			>
				<Icon name="plus" class="size-3.5" />
				<span class="hidden sm:inline">Add Dealer</span>
				<span class="sm:hidden">Add</span>
			</Button>
		{/snippet}

		{#snippet gridItem(vendor: Vendor)}
			<EntityCard
				icon="store"
				title={vendor.name || '—'}
				subtitle={vendor.code}
				metadata={[
					...(vendor.depot?.trim()
						? [{ icon: 'map-pin' as const, label: 'Location', value: vendor.depot }]
						: []),
					...(vendor.phoneNo?.trim() ? [{ icon: 'phone' as const, label: 'Phone', value: vendor.phoneNo }] : []),
					...(vendor.dealershipName?.trim()
						? [{ icon: 'user' as const, label: 'Dealership', value: vendor.dealershipName }]
						: []),
					...(vendor.eMail?.trim()
						? [{ icon: 'mail' as const, label: 'Email', value: vendor.eMail }]
						: [])
				]}
				onclick={() => goto(vendorDetailPath(vendor))}
			/>
		{/snippet}

		{#snippet tableHeader()}
			<TableHead class="w-[80px] text-center">Code</TableHead>
			<TableHead class="cursor-pointer hover:text-primary transition-colors">Name</TableHead>
			<TableHead class="hidden md:table-cell">Location</TableHead>
			<TableHead class="hidden lg:table-cell">Contact</TableHead>
			<TableHead class="text-right">Actions</TableHead>
		{/snippet}

		{#snippet tableRow(vendor: Vendor)}
			<TableCell class="text-center p-2">
				<div
					class="mx-auto flex size-10 items-center justify-center rounded-lg bg-primary/5 text-primary ring-2 ring-transparent"
				>
					<Icon name="store" class="size-5" />
				</div>
				<code class="mt-1 block text-[10px] font-mono text-muted-foreground truncate max-w-[70px] mx-auto">{vendor.code || '—'}</code>
			</TableCell>
			<TableCell>
				<div class="font-medium text-foreground">{vendor.name || 'N/A'}</div>
				<div class="text-xs text-muted-foreground md:hidden font-mono">{vendor.code}</div>
			</TableCell>
			<TableCell class="hidden md:table-cell">
				<span class="text-sm text-muted-foreground">{vendor.depot?.trim() || '—'}</span>
			</TableCell>
			<TableCell class="hidden lg:table-cell">
				<div class="flex flex-col gap-1 text-xs">
					{#if vendor.dealershipName?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="user" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.dealershipName}</span>
						</div>
					{/if}
					{#if vendor.eMail?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="mail" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.eMail}</span>
						</div>
					{/if}
					{#if vendor.phoneNo?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="phone" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{vendor.phoneNo}</span>
						</div>
					{/if}
					{#if !vendor.dealershipName?.trim() && !vendor.phoneNo?.trim() && !vendor.eMail?.trim()}
						<span class="text-muted-foreground/60">—</span>
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
							onClick: () => goto(vendorDetailPath(vendor))
						}
					]}
				/>
			</TableCell>
		{/snippet}
	</MasterList>
</div>
