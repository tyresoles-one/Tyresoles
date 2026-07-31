<script lang="ts">
	import { onMount } from 'svelte';
	import { buildQuery, buildMutation, graphqlQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import { usePaginatedList } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { TableActions } from '$lib/components/venUI/tableActions';
	import { DataGrid, type DataGridColumn, type FilterRule } from '$lib/components/venUI/datagrid';
	import { goto } from '$app/navigation';
	import { authStore } from '$lib/stores/auth';

	type CrmContact = {
		id: string;
		contactType?: string | null;
		fullName: string;
		companyName?: string | null;
		mobileNo?: string | null;
		mobileNo2?: string | null;
		emailIds?: string | null;
		isDecisionMaker: boolean;
		address?: string | null;
		city?: string | null;
		state?: string | null;
		respCenter?: string | null;
		erpCustomerNos?: string | null;
		erpAreaCodes?: string | null;
		products?: string | null;
		tags?: string | null;
		isActive: boolean;
		createdBy?: string | null;
	};

	type CrmMasterItem = {
		id: number;
		name: string;
	};

	type CrmMasterItemsResult = {
		crmMasterItems: CrmMasterItem[];
	};

	type GetCrmContactsResult = {
		crmContacts: {
			items: CrmContact[];
			totalCount: number;
		};
	};

	type SaveCrmContactResult = {
		saveCrmContact: CrmContact;
	};

	type DeleteCrmContactResult = {
		deleteCrmContact: boolean;
	};

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<CrmMasterItemsResult, { type: string; where?: any }>;

	const GetCrmContactsDocument = buildQuery`
		query GetCrmContacts($skip: Int, $take: Int, $where: CrmContactFilterInput, $order: [CrmContactSortInput!]) {
			crmContacts: getCrmContacts(skip: $skip, take: $take, where: $where, order: $order) {
				items {
					id
					contactType
					contactCategory
					fullName
					companyName
					mobileNo
					mobileNo2
					emailIds
					isDecisionMaker
					address
					city
					state
					respCenter
					erpCustomerNos
					erpAreaCodes
					products
					tags
					isActive
					createdBy
				}
				totalCount
			}
		}
	` as unknown as TypedDocumentNode<GetCrmContactsResult, { skip?: number; take?: number; where?: any; order?: any }>;

	const SaveCrmContactDocument = buildMutation`
		mutation SaveCrmContact($input: CrmContactInput!) {
			saveCrmContact(input: $input) {
				id
				contactType
				fullName
				companyName
				mobileNo
				mobileNo2
				emailIds
				isDecisionMaker
				address
				city
				state
				respCenter
				erpCustomerNos
				erpAreaCodes
				products
				tags
				isActive
				createdBy
			}
		}
	` as unknown as TypedDocumentNode<SaveCrmContactResult, { input: any }>;

	const DeleteCrmContactDocument = buildMutation`
		mutation DeleteCrmContact($id: UUID!) {
			deleteCrmContact(id: $id)
		}
	` as unknown as TypedDocumentNode<DeleteCrmContactResult, { id: string }>;

	const FILTER_STORAGE_KEY = 'crm_contacts_filter_rules';

	let filterRules = $state<FilterRule[]>([]);

	function searchToWhere(term: string, rules: FilterRule[] = filterRules) {
		const q = term.trim();
		const andConds: Record<string, any>[] = [];

		if (q) {
			andConds.push({
				or: [
					{ fullName: { contains: q } },
					{ companyName: { contains: q } },
					{ erpCustomerNos: { contains: q } },
					{ mobileNo: { contains: q } },
					{ emailIds: { contains: q } },
					{ city: { contains: q } },
					{ tags: { contains: q } }
				]
			});
		}

		if (rules && rules.length > 0) {
			const filterConds = rules.map((r) => {
				let val = r.value;
				let operator = r.operator;
				if (r.columnId === 'isActive' && typeof val === 'string') {
					const lowerVal = val.toLowerCase();
					if (lowerVal === 'active' || lowerVal === 'true' || lowerVal === '1') {
						val = true;
						operator = 'eq';
					} else if (lowerVal === 'inactive' || lowerVal === 'false' || lowerVal === '0') {
						val = false;
						operator = 'eq';
					}
				}
				return { [r.columnId]: { [operator]: val } };
			});
			andConds.push(...filterConds);
		}

		if (andConds.length === 0) return { where: null };
		if (andConds.length === 1) return { where: andConds[0] };
		return { where: { and: andConds } };
	}

	const list = usePaginatedList<CrmContact>({
		query: GetCrmContactsDocument,
		dataPath: 'crmContacts',
		itemsPath: 'crmContacts.items',
		countPath: 'crmContacts.totalCount',
		strategy: 'server',
		pageSize: 50,
		mapSearchToVariables: (term) => searchToWhere(term, filterRules),
		serverVariableAllowlist: ['where', 'order', 'skip', 'take']
	});

	function handleFilterRulesChange(rules: FilterRule[]) {
		filterRules = rules;
		if (rules && rules.length > 0) {
			try {
				localStorage.setItem(FILTER_STORAGE_KEY, JSON.stringify(rules));
			} catch (e) {
				console.error('Failed to save filter rules', e);
			}
		} else {
			try {
				localStorage.removeItem(FILTER_STORAGE_KEY);
			} catch (e) {
				console.error('Failed to remove filter rules', e);
			}
		}
		list.pagination.setVariables(searchToWhere(list.searchQuery.value, rules));
		list.onRefresh();
	}

	function loadStoredFilterRules() {
		try {
			const saved = localStorage.getItem(FILTER_STORAGE_KEY);
			if (saved) {
				const parsed = JSON.parse(saved);
				if (Array.isArray(parsed) && parsed.length > 0) {
					filterRules = parsed;
					list.pagination.setVariables(searchToWhere(list.searchQuery.value, parsed));
					list.onRefresh();
				}
			}
		} catch (e) {
			console.error('Failed to load saved filter rules', e);
		}
	}

	const uniqueTags = $derived(
		Array.from(new Set(
			list.items.flatMap(item => 
				item.tags ? item.tags.split(',').map(t => t.trim()).filter(Boolean) : []
			)
		))
	);

	let contactTypes = $state<{ value: string; label: string }[]>([]);

	async function loadContactTypes() {
		try {
			const res = await graphqlQuery<CrmMasterItemsResult>(GetCrmMasterItemsDocument, {
				variables: { type: 'CONTACT_TYPE' }
			});
			if (res.success && res.data?.crmMasterItems) {
				contactTypes = res.data.crmMasterItems.map(x => ({
					value: x.name,
					label: x.name
				}));
			} else if (!res.success) {
				console.error('Failed to load contact types:', res.error);
				toast.error('Failed to load Contact Types');
			}
		} catch (err) {
			console.error('Failed to load contact types', err);
			toast.error('Failed to load Contact Types');
		}
	}

	onMount(() => {
		loadContactTypes();
		loadStoredFilterRules();
	});


	function handleRowClick(contact: CrmContact) {
		goto(`/crm-contacts/${contact.id}`);
	}

	function openAddDialog() {
		goto('/crm-contacts/new');
	}

	const columns: DataGridColumn<CrmContact>[] = [
		{ accessorKey: 'fullName', header: 'Full Name' },
		{ accessorKey: 'companyName', header: 'Company' },
		{ accessorKey: 'erpCustomerNos', header: 'ERP Customer No' },
		{ accessorKey: 'contactType', header: 'Type' },
		{ accessorKey: 'contactCategory', header: 'Category' },
		{ accessorKey: 'mobileNo', header: 'Mobile' },
		{ accessorKey: 'emailIds', header: 'Email' },
		{ accessorKey: 'city', header: 'City' },
		{ accessorKey: 'state', header: 'State' },
		{ accessorKey: 'products', header: 'Products' },
		{ accessorKey: 'respCenter', header: 'Resp Center' },
		{
			accessorKey: 'isActive',
			header: 'Status',
			cell: ({ getValue }) => (getValue() ? 'Active' : 'Inactive')
		}
	];
</script>

<svelte:head>
	<title>CRM Contacts | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background pb-20 pt-8">
	<DataGrid
		title="CRM Contacts"
		description="Manage customer and lead contact profiles"
		items={list.items}
		{columns}
		pagination={list.pagination}
		loading={list.loading}
		loadingMore={list.loadingMore}
		bind:searchQuery={list.searchQuery.value}
		mobileCardTitleKey="fullName"
		mobileCardSubtitleKey="companyName"
		onRowClick={handleRowClick}
		showFilters={true}
		bind:filterRules={filterRules}
		onFilterRulesChange={handleFilterRulesChange}
		mobileCardFallback={true}
	>
		{#snippet actions()}
			<Button
				size="sm"
				class="gap-2 shrink-0 bg-primary hover:bg-primary/95 text-primary-foreground font-medium shadow-sm rounded-xl px-4 py-2 transition-all"
				onclick={openAddDialog}
			>
				<Icon name="plus" class="size-3.5" />
				<span>Add Contact</span>
			</Button>
		{/snippet}
	</DataGrid>
</div>

<style>
	:global(.scrollbar-hide::-webkit-scrollbar) {
		display: none;
	}
	:global(.scrollbar-hide) {
		-ms-overflow-style: none;
		scrollbar-width: none;
	}
	:global(.animate-spin) {
		animation: spin 1s linear infinite;
	}
	@keyframes spin {
		from {
			transform: rotate(0deg);
		}
		to {
			transform: rotate(360deg);
		}
	}
</style>
