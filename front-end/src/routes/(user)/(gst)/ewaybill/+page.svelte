<script lang="ts">
	import { onMount } from 'svelte';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Badge } from '$lib/components/ui/badge';
	import { Card, CardContent } from '$lib/components/ui/card';
	import * as Dialog from '$lib/components/ui/dialog';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	import {
		GetPostedDcHeadersDocument,
		ProcessPostedDcEWayBillsDocument,
		type DCHeaderPosted
	} from './queries';

	// Filters & Date Presets
	let datePreset = $state<'today' | 'yesterday' | 'week' | 'month' | 'custom' | 'all'>('week');
	let fromDate = $state<string>('');
	let toDate = $state<string>('');
	let textSearch = $state<string>('');
	let filterSkipEWayBill = $state<'all' | 'pending' | 'skipped'>('pending');

	// Selection & Pagination State
	let selectedDcNos = $state<Set<string>>(new Set());
	let currentPage = $state<number>(1);
	let pageSize = $state<number>(20);

	// Data & Processing State
	let dcHeaders = $state<DCHeaderPosted[]>([]);
	let isLoading = $state<boolean>(false);
	let isProcessing = $state<boolean>(false);

	// Detail Modal State
	let selectedDc = $state<DCHeaderPosted | null>(null);
	let showDetailModal = $state<boolean>(false);

	// Date preset helper
	function setPreset(preset: 'today' | 'yesterday' | 'week' | 'month' | 'all') {
		datePreset = preset;
		const now = new Date();
		const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).toISOString().split('T')[0];
		const endOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59).toISOString().split('T')[0];

		if (preset === 'today') {
			fromDate = startOfDay(now);
			toDate = endOfDay(now);
		} else if (preset === 'yesterday') {
			const yest = new Date(now);
			yest.setDate(yest.getDate() - 1);
			fromDate = startOfDay(yest);
			toDate = endOfDay(yest);
		} else if (preset === 'week') {
			const weekAgo = new Date(now);
			weekAgo.setDate(weekAgo.getDate() - 7);
			fromDate = startOfDay(weekAgo);
			toDate = endOfDay(now);
		} else if (preset === 'month') {
			const monthAgo = new Date(now);
			monthAgo.setMonth(monthAgo.getMonth() - 1);
			fromDate = startOfDay(monthAgo);
			toDate = endOfDay(now);
		} else if (preset === 'all') {
			fromDate = '';
			toDate = '';
		}
		currentPage = 1;
		selectedDcNos.clear();
		loadPostedDcHeaders();
	}

	onMount(() => {
		setPreset('week');
	});

	async function loadPostedDcHeaders() {
		isLoading = true;
		try {
			const whereClause: any = {};
			const andConditions: any[] = [];

			if (fromDate) {
				const fDate = new Date(fromDate);
				andConditions.push({ date: { gte: fDate.toISOString() } });
			}
			if (toDate) {
				const tDate = new Date(toDate);
				if (toDate.length <= 10) {
					tDate.setHours(23, 59, 59, 999);
				}
				andConditions.push({ date: { lte: tDate.toISOString() } });
			}

			if (filterSkipEWayBill === 'pending') {
				andConditions.push({ skipEWayBill: { eq: 0 } });
			} else if (filterSkipEWayBill === 'skipped') {
				andConditions.push({ skipEWayBill: { ne: 0 } });
			}

			if (andConditions.length > 0) {
				whereClause.and = andConditions;
			}

			const res = await graphqlQuery<any>(GetPostedDcHeadersDocument, {
				variables: {
					skip: 0,
					take: 2000,
					where: Object.keys(whereClause).length > 0 ? whereClause : undefined,
					order: [{ date: 'DESC' }]
				}
			});

			if (res.success && res.data?.getPostedDcHeaders?.items) {
				dcHeaders = res.data.getPostedDcHeaders.items || [];
			} else {
				dcHeaders = [];
			}
		} catch (e) {
			console.error('Failed to load Posted DC Headers', e);
			toast.error('Failed to fetch Posted DC headers from NAV');
		} finally {
			isLoading = false;
		}
	}

	// Client-side search filtering
	let filteredDcHeaders = $derived.by(() => {
		if (!textSearch.trim()) return dcHeaders;
		const q = textSearch.toLowerCase();
		return dcHeaders.filter(dc => {
			return (
				dc.no?.toLowerCase().includes(q) ||
				dc.orderNo?.toLowerCase().includes(q) ||
				dc.vehicleNo?.toLowerCase().includes(q) ||
				dc.transpGSTIN?.toLowerCase().includes(q) ||
				dc.responsibilityCenter?.toLowerCase().includes(q) ||
				dc.transpDocNo?.toLowerCase().includes(q)
			);
		});
	});

	// Pagination slicing
	let pagedDcHeaders = $derived(
		filteredDcHeaders.slice((currentPage - 1) * pageSize, currentPage * pageSize)
	);
	let totalPages = $derived(Math.ceil(filteredDcHeaders.length / pageSize) || 1);

	// Select All logic
	let isAllSelected = $derived(
		pagedDcHeaders.length > 0 && pagedDcHeaders.every(dc => selectedDcNos.has(dc.no))
	);

	function toggleSelectAll() {
		if (isAllSelected) {
			pagedDcHeaders.forEach(dc => selectedDcNos.delete(dc.no));
		} else {
			pagedDcHeaders.forEach(dc => selectedDcNos.add(dc.no));
		}
	}

	function toggleSelectDc(no: string) {
		if (selectedDcNos.has(no)) {
			selectedDcNos.delete(no);
		} else {
			selectedDcNos.add(no);
		}
	}

	async function processSelectedDcHeaders(nosToProcess?: string[]) {
		const targetNos = nosToProcess || Array.from(selectedDcNos);
		if (targetNos.length === 0) {
			toast.error('Please select at least one Posted DC to process.');
			return;
		}

		isProcessing = true;
		try {
			const res = await graphqlMutation<{ processPostedDcEWayBills: { success: boolean; message: string } }>(
				ProcessPostedDcEWayBillsDocument,
				{ dcNumbers: targetNos }
			);

			if (res.success && res.data?.processPostedDcEWayBills?.success) {
				toast.success(res.data.processPostedDcEWayBills.message || `Processed ${targetNos.length} DC(s)`);
				selectedDcNos.clear();
				await loadPostedDcHeaders();
			} else {
				toast.error(res.data?.processPostedDcEWayBills?.message || 'Failed to process E-Way bills');
			}
		} catch (e) {
			console.error('Error processing E-Way bills', e);
			toast.error('Error executing NAV Connector process');
		} finally {
			isProcessing = false;
		}
	}

	function formatDate(dtStr?: string | null): string {
		if (!dtStr) return 'N/A';
		try {
			const d = new Date(dtStr);
			return d.toLocaleDateString('en-IN', {
				day: '2-digit',
				month: 'short',
				year: 'numeric'
			});
		} catch {
			return dtStr;
		}
	}
