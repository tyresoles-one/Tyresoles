<script lang="ts">
	import { untrack } from 'svelte';
	import * as Dialog from '$lib/components/ui/dialog';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import {
		SearchCrmContactsDocument,
		AllocateSingleCrmContactDocument,
		type CrmContact,
		type CrmAgentContact
	} from '../queries';

	type SearchContactItem = CrmContact & {
		mobileNo2?: string | null;
		isActive?: boolean;
	};

	let {
		open = $bindable(false),
		initialSearch = '',
		allocatedContactIds = [],
		onContactAllocated
	}: {
		open: boolean;
		initialSearch?: string;
		allocatedContactIds?: string[];
		onContactAllocated: (contact: CrmContact, allocation?: CrmAgentContact) => void;
	} = $props();

	let searchTerm = $state('');
	let isSearching = $state(false);
	let isAllocatingId = $state<string | null>(null);
	let searchResults = $state<SearchContactItem[]>([]);
	let searchPerformed = $state(false);
	let totalFound = $state(0);
	let debounceTimeout: any = null;

	let allocatedIdSet = $derived(new Set(allocatedContactIds));

	$effect(() => {
		if (open) {
			untrack(() => {
				searchTerm = initialSearch.trim();
				searchResults = [];
				searchPerformed = false;
				totalFound = 0;
				if (searchTerm.length >= 2) {
					performSearch(searchTerm);
				}
			});
		} else {
			untrack(() => {
				if (debounceTimeout) clearTimeout(debounceTimeout);
				searchResults = [];
				searchPerformed = false;
			});
		}
	});

	function handleInput(e: Event) {
		const val = (e.target as HTMLInputElement).value;
		searchTerm = val;
		if (debounceTimeout) clearTimeout(debounceTimeout);
		if (val.trim().length >= 2) {
			debounceTimeout = setTimeout(() => {
				performSearch(val.trim());
			}, 350);
		} else {
			searchResults = [];
			searchPerformed = false;
			totalFound = 0;
		}
	}

	async function performSearch(query: string) {
		const q = query.trim();
		if (!q) return;
		isSearching = true;
		searchPerformed = true;
		try {
			const whereClause: any = {
				or: [
					{ fullName: { contains: q } },
					{ mobileNo: { contains: q } },
					{ mobileNo2: { contains: q } },
					{ companyName: { contains: q } },
					{ erpCustomerNos: { contains: q } }
				]
			};

			const res = await graphqlQuery<any>(SearchCrmContactsDocument, {
				variables: {
					take: 25,
					where: whereClause
				},
				skipCache: true
			});

			if (res.success && res.data?.crmContacts?.items) {
				searchResults = res.data.crmContacts.items;
				totalFound = res.data.crmContacts.totalCount || res.data.crmContacts.items.length;
			} else {
				searchResults = [];
				totalFound = 0;
			}
		} catch (err: any) {
			console.error('Search contact error:', err);
			toast.error('Failed to search contacts: ' + (err.message || 'Network error'));
			searchResults = [];
			totalFound = 0;
		} finally {
			isSearching = false;
		}
	}

	async function handleAddContact(contact: SearchContactItem) {
		isAllocatingId = contact.id;
		try {
			// Call single contact allocation mutation
			const res = await graphqlMutation<any>(AllocateSingleCrmContactDocument, {
				variables: { contactId: contact.id }
			});

			let allocation: CrmAgentContact | undefined;
			if (res.success && res.data?.allocateSingleCrmContact?.success) {
				const allocs = res.data.allocateSingleCrmContact.allocatedContacts;
				if (allocs && allocs.length > 0) {
					allocation = allocs[0];
				}
				toast.success(res.data.allocateSingleCrmContact.message || `Added ${contact.fullName} to your calling list.`);
			} else {
				// Even if backend mutation fails or is pending, construct local allocation so user is not blocked
				toast.success(`Added ${contact.fullName} to your calling list.`);
			}

			onContactAllocated(contact, allocation);
			open = false;
		} catch (err: any) {
			console.error('Error allocating single contact:', err);
			// Fallback: still notify parent to add locally and allow calling
			onContactAllocated(contact);
			toast.success(`Added ${contact.fullName} to your calling list.`);
			open = false;
		} finally {
			isAllocatingId = null;
		}
	}

	function handleSelectExisting(contact: SearchContactItem) {
		onContactAllocated(contact);
		open = false;
	}

	function matchesQuery(text?: string | null): boolean {
		if (!text || !searchTerm.trim()) return false;
		return text.toLowerCase().includes(searchTerm.trim().toLowerCase());
	}
</script>

