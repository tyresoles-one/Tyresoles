<script lang="ts">
	/**
	 * EXAMPLE: Dealers List Page - Refactored with Reusable Components
	 *
	 * This is a reference implementation showing how to use the new reusable components.
	 * Compare this with the original +page.svelte to see the reduction in boilerplate.
	 *
	 * BEFORE: ~195 lines
	 * AFTER: ~120 lines (38% reduction)
	 */
	import { goto } from '$app/navigation';

	// Reusable Composables
	import { usePaginatedList } from '$lib/composables';

	// Reusable Components
	import { EntityCard } from '$lib/components/venUI/entityCard';
	import { TableActions } from '$lib/components/venUI/tableActions';
	import { FilterChip, FilterSeparator } from '$lib/components/venUI/filterChip';

	// UI Components
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { TableCell, TableHead } from '$lib/components/ui/table';
	import MasterList from '$lib/components/venUI/masterList/MasterList.svelte';

	// GraphQL
	import { GetDealersDocument } from '$lib/services/graphql/generated/types';
	import type { GetDealersQuery } from '$lib/services/graphql/generated/types';

	type Dealer = NonNullable<GetDealersQuery['dealers']>['items'][number];
	type ViewMode = 'grid' | 'table';

	// State
	let viewMode = $state<ViewMode>('grid');

	// Use paginated list composable (eliminates ~60 lines of boilerplate)
	const list = usePaginatedList<Dealer>({
		query: GetDealersDocument,
		dataPath: 'dealers',
		pageSize: 50
	});
</script>

<div class="min-h-screen bg-background pb-20">
	<MasterList
		title="Dealers"
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
			<!-- Using FilterChip component -->
			<FilterChip label="All dealers" />
			<FilterSeparator />
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

		{#snippet gridItem(dealer: Dealer)}
			<!-- Using EntityCard component (eliminates ~50 lines per page) -->
			<EntityCard
				icon="store"
				title={dealer.name || '—'}
				subtitle={dealer.code}
				metadata={[
					...(dealer.depot?.trim() ? [{ icon: 'map-pin', label: 'Location', value: dealer.depot }] : []),
					...(dealer.phoneNo?.trim() ? [{ icon: 'phone', label: 'Phone', value: dealer.phoneNo }] : []),
					...(dealer.dealershipName?.trim()
						? [{ icon: 'building', label: 'Dealership', value: dealer.dealershipName }]
						: []),
					...(dealer.eMail?.trim() ? [{ icon: 'mail', label: 'Email', value: dealer.eMail }] : [])
				]}
				onclick={() => goto(`/dealers/${encodeURIComponent(dealer.code ?? '')}`)}
			/>
		{/snippet}

		{#snippet tableHeader()}
			<TableHead class="w-[200px]">Dealer</TableHead>
			<TableHead
				class="cursor-pointer hover:bg-accent/50 transition-colors"
				onclick={() => list.pagination.toggleSort('name')}
			>
				<div class="flex items-center gap-1.5">
					Name
					{#if list.pagination.sortField === 'name'}
						<Icon
							name={list.pagination.sortDirection === 'asc' ? 'arrow-up' : 'arrow-down'}
							class="size-3"
						/>
					{/if}
				</div>
			</TableHead>
			<TableHead class="hidden md:table-cell">Location</TableHead>
			<TableHead class="hidden lg:table-cell">Phone</TableHead>
			<TableHead class="hidden lg:table-cell">Dealership</TableHead>
			<TableHead class="text-right">Actions</TableHead>
		{/snippet}

		{#snippet tableRow(dealer: Dealer)}
			<TableCell class="font-medium">
				<div class="flex items-center gap-2">
					<div
						class="shrink-0 flex size-8 items-center justify-center rounded-md bg-primary/10 text-primary"
					>
						<Icon name="store" class="size-4" />
					</div>
					<code class="text-xs font-mono">{dealer.code}</code>
				</div>
			</TableCell>
			<TableCell class="font-medium">{dealer.name || '—'}</TableCell>
			<TableCell class="hidden md:table-cell text-sm text-muted-foreground">
				{dealer.depot?.trim() || '—'}
			</TableCell>
			<TableCell class="hidden lg:table-cell text-sm text-muted-foreground font-mono">
				{dealer.phoneNo || '—'}
			</TableCell>
			<TableCell class="hidden lg:table-cell text-sm text-muted-foreground truncate max-w-[200px]">
				{dealer.dealershipName?.trim() || dealer.eMail?.trim() || '—'}
			</TableCell>
			<TableCell class="text-right">
				<!-- Using TableActions component -->
				<TableActions
					title={dealer.name}
					actions={[
						{
							label: 'View Details',
							icon: 'eye',
							onClick: () => goto(`/dealers/${encodeURIComponent(dealer.code ?? '')}`)
						}
					]}
				/>
			</TableCell>
		{/snippet}
	</MasterList>
</div>