</script>

<div class="min-h-screen bg-background text-foreground pb-24 pt-2 px-3 max-w-7xl mx-auto space-y-6">
	<!-- Page Header -->
	<PageHeading
		title="E-Way Bill Generation"
		description="Fetch Posted DC Headers (D.C Header (Posted)) and execute NAV Connector E-Way Bill processing"
		icon="truck"
		backHref="/rungstprocess"
		backLabel="Back to GST Center"
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<Button
					variant="default"
					size="sm"
					class="gap-2 rounded-2xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold shadow-md"
					onclick={() => processSelectedDcHeaders()}
					disabled={isProcessing || selectedDcNos.size === 0}
				>
					{#if isProcessing}
						<Loader2 class="size-4 animate-spin" />
						<span>Processing NAV Connector...</span>
					{:else}
						<Icon name="rocket" class="size-4" />
						<span>Process Selected ({selectedDcNos.size})</span>
					{/if}
				</Button>

				<Button
					variant="outline"
					size="sm"
					class="gap-2 rounded-2xl"
					onclick={() => loadPostedDcHeaders()}
					disabled={isLoading}
				>
					<Icon name="refresh-cw" class={`size-4 ${isLoading ? 'animate-spin' : ''}`} />
					<span class="hidden sm:inline">Refresh</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- Filter Control Panel -->
	<Card class="border-border/80 bg-card shadow-md rounded-3xl p-4 space-y-4">
		<!-- Top Bar: Search & Status Filters -->
		<div class="flex flex-wrap items-center justify-between gap-3">
			<div class="relative flex-1 min-w-[260px]">
				<Icon name="search" class="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
				<Input
					type="text"
					placeholder="Search by DC No, Order No, Vehicle No, Transp GSTIN..."
					bind:value={textSearch}
					oninput={() => (currentPage = 1)}
					class="pl-10 pr-4 py-2.5 rounded-2xl border-border/60 bg-background/50 focus:bg-background text-sm transition-all"
				/>
				{#if textSearch}
					<button
						onclick={() => {
							textSearch = '';
							currentPage = 1;
						}}
						class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
					>
						<Icon name="x" class="size-4" />
					</button>
				{/if}
			</div>

			<!-- Status Filter Toggle -->
			<div class="flex items-center gap-1.5 bg-muted/40 p-1 rounded-2xl border border-border/60 text-xs font-semibold">
				<button
					onclick={() => {
						filterSkipEWayBill = 'pending';
						currentPage = 1;
						loadPostedDcHeaders();
					}}
					class={`px-3 py-1.5 rounded-xl transition-all ${
						filterSkipEWayBill === 'pending'
							? 'bg-primary text-primary-foreground font-bold shadow-sm'
							: 'text-muted-foreground hover:text-foreground'
					}`}
				>
					Pending EWB
				</button>
				<button
					onclick={() => {
						filterSkipEWayBill = 'all';
						currentPage = 1;
						loadPostedDcHeaders();
					}}
					class={`px-3 py-1.5 rounded-xl transition-all ${
						filterSkipEWayBill === 'all'
							? 'bg-primary text-primary-foreground font-bold shadow-sm'
							: 'text-muted-foreground hover:text-foreground'
					}`}
				>
					All DCs
				</button>
				<button
					onclick={() => {
						filterSkipEWayBill = 'skipped';
						currentPage = 1;
						loadPostedDcHeaders();
					}}
					class={`px-3 py-1.5 rounded-xl transition-all ${
						filterSkipEWayBill === 'skipped'
							? 'bg-primary text-primary-foreground font-bold shadow-sm'
							: 'text-muted-foreground hover:text-foreground'
					}`}
				>
					Skipped EWB
				</button>
			</div>
		</div>

		<!-- Date Presets & Custom Input Bar -->
		<div class="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-border/40">
			<div class="flex flex-wrap items-center gap-1.5">
				<span class="text-xs font-semibold text-muted-foreground mr-1">Date Range:</span>
				<button
					onclick={() => setPreset('today')}
					class={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all ${
						datePreset === 'today' ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/40 hover:bg-muted text-muted-foreground'
					}`}
				>
					Today
				</button>
				<button
					onclick={() => setPreset('yesterday')}
					class={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all ${
						datePreset === 'yesterday' ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/40 hover:bg-muted text-muted-foreground'
					}`}
				>
					Yesterday
				</button>
				<button
					onclick={() => setPreset('week')}
					class={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all ${
						datePreset === 'week' ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/40 hover:bg-muted text-muted-foreground'
					}`}
				>
					Last 7 Days
				</button>
				<button
					onclick={() => setPreset('month')}
					class={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all ${
						datePreset === 'month' ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/40 hover:bg-muted text-muted-foreground'
					}`}
				>
					Last 30 Days
				</button>
				<button
					onclick={() => setPreset('all')}
					class={`px-3 py-1.5 rounded-xl text-xs font-semibold transition-all ${
						datePreset === 'all' ? 'bg-primary text-primary-foreground shadow-sm' : 'bg-muted/40 hover:bg-muted text-muted-foreground'
					}`}
				>
					All Time
				</button>
			</div>

			<div class="flex items-center gap-2">
				<div class="flex items-center gap-1.5 bg-background/50 border border-border/60 rounded-2xl px-3 py-1 text-xs">
					<span class="text-muted-foreground font-medium">From:</span>
					<input
						type="date"
						bind:value={fromDate}
						onchange={() => {
							datePreset = 'custom';
							currentPage = 1;
							loadPostedDcHeaders();
						}}
						class="bg-transparent text-foreground outline-none font-semibold"
					/>
				</div>
				<div class="flex items-center gap-1.5 bg-background/50 border border-border/60 rounded-2xl px-3 py-1 text-xs">
					<span class="text-muted-foreground font-medium">To:</span>
					<input
						type="date"
						bind:value={toDate}
						onchange={() => {
							datePreset = 'custom';
							currentPage = 1;
							loadPostedDcHeaders();
						}}
						class="bg-transparent text-foreground outline-none font-semibold"
					/>
				</div>
			</div>
		</div>
	</Card>

	<!-- Table Feed -->
	<Card class="border-border/80 bg-card shadow-lg rounded-3xl overflow-hidden">
		{#if isLoading}
			<div class="flex flex-col items-center justify-center p-16 space-y-4">
				<Loader2 class="size-8 animate-spin text-indigo-600" />
				<p class="text-sm font-semibold text-muted-foreground">Loading Posted DC Headers from NAV...</p>
			</div>
		{:else if filteredDcHeaders.length === 0}
			<div class="flex flex-col items-center justify-center p-16 space-y-3 text-center">
				<div class="p-4 rounded-3xl bg-muted/60 text-muted-foreground">
					<Icon name="truck" class="size-8 opacity-60" />
				</div>
				<h3 class="text-lg font-bold">No Posted DC Records Found</h3>
				<p class="text-sm text-muted-foreground max-w-md">
					No records match your selected date range or filter options in table [D.C Header (Posted)].
				</p>
				<Button
					variant="outline"
					size="sm"
					class="rounded-2xl mt-2"
					onclick={() => setPreset('all')}
				>
					Clear Date Filters
				</Button>
			</div>
		{:else}
			<div class="overflow-x-auto">
				<table class="w-full text-left text-sm border-collapse">
					<thead>
						<tr class="border-b border-border/60 bg-muted/40 text-xs uppercase font-bold text-muted-foreground tracking-wider">
							<th class="py-3.5 px-4 w-10 text-center">
								<input
									type="checkbox"
									checked={isAllSelected}
									onchange={toggleSelectAll}
									class="size-4 rounded border-border text-primary focus:ring-primary cursor-pointer"
								/>
							</th>
							<th class="py-3.5 px-4">DC Number</th>
							<th class="py-3.5 px-4">Posting Date</th>
							<th class="py-3.5 px-4">Order No</th>
							<th class="py-3.5 px-4">Vehicle No</th>
							<th class="py-3.5 px-4">Transp GSTIN</th>
							<th class="py-3.5 px-4">Resp Center</th>
							<th class="py-3.5 px-4">EWB Status</th>
							<th class="py-3.5 px-4 text-right">Actions</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border/40">
						{#each pagedDcHeaders as dc (dc.no)}
							{@const isSelected = selectedDcNos.has(dc.no)}
							<tr class={`hover:bg-muted/30 transition-colors ${isSelected ? 'bg-indigo-500/10 dark:bg-indigo-500/15' : ''}`}>
								<!-- Checkbox -->
								<td class="py-3.5 px-4 text-center">
									<input
										type="checkbox"
										checked={isSelected}
										onchange={() => toggleSelectDc(dc.no)}
										class="size-4 rounded border-border text-primary focus:ring-primary cursor-pointer"
									/>
								</td>

								<!-- DC Number -->
								<td class="py-3.5 px-4 font-mono font-bold text-foreground">
									{dc.no}
								</td>

								<!-- Posting Date -->
								<td class="py-3.5 px-4 whitespace-nowrap text-xs">
									<div class="flex items-center gap-1.5">
										<Icon name="calendar" class="size-3.5 text-muted-foreground" />
										<span>{formatDate(dc.date)}</span>
									</div>
								</td>

								<!-- Order No -->
								<td class="py-3.5 px-4 font-mono text-xs text-muted-foreground">
									{dc.orderNo || '-'}
								</td>

								<!-- Vehicle No -->
								<td class="py-3.5 px-4 font-mono text-xs font-semibold">
									{#if dc.vehicleNo}
										<span class="px-2 py-0.5 rounded-lg bg-muted border border-border/60">
											{dc.vehicleNo}
										</span>
									{:else}
										<span class="text-muted-foreground italic">-</span>
									{/if}
								</td>

								<!-- Transp GSTIN -->
								<td class="py-3.5 px-4 font-mono text-xs text-muted-foreground">
									{dc.transpGSTIN || '-'}
								</td>

								<!-- Resp Center -->
								<td class="py-3.5 px-4 text-xs font-medium">
									{dc.responsibilityCenter || dc.serRespCenter || '-'}
								</td>

								<!-- EWB Status -->
								<td class="py-3.5 px-4 whitespace-nowrap">
									{#if dc.skipEWayBill === 0}
										<Badge variant="outline" class="bg-emerald-500/10 border-emerald-500/30 text-emerald-600 dark:text-emerald-400 font-bold rounded-xl px-2.5 py-0.5 text-xs">
											Pending EWB
										</Badge>
									{:else}
										<Badge variant="outline" class="bg-muted border-border/60 text-muted-foreground font-medium rounded-xl px-2.5 py-0.5 text-xs">
											Skipped
										</Badge>
									{/if}
								</td>

								<!-- Actions -->
								<td class="py-3.5 px-4 whitespace-nowrap text-right">
									<div class="flex items-center justify-end gap-1">
										<!-- Process Single DC -->
										<button
											onclick={() => processSelectedDcHeaders([dc.no])}
											class="p-2 rounded-xl bg-indigo-500/10 text-indigo-600 hover:bg-indigo-600 hover:text-white transition-all shadow-sm"
											title="Process E-Way Bill via NAV Connector"
										>
											<Icon name="rocket" class="size-3.5" />
										</button>

										<!-- View Details -->
										<button
											onclick={() => {
												selectedDc = dc;
												showDetailModal = true;
											}}
											class="p-2 rounded-xl bg-primary/10 text-primary hover:bg-primary hover:text-primary-foreground transition-all shadow-sm"
											title="View DC Details"
										>
											<Icon name="eye" class="size-3.5" />
										</button>
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			<!-- Footer -->
			<div class="flex flex-wrap items-center justify-between gap-4 p-4 border-t border-border/60 bg-muted/20 text-xs">
				<div class="text-muted-foreground font-medium">
					Showing <strong>{pagedDcHeaders.length}</strong> of <strong>{filteredDcHeaders.length}</strong> Posted DCs
					{#if selectedDcNos.size > 0}
						• <strong class="text-indigo-600">{selectedDcNos.size} Selected</strong>
					{/if}
				</div>

				<div class="flex items-center gap-2">
					<button
						disabled={currentPage <= 1 || isLoading}
						onclick={() => currentPage--}
						class="px-3 py-1.5 rounded-xl border border-border/60 bg-background hover:bg-muted font-semibold disabled:opacity-40 transition-all"
					>
						Previous
					</button>

					<span class="font-bold text-foreground px-2">Page {currentPage} of {totalPages}</span>

					<button
						disabled={currentPage >= totalPages || isLoading}
						onclick={() => currentPage++}
						class="px-3 py-1.5 rounded-xl border border-border/60 bg-background hover:bg-muted font-semibold disabled:opacity-40 transition-all"
					>
						Next
					</button>
				</div>
			</div>
		{/if}
	</Card>
</div>

<!-- Modal: DC Header Full Details -->
<Dialog.Root bind:open={showDetailModal}>
	<Dialog.Content class="sm:max-w-md rounded-3xl p-6 space-y-4">
		{#if selectedDc}
			<Dialog.Header>
				<Dialog.Title class="text-lg font-bold flex items-center gap-2 text-indigo-600">
					<Icon name="file-text" class="size-5" />
					<span>Posted DC Details</span>
				</Dialog.Title>
			</Dialog.Header>

			<div class="space-y-3 text-sm">
				<div class="p-3.5 rounded-2xl bg-indigo-500/10 border border-indigo-500/20 space-y-1">
					<p class="text-xs font-semibold text-indigo-700 dark:text-indigo-300 uppercase tracking-wider">DC Number</p>
					<h4 class="font-mono font-bold text-lg text-foreground">{selectedDc.no}</h4>
					<p class="text-xs text-muted-foreground">Posting Date: {formatDate(selectedDc.date)}</p>
				</div>

				<div class="grid grid-cols-2 gap-3 text-xs">
					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="font-semibold text-muted-foreground uppercase">Order No</p>
						<p class="font-mono font-bold mt-1 text-foreground">{selectedDc.orderNo || 'N/A'}</p>
					</div>

					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="font-semibold text-muted-foreground uppercase">Vehicle No</p>
						<p class="font-mono font-bold mt-1 text-foreground">{selectedDc.vehicleNo || 'N/A'}</p>
					</div>
				</div>

				<div class="grid grid-cols-2 gap-3 text-xs">
					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="font-semibold text-muted-foreground uppercase">Transp GSTIN</p>
						<p class="font-mono font-bold mt-1 text-foreground">{selectedDc.transpGSTIN || 'N/A'}</p>
					</div>

					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="font-semibold text-muted-foreground uppercase">Resp Center</p>
						<p class="font-semibold mt-1 text-foreground">{selectedDc.responsibilityCenter || selectedDc.serRespCenter || 'N/A'}</p>
					</div>
				</div>

				{#if selectedDc.transpDocNo}
					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40 text-xs">
						<p class="font-semibold text-muted-foreground uppercase">Transport Doc Info</p>
						<p class="font-mono text-foreground mt-1">Doc No: {selectedDc.transpDocNo} • Date: {formatDate(selectedDc.transpDocDate)}</p>
					</div>
				{/if}
			</div>

			<Dialog.Footer class="pt-2 flex gap-2">
				<Button
					variant="default"
					size="sm"
					class="w-full rounded-2xl bg-indigo-600 hover:bg-indigo-700 text-white font-bold gap-2"
					onclick={() => {
						showDetailModal = false;
						processSelectedDcHeaders([selectedDc!.no]);
					}}
				>
					<Icon name="rocket" class="size-4" />
					<span>Process E-Way Bill</span>
				</Button>

				<Button
					variant="outline"
					size="sm"
					class="rounded-2xl text-xs"
					onclick={() => (showDetailModal = false)}
				>
					Close
				</Button>
			</Dialog.Footer>
		{/if}
	</Dialog.Content>
</Dialog.Root>
