<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import EmptyState from '$lib/components/venUI/emptyState/EmptyState.svelte';
	import type { CrmContact, CallLog, CallReminder, ContactInvoice, ContactClaim } from '../queries';
	import CallLogger from './CallLogger.svelte';
	import HistoryTimeline from './HistoryTimeline.svelte';
	import BusinessDataViewer from './BusinessDataViewer.svelte';
	import CallStatusBadge from './CallStatusBadge.svelte';

	let {
		selectedContact = $bindable(null),
		isListCollapsed = $bindable(false),
		isDeallocating,
		onDeallocate,
		onCallMobile,
		// State passed down to child tabs
		activeTab = $bindable('log'),
		callLogs,
		reminders,
		invoices,
		claims,
		loadingHistory,
		loadingInvoices,
		loadingClaims,
		printingDocNo = null,
		completingReminderId = null,
		// Action callbacks for children
		onSaveCallLog,
		onUndoCallLog,
		onCompleteReminder,
		onPrintDocument,
		onRefreshHistory,
		onUpdateCallLogNotes,
		salesUsersList = [],
		isSavingLog,
		isUndoingLog
	}: {
		selectedContact: CrmContact | null;
		isListCollapsed: boolean;
		isDeallocating: boolean;
		onDeallocate: (id: string) => void;
		onCallMobile: (mobile: string) => void;
		
		activeTab: 'log' | 'history' | 'reminders' | 'business' | 'claims';
		callLogs: CallLog[];
		reminders: CallReminder[];
		invoices: ContactInvoice[];
		claims: ContactClaim[];
		loadingHistory: boolean;
		loadingInvoices: boolean;
		loadingClaims: boolean;
		printingDocNo?: string | null;
		completingReminderId?: string | null;
		
		onSaveCallLog: (data: any) => Promise<void>;
		onUndoCallLog: (id: string) => void;
		onCompleteReminder: (id: string) => void;
		onPrintDocument: (no: string, type: string) => void;
		onRefreshHistory?: () => void;
		onUpdateCallLogNotes?: (callLogId: string, notes: string | null) => Promise<boolean>;
		salesUsersList?: { userName: string; fullName: string }[];
		isSavingLog: boolean;
		isUndoingLog: string | null;
	} = $props();

	let isUntouched = $derived.by(() => {
		if (!selectedContact) return false;
		if (callLogs && callLogs.length > 0) return false;
		if (selectedContact.lastCallOutcome && selectedContact.lastCallOutcome.trim() !== '') return false;
		if (selectedContact.lastCallDate) return false;
		return true;
	});

</script>

