<script lang="ts">
	import { untrack } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import * as DropdownMenu from '$lib/components/ui/dropdown-menu';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import type { CrmContact } from '../queries';
	import CallStatusBadge from './CallStatusBadge.svelte';

	let {
		list,
		filteredContacts,
		selectedContact = $bindable(),
		isAllocating,
		isFetchingContactDetails = false,
		filterCallDate = $bindable('pending'),
		onSelectContact,
		onLoadSummary,
		onRequestMoreContacts,
		onQuickLoadContacts,
		onOpenSearchSingle,
		onBulkDeallocate,
		isBulkDeallocating = false
	}: {
		list: any;
		filteredContacts: CrmContact[];
		selectedContact: CrmContact | null;
		isAllocating: boolean;
		isFetchingContactDetails?: boolean;
		filterCallDate?: string;
		onSelectContact: (contact: CrmContact) => void;
		onLoadSummary?: () => void;
		onRequestMoreContacts: () => void;
		onQuickLoadContacts?: (limit: number) => Promise<void>;
		onOpenSearchSingle?: (initialTerm?: string) => void;
		onBulkDeallocate?: (ids: string[]) => Promise<void>;
		isBulkDeallocating?: boolean;
	} = $props();

	let selectedIds = $state<string[]>([]);

	function getInitialToolbarCollapsed(): boolean {
		try {
			return localStorage.getItem('crm_calling_toolbar_collapsed') === 'true';
		} catch {
			return false;
		}
	}

	let isToolbarCollapsed = $state<boolean>(getInitialToolbarCollapsed());

	function toggleToolbar() {
		isToolbarCollapsed = !isToolbarCollapsed;
		try {
			localStorage.setItem('crm_calling_toolbar_collapsed', String(isToolbarCollapsed));
		} catch {}
	}

	// Whenever filter changes, reset ticked selection to prevent accidental actions
	$effect(() => {
		const _ = filterCallDate;
		untrack(() => {
			selectedIds = [];
		});
	});

	function isToday(dateStr?: string | null): boolean {
		if (!dateStr) return false;
		const d = new Date(dateStr);
		const now = new Date();
		return (
			d.getFullYear() === now.getFullYear() &&
			d.getMonth() === now.getMonth() &&
			d.getDate() === now.getDate()
		);
	}

	// Outcome & status counts derived from all allocated contacts
	let todayCount = $derived(
		(list.items || []).filter((ac: any) => {
			const c = ac.contact || ac;
			const lastDate = ac.lastCallDate || c.lastCallDate;
			const outcome = ac.lastCallOutcome || c.lastCallOutcome;
			const count = ac.callCount ?? c.callCount;
			if (isToday(lastDate)) return true;
			if (!lastDate || !outcome || outcome.trim() === '' || count === 0) return true;
			return false;
		}).length
	);

	let calledTodayCount = $derived(
		(list.items || []).filter((ac: any) => {
			const c = ac.contact || ac;
			const lastDate = ac.lastCallDate || c.lastCallDate;
			return isToday(lastDate);
		}).length
	);

	let pendingCount = $derived(
		(list.items || []).filter((ac: any) => {
			const c = ac.contact || ac;
			const lastDate = ac.lastCallDate || c.lastCallDate;
			const outcome = ac.lastCallOutcome || c.lastCallOutcome;
			const count = ac.callCount ?? c.callCount;
			return !outcome || outcome.trim() === '' || !lastDate || count === 0;
		}).length
	);

	let allCount = $derived((list.items || []).length);

	let distinctOutcomes = $derived.by(() => {
		const counts = new Map<string, number>();
		for (const ac of (list.items || [])) {
			const o = ac.lastCallOutcome?.trim();
			if (o) {
				counts.set(o, (counts.get(o) || 0) + 1);
			}
		}
		return Array.from(counts.entries()).map(([name, count]) => ({ name, count }));
	});

	function isUntouchedContact(c?: any): boolean {
		if (!c) return false;
		const contact = c.contact || c;
		const callCount = c.callCount ?? contact.callCount ?? 0;
		const lastDate = c.lastCallDate || contact.lastCallDate;
		const outcome = c.lastCallOutcome || contact.lastCallOutcome;
		return callCount === 0 && !lastDate && (!outcome || outcome.trim() === '');
	}

	// Only untouched / pending contacts can be selected for deallocation
	let selectableContacts = $derived(
		filteredContacts.filter((c) => {
			const ac = (list.items || []).find((item: any) => item.contactId === c.id || item.contact?.id === c.id);
			return isUntouchedContact(ac || c);
		})
	);

	let isAllSelected = $derived(
		selectableContacts.length > 0 && selectableContacts.every((c) => selectedIds.includes(c.id))
	);
	let isSomeSelected = $derived(
		selectableContacts.some((c) => selectedIds.includes(c.id)) && !isAllSelected
	);

	function toggleSelect(id: string) {
		const target = (list.items || []).find((ac: any) => ac.contactId === id || ac.contact?.id === id);
		const c = filteredContacts.find((x) => x.id === id);
		if (!isUntouchedContact(target || c)) return;

		if (selectedIds.includes(id)) {
			selectedIds = selectedIds.filter((x) => x !== id);
		} else {
			selectedIds = [...selectedIds, id];
		}
	}

	function toggleSelectAll() {
		if (isAllSelected) {
			const selectableSet = new Set(selectableContacts.map((c) => c.id));
			selectedIds = selectedIds.filter((id) => !selectableSet.has(id));
		} else {
			const set = new Set([...selectedIds, ...selectableContacts.map((c) => c.id)]);
			selectedIds = Array.from(set);
		}
	}

	async function handleBulkDeallocateClick() {
		const validUntouchedIds = selectedIds.filter((id) => {
			const target = (list.items || []).find((ac: any) => ac.contactId === id || ac.contact?.id === id);
			const c = filteredContacts.find((x) => x.id === id);
			return isUntouchedContact(target || c);
		});
		if (validUntouchedIds.length === 0 || !onBulkDeallocate) return;
		await onBulkDeallocate(validUntouchedIds);
		selectedIds = [];
	}

	let currentFilterLabel = $derived.by(() => {
		if (filterCallDate === 'today') return "Today's Contacts";
		if (filterCallDate === 'called_today') return 'Called Today';
		if (filterCallDate === 'pending') return 'Untouched';
		if (filterCallDate === 'all') return 'All Contacts';
		if (filterCallDate === 'recent_7d') return 'Recent 7D';
		if (filterCallDate === 'connected') return 'Connected';
		if (filterCallDate === 'positive') return 'Positive';
		if (filterCallDate === 'followup') return 'Reminders';
		return filterCallDate;
	});
