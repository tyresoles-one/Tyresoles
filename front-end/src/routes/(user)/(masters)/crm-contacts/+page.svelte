<script lang="ts">
	import { onMount } from 'svelte';
	import { usePaginatedList } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import * as Dialog from '$lib/components/ui/dialog';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { TableActions } from '$lib/components/venUI/tableActions';
	import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { Switch } from '$lib/components/ui/switch';
	import { Select } from '$lib/components/venUI/select';
	import { graphqlQuery, graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import { authStore } from '$lib/stores/auth';
	import Loader2 from '@lucide/svelte/icons/loader-2';

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
		tags?: string | null;
		isActive: boolean;
		createdBy?: string | null;
		assignedTo?: string | null;
	};

	type CrmMasterItem = {
		id: number;
		name: string;
	};

	type CrmMasterItemsResult = {
		crmMasterItems: CrmMasterItem[];
	};

	type GetCrmContactsResult = {
		crmContacts: CrmContact[];
	};

	type SaveCrmContactResult = {
		saveCrmContact: CrmContact;
	};

	type DeleteCrmContactResult = {
		deleteCrmContact: boolean;
	};

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!) {
			crmMasterItems: getCrmMasterItems(type: $type) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<CrmMasterItemsResult, { type: string }>;

	const GetCrmContactsDocument = buildQuery`
		query GetCrmContacts($where: CrmContactFilterInput, $order: [CrmContactSortInput!]) {
			crmContacts: getCrmContacts(where: $where, order: $order) {
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
				tags
				isActive
				createdBy
				assignedTo
			}
		}
	` as unknown as TypedDocumentNode<GetCrmContactsResult, { where?: any; order?: any }>;

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
				tags
				isActive
				createdBy
				assignedTo
			}
		}
	` as unknown as TypedDocumentNode<SaveCrmContactResult, { input: any }>;

	const DeleteCrmContactDocument = buildMutation`
		mutation DeleteCrmContact($id: UUID!) {
			deleteCrmContact(id: $id)
		}
	` as unknown as TypedDocumentNode<DeleteCrmContactResult, { id: string }>;

	const list = usePaginatedList<CrmContact>({
		query: GetCrmContactsDocument,
		dataPath: 'crmContacts',
		itemsPath: 'crmContacts',
		countPath: 'crmContacts.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			where: term ? {
				or: [
					{ fullName: { contains: term } },
					{ companyName: { contains: term } },
					{ mobileNo: { contains: term } },
					{ emailIds: { contains: term } },
					{ city: { contains: term } },
					{ tags: { contains: term } }
				]
			} : null
		}),
		serverVariableAllowlist: ['where', 'order']
	});

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
			}
		} catch (err) {
			console.error('Failed to load contact types', err);
		}
	}

	onMount(() => {
		loadContactTypes();
	});

	// Dialog editing states
	let dialogOpen = $state(false);
	let dialogMode = $state<'add' | 'edit'>('add');
	let isSaving = $state(false);

	let editingContact = $state<Partial<CrmContact>>({
		id: undefined,
		contactType: '',
		fullName: '',
		companyName: '',
		mobileNo: '',
		mobileNo2: '',
		emailIds: '',
		isDecisionMaker: false,
		address: '',
		city: '',
		state: '',
		respCenter: '',
		erpCustomerNos: '',
		erpAreaCodes: '',
		tags: '',
		isActive: true,
		createdBy: '',
		assignedTo: ''
	});

	// Wrapper form object for MasterSelect compatibility
	const masterSelectForm = {
		get values() { return editingContact; },
		setTouched(name: string) {}
	};

	// Dialog deletion states
	let deleteDialogOpen = $state(false);
	let isDeleting = $state(false);

	function openAddDialog() {
		dialogMode = 'add';
		editingContact = {
			id: undefined,
			contactType: '',
			fullName: '',
			companyName: '',
			mobileNo: '',
			mobileNo2: '',
			emailIds: '',
			isDecisionMaker: false,
			address: '',
			city: '',
			state: '',
			respCenter: '',
			erpCustomerNos: '',
			erpAreaCodes: '',
			tags: '',
			isActive: true,
			createdBy: authStore.get().username || '',
			assignedTo: ''
		};
		dialogOpen = true;
	}

	function openEditDialog(contact: CrmContact) {
		dialogMode = 'edit';
		editingContact = { ...contact };
		dialogOpen = true;
	}

	function handleRowClick(contact: CrmContact) {
		openEditDialog(contact);
	}

	async function saveContact() {
		const name = editingContact.fullName?.trim();
		if (!name) {
			toast.error('Full Name is required.');
			return;
		}

		isSaving = true;
		try {
			// clean input fields
			const input = {
				id: editingContact.id || null,
				contactType: editingContact.contactType || null,
				fullName: name,
				companyName: editingContact.companyName || null,
				mobileNo: editingContact.mobileNo || null,
				mobileNo2: editingContact.mobileNo2 || null,
				emailIds: editingContact.emailIds || null,
				isDecisionMaker: !!editingContact.isDecisionMaker,
				address: editingContact.address || null,
				city: editingContact.city || null,
				state: editingContact.state || null,
				respCenter: editingContact.respCenter || null,
				erpCustomerNos: editingContact.erpCustomerNos || null,
				erpAreaCodes: editingContact.erpAreaCodes || null,
				tags: editingContact.tags || null,
				isActive: !!editingContact.isActive,
				createdBy: editingContact.createdBy || null,
				assignedTo: editingContact.assignedTo || null
			};

			const res = await graphqlMutation<SaveCrmContactResult>(SaveCrmContactDocument, {
				variables: { input }
			});

			if (res.success && res.data?.saveCrmContact) {
				toast.success(`Contact "${name}" saved successfully.`);
				dialogOpen = false;
				list.onRefresh();
			} else {
				toast.error(res.error || 'Failed to save contact');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while saving.');
		} finally {
			isSaving = false;
		}
	}

	async function confirmDelete() {
		if (!editingContact.id) return;
		isDeleting = true;
		try {
			const res = await graphqlMutation<DeleteCrmContactResult>(DeleteCrmContactDocument, {
				variables: { id: editingContact.id }
			});

			if (res.success && res.data?.deleteCrmContact) {
				toast.success(`Contact deleted successfully.`);
				deleteDialogOpen = false;
				dialogOpen = false;
				list.onRefresh();
			} else {
				toast.error(res.error || 'Failed to delete contact');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while deleting.');
		} finally {
			isDeleting = false;
		}
	}

	const columns: DataGridColumn<CrmContact>[] = [
		{ accessorKey: 'fullName', header: 'Full Name' },
		{ accessorKey: 'companyName', header: 'Company' },
		{ accessorKey: 'contactType', header: 'Type' },
		{ accessorKey: 'mobileNo', header: 'Mobile' },
		{ accessorKey: 'emailIds', header: 'Email' },
		{ accessorKey: 'city', header: 'City' },
		{ accessorKey: 'state', header: 'State' },
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
		showFilters={false}
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

<!-- Add/Edit Modal -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-2xl">
		<Dialog.Header>
			<Dialog.Title>
				{dialogMode === 'add' ? 'Add Contact' : 'Edit Contact'}
			</Dialog.Title>
		</Dialog.Header>

		<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 py-3 max-h-[65vh] overflow-y-auto px-1 select-none">
			<Field.Field class="w-full">
				<Field.Label for="contact-fullname" class="text-muted-foreground">Full Name <span class="text-rose-500">*</span></Field.Label>
				<Field.Content>
					<Input
						id="contact-fullname"
						bind:value={editingContact.fullName}
						placeholder="Enter contact full name"
						autocomplete="off"
						class="rounded-xl h-9"
					/>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label for="contact-company" class="text-muted-foreground">Company Name</Field.Label>
				<Field.Content>
					<Input
						id="contact-company"
						bind:value={editingContact.companyName}
						placeholder="Enter company name"
						autocomplete="off"
						class="rounded-xl h-9"
					/>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label for="contact-type" class="text-muted-foreground">Contact Type</Field.Label>
				<Field.Content>
					<Select
						options={contactTypes}
						bind:value={editingContact.contactType}
						placeholder="Select type..."
						valueKey="value"
						labelKey="label"
						class="rounded-xl w-full h-9"
					/>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label for="contact-mobile" class="text-muted-foreground">Mobile No</Field.Label>
				<Field.Content>
					<Input
						id="contact-mobile"
						bind:value={editingContact.mobileNo}
						placeholder="Enter mobile number"
						autocomplete="off"
						class="rounded-xl h-9"
					/>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label for="contact-mobile2" class="text-muted-foreground">Alternative Mobile No</Field.Label>
				<Field.Content>
					<Input
						id="contact-mobile2"
						bind:value={editingContact.mobileNo2}
						placeholder="Enter alt mobile number"
						autocomplete="off"
						class="rounded-xl h-9"
					/>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full">
				<Field.Label class="text-muted-foreground">Email IDs</Field.Label>
				<Field.Content>
					<div class="flex flex-col gap-2 w-full">
						<div class="flex flex-wrap gap-1.5 p-1.5 border border-input rounded-xl bg-transparent min-h-9 items-center focus-within:ring-1 focus-within:ring-ring">
							{#each (editingContact.emailIds ? editingContact.emailIds.split(',').map(e => e.trim()).filter(Boolean) : []) as email}
								<span class="inline-flex items-center gap-1 bg-primary/10 text-primary text-xs px-2 py-0.5 rounded-lg border border-primary/20">
									{email}
									<button
										type="button"
										onclick={() => {
											const current = editingContact.emailIds ? editingContact.emailIds.split(',').map(e => e.trim()).filter(Boolean) : [];
											editingContact.emailIds = current.filter(e => e !== email).join(', ');
										}}
										class="hover:text-destructive transition-colors"
									>
										<Icon name="x" class="size-3" />
									</button>
								</span>
							{/each}
							<input
								type="text"
								placeholder={editingContact.emailIds ? "" : "Type email and press Enter..."}
								onkeydown={(e) => {
									if (e.key === 'Enter' || e.key === ',') {
										e.preventDefault();
										const target = e.currentTarget as HTMLInputElement;
										const email = target.value.trim();
										if (email) {
											const current = editingContact.emailIds ? editingContact.emailIds.split(',').map(e => e.trim()).filter(Boolean) : [];
											if (!current.includes(email)) {
												editingContact.emailIds = [...current, email].join(', ');
											}
										}
										target.value = '';
									}
								}}
								class="flex-1 min-w-[120px] bg-transparent border-0 outline-none ring-0 text-sm p-0 h-6 focus:ring-0 focus:outline-none"
							/>
						</div>
					</div>
				</Field.Content>
			</Field.Field>

			<Field.Field class="w-full sm:col-span-2">
				<Field.Label for="contact-address" class="text-muted-foreground">Address</Field.Label>
				<Field.Content>
					<Textarea
						id="contact-address"
						bind:value={editingContact.address}
						placeholder="Enter street address"
						class="rounded-xl min-h-[60px]"
					/>
				</Field.Content>
			</Field.Field>

			<MasterSelect
				fieldName="city"
				masterType="postCodes"
				label="City"
				placeholder="Search PIN or City..."
				singleSelect={true}
				form={masterSelectForm}
				onPicked={({ value, meta }) => {
					if (meta) {
						editingContact.city = String(meta.city);
						editingContact.state = String(meta.stateCode);
					}
				}}
			/>

			<MasterSelect
				fieldName="state"
				masterType="states"
				label="State"
				placeholder="Select state..."
				singleSelect={true}
				form={masterSelectForm}
			/>

			<MasterSelect
				fieldName="respCenter"
				masterType="respCenters"
				label="Responsibility Center"
				placeholder="Select center..."
				singleSelect={true}
				form={masterSelectForm}
			/>

			<MasterSelect
				fieldName="erpCustomerNos"
				masterType="customers"
				label="ERP Customer Nos"
				placeholder="Select customer numbers..."
				singleSelect={false}
				respCenterOverride={editingContact.respCenter}
				form={masterSelectForm}
			/>

			<MasterSelect
				fieldName="erpAreaCodes"
				masterType="areas"
				label="ERP Area Codes"
				placeholder="Select area codes..."
				singleSelect={false}
				form={masterSelectForm}
			/>

			<Field.Field class="w-full">
				<Field.Label class="text-muted-foreground">Tags</Field.Label>
				<Field.Content>
					<div class="flex flex-col gap-2 w-full">
						<div class="flex flex-wrap gap-1.5 p-1.5 border border-input rounded-xl bg-transparent min-h-9 items-center focus-within:ring-1 focus-within:ring-ring">
							{#each (editingContact.tags ? editingContact.tags.split(',').map(t => t.trim()).filter(Boolean) : []) as tag}
								<span class="inline-flex items-center gap-1 bg-primary/10 text-primary text-xs px-2 py-0.5 rounded-lg border border-primary/20">
									{tag}
									<button
										type="button"
										onclick={() => {
											const current = editingContact.tags ? editingContact.tags.split(',').map(t => t.trim()).filter(Boolean) : [];
											editingContact.tags = current.filter(t => t !== tag).join(', ');
										}}
										class="hover:text-destructive transition-colors"
									>
										<Icon name="x" class="size-3" />
									</button>
								</span>
							{/each}
							<input
								type="text"
								placeholder={editingContact.tags ? "" : "Type tag and press Enter..."}
								onkeydown={(e) => {
									if (e.key === 'Enter' || e.key === ',') {
										e.preventDefault();
										const target = e.currentTarget as HTMLInputElement;
										const tag = target.value.trim();
										if (tag) {
											const current = editingContact.tags ? editingContact.tags.split(',').map(t => t.trim()).filter(Boolean) : [];
											if (!current.includes(tag)) {
												editingContact.tags = [...current, tag].join(', ');
											}
										}
										target.value = '';
									}
								}}
								class="flex-1 min-w-[120px] bg-transparent border-0 outline-none ring-0 text-sm p-0 h-6 focus:ring-0 focus:outline-none"
							/>
						</div>
						
						{#if uniqueTags.length > 0}
							<div class="flex flex-wrap gap-1.5 items-center">
								<span class="text-[10px] text-muted-foreground uppercase font-semibold">Suggestions:</span>
								{#each uniqueTags as tag}
									{@const current = editingContact.tags ? editingContact.tags.split(',').map(t => t.trim()).filter(Boolean) : []}
									{#if !current.includes(tag)}
										<button
											type="button"
											onclick={() => {
												editingContact.tags = [...current, tag].join(', ');
											}}
											class="text-[10px] bg-muted hover:bg-primary/10 hover:text-primary border border-border rounded px-1.5 py-0.5 transition-colors"
										>
											+ {tag}
										</button>
									{/if}
								{/each}
							</div>
						{/if}
					</div>
				</Field.Content>
			</Field.Field>

			<MasterSelect
				fieldName="assignedTo"
				masterType="users"
				label="Assigned To"
				placeholder="Select assignee..."
				singleSelect={true}
				form={masterSelectForm}
			/>

			<div class="flex flex-row gap-8 items-center pt-2 sm:col-span-2">
				<label class="flex items-center gap-3 cursor-pointer select-none">
					<Switch bind:checked={editingContact.isDecisionMaker} />
					<span class="text-sm font-medium text-muted-foreground">Decision Maker</span>
				</label>

				<label class="flex items-center gap-3 cursor-pointer select-none">
					<Switch bind:checked={editingContact.isActive} />
					<span class="text-sm font-medium text-muted-foreground">Active</span>
				</label>
			</div>
		</div>

		<Dialog.Footer class="flex gap-2 justify-between items-center pt-4 border-t w-full">
			<div>
				{#if dialogMode === 'edit'}
					<Button
						type="button"
						variant="destructive"
						class="rounded-xl px-4"
						onclick={() => (deleteDialogOpen = true)}
					>
						Delete
					</Button>
				{/if}
			</div>

			<div class="flex gap-2">
				<Button
					type="button"
					variant="outline"
					disabled={isSaving}
					onclick={() => (dialogOpen = false)}
					class="rounded-xl"
				>
					Cancel
				</Button>
				<Button
					type="button"
					disabled={!editingContact.fullName || isSaving}
					onclick={saveContact}
					class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 shadow-lg hover:shadow-indigo-500/10"
				>
					{#if isSaving}
						<Loader2 class="size-4 animate-spin shrink-0" />
					{/if}
					{dialogMode === 'add' ? 'Create' : 'Save Changes'}
				</Button>
			</div>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Delete Confirmation Modal -->
<Dialog.Root bind:open={deleteDialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>Delete Contact</Dialog.Title>
		</Dialog.Header>

		<div class="py-3 select-none">
			<p class="text-sm text-muted-foreground leading-relaxed">
				Are you sure you want to delete <strong class="text-foreground">"{editingContact.fullName}"</strong>? This action cannot be undone.
			</p>
		</div>

		<Dialog.Footer class="flex gap-2 justify-end pt-4 border-t">
			<Button
				type="button"
				variant="outline"
				disabled={isDeleting}
				onclick={() => (deleteDialogOpen = false)}
				class="rounded-xl"
			>
				Cancel
			</Button>
			<Button
				type="button"
				disabled={isDeleting}
				onclick={confirmDelete}
				class="bg-rose-600 hover:bg-rose-500 text-white rounded-xl gap-2 shadow-lg hover:shadow-rose-500/10"
			>
				{#if isDeleting}
					<Loader2 class="size-4 animate-spin shrink-0" />
				{/if}
				Delete Contact
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

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
