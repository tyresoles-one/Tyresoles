<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { Switch } from '$lib/components/ui/switch';
	import { Select } from '$lib/components/venUI/select';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import FleetDetails from './components/FleetDetails.svelte';

	const id = $page.params.id;
	let isNew = $state(id === 'new');
	let loading = $state(!isNew);
	let isSaving = $state(false);
	let isDeleting = $state(false);
	
	let activeTab: 'general' | 'fleet' = $state('general');

	let editingContact: any = $state({
		id: isNew ? null : id,
		fullName: '',
		contactType: null,
		contactCategory: null,
		companyName: null,
		mobileNo: null,
		mobileNo2: null,
		emailIds: null,
		isDecisionMaker: false,
		address: null,
		city: null,
		state: null,
		respCenter: null,
		erpCustomerNos: null,
		erpAreaCodes: null,
		products: null,
		tags: null,
		isActive: true,
		createdBy: null
	});

	let masterSelectForm = {
		get values() { return editingContact; },
		setTouched: () => {},
		errors: {}
	};
	let contactTypes = $state<{ value: string; label: string }[]>([]);
	let contactCategories = $state<{ value: string; label: string }[]>([]);
	const uniqueTags = ['VIP', 'Hot', 'Cold', 'Follow Up', 'Key Account'];

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<any, { type: string; where?: any }>;

	const GetCrmContactByIdDocument = buildQuery`
		query GetCrmContactById($id: UUID!) {
			crmContact: getCrmContactById(id: $id) {
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
		}
	` as unknown as TypedDocumentNode<any, { id: string }>;

	const SaveCrmContactDocument = buildMutation`
		mutation SaveCrmContact($input: CrmContactInput!) {
			saveCrmContact(input: $input) {
				id
				fullName
			}
		}
	` as unknown as TypedDocumentNode<any, { input: any }>;

	const DeleteCrmContactDocument = buildMutation`
		mutation DeleteCrmContact($id: UUID!) {
			deleteCrmContact(id: $id)
		}
	` as unknown as TypedDocumentNode<any, { id: string }>;

	onMount(async () => {
		loadContactTypes();
		loadContactCategories();
		if (!isNew) {
			await loadContact();
		}
	});

	async function loadContactTypes() {
		try {
			const res = await graphqlQuery(GetCrmMasterItemsDocument, {
				variables: { type: 'CONTACT_TYPE' }
			});
			if (res.success && res.data?.crmMasterItems) {
				contactTypes = res.data.crmMasterItems.map((x: any) => ({
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

	async function loadContactCategories() {
		try {
			const res = await graphqlQuery(GetCrmMasterItemsDocument, {
				variables: { type: 'CONTACT_CATEGORY' }
			});
			if (res.success && res.data?.crmMasterItems) {
				contactCategories = res.data.crmMasterItems.map((x: any) => ({
					value: x.name,
					label: x.name
				}));
			} else if (!res.success) {
				console.error('Failed to load contact categories:', res.error);
				toast.error('Failed to load Contact Categories');
			}
		} catch (err) {
			console.error('Failed to load contact categories', err);
			toast.error('Failed to load Contact Categories');
		}
	}

	async function loadContact() {
		try {
			loading = true;
			const res = await graphqlQuery(GetCrmContactByIdDocument, { variables: { id } });
			if (res.success && res.data?.crmContact) {
				editingContact = { ...res.data.crmContact };
			} else {
				toast.error('Failed to load contact');
				goto('/crm-contacts');
			}
		} catch (err: any) {
			toast.error(err.message || 'Error loading contact');
		} finally {
			loading = false;
		}
	}

	async function saveContact() {
		if (!editingContact.fullName) {
			toast.error('Full Name is required');
			return;
		}
		isSaving = true;
		try {
			const input = {
				id: editingContact.id,
				fullName: editingContact.fullName,
				contactType: editingContact.contactType || null,
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
				products: editingContact.products || null,
				tags: editingContact.tags || null,
				isActive: !!editingContact.isActive,
				createdBy: editingContact.createdBy || null
			};

			const res = await graphqlMutation(SaveCrmContactDocument, { variables: { input } });
			if (res.success && res.data?.saveCrmContact) {
				toast.success('Contact saved successfully.');
				if (isNew) {
					goto(`/crm-contacts/${res.data.saveCrmContact.id}`);
				}
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
		if (!confirm('Are you sure you want to delete this contact?')) return;
		
		isDeleting = true;
		try {
			const res = await graphqlMutation(DeleteCrmContactDocument, { variables: { id } });
			if (res.success && res.data?.deleteCrmContact) {
				toast.success('Contact deleted successfully.');
				goto('/crm-contacts');
			} else {
				toast.error(res.error || 'Failed to delete contact');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while deleting.');
		} finally {
			isDeleting = false;
		}
	}
</script>

<svelte:head>
	<title>{isNew ? 'New Contact' : editingContact.fullName} | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background pb-20 pt-8">
	<div class="max-w-6xl mx-auto px-4 md:px-6">
		<!-- Header -->
		<div class="flex items-center gap-4 mb-6">
			<Button variant="ghost" size="sm" class="gap-2 px-0 text-muted-foreground hover:bg-transparent hover:text-foreground" onclick={() => goto('/crm-contacts')}>
				<Icon name="arrow-left" class="size-4" /> Back
			</Button>
			<h1 class="text-2xl font-bold">{isNew ? 'New Contact' : editingContact.fullName}</h1>
			{#if !isNew && editingContact.companyName}
				<span class="text-muted-foreground text-sm mt-1">{editingContact.companyName}</span>
			{/if}
			<div class="ml-auto flex gap-2">
				{#if !isNew}
					<Button variant="destructive" size="sm" class="gap-2 rounded-xl shadow-xs" onclick={confirmDelete} disabled={isDeleting}>
						{#if isDeleting}<Loader2 class="size-3 animate-spin shrink-0" />{:else}<Icon name="trash" class="size-3.5" />{/if}
						Delete
					</Button>
				{/if}
				<Button size="sm" class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl shadow-xs gap-2" onclick={saveContact} disabled={isSaving || !editingContact.fullName}>
					{#if isSaving}<Loader2 class="size-3 animate-spin shrink-0" />{:else}<Icon name="save" class="size-3.5" />{/if}
					Save Contact
				</Button>
			</div>
		</div>

		{#if loading}
			<div class="flex flex-col items-center justify-center h-[50vh] gap-3 text-muted-foreground">
				<Loader2 class="size-8 animate-spin" />
				<p>Loading contact...</p>
			</div>
		{:else}
			<div class="bg-card border border-border rounded-2xl overflow-hidden shadow-xs">
				<!-- Tab Bar -->
				<div class="flex border-b border-border bg-muted/20 overflow-x-auto scrollbar-hide">
					<button
						onclick={() => (activeTab = 'general')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'general' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="user" class="size-4" />
						General Info
					</button>
					{#if !isNew}
						<button
							onclick={() => (activeTab = 'fleet')}
							class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'fleet' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
						>
							<Icon name="truck" class="size-4" />
							Fleet Details
						</button>
					{/if}
				</div>

				<!-- Tab Content -->
				<div class="p-6">
					{#if activeTab === 'general'}
						<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
							<!-- Identity -->
							<div class="space-y-4 lg:col-span-1">
								<h3 class="text-sm font-semibold uppercase tracking-wider text-muted-foreground mb-2">Identity</h3>
								<Field.Field class="w-full">
									<Field.Label for="contact-fullname" class="text-muted-foreground">Full Name <span class="text-rose-500">*</span></Field.Label>
									<Field.Content>
										<Input id="contact-fullname" bind:value={editingContact.fullName} placeholder="Enter contact full name" class="rounded-xl h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label for="contact-company" class="text-muted-foreground">Company Name</Field.Label>
									<Field.Content>
										<Input id="contact-company" bind:value={editingContact.companyName} placeholder="Enter company name" class="rounded-xl h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label for="contact-type" class="text-muted-foreground">Contact Type</Field.Label>
									<Field.Content>
										<Select options={contactTypes} bind:value={editingContact.contactType} placeholder="Select type..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label for="contact-category" class="text-muted-foreground">Contact Category</Field.Label>
									<Field.Content>
										<Select options={contactCategories} bind:value={editingContact.contactCategory} placeholder="Select category..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
									</Field.Content>
								</Field.Field>
								
								<div class="flex flex-row gap-8 items-center pt-2">
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

							<!-- Contact & Address -->
							<div class="space-y-4 lg:col-span-1">
								<h3 class="text-sm font-semibold uppercase tracking-wider text-muted-foreground mb-2">Contact & Address</h3>
								<Field.Field class="w-full">
									<Field.Label for="contact-mobile" class="text-muted-foreground">Mobile No</Field.Label>
									<Field.Content>
										<Input id="contact-mobile" bind:value={editingContact.mobileNo} placeholder="Enter mobile number" class="rounded-xl h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label for="contact-mobile2" class="text-muted-foreground">Alt Mobile No</Field.Label>
									<Field.Content>
										<Input id="contact-mobile2" bind:value={editingContact.mobileNo2} placeholder="Enter alt mobile number" class="rounded-xl h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label class="text-muted-foreground">Email IDs</Field.Label>
									<Field.Content>
										<Input bind:value={editingContact.emailIds} placeholder="comma, separated, emails" class="rounded-xl h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<Field.Label for="contact-address" class="text-muted-foreground">Address</Field.Label>
									<Field.Content>
										<Textarea id="contact-address" bind:value={editingContact.address} placeholder="Enter street address" class="rounded-xl min-h-[60px]" />
									</Field.Content>
								</Field.Field>
							</div>

							<!-- Business Data -->
							<div class="space-y-4 lg:col-span-1">
								<h3 class="text-sm font-semibold uppercase tracking-wider text-muted-foreground mb-2">Business Settings</h3>
								
								<MasterSelect fieldName="city" masterType="postCodes" label="City" placeholder="Search PIN or City..." singleSelect={true} form={masterSelectForm} onPicked={({ value, meta }) => { if (meta) { editingContact.city = String(meta.city); editingContact.state = String(meta.stateCode); } }} />
								<MasterSelect fieldName="state" masterType="states" label="State" placeholder="Select state..." singleSelect={true} form={masterSelectForm} />
								<MasterSelect fieldName="respCenter" masterType="respCenters" label="Responsibility Center" placeholder="Select center..." singleSelect={true} form={masterSelectForm} />
								<MasterSelect fieldName="erpCustomerNos" masterType="customers" label="ERP Customer Nos" placeholder="Select customer numbers..." singleSelect={false} respCenterOverride={editingContact.respCenter} form={masterSelectForm} />
								<MasterSelect fieldName="erpAreaCodes" masterType="areas" label="ERP Area Codes" placeholder="Select area codes..." singleSelect={false} form={masterSelectForm} />
								
								<MasterSelect fieldName="products" masterType="items" label="Products" placeholder="Select products..." singleSelect={false} form={masterSelectForm} />

								<Field.Field class="w-full">
									<Field.Label class="text-muted-foreground">Tags</Field.Label>
									<Field.Content>
										<div class="flex flex-col gap-2">
											<div class="flex flex-wrap gap-2">
												{#each (editingContact.tags || '').split(',').map(t => t.trim()).filter(Boolean) as tag}
													<span class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-secondary text-secondary-foreground text-xs font-medium group">
														{tag}
														<button type="button" class="hover:text-destructive transition-colors" onclick={() => {
															editingContact.tags = (editingContact.tags || '').split(',').map(t => t.trim()).filter(Boolean).filter(t => t !== tag).join(', ');
														}}>
															<Icon name="x" class="size-3" />
														</button>
													</span>
												{/each}
											</div>
											<Input 
												placeholder="Type tag and press Enter..." 
												class="rounded-xl h-9" 
												onkeydown={(e: KeyboardEvent & { currentTarget: HTMLInputElement }) => {
													if (e.key === 'Enter') {
														e.preventDefault();
														const val = e.currentTarget.value.trim();
														if (val) {
															const currentTags = (editingContact.tags || '').split(',').map(t => t.trim()).filter(Boolean);
															if (!currentTags.includes(val)) {
																editingContact.tags = [...currentTags, val].join(', ');
															}
															e.currentTarget.value = '';
														}
													}
												}} 
											/>
										</div>
									</Field.Content>
								</Field.Field>
							</div>
						</div>
					{:else if activeTab === 'fleet'}
						{#if !isNew}
							<FleetDetails contactId={id} />
						{/if}
					{/if}
				</div>
			</div>
		{/if}
	</div>
</div>
