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
		createdBy: null,
		createdAt: null,
		modifiedBy: null,
		modifiedAt: null,
		prefLanguage: null,
		location: null,
		leadSourceType: 'Manual',
		leadSourceChannel: null,
		sourceUrl: null,
		qualityScore: null,
		harvestedAt: null
	});

	let leadSourceTypeOptions = $state<{ value: string; label: string }[]>([
		{ value: 'Manual', label: 'Manual' },
		{ value: 'Automated', label: 'Automated' },
		{ value: 'Inbound', label: 'Inbound Inquiry' },
		{ value: 'Referral', label: 'Referral' },
		{ value: 'Trade Show', label: 'Trade Show / Event' },
		{ value: 'Cold Outreach', label: 'Cold Outreach' },
		{ value: 'Campaign', label: 'Campaign (WhatsApp/Email)' },
		{ value: 'Tender', label: 'Tender / Contract' },
		{ value: 'Other', label: 'Other' }
	]);

	let leadSourceChannels = $state<{ value: string; label: string }[]>([
		{ value: 'Web-Harvester', label: 'Web-Harvester' },
		{ value: 'Google-Maps', label: 'Google-Maps' },
		{ value: 'IndiaMART', label: 'IndiaMART' },
		{ value: 'TradeIndia', label: 'TradeIndia' },
		{ value: 'Justdial', label: 'Justdial' },
		{ value: 'Direct-Call', label: 'Direct-Call' },
		{ value: 'WhatsApp', label: 'WhatsApp' },
		{ value: 'Field-Visit', label: 'Field-Visit' },
		{ value: 'Website-Form', label: 'Website-Form' },
		{ value: 'Referral', label: 'Referral' },
		{ value: 'Directory', label: 'Industry Directory' }
	]);

	let masterSelectForm = {
		get values() { return editingContact; },
		setTouched: () => {},
		errors: {}
	};
	let contactTypes = $state<{ value: string; label: string }[]>([]);
	let contactCategories = $state<{ value: string; label: string }[]>([]);
	let languages = $state<{ value: string; label: string }[]>([]);
	let isCapturingLocation = $state(false);
	const uniqueTags = ['VIP', 'Hot', 'Cold', 'Follow Up', 'Key Account'];

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				code
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
				createdAt
				modifiedBy
				modifiedAt
				prefLanguage
				location
				leadSourceType
				leadSourceChannel
				sourceUrl
				qualityScore
				harvestedAt
			}
		}
	` as unknown as TypedDocumentNode<any, { id: string }>;

	const SaveCrmContactDocument = buildMutation`
		mutation SaveCrmContact($input: CrmContactInput!) {
			saveCrmContact(input: $input) {
				id
				fullName
				prefLanguage
				location
				leadSourceType
				leadSourceChannel
				createdBy
				createdAt
				modifiedBy
				modifiedAt
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
		loadLanguages();
		loadLeadSources();
		loadLeadSourceChannels();
		if (!isNew) {
			await loadContact();
		}
	});

	async function loadLeadSources() {
		try {
			const res = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
				variables: { type: 'SOURCE' }
			});
			if (res.success && res.data?.crmMasterItems?.length) {
				leadSourceTypeOptions = res.data.crmMasterItems.map((x: any) => ({
					value: x.name,
					label: x.name
				}));
			}
		} catch (err) {
			console.error('Failed to load lead sources', err);
		}
	}

	async function loadLeadSourceChannels() {
		try {
			const res = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
				variables: { type: 'SOURCE_CHANNEL' }
			});
			if (res.success && res.data?.crmMasterItems?.length) {
				leadSourceChannels = res.data.crmMasterItems.map((x: any) => ({
					value: x.name,
					label: x.code ? `${x.name} (${x.code})` : x.name
				}));
			}
		} catch (err) {
			console.error('Failed to load lead source channels', err);
		}
	}

	async function loadLanguages() {
		try {
			const res = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
				variables: { type: 'LANGUAGE' }
			});
			if (res.success && res.data?.crmMasterItems) {
				languages = res.data.crmMasterItems.map((x: any) => ({
					value: x.name,
					label: x.code ? `${x.name} (${x.code})` : x.name
				}));
			}
		} catch (err) {
			console.error('Failed to load languages', err);
		}
	}

	function captureLiveLocation() {
		if (!('geolocation' in navigator)) {
			toast.error('Geolocation is not supported by your browser.');
			return;
		}
		isCapturingLocation = true;
		navigator.geolocation.getCurrentPosition(
			(pos) => {
				const lat = pos.coords.latitude.toFixed(6);
				const lng = pos.coords.longitude.toFixed(6);
				editingContact.location = `${lat}, ${lng}`;
				toast.success(`Live GPS location captured: ${editingContact.location}`);
				isCapturingLocation = false;
			},
			(err) => {
				console.error('Error obtaining location', err);
				toast.error(err.message || 'Unable to retrieve live location. Ensure location permissions are granted.');
				isCapturingLocation = false;
			},
			{ enableHighAccuracy: true, timeout: 15000, maximumAge: 0 }
		);
	}

	async function loadContactTypes() {
		try {
			const res = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
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
			const res = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
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
			const res = await graphqlQuery<any>(GetCrmContactByIdDocument, { variables: { id } });
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
				createdBy: editingContact.createdBy || null,
				prefLanguage: editingContact.prefLanguage || null,
				location: editingContact.location || null,
				leadSourceType: editingContact.leadSourceType || 'Manual',
				leadSourceChannel: editingContact.leadSourceChannel || null
			};

			const res = await graphqlMutation<any>(SaveCrmContactDocument, { variables: { input } });
			if (res.success && res.data?.saveCrmContact) {
				toast.success('Contact saved successfully.');
				if (isNew) {
					goto(`/crm-contacts/${res.data.saveCrmContact.id}`);
				} else {
					editingContact.modifiedAt = res.data.saveCrmContact.modifiedAt;
					editingContact.modifiedBy = res.data.saveCrmContact.modifiedBy;
					editingContact.createdAt = res.data.saveCrmContact.createdAt;
					editingContact.createdBy = res.data.saveCrmContact.createdBy;
					editingContact.leadSourceType = res.data.saveCrmContact.leadSourceType;
					editingContact.leadSourceChannel = res.data.saveCrmContact.leadSourceChannel;
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
			const res = await graphqlMutation<any>(DeleteCrmContactDocument, { variables: { id } });
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
			<div>
				<div class="flex items-center gap-3">
					<h1 class="text-2xl font-bold">{isNew ? 'New Contact' : editingContact.fullName}</h1>
					{#if !isNew && editingContact.leadSourceType}
						<span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium {editingContact.leadSourceType === 'Automated' ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/60 dark:text-amber-300 border border-amber-300/40' : 'bg-blue-100 text-blue-800 dark:bg-blue-950/60 dark:text-blue-300 border border-blue-300/40'}">
							{editingContact.leadSourceType}
						</span>
					{/if}
				</div>
				{#if !isNew && editingContact.companyName}
					<span class="text-muted-foreground text-sm mt-0.5 block">{editingContact.companyName}</span>
				{/if}
			</div>
			<div class="ml-auto flex items-center gap-2">
				<a
					href="/crm-masters?category=leads"
					target="_blank"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl border border-border bg-card text-xs font-medium text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors shadow-xs"
					title="Configure Contact Types, Categories, and Sources in CRM Masters"
				>
					<Icon name="database" class="size-3.5 text-primary" />
					<span class="hidden sm:inline">CRM Masters</span>
				</a>
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
									<div class="flex items-center justify-between">
										<Field.Label for="contact-type" class="text-muted-foreground mb-0">Contact Type</Field.Label>
										<a
											href="/crm-masters?tab=CONTACT_TYPE"
											target="_blank"
											class="text-[11px] text-primary hover:underline flex items-center gap-0.5 font-medium"
											title="Configure Contact Types in CRM Masters"
										>
											Manage <Icon name="external-link" class="size-2.5" />
										</a>
									</div>
									<Field.Content>
										<Select options={contactTypes} bind:value={editingContact.contactType} placeholder="Select type..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<div class="flex items-center justify-between">
										<Field.Label for="contact-category" class="text-muted-foreground mb-0">Contact Category</Field.Label>
										<a
											href="/crm-masters?tab=CONTACT_CATEGORY"
											target="_blank"
											class="text-[11px] text-primary hover:underline flex items-center gap-0.5 font-medium"
											title="Configure Contact Categories in CRM Masters"
										>
											Manage <Icon name="external-link" class="size-2.5" />
										</a>
									</div>
									<Field.Content>
										<Select options={contactCategories} bind:value={editingContact.contactCategory} placeholder="Select category..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
									</Field.Content>
								</Field.Field>

								<Field.Field class="w-full">
									<div class="flex items-center justify-between">
										<Field.Label for="contact-language" class="text-muted-foreground mb-0">Pref Language</Field.Label>
										<a
											href="/crm-masters?tab=LANGUAGE"
											target="_blank"
											class="text-[11px] text-primary hover:underline flex items-center gap-0.5 font-medium"
											title="Configure Languages in CRM Masters"
										>
											Manage <Icon name="external-link" class="size-2.5" />
										</a>
									</div>
									<Field.Content>
										<Select options={languages} bind:value={editingContact.prefLanguage} placeholder="Select preferred language..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
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

								<div class="pt-4 border-t border-border/50 space-y-4">
									<h3 class="text-sm font-semibold uppercase tracking-wider text-muted-foreground mb-2">Lead Source</h3>
									<Field.Field class="w-full">
										<div class="flex items-center justify-between">
											<Field.Label for="contact-lead-source-type" class="text-muted-foreground mb-0">Lead Source Type</Field.Label>
											<a
												href="/crm-masters?tab=SOURCE"
												target="_blank"
												class="text-[11px] text-primary hover:underline flex items-center gap-0.5 font-medium"
												title="Configure Lead Sources in CRM Masters"
											>
												Manage <Icon name="external-link" class="size-2.5" />
											</a>
										</div>
										<Field.Content>
											<Select options={leadSourceTypeOptions} bind:value={editingContact.leadSourceType} placeholder="Select source type..." valueKey="value" labelKey="label" class="rounded-xl w-full h-9" />
										</Field.Content>
									</Field.Field>

									<Field.Field class="w-full">
										<div class="flex items-center justify-between">
											<Field.Label for="contact-lead-source-channel" class="text-muted-foreground mb-0">Lead Source Channel</Field.Label>
											<a
												href="/crm-masters?tab=SOURCE_CHANNEL"
												target="_blank"
												class="text-[11px] text-primary hover:underline flex items-center gap-0.5 font-medium"
												title="Configure Source Channels in CRM Masters"
											>
												Manage <Icon name="external-link" class="size-2.5" />
											</a>
										</div>
										<Field.Content>
											<div class="relative">
												<Input
													id="contact-lead-source-channel"
													list="channel-options"
													bind:value={editingContact.leadSourceChannel}
													placeholder="e.g. Google-Maps, Web-Harvester, Direct-Call"
													class="rounded-xl h-9"
												/>
												<datalist id="channel-options">
													{#each leadSourceChannels as ch}
														<option value={ch.value}>{ch.label}</option>
													{/each}
												</datalist>
											</div>
										</Field.Content>
									</Field.Field>

									{#if editingContact.sourceUrl}
										<div class="flex items-center justify-between text-xs pt-1">
											<span class="text-muted-foreground truncate max-w-[180px]" title={editingContact.sourceUrl}>Source: {editingContact.sourceUrl}</span>
											<a
												href={editingContact.sourceUrl}
												target="_blank"
												rel="noopener noreferrer"
												class="inline-flex items-center gap-1 font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400 hover:underline shrink-0"
											>
												<Icon name="external-link" class="size-3" /> Visit Link
											</a>
										</div>
									{/if}
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

								<Field.Field class="w-full">
									<div class="flex items-center justify-between mb-1">
										<Field.Label for="contact-location" class="text-muted-foreground mb-0">Live Location (GPS)</Field.Label>
										{#if editingContact.location}
											<a
												href={`https://www.google.com/maps?q=${encodeURIComponent(editingContact.location)}`}
												target="_blank"
												rel="noopener noreferrer"
												class="inline-flex items-center gap-1 text-[11px] font-medium text-indigo-600 hover:text-indigo-500 dark:text-indigo-400 hover:underline"
											>
												<Icon name="map-pin" class="size-3" />
												View Map
											</a>
										{/if}
									</div>
									<Field.Content>
										<div class="flex gap-2 items-center">
											<Input
												id="contact-location"
												bind:value={editingContact.location}
												placeholder="e.g. 19.0760, 72.8777"
												class="rounded-xl h-9 font-mono text-xs flex-1"
											/>
											<Button
												type="button"
												variant="outline"
												size="sm"
												class="rounded-xl h-9 px-3 gap-1.5 shrink-0 bg-secondary/50 hover:bg-secondary"
												onclick={captureLiveLocation}
												disabled={isCapturingLocation}
												title="Capture current live GPS coordinates"
											>
												{#if isCapturingLocation}
													<Loader2 class="size-3.5 animate-spin text-primary" />
												{:else}
													<Icon name="crosshair" class="size-3.5 text-primary" />
												{/if}
												<span class="text-xs">Live GPS</span>
											</Button>
										</div>
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
												{#each (editingContact.tags || '').split(',').map((t: string) => t.trim()).filter(Boolean) as tag}
													<span class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-secondary text-secondary-foreground text-xs font-medium group">
														{tag}
														<button type="button" class="hover:text-destructive transition-colors" onclick={() => {
															editingContact.tags = (editingContact.tags || '').split(',').map((t: string) => t.trim()).filter(Boolean).filter((t: string) => t !== tag).join(', ');
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
															const currentTags = (editingContact.tags || '').split(',').map((t: string) => t.trim()).filter(Boolean);
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

						{#if !isNew}
							<div class="mt-8 pt-4 border-t border-border flex flex-wrap items-center justify-between gap-4 text-xs text-muted-foreground bg-muted/10 -mx-6 -mb-6 px-6 py-3.5">
								<div class="flex flex-wrap items-center gap-6">
									<div>
										<span class="font-medium text-foreground">Created:</span>
										{editingContact.createdAt ? new Date(editingContact.createdAt).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : 'N/A'}
										{#if editingContact.createdBy}
											<span class="text-muted-foreground">by</span> <span class="font-semibold text-foreground">{editingContact.createdBy}</span>
										{/if}
									</div>
									{#if editingContact.modifiedAt || editingContact.modifiedBy}
										<div class="border-l border-border pl-6">
											<span class="font-medium text-foreground">Modified:</span>
											{editingContact.modifiedAt ? new Date(editingContact.modifiedAt).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' }) : 'N/A'}
											{#if editingContact.modifiedBy}
												<span class="text-muted-foreground">by</span> <span class="font-semibold text-foreground">{editingContact.modifiedBy}</span>
											{/if}
										</div>
									{/if}
								</div>
								{#if editingContact.harvestedAt}
									<div class="text-[11px] bg-amber-500/10 text-amber-600 dark:text-amber-400 px-2.5 py-1 rounded-md border border-amber-500/20 font-medium">
										Harvested: {new Date(editingContact.harvestedAt).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' })}
									</div>
								{/if}
							</div>
						{/if}
					{:else if activeTab === 'fleet'}
						{#if !isNew}
							<FleetDetails contactId={id || ''} />
						{/if}
					{/if}
				</div>
			</div>
		{/if}
	</div>
</div>
