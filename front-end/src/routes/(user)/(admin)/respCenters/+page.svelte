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
	// Temporary mocks until backend implements responsibilityCenters
	import { gql } from 'graphql-request';
	const GetResponsibilityCentersDocument: any = gql`query GetRespCentersMock { __typename }`;
	type GetResponsibilityCentersQuery = { responsibilityCenters: { items: Array<Record<string, any>> } };

	type RespCenter = NonNullable<GetResponsibilityCentersQuery['responsibilityCenters']>['items'][number];
	type ViewMode = 'grid' | 'table';

	let viewMode = $state<ViewMode>('grid');

	const list = usePaginatedList<RespCenter>({
		query: GetResponsibilityCentersDocument,
		dataPath: 'responsibilityCenters',
		pageSize: 50
	});
</script>

<MasterList
	title="Responsibility Centers"
	description="View and manage responsibility centers"
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
			<span class="px-2.5 py-1 text-xs font-medium text-muted-foreground">All centers</span>
		</div>
		<div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>
	{/snippet}

	{#snippet actions()}
		<Button
			size="sm"
			class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
		>
			<Icon name="plus" class="size-3.5" />
			<span class="hidden sm:inline">Add Center</span>
			<span class="sm:hidden">Add</span>
		</Button>
	{/snippet}

	{#snippet gridItem(rc: RespCenter)}
		<EntityCard
			icon="building-2"
			title={rc.name || '—'}
			subtitle={rc.code}
			metadata={[
				...(rc.city?.trim() ? [{ icon: 'map-pin', label: 'City', value: rc.city }] : []),
				...(rc.phoneNo?.trim() ? [{ icon: 'phone', label: 'Phone', value: rc.phoneNo }] : []),
				...(rc.contact?.trim() ? [{ icon: 'user', label: 'Contact', value: rc.contact }] : [])
			]}
			onclick={() => goto(`/respCenters/${encodeURIComponent(rc.code)}`)}
		/>
	{/snippet}

	{#snippet tableHeader()}
		<TableHead class="w-[80px] text-center">Code</TableHead>
		<TableHead class="cursor-pointer hover:text-primary transition-colors">Name</TableHead>
		<TableHead class="hidden md:table-cell">City</TableHead>
		<TableHead class="hidden lg:table-cell">Contact</TableHead>
		<TableHead class="text-right">Actions</TableHead>
	{/snippet}

	{#snippet tableRow(rc: RespCenter)}
		<TableCell class="text-center p-2">
			<div
				class="mx-auto flex size-10 items-center justify-center rounded-lg bg-primary/5 text-primary ring-2 ring-transparent"
			>
				<Icon name="building-2" class="size-5" />
			</div>
			<code class="mt-1 block text-[10px] font-mono text-muted-foreground truncate max-w-[70px] mx-auto">{rc.code || '—'}</code>
		</TableCell>
		<TableCell>
			<div class="font-medium text-foreground">{rc.name || 'N/A'}</div>
			<div class="text-xs text-muted-foreground md:hidden font-mono">{rc.code}</div>
		</TableCell>
		<TableCell class="hidden md:table-cell">
			<span class="text-sm text-muted-foreground">{rc.city?.trim() || '—'}</span>
		</TableCell>
		<TableCell class="hidden lg:table-cell">
			<div class="flex flex-col gap-1 text-xs">
				{#if rc.contact?.trim()}
					<div class="flex items-center gap-2 text-muted-foreground">
						<Icon name="user" class="size-3 shrink-0" />
						<span class="truncate max-w-[140px]">{rc.contact}</span>
					</div>
				{/if}
				{#if rc.phoneNo?.trim()}
					<div class="flex items-center gap-2 text-muted-foreground">
						<Icon name="phone" class="size-3 shrink-0" />
						<span class="truncate max-w-[140px]">{rc.phoneNo}</span>
					</div>
				{/if}
				{#if !rc.contact?.trim() && !rc.phoneNo?.trim()}
					<span class="text-muted-foreground/60">—</span>
				{/if}
			</div>
		</TableCell>
		<TableCell class="text-right">
			<TableActions
				title={rc.name}
				actions={[
					{
						label: 'View Details',
						icon: 'eye',
						onClick: () => goto(`/respCenters/${encodeURIComponent(rc.code)}`)
					}
				]}
			/>
		</TableCell>
	{/snippet}
</MasterList>
