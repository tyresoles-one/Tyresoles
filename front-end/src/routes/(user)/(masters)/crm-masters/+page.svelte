<script lang="ts">
	import { untrack } from 'svelte';
	import { usePaginatedList } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import * as Dialog from '$lib/components/ui/dialog';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { TableCell, TableHead } from '$lib/components/ui/table';
	import { TableActions } from '$lib/components/venUI/tableActions';
	import MasterList from '$lib/components/venUI/masterList/MasterList.svelte';
	import { graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	type CrmMasterItem = {
		id: number;
		name: string;
	};

	type CrmMasterType =
		| 'CONTACT_TYPE'
		| 'SOURCE'
		| 'STAGE'
		| 'PRIORITY'
		| 'ACTIVITY_TYPE'
		| 'ACTIVITY_OUTCOME';

	type CrmMasterItemsResult = {
		crmMasterItems: CrmMasterItem[];
	};

	type CreateItemResult = {
		createCrmMasterItem: CrmMasterItem;
	};

	type UpdateItemResult = {
		updateCrmMasterItem: CrmMasterItem;
	};

	type DeleteItemResult = {
		deleteCrmMasterItem: boolean;
	};

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<CrmMasterItemsResult, { type: CrmMasterType; where?: any }>;

	const CreateCrmMasterItemDocument = buildMutation`
		mutation CreateCrmMasterItem($type: CrmMasterType!, $name: String!) {
			createCrmMasterItem(type: $type, name: $name) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<CreateItemResult, { type: CrmMasterType; name: string }>;

	const UpdateCrmMasterItemDocument = buildMutation`
		mutation UpdateCrmMasterItem($type: CrmMasterType!, $id: Int!, $name: String!) {
			updateCrmMasterItem(type: $type, id: $id, name: $name) {
				id
				name
			}
		}
	` as unknown as TypedDocumentNode<UpdateItemResult, { type: CrmMasterType; id: number; name: string }>;

	const DeleteCrmMasterItemDocument = buildMutation`
		mutation DeleteCrmMasterItem($type: CrmMasterType!, $id: Int!) {
			deleteCrmMasterItem(type: $type, id: $id)
		}
	` as unknown as TypedDocumentNode<DeleteItemResult, { type: CrmMasterType; id: number }>;

	const lookupTypes: { type: CrmMasterType; label: string; icon: string; description: string }[] = [
		{
			type: 'CONTACT_TYPE',
			label: 'Contact Types',
			icon: 'user-cog',
			description: 'Manage relationship categories for fleet contacts.'
		},
		{
			type: 'SOURCE',
			label: 'Crm Sources',
			icon: 'share-2',
			description: 'Track how new retread leads hear about us.'
		},
		{
			type: 'STAGE',
			label: 'Crm Stages',
			icon: 'git-merge',
			description: 'Define pipeline stages for retreading opportunities.'
		},
		{
			type: 'PRIORITY',
			label: 'Crm Priorities',
			icon: 'alert-circle',
			description: 'Set urgency level for opportunities and deals.'
		},
		{
			type: 'ACTIVITY_TYPE',
			label: 'Activity Types',
			icon: 'phone-call',
			description: 'Sales interaction channels (e.g., Yard Audit).'
		},
		{
			type: 'ACTIVITY_OUTCOME',
			label: 'Activity Outcomes',
			icon: 'check-square',
			description: 'Log standard results of logged activities.'
		}
	];

	let activeTab = $state<CrmMasterType>('CONTACT_TYPE');
	let viewMode = $state<'grid' | 'table'>('grid');

	const list = usePaginatedList<CrmMasterItem>({
		query: GetCrmMasterItemsDocument,
		dataPath: 'crmMasterItems',
		itemsPath: 'crmMasterItems',
		countPath: 'crmMasterItems.length',
		strategy: 'client',
		pageSize: 50,
		mapSearchToVariables: (term) => ({
			type: activeTab,
			where: term ? { name: { contains: term } } : null
		}),
		serverVariableAllowlist: ['type', 'where']
	});

	// Reactively refresh items whenever the active type changes
	$effect(() => {
		const tab = activeTab;
		untrack(() => {
			list.pagination.setVariables({
				type: tab,
				where: list.searchQuery.value ? { name: { contains: list.searchQuery.value } } : null
			});
			list.onRefresh();
		});
	});

	// Dialog editing states
	let dialogOpen = $state(false);
	let dialogMode = $state<'add' | 'edit'>('add');
	let editItemId = $state<number | null>(null);
	let itemNameInput = $state('');
	let isSaving = $state(false);

	// Dialog deletion states
	let deleteDialogOpen = $state(false);
	let deleteItemId = $state<number | null>(null);
	let deleteItemName = $state('');
	let isDeleting = $state(false);

	const activeConfig = $derived(lookupTypes.find((x) => x.type === activeTab)!);

	function openAddDialog() {
		dialogMode = 'add';
		editItemId = null;
		itemNameInput = '';
		dialogOpen = true;
	}

	function openEditDialog(item: CrmMasterItem) {
		dialogMode = 'edit';
		editItemId = item.id;
		itemNameInput = item.name;
		dialogOpen = true;
	}

	function openDeleteDialog(item: CrmMasterItem) {
		deleteItemId = item.id;
		deleteItemName = item.name;
		deleteDialogOpen = true;
	}

	async function saveItem() {
		const name = itemNameInput.trim();
		if (!name) {
			toast.error('Item name cannot be empty');
			return;
		}

		isSaving = true;
		try {
			if (dialogMode === 'add') {
				const res = await graphqlMutation<CreateItemResult>(CreateCrmMasterItemDocument, {
					variables: { type: activeTab, name }
				});

				if (res.success && res.data?.createCrmMasterItem) {
					toast.success(`"${name}" added successfully.`);
					dialogOpen = false;
					list.onRefresh();
				} else {
					toast.error(res.error || 'Failed to add item');
				}
			} else {
				if (editItemId === null) return;
				const res = await graphqlMutation<UpdateItemResult>(UpdateCrmMasterItemDocument, {
					variables: { type: activeTab, id: editItemId, name }
				});

				if (res.success && res.data?.updateCrmMasterItem) {
					toast.success(`Item updated to "${name}".`);
					dialogOpen = false;
					list.onRefresh();
				} else {
					toast.error(res.error || 'Failed to update item');
				}
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while saving.');
		} finally {
			isSaving = false;
		}
	}

	async function confirmDelete() {
		if (deleteItemId === null) return;
		isDeleting = true;
		try {
			const res = await graphqlMutation<DeleteItemResult>(DeleteCrmMasterItemDocument, {
				variables: { type: activeTab, id: deleteItemId }
			});

			if (res.success && res.data?.deleteCrmMasterItem) {
				toast.success(`"${deleteItemName}" deleted successfully.`);
				deleteDialogOpen = false;
				list.onRefresh();
			} else {
				toast.error(res.error || 'Failed to delete item');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred while deleting.');
		} finally {
			isDeleting = false;
		}
	}
</script>

<svelte:head>
	<title>{activeConfig.label} CRM Master | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background text-foreground pb-20 selection:bg-primary/20">
	<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-8 relative z-10">
		<div class="flex flex-col lg:flex-row gap-8">
			<!-- Lookup selection sidebar (desktop) / scrolling pill selector (mobile) -->
			<aside class="w-full lg:w-80 shrink-0">
				<!-- Scrolling horizontal tabs on mobile, vertical list on desktop -->
				<div class="lg:sticky lg:top-24">
					<div class="flex flex-col gap-1">
						<h2 class="text-xs font-semibold text-muted-foreground uppercase tracking-wider px-3 mb-2 hidden lg:block">
							CRM Config Masters
						</h2>

						<!-- Mobile horizontal scroll container -->
						<div class="flex flex-row overflow-x-auto gap-2 pb-2 lg:pb-0 lg:flex-col lg:overflow-x-visible scrollbar-hide">
							{#each lookupTypes as item}
								<button
									type="button"
									onclick={() => (activeTab = item.type)}
									class="flex items-center gap-3 px-4 py-3 rounded-xl border transition-all text-left shrink-0 lg:shrink select-none
										{activeTab === item.type
											? 'bg-indigo-50 border-indigo-100 text-indigo-600 dark:bg-indigo-950/40 dark:border-indigo-900/30 dark:text-indigo-400 font-semibold shadow-xs'
											: 'border-border bg-card text-muted-foreground hover:text-foreground hover:bg-muted/50'}"
								>
									<div class="p-1.5 rounded-lg {activeTab === item.type ? 'bg-indigo-100/80 text-indigo-600 dark:bg-indigo-900/50 dark:text-indigo-400' : 'bg-muted text-muted-foreground'}">
										<Icon name={item.icon} class="size-4" />
									</div>
									<div class="min-w-0">
										<div class="text-sm truncate">{item.label}</div>
										<div class="text-[10px] text-muted-foreground truncate hidden lg:block mt-0.5">{item.description}</div>
									</div>
								</button>
							{/each}
						</div>
					</div>
				</div>
			</aside>

			<!-- Master List Content Area -->
			<main class="flex-1 min-w-0">
				<MasterList
					embedded={true}
					title={activeConfig.label}
					description={activeConfig.description}
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
					{#snippet actions()}
						<Button
							size="sm"
							class="gap-2 shrink-0 bg-indigo-600 hover:bg-indigo-500 text-white font-medium shadow-lg hover:shadow-indigo-500/20 rounded-xl px-4 py-2 transition-all"
							onclick={openAddDialog}
						>
							<Icon name="plus" class="size-3.5" />
							<span>Add {activeConfig.label.slice(0, -1)}</span>
						</Button>
					{/snippet}

					{#snippet gridItem(item: CrmMasterItem)}
						<div class="h-full rounded-xl border border-border bg-card hover:bg-accent/30 hover:border-border/80 backdrop-blur-sm p-4 relative group flex flex-col justify-between transition-all duration-300">
							<div class="flex items-start justify-between gap-4">
								<div class="flex items-center gap-3">
									<div class="p-2.5 rounded-xl bg-primary/10 border border-primary/20 text-primary">
										<Icon name={activeConfig.icon} class="size-5" />
									</div>
									<div>
										<h3 class="font-semibold text-sm text-foreground group-hover:text-primary transition-colors">
											{item.name}
										</h3>
										<span class="text-[10px] font-mono text-muted-foreground mt-0.5 block">
											ID: {item.id}
										</span>
									</div>
								</div>

								<div class="opacity-0 group-hover:opacity-100 transition-opacity">
									<TableActions
										title={item.name}
										actions={[
											{
												label: 'Edit',
												icon: 'edit',
												onClick: () => openEditDialog(item)
											},
											{
												label: 'Delete',
												icon: 'trash',
												onClick: () => openDeleteDialog(item),
												variant: 'destructive'
											}
										]}
									/>
								</div>
							</div>
						</div>
					{/snippet}

					{#snippet tableHeader()}
						<TableHead class="w-[80px] text-center text-muted-foreground">ID</TableHead>
						<TableHead class="text-muted-foreground">Name</TableHead>
						<TableHead class="text-right text-muted-foreground w-[100px]">Actions</TableHead>
					{/snippet}

					{#snippet tableRow(item: CrmMasterItem)}
						<TableCell class="text-center font-mono text-xs text-muted-foreground p-3">{item.id}</TableCell>
						<TableCell class="font-medium text-foreground">{item.name}</TableCell>
						<TableCell class="text-right p-3">
							<TableActions
								title={item.name}
								actions={[
									{
										label: 'Edit',
										icon: 'edit',
										onClick: () => openEditDialog(item)
									},
									{
										label: 'Delete',
										icon: 'trash',
										onClick: () => openDeleteDialog(item),
										variant: 'destructive'
									}
								]}
							/>
						</TableCell>
					{/snippet}
				</MasterList>
			</main>
		</div>
	</div>
</div>

<!-- Add/Edit Modal -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>{dialogMode === 'add' ? 'Add' : 'Edit'} {activeConfig.label.slice(0, -1)}</Dialog.Title>
		</Dialog.Header>

		<div class="flex flex-col gap-4 py-3">
			<Field.Field class="w-full">
				<Field.Label for="master-item-name">Item Name</Field.Label>
				<Field.Content>
					<Input
						id="master-item-name"
						bind:value={itemNameInput}
						placeholder={`e.g., New ${activeConfig.label.slice(0, -1)}`}
						autocomplete="off"
						class="rounded-xl"
					/>
				</Field.Content>
			</Field.Field>
		</div>

		<Dialog.Footer class="flex gap-2 justify-end pt-4 border-t">
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
				disabled={!itemNameInput.trim() || isSaving}
				onclick={saveItem}
				class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 shadow-lg hover:shadow-indigo-500/10"
			>
				{#if isSaving}
					<Loader2 class="size-4 animate-spin shrink-0" />
				{/if}
				{dialogMode === 'add' ? 'Create' : 'Save Changes'}
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Delete Confirmation Modal -->
<Dialog.Root bind:open={deleteDialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>Delete item</Dialog.Title>
		</Dialog.Header>

		<div class="py-3">
			<p class="text-sm text-muted-foreground leading-relaxed">
				Are you sure you want to delete <strong class="text-foreground">"{deleteItemName}"</strong>? This action cannot be undone and may affect associated records.
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
				Delete Item
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
