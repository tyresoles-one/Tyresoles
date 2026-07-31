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
	import { graphqlQuery } from '$lib/services/graphql';
	import { toast } from '$lib/components/venUI/toast';

	const LIST_Q = `
		query GetDownloadsConfig {
			downloadsConfig {
				items {
					name
					path
					description
				}
			}
		}
	`;

	type Row = {
		name: string;
		path: string;
		description: string;
		type: string;
	};

	let rows = $state<Row[]>([]);
	let loading = $state(true);
	let searchQuery = $state('');
	let filterRules = $state<FilterRule[]>([]);

	const cols: DataGridColumn<Row>[] = [
		{ accessorKey: 'name', header: 'Name', enableSorting: true },
		{ accessorKey: 'type', header: 'File Type', enableSorting: true },
		{ accessorKey: 'description', header: 'Description', enableSorting: true },
		{ id: 'actions', header: 'Actions', enableSorting: false }
	];

	function getAbsoluteUrl(path: string): string {
		if (!path) return '';
		if (path.startsWith('http://') || path.startsWith('https://')) {
			return path;
		}
		try {
			return new URL(path, window.location.origin).href;
		} catch {
			return path;
		}
	}

	function getFileExtension(path: string): string {
		if (!path) return 'FILE';
		try {
			const url = new URL(path);
			const pathname = url.pathname;
			const parts = pathname.split('.');
			if (parts.length > 1) {
				return parts.pop()?.toUpperCase() ?? 'FILE';
			}
		} catch {
			const parts = path.split('.');
			if (parts.length > 1) {
				return parts.pop()?.toUpperCase() ?? 'FILE';
			}
		}
		return 'FILE';
	}

	async function reload() {
		loading = true;
		try {
			const res = await graphqlQuery<{ downloadsConfig: { items: any[] } }>(LIST_Q, {
				cacheKey: 'downloadsConfig_list',
				skipCache: true
			});
			if (!res.success || !res.data?.downloadsConfig) {
				toast.error(res.error ?? 'Failed to load downloads.');
				return;
			}
			rows = (res.data.downloadsConfig.items || []).map((r) => ({
				name: r.name || '',
				path: r.path || '',
				description: r.description || '',
				type: getFileExtension(r.path)
			}));
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		reload();
	});

	function triggerDownload(item: Row) {
		try {
			toast.success(`Starting download: ${item.name}`);
			// Open the download link in a new tab/window
			window.open(item.path, '_blank');
		} catch (e) {
			toast.error(`Failed to start download for ${item.name}`);
			console.error(e);
		}
	}

	const gridItems = $derived.by(() => {
		const q = searchQuery.trim().toLowerCase();
		if (!q) return rows;
		return rows.filter(
			(r) =>
				r.name.toLowerCase().includes(q) ||
				r.description.toLowerCase().includes(q) ||
				r.type.toLowerCase().includes(q)
		);
	});
</script>

<svelte:head>
	<title>Downloads</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
	<PageHeading backHref="/" icon="download" pageTitle="Downloads">
		{#snippet title()}Downloads{/snippet}
	</PageHeading>

	<main class="flex-1 space-y-3 pb-20 pt-2 px-4">
		<DataGrid
			title=""
			description="Click on any item in the grid to download the file."
			items={gridItems}
			columns={cols}
			bind:searchQuery
			bind:filterRules
			showFilters={false}
			{loading}
			loadingMore={false}
			onRowClick={(item) => triggerDownload(item)}
			mobileCardTitleKey="name"
			mobileCardSubtitleKey="type"
		>
			{#snippet cell({ column, row, renderDefault })}
				{#if column.id === 'actions'}
					<div class="flex items-center justify-end" onclick={(e) => e.stopPropagation()}>
						<Button
							variant="ghost"
							size="sm"
							class="h-8 gap-1.5 text-muted-foreground hover:text-foreground hover:bg-muted"
							onclick={(e) => {
								e.stopPropagation();
								const absUrl = getAbsoluteUrl(row.path);
								navigator.clipboard.writeText(absUrl);
								toast.success('Download link copied!');
							}}
							title="Copy download link"
						>
							<Icon name="copy" class="size-3.5" />
							<span>Copy Link</span>
						</Button>
					</div>
				{:else}
					{renderDefault()}
				{/if}
			{/snippet}
			{#snippet actions()}
				<div class="flex w-full min-w-0 flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center sm:justify-end">
					<Button
						variant="outline"
						size="sm"
						class="gap-1.5 shrink-0"
						onclick={() => void reload()}
						disabled={loading}
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

