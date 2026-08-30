<script lang="ts">
	import { untrack } from 'svelte';
	import * as Dialog from '$lib/components/ui/dialog';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { authStore } from '$lib/stores/auth';
	import { graphqlQuery } from '$lib/services/graphql';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { GetCrmContactProductsDocument, GetCrmContactLookupsDocument, GetCrmMasterItemsDocument } from './queries';

	let {
		open = $bindable(false),
		onAllocate
	}: {
		open: boolean;
		onAllocate: (filters: {
			coolDownDays: number | null;
			respCenters: string[];
			products: string[];
			areas: string[];
			states: string[];
			cities: string[];
			types: string[];
			categories: string[];
			tags: string[];
		}) => Promise<void>;
	} = $props();

	let isLoading = $state(false);
	let coolDownDays: number | null = $state(null);
	
	let dialogFormValues = $state({
		respCenter: '',
		areas: ''
	});

	const dialogForm = {
		get values() {
			return dialogFormValues;
		},
		setTouched(name: string) {}
	};

	let allProducts: string[] = $state([]);
	let selectedProducts: string[] = $state([]);
	let productsLoading = $state(false);

	let allStates: string[] = $state([]);
	let allCities: string[] = $state([]);
	let allTags: string[] = $state([]);
	let allTypes: string[] = $state([]);
	let allCategories: string[] = $state([]);

	let selectedStates: string[] = $state([]);
	let selectedCities: string[] = $state([]);
	let selectedTags: string[] = $state([]);
	let selectedTypes: string[] = $state([]);
	let selectedCategories: string[] = $state([]);

	let lookupsLoading = $state(false);

	let userLocations = $derived($authStore.locations || []);
	let isSingleLocation = $derived(userLocations.length === 1);

	$effect(() => {
		if (open) {
			untrack(() => {
				coolDownDays = null;
				if (isSingleLocation) {
					dialogFormValues.respCenter = userLocations[0]?.code || '';
				} else {
					dialogFormValues.respCenter = '';
				}
				dialogFormValues.areas = '';
				
				selectedProducts = [];
				selectedStates = [];
				selectedCities = [];
				selectedTags = [];
				selectedTypes = [];
				selectedCategories = [];
			});
		}
	});

	$effect(() => {
		if (open) {
			const rc = dialogFormValues.respCenter;
			fetchProducts(rc);
			fetchLookups(rc);
		}
	});

	async function fetchProducts(respCenter: string) {
		productsLoading = true;
		try {
			const variables: any = {};
			if (respCenter) {
				variables.respCenter = respCenter.split(',')[0]; // Use first selected respCenter for products query if multi
			}
			const res = await graphqlQuery(GetCrmContactProductsDocument, { variables });
			if (res.success && res.data?.getCrmContactProducts) {
				allProducts = res.data.getCrmContactProducts;
			}
		} catch (e) {
			console.error('Failed to load products', e);
		} finally {
			productsLoading = false;
		}
	}

	async function fetchLookups(respCenter: string) {
		lookupsLoading = true;
		try {
			const variables: any = {};
			if (respCenter) {
				variables.respCenter = respCenter.split(',')[0];
			}
			
			const lookupsRes = await graphqlQuery(GetCrmContactLookupsDocument, { variables });
			const typesRes = await graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'CONTACT_TYPE' } });
			const categoriesRes = await graphqlQuery(GetCrmMasterItemsDocument, { variables: { type: 'CONTACT_CATEGORY' } });

			if (lookupsRes.success && lookupsRes.data?.getCrmContactLookups) {
				allStates = lookupsRes.data.getCrmContactLookups.states;
				allCities = lookupsRes.data.getCrmContactLookups.cities;
				allTags = lookupsRes.data.getCrmContactLookups.tags;
			}

			if (typesRes.success && typesRes.data?.crmMasterItems) {
				allTypes = typesRes.data.crmMasterItems.map(i => i.name);
			}

			if (categoriesRes.success && categoriesRes.data?.crmMasterItems) {
				allCategories = categoriesRes.data.crmMasterItems.map(i => i.name);
			}
		} catch (e) {
			console.error('Failed to load lookups', e);
		} finally {
			lookupsLoading = false;
		}
	}

	async function handleAllocate() {
		const filters = {
			coolDownDays: coolDownDays,
			respCenters: dialogFormValues.respCenter ? dialogFormValues.respCenter.split(',') : [],
			areas: dialogFormValues.areas ? dialogFormValues.areas.split(',') : [],
			products: selectedProducts,
			states: selectedStates,
			cities: selectedCities,
			types: selectedTypes,
			categories: selectedCategories,
			tags: selectedTags
		};
		
		isLoading = true;
		await onAllocate(filters);
		isLoading = false;
		open = false;
	}