<div class="flex-1 bg-muted/10 p-3 sm:p-4 md:p-5 overflow-y-auto min-h-0 relative {selectedContact ? 'block' : 'hidden md:block'}">
	{#if !selectedContact}
		<EmptyState
			icon="phone"
			title="Workspace Ready"
			description="Select a contact from the left list to dial their number, view calling logs, and record calling responses."
			class="h-full justify-center"
		/>
	{:else}
		<div class="w-full max-w-6xl mx-auto space-y-3">
			<!-- Contact Profile Card (Compact & High-Density) -->
			<div class="bg-card border border-border rounded-xl p-3 sm:p-4 shadow-2xs space-y-2.5">
				<!-- Row 1: Contact Name, Badges, Status & Actions -->
				<div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-2.5">
					<div class="flex items-center gap-2 flex-wrap min-w-0">
						<!-- Desktop List Toggle Button -->
						{#if isListCollapsed}
							<Button
								variant="outline"
								size="icon"
								class="hidden md:inline-flex size-7.5 rounded-lg shadow-2xs bg-muted/30 hover:bg-muted shrink-0"
								onclick={() => (isListCollapsed = false)}
								title="Show contacts list"
							>
								<Icon name="panel-left-open" class="size-3.5 text-muted-foreground" />
							</Button>
						{/if}

						<!-- Mobile Back Button -->
						<div class="md:hidden">
							<Button
								variant="ghost"
								size="sm"
								onclick={() => {
									selectedContact = null;
									isListCollapsed = false;
								}}
								class="h-7 px-1.5 text-muted-foreground hover:text-foreground"
							>
								<Icon name="arrow-left" class="size-4" />
							</Button>
						</div>

						<h2 class="text-base sm:text-lg font-bold text-foreground truncate flex items-center gap-1.5">
							<a
								href="/crm-contacts/{selectedContact.id}?from={encodeURIComponent('/crm-calling?contactId=' + selectedContact.id)}"
								class="hover:text-primary hover:underline transition-colors flex items-center gap-1.5 truncate group"
								title="Open contact master"
							>
								<span class="truncate">{selectedContact.fullName}</span>
								<Icon name="external-link" class="size-3.5 text-muted-foreground opacity-50 group-hover:opacity-100 group-hover:text-primary transition-opacity shrink-0" />
							</a>
							{#if loadingHistory || loadingInvoices || loadingClaims}
								<span title="Loading contact data..."><Loader2 class="size-3.5 animate-spin text-primary/70 shrink-0" /></span>
							{/if}
						</h2>

						{#if selectedContact.state}
							<span class="text-[11px] font-semibold bg-muted text-muted-foreground px-1.5 py-0.5 rounded">
								{selectedContact.state}
							</span>
						{/if}

						<!-- Sleek inline status pill (no giant banner) -->
						<CallStatusBadge
							lastCallDate={selectedContact.lastCallDate}
							outcome={selectedContact.lastCallOutcome}
							variant="pill"
						/>

						{#if selectedContact.companyName}
							<span class="text-xs text-muted-foreground hidden sm:inline truncate max-w-[200px]">
								({selectedContact.companyName})
							</span>
						{/if}
					</div>

					<!-- Actions: Call & Deallocate -->
					<div class="flex items-center gap-2 w-full sm:w-auto shrink-0 justify-end">
						{#if isUntouched}
							<Button
								variant="outline"
								size="sm"
								onclick={() => onDeallocate(selectedContact!.id)}
								disabled={isDeallocating}
								class="h-8 px-2.5 gap-1.5 text-xs text-rose-600 border-rose-200 hover:bg-rose-50 hover:text-rose-700 dark:border-rose-900/40 dark:hover:bg-rose-950/20 rounded-lg font-medium"
								title="Deallocate this untouched contact"
							>
								{#if isDeallocating}
									<Loader2 class="size-3.5 animate-spin" />
									<span>Deallocating...</span>
								{:else}
									<Icon name="user-minus" class="size-3.5" />
									<span>Deallocate</span>
								{/if}
							</Button>
						{/if}

						{#if selectedContact.mobileNo}
							<Button
								size="sm"
								onclick={() => onCallMobile(selectedContact!.mobileNo!)}
								class="h-8 px-3.5 gap-1.5 text-xs bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg shadow-xs font-semibold"
							>
								<Icon name="phone" class="size-3.5" />
								<span>Call {selectedContact.mobileNo}</span>
							</Button>
						{/if}
					</div>
				</div>

				<!-- Row 2: Metadata & Purchased Products Chips -->
				<div class="flex flex-wrap items-center gap-x-3 gap-y-1.5 text-xs text-muted-foreground pt-1 border-t border-border/50">
					{#if selectedContact.city}
						<span class="flex items-center gap-1">
							<Icon name="map-pin" class="size-3 text-muted-foreground/70" />
							{selectedContact.city}
						</span>
					{/if}
					{#if selectedContact.respCenter}
						<span class="flex items-center gap-1">
							<Icon name="building" class="size-3 text-muted-foreground/70" />
							RC: <strong class="font-medium text-foreground/80">{selectedContact.respCenter}</strong>
						</span>
					{/if}
					{#if selectedContact.erpCustomerNos}
						<span class="flex items-center gap-1">
							<Icon name="hash" class="size-3 text-muted-foreground/70" />
							Cust No: <strong class="font-medium text-foreground/80">{selectedContact.erpCustomerNos}</strong>
						</span>
					{/if}
					{#if selectedContact.erpAreaCodes}
						<span class="flex items-center gap-1">
							<Icon name="map" class="size-3 text-muted-foreground/70" />
							Area: <strong class="font-medium text-foreground/80">{selectedContact.erpAreaCodes}</strong>
						</span>
					{/if}
					{#if selectedContact.products}
						<span class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 text-[11px] font-medium border border-indigo-500/20">
							<Icon name="package" class="size-3 text-indigo-500" />
							<span><strong class="font-semibold">Products:</strong> {selectedContact.products}</span>
						</span>
					{/if}
				</div>
			</div>

			<!-- Workspace Tabs & Content Card -->
			<div class="bg-card border border-border rounded-xl overflow-hidden shadow-2xs flex-1 flex flex-col min-h-0">
				<!-- Tab Bar -->
				<div class="flex border-b border-border bg-muted/20 overflow-x-auto scrollbar-hide text-xs sm:text-sm">
					<button
						onclick={() => (activeTab = 'log')}
						class="flex-1 shrink-0 py-2.5 px-3 font-semibold border-b-2 transition-colors flex items-center justify-center gap-1.5 {activeTab === 'log' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="activity" class="size-3.5" />
						Log Response
					</button>
					<button
						onclick={() => {
							activeTab = 'history';
							if (selectedContact) onRefreshHistory?.();
						}}
						class="flex-1 shrink-0 py-2.5 px-3 font-semibold border-b-2 transition-colors flex items-center justify-center gap-1.5 {activeTab === 'history' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="history" class="size-3.5" />
						Call History
						{#if loadingHistory}
							<Loader2 class="size-3 animate-spin text-primary shrink-0" />
						{:else if callLogs.length > 0}
							<span class="text-[10px] bg-muted px-1.5 py-0.2 rounded-full font-bold">{callLogs.length}</span>
						{/if}
					</button>
					<button
						onclick={() => {
							activeTab = 'reminders';
							if (selectedContact) onRefreshHistory?.();
						}}
						class="flex-1 shrink-0 py-2.5 px-3 font-semibold border-b-2 transition-colors flex items-center justify-center gap-1.5 {activeTab === 'reminders' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="calendar" class="size-3.5" />
						Reminders
						{#if loadingHistory}
							<Loader2 class="size-3 animate-spin text-rose-500 shrink-0" />
						{:else if reminders.filter(r => !r.isCompleted).length > 0}
							<span class="text-[10px] bg-rose-500/10 text-rose-600 dark:text-rose-400 px-1.5 py-0.2 rounded-full font-bold">
								{reminders.filter(r => !r.isCompleted).length}
							</span>
						{/if}
					</button>
					<button
						onclick={() => (activeTab = 'business')}
						class="flex-1 shrink-0 py-2.5 px-3 font-semibold border-b-2 transition-colors flex items-center justify-center gap-1.5 {activeTab === 'business' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="receipt" class="size-3.5" />
						Sales History
						{#if loadingInvoices}
							<Loader2 class="size-3 animate-spin text-primary shrink-0" />
						{:else if invoices.length > 0}
							<span class="text-[10px] bg-muted px-1.5 py-0.2 rounded-full font-bold">{invoices.length}</span>
						{/if}
					</button>
					<button
						onclick={() => (activeTab = 'claims')}
						class="flex-1 shrink-0 py-2.5 px-3 font-semibold border-b-2 transition-colors flex items-center justify-center gap-1.5 {activeTab === 'claims' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="file-search" class="size-3.5" />
						Claim History
						{#if loadingClaims}
							<Loader2 class="size-3 animate-spin text-primary shrink-0" />
						{:else if claims.length > 0}
							<span class="text-[10px] bg-muted px-1.5 py-0.2 rounded-full font-bold">{claims.length}</span>
						{/if}
					</button>
				</div>

				<!-- Tab Content -->
				<div class="p-3.5 sm:p-4">
					{#if activeTab === 'log'}
						<CallLogger {selectedContact} {onSaveCallLog} {isSavingLog} />
					{:else if activeTab === 'history' || activeTab === 'reminders'}
						<HistoryTimeline 
							type={activeTab} 
							{callLogs} 
							{reminders} 
							{loadingHistory} 
							{onUndoCallLog}
							{isUndoingLog}
							{onCompleteReminder}
							{completingReminderId}
							{onRefreshHistory}
							{onUpdateCallLogNotes}
							{salesUsersList}
						/>
					{:else if activeTab === 'business' || activeTab === 'claims'}
						<BusinessDataViewer
							type={activeTab}
							{invoices}
							{claims}
							loading={activeTab === 'business' ? loadingInvoices : loadingClaims}
							{onPrintDocument}
							{printingDocNo}
						/>
					{/if}
				</div>
			</div>
		</div>
	{/if}
</div>
