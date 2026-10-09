<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { page } from '$app/stores';
	import { authStore } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import * as Dialog from '$lib/components/ui/dialog';
	import PdfViewer from '$lib/components/venUI/pdf-viewer/PdfViewer.svelte';
	import AllocateContactsDialog from './AllocateContactsDialog.svelte';
	import SearchSingleContactDialog from './components/SearchSingleContactDialog.svelte';
	import ContactList from './components/ContactList.svelte';
	import Workspace from './components/Workspace.svelte';
	import CallingStatsBar from './components/CallingStatsBar.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import Loader2 from '@lucide/svelte/icons/loader-2';


	import {
		GetCrmAgentContactsDocument,
		GetCrmContactByIdDocument,
		GetCrmSettingDocument,
		GetCrmCallLogsDocument,
		GetCrmCallRemindersDocument,
		GetCrmContactInvoicesDocument,
		GetCrmContactClaimsDocument,
		GetCrmContactFleetDetailsDocument,
		SaveCrmContactFleetDetailDocument,
		DeleteCrmContactFleetDetailDocument,
		LogCrmCallDocument,
		UndoCrmCallDocument,
		UpdateCrmCallLogNotesDocument,
		GetSalesUsersDocument,
		CompleteCrmReminderDocument,
		AllocateAgentContactsDocument,
		DeallocateCrmContactDocument,
		DeallocateCrmContactsDocument,
		PrintDocumentsMutation,
		type CrmContact,
		type CallLog,
		type CallReminder,
		type ContactInvoice,
		type ContactClaim,
		type CrmAgentContact,
		type CrmContactFleetDetail,
		type CrmContactFleetDetailInput
	} from './queries';

	// State
	let selectedContact = $state<CrmContact | null>(null);
	let activeTab = $state<'log' | 'history' | 'reminders' | 'business' | 'claims' | 'fleet'>('log');
	
	let callLogs = $state<CallLog[]>([]);
	let reminders = $state<CallReminder[]>([]);
	let invoices = $state<ContactInvoice[]>([]);
	let claims = $state<ContactClaim[]>([]);
	let fleetDetails = $state<CrmContactFleetDetail[]>([]);
	
	let loadingHistory = $state(false);
	let loadingInvoices = $state(false);
	let loadingClaims = $state(false);
	let loadingFleet = $state(false);

	let isSavingLog = $state(false);
	let isUndoingLog = $state<string | null>(null);
	let completingReminderId = $state<string | null>(null);
	let salesUsersList = $state<{ userName: string; fullName: string }[]>([]);
	
	let pdfData = $state<Uint8Array | null>(null);
	let pdfFileName = $state<string>('');
	let showPdfViewer = $state(false);
	let loadingPdf = $state(false);
	let printingDocNo = $state<string | null>(null);

	let isFetchingContactDetails = $derived(loadingHistory || loadingInvoices || loadingClaims || loadingFleet);

	let allocateDialogOpen = $state(false);
	let searchSingleDialogOpen = $state(false);
	let searchSingleInitialTerm = $state('');
	let isAllocating = $state(false);
	let isDeallocating = $state(false);
	let isBulkDeallocating = $state(false);
	let statsBarRef = $state<any>(null);

	// Custom List State
	let allocatedAgentContacts = $state<CrmAgentContact[]>([]);
	let isListLoading = $state(false);
	let searchQuery = $state('');
	let filterCallDate = $state('pending');
	let pageSize = $state(10);
	let isListCollapsed = $state(false);

	function cleanUsername(raw?: string | null): string {
		if (!raw) return '';
		return raw.replace(/^tyresoles\\/i, '').trim();
	}

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
		let items = allocatedAgentContacts
			.map((ac) => {
				const c = ac.contact;
				if (!c) return null;
				return {
					...c,
					lastCallDate: ac.lastCallDate || c.lastCallDate,
					lastCallOutcome: ac.lastCallOutcome || c.lastCallOutcome,
					callCount: ac.callCount
				};
			})
			.filter(Boolean) as (CrmContact & { callCount?: number })[];
		
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

		if (filterCallDate === 'today') {
			// Shows today's contacts:
			// 1. Contacts called today (even if finished calling!)
			// 2. Untouched / pending contacts ready to be called today
			return items.filter(c => {
				if (isToday(c.lastCallDate)) return true;
				if (!c.lastCallDate || !c.lastCallOutcome || c.lastCallOutcome.trim() === '' || c.callCount === 0) return true;
				return false;
			});
		} else if (filterCallDate === 'called_today') {
			// Strictly contacts called today
			return items.filter(c => isToday(c.lastCallDate));
		} else if (filterCallDate === 'pending') {
			// Untouched / pending contacts: NO call logged or never called!
			return items.filter(c => !c.lastCallOutcome || c.lastCallOutcome.trim() === '' || !c.lastCallDate || c.callCount === 0);
		} else if (filterCallDate === 'all') {
			return items;
		} else if (filterCallDate === 'recent_7d') {
			const sevenDaysAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
			return items.filter(c => {
				if (!c.lastCallDate) return false;
				return new Date(c.lastCallDate).getTime() >= sevenDaysAgo;
			});
		} else if (filterCallDate === 'connected') {
			const sevenDaysAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
			return items.filter(c => {
				if (!c.lastCallDate || new Date(c.lastCallDate).getTime() < sevenDaysAgo) return false;
				const norm = (c.lastCallOutcome || '').toLowerCase();
				return !norm.includes('unreachable') && !norm.includes('no answer') && !norm.includes('ringing') && !norm.includes('switched off') && !norm.includes('not reachable') && !norm.includes('missed') && !norm.includes('busy');
			});
		} else if (filterCallDate === 'not_called_7d') {
			const sevenDaysAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
			return items.filter(c => {
				if (!c.lastCallDate) return true;
				return new Date(c.lastCallDate).getTime() < sevenDaysAgo;
			});
		} else if (filterCallDate === 'followup') {
			const sevenDaysAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
			return items.filter(c => {
				if (!c.lastCallDate || new Date(c.lastCallDate).getTime() < sevenDaysAgo) return false;
				const norm = (c.lastCallOutcome || '').toLowerCase();
				return norm.includes('follow') || norm.includes('callback') || norm.includes('reminder') || norm.includes('busy');
			});
		} else if (filterCallDate === 'positive') {
			const sevenDaysAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
			return items.filter(c => {
				if (!c.lastCallDate || new Date(c.lastCallDate).getTime() < sevenDaysAgo) return false;
				const norm = (c.lastCallOutcome || '').toLowerCase();
				return !norm.includes('not interested') && (norm.includes('interested') || norm.includes('sale') || norm.includes('order') || norm.includes('won') || norm.includes('completed'));
			});
		} else {
			// Specific outcome match from compact filter
			return items.filter(c => {
				if (!c.lastCallOutcome) return false;
				return c.lastCallOutcome.trim().toLowerCase() === filterCallDate.trim().toLowerCase();
			});
		}
	});

	$effect(() => {
		const id = selectedContact?.id;
		untrack(() => {
			if (id) {
				activeTab = 'log';
				loadHistory(id);
				loadInvoices(id);
				loadClaims(id);
				loadFleet(id);
			} else {
				callLogs = [];
				reminders = [];
				invoices = [];
				claims = [];
				fleetDetails = [];
			}
		});
	});

	async function loadAllocatedContacts() {
		isListLoading = true;
		try {
			const raw = $authStore.username || '';
			const clean = cleanUsername(raw);

			// Only show contacts for the same day; on next date, it starts as a clean blank list
			const now = new Date();
			const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0, 0);
			const startOfTodayIso = startOfToday.toISOString();

			const whereClause: any = {
				deallocatedAt: { eq: null },
				allocatedAt: { gte: startOfTodayIso }
			};
			if (clean) {
				whereClause.or = [
					{ agentUsername: { eq: raw } },
					{ agentUsername: { eq: clean } },
					{ agentUsername: { contains: clean } }
				];
			}

			const res = await graphqlQuery<any>(GetCrmAgentContactsDocument, {
				variables: {
					take: 500, // Load active pool of contacts
					where: whereClause,
					order: [{ contact: { lastCallDate: 'ASC' } }]
				}
			});
			if (res.success && res.data?.crmAgentContacts?.items) {
				// Client-side same-day safeguard: only keep contacts allocated or called today
				allocatedAgentContacts = res.data.crmAgentContacts.items.filter((ac: any) => 
					isToday(ac.allocatedAt) || isToday(ac.lastCallDate)
				);
				filterCallDate = 'pending';
			}
		} catch (err) {
			console.error('Failed to load initial contacts', err);
		} finally {
			isListLoading = false;
		}
	}

	async function loadHistory(contactId: string, skipCache: boolean = true) {
		loadingHistory = true;
		try {
			const [logsRes, remRes] = await Promise.all([
				graphqlQuery<{ crmCallLogs: CallLog[] }>(GetCrmCallLogsDocument, { variables: { contactId }, skipCache }),
				graphqlQuery<{ crmCallReminders: CallReminder[] }>(GetCrmCallRemindersDocument, { variables: { contactId, includeCompleted: false }, skipCache })
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

	async function loadFleet(contactId: string) {
		loadingFleet = true;
		try {
			const res = await graphqlQuery<{ crmContactFleetDetails: CrmContactFleetDetail[] }>(
				GetCrmContactFleetDetailsDocument,
				{ variables: { contactId }, skipCache: true }
			);
			if (res.success && res.data?.crmContactFleetDetails) {
				fleetDetails = res.data.crmContactFleetDetails;
			} else {
				fleetDetails = [];
			}
		} catch (err) {
			console.error('Failed to load fleet details', err);
			fleetDetails = [];
		} finally {
			loadingFleet = false;
		}
	}

	async function handleSaveFleetItem(input: CrmContactFleetDetailInput): Promise<boolean> {
		try {
			const res = await graphqlMutation<{ saveCrmContactFleetDetail: CrmContactFleetDetail }>(
				SaveCrmContactFleetDetailDocument,
				{ variables: { input } }
			);
			if (res.success && res.data?.saveCrmContactFleetDetail) {
				const saved = res.data.saveCrmContactFleetDetail;
				const idx = fleetDetails.findIndex((f) => f.id === saved.id);
				if (idx !== -1) {
					fleetDetails = fleetDetails.map((f) => (f.id === saved.id ? saved : f));
				} else {
					fleetDetails = [...fleetDetails, saved];
				}
				toast.success('Fleet record saved successfully.');
				return true;
			} else {
				toast.error(res.error || 'Failed to save fleet record');
				return false;
			}
		} catch (err: any) {
			console.error('Error saving fleet record', err);
			toast.error(err.message || 'Error saving fleet record');
			return false;
		}
	}

	async function handleDeleteFleetItem(id: string): Promise<boolean> {
		try {
			const res = await graphqlMutation<{ deleteCrmContactFleetDetail: boolean }>(
				DeleteCrmContactFleetDetailDocument,
				{ variables: { id } }
			);
			if (res.success && res.data?.deleteCrmContactFleetDetail) {
				fleetDetails = fleetDetails.filter((f) => f.id !== id);
				toast.success('Fleet record removed.');
				return true;
			} else {
				toast.error(res.error || 'Failed to remove fleet record');
				return false;
			}
		} catch (err: any) {
			console.error('Error deleting fleet record', err);
			toast.error(err.message || 'Error deleting fleet record');
			return false;
		}
	}


	function handleSelectContactById(contactId: string) {
		const found =
			filteredContacts.find((c) => c.id === contactId) ||
			allocatedAgentContacts.find((ac) => ac.contactId === contactId || ac.contact?.id === contactId)?.contact;
		if (found) {
			selectedContact = found;
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
				contactIsActive: data.isPositive === false ? false : null,
				salesUserId: data.salesUserId || null
			};
			const res = await graphqlMutation<{ logCrmCall: { success: boolean; message: string } }>(LogCrmCallDocument, { variables: input });
			if (res.success && res.data?.logCrmCall.success) {
				toast.success('Call log saved successfully.');
				activeTab = 'history';
				await loadHistory(selectedContact.id, true);

				const nowIso = new Date().toISOString();
				selectedContact.lastCallDate = nowIso;
				selectedContact.lastCallOutcome = data.outcome;

				allocatedAgentContacts = allocatedAgentContacts.map((ac) => {
					if (ac.contactId === selectedContact!.id || ac.contact?.id === selectedContact!.id) {
						return {
							...ac,
							lastCallDate: nowIso,
							lastCallOutcome: data.outcome,
							callCount: (ac.callCount || 0) + 1,
							contact: ac.contact
								? {
										...ac.contact,
										lastCallDate: nowIso,
										lastCallOutcome: data.outcome
									}
								: ac.contact
						};
					}
					return ac;
				});
				statsBarRef?.loadData?.();
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
				if (selectedContact) {
					await loadHistory(selectedContact.id);
					const latest = callLogs.length > 0 ? callLogs[0] : null;
					const newDate = latest ? latest.callDate : null;
					const newOutcome = latest ? latest.outcome : null;
					selectedContact.lastCallDate = newDate;
					selectedContact.lastCallOutcome = newOutcome;

					allocatedAgentContacts = allocatedAgentContacts.map((ac) => {
						if (ac.contactId === selectedContact!.id || ac.contact?.id === selectedContact!.id) {
							return {
								...ac,
								lastCallDate: newDate,
								lastCallOutcome: newOutcome,
								contact: ac.contact
									? {
											...ac.contact,
											lastCallDate: newDate,
											lastCallOutcome: newOutcome
										}
									: ac.contact
							};
						}
						return ac;
					});
				}
				statsBarRef?.loadData?.();
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

	async function handleUpdateCallLogNotes(callLogId: string, notes: string | null): Promise<boolean> {
		try {
			const res = await graphqlMutation<{ updateCrmCallLogNotes: { success: boolean; message: string } }>(
				UpdateCrmCallLogNotesDocument,
				{ variables: { callLogId, notes } }
			);
			if (res.success && res.data?.updateCrmCallLogNotes.success) {
				toast.success('Comment updated successfully.');
				callLogs = callLogs.map((l) => (l.id === callLogId ? { ...l, notes } : l));
				return true;
			} else {
				toast.error(res.error || 'Failed to update comment.');
				return false;
			}
		} catch (err: any) {
			console.error(err);
			toast.error(err.message || 'An error occurred while updating comment.');
			return false;
		}
	}

	async function handleCompleteReminder(reminderId: string) {
		completingReminderId = reminderId;
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
		} finally {
			completingReminderId = null;
		}
	}

	async function handleDeallocateContact(contactId: string) {
		const target = allocatedAgentContacts.find(c => c.contactId === contactId || c.contact?.id === contactId);
		const c = target?.contact;
		const callCount = target?.callCount ?? 0;
		const lastDate = target?.lastCallDate || c?.lastCallDate;
		const outcome = target?.lastCallOutcome || c?.lastCallOutcome;
		if (callCount > 0 || lastDate || (outcome && outcome.trim() !== '')) {
			toast.error('Only pending and untouched contacts can be deallocated.');
			return;
		}

		if (!confirm('Are you sure you want to deallocate this contact?')) return;
		isDeallocating = true;
		try {
			const res = await graphqlMutation<{ deallocateCrmContact: { success: boolean; message: string } }>(DeallocateCrmContactDocument, { variables: { contactId } });
			if (res.success && res.data?.deallocateCrmContact.success) {
				toast.success('Contact deallocated successfully.');
				selectedContact = null;
				isListCollapsed = false;
				allocatedAgentContacts = allocatedAgentContacts.filter(c => c.contactId !== contactId);
				statsBarRef?.loadData?.();
			} else {
				toast.error(res.error || 'Failed to deallocate contact.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			isDeallocating = false;
		}
	}

	async function handleBulkDeallocate(contactIds: string[]) {
		if (!contactIds || contactIds.length === 0) return;

		// Guard: only allow deallocation for untouched contacts
		const untouchedIds = contactIds.filter(id => {
			const target = allocatedAgentContacts.find(c => c.contactId === id || c.contact?.id === id);
			const c = target?.contact;
			const callCount = target?.callCount ?? 0;
			const lastDate = target?.lastCallDate || c?.lastCallDate;
			const outcome = target?.lastCallOutcome || c?.lastCallOutcome;
			return callCount === 0 && !lastDate && (!outcome || outcome.trim() === '');
		});

		if (untouchedIds.length === 0) {
			toast.error('Cannot deallocate: only pending and untouched contacts can be deallocated.');
			return;
		}

		if (!confirm(`Are you sure you want to deallocate ${untouchedIds.length} untouched contact${untouchedIds.length === 1 ? '' : 's'}?`)) return;
		isBulkDeallocating = true;
		try {
			const res = await graphqlMutation<{ deallocateCrmContacts: { success: boolean; message: string } }>(
				DeallocateCrmContactsDocument,
				{ variables: { contactIds: untouchedIds } }
			);
			if (res.success && res.data?.deallocateCrmContacts.success) {
				toast.success(res.data.deallocateCrmContacts.message || `Successfully deallocated ${untouchedIds.length} contact${untouchedIds.length === 1 ? '' : 's'}.`);
				const deallocatedSet = new Set(untouchedIds);
				if (selectedContact && deallocatedSet.has(selectedContact.id)) {
					selectedContact = null;
					isListCollapsed = false;
				}
				allocatedAgentContacts = allocatedAgentContacts.filter(c => !deallocatedSet.has(c.contactId));
				statsBarRef?.loadData?.();
			} else {
				toast.error(res.error || res.data?.deallocateCrmContacts.message || 'Failed to deallocate contacts.');
			}
		} catch (err: any) {
			console.error(err);
			toast.error(err.message || 'An error occurred during deallocation.');
		} finally {
			isBulkDeallocating = false;
		}
	}

	const ALLOCATION_FILTER_STORAGE_KEY = 'crm_calling_last_allocation_filters';

	function getSavedAllocationFilters(): any | null {
		try {
			const stored = localStorage.getItem(ALLOCATION_FILTER_STORAGE_KEY);
			if (!stored) return null;
			const parsed = JSON.parse(stored);
			if (parsed && typeof parsed === 'object' && Object.keys(parsed).length > 0) {
				return parsed;
			}
		} catch (e) {
			console.error('Failed to read stored allocation filters', e);
		}
		return null;
	}

	async function handleQuickLoadContacts(limit: number = 10) {
		const savedFilters = getSavedAllocationFilters();

		// If filter is not saved, clicking get contact button should show filter dialog
		if (!savedFilters) {
			allocateDialogOpen = true;
			return;
		}

		// Otherwise, load contacts using saved filter
		await handleAllocateContacts({
			...savedFilters,
			limit
		});
	}

	async function handleAllocateContacts(filters: any) {
		isAllocating = true;
		try {
			// Normalize filters so only valid fields on AllocateAgentContactsInput are sent
			const payload: any = {
				coolDownDays: filters.coolDownDays ?? null,
				respCenters: Array.isArray(filters.respCenters)
					? filters.respCenters
					: (filters.respCenter ? filters.respCenter.split(',').map((s: string) => s.trim()).filter(Boolean) : []),
				areas: Array.isArray(filters.areas)
					? filters.areas
					: (filters.areas ? filters.areas.split(',').map((s: string) => s.trim()).filter(Boolean) : []),
				products: filters.products || [],
				states: filters.states || [],
				cities: filters.cities || [],
				types: filters.types || [],
				categories: filters.categories || [],
				tags: filters.tags || [],
				limit: filters.limit ?? 10
			};

			const res = await graphqlMutation<{ allocateAgentContacts: { success: boolean; message: string; allocatedContacts: CrmAgentContact[] } }>(
				AllocateAgentContactsDocument, 
				{ variables: { input: payload } }
			);
			if (res.success && res.data?.allocateAgentContacts.success) {
				const newContacts = res.data.allocateAgentContacts.allocatedContacts || [];
				toast.success(res.data.allocateAgentContacts.message || `Successfully allocated ${newContacts.length} new contacts.`);
				
				// Append new contacts to the active list without duplicates
				const existingIds = new Set(allocatedAgentContacts.map(c => c.contactId || c.contact?.id));
				const fresh = newContacts.filter(c => !existingIds.has(c.contactId || c.contact?.id));
				allocatedAgentContacts = [...allocatedAgentContacts, ...fresh];
				if (newContacts.length > 0) {
					filterCallDate = 'pending';
				}
				statsBarRef?.loadData?.();
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

	function handleOpenSearchSingle(term: string = '') {
		searchSingleInitialTerm = term;
		searchSingleDialogOpen = true;
	}

	function handleContactAllocatedOnDemand(contact: CrmContact, allocation?: CrmAgentContact) {
		// Check if already present in allocated list
		const existingIdx = allocatedAgentContacts.findIndex(
			(ac) => ac.contactId === contact.id || ac.contact?.id === contact.id
		);

		let targetAlloc: CrmAgentContact;
		if (existingIdx !== -1) {
			targetAlloc = allocatedAgentContacts[existingIdx];
		} else {
			targetAlloc = allocation || {
				id: crypto.randomUUID(),
				agentUsername: cleanUsername($authStore.username),
				contactId: contact.id,
				contact: contact,
				allocatedAt: new Date().toISOString(),
				callCount: 0
			};
			allocatedAgentContacts = [targetAlloc, ...allocatedAgentContacts];
		}

		selectedContact = targetAlloc.contact || contact;
		isListCollapsed = true;
		if (filterCallDate !== 'all' && filterCallDate !== 'today' && filterCallDate !== 'pending') {
			filterCallDate = 'all';
		}
		statsBarRef?.loadData?.();
	}

	async function handlePrintDocument(docNo: string, view: string) {
		if (loadingPdf) return;
		loadingPdf = true;
		printingDocNo = docNo;
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
			printingDocNo = null;
		}
	}

	async function selectContactFromUrlParam() {
		const targetContactId = $page.url.searchParams.get('contactId');
		if (!targetContactId) return;

		const targetAlloc = allocatedAgentContacts.find(
			(ac) => ac.contactId === targetContactId || ac.contact?.id === targetContactId
		);

		if (targetAlloc) {
			selectedContact = targetAlloc.contact;
			isListCollapsed = true;
			// Refresh single contact details in case it was modified in master
			graphqlQuery<{ contact: CrmContact | null }>(GetCrmContactByIdDocument, {
				variables: { id: targetContactId },
				skipCache: true
			}).then((res) => {
				if (res.success && res.data?.contact) {
					const updated = res.data.contact;
					selectedContact = updated;
					allocatedAgentContacts = allocatedAgentContacts.map((ac) => {
						if (ac.contactId === targetContactId || ac.contact?.id === targetContactId) {
							return { ...ac, contact: updated };
						}
						return ac;
					});
				}
			});
		} else {
			// If not currently in active list, fetch from server and allocate on-demand
			try {
				const res = await graphqlQuery<{ contact: CrmContact | null }>(GetCrmContactByIdDocument, {
					variables: { id: targetContactId },
					skipCache: true
				});
				if (res.success && res.data?.contact) {
					handleContactAllocatedOnDemand(res.data.contact);
				}
			} catch (err) {
				console.error('Failed to load contact specified in URL', err);
			}
		}
	}

	onMount(async () => {
		try {
			const settingRes = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, { variables: { key: 'ContactsPerAgent' } });
			if (settingRes.success && settingRes.data?.getCrmSetting) {
				const limit = parseInt(settingRes.data.getCrmSetting.value, 10);
				if (!isNaN(limit) && limit > 0) pageSize = limit;
			}
			
			// Initial load of agent's allocated contacts
			await loadAllocatedContacts();
			await selectContactFromUrlParam();

			// Fetch sales users list for forwarded sales person name resolution
			graphqlQuery<any>(GetSalesUsersDocument, {
				variables: {
					where: { userType: { eq: 'SALES' } },
					take: 200
				},
				silent: true
			}).then((res) => {
				if (res.data?.users?.items) {
					salesUsersList = res.data.users.items;
				}
			}).catch((e) => console.error('Failed to load sales users list', e));

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

	$effect(() => {
		const user = $authStore.username;
		untrack(() => {
			if (user && allocatedAgentContacts.length === 0 && !isListLoading) {
				loadAllocatedContacts();
			}
		});
	});

	$effect(() => {
		const targetContactId = $page.url.searchParams.get('contactId');
		if (targetContactId && (!selectedContact || selectedContact.id !== targetContactId)) {
			untrack(() => {
				selectContactFromUrlParam();
			});
		}
	});

	function handleCallMobile(mobile: string) {
		window.open(`tel:${mobile}`);
	}
</script>

<svelte:head>
	<title>CRM Call Center | Tyresoles</title>
</svelte:head>

<div class="h-screen max-h-screen overflow-hidden bg-background text-foreground flex flex-col select-none">
	<!-- Top Performance & Goal Trend Bar (Compact & Collapsible) -->
	<CallingStatsBar
		bind:this={statsBarRef}
		bind:filterCallDate
		onSelectContactById={handleSelectContactById}
		{salesUsersList}
	/>

	<!-- Two-column Calling Center Area -->
	<div class="flex-1 min-h-0 flex flex-col md:flex-row overflow-hidden">
		<div class="{isListCollapsed ? 'hidden' : 'flex'} h-full shrink-0">
			<ContactList
				list={listMock}
				{filteredContacts}
				bind:selectedContact
				bind:filterCallDate
				{isAllocating}
				{isFetchingContactDetails}
				{isBulkDeallocating}
				onBulkDeallocate={handleBulkDeallocate}
				onSelectContact={(c) => {
					selectedContact = c;
					isListCollapsed = true;
				}}
				onRequestMoreContacts={() => (allocateDialogOpen = true)}
				onQuickLoadContacts={handleQuickLoadContacts}
				onOpenSearchSingle={handleOpenSearchSingle}
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
			{fleetDetails}
			{loadingFleet}
			{loadingHistory}
			{loadingInvoices}
			{loadingClaims}
			{printingDocNo}
			onSaveCallLog={handleSaveCallLog}
			onUndoCallLog={handleUndoCallLog}
			onCompleteReminder={handleCompleteReminder}
			{completingReminderId}
			onPrintDocument={handlePrintDocument}
			onRefreshHistory={() => selectedContact && loadHistory(selectedContact.id, true)}
			onUpdateCallLogNotes={handleUpdateCallLogNotes}
			onSaveFleetItem={handleSaveFleetItem}
			onDeleteFleetItem={handleDeleteFleetItem}
			{salesUsersList}
			{isSavingLog}
			{isUndoingLog}
		/>
	</div>
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

<AllocateContactsDialog bind:open={allocateDialogOpen} onAllocate={handleAllocateContacts} />

<SearchSingleContactDialog
	bind:open={searchSingleDialogOpen}
	initialSearch={searchSingleInitialTerm}
	allocatedContactIds={allocatedAgentContacts.map(c => c.contactId || c.contact?.id)}
	onContactAllocated={handleContactAllocatedOnDemand}
/>

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
		line-clamp: 1;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}
	:global(.line-clamp-2) {
		display: -webkit-box;
		-webkit-line-clamp: 2;
		line-clamp: 2;
		-webkit-box-orient: vertical;
		overflow: hidden;
	}
</style>