</script>

<Dialog.Root bind:open>
	<Dialog.Content class="sm:max-w-[800px] p-0 overflow-hidden flex flex-col h-[90vh] sm:h-auto sm:max-h-[85vh]">
		<Dialog.Header class="px-6 py-4 border-b flex-shrink-0 bg-muted/20">
			<Dialog.Title class="text-xl">Allocate Contacts</Dialog.Title>
			<Dialog.Description>
				Set filters to dynamically pull a fresh batch of unassigned contacts.
			</Dialog.Description>
		</Dialog.Header>

		<div class="p-6 overflow-y-auto flex-1 custom-scrollbar">
			<div class="grid grid-cols-1 md:grid-cols-2 gap-x-8 gap-y-6">
				<!-- Column 1: Location & General -->
				<div class="space-y-5">
					<div>
						<h4 class="text-sm font-semibold text-primary uppercase tracking-wider mb-3">General & Location</h4>
						<div class="space-y-4">
							<div class="space-y-1.5">
								<label for="cooldown" class="text-sm font-medium">Cool Down Period (Days)</label>
								<Input id="cooldown" type="number" bind:value={coolDownDays} placeholder="Leave empty for default" />
								<p class="text-xs text-muted-foreground">Days to avoid recalling after the last invoice/call.</p>
							</div>

							{#if !isSingleLocation}
								<div class="space-y-1.5">
									<label class="text-sm font-medium">Responsibility Centers</label>
									<MasterSelect
										fieldName="respCenter"
										masterType="respCenters"
										placeholder="Select Locations"
										singleSelect={true}
										form={dialogForm}
									/>
								</div>
							{/if}

							<div class="space-y-1.5">
								<label class="text-sm font-medium">Areas</label>
								<MasterSelect
									fieldName="areas"
									masterType="areas"
									placeholder="Select Areas"
									singleSelect={false}
									form={dialogForm}
									respCenterOverride={dialogFormValues.respCenter}
								/>
							</div>

							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									States
									{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select options={allStates.map(p => ({ value: p, label: p }))} bind:value={selectedStates} multiple valueKey="value" labelKey="label" placeholder="Select states..." />
							</div>

							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									Cities
									{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select options={allCities.map(p => ({ value: p, label: p }))} bind:value={selectedCities} multiple valueKey="value" labelKey="label" placeholder="Select cities..." />
							</div>
						</div>
					</div>
				</div>

				<!-- Column 2: Attributes & Products -->
				<div class="space-y-5">
					<div>
						<h4 class="text-sm font-semibold text-primary uppercase tracking-wider mb-3">Contact Attributes</h4>
						<div class="space-y-4">
							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									Contact Type
									{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select options={allTypes.map(p => ({ value: p, label: p }))} bind:value={selectedTypes} multiple valueKey="value" labelKey="label" placeholder="Select types..." />
							</div>

							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									Contact Category
									{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select options={allCategories.map(p => ({ value: p, label: p }))} bind:value={selectedCategories} multiple valueKey="value" labelKey="label" placeholder="Select categories..." />
							</div>

							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									Tags
									{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select options={allTags.map(p => ({ value: p, label: p }))} bind:value={selectedTags} multiple valueKey="value" labelKey="label" placeholder="Select tags..." />
							</div>

							<div class="space-y-1.5">
								<label class="text-sm font-medium flex justify-between items-center">
									Products
									{#if productsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
								</label>
								<Select 
									options={allProducts.map(p => ({ value: p, label: p }))} 
									bind:value={selectedProducts} 
									multiple 
									valueKey="value"
									labelKey="label"
									placeholder="Select products..." 
								/>
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>

		<Dialog.Footer class="px-6 py-4 border-t flex-shrink-0 bg-muted/10">
			<Button variant="outline" onclick={() => (open = false)} class="w-full sm:w-auto">Cancel</Button>
			<Button onclick={handleAllocate} disabled={isLoading} class="w-full sm:w-auto">
				{#if isLoading}
					<Loader2 class="mr-2 size-4 animate-spin" /> Allocating...
				{:else}
					Allocate Contacts
				{/if}
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<style>
	.custom-scrollbar::-webkit-scrollbar {
		width: 6px;
	}
	.custom-scrollbar::-webkit-scrollbar-track {
		background: transparent;
	}
	.custom-scrollbar::-webkit-scrollbar-thumb {
		background-color: hsl(var(--muted-foreground) / 0.3);
		border-radius: 20px;
	}
</style>
