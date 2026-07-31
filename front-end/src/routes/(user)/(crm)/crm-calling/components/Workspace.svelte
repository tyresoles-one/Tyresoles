<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import EmptyState from '$lib/components/venUI/emptyState/EmptyState.svelte';
	import type { CrmContact, CallLog, CallReminder, ContactInvoice, ContactClaim } from '../queries';
	import CallLogger from './CallLogger.svelte';
	import HistoryTimeline from './HistoryTimeline.svelte';
	import BusinessDataViewer from './BusinessDataViewer.svelte';

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
		// Action callbacks for children
		onSaveCallLog,
		onUndoCallLog,
		onCompleteReminder,
		onPrintDocument,
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
		
		onSaveCallLog: (data: any) => Promise<void>;
		onUndoCallLog: (id: string) => void;
		onCompleteReminder: (id: string) => void;
		onPrintDocument: (no: string, type: string) => void;
		isSavingLog: boolean;
		isUndoingLog: string | null;
	} = $props();

</script>

<div class="flex-1 bg-muted/10 p-4 md:p-6 overflow-y-auto h-screen relative {selectedContact ? 'block' : 'hidden md:block'}">
	<!-- Desktop Collapse Toggle -->
	<div class="hidden md:block absolute top-4 left-4 z-10">
		<Button
			variant="outline"
			size="icon"
			class="size-8 rounded-full shadow-sm bg-background hover:bg-muted"
			onclick={() => isListCollapsed = !isListCollapsed}
			title={isListCollapsed ? "Expand list" : "Collapse list"}
		>
			<Icon name={isListCollapsed ? 'panel-left-open' : 'panel-left-close'} class="size-4 text-muted-foreground" />
		</Button>
	</div>

	{#if !selectedContact}
		<EmptyState
			icon="phone"
			title="Workspace Ready"
			description="Select a contact from the left list to dial their number, view calling logs, and record calling responses."
			class="h-full justify-center"
		/>
	{:else}
		<div class="max-w-4xl mx-auto space-y-6">
			<!-- Back to list on mobile -->
			<div class="md:hidden">
				<Button
					variant="ghost"
					size="sm"
					onclick={() => {
						selectedContact = null;
						isListCollapsed = false;
					}}
					class="gap-1.5 px-0 text-muted-foreground hover:text-foreground hover:bg-transparent"
				>
					<Icon name="arrow-left" class="size-4" />
					Back to Contacts
				</Button>
			</div>
			
			<!-- Contact Profile Card -->
			<div class="bg-card border border-border rounded-2xl p-6 shadow-xs flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
				<div class="space-y-1.5">
					<div class="flex items-center gap-2">
						<h2 class="text-xl font-bold">{selectedContact.fullName}</h2>
						{#if selectedContact.state}
							<span class="text-xs bg-secondary/80 text-secondary-foreground px-2 py-0.5 rounded-md font-medium">
								{selectedContact.state}
							</span>
						{/if}
					</div>
					{#if selectedContact.companyName}
						<p class="text-sm text-muted-foreground">{selectedContact.companyName}</p>
					{/if}
					<div class="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-xs text-muted-foreground pt-1">
						{#if selectedContact.city}
							<span class="flex items-center gap-1.5">
								<Icon name="map-pin" class="size-3.5" />
								{selectedContact.city}
							</span>
						{/if}
						{#if selectedContact.respCenter}
							<span class="flex items-center gap-1.5">
								<Icon name="building" class="size-3.5" />
								Resp Center: {selectedContact.respCenter}
							</span>
						{/if}
						{#if selectedContact.erpCustomerNos}
							<span class="flex items-center gap-1.5">
								<Icon name="hash" class="size-3.5" />
								Cust No: {selectedContact.erpCustomerNos}
							</span>
						{/if}
						{#if selectedContact.erpAreaCodes}
							<span class="flex items-center gap-1.5">
								<Icon name="map" class="size-3.5" />
								Area: {selectedContact.erpAreaCodes}
							</span>
						{/if}
					</div>
				</div>

				<div class="flex items-center gap-2 w-full sm:w-auto shrink-0 flex-wrap">
					<Button
						variant="outline"
						onclick={() => onDeallocate(selectedContact!.id)}
						disabled={isDeallocating}
						class="w-full sm:w-auto gap-2 text-rose-600 border-rose-200 hover:bg-rose-50 hover:text-rose-700 hover:border-rose-300 dark:text-rose-400 dark:border-rose-900/30 dark:hover:bg-rose-950/20 dark:hover:border-rose-800 rounded-xl transition-all font-semibold"
					>
						{#if isDeallocating}
							<Loader2 class="size-4 animate-spin" />
							<span>Deallocating...</span>
						{:else}
							<Icon name="user-minus" class="size-4" />
							<span>Deallocate</span>
						{/if}
					</Button>

					{#if selectedContact.mobileNo}
						<Button
							onclick={() => onCallMobile(selectedContact!.mobileNo!)}
							class="w-full sm:w-auto gap-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl shadow-lg hover:shadow-emerald-500/10 transition-all font-semibold"
						>
							<Icon name="phone" class="size-4" />
							<span>Call {selectedContact.mobileNo}</span>
						</Button>
					{/if}
				</div>
			</div>

			<!-- Products Display Card -->
			{#if selectedContact.products}
				<div class="bg-indigo-500/5 border border-indigo-500/10 rounded-2xl p-4 flex items-start gap-3">
					<div class="p-2 rounded-xl bg-indigo-500/10 text-indigo-500 mt-0.5">
						<Icon name="package" class="size-5" />
					</div>
					<div class="space-y-1">
						<h4 class="text-xs font-bold text-indigo-600 dark:text-indigo-400 uppercase tracking-wider">Purchased Products</h4>
						<p class="text-sm font-medium text-foreground/80">{selectedContact.products}</p>
					</div>
				</div>
			{/if}

			<!-- Workspace Panels -->
			<div class="bg-card border border-border rounded-2xl overflow-hidden shadow-xs">
				<!-- Tab Bar -->
				<div class="flex border-b border-border bg-muted/20 overflow-x-auto scrollbar-hide">
					<button
						onclick={() => (activeTab = 'log')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'log' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="activity" class="size-4" />
						Log Response
					</button>
					<button
						onclick={() => (activeTab = 'history')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'history' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="history" class="size-4" />
						Call History
						{#if callLogs.length > 0}
							<span class="text-xs bg-muted px-1.5 py-0.2 rounded-full font-bold">{callLogs.length}</span>
						{/if}
					</button>
					<button
						onclick={() => (activeTab = 'reminders')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'reminders' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="calendar" class="size-4" />
						Reminders
						{#if reminders.filter(r => !r.isCompleted).length > 0}
							<span class="text-xs bg-rose-500/10 text-rose-600 dark:text-rose-400 px-1.5 py-0.2 rounded-full font-bold">
								{reminders.filter(r => !r.isCompleted).length}
							</span>
						{/if}
					</button>
					<button
						onclick={() => (activeTab = 'business')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'business' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="receipt" class="size-4" />
						Sales History
						{#if invoices.length > 0}
							<span class="text-xs bg-muted px-1.5 py-0.2 rounded-full font-bold">{invoices.length}</span>
						{/if}
					</button>
					<button
						onclick={() => (activeTab = 'claims')}
						class="flex-1 shrink-0 min-w-[130px] py-3.5 px-4 font-semibold text-sm border-b-2 transition-colors flex items-center justify-center gap-2 {activeTab === 'claims' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="file-search" class="size-4" />
						Claim History
						{#if claims.length > 0}
							<span class="text-xs bg-muted px-1.5 py-0.2 rounded-full font-bold">{claims.length}</span>
						{/if}
					</button>
				</div>

				<!-- Tab Content -->
				<div class="p-6">
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
						/>
					{:else if activeTab === 'business' || activeTab === 'claims'}
						<BusinessDataViewer
							type={activeTab}
							{invoices}
							{claims}
							loading={activeTab === 'business' ? loadingInvoices : loadingClaims}
							{onPrintDocument}
						/>
					{/if}
				</div>
			</div>
		</div>
	{/if}
</div>
