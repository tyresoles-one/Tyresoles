<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { Input } from '$lib/components/ui/input';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import EmptyState from '$lib/components/venUI/emptyState/EmptyState.svelte';
	import type { ContactInvoice, ContactClaim } from '../queries';

	let {
		type,
		invoices,
		claims,
		loading,
		onPrintDocument
	}: {
		type: 'business' | 'claims';
		invoices: ContactInvoice[];
		claims: ContactClaim[];
		loading: boolean;
		onPrintDocument: (no: string, docType: string) => void;
	} = $props();

	// Sales History Table State
	let invoiceSearch = $state('');
	let invoiceSortField = $state<'date' | 'no' | 'customerName' | 'items' | 'qty' | 'amountToCustomer'>('date');
	let invoiceSortOrder = $state<'ASC' | 'DESC'>('DESC');

	function toggleInvoiceSort(field: 'date' | 'no' | 'customerName' | 'items' | 'qty' | 'amountToCustomer') {
		if (invoiceSortField === field) {
			invoiceSortOrder = invoiceSortOrder === 'ASC' ? 'DESC' : 'ASC';
		} else {
			invoiceSortField = field;
			invoiceSortOrder = (field === 'date' || field === 'qty' || field === 'amountToCustomer') ? 'DESC' : 'ASC';
		}
	}

	let filteredInvoices = $derived.by(() => {
		let list = [...invoices];

		if (invoiceSearch.trim()) {
			const q = invoiceSearch.trim().toLowerCase();
			list = list.filter(
				inv =>
					inv.no?.toLowerCase().includes(q) ||
					inv.customerName?.toLowerCase().includes(q) ||
					inv.items?.toLowerCase().includes(q)
			);
		}

		list.sort((a, b) => {
			let valA: any = a[invoiceSortField];
			let valB: any = b[invoiceSortField];

			if (invoiceSortField === 'date') {
				valA = a.date ? new Date(a.date).getTime() : 0;
				valB = b.date ? new Date(b.date).getTime() : 0;
			} else if (invoiceSortField === 'qty') {
				valA = a.qty ?? 0;
				valB = b.qty ?? 0;
			} else if (invoiceSortField === 'amountToCustomer') {
				valA = a.amountToCustomer ?? 0;
				valB = b.amountToCustomer ?? 0;
			} else {
				valA = (valA ?? '').toString().toLowerCase();
				valB = (valB ?? '').toString().toLowerCase();
			}

			if (valA < valB) return invoiceSortOrder === 'ASC' ? -1 : 1;
			if (valA > valB) return invoiceSortOrder === 'ASC' ? 1 : -1;
			return 0;
		});

		return list;
	});

	// Claim History Table State
	let claimSearch = $state('');
	let claimSortField = $state<'date' | 'no' | 'itemNo' | 'faultDescription' | 'decision' | 'compensationAmount'>('date');
	let claimSortOrder = $state<'ASC' | 'DESC'>('DESC');

	function toggleClaimSort(field: 'date' | 'no' | 'itemNo' | 'faultDescription' | 'decision' | 'compensationAmount') {
		if (claimSortField === field) {
			claimSortOrder = claimSortOrder === 'ASC' ? 'DESC' : 'ASC';
		} else {
			claimSortField = field;
			claimSortOrder = (field === 'date' || field === 'compensationAmount') ? 'DESC' : 'ASC';
		}
	}

	let filteredClaims = $derived.by(() => {
		let list = [...claims];

		if (claimSearch.trim()) {
			const q = claimSearch.trim().toLowerCase();
			list = list.filter(
				c =>
					c.no?.toLowerCase().includes(q) ||
					c.itemNo?.toLowerCase().includes(q) ||
					c.serialNo?.toLowerCase().includes(q) ||
					c.make?.toLowerCase().includes(q) ||
					c.faultDescription?.toLowerCase().includes(q) ||
					c.decision?.toLowerCase().includes(q) ||
					c.mobileNo?.toLowerCase().includes(q)
			);
		}

		list.sort((a, b) => {
			let valA: any = a[claimSortField];
			let valB: any = b[claimSortField];

			if (claimSortField === 'date') {
				valA = a.date ? new Date(a.date).getTime() : 0;
				valB = b.date ? new Date(b.date).getTime() : 0;
			} else if (claimSortField === 'compensationAmount') {
				valA = a.compensationAmount ?? 0;
				valB = b.compensationAmount ?? 0;
			} else {
				valA = (valA ?? '').toString().toLowerCase();
				valB = (valB ?? '').toString().toLowerCase();
			}

			if (valA < valB) return claimSortOrder === 'ASC' ? -1 : 1;
			if (valA > valB) return claimSortOrder === 'ASC' ? 1 : -1;
			return 0;
		});

		return list;
	});

	function formatInvoiceDate(dateStr: string | null | undefined) {
		if (!dateStr) return '—';
		const date = new Date(dateStr);
		return date.toLocaleDateString('en-IN', {
			day: '2-digit',
			month: 'short',
			year: 'numeric'
		});
	}

	function formatCurrency(amount: number) {
		return new Intl.NumberFormat('en-IN', {
			style: 'currency',
			currency: 'INR',
			minimumFractionDigits: 2
		}).format(amount);
	}

	function getDecisionBadgeClass(decision: string) {
		if (!decision) return 'bg-muted text-muted-foreground';
		const dec = decision.toLowerCase();
		if (dec.includes('reject')) {
			return 'bg-rose-500/10 text-rose-600 dark:text-rose-400';
		}
		if (
			dec.includes('repair') ||
			dec.includes('retread') ||
			dec.includes('replace') ||
			dec.includes('credit')
		) {
			return 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400';
		}
		return 'bg-amber-500/10 text-amber-600 dark:text-amber-400';
	}
