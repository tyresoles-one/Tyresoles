<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import {
		DataGrid,
		type DataGridColumn,
		type FilterRule
	} from '$lib/components/venUI/datagrid';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import * as Dialog from '$lib/components/ui/dialog';
	import { graphqlQuery } from '$lib/services/graphql';
	import { toast } from '$lib/components/venUI/toast';
	import { insertCasingItems } from '../logic';

	const LIST_Q = `
		query GetCasingItems {
			casingItems {
				code
				minRate
				maxRate
				category
				name
				isActive
			}
		}
	`;

	type Row = {
		code: string;
		minRate: string;
		maxRate: string;
		category: string;
		name: string;
		isActive: boolean;
	};

	let rows = $state<Row[]>([]);
	let loading = $state(true);
	let saving = $state(false);
	let searchQuery = $state('');
	let filterRules = $state<FilterRule[]>([]);

	let dialogOpen = $state(false);
	let editingRowIndex = $state<number>(-1);
	let draftMinRate = $state('');
	let draftMaxRate = $state('');
	let draftIsActive = $state(false);

	const cols: DataGridColumn<Row>[] = [
		{ accessorKey: 'code', header: 'Tyre Size', enableSorting: true },
		{ accessorKey: 'minRate', header: 'Min Rate', enableSorting: true },
		{ accessorKey: 'maxRate', header: 'Max Rate', enableSorting: true },
		{
			id: 'isActive',
			accessorKey: 'isActive',
			header: 'Active',
			enableSorting: true,
			cell: ({ row }) => (row.original.isActive ? 'Yes' : 'No')
		}
	];

	async function reload() {
		loading = true;
		try {
			const res = await graphqlQuery<{ casingItems: Row[] }>(LIST_Q, {
				cacheKey: 'casingItems',
				skipCache: true
			});
			if (!res.success || !res.data) {
				toast.error(res.error ?? 'Failed to load casing items.');
				return;
			}
			rows = (res.data.casingItems || []).map((r) => ({
				code: r.code || '',
				minRate: r.minRate || '',
				maxRate: r.maxRate || '',
				category: r.category || 'BCASING',
				name: r.name || r.code || '',
				isActive: r.isActive || false
			}));
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		reload();
	});

	function openEdit(r: Row) {
		const idx = rows.findIndex(x => x.code === r.code);
		if (idx >= 0) {
			editingRowIndex = idx;
			draftMinRate = r.minRate;
			draftMaxRate = r.maxRate;
			draftIsActive = r.isActive;
			dialogOpen = true;
		}
	}

	function onDialogSave() {
		if (editingRowIndex >= 0 && editingRowIndex < rows.length) {
			rows[editingRowIndex].minRate = draftMinRate;
			rows[editingRowIndex].maxRate = draftMaxRate;
			rows[editingRowIndex].isActive = draftIsActive;
		}
		dialogOpen = false;
	}

	async function onSaveAll() {
		const itemsToSave = rows.map(r => ({
			code: r.code,
			minRate: r.minRate,
			maxRate: r.maxRate,
			category: r.category,
			name: r.name,
			isActive: r.isActive
		}));
		saving = true;
		try {
			const res = await insertCasingItems(itemsToSave);
			if (res.success) {
				await reload();
			}
		} finally {
			saving = false;
		}
	}

	const gridItems = $derived.by(() => {
		const q = searchQuery.trim().toLowerCase();
		if (!q) return rows;
		return rows.filter(
			(r) =>
				r.code.toLowerCase().includes(q) ||
				r.minRate.toLowerCase().includes(q) ||
				r.maxRate.toLowerCase().includes(q)
		);
	});
</script>

<svelte:head>
	<title>Casings</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
	<PageHeading backHref="/ecoproc" icon="box" pageTitle="Casings">
		{#snippet title()}Casings{/snippet}
		{#snippet actions()}
			<Button size="sm" class="gap-2" onclick={onSaveAll} disabled={loading || saving}>
				<Icon name="save" class="size-3.5" />
				{saving ? 'Saving...' : 'Save Active Casings'}
			</Button>
		{/snippet}
	</PageHeading>

	<main class="flex-1 space-y-3 pb-20 pt-2 px-4">
		<DataGrid
			title=""
			items={gridItems}
			columns={cols}
			bind:searchQuery
			bind:filterRules
			showFilters={true}
			{loading}
			loadingMore={false}
			onRowClick={(r) => openEdit(r)}
			mobileCardTitleKey="code"
			mobileCardSubtitleKey="minRate"
		>
			{#snippet actions()}
				<div class="flex w-full min-w-0 flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center sm:justify-end">
					<Button
						variant="outline"
						size="sm"
						class="gap-1.5 shrink-0"
						onclick={() => void reload()}
						disabled={loading || saving}
						title="Reload from server"
					>
						<Icon name="refresh-cw" class="size-3.5 {!loading ? '' : 'animate-spin'}" />
						Refresh
					</Button>
				</div>
			{/snippet}
		</DataGrid>
	</main>
</div>

<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>Edit Casing: {editingRowIndex >= 0 ? rows[editingRowIndex].code : ''}</Dialog.Title>
		</Dialog.Header>

		<div class="grid gap-4 py-4">
			<label class="grid gap-1.5 text-sm">
				<span class="font-medium leading-none text-foreground">Min Rate</span>
				<Input bind:value={draftMinRate} disabled={saving} />
			</label>
			<label class="grid gap-1.5 text-sm">
				<span class="font-medium leading-none text-foreground">Max Rate</span>
				<Input bind:value={draftMaxRate} disabled={saving} />
			</label>
			<label class="flex items-center gap-2 text-sm font-medium cursor-pointer">
				<input type="checkbox" bind:checked={draftIsActive} disabled={saving} class="size-4 rounded border-gray-300 text-primary focus:ring-primary" />
				Active
			</label>
		</div>

		<Dialog.Footer class="flex flex-wrap gap-2 sm:justify-end">
			<Button variant="outline" onclick={() => (dialogOpen = false)} disabled={saving}>Cancel</Button>
			<Button onclick={onDialogSave} disabled={saving}>Confirm</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
