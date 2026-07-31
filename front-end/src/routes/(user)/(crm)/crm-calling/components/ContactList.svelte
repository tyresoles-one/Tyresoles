<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import type { CrmContact } from '../queries';

	let {
		list,
		filteredContacts,
		selectedContact,
		isAllocating,
		filterCallDate = $bindable(),
		onSelectContact,
		onLoadSummary,
		onRequestMoreContacts
	}: {
		list: any;
		filteredContacts: CrmContact[];
		selectedContact: CrmContact | null;
		isAllocating: boolean;
		filterCallDate: string;
		onSelectContact: (contact: CrmContact) => void;
		onLoadSummary: () => void;
		onRequestMoreContacts: () => void;
	} = $props();
</script>

<div class="w-full md:w-[380px] border-r border-border bg-card flex flex-col h-screen sticky top-0 shrink-0 {selectedContact ? 'hidden md:flex' : 'flex'}">
	<div class="p-4 border-b border-border space-y-3">
		<div class="flex items-center justify-between gap-2">
			<h1 class="text-xl font-bold tracking-tight">Call Center</h1>
			<div class="flex items-center gap-1.5">
				<Button
					variant="outline"
					size="sm"
					onclick={onLoadSummary}
					class="h-7 px-2 rounded-lg text-[10px] font-semibold flex items-center gap-1 bg-muted/20 border-border hover:bg-indigo-50 dark:hover:bg-indigo-900/20 text-indigo-600 dark:text-indigo-400"
				>
					<Icon name="bar-chart-2" class="size-3" />
					<span>Summary</span>
				</Button>
				<Button
					variant="outline"
					size="sm"
					onclick={onRequestMoreContacts}
					disabled={isAllocating}
					class="h-7 px-2 rounded-lg text-[10px] font-semibold flex items-center gap-1 bg-muted/20 border-border hover:bg-muted/50"
				>
					{#if isAllocating}
						<Loader2 class="size-3 animate-spin text-muted-foreground" />
						<span>Getting...</span>
					{:else}
						<Icon name="plus" class="size-3" />
						<span>Get Contacts</span>
					{/if}
				</Button>
				<span class="text-[10px] bg-primary/10 text-primary px-2 py-0.5 rounded-full font-semibold shrink-0">
					{filteredContacts.length} Contacts
				</span>
			</div>
		</div>
		
		<div class="relative">
			<Icon name="search" class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
			<Input
				placeholder="Search contacts..."
				bind:value={list.searchQuery.value}
				class="pl-9 rounded-xl h-9 bg-muted/30 focus-visible:ring-1 focus-visible:ring-ring border-none shadow-none"
			/>
		</div>

		<!-- Filters Group -->
		<div class="pt-1 pb-1">
			<select
				bind:value={filterCallDate}
				class="w-full h-8 text-xs bg-muted/20 border border-border/50 rounded-xl px-2 focus:ring-1 focus:ring-ring outline-none text-foreground"
			>
				<option value="pending">Show Pending (Not Called Today)</option>
				<option value="all">Show All Allocated</option>
			</select>
		</div>
	</div>

	<!-- List Container -->
	<div class="flex-1 overflow-y-auto divide-y divide-border">
		{#if list.loading && list.items.length === 0}
			<div class="flex items-center justify-center h-48">
				<Loader2 class="size-6 animate-spin text-primary" />
			</div>
		{:else if filteredContacts.length === 0}
			<div class="p-8 text-center text-muted-foreground text-sm">
				No contacts match the filters.
			</div>
		{:else}
			{#each filteredContacts as contact (contact.id)}
				<button
					onclick={() => onSelectContact(contact)}
					class="w-full text-left p-4 hover:bg-muted/30 active:bg-muted/50 transition-colors flex flex-col gap-1.5 {selectedContact?.id === contact.id ? 'bg-primary/5 border-l-2 border-primary' : ''}"
				>
					<div class="flex items-start justify-between gap-2">
						<span class="font-semibold text-sm line-clamp-1">{contact.fullName}</span>
						{#if contact.respCenter}
							<span class="text-[10px] bg-muted px-1.5 py-0.5 rounded text-muted-foreground font-medium shrink-0">
								{contact.respCenter}
							</span>
						{/if}
					</div>
					
					{#if contact.companyName}
						<span class="text-xs text-muted-foreground line-clamp-1">{contact.companyName}</span>
					{/if}

					<div class="flex items-center justify-between text-xs text-muted-foreground pt-1">
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
				</button>
			{/each}

			{#if list.hasMore}
				<div class="p-3 text-center border-t border-border bg-card">
					<Button 
						variant="outline" 
						size="sm" 
						class="w-full text-xs rounded-lg h-8 border-border hover:bg-muted/50" 
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
