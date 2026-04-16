<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';

	// Reusable composables
	import { usePaginatedList } from '$lib/composables';

	import { authStore } from '$lib/stores/auth';
	import { toFetchParamsInput } from '$lib/business/fetch-params';
	import type { FetchParams } from '$lib/business/models';
	import { graphqlMutation } from '$lib/services/graphql';
	import { toast } from '$lib/components/venUI/toast';

	// UI
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { DataGrid, type DataGridColumn, type FilterRule } from '$lib/components/venUI/datagrid';

	// GraphQL
	import {
		CreateProductionVendorDocument,
		GetMyVendorsDocument
	} from '$lib/services/graphql/generated/types';
	import type {
		CreateProductionVendorMutation,
		FetchParamsInput,
		GetMyVendorsQuery,
		LoginUser
	} from '$lib/services/graphql/generated/types';

	type Vendor = NonNullable<NonNullable<GetMyVendorsQuery['myVendors']>['items']>[number];
	type ViewMode = 'grid' | 'table';

	let viewMode = $state<ViewMode>('grid');
	let filterRules = $state<FilterRule[]>([]);

	/** Ecomile procurement (`ECOPROC`): scope `myVendors` to this manager via `ecoMgr`. */
	function ecoMgrForUser(u: LoginUser | null | undefined): string | null {
		if (u?.userType?.toUpperCase() !== 'ECOPROC' || !u.entityCode?.trim()) return null;
		return u.entityCode.trim();
	}

	function ecoMgrFromAuth(): string | null {
		return ecoMgrForUser(authStore.get().user);
	}

	function buildFilterRulesWhere(rules: FilterRule[]): Record<string, any>[] | undefined {
		if (rules.length === 0) return undefined;
		return rules.map((rule) => {
			return { [rule.columnId]: { [rule.operator]: rule.value } };
		});
	}

	/** Maps MasterList search AND filters to Hot Chocolate `where` on `myVendors` (server-side filter). */
	function vendorsSearchToWhere(term: string, rulesForWhere: FilterRule[] = filterRules): Record<string, unknown> {
		const q = term.trim();
		const filterConds = buildFilterRulesWhere(rulesForWhere);
		const andConds: Record<string, any>[] = [];

		if (q) {
			andConds.push({
				or: [
					{ no: { contains: q } },
					{ name: { contains: q } },
					{ city: { contains: q } },
					{ phoneNo: { contains: q } },					
					{ contact: { contains: q } }
				]
			});
		}

		if (filterConds && filterConds.length > 0) {
			andConds.push(...filterConds);
		}

		let wherePayload: Record<string, unknown>;
		if (andConds.length === 0) {
			wherePayload = { where: null };
		} else if (andConds.length === 1) {
			wherePayload = { where: andConds[0] };
		} else {
			wherePayload = { where: { and: andConds } };
		}

		const ecoMgr = ecoMgrFromAuth();
		return { ...wherePayload, ecoMgr: ecoMgr ?? null };
	}

	const list = usePaginatedList<Vendor>({
		query: GetMyVendorsDocument,
		dataPath: 'myVendors',
		pageSize: 50,
		mapSearchToVariables: (term) => vendorsSearchToWhere(term, filterRules),
		serverVariableAllowlist: ['respCenter', 'categories', 'ecoMgr', 'where', 'order'],
		paginationMode: 'cursor',
		pageInfoPath: 'myVendors.pageInfo'
	});

	onMount(() => {
		const unsub = authStore.subscribe((auth) => {
			list.pagination.setVariables({ ecoMgr: ecoMgrForUser(auth.user) });
		});
		return unsub;
	});

	function handleFilterRulesChange(rules: FilterRule[]) {
		filterRules = rules;
		// Pass `rules` explicitly — `filterRules` may not be updated yet this tick (Svelte batching).
		const vars = vendorsSearchToWhere(list.searchQuery.value, rules);
		list.pagination.setVariables(vars);
		list.onRefresh();
	}

	function vendorDetailPath(d: Vendor) {
		const id = d.no?.trim();
		return id ? `/vendors/${encodeURIComponent(id)}` : '/vendors';
	}

	function formatVendorBalance(value: unknown): string {
		if (value === null || value === undefined || value === '') return '—';
		const n = typeof value === 'number' ? value : parseFloat(String(value));
		if (Number.isNaN(n)) return '—';
		return n.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
	}

	const columns: DataGridColumn<Vendor>[] = [
		{ accessorKey: 'no', header: 'Vendor No' },
		{ accessorKey: 'name', header: 'Vendor Name' },
		{ accessorKey: 'city', header: 'City' },
		{ accessorKey: 'phoneNo', header: 'Mobile No' },
		{
			accessorKey: 'balance',
			header: 'Balance',
			meta: { align: 'right' as const },
			cell: ({ getValue }) => formatVendorBalance(getValue())
		}
	];

	let creatingVendor = $state(false);

	/** Same shape as ecoproc `ensureFetchParams` — required by `createProductionVendor` → `CreateVendorAsync`. */
	function buildCreateVendorParam(): FetchParamsInput | null {
		const u = authStore.get().user;
		if (!u?.userId?.trim()) return null;
		const fp: FetchParams = {
			respCenters: u.respCenter ? [u.respCenter] : [],
			userCode: u.userId,
			userDepartment: u.department ?? '',
			userName: u.fullName ?? '',
			userSpecialToken: u.userSpecialToken ?? '',
			userType: u.userType ?? '',
			regions: [],
			view: '',
			type: ''
		};
		return toFetchParamsInput(fp);
	}

	async function addVendor() {
		if (creatingVendor) return;
		const param = buildCreateVendorParam();
		if (!param) {
			toast.error('Sign in required to create a vendor.');
			return;
		}
		creatingVendor = true;
		try {
			const res = await graphqlMutation<CreateProductionVendorMutation>(CreateProductionVendorDocument, {
				variables: { param }
			});
			if (!res.success || !res.data) {
				toast.error(res.error ?? 'Failed to create vendor.');
				return;
			}
			const newNo = res.data.createProductionVendor?.trim();
			if (!newNo) {
				toast.error('Server did not return a vendor number.');
				return;
			}
			toast.success('Vendor created.');
			await goto(`/vendors/${encodeURIComponent(newNo)}`);
		} finally {
			creatingVendor = false;
		}
	}
</script>

<div class="min-h-screen bg-background pb-20 pt-8">
	<DataGrid
		title="Vendors"
		description="View and manage vendors"
		items={list.items}
		{columns}
		pagination={list.pagination}
		loading={list.loading}
		loadingMore={list.loadingMore}
		bind:searchQuery={list.searchQuery.value}
		mobileCardTitleKey="name"
		mobileCardSubtitleKey="no"
		onRowClick={(vendor: Vendor) => goto(vendorDetailPath(vendor))}
		showFilters={true}
		{filterRules}
		onFilterRulesChange={handleFilterRulesChange}
	>
		{#snippet actions()}
			<Button
				size="sm"
				class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
				disabled={creatingVendor}
				onclick={() => void addVendor()}
			>
				{#if creatingVendor}
					<Icon name="loader-2" class="size-3.5 animate-spin" />
				{:else}
					<Icon name="plus" class="size-3.5" />
				{/if}
				<span class="hidden sm:inline">{creatingVendor ? 'Creating…' : 'Add Vendor'}</span>
				<span class="sm:hidden">{creatingVendor ? '…' : 'Add'}</span>
			</Button>
		{/snippet}
	</DataGrid>
</div>