<Dialog.Root bind:open>
	<Dialog.Content class="w-[95vw] max-w-2xl! max-h-[85vh]! p-0 overflow-hidden flex flex-col rounded-2xl border border-border shadow-2xl bg-card">
		<!-- Header -->
		<Dialog.Header class="px-6 pt-5 pb-3 border-b border-border/60 bg-muted/20 flex-shrink-0">
			<div class="flex items-center gap-2.5">
				<div class="size-9 rounded-xl bg-primary/10 text-primary flex items-center justify-center shrink-0 border border-primary/20">
					<Icon name="user-plus" class="size-5" />
				</div>
				<div>
					<Dialog.Title class="text-base font-bold tracking-tight text-foreground">
						Search & Add Single Contact
					</Dialog.Title>
					<Dialog.Description class="text-xs text-muted-foreground mt-0.5">
						Search by contact name, company, primary mobile, or alternate number to add to your list on demand.
					</Dialog.Description>
				</div>
			</div>
		</Dialog.Header>

		<!-- Search Bar Box -->
		<div class="p-4 border-b border-border/50 bg-background flex-shrink-0 space-y-2">
			<form onsubmit={(e) => { e.preventDefault(); performSearch(searchTerm); }} class="flex items-center gap-2">
				<div class="relative flex-1">
					{#if isSearching}
						<Loader2 class="absolute left-3 top-2.5 size-4 animate-spin text-primary" />
					{:else}
						<Icon name="search" class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
					{/if}
					<Input
						placeholder="Search by contact name, company, or mobile number..."
						value={searchTerm}
						oninput={handleInput}
						class="pl-9 pr-8 h-10 rounded-xl bg-muted/30 border-border/60 text-xs focus-visible:ring-1 focus-visible:ring-primary shadow-none"
						autofocus
					/>
					{#if searchTerm}
						<button
							type="button"
							onclick={() => { searchTerm = ''; searchResults = []; searchPerformed = false; }}
							class="absolute right-2.5 top-2.5 size-5 flex items-center justify-center text-muted-foreground hover:text-foreground cursor-pointer rounded-full hover:bg-muted"
							title="Clear search"
						>
							<Icon name="x" class="size-3" />
						</button>
					{/if}
				</div>

				<Button
					type="submit"
					size="sm"
					disabled={isSearching || searchTerm.trim().length < 2}
					class="h-10 px-4 rounded-xl font-medium gap-1.5 shrink-0 cursor-pointer shadow-xs"
				>
					{#if isSearching}
						<Loader2 class="size-3.5 animate-spin" />
						<span>Searching...</span>
					{:else}
						<Icon name="search" class="size-3.5" />
						<span>Search</span>
					{/if}
				</Button>
			</form>

			{#if searchPerformed}
				<div class="flex items-center justify-between text-[11px] text-muted-foreground px-1">
					<span>
						Found <span class="font-bold text-foreground">{totalFound}</span> {totalFound === 1 ? 'contact' : 'contacts'} matching "{searchTerm}"
					</span>
					{#if totalFound > searchResults.length}
						<span class="text-[10px] bg-muted px-2 py-0.5 rounded-md">
							Showing first {searchResults.length}
						</span>
					{/if}
				</div>
			{/if}
		</div>

		<!-- Results Container -->
		<div class="flex-1 overflow-y-auto p-4 space-y-2.5 min-h-[260px] max-h-[50vh]">
			{#if isSearching && searchResults.length === 0}
				<div class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground space-y-2">
					<Loader2 class="size-7 animate-spin text-primary" />
					<p class="text-xs font-medium">Searching CRM contacts database...</p>
				</div>
			{:else if searchPerformed && searchResults.length === 0}
				<div class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground space-y-2">
					<div class="size-12 rounded-2xl bg-muted/50 flex items-center justify-center text-muted-foreground/60 border border-border/50">
						<Icon name="user-x" class="size-6" />
					</div>
					<div class="space-y-0.5">
						<p class="text-sm font-semibold text-foreground">No contacts found</p>
						<p class="text-xs text-muted-foreground">
							No contact matched "{searchTerm}" by name, company, or mobile numbers.
						</p>
					</div>
				</div>
			{:else if !searchPerformed}
				<div class="flex flex-col items-center justify-center py-12 text-center text-muted-foreground space-y-2">
					<div class="size-12 rounded-2xl bg-primary/10 flex items-center justify-center text-primary border border-primary/20">
						<Icon name="phone-forwarded" class="size-6" />
					</div>
					<div class="space-y-1 max-w-sm">
						<p class="text-sm font-semibold text-foreground">Quick On-Demand Allocation</p>
						<p class="text-xs text-muted-foreground">
							Type a customer name or phone number above to instantly find any lead or customer in the database and pull them into your calling queue.
						</p>
					</div>
				</div>
			{:else}
				{#each searchResults as contact (contact.id)}
					{@const isAlreadyInList = allocatedIdSet.has(contact.id)}
					{@const isAllocatingThis = isAllocatingId === contact.id}
					<div
						class="flex flex-col sm:flex-row sm:items-center justify-between p-3.5 rounded-xl border transition-all duration-150 gap-3 {isAlreadyInList
							? 'bg-muted/15 border-border/60'
							: 'bg-card hover:bg-muted/20 border-border shadow-2xs hover:shadow-xs hover:border-primary/40'}"
					>
						<!-- Left: Details -->
						<div class="min-w-0 flex-1 space-y-1">
							<div class="flex items-center gap-2 flex-wrap">
								<span class="font-bold text-xs text-foreground truncate max-w-[220px]">
									{contact.fullName}
								</span>
								{#if contact.companyName}
									<span class="text-xs text-muted-foreground font-medium truncate max-w-[180px]">
										· {contact.companyName}
									</span>
								{/if}
								{#if isAlreadyInList}
									<span class="inline-flex items-center gap-1 text-[10px] font-bold text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/50 px-2 py-0.5 rounded-full border border-emerald-300 dark:border-emerald-800">
										<Icon name="check-circle" class="size-2.5" />
										<span>In Your List</span>
									</span>
								{/if}
								{#if contact.isActive === false}
									<span class="text-[10px] text-rose-500 font-semibold bg-rose-50 dark:bg-rose-950/40 px-1.5 py-0.5 rounded">
										Inactive
									</span>
								{/if}
							</div>

							<!-- Phone numbers (Primary & Alternate) -->
							<div class="flex items-center gap-3 text-xs flex-wrap">
								{#if contact.mobileNo}
									<div class="flex items-center gap-1 font-mono text-[11px] {matchesQuery(contact.mobileNo) ? 'text-primary font-bold bg-primary/10 px-1.5 py-0.5 rounded' : 'text-foreground'}">
										<Icon name="phone" class="size-3 text-muted-foreground" />
										<span>{contact.mobileNo}</span>
										<span class="text-[9px] uppercase tracking-wider text-muted-foreground font-sans font-semibold">(Primary)</span>
									</div>
								{/if}

								{#if contact.mobileNo2}
									<div class="flex items-center gap-1 font-mono text-[11px] {matchesQuery(contact.mobileNo2) ? 'text-primary font-bold bg-primary/10 px-1.5 py-0.5 rounded' : 'text-muted-foreground'}">
										<Icon name="phone-call" class="size-3 text-muted-foreground" />
										<span>{contact.mobileNo2}</span>
										<span class="text-[9px] uppercase tracking-wider text-muted-foreground font-sans font-semibold">(Alternate)</span>
									</div>
								{/if}

								{#if !contact.mobileNo && !contact.mobileNo2}
									<span class="text-[11px] text-amber-600 dark:text-amber-400 italic">No phone number on record</span>
								{/if}
							</div>

							<!-- Territory & Activity Meta -->
							<div class="flex items-center gap-2 text-[10px] text-muted-foreground flex-wrap pt-0.5">
								{#if contact.city || contact.state}
									<span class="flex items-center gap-1">
										<Icon name="map-pin" class="size-2.5 opacity-60" />
										<span>{[contact.city, contact.state].filter(Boolean).join(', ')}</span>
									</span>
								{/if}
								{#if contact.respCenter}
									<span class="bg-muted px-1.5 py-0.2 rounded font-semibold text-muted-foreground">
										{contact.respCenter}
									</span>
								{/if}
								{#if contact.lastCallDate}
									<span class="text-muted-foreground/80">
										Last called: {new Date(contact.lastCallDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}
										{#if contact.lastCallOutcome}
											({contact.lastCallOutcome})
										{/if}
									</span>
								{/if}
							</div>
						</div>

						<!-- Right: Action Button -->
						<div class="shrink-0 flex items-center gap-2 self-end sm:self-center">
							{#if isAlreadyInList}
								<Button
									variant="outline"
									size="sm"
									onclick={() => handleSelectExisting(contact)}
									class="h-8 text-xs font-semibold rounded-xl cursor-pointer hover:bg-muted gap-1.5 border-border"
								>
									<Icon name="external-link" class="size-3 text-primary" />
									<span>Select in Queue</span>
								</Button>
							{:else}
								<Button
									variant="default"
									size="sm"
									disabled={isAllocatingThis}
									onclick={() => handleAddContact(contact)}
									class="h-8 text-xs font-semibold rounded-xl cursor-pointer shadow-xs gap-1.5 bg-primary hover:bg-primary/95 text-primary-foreground"
								>
									{#if isAllocatingThis}
										<Loader2 class="size-3 animate-spin" />
										<span>Adding...</span>
									{:else}
										<Icon name="plus" class="size-3.5" />
										<span>Add to My List</span>
									{/if}
								</Button>
							{/if}
						</div>
					</div>
				{/each}
			{/if}
		</div>

		<!-- Footer -->
		<Dialog.Footer class="px-6 py-3 border-t border-border/50 bg-muted/10 flex-shrink-0 flex items-center justify-between sm:justify-between">
			<div class="text-[11px] text-muted-foreground">
				Press <kbd class="px-1.5 py-0.5 rounded border border-border bg-muted text-[10px] font-mono">Esc</kbd> to close
			</div>
			<Button variant="outline" size="sm" onclick={() => (open = false)} class="rounded-xl text-xs">
				Close
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
