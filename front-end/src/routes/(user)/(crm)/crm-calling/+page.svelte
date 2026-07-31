<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { authStore } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import * as Dialog from '$lib/components/ui/dialog';
	import PdfViewer from '$lib/components/venUI/pdf-viewer/PdfViewer.svelte';
	import AllocateContactsDialog from './AllocateContactsDialog.svelte';
	import ContactList from './components/ContactList.svelte';
	import Workspace from './components/Workspace.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	import {
		GetCrmAgentContactsDocument,
		GetCrmSettingDocument,
		GetCrmCallLogsDocument,
		GetCrmCallRemindersDocument,
		GetCrmContactInvoicesDocument,
		GetCrmContactClaimsDocument,
		LogCrmCallDocument,
		UndoCrmCallDocument,
		GetCrmMyCallingSummaryDocument,
		CompleteCrmReminderDocument,
		AllocateAgentContactsDocument,
		DeallocateCrmContactDocument,
		PrintDocumentsMutation,
		type CrmContact,
		type CallLog,
		type CallReminder,
		type ContactInvoice,
		type ContactClaim,
		type CrmAgentContact
	} from './queries';

	// State
	let selectedContact = $state<CrmContact | null>(null);
	let activeTab = $state<'log' | 'history' | 'reminders' | 'business' | 'claims'>('log');
	
	let callLogs = $state<CallLog[]>([]);
	let reminders = $state<CallReminder[]>([]);
	let invoices = $state<ContactInvoice[]>([]);
	let claims = $state<ContactClaim[]>([]);
	
	let loadingHistory = $state(false);
	let loadingInvoices = $state(false);
	let loadingClaims = $state(false);

	let isSavingLog = $state(false);
	let isUndoingLog = $state<string | null>(null);
	
	let pdfData = $state<Uint8Array | null>(null);
	let pdfFileName = $state<string>('');
	let showPdfViewer = $state(false);
	let loadingPdf = $state(false);

	let showSummaryModal = $state(false);
	let callingSummary = $state<{ outcome: string; count: number }[]>([]);
	let loadingSummary = $state(false);

	let allocateDialogOpen = $state(false);
	let isAllocating = $state(false);
	let isDeallocating = $state(false);

	// Custom List State
	let allocatedAgentContacts = $state<CrmAgentContact[]>([]);
	let isListLoading = $state(false);
	let searchQuery = $state('');
	let filterCallDate = $state('pending');
	let pageSize = $state(50);
	let isListCollapsed = $state(false);

	// Mocking list object structure so ContactList.svelte doesn't break.
	// In ContactList.svelte we access list.searchQuery.value, list.loading, list.items, list.hasMore, list.onLoadMore
	let listMock = $derived({
		loading: isListLoading,
		items: allocatedAgentContacts,
		hasMore: false, // We append new allocations on demand, so standard pagination is disabled
		loadingMore: isAllocating,
		searchQuery: {
			get value() { return searchQuery; },
			set value(v: string) { searchQuery = v; }
		},
		onLoadMore: () => {
			allocateDialogOpen = true;
		},
		onRefresh: async () => {
			await loadAllocatedContacts();
		}
	});

	let filteredContacts = $derived.by(() => {
		let items = allocatedAgentContacts.map(ac => ac.contact).filter(c => !!c);
		
		// Apply search filter manually
		if (searchQuery) {
			const q = searchQuery.toLowerCase();
			items = items.filter(c => 
				c.fullName?.toLowerCase().includes(q) ||
				c.mobileNo?.toLowerCase().includes(q) ||
				c.companyName?.toLowerCase().includes(q) ||
				c.erpCustomerNos?.toLowerCase().includes(q)
			);
		}

		if (filterCallDate === 'pending') {
			const todayStr = new Date().toDateString();
			return items.filter(c => {
				if (!c.lastCallDate) return true;
				return new Date(c.lastCallDate).toDateString() !== todayStr;
			});
		}
		return items;
	});

	$effect(() => {
		const id = selectedContact?.id;
		untrack(() => {
			if (id) {
				activeTab = 'log';
				loadHistory(id);
				loadInvoices(id);
				loadClaims(id);
			} else {
				callLogs = [];
				reminders = [];
				invoices = [];
				claims = [];
			}
		});
	});

	async function loadAllocatedContacts() {
		isListLoading = true;
		try {
			const res = await graphqlQuery<any>(GetCrmAgentContactsDocument, {
				variables: {
					take: pageSize, // load the exact limit
					where: { agentUsername: { eq: $authStore.username }, deallocatedAt: { eq: null } },
					order: [{ contact: { lastCallDate: 'ASC' } }]
				}
			});
			if (res.success && res.data?.crmAgentContacts?.items) {
				allocatedAgentContacts = res.data.crmAgentContacts.items;
			}
		} catch (err) {
			console.error('Failed to load initial contacts', err);
		} finally {
			isListLoading = false;
		}
	}

	async function loadHistory(contactId: string) {
		loadingHistory = true;
		try {
			const [logsRes, remRes] = await Promise.all([
				graphqlQuery<{ crmCallLogs: CallLog[] }>(GetCrmCallLogsDocument, { variables: { contactId } }),
				graphqlQuery<{ crmCallReminders: CallReminder[] }>(GetCrmCallRemindersDocument, { variables: { contactId, includeCompleted: false } })
			]);
			if (logsRes.success && logsRes.data) callLogs = logsRes.data.crmCallLogs;
			if (remRes.success && remRes.data) reminders = remRes.data.crmCallReminders;
		} catch (err) {
			console.error('Failed to load history', err);
		} finally {
			loadingHistory = false;
		}
	}

	async function loadInvoices(contactId: string) {
		loadingInvoices = true;
		try {
			const res = await graphqlQuery<{ invoices: ContactInvoice[] }>(GetCrmContactInvoicesDocument, { variables: { contactId } });
			if (res.success && res.data) invoices = res.data.invoices;
		} catch (err) {
			console.error(err);
		} finally {
			loadingInvoices = false;
		}
	}

	async function loadClaims(contactId: string) {
		loadingClaims = true;
		try {
			const res = await graphqlQuery<{ claims: ContactClaim[] }>(GetCrmContactClaimsDocument, { variables: { contactId } });
			if (res.success && res.data) claims = res.data.claims;
		} catch (err) {
			console.error(err);
		} finally {
			loadingClaims = false;
		}
	}

	async function loadCallingSummary() {
		showSummaryModal = true;
		loadingSummary = true;
		try {
			const res = await graphqlQuery<{ summary: { outcome: string; count: number }[] }>(GetCrmMyCallingSummaryDocument, {});
			if (res.success && res.data) callingSummary = res.data.summary;
		} catch (err) {
			console.error(err);
		} finally {
			loadingSummary = false;
		}
	}

	async function handleSaveCallLog(data: any) {
		if (!selectedContact) return;
		isSavingLog = true;
		try {
			const input = {
				contactId: selectedContact.id,
				outcome: data.outcome,
				notes: data.notes || null,
				followUpDate: data.scheduleFollowUp ? data.followUpDate : null,
				followUpNotes: data.scheduleFollowUp ? data.followUpNotes : null,
				contactIsActive: data.isPositive === false ? false : null
			};
			const res = await graphqlMutation<{ logCrmCall: { success: boolean; message: string } }>(LogCrmCallDocument, { variables: input });
			if (res.success && res.data?.logCrmCall.success) {
				toast.success('Call log saved successfully.');
				await loadHistory(selectedContact.id);
				activeTab = 'history';
			} else {
				toast.error(res.error || 'Failed to save call log.');
			}
		} catch (err) {
			console.error(err);
			toast.error('An error occurred while saving the call log.');
		} finally {
			isSavingLog = false;
		}
	}

	async function handleUndoCallLog(callLogId: string) {
		if (!confirm('Are you sure you want to undo this call log?')) return;
		isUndoingLog = callLogId;
		try {
			const res = await graphqlMutation<{ undoCrmCall: { success: boolean; message: string } }>(UndoCrmCallDocument, { variables: { callLogId } });
			if (res.success && res.data?.undoCrmCall.success) {
				toast.success('Call log undone successfully.');
				if (selectedContact) await loadHistory(selectedContact.id);
			} else {
				toast.error(res.error || 'Failed to undo call log.');
			}
		} catch (err) {
			console.error(err);
			toast.error('An error occurred.');
		} finally {
			isUndoingLog = null;
		}
	}

	async function handleCompleteReminder(reminderId: string) {
		try {
			const res = await graphqlMutation<{ completeCrmReminder: { success: boolean; message: string } }>(CompleteCrmReminderDocument, { variables: { reminderId } });
			if (res.success && res.data?.completeCrmReminder.success) {
				toast.success('Reminder marked as completed.');
				if (selectedContact) await loadHistory(selectedContact.id);
			} else {
				toast.error(res.error || 'Failed to complete reminder.');
			}
		} catch (err) {
			console.error(err);
			toast.error('An error occurred.');
		}
	}

	async function handleDeallocateContact(contactId: string) {
		if (!confirm('Are you sure you want to deallocate this contact?')) return;
		isDeallocating = true;
		try {
			const res = await graphqlMutation<{ deallocateCrmContact: { success: boolean; message: string } }>(DeallocateCrmContactDocument, { variables: { contactId } });
			if (res.success && res.data?.deallocateCrmContact.success) {
				toast.success('Contact deallocated successfully.');
				selectedContact = null;
				allocatedAgentContacts = allocatedAgentContacts.filter(c => c.contactId !== contactId);
			} else {
				toast.error(res.error || 'Failed to deallocate contact.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			isDeallocating = false;
		}
	}

	async function handleAllocateContacts(filters: any) {
		isAllocating = true;
		try {
			const res = await graphqlMutation<{ allocateAgentContacts: { success: boolean; message: string; allocatedContacts: CrmAgentContact[] } }>(
				AllocateAgentContactsDocument, 
				{ variables: { input: filters } }
			);
			if (res.success && res.data?.allocateAgentContacts.success) {
				const newContacts = res.data.allocateAgentContacts.allocatedContacts || [];
				toast.success(res.data.allocateAgentContacts.message || `Successfully allocated ${newContacts.length} new contacts.`);
				
				// Append new contacts to the active list
				allocatedAgentContacts = [...allocatedAgentContacts, ...newContacts];
			} else {
				toast.error(res.error || res.data?.allocateAgentContacts.message || 'Failed to allocate contacts.');
			}
		} catch (err) {
			console.error(err);
			toast.error('An error occurred during allocation.');
		} finally {
			isAllocating = false;
		}
	}

	async function handlePrintDocument(docNo: string, view: string) {
		if (loadingPdf) return;
		loadingPdf = true;
		try {
			const res = await graphqlMutation<{ printDocuments: string }>(PrintDocumentsMutation, {
				variables: { input: { view, nos: [docNo], reportOutput: 'PDF' } }
			});
			if (res.success && res.data?.printDocuments) {
				const base64 = res.data.printDocuments;
				const binaryString = window.atob(base64);
				const bytes = new Uint8Array(binaryString.length);
				for (let i = 0; i < binaryString.length; i++) bytes[i] = binaryString.charCodeAt(i);
				pdfData = bytes;
				pdfFileName = `${view}_${docNo}.pdf`;
				showPdfViewer = true;
			} else {
				toast.error(res.error || `Failed to generate ${view} PDF.`);
			}
		} catch (err: any) {
			toast.error(err.message || `Error printing ${view}.`);
		} finally {
			loadingPdf = false;
		}
	}

	onMount(async () => {
		try {
			const settingRes = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, { variables: { key: 'ContactsPerAgent' } });
			if (settingRes.success && settingRes.data?.getCrmSetting) {
				const limit = parseInt(settingRes.data.getCrmSetting.value, 10);
				if (!isNaN(limit) && limit > 0) pageSize = limit;
			}
			await loadAllocatedContacts();
			
			// Background prefetch Whatsapp data so it loads instantly when requested
			import('./queries').then((q) => {
				graphqlQuery(q.GetCrmWhatsappImagesDocument, {
					cacheKey: 'crm-whatsapp-images',
					cacheTTL: 24 * 60 * 60 * 1000 // 24 hours
				});
				graphqlQuery(q.GetCrmWhatsappTemplatesDocument, {
					cacheKey: 'crm-whatsapp-templates',
					cacheTTL: 24 * 60 * 60 * 1000 // 24 hours
				});
			});
		} catch (err) {
			console.error('Error during initial mount', err);
		}
	});

	function handleCallMobile(mobile: string) {
		window.open(`tel:${mobile}`);
	}
</script>

<svelte:head>
	<title>CRM Call Center | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background text-foreground flex flex-col md:flex-row select-none">
	<div class={isListCollapsed ? 'hidden' : 'block'}>
		<ContactList
			list={listMock}
			{filteredContacts}
			bind:selectedContact
			bind:filterCallDate
			{isAllocating}
			onSelectContact={(c) => {
				selectedContact = c;
				isListCollapsed = true;
			}}
			onLoadSummary={loadCallingSummary}
			onRequestMoreContacts={() => (allocateDialogOpen = true)}
		/>
	</div>

	<Workspace
		bind:selectedContact
		bind:isListCollapsed
		bind:activeTab
		{isDeallocating}
		onDeallocate={handleDeallocateContact}
		onCallMobile={handleCallMobile}
		{callLogs}
		{reminders}
		{invoices}
		{claims}
		{loadingHistory}
		{loadingInvoices}
		{loadingClaims}
		onSaveCallLog={handleSaveCallLog}
		onUndoCallLog={handleUndoCallLog}
		onCompleteReminder={handleCompleteReminder}
		onPrintDocument={handlePrintDocument}
		{isSavingLog}
		{isUndoingLog}
	/>
</div>

<!-- PDF Viewer Modal -->
<Dialog.Root bind:open={showPdfViewer}>
	<Dialog.Content class="w-[96vw] max-w-[min(96vw,2400px)]! h-[95vh]! p-0 overflow-hidden flex flex-col rounded-lg">
		<Dialog.Header class="px-6 py-4 border-b flex-shrink-0">
			<Dialog.Title class="flex items-center gap-2">
				<Icon name="file-text" class="text-primary" />
				<span>Document Preview</span>
			</Dialog.Title>
		</Dialog.Header>

		<div class="flex-1 min-h-0 bg-muted/20">
			{#if pdfData}
				<PdfViewer 
					data={pdfData} 
					fileName={pdfFileName} 
					class="h-full w-full" 
				/>
			{/if}
		</div>

		<Dialog.Footer class="px-6 py-3 border-t flex-shrink-0 bg-muted/5">
			<Button variant="outline" onclick={() => showPdfViewer = false}>Close Preview</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Summary Modal -->
<Dialog.Root bind:open={showSummaryModal}>
	<Dialog.Content class="sm:max-w-[425px]">
		<Dialog.Header>
			<Dialog.Title class="flex items-center gap-2">
				<Icon name="bar-chart-2" class="text-indigo-500" />
				My Calling Summary
			</Dialog.Title>
			<Dialog.Description>
				Summary of your call logs for today.
			</Dialog.Description>
		</Dialog.Header>

		<div class="py-4">
			{#if loadingSummary}
				<div class="flex justify-center py-6">
					<Loader2 class="size-6 animate-spin text-primary" />
				</div>
			{:else if callingSummary.length === 0}
				<div class="text-center text-muted-foreground text-sm py-6">
					No calls logged today.
				</div>
			{:else}
				<div class="space-y-3">
					{#each callingSummary as item}
						<div class="flex items-center justify-between p-3 rounded-lg border border-border bg-muted/20">
							<span class="font-semibold text-sm">{item.outcome}</span>
							<span class="bg-indigo-100 text-indigo-700 dark:bg-indigo-900/30 dark:text-indigo-300 font-bold px-2.5 py-0.5 rounded-full text-xs">
								{item.count}
							</span>
						</div>
					{/each}
					<div class="flex items-center justify-between p-3 rounded-lg bg-primary/10 mt-2">
						<span class="font-bold text-sm text-primary">Total Calls</span>
						<span class="font-bold text-primary text-sm">
							{callingSummary.reduce((sum, item) => sum + item.count, 0)}
						</span>
					</div>
				</div>
			{/if}
		</div>

		<Dialog.Footer>
			<Button variant="outline" onclick={() => showSummaryModal = false}>Close</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<AllocateContactsDialog bind:open={allocateDialogOpen} onAllocate={handleAllocateContacts} />

<style>
	:global(.scrollbar-hide) {
		-ms-overflow-style: none;
		scrollbar-width: none;
	}
	:global(.scrollbar-hide::-webkit-scrollbar) {
		display: none;
	}
	:global(.line-clamp-1) {
		display: -webkit-box;
		-webkit-line-clamp: 1;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}
	:global(.line-clamp-2) {
		display: -webkit-box;
		-webkit-line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}
</style>
