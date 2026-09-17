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
	import { Icon } from '$lib/components/venUI/icon';
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
			limit?: number | null;
		}) => Promise<void>;
	} = $props();

	let isLoading = $state(false);
	let coolDownDays: number | null = $state(null);
	let batchLimit: number = $state(10);
	
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
	let userProfileRespCenter = $derived($authStore.user?.respCenter?.trim() || '');

	let distinctRespCenters = $derived.by(() => {
		const codes = new Set<string>();
		for (const loc of userLocations) {
			if (loc?.code && loc.code.trim()) {
				codes.add(loc.code.trim());
			}
		}
		if (codes.size === 0 && userProfileRespCenter) {
			codes.add(userProfileRespCenter);
		}
		return Array.from(codes);
	});

	let isSingleLocation = $derived(distinctRespCenters.length === 1);
	let singleRespCenterCode = $derived(distinctRespCenters.length === 1 ? distinctRespCenters[0] : '');

	const STORAGE_KEY = 'crm_calling_last_allocation_filters';

	$effect(() => {
		if (open) {
			untrack(() => {
				try {
					const savedStr = localStorage.getItem(STORAGE_KEY);
					if (savedStr) {
						const saved = JSON.parse(savedStr);
						coolDownDays = saved.coolDownDays ?? null;
						batchLimit = saved.limit || 10;
						dialogFormValues.respCenter = saved.respCenter || (isSingleLocation ? singleRespCenterCode : '');
						dialogFormValues.areas = saved.areas || '';
						selectedProducts = saved.products || [];
						selectedStates = saved.states || [];
						selectedCities = saved.cities || [];
						selectedTags = saved.tags || [];
						selectedTypes = saved.types || ['Customer'];
						selectedCategories = saved.categories || [];
						return;
					}
				} catch (e) {
					console.error('Failed to parse saved allocation filter', e);
				}

				coolDownDays = null;
				batchLimit = 10;
				if (isSingleLocation) {
					dialogFormValues.respCenter = singleRespCenterCode;
				} else {
					dialogFormValues.respCenter = '';
				}
				dialogFormValues.areas = '';
				
				selectedProducts = [];
				selectedStates = [];
				selectedCities = [];
				selectedTags = [];
				const custMatch = allTypes.find((t) => t.toLowerCase() === 'customer');
				selectedTypes = [custMatch || 'Customer'];
				selectedCategories = [];
			});
		}
	});

	$effect(() => {
		if (open && isSingleLocation && singleRespCenterCode && !dialogFormValues.respCenter) {
			dialogFormValues.respCenter = singleRespCenterCode;
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
			const res = await graphqlQuery<any>(GetCrmContactProductsDocument, { variables });
			const pData = res.data as any;
			if (res.success && pData?.getCrmContactProducts) {
				allProducts = pData.getCrmContactProducts;
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
			
			const lookupsRes = await graphqlQuery<any>(GetCrmContactLookupsDocument, { variables });
			const typesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, { variables: { type: 'CONTACT_TYPE' } });
			const categoriesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, { variables: { type: 'CONTACT_CATEGORY' } });

			const lData = lookupsRes.data as any;
			const tData = typesRes.data as any;
			const cData = categoriesRes.data as any;

			if (lookupsRes.success && lData?.getCrmContactLookups) {
				allStates = lData.getCrmContactLookups.states || [];
				allCities = lData.getCrmContactLookups.cities || [];
				allTags = lData.getCrmContactLookups.tags || [];
			}

			if (typesRes.success && tData?.crmMasterItems) {
				allTypes = tData.crmMasterItems.map((i: any) => i.name);
				const custMatch = allTypes.find((t: string) => t.toLowerCase() === 'customer');
				if (custMatch) {
					if (selectedTypes.length === 0 || selectedTypes.some((s) => s.toLowerCase() === 'customer')) {
						selectedTypes = [custMatch, ...selectedTypes.filter((s) => s.toLowerCase() !== 'customer')];
					}
				}
			}

			if (categoriesRes.success && cData?.crmMasterItems) {
				allCategories = cData.crmMasterItems.map((i: any) => i.name);
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
			tags: selectedTags,
			limit: batchLimit
		};

		try {
			localStorage.setItem(STORAGE_KEY, JSON.stringify({
				coolDownDays,
				respCenter: dialogFormValues.respCenter,
				areas: dialogFormValues.areas,
				products: selectedProducts,
				states: selectedStates,
				cities: selectedCities,
				types: selectedTypes,
				categories: selectedCategories,
				tags: selectedTags,
				limit: batchLimit
			}));
		} catch (e) {
			console.error('Failed to store allocation filter', e);
		}
		
		isLoading = true;
		await onAllocate(filters);
		isLoading = false;
		open = false;
	}
</script>

<Dialog.Root bind:open>
	<Dialog.Content class="w-[95vw] sm:max-w-[760px] p-0 overflow-hidden flex flex-col max-h-[90dvh] sm:max-h-[85vh] rounded-2xl shadow-xl border border-border">
		<!-- Header with Title Only (Unwanted Subheading Removed) -->
		<Dialog.Header class="px-5 py-3.5 sm:px-6 sm:py-4 border-b border-border flex-shrink-0 bg-muted/20">
			<Dialog.Title class="text-base sm:text-lg font-bold">Allocate Contacts</Dialog.Title>
			<Dialog.Description class="sr-only">Set filters to dynamically pull unassigned contacts.</Dialog.Description>
		</Dialog.Header>

		<!-- Scrollable Body: Mobile-First Responsive Grid with Logical Hierarchy -->
		<div class="p-4 sm:p-6 overflow-y-auto flex-1 min-h-0 custom-scrollbar">
			<div class="grid grid-cols-1 md:grid-cols-2 gap-5 sm:gap-6">
				<!-- Section 1: Territory & Location Hierarchy -->
				<div class="space-y-3.5">
					<div class="flex items-center gap-2 pb-1.5 border-b border-border/60">
						<Icon name="map-pin" class="size-4 text-primary" />
						<h4 class="text-xs font-bold uppercase tracking-wider text-foreground">Territory & Location</h4>
					</div>

					<div class="space-y-3">
						{#if !isSingleLocation}
							<div class="space-y-1.5">
								<label class="text-xs font-semibold text-foreground">Responsibility Center</label>
								<MasterSelect
									fieldName="respCenter"
									masterType="respCenters"
									placeholder="Select location..."
									singleSelect={true}
									form={dialogForm}
								/>
							</div>
						{/if}

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
								States
								{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
							</label>
							<Select
								options={allStates.map(p => ({ value: p, label: p }))}
								bind:value={selectedStates}
								multiple
								valueKey="value"
								labelKey="label"
								placeholder="Select states..."
							/>
						</div>

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
								Cities
								{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
							</label>
							<Select
								options={allCities.map(p => ({ value: p, label: p }))}
								bind:value={selectedCities}
								multiple
								valueKey="value"
								labelKey="label"
								placeholder="Select cities..."
							/>
						</div>

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground">Areas</label>
							<MasterSelect
								fieldName="areas"
								masterType="areas"
								placeholder="Select areas..."
								singleSelect={false}
								form={dialogForm}
								respCenterOverride={dialogFormValues.respCenter}
							/>
						</div>
					</div>
				</div>

				<!-- Section 2: Contact Attributes, Products & Rules -->
				<div class="space-y-3.5">
					<div class="flex items-center gap-2 pb-1.5 border-b border-border/60">
						<Icon name="users" class="size-4 text-primary" />
						<h4 class="text-xs font-bold uppercase tracking-wider text-foreground">Contact Segmentation</h4>
					</div>

					<div class="space-y-3">
						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
								Contact Type
								{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
							</label>
							<Select
								options={allTypes.map(p => ({ value: p, label: p }))}
								bind:value={selectedTypes}
								multiple
								valueKey="value"
								labelKey="label"
								placeholder="Select types..."
							/>
						</div>

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
								Contact Category
								{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
							</label>
							<Select
								options={allCategories.map(p => ({ value: p, label: p }))}
								bind:value={selectedCategories}
								multiple
								valueKey="value"
								labelKey="label"
								placeholder="Select categories..."
							/>
						</div>

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
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

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground flex justify-between items-center">
								Tags
								{#if lookupsLoading}<Loader2 class="size-3 animate-spin text-muted-foreground" />{/if}
							</label>
							<Select
								options={allTags.map(p => ({ value: p, label: p }))}
								bind:value={selectedTags}
								multiple
								valueKey="value"
								labelKey="label"
								placeholder="Select tags..."
							/>
						</div>

						<div class="space-y-1.5">
							<label for="cooldown" class="text-xs font-semibold text-foreground">Cool Down Period (Days)</label>
							<Input
								id="cooldown"
								type="number"
								min="0"
								bind:value={coolDownDays}
								placeholder="e.g. 30"
								class="h-9 text-xs rounded-lg"
							/>
						</div>

						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground">Batch Size to Allocate</label>
							<div class="grid grid-cols-2 gap-2">
								<button
									type="button"
									onclick={() => (batchLimit = 10)}
									class="flex items-center justify-between px-3 py-2 text-xs font-medium rounded-lg border transition-all cursor-pointer {batchLimit === 10 ? 'border-primary bg-primary/10 text-primary font-semibold shadow-xs' : 'border-border bg-background hover:bg-muted text-muted-foreground'}"
								>
									<span>10 Contacts</span>
									<span class="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded {batchLimit === 10 ? 'bg-primary/20 text-primary' : 'bg-muted text-muted-foreground'} font-bold">Default</span>
								</button>
								<button
									type="button"
									onclick={() => (batchLimit = 20)}
									class="flex items-center justify-between px-3 py-2 text-xs font-medium rounded-lg border transition-all cursor-pointer {batchLimit === 20 ? 'border-primary bg-primary/10 text-primary font-semibold shadow-xs' : 'border-border bg-background hover:bg-muted text-muted-foreground'}"
								>
									<span>20 Contacts</span>
									<span class="text-[10px] uppercase tracking-wider px-1.5 py-0.5 rounded {batchLimit === 20 ? 'bg-primary/20 text-primary' : 'bg-muted text-muted-foreground'} font-bold">Optional</span>
								</button>
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>

		<!-- Footer: Mobile-First Responsive Actions -->
		<Dialog.Footer class="px-5 py-3 sm:px-6 sm:py-3.5 border-t border-border flex-shrink-0 bg-muted/10 flex flex-col-reverse sm:flex-row sm:justify-end gap-2">
			<Button variant="outline" onclick={() => (open = false)} class="w-full sm:w-auto h-9 text-xs sm:text-sm font-medium">Cancel</Button>
			<Button onclick={handleAllocate} disabled={isLoading} class="w-full sm:w-auto h-9 text-xs sm:text-sm font-semibold">
				{#if isLoading}
					<Loader2 class="mr-2 size-3.5 animate-spin" /> Allocating...
				{:else}
					Allocate {batchLimit} Contacts
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