</script>

{#if type === 'business'}
	{#if loading}
		<div class="flex justify-center py-12">
			<Loader2 class="size-6 animate-spin text-primary" />
		</div>
	{:else if invoices.length === 0}
		<EmptyState
			icon="receipt"
			title="No Sales History"
			description="No sales history found for this contact."
			class="py-8"
		/>
	{:else}
		<div class="space-y-3">
			<!-- Filter & Toolbar -->
			<div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-muted/20 p-2.5 rounded-xl border border-border">
				<div class="relative flex-1 max-w-sm">
					<Icon name="search" class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
					<Input
						placeholder="Filter by doc no, customer, items..."
						bind:value={invoiceSearch}
						class="pl-9 pr-8 h-9 rounded-lg text-xs bg-background"
					/>
					{#if invoiceSearch}
						<button
							type="button"
							onclick={() => (invoiceSearch = '')}
							class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
						>
							<Icon name="x" class="size-3.5" />
						</button>
					{/if}
				</div>
				<div class="text-xs text-muted-foreground font-medium self-end sm:self-center px-1">
					Showing {filteredInvoices.length} of {invoices.length} {invoices.length === 1 ? 'invoice' : 'invoices'}
				</div>
			</div>

			<!-- Sales Table -->
			{#if filteredInvoices.length === 0}
				<div class="p-8 text-center border border-border rounded-xl bg-card space-y-1">
					<p class="text-sm font-semibold">No matching sales records found</p>
					<p class="text-xs text-muted-foreground">Try adjusting your filter query.</p>
				</div>
			{:else}
				<div class="overflow-x-auto border border-border rounded-xl">
					<table class="w-full text-left border-collapse text-xs md:text-sm">
						<thead>
							<tr class="border-b border-border bg-muted/30">
								<th
									onclick={() => toggleInvoiceSort('date')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Date</span>
										{#if invoiceSortField === 'date'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleInvoiceSort('no')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Doc No</span>
										{#if invoiceSortField === 'no'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleInvoiceSort('customerName')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Customer Name</span>
										{#if invoiceSortField === 'customerName'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleInvoiceSort('items')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Items</span>
										{#if invoiceSortField === 'items'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleInvoiceSort('qty')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] text-right cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center justify-end gap-1">
										<span>Qty</span>
										{#if invoiceSortField === 'qty'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleInvoiceSort('amountToCustomer')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] text-right cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center justify-end gap-1">
										<span>Amount</span>
										{#if invoiceSortField === 'amountToCustomer'}
											<Icon name={invoiceSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-border">
							{#each filteredInvoices as inv (inv.no)}
								<tr onclick={() => onPrintDocument(inv.no, 'Invoice')} class="hover:bg-muted/10 transition-colors cursor-pointer group">
									<td class="p-3 whitespace-nowrap font-medium text-muted-foreground">
										{formatInvoiceDate(inv.date)}
									</td>
									<td class="p-3 font-mono font-bold text-indigo-600 dark:text-indigo-400 whitespace-nowrap">
										<div class="flex items-center gap-1.5">
											<span>{inv.no}</span>
											<Icon name="file-text" class="size-3.5 opacity-0 group-hover:opacity-100 transition-opacity text-indigo-500 shrink-0" />
										</div>
									</td>
									<td class="p-3 max-w-[180px] sm:max-w-[220px]">
										<span class="font-medium text-foreground line-clamp-1" title={inv.customerName || ''}>
											{inv.customerName || '—'}
										</span>
									</td>
									<td class="p-3 max-w-[280px] sm:max-w-[400px]">
										<span class="line-clamp-2 leading-relaxed" title={inv.items}>
											{inv.items || 'No item details'}
										</span>
									</td>
									<td class="p-3 text-right font-medium whitespace-nowrap">
										{inv.qty.toLocaleString('en-IN')}
									</td>
									<td class="p-3 text-right font-bold text-foreground/90 whitespace-nowrap">
										{formatCurrency(inv.amountToCustomer)}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			{/if}
		</div>
	{/if}
{:else if type === 'claims'}
	{#if loading}
		<div class="flex justify-center py-12">
			<Loader2 class="size-6 animate-spin text-primary" />
		</div>
	{:else if claims.length === 0}
		<EmptyState
			icon="file-search"
			title="No Claim History"
			description="No claim history found for this contact."
			class="py-8"
		/>
	{:else}
		<div class="space-y-3">
			<!-- Filter & Toolbar -->
			<div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-muted/20 p-2.5 rounded-xl border border-border">
				<div class="relative flex-1 max-w-sm">
					<Icon name="search" class="absolute left-3 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
					<Input
						placeholder="Filter by claim no, item, fault, decision..."
						bind:value={claimSearch}
						class="pl-9 pr-8 h-9 rounded-lg text-xs bg-background"
					/>
					{#if claimSearch}
						<button
							type="button"
							onclick={() => (claimSearch = '')}
							class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
						>
							<Icon name="x" class="size-3.5" />
						</button>
					{/if}
				</div>
				<div class="text-xs text-muted-foreground font-medium self-end sm:self-center px-1">
					Showing {filteredClaims.length} of {claims.length} {claims.length === 1 ? 'claim' : 'claims'}
				</div>
			</div>

			<!-- Claims Table -->
			{#if filteredClaims.length === 0}
				<div class="p-8 text-center border border-border rounded-xl bg-card space-y-1">
					<p class="text-sm font-semibold">No matching claim records found</p>
					<p class="text-xs text-muted-foreground">Try adjusting your filter query.</p>
				</div>
			{:else}
				<div class="overflow-x-auto border border-border rounded-xl">
					<table class="w-full text-left border-collapse text-xs md:text-sm">
						<thead>
							<tr class="border-b border-border bg-muted/30">
								<th
									onclick={() => toggleClaimSort('date')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Date</span>
										{#if claimSortField === 'date'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleClaimSort('no')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Claim No</span>
										{#if claimSortField === 'no'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleClaimSort('itemNo')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Item / Serial / Make</span>
										{#if claimSortField === 'itemNo'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleClaimSort('faultDescription')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Fault Description</span>
										{#if claimSortField === 'faultDescription'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleClaimSort('decision')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center gap-1">
										<span>Decision</span>
										{#if claimSortField === 'decision'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
								<th
									onclick={() => toggleClaimSort('compensationAmount')}
									class="p-3 font-semibold text-muted-foreground uppercase tracking-wider text-[10px] text-right cursor-pointer hover:bg-muted/50 transition-colors select-none group/th"
								>
									<div class="flex items-center justify-end gap-1">
										<span>Compensation</span>
										{#if claimSortField === 'compensationAmount'}
											<Icon name={claimSortOrder === 'ASC' ? 'arrow-up' : 'arrow-down'} class="size-3 text-primary shrink-0" />
										{:else}
											<Icon name="chevrons-up-down" class="size-3 opacity-0 group-hover/th:opacity-50 shrink-0" />
										{/if}
									</div>
								</th>
							</tr>
						</thead>
						<tbody class="divide-y divide-border">
							{#each filteredClaims as claim (claim.no)}
								<tr onclick={() => onPrintDocument(claim.no, 'Claim')} class="hover:bg-muted/10 transition-colors cursor-pointer group">
									<td class="p-3 whitespace-nowrap font-medium text-muted-foreground">
										{formatInvoiceDate(claim.date)}
									</td>
									<td class="p-3 font-mono font-bold text-indigo-600 dark:text-indigo-400 whitespace-nowrap">
										<div class="flex items-center gap-1.5">
											<span>{claim.no}</span>
											<Icon name="file-text" class="size-3.5 opacity-0 group-hover:opacity-100 transition-opacity text-indigo-500 shrink-0" />
										</div>
										{#if claim.mobileNo}
											<div class="text-[10px] font-normal text-muted-foreground font-sans mt-0.5">{claim.mobileNo}</div>
										{/if}
									</td>
									<td class="p-3 max-w-[200px] truncate">
										<div class="font-medium text-foreground">{claim.itemNo || '—'}</div>
										<div class="text-[10px] text-muted-foreground">
											{claim.serialNo || 'No Serial'}
											{#if claim.make}
												<span class="mx-1">•</span>{claim.make}
											{/if}
										</div>
									</td>
									<td class="p-3 max-w-[220px]">
										<span class="line-clamp-2 leading-relaxed" title={claim.faultDescription}>
											{claim.faultDescription || 'No description'}
										</span>
									</td>
									<td class="p-3 whitespace-nowrap">
										{#if claim.decision}
											<span class="text-[10px] px-2.5 py-0.5 rounded-full font-bold uppercase tracking-wider {getDecisionBadgeClass(claim.decision)}">
												{claim.decision}
											</span>
										{:else}
											<span class="text-xs text-muted-foreground">—</span>
										{/if}
									</td>
									<td class="p-3 text-right font-bold text-foreground/90 whitespace-nowrap">
										{formatCurrency(claim.compensationAmount)}
									</td>
								</tr>
							{/each}
						</tbody>
					</table>
				</div>
			{/if}
		</div>
	{/if}
{/if}
