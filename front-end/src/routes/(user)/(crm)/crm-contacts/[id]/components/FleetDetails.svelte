<script lang="ts">
	import { onMount } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import * as Field from '$lib/components/ui/field';
	import * as Dialog from '$lib/components/ui/dialog';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { DataGrid, type DataGridColumn } from '$lib/components/venUI/datagrid';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Select } from '$lib/components/venUI/select';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { authStore } from '$lib/stores/auth';
	import type { FetchParamsInput } from '$lib/services/graphql/generated/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

	let { contactId }: { contactId: string } = $props();

	let items: any[] = $state([]);
	let loading = $state(false);
	let dialogOpen = $state(false);
	let isSaving = $state(false);
	let isDeleting = $state(false);
	let deleteDialogOpen = $state(false);

	let editingItem: any = $state(resetItem());

	let masterSelectForm = {
		get values() { return editingItem; },
		setTouched: () => {},
		errors: {}
	};

	const purchaseItemParam = $derived.by((): FetchParamsInput | null => {
		const u = $authStore.user;
		if (!u) return null;
		return {
			regions: ['CASING'],
			type: 'FromGroupDetail',
			respCenters: u.respCenter ? [u.respCenter] : [],
			areas: [],
			nos: [],
			from: '',
			to: '',
			reportName: '',
			userCode: u.entityCode ?? '',
			userDepartment: u.department ?? '',
			userSpecialToken: u.userSpecialToken ?? '',
			userType: u.entityType ?? '',
			view: ''
		};
	});

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!) {
			crmMasterItems: getCrmMasterItems(type: $type) {
				id
				name
				parentId
			}
		}
	` as unknown as TypedDocumentNode<any, { type: string }>;

	const GetCrmContactFleetDetailsDocument = buildQuery`
		query GetCrmContactFleetDetails($contactId: UUID!) {
			crmContactFleetDetails: getCrmContactFleetDetails(contactId: $contactId) {
				id
				contactId
				vehicleType
				make
				model
				quantity
				tyreSize
				application
			}
		}
	` as unknown as TypedDocumentNode<any, { contactId: string }>;

	const SaveCrmContactFleetDetailDocument = buildMutation`
		mutation SaveCrmContactFleetDetail($input: CrmContactFleetDetailInput!) {
			saveCrmContactFleetDetail(input: $input) {
				id
			}
		}
	` as unknown as TypedDocumentNode<any, { input: any }>;

	const DeleteCrmContactFleetDetailDocument = buildMutation`
		mutation DeleteCrmContactFleetDetail($id: UUID!) {
			deleteCrmContactFleetDetail(id: $id)
		}
	` as unknown as TypedDocumentNode<any, { id: string }>;

	const columns: DataGridColumn<any>[] = [
		{ accessorKey: 'vehicleType', header: 'Vehicle Type' },
		{ accessorKey: 'make', header: 'Make' },
		{ accessorKey: 'model', header: 'Model' },
		{ accessorKey: 'quantity', header: 'Quantity' },
		{ accessorKey: 'tyreSize', header: 'Tyre Size' },
		{ accessorKey: 'application', header: 'Application' }
	];

	function resetItem() {
		return {
			id: null,
			contactId,
			vehicleType: '',
			make: null,
			model: null,
			quantity: 1,
			tyreSize: null,
			application: null
		};
	}

	let vehicleTypes = $state<any[]>([]);
	let vehicleMakes = $state<any[]>([]);
	let vehicleModels = $state<any[]>([]);
	let applications = $state<any[]>([]);

	let filteredMakes = $derived.by(() => {
		if (!editingItem.vehicleType) return vehicleMakes;
		const t = vehicleTypes.find(x => x.name === editingItem.vehicleType);
		if (!t) return vehicleMakes;
		return vehicleMakes.filter(x => x.parentId === t.id);
	});

	let filteredModels = $derived.by(() => {
		if (!editingItem.make) return vehicleModels;
		const m = vehicleMakes.find(x => x.name === editingItem.make);
		if (!m) return vehicleModels;
		return vehicleModels.filter(x => x.parentId === m.id);
	});

	onMount(() => {
		loadItems();
		loadMasters();
	});

	async function loadMasters() {
		try {
			const [resT, resM, resMo, resA] = await Promise.all([
				graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'VEHICLE_TYPE' } }),
				graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'VEHICLE_MAKE' } }),
				graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'VEHICLE_MODEL' } }),
				graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'APPLICATION' } })
			]);
			
			if (resT.success) vehicleTypes = resT.data?.crmMasterItems || [];
			if (resM.success) vehicleMakes = resM.data?.crmMasterItems || [];
			if (resMo.success) vehicleModels = resMo.data?.crmMasterItems || [];
			if (resA.success) applications = resA.data?.crmMasterItems || [];
		} catch (e) {
			console.error(e);
		}
	}

	async function loadItems() {
		loading = true;
		try {
			const res = await graphqlQuery(GetCrmContactFleetDetailsDocument, { 
				variables: { contactId },
				skipCache: true
			});
			if (res.success && res.data?.crmContactFleetDetails) {
				items = res.data.crmContactFleetDetails;
			}
		} catch (err: any) {
			toast.error('Failed to load fleet details');
		} finally {
			loading = false;
		}
	}

	function onRowClick(item: any) {
		editingItem = { ...item };
		dialogOpen = true;
	}

	function openAddDialog() {
		editingItem = resetItem();
		dialogOpen = true;
	}

	async function saveItem() {
		if (!editingItem.vehicleType) {
			toast.error('Vehicle Type is required');
			return;
		}
		isSaving = true;
		try {
			const input: any = {
				contactId: editingItem.contactId,
				vehicleType: editingItem.vehicleType,
				make: editingItem.make || null,
				model: editingItem.model || null,
				quantity: Number(editingItem.quantity) || 0,
				tyreSize: editingItem.tyreSize || null,
				application: editingItem.application || null
			};
			if (editingItem.id) {
				input.id = editingItem.id;
			}

			const res = await graphqlMutation(SaveCrmContactFleetDetailDocument, { variables: { input } });
			if (res.success && res.data?.saveCrmContactFleetDetail) {
				toast.success('Fleet detail saved successfully.');
				dialogOpen = false;
				loadItems();
			} else {
				toast.error(res.error || 'Failed to save fleet detail');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while saving.');
		} finally {
			isSaving = false;
		}
	}

	async function confirmDelete() {
		if (!editingItem.id) return;
		isDeleting = true;
		try {
			const res = await graphqlMutation(DeleteCrmContactFleetDetailDocument, { variables: { id: editingItem.id } });
			if (res.success && res.data?.deleteCrmContactFleetDetail) {
				toast.success('Fleet detail deleted.');
				deleteDialogOpen = false;
				dialogOpen = false;
				loadItems();
			} else {
				toast.error(res.error || 'Failed to delete fleet detail');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while deleting.');
		} finally {
			isDeleting = false;
		}
	}
</script>

<div class="space-y-4">
	<div class="flex items-center justify-between mb-2">
		<h2 class="text-lg font-bold">Customer Fleet Details</h2>
		<Button size="sm" class="gap-2 bg-primary text-primary-foreground rounded-xl shadow-xs" onclick={openAddDialog}>
			<Icon name="plus" class="size-3.5" />
			Add Fleet Record
		</Button>
	</div>

	<DataGrid
		items={items}
		{columns}
		{loading}
		{onRowClick}
		mobileCardTitleKey="vehicleType"
		mobileCardSubtitleKey="make"
		mobileCardFallback={true}
	/>
</div>

<!-- Add/Edit Modal -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>
				{editingItem.id ? 'Edit Fleet Record' : 'Add Fleet Record'}
			</Dialog.Title>
		</Dialog.Header>

		<div class="grid grid-cols-1 gap-4 py-3 select-none">
			<Field.Field class="w-full">
				<Field.Label for="fleet-type" class="text-muted-foreground">Vehicle Type <span class="text-rose-500">*</span></Field.Label>
				<Field.Content>
					<Select options={vehicleTypes} bind:value={editingItem.vehicleType} valueKey="name" labelKey="name" placeholder="Select Type..." class="rounded-xl w-full h-9" />
				</Field.Content>
			</Field.Field>

			<div class="grid grid-cols-2 gap-4">
				<Field.Field class="w-full">
					<Field.Label for="fleet-make" class="text-muted-foreground">Make</Field.Label>
					<Field.Content>
						<Select options={filteredMakes} bind:value={editingItem.make} valueKey="name" labelKey="name" placeholder="Select Make..." class="rounded-xl w-full h-9" />
					</Field.Content>
				</Field.Field>
				
				<Field.Field class="w-full">
					<Field.Label for="fleet-model" class="text-muted-foreground">Model</Field.Label>
					<Field.Content>
						<Select options={filteredModels} bind:value={editingItem.model} valueKey="name" labelKey="name" placeholder="Select Model..." class="rounded-xl w-full h-9" />
					</Field.Content>
				</Field.Field>
			</div>

			<div class="grid grid-cols-2 gap-4">
				<Field.Field class="w-full">
					<Field.Label for="fleet-qty" class="text-muted-foreground">Quantity</Field.Label>
					<Field.Content>
						<Input id="fleet-qty" type="number" bind:value={editingItem.quantity} class="rounded-xl h-9" min="1" />
					</Field.Content>
				</Field.Field>
				
				<MasterSelect fieldName="tyreSize" masterType="purchaseItems" label="Tyre Size" placeholder="Search sizes..." singleSelect={false} form={masterSelectForm} {purchaseItemParam} />
			</div>

			<Field.Field class="w-full">
				<Field.Label for="fleet-app" class="text-muted-foreground">Application</Field.Label>
				<Field.Content>
					<Select options={applications} bind:value={editingItem.application} valueKey="name" labelKey="name" placeholder="Select Application..." class="rounded-xl w-full h-9" />
				</Field.Content>
			</Field.Field>
		</div>

		<Dialog.Footer class="flex gap-2 justify-between items-center pt-4 border-t">
			<div>
				{#if editingItem.id}
					<Button type="button" variant="destructive" class="rounded-xl px-4" onclick={() => (deleteDialogOpen = true)}>
						Delete
					</Button>
				{/if}
			</div>

			<div class="flex gap-2">
				<Button type="button" variant="outline" disabled={isSaving} onclick={() => (dialogOpen = false)} class="rounded-xl">
					Cancel
				</Button>
				<Button type="button" disabled={!editingItem.vehicleType || isSaving} onclick={saveItem} class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 shadow-lg">
					{#if isSaving}<Loader2 class="size-4 animate-spin shrink-0" />{/if}
					{editingItem.id ? 'Save Changes' : 'Add Record'}
				</Button>
			</div>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Delete Confirmation -->
<Dialog.Root bind:open={deleteDialogOpen}>
	<Dialog.Content class="sm:max-w-sm">
		<Dialog.Header>
			<Dialog.Title>Confirm Delete</Dialog.Title>
		</Dialog.Header>
		<p class="py-4 text-muted-foreground text-sm">Are you sure you want to delete this fleet record?</p>
		<Dialog.Footer class="flex gap-2 justify-end pt-2 border-t">
			<Button variant="outline" onclick={() => (deleteDialogOpen = false)} class="rounded-xl">Cancel</Button>
			<Button variant="destructive" onclick={confirmDelete} disabled={isDeleting} class="rounded-xl">
				{#if isDeleting}<Loader2 class="size-4 animate-spin shrink-0 mr-2" />{/if} Delete
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