</script>

<div class="w-full md:w-[380px] border-r border-border bg-card flex flex-col h-full min-h-0 shrink-0 {selectedContact ? 'hidden md:flex' : 'flex'}">
	<div class="p-2.5 border-b border-border space-y-2">
		<!-- Search Input & Total Contacts + Toggle Toolbar + Split Button Get Contacts -->
		<div class="flex items-center gap-1.5">
			<div class="relative flex-1 min-w-0">
				{#if list.loading}
					<Loader2 class="absolute left-2.5 top-2.5 size-4 animate-spin text-primary" />
				{:else}
					<Icon name="search" class="absolute left-2.5 top-2.5 size-4 text-muted-foreground" />
				{/if}
				<Input
					placeholder="Search..."
					bind:value={list.searchQuery.value}
					class="pl-8 pr-10 rounded-xl h-9 bg-muted/30 focus-visible:ring-1 focus-visible:ring-ring border border-border/50 text-xs shadow-none"
				/>
				<span class="absolute right-2 top-2 text-[10px] font-bold text-muted-foreground bg-muted/70 px-1.5 py-0.5 rounded-md pointer-events-none">
					{filteredContacts.length}
				</span>
			</div>

			<!-- Toggle Button for red marked toolbar -->
			<Button
				variant="outline"
				size="sm"
				onclick={toggleToolbar}
				class="h-9 w-9 p-0 rounded-xl border-border bg-muted/20 hover:bg-muted/50 shrink-0 cursor-pointer transition-colors {isToolbarCollapsed && selectedIds.length === 0 ? 'text-muted-foreground' : 'text-primary bg-primary/10 border-primary/30'}"
				title={isToolbarCollapsed ? 'Show selection and filter toolbar' : 'Hide selection and filter toolbar'}
			>
				<Icon name="sliders-horizontal" class="size-3.5" />
			</Button>

			<!-- Split Button: Get Contacts -->
			<div class="inline-flex rounded-xl shadow-2xs border border-border/60 bg-muted/20 shrink-0 overflow-hidden">
				<Button
					variant="ghost"
					size="sm"
					onclick={() => onQuickLoadContacts ? onQuickLoadContacts(10) : onRequestMoreContacts()}
					disabled={isAllocating}
					class="h-9 px-2 rounded-none text-xs font-semibold flex items-center gap-1.5 hover:bg-muted/60 shrink-0 cursor-pointer border-r border-border/50"
					title="Quick load next 10 contacts using saved filter"
				>
					{#if isAllocating}
						<Loader2 class="size-3.5 animate-spin text-muted-foreground" />
						<span class="hidden sm:inline">Getting...</span>
					{:else}
						<Icon name="user-plus" class="size-3.5 text-primary" />
						<span>Get Contacts</span>
					{/if}
				</Button>

				<DropdownMenu.Root>
					<DropdownMenu.Trigger>
						{#snippet child({ props })}
							<Button
								variant="ghost"
								size="sm"
								disabled={isAllocating}
								class="h-9 w-6.5 p-0 rounded-none hover:bg-muted/60 cursor-pointer flex items-center justify-center text-muted-foreground hover:text-foreground"
								title="Load options"
								{...props}
							>
								<Icon name="chevron-down" class="size-3.5" />
							</Button>
						{/snippet}
					</DropdownMenu.Trigger>
					<DropdownMenu.Content align="end" class="w-56 p-1 bg-card border border-border shadow-lg rounded-xl text-xs z-50">
						<DropdownMenu.Label class="px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
							Quick Load Next Contacts
						</DropdownMenu.Label>
						<DropdownMenu.Item
							onclick={() => onQuickLoadContacts ? onQuickLoadContacts(10) : onRequestMoreContacts()}
							class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted font-medium"
						>
							<span class="flex items-center gap-2">
								<Icon name="plus-circle" class="size-3.5 text-primary" />
								<span>Load Next 10 Contacts</span>
							</span>
							<span class="text-[10px] bg-primary/10 text-primary px-1.5 py-0.5 rounded font-bold">+10 (Default)</span>
						</DropdownMenu.Item>
						<DropdownMenu.Item
							onclick={() => onQuickLoadContacts ? onQuickLoadContacts(20) : onRequestMoreContacts()}
							class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted font-medium"
						>
							<span class="flex items-center gap-2">
								<Icon name="plus-circle" class="size-3.5 text-blue-500" />
								<span>Load Next 20 Contacts</span>
							</span>
							<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded font-bold text-muted-foreground">+20 (Optional)</span>
						</DropdownMenu.Item>
						<DropdownMenu.Separator class="my-1 bg-border/50" />
						<DropdownMenu.Item
							onclick={() => onOpenSearchSingle ? onOpenSearchSingle(list.searchQuery.value) : null}
							class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted font-medium text-foreground"
						>
							<span class="flex items-center gap-2">
								<Icon name="search" class="size-3.5 text-amber-500" />
								<span>Search & Add Contact...</span>
							</span>
							<span class="text-[10px] bg-amber-500/10 text-amber-600 dark:text-amber-400 px-1.5 py-0.5 rounded font-bold">On Demand</span>
						</DropdownMenu.Item>
						<DropdownMenu.Separator class="my-1 bg-border/50" />
						<DropdownMenu.Item
							onclick={onRequestMoreContacts}
							class="flex items-center gap-2 px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted font-medium text-foreground"
						>
							<Icon name="sliders" class="size-3.5 text-muted-foreground" />
							<span>Change Filter Criteria...</span>
						</DropdownMenu.Item>
					</DropdownMenu.Content>
				</DropdownMenu.Root>
			</div>
		</div>

		{#if !isToolbarCollapsed || selectedIds.length > 0}
			<!-- Toolbar Row: Select All Checkbox + One-Click Bulk Deallocate + Compact Outcome Filter -->
			<div class="flex items-center justify-between gap-2 pt-1 border-t border-border/50 text-xs transition-all duration-200">
				<!-- Select All Checkbox -->
				<label class="flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground cursor-pointer select-none {selectableContacts.length === 0 ? 'opacity-40 cursor-not-allowed' : ''}">
					<input
						type="checkbox"
						checked={isAllSelected}
						indeterminate={isSomeSelected}
						onchange={toggleSelectAll}
						disabled={selectableContacts.length === 0}
						class="size-4 rounded border-border text-primary focus:ring-primary/30 cursor-pointer accent-primary disabled:cursor-not-allowed"
					/>
					<span class="text-[11px] font-semibold">
						{#if selectedIds.length > 0}
							<span class="text-foreground font-bold">{selectedIds.length}</span> of {selectableContacts.length}
						{:else if selectableContacts.length > 0}
							Select All ({selectableContacts.length})
						{:else}
							Select All (0)
						{/if}
					</span>
				</label>

				<!-- Right: Bulk Deallocate (when selected) + Compact Outcome Filter -->
				<div class="flex items-center gap-1.5 shrink-0">
					{#if selectedIds.length > 0}
						<Button
							variant="destructive"
							size="sm"
							onclick={handleBulkDeallocateClick}
							disabled={isBulkDeallocating}
							class="h-7 px-2 text-[11px] font-bold gap-1 bg-rose-600 hover:bg-rose-700 text-white rounded-lg shadow-2xs transition-all cursor-pointer animate-in fade-in zoom-in-95 duration-150"
							title="Deallocate all {selectedIds.length} selected contacts"
						>
							{#if isBulkDeallocating}
								<Loader2 class="size-3 animate-spin" />
								<span>Deallocating...</span>
							{:else}
								<Icon name="user-minus" class="size-3" />
								<span>Deallocate ({selectedIds.length})</span>
							{/if}
						</Button>
					{/if}

					<!-- Compact Outcome Filter Dropdown Button -->
					<DropdownMenu.Root>
						<DropdownMenu.Trigger>
							{#snippet child({ props })}
								<Button
									variant="outline"
									size="sm"
									class="h-7 px-2 rounded-lg text-[11px] font-semibold flex items-center gap-1 border-border bg-muted/20 hover:bg-muted/40 cursor-pointer shrink-0"
									title="Filter contacts by outcome"
									{...props}
								>
									<Icon name="filter" class="size-3 text-muted-foreground" />
									<span class="max-w-[85px] truncate">{currentFilterLabel}</span>
									<Icon name="chevron-down" class="size-2.5 opacity-60 shrink-0" />
								</Button>
							{/snippet}
						</DropdownMenu.Trigger>
						<DropdownMenu.Content align="end" class="w-56 p-1 bg-card border border-border shadow-lg rounded-xl text-xs z-50">
							<DropdownMenu.Label class="px-2 py-1 text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
								Filter By Status / Outcome
							</DropdownMenu.Label>
							<DropdownMenu.Item
								onclick={() => (filterCallDate = 'today')}
								class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted {filterCallDate === 'today' ? 'bg-primary/10 font-bold text-primary' : ''}"
							>
								<span class="flex items-center gap-2">
									<Icon name="calendar" class="size-3.5 text-primary" />
									<span>Today's Contacts</span>
								</span>
								<span class="text-[10px] bg-primary/10 text-primary px-1.5 py-0.5 rounded-md font-semibold">
									{todayCount}
								</span>
							</DropdownMenu.Item>
							<DropdownMenu.Item
								onclick={() => (filterCallDate = 'pending')}
								class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted {filterCallDate === 'pending' ? 'bg-primary/10 font-bold text-primary' : ''}"
							>
								<span class="flex items-center gap-2">
									<Icon name="sparkles" class="size-3.5 text-amber-500" />
									<span>Untouched / Pending</span>
								</span>
								<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded-md font-semibold text-muted-foreground">
									{pendingCount}
								</span>
							</DropdownMenu.Item>
							<DropdownMenu.Item
								onclick={() => (filterCallDate = 'called_today')}
								class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted {filterCallDate === 'called_today' ? 'bg-primary/10 font-bold text-primary' : ''}"
							>
								<span class="flex items-center gap-2">
									<Icon name="phone" class="size-3.5 text-emerald-500" />
									<span>Called Today</span>
								</span>
								<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded-md font-semibold text-muted-foreground">
									{calledTodayCount}
								</span>
							</DropdownMenu.Item>
							<DropdownMenu.Item
								onclick={() => (filterCallDate = 'all')}
								class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted {filterCallDate === 'all' ? 'bg-primary/10 font-bold text-primary' : ''}"
							>
								<span class="flex items-center gap-2">
									<Icon name="users" class="size-3.5 text-blue-500" />
									<span>All Contacts</span>
								</span>
								<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded-md font-semibold text-muted-foreground">
									{allCount}
								</span>
							</DropdownMenu.Item>

							{#if distinctOutcomes.length > 0}
								<DropdownMenu.Separator class="my-1 bg-border/50" />
								<DropdownMenu.Label class="px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
									By Call Outcome
								</DropdownMenu.Label>
								{#each distinctOutcomes as out}
									<DropdownMenu.Item
										onclick={() => (filterCallDate = out.name)}
										class="flex items-center justify-between px-2 py-1.5 rounded-lg cursor-pointer hover:bg-muted {filterCallDate === out.name ? 'bg-primary/10 font-bold text-primary' : ''}"
									>
										<span class="truncate max-w-[140px]">{out.name}</span>
										<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded-md font-semibold text-muted-foreground">
											{out.count}
										</span>
									</DropdownMenu.Item>
								{/each}
							{/if}
						</DropdownMenu.Content>
					</DropdownMenu.Root>
				</div>
			</div>
		{/if}

		<!-- Active KPI filter indicator (when filtered from top bar badges or specific outcome) -->
		{#if filterCallDate && filterCallDate !== 'pending' && filterCallDate !== 'all' && filterCallDate !== 'today' && filterCallDate !== 'called_today'}
			<div class="flex items-center justify-between px-2.5 py-1 rounded-lg bg-primary/5 border border-primary/20 text-[11px] text-muted-foreground">
				<span class="flex items-center gap-1.5">
					<span class="size-1.5 rounded-full bg-primary animate-pulse"></span>
					Filtered: <strong class="text-foreground capitalize">{filterCallDate === 'recent_7d' ? 'Recent 7 Days' : filterCallDate === 'followup' ? 'Reminders / Follow-up' : filterCallDate}</strong>
				</span>
				<button
					type="button"
					onclick={() => (filterCallDate = 'pending')}
					class="text-[10px] font-bold text-primary hover:underline cursor-pointer"
				>
					Reset to Untouched
				</button>
			</div>
		{/if}
	</div>

	<!-- List Container -->
	<div class="flex-1 overflow-y-auto divide-y divide-border">
		{#if list.loading && list.items.length === 0}
			<div class="flex items-center justify-center h-48">
				<Loader2 class="size-6 animate-spin text-primary" />
			</div>
		{:else if list.items.length === 0}
			<div class="p-6 text-center flex flex-col items-center justify-center space-y-3.5 my-auto">
				<div class="p-3 rounded-full bg-primary/10 text-primary">
					<Icon name="user-plus" class="size-6" />
				</div>
				<div class="space-y-1">
					<h4 class="font-semibold text-sm text-foreground">No Contacts Loaded</h4>
					<p class="text-xs text-muted-foreground max-w-[240px] leading-relaxed">
						Contacts are retrieved on manual demand. Click below to select criteria and fetch contacts to call.
					</p>
				</div>
				<div class="flex flex-col gap-2 w-full max-w-[240px] pt-1">
					<Button
						size="sm"
						onclick={() => onQuickLoadContacts ? onQuickLoadContacts(10) : onRequestMoreContacts()}
						disabled={isAllocating}
						class="w-full gap-2 rounded-xl text-xs font-semibold h-9.5 cursor-pointer shadow-xs"
					>
						{#if isAllocating}
							<Loader2 class="size-3.5 animate-spin text-muted-foreground" />
							<span>Getting contacts...</span>
						{:else}
							<Icon name="user-plus" class="size-4" />
							<span>Get 10 Contacts</span>
						{/if}
					</Button>
					<Button
						variant="outline"
						size="sm"
						onclick={onRequestMoreContacts}
						disabled={isAllocating}
						class="w-full rounded-xl text-xs font-medium h-9 cursor-pointer border-border hover:bg-muted/50 gap-2 justify-center"
					>
						<Icon name="sliders" class="size-3.5 text-muted-foreground" />
						<span>Filter Options</span>
					</Button>
					<Button
						variant="outline"
						size="sm"
						onclick={() => onOpenSearchSingle ? onOpenSearchSingle() : null}
						class="w-full rounded-xl text-xs font-medium h-9 cursor-pointer border-border hover:bg-muted/50 gap-2 justify-center"
						title="Search a single contact by name or number and add to your list"
					>
						<Icon name="search" class="size-3.5 text-amber-500" />
						<span>Find Single Contact</span>
					</Button>
				</div>
			</div>
		{:else if filteredContacts.length === 0}
			<div class="p-6 text-center text-muted-foreground text-sm flex flex-col items-center justify-center space-y-3">
				<p class="text-xs font-medium text-foreground">
					{#if filterCallDate === 'pending' && allCount > 0}
						All {allCount} contacts allocated today have been called!
					{:else if list.searchQuery.value && list.searchQuery.value.trim()}
						No allocated contacts match "{list.searchQuery.value}".
					{:else}
						No contacts match the active filter.
					{/if}
				</p>
				{#if list.searchQuery.value && list.searchQuery.value.trim()}
					<div class="pt-1 w-full max-w-[260px]">
						<Button
							variant="outline"
							size="sm"
							onclick={() => onOpenSearchSingle ? onOpenSearchSingle(list.searchQuery.value) : null}
							class="w-full rounded-xl text-xs font-semibold px-3 h-8.5 cursor-pointer border-primary/40 bg-primary/5 hover:bg-primary/10 text-primary gap-1.5 shadow-2xs"
						>
							<Icon name="search" class="size-3.5 shrink-0" />
							<span class="truncate">Search CRM for "{list.searchQuery.value}"</span>
						</Button>
					</div>
				{/if}
				{#if allCount > 0 && filterCallDate !== 'all' && (!list.searchQuery.value || !list.searchQuery.value.trim())}
					<div class="w-full max-w-[260px]">
						<Button
							variant="outline"
							size="sm"
							onclick={() => (filterCallDate = 'all')}
							class="w-full rounded-xl text-xs font-medium px-3 h-8 cursor-pointer border-border"
						>
							<Icon name="users" class="size-3.5 mr-1.5 text-primary shrink-0" />
							<span class="truncate">Show All Today ({allCount})</span>
						</Button>
					</div>
				{/if}
			</div>
		{:else}
			{#each filteredContacts as contact (contact.id)}
				{@const isUntouched = isUntouchedContact(contact)}
				<div
					class="w-full text-left p-2.5 sm:p-3 hover:bg-muted/30 active:bg-muted/50 transition-all flex items-start gap-2.5 relative group {selectedContact?.id === contact.id ? 'bg-primary/5 border-l-[3px] border-primary' : ''}"
				>
					<!-- Row Checkbox for Multi-Select Ticking -->
					<div class="shrink-0 pt-1 pl-0.5">
						{#if isUntouched}
							<input
								type="checkbox"
								checked={selectedIds.includes(contact.id)}
								onclick={(e) => e.stopPropagation()}
								onchange={() => toggleSelect(contact.id)}
								class="size-4 rounded border-border text-primary focus:ring-primary/30 cursor-pointer accent-primary"
								aria-label="Select {contact.fullName} for deallocation"
							/>
						{:else}
							<input
								type="checkbox"
								disabled
								class="size-4 rounded border-border/40 bg-muted/40 cursor-not-allowed opacity-25"
								title="Cannot deallocate: contact has already been touched/called"
								aria-label="Contact touched; cannot deallocate"
							/>
						{/if}
					</div>

					<!-- Clickable Contact Row Button (opens contact workspace) -->
					<button
						type="button"
						onclick={() => onSelectContact(contact)}
						class="flex-1 min-w-0 text-left flex items-start gap-2.5 cursor-pointer"
					>
						<!-- Status Icon Emblem based on Past 7 Days History -->
						<div class="shrink-0 pt-0.5">
							<CallStatusBadge
								lastCallDate={contact.lastCallDate}
								outcome={contact.lastCallOutcome}
								variant="avatar"
							/>
						</div>

						<div class="flex-1 min-w-0 space-y-1">
							<div class="flex items-start justify-between gap-1.5">
								<div class="flex items-center gap-1.5 min-w-0">
									<span class="font-semibold text-sm line-clamp-1 text-foreground group-hover:text-primary transition-colors">
										{contact.fullName}
									</span>
									{#if selectedContact?.id === contact.id && isFetchingContactDetails}
										<Loader2 class="size-3 animate-spin text-primary shrink-0" />
									{/if}
								</div>
								{#if contact.respCenter}
									<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded text-muted-foreground font-medium shrink-0">
										{contact.respCenter}
									</span>
								{/if}
							</div>

							<!-- Status Tag Badge -->
							<div class="flex items-center gap-1.5">
								<CallStatusBadge
									lastCallDate={contact.lastCallDate}
									outcome={contact.lastCallOutcome}
									variant="pill"
								/>
							</div>
							
							{#if contact.companyName}
								<span class="text-xs text-muted-foreground line-clamp-1">{contact.companyName}</span>
							{/if}

							<div class="flex items-center justify-between text-xs text-muted-foreground pt-0.5">
								<span class="flex items-center gap-1 font-medium text-foreground/80">
									<Icon name="phone" class="size-3 text-muted-foreground/60" />
									{contact.mobileNo || 'No Mobile'}
								</span>
								{#if contact.city}
									<span class="flex items-center gap-1">
										<Icon name="map-pin" class="size-3 text-muted-foreground/60" />
										{contact.city}
									</span>
								{/if}
							</div>

							<!-- Area and Products Badges -->
							{#if contact.erpCustomerNos || contact.erpAreaCodes || contact.products}
								<div class="flex flex-wrap gap-1 mt-1">
									{#if contact.erpCustomerNos}
										<span class="text-[9px] bg-amber-500/10 text-amber-600 dark:text-amber-400 px-1.5 py-0.5 rounded font-medium">
											No: {contact.erpCustomerNos}
										</span>
									{/if}
									{#if contact.erpAreaCodes}
										<span class="text-[9px] bg-sky-500/10 text-sky-600 dark:text-sky-400 px-1.5 py-0.5 rounded font-medium">
											Area: {contact.erpAreaCodes}
										</span>
									{/if}
									{#if contact.products}
										<span class="text-[9px] bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 px-1.5 py-0.5 rounded font-medium truncate max-w-[180px]">
											{contact.products}
										</span>
									{/if}
								</div>
							{/if}
						</div>
					</button>
				</div>
			{/each}

			{#if list.hasMore}
				<div class="p-3 text-center border-t border-border bg-card">
					<Button 
						variant="outline" 
						size="sm" 
						class="w-full text-xs rounded-lg h-8 border-border hover:bg-muted/50 cursor-pointer" 
						onclick={() => list.onLoadMore()}
						disabled={list.loadingMore}
					>
						{#if list.loadingMore}
							<Loader2 class="size-3 animate-spin mr-1.5 text-muted-foreground" />
							Loading...
						{:else}
							Load More
						{/if}
					</Button>
				</div>
			{/if}
		{/if}
	</div>
</div>
