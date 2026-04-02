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
	import * as Dialog from '$lib/components/ui/dialog';
	import * as Command from '$lib/components/ui/command';
	import * as Popover from '$lib/components/ui/popover';
	import * as Field from '$lib/components/ui/field';
	import { Input } from '$lib/components/ui/input';
	import Check from '@lucide/svelte/icons/check';
	import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { cn } from '$lib/utils';
	import { graphqlQuery, graphqlMutation, buildMutation } from '$lib/services/graphql';
	import { authStore } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { PAGE_SIZE } from '$lib/components/venUI/master-select/masters-api';

	// GraphQL
	import { GetDealersDocument, GetMyCustomersDocument } from '$lib/services/graphql/generated/types';
	import type {
		GetDealersQuery,
		CustomerFilterInput,
		SalespersonPurchaserFilterInput
	} from '$lib/services/graphql/generated/types';

	type Dealer = NonNullable<NonNullable<GetDealersQuery['dealers']>['items']>[number];
	type ViewMode = 'grid' | 'table';

	type MyCustomersConn = {
		myCustomers?: {
			nodes: { no: string; name?: string | null }[];
			pageInfo: { hasNextPage: boolean; endCursor: string | null };
			totalCount: number;
		};
	};

	let viewMode = $state<ViewMode>('grid');

	/** Maps MasterList search to Hot Chocolate `where` on `myDealers` (server-side filter). */
	function dealersSearchToWhere(term: string): { where: SalespersonPurchaserFilterInput | null } {
		const q = term.trim();
		if (!q) return { where: null };
		return {
			where: {
				or: [
					{ code: { contains: q } },
					{ name: { contains: q } },
					{ dealershipName: { contains: q } },
					{ mobileNo: { contains: q } },
					{ eMail: { contains: q } },
					{ depot: { contains: q } }
				]
			}
		};
	}

	const list = usePaginatedList<Dealer>({
		query: GetDealersDocument,
		dataPath: 'dealers',
		pageSize: 50,
		mapSearchToVariables: dealersSearchToWhere
	});

	const user = $derived(authStore.get().user);

	$effect(() => {
		const u = $authStore.user;
		list.pagination.setVariables({
			entityType: u?.entityType ?? null,
			entityCode: u?.entityCode ?? null,
			department: u?.department ?? null,
			respCenter: u?.respCenter ?? null
		});
	});
	/** Non–sales employees only (LoginUser uses `department`, not entityDepartment). */
	const canAddDealer = $derived(
		user?.entityType === 'Employee' && user?.department !== 'Sales'
	);

	let addDialogOpen = $state(false);
	let pickerOpen = $state(false);
	let searchQuery = $state('');
	let debouncedSearch = $state('');
	let options = $state<{ label: string; value: string; name: string }[]>([]);
	let loadingCustomers = $state(false);
	let loadingMoreCustomers = $state(false);
	let hasNextPage = $state(false);
	let endCursor = $state<string | null>(null);
	let selectedCustomerNo = $state('');
	let selectedCustomerName = $state('');
	let createDealerLoading = $state(false);

	const CreateDealerDocument = buildMutation`
		mutation CreateDealer($customerNo: String!) {
			createDealer(customerNo: $customerNo) {
				success
				message
				dealerCode
			}
		}
	`;

	type CreateDealerMutationData = {
		createDealer: { success: boolean; message: string; dealerCode?: string | null };
	};

	function buildUnassignedWhere(search: string): CustomerFilterInput {
		const emptyDealer: CustomerFilterInput = { dealerCode: { eq: '' } };
		const q = search?.trim();
		if (!q) return emptyDealer;
		return {
			and: [emptyDealer, { or: [{ no: { contains: q } }, { name: { contains: q } }] }]
		};
	}

	$effect(() => {
		const q = searchQuery;
		const t = setTimeout(() => {
			debouncedSearch = q;
		}, 300);
		return () => clearTimeout(t);
	});

	$effect(() => {
		if (!addDialogOpen) return;
		selectedCustomerNo = '';
		selectedCustomerName = '';
		searchQuery = '';
		debouncedSearch = '';
		options = [];
		endCursor = null;
		hasNextPage = false;
		pickerOpen = false;
	});

	async function fetchCustomersPage(append: boolean) {
		const u = authStore.get().user;
		if (!u) return;
		const variables = {
			entityType: u.entityType ?? null,
			entityCode: u.entityCode ?? null,
			department: u.department ?? null,
			respCenter: u.respCenter ?? null,
			first: PAGE_SIZE,
			after: append ? endCursor : undefined,
			where: buildUnassignedWhere(debouncedSearch)
		};
		if (append) loadingMoreCustomers = true;
		else loadingCustomers = true;
		try {
			const result = await graphqlQuery<MyCustomersConn>(GetMyCustomersDocument, {
				variables,
				skipLoading: true,
				skipCache: true
			});
			if (!result.success || !result.data?.myCustomers?.nodes) return;
			const conn = result.data.myCustomers;
			const newOpts = conn.nodes.map((n) => {
				const no = n.no ?? '';
				const name = (n.name ?? '').trim();
				return { value: no, name, label: name ? `${no} — ${name}` : no };
			});
			if (append) options = [...options, ...newOpts];
			else options = newOpts;
			hasNextPage = conn.pageInfo?.hasNextPage ?? false;
			endCursor = conn.pageInfo?.endCursor ?? null;
		} finally {
			loadingCustomers = false;
			loadingMoreCustomers = false;
		}
	}

	$effect(() => {
		if (!pickerOpen) return;
		debouncedSearch;
		void fetchCustomersPage(false);
	});

	function viewport(node: HTMLElement) {
		const observer = new IntersectionObserver(
			(entries) => {
				if (entries[0].isIntersecting && hasNextPage && !loadingMoreCustomers && !loadingCustomers) {
					void fetchCustomersPage(true);
				}
			},
			{ root: null, rootMargin: '80px' }
		);
		observer.observe(node);
		return { destroy: () => observer.disconnect() };
	}

	function pickCustomer(no: string, name: string) {
		selectedCustomerNo = no;
		selectedCustomerName = name;
		pickerOpen = false;
		searchQuery = '';
	}

	const pickerTriggerLabel = $derived(
		selectedCustomerNo
			? (() => {
					const o = options.find((x) => x.value === selectedCustomerNo);
					if (o) return o.label;
					return selectedCustomerName
						? `${selectedCustomerNo} — ${selectedCustomerName}`
						: selectedCustomerNo;
				})()
			: 'Search customers without dealer…'
	);

	async function handleCreateDealer() {
		if (!selectedCustomerNo) {
			toast.error('Select a customer first.');
			return;
		}
		createDealerLoading = true;
		try {
			const result = await graphqlMutation<CreateDealerMutationData>(CreateDealerDocument, {
				variables: { customerNo: selectedCustomerNo },
				skipLoading: true
			});
			if (!result.success) {
				toast.error(result.error ?? 'Failed to create dealer.');
				return;
			}
			const payload = result.data?.createDealer;
			if (!payload) {
				toast.error('No response from server.');
				return;
			}
			toast.success(payload.message || `Dealer ${payload.dealerCode ?? ''} created.`);
			addDialogOpen = false;
			selectedCustomerNo = '';
			selectedCustomerName = '';
			list.onRefresh();
		} finally {
			createDealerLoading = false;
		}
	}

	function dealerDetailPath(d: Dealer) {
		const id = d.code?.trim();
		return id ? `/dealers/${encodeURIComponent(id)}` : '/dealers';
	}
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
			<!-- Inline filter bar (avoids SSR snippet scope issue with FilterChip/FilterSeparator) -->
			<div class="hidden sm:flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20">
				<span class="px-2.5 py-1 text-xs font-medium text-muted-foreground">All dealers</span>
			</div>
			<div class="w-px h-6 bg-border/80 mx-1 hidden sm:block"></div>
		{/snippet}

		{#snippet actions()}
			{#if canAddDealer}
				<Button
					size="sm"
					class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
					onclick={() => (addDialogOpen = true)}
				>
					<Icon name="plus" class="size-3.5" />
					<span class="hidden sm:inline">Add Dealer</span>
					<span class="sm:hidden">Add</span>
				</Button>
			{/if}
		{/snippet}

		{#snippet gridItem(dealer: Dealer)}
			<EntityCard
				icon="store"
				title={dealer.name || '—'}
				subtitle={dealer.code ?? ''}
				metadata={[
					...(dealer.depot?.trim()
						? [{ icon: 'map-pin' as const, label: 'Location', value: dealer.depot }]
						: []),
					...(dealer.phoneNo?.trim() ? [{ icon: 'phone' as const, label: 'Phone', value: dealer.phoneNo }] : []),
					...(dealer.dealershipName?.trim()
						? [{ icon: 'user' as const, label: 'Dealership', value: dealer.dealershipName }]
						: []),
					...(dealer.eMail?.trim()
						? [{ icon: 'mail' as const, label: 'Email', value: dealer.eMail }]
						: [])
				]}
				onclick={() => goto(dealerDetailPath(dealer))}
			/>
		{/snippet}

		{#snippet tableHeader()}
			<TableHead class="w-[80px] text-center">Code</TableHead>
			<TableHead class="cursor-pointer hover:text-primary transition-colors">Name</TableHead>
			<TableHead class="hidden md:table-cell">Location</TableHead>
			<TableHead class="hidden lg:table-cell">Contact</TableHead>
			<TableHead class="text-right">Actions</TableHead>
		{/snippet}

		{#snippet tableRow(dealer: Dealer)}
			<TableCell class="text-center p-2">
				<div
					class="mx-auto flex size-10 items-center justify-center rounded-lg bg-primary/5 text-primary ring-2 ring-transparent"
				>
					<Icon name="store" class="size-5" />
				</div>
				<code class="mt-1 block text-[10px] font-mono text-muted-foreground truncate max-w-[70px] mx-auto">{dealer.code || '—'}</code>
			</TableCell>
			<TableCell>
				<div class="font-medium text-foreground">{dealer.name || 'N/A'}</div>
				<div class="text-xs text-muted-foreground md:hidden font-mono">{dealer.code}</div>
			</TableCell>
			<TableCell class="hidden md:table-cell">
				<span class="text-sm text-muted-foreground">{dealer.depot?.trim() || '—'}</span>
			</TableCell>
			<TableCell class="hidden lg:table-cell">
				<div class="flex flex-col gap-1 text-xs">
					{#if dealer.dealershipName?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="user" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{dealer.dealershipName}</span>
						</div>
					{/if}
					{#if dealer.eMail?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="mail" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{dealer.eMail}</span>
						</div>
					{/if}
					{#if dealer.phoneNo?.trim()}
						<div class="flex items-center gap-2 text-muted-foreground">
							<Icon name="phone" class="size-3 shrink-0" />
							<span class="truncate max-w-[140px]">{dealer.phoneNo}</span>
						</div>
					{/if}
					{#if !dealer.dealershipName?.trim() && !dealer.phoneNo?.trim() && !dealer.eMail?.trim()}
						<span class="text-muted-foreground/60">—</span>
					{/if}
				</div>
			</TableCell>
			<TableCell class="text-right">
				<TableActions
					title={dealer.name ?? ''}
					actions={[
						{
							label: 'View Details',
							icon: 'eye',
							onClick: () => goto(dealerDetailPath(dealer))
						}
					]}
				/>
			</TableCell>
		{/snippet}
	</MasterList>
</div>

<Dialog.Root bind:open={addDialogOpen}>
	<Dialog.Content class="sm:max-w-lg">
		<Dialog.Header>
			<Dialog.Title>Add dealer</Dialog.Title>
			<Dialog.Description>
				Choose a customer with no dealer code. The name fills in when you select a row.
			</Dialog.Description>
		</Dialog.Header>

		<div class="flex flex-col gap-4 py-2">
			<Field.Field class="w-full">
				<Field.Label>Customer (no dealer code)</Field.Label>
				<Field.Content>
					<Popover.Root bind:open={pickerOpen}>
						<Popover.Trigger
							class={cn(
								'border-input ring-offset-background placeholder:text-muted-foreground focus:ring-ring flex h-9 w-full items-center justify-between whitespace-nowrap rounded-md border bg-transparent px-3 py-2 text-sm shadow-xs focus:outline-none focus:ring-1 [&>span]:line-clamp-1'
							)}
						>
							<span class="truncate text-left">{pickerTriggerLabel}</span>
							<ChevronsUpDown class="ml-2 size-4 shrink-0 opacity-50" />
						</Popover.Trigger>
						<Popover.Content
							class="min-w-[200px] p-0 max-w-[calc(100vw-2rem)] w-(--bits-popover-anchor-width)"
							align="start"
							sideOffset={4}
						>
							<Command.Root shouldFilter={false} class="flex flex-col max-h-[min(80vh,400px)]">
								<Command.Input placeholder="Search by no. or name…" bind:value={searchQuery} />
								<Command.List class="overflow-x-hidden overflow-y-auto flex-1 max-h-none min-h-[120px]">
									{#if loadingCustomers}
										<div class="flex items-center justify-center py-8 text-muted-foreground">
											<Loader2 class="size-5 animate-spin" />
										</div>
									{:else}
										<Command.Empty>No unassigned customers.</Command.Empty>
										<Command.Group class="overflow-visible min-w-full w-max">
											{#each options as opt}
												{@const isSelected = opt.value === selectedCustomerNo}
												<Command.Item
													value={opt.value}
													keywords={[]}
													onSelect={() => pickCustomer(opt.value, opt.name)}
													class="cursor-pointer pr-4 min-w-full w-max flex items-center gap-2"
												>
													<div class="flex h-4 w-4 shrink-0 items-center justify-center">
														<Check class={cn('size-4', isSelected ? 'opacity-100' : 'opacity-0')} />
													</div>
													{opt.label}
												</Command.Item>
											{/each}
											{#if hasNextPage && options.length > 0}
												<div use:viewport class="h-6 flex items-center justify-center">
													{#if loadingMoreCustomers}
														<Loader2 class="size-4 animate-spin text-muted-foreground" />
													{:else}
														<span class="text-muted-foreground text-xs">Scroll for more</span>
													{/if}
												</div>
											{/if}
										</Command.Group>
									{/if}
								</Command.List>
							</Command.Root>
						</Popover.Content>
					</Popover.Root>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label>Customer name</Field.Label>
				<Field.Content>
					<Input
						readonly
						class="bg-muted/40"
						value={selectedCustomerName}
						placeholder="—"
					/>
				</Field.Content>
			</Field.Field>
		</div>

		<Dialog.Footer class="flex gap-2 justify-end">
			<Button
				type="button"
				variant="outline"
				disabled={createDealerLoading}
				onclick={() => (addDialogOpen = false)}>Cancel</Button>
			<Button
				type="button"
				disabled={!selectedCustomerNo || createDealerLoading}
				onclick={handleCreateDealer}
				class="gap-2"
			>
				{#if createDealerLoading}
					<Loader2 class="size-4 animate-spin" />
				{/if}
				Create
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
