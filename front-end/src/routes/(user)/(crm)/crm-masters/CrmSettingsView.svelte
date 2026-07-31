<script lang="ts">
	import { onMount } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	const GetCrmSettingDocument = buildQuery`
		query GetCrmSetting($key: String!) {
			getCrmSetting(key: $key) {
				key
				value
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmSetting: { key: string; value: string } | null }, { key: string }>;

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmSetting: { success: boolean; message: string } }, { key: string; value: string; description?: string }>;

	const SyncCrmProductsFromPriceGroupDocument = buildMutation`
		mutation SyncCrmProductsFromPriceGroup($priceGroupCode: String!, $respCenters: String) {
			count: syncCrmProductsFromPriceGroup(priceGroupCode: $priceGroupCode, respCenters: $respCenters)
		}
	` as unknown as TypedDocumentNode<{ count: number }, { priceGroupCode: string; respCenters?: string }>;

	type Mapping = {
		id: string;
		priceGroupCode: string;
		respCenters: string[];
	};

	let mappings = $state<Mapping[]>([]);
	let loading = $state(true);
	let saving = $state(false);
	let syncingId = $state<string | null>(null);

	const SETTING_KEY = 'CUSTOMER_PRICE_GROUP_MAPPING';

	onMount(async () => {
		try {
			const res = await graphqlQuery(GetCrmSettingDocument, { variables: { key: SETTING_KEY } });
			if (res.data?.getCrmSetting?.value) {
				mappings = JSON.parse(res.data.getCrmSetting.value);
			} else {
				mappings = [];
			}
		} catch (err: any) {
			toast.error('Failed to load settings', err.message);
		} finally {
			loading = false;
		}
	});

	function addMapping() {
		mappings = [...mappings, { id: crypto.randomUUID(), priceGroupCode: '', respCenters: [] }];
	}

	function removeMapping(id: string) {
		mappings = mappings.filter((m) => m.id !== id);
	}

	async function syncProducts(mapping: Mapping) {
		if (!mapping.priceGroupCode) {
			toast.error('Please select a Customer Price Group first');
			return;
		}
		syncingId = mapping.id;
		try {
			const res = await graphqlMutation(SyncCrmProductsFromPriceGroupDocument, {
				variables: {
					priceGroupCode: mapping.priceGroupCode,
					respCenters: mapping.respCenters.join(',')
				}
			});
			if (res.data?.count != null) {
				toast.success(`Successfully fetched & saved ${res.data.count} product(s) with prices into CRM Products!`);
			} else {
				throw new Error(res.error || 'Failed to sync products');
			}
		} catch (err: any) {
			toast.error('Sync failed', err.message);
		} finally {
			syncingId = null;
		}
	}

	async function saveSettings() {
		saving = true;
		try {
			const value = JSON.stringify(mappings);
			const res = await graphqlMutation(SaveCrmSettingDocument, {
				variables: {
					key: SETTING_KEY,
					value,
					description: 'Customer Price Group Mappings per Resp Center'
				}
			});
			if (res.data?.saveCrmSetting.success) {
				toast.success('Settings saved successfully');
			} else {
				throw new Error(res.data?.saveCrmSetting.message || 'Unknown error');
			}
		} catch (err: any) {
			toast.error('Failed to save settings', err.message);
		} finally {
			saving = false;
		}
	}
</script>

<div class="flex flex-col h-full bg-slate-50 dark:bg-slate-900 overflow-hidden">
	<div class="p-6 border-b bg-white dark:bg-slate-950 flex items-center justify-between shadow-sm shrink-0">
		<div>
			<h2 class="text-xl font-semibold tracking-tight">CRM Settings</h2>
			<p class="text-sm text-muted-foreground mt-1">Configure global CRM settings and mappings.</p>
		</div>
		<Button onclick={saveSettings} disabled={loading || saving} class="gap-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium shadow-lg hover:shadow-indigo-500/20 rounded-xl px-6">
			{#if saving}
				<Loader2 class="size-4 animate-spin" />
			{:else}
				<Icon name="save" class="size-4" />
			{/if}
			<span>Save Changes</span>
		</Button>
	</div>

	<div class="flex-1 overflow-y-auto p-6">
		{#if loading}
			<div class="flex items-center justify-center py-12">
				<Loader2 class="size-6 animate-spin text-muted-foreground" />
			</div>
		{:else}
			<div class="max-w-4xl space-y-8">
				<!-- Customer Price Group Mapping Section -->
				<div class="bg-white dark:bg-slate-950 rounded-xl border shadow-sm p-6">
					<div class="flex items-center justify-between mb-6">
						<div>
							<h3 class="text-lg font-medium text-slate-900 dark:text-slate-100">Customer Price Group Mappings</h3>
							<p class="text-sm text-muted-foreground mt-1">Map Customer Price Groups to specific Responsibility Centers for contact allocation and calling.</p>
						</div>
						<Button variant="outline" size="sm" onclick={addMapping} class="gap-2 rounded-lg">
							<Icon name="plus" class="size-4" />
							<span>Add Mapping</span>
						</Button>
					</div>

					<div class="space-y-4">
						{#if mappings.length === 0}
							<div class="text-center py-8 text-muted-foreground border-2 border-dashed rounded-xl bg-slate-50 dark:bg-slate-900/50">
								No mappings configured. Click 'Add Mapping' to create one.
							</div>
						{:else}
							{#each mappings as mapping, idx (mapping.id)}
								<div class="p-4 bg-slate-50 dark:bg-slate-900/50 rounded-xl border border-slate-200 dark:border-slate-800 space-y-4">
									<div class="flex items-center justify-between border-b border-slate-200/60 dark:border-slate-800/60 pb-3">
										<span class="text-xs font-semibold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
											<Icon name="layers" class="size-3.5 text-indigo-500" />
											Mapping #{idx + 1}
										</span>
										<div class="flex items-center gap-2">
											<Button
												variant="outline"
												size="sm"
												disabled={syncingId === mapping.id || !mapping.priceGroupCode}
												onclick={() => syncProducts(mapping)}
												class="gap-1.5 border-indigo-200 text-indigo-700 hover:bg-indigo-50 dark:hover:bg-indigo-950/40 rounded-xl h-8 text-xs px-3 shadow-2xs"
												title="Fetch all items, categories, and prices for this price group into CRM Products"
											>
												{#if syncingId === mapping.id}
													<Loader2 class="size-3.5 animate-spin" />
													<span>Fetching...</span>
												{:else}
													<Icon name="refresh-cw" class="size-3.5 text-indigo-600" />
													<span>Fetch Items & Prices</span>
												{/if}
											</Button>

											<Button variant="ghost" size="icon" class="text-red-500 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/50 h-8 w-8 rounded-lg" onclick={() => removeMapping(mapping.id)} title="Delete Mapping">
												<Icon name="trash-2" class="size-4" />
											</Button>
										</div>
									</div>

									<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
										<div class="space-y-1.5">
											<label class="text-xs font-medium text-slate-700 dark:text-slate-300">Customer Price Group</label>
											<MasterSelect
												form={{
													values: { 
														get priceGroup() { return mapping.priceGroupCode; },
														set priceGroup(v) { mapping.priceGroupCode = v; }
													},
													setTouched: () => {}
												}}
												fieldName="priceGroup"
												masterType="customerPriceGroups"
												placeholder="Select Price Group..."
												singleSelect={true}
												respCenterOverride={mapping.respCenters}
											/>
										</div>
										<div class="space-y-1.5">
											<label class="text-xs font-medium text-slate-700 dark:text-slate-300">Responsibility Centers</label>
											<MasterSelect
												form={{
													values: { 
														get respCenters() { return mapping.respCenters.join(', '); },
														set respCenters(v) { mapping.respCenters = v.split(',').map((s) => s.trim()).filter(Boolean); }
													},
													setTouched: () => {}
												}}
												fieldName="respCenters"
												masterType="respCenters"
												respCenterType="Sale"
												placeholder="Select Resp Centers..."
												singleSelect={false}
											/>
										</div>
									</div>
								</div>
							{/each}
						{/if}
					</div>
				</div>
			</div>
		{/if}
	</div>
</div>

