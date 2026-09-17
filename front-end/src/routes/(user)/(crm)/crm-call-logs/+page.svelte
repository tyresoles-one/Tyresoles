<script lang="ts">
	import { onMount } from 'svelte';
	import { authStore } from '$lib/stores/auth';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { goto } from '$app/navigation';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Badge } from '$lib/components/ui/badge';
	import { Card, CardContent } from '$lib/components/ui/card';
	import * as Dialog from '$lib/components/ui/dialog';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	import { DatePicker, DatePresets, type PresetEntry } from '$lib/components/venUI/date-picker';
	import { Select } from '$lib/components/venUI/select';
	import { today, getLocalTimeZone, CalendarDate } from '@internationalized/date';

	import {
		GetAllCrmCallLogsDocument,
		GetCrmAgentContactsDocument,
		GetCrmMasterItemsDocument,
		GetCrmCallLogUsersDocument,
		GetAllCrmCallRemindersDocument,
		CompleteCrmReminderDocument,
		GetMyDocumentsDocument,
		UpdateCrmCallLogInvoicesDocument,
		type DocumentDto,
		type DetailedCallLog,
		type CrmAgentContactInfo,
		type CrmContactInfo,
		type CrmCallReminderInfo
	} from './queries';

	// Filters State
	let datePreset = $state<'today' | 'yesterday' | 'week' | 'month' | 'custom' | 'all'>('week');
	let dateRange = $state<{ start: any; end: any } | undefined>();
	let selectedOutcome = $state<string>('ALL');
	let textSearch = $state<string>('');
	let remindersOnly = $state<boolean>(false);
	let selectedAllocatedContact = $state<CrmAgentContactInfo | null>(null);

	// Date Presets for venUI DatePicker
	const nowCal = today(getLocalTimeZone());
	const callLogPresets: PresetEntry[] = [
		{ label: 'Today', value: { start: nowCal, end: nowCal } },
		{ label: 'Yesterday', value: { start: nowCal.subtract({ days: 1 }), end: nowCal.subtract({ days: 1 }) } },
		{ label: 'Last 7 Days', value: { start: nowCal.subtract({ days: 7 }), end: nowCal } },
		{ label: 'Last 30 Days', value: { start: nowCal.subtract({ days: 30 }), end: nowCal } },
		DatePresets.range.thisMonth(nowCal),
		DatePresets.range.lastMonth(nowCal)
	];

	// User / Agent Multi-Select Filter State
	let availableUsers = $state<string[]>([]);
	let selectedUsers = $state<string[]>([]);
	let showUserFilterModal = $state<boolean>(false);
	let userFilterSearch = $state<string>('');
	let isLoadingUsers = $state<boolean>(false);

	// Pagination State
	let currentPage = $state<number>(1);
	let pageSize = $state<number>(20);

	// Data & Loading State
	let allCallLogs = $state<DetailedCallLog[]>([]);
	let isLoading = $state<boolean>(false);
	let outcomes = $state<{ id: number; name: string; isPositive?: boolean }[]>([]);

	// Active Reminders Map (keyed by contactId)
	let activeReminders = $state<CrmCallReminderInfo[]>([]);
	let remindersMap = $derived.by(() => {
		const map = new Map<string, CrmCallReminderInfo>();
		for (const r of activeReminders) {
			if (!r.isCompleted && r.contactId) {
				if (!map.has(r.contactId) || new Date(r.reminderDate) < new Date(map.get(r.contactId)!.reminderDate)) {
					map.set(r.contactId, r);
				}
			}
		}
		return map;
	});

	// Allocated Contacts Finder Modal
	let showContactFinderModal = $state<boolean>(false);
	let allocatedContacts = $state<CrmAgentContactInfo[]>([]);
	let isLoadingAllocated = $state<boolean>(false);
	let contactFinderSearch = $state<string>('');

	// Call Log Detail Modal
	let selectedCallLog = $state<DetailedCallLog | null>(null);
	let showDetailModal = $state<boolean>(false);

	// Reminder Detail Modal State
	let selectedReminder = $state<CrmCallReminderInfo | null>(null);
	let showReminderModal = $state<boolean>(false);
	let isCompletingReminder = $state<boolean>(false);

	// Sales Invoices (last 30 days & respCenter 'BEL')
	let salesInvoices = $state<DocumentDto[]>([]);
	let isLoadingInvoices = $state<boolean>(false);
	let selectedInvoiceNos = $state<string[]>([]);
	let initialLoadedInvoiceNos = $state<string[]>([]);
	let isSavingInvoices = $state<boolean>(false);
	let selectedInvoiceMap = $state<Map<string, string[]>>(new Map());

	let hasInvoiceChanges = $derived.by(() => {
		if (!selectedCallLog) return false;
		if (selectedInvoiceNos.length !== initialLoadedInvoiceNos.length) return true;
		const setA = new Set(selectedInvoiceNos);
		return !initialLoadedInvoiceNos.every((no) => setA.has(no));
	});

	let selectedInvoices = $derived.by(() => {
		const invoiceMap = new Map(salesInvoices.map((inv) => [inv.no, inv]));
		return selectedInvoiceNos.map((no) => {
			if (invoiceMap.has(no)) {
				return invoiceMap.get(no)!;
			}
			return {
				no,
				date: '',
				customerNo: '',
				name: 'Linked Invoice',
				amount: 0,
				quantity: 0
			} as DocumentDto;
		});
	});

	let totalSelectedInvoicesAmount = $derived(
		selectedInvoices.reduce((sum, inv) => sum + (inv.amount || 0), 0)
	);

	let totalSelectedInvoicesTyres = $derived(
		selectedInvoices.reduce((sum, inv) => sum + (inv.quantity || 0), 0)
	);

	const invoiceColumns = [
		{ header: 'Invoice No', accessor: (row: DocumentDto) => row.no, class: 'font-mono font-bold min-w-[100px]' },
		{ header: 'Customer', accessor: (row: DocumentDto) => row.name, class: 'min-w-[140px] truncate' },
		{ header: 'Tyres', accessor: (row: DocumentDto) => (row.quantity ? `${row.quantity} tyres` : '0 tyres'), class: 'text-center font-semibold whitespace-nowrap text-amber-600 dark:text-amber-400' },
		{ header: 'Date', accessor: (row: DocumentDto) => formatInvoiceDate(row.date), class: 'text-right whitespace-nowrap' },
		{ header: 'Amount', accessor: (row: DocumentDto) => formatCurrency(row.amount), class: 'text-right font-mono font-semibold whitespace-nowrap text-emerald-600 dark:text-emerald-400' }
	];

	function formatInvoiceDate(iso?: string) {
		if (!iso) return '—';
		try {
			return new Date(iso).toLocaleDateString('en-IN', {
				day: '2-digit',
				month: 'short',
				year: 'numeric'
			});
		} catch {
			return iso;
		}
	}

	function formatCurrency(n?: number) {
		return new Intl.NumberFormat('en-IN', {
			style: 'currency',
			currency: 'INR',
			maximumFractionDigits: 0
		}).format(n || 0);
	}

	async function loadSalesInvoices() {
		if (salesInvoices.length > 0 || isLoadingInvoices) return;
		isLoadingInvoices = true;
		try {
			const now = new Date();
			const thirtyDaysAgo = new Date();
			thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
			thirtyDaysAgo.setHours(0, 0, 0, 0);

			const res = await graphqlQuery<{ getMyDocuments: DocumentDto[] }>(GetMyDocumentsDocument, {
				variables: {
					input: {
						view: 'Invoice',
						respCenters: ['BEL'],
						from: thirtyDaysAgo.toISOString(),
						to: now.toISOString(),
						take: 500
					}
				}
			});
			if (res.success && res.data?.getMyDocuments) {
				salesInvoices = res.data.getMyDocuments;
			}
		} catch (e) {
			console.error('Failed to load sales invoices for BEL', e);
		} finally {
			isLoadingInvoices = false;
		}
	}

	function openCallLogDetails(log: DetailedCallLog) {
		if (!['Interested', 'Order Placed'].includes(log.outcome)) {
			toast.info('Invoice linking is only available for "Interested" or "Order Placed" call logs.');
			return;
		}
		selectedCallLog = log;
		const initialNos = log.invoiceNos
			? log.invoiceNos.split(',').map((s) => s.trim()).filter(Boolean)
			: selectedInvoiceMap.get(log.id)
			? [...selectedInvoiceMap.get(log.id)!]
			: [];
		selectedInvoiceNos = [...initialNos];
		initialLoadedInvoiceNos = [...initialNos];
		showDetailModal = true;
		loadSalesInvoices();
	}

	async function saveCallLogInvoices() {
		if (!selectedCallLog || isSavingInvoices) return;
		isSavingInvoices = true;
		try {
			const res = await graphqlMutation<{ updateCrmCallLogInvoices: { success: boolean; message: string } }>(
				UpdateCrmCallLogInvoicesDocument,
				{
					variables: {
						callLogId: selectedCallLog.id,
						invoiceNos: selectedInvoiceNos
					}
				}
			);

			if (res.success && res.data?.updateCrmCallLogInvoices.success) {
				toast.success(res.data.updateCrmCallLogInvoices.message || 'Invoices linked to call log successfully');
				const joinedNos = selectedInvoiceNos.length > 0 ? selectedInvoiceNos.join(',') : null;
				const savedAmount = totalSelectedInvoicesAmount;
				const savedTyres = totalSelectedInvoicesTyres;

				selectedCallLog.invoiceNos = joinedNos;
				selectedCallLog.invoiceAmount = savedAmount;
				selectedCallLog.tyreQuantity = savedTyres;
				initialLoadedInvoiceNos = [...selectedInvoiceNos];
				if (selectedInvoiceNos.length > 0) {
					selectedInvoiceMap.set(selectedCallLog.id, [...selectedInvoiceNos]);
				} else {
					selectedInvoiceMap.delete(selectedCallLog.id);
				}

				allCallLogs = allCallLogs.map((l) =>
					l.id === selectedCallLog!.id
						? { ...l, invoiceNos: joinedNos, invoiceAmount: savedAmount, tyreQuantity: savedTyres }
						: l
				);
			} else {
				toast.error(
					res.data?.updateCrmCallLogInvoices.message ||
					res.errors?.[0]?.message ||
					'Failed to save invoices'
				);
			}
		} catch (e: any) {
			console.error('Failed to save call log invoices', e);
			toast.error(e?.message || 'Error saving invoices');
		} finally {
			isSavingInvoices = false;
		}
	}

	function removeSelectedInvoice(no: string) {
		selectedInvoiceNos = selectedInvoiceNos.filter((n) => n !== no);
		if (selectedCallLog) {
			if (selectedInvoiceNos.length > 0) {
				selectedInvoiceMap.set(selectedCallLog.id, [...selectedInvoiceNos]);
			} else {
				selectedInvoiceMap.delete(selectedCallLog.id);
			}
		}
	}

	$effect(() => {
		if (selectedCallLog && selectedInvoiceNos) {
			if (selectedInvoiceNos.length > 0) {
				selectedInvoiceMap.set(selectedCallLog.id, [...selectedInvoiceNos]);
			} else {
				selectedInvoiceMap.delete(selectedCallLog.id);
			}
		}
	});

	// Helper to calculate full local day boundaries converted to UTC ISO for GraphQL
	function toUtcDayRange(range?: { start?: any; end?: any }) {
		if (!range || (!range.start && !range.end)) return { gte: null, lte: null };
		const tz = getLocalTimeZone();

		function toStartDate(val: any): Date {
			if (val?.toDate) return val.toDate(tz);
			if (val instanceof Date) return new Date(val);
			if (typeof val === 'string') {
				const [y, m, d] = val.split('-').map(Number);
				return new Date(y, m - 1, d, 0, 0, 0, 0);
			}
			return new Date(val);
		}

		function toEndDate(val: any): Date {
			if (val?.toDate) return val.toDate(tz);
			if (val instanceof Date) return new Date(val);
			if (typeof val === 'string') {
				const [y, m, d] = val.split('-').map(Number);
				return new Date(y, m - 1, d, 23, 59, 59, 999);
			}
			return new Date(val);
		}

		let gte: string | null = null;
		let lte: string | null = null;

		if (range.start) {
			const s = toStartDate(range.start);
			s.setHours(0, 0, 0, 0);
			gte = s.toISOString();
		}
		if (range.end) {
			const e = toEndDate(range.end);
			e.setHours(23, 59, 59, 999);
			lte = e.toISOString();
		} else if (range.start) {
			const e = toEndDate(range.start);
			e.setHours(23, 59, 59, 999);
			lte = e.toISOString();
		}

		return { gte, lte };
	}

	// Helper for date presets
	function setPreset(preset: 'today' | 'yesterday' | 'week' | 'month' | 'all') {
		datePreset = preset;
		const now = today(getLocalTimeZone());

		if (preset === 'today') {
			dateRange = { start: now, end: now };
		} else if (preset === 'yesterday') {
			const yest = now.subtract({ days: 1 });
			dateRange = { start: yest, end: yest };
		} else if (preset === 'week') {
			dateRange = { start: now.subtract({ days: 7 }), end: now };
		} else if (preset === 'month') {
			dateRange = { start: now.subtract({ days: 30 }), end: now };
		} else if (preset === 'all') {
			dateRange = undefined;
		}
		currentPage = 1;
		loadCallLogs();
	}

	function handleDateRangeChange(val: any) {
		if (val?.start && !val?.end) {
			// Selection in progress (start picked, waiting for end date)
			return;
		}
		dateRange = val;
		if (!val) {
			datePreset = 'all';
		} else {
			datePreset = 'custom';
		}
		currentPage = 1;
		loadCallLogs();
	}

	onMount(async () => {
		// Default to logged-in user on load
		const currentUser = $authStore.username;
		if (currentUser) {
			selectedUsers = [currentUser];
		}

		setPreset('week');
		await Promise.all([
			loadOutcomes(),
			loadUsers(),
			loadAllocatedContacts(),
			loadSalesInvoices()
		]);
	});

	async function loadUsers() {
		isLoadingUsers = true;
		try {
			const res = await graphqlQuery<{ users: string[] }>(GetCrmCallLogUsersDocument);
			if (res.success && res.data?.users) {
				const usersList = res.data.users || [];
				const currentUser = $authStore.username;
				if (currentUser && !usersList.includes(currentUser)) {
					usersList.unshift(currentUser);
				}
				availableUsers = usersList;
			}
		} catch (e) {
			console.error('Failed to load call log users', e);
		} finally {
			isLoadingUsers = false;
		}
	}

	async function loadOutcomes() {
		try {
			const res = await graphqlQuery<{ crmMasterItems: { id: number; name: string; isPositive?: boolean }[] }>(
				GetCrmMasterItemsDocument,
				{ variables: { type: 'ACTIVITY_OUTCOME' } }
			);
			if (res.success && res.data?.crmMasterItems) {
				outcomes = res.data.crmMasterItems;
			}
		} catch (e) {
			console.error('Failed to load activity outcomes', e);
		}
	}

	async function loadAllocatedContacts() {
		isLoadingAllocated = true;
		try {
			const targetUsers = selectedUsers.length > 0 ? selectedUsers : [$authStore.username].filter(Boolean);
			if (targetUsers.length === 0) {
				allocatedContacts = [];
				return;
			}

			const res = await graphqlQuery<any>(GetCrmAgentContactsDocument, {
				variables: {
					take: 1000,
					where: {
						agentUsername: { in: targetUsers },
						deallocatedAt: { eq: null }
					},
					order: [{ allocatedAt: 'DESC' }]
				}
			});
			if (res.success && res.data?.crmAgentContacts?.items) {
				allocatedContacts = res.data.crmAgentContacts.items;
			} else {
				allocatedContacts = [];
			}
		} catch (e) {
			console.error('Failed to load allocated contacts', e);
		} finally {
			isLoadingAllocated = false;
		}
	}

	async function loadReminders() {
		try {
			const res = await graphqlQuery<any>(GetAllCrmCallRemindersDocument, {
				variables: {
					take: 1000,
					where: { isCompleted: { eq: false } },
					order: [{ reminderDate: 'ASC' }]
				}
			});
			if (res.success && res.data?.crmCallReminders?.items) {
				activeReminders = res.data.crmCallReminders.items;
			} else {
				activeReminders = [];
			}
		} catch (e) {
			console.error('Failed to load active reminders', e);
		}
	}

	async function loadCallLogs() {
		isLoading = true;
		try {
			const whereClause: any = {};
			const andConditions: any[] = [];

			// Date filtering
			const { gte, lte } = toUtcDayRange(dateRange);
			if (gte) {
				andConditions.push({ callDate: { gte } });
			}
			if (lte) {
				andConditions.push({ callDate: { lte } });
			}

			// Specific allocated contact selected
			if (selectedAllocatedContact) {
				andConditions.push({ contactId: { eq: selectedAllocatedContact.contactId } });
			} else if (selectedUsers.length > 0) {
				// User Multi-Select Filter: Include call logs created by selectedUsers OR contacts allocated to selectedUsers
				const allocatedIds = allocatedContacts.map(ac => ac.contactId).filter(Boolean);
				
				if (allocatedIds.length > 0) {
					andConditions.push({
						or: [
							{ createdBy: { in: selectedUsers } },
							{ contactId: { in: allocatedIds } }
						]
					});
				} else {
					andConditions.push({ createdBy: { in: selectedUsers } });
				}
			}

			// Outcome filter
			if (selectedOutcome !== 'ALL' && selectedOutcome !== 'REMINDERS_ONLY') {
				andConditions.push({ outcome: { eq: selectedOutcome } });
			}

			if (andConditions.length > 0) {
				whereClause.and = andConditions;
			}

			const [logsRes] = await Promise.all([
				graphqlQuery<any>(GetAllCrmCallLogsDocument, {
					variables: {
						skip: 0,
						take: 1000,
						where: Object.keys(whereClause).length > 0 ? whereClause : undefined,
						order: [{ callDate: 'DESC' }]
					}
				}),
				loadReminders()
			]);

			if (logsRes.success && logsRes.data?.crmCallLogs) {
				allCallLogs = logsRes.data.crmCallLogs.items || [];
			} else {
				allCallLogs = [];
			}
		} catch (e) {
			console.error('Failed to fetch call logs', e);
			toast.error('Failed to load call logs');
		} finally {
			isLoading = false;
		}
	}

	async function handleCompleteReminder(reminderId: string) {
		isCompletingReminder = true;
		try {
			const res = await graphqlMutation<{ completeCrmReminder: { success: boolean; message: string } }>(
				CompleteCrmReminderDocument,
				{ variables: { reminderId } }
			);
			if (res.success && res.data?.completeCrmReminder?.success) {
				toast.success(res.data.completeCrmReminder.message || 'Reminder completed');
				showReminderModal = false;
				selectedReminder = null;
				await loadReminders();
			} else {
				toast.error(res.data?.completeCrmReminder?.message || 'Failed to complete reminder');
			}
		} catch (e) {
			console.error('Error completing reminder', e);
			toast.error('Error completing reminder');
		} finally {
			isCompletingReminder = false;
		}
	}

	// Filtered list by client-side text search & Reminders filter
	let filteredCallLogs = $derived.by(() => {
		let logs = [...allCallLogs];

		// Active Reminders Filter
		if (remindersOnly || selectedOutcome === 'REMINDERS_ONLY') {
			const existingContactIds = new Set(logs.map(l => l.contactId));
			for (const [cId, rem] of remindersMap.entries()) {
				if (!existingContactIds.has(cId) && rem.contact) {
					// Synthetic entry so contacts with active reminder but no call log in current range are visible
					logs.push({
						id: `rem-${rem.id}`,
						contactId: cId,
						callDate: rem.reminderDate,
						outcome: 'Active Reminder',
						notes: rem.notes,
						createdBy: rem.createdBy,
						contact: rem.contact
					});
				}
			}
			logs = logs.filter(log => remindersMap.has(log.contactId) || log.outcome === 'Active Reminder');
		}

		if (!textSearch.trim()) return logs;
		const q = textSearch.toLowerCase();
		return logs.filter(log => {
			const contactName = log.contact?.fullName?.toLowerCase() || '';
			const companyName = log.contact?.companyName?.toLowerCase() || '';
			const mobileNo = log.contact?.mobileNo?.toLowerCase() || '';
			const notes = log.notes?.toLowerCase() || '';
			const outcome = log.outcome.toLowerCase();
			const createdBy = log.createdBy.toLowerCase();
			return (
				contactName.includes(q) ||
				companyName.includes(q) ||
				mobileNo.includes(q) ||
				notes.includes(q) ||
				outcome.includes(q) ||
				createdBy.includes(q)
			);
		});
	});

	// Derive Summary Metrics across ALL filtered logs
	let totalLogsCount = $derived(filteredCallLogs.length);
	let interestedCount = $derived(
		filteredCallLogs.filter(l => {
			const norm = l.outcome.toLowerCase();
			return !norm.includes('not interested') && (norm.includes('interested') || norm.includes('sale') || norm.includes('completed') || norm.includes('order'));
		}).length
	);
	let callbacksCount = $derived(
		filteredCallLogs.filter(l => {
			const norm = l.outcome.toLowerCase();
			return norm.includes('callback') || norm.includes('follow') || norm.includes('busy') || norm.includes('reminder');
		}).length
	);
	let uniqueContactsCount = $derived(new Set(filteredCallLogs.map(l => l.contactId)).size);

	// Table page slice & total pages
	let pagedCallLogs = $derived(
		filteredCallLogs.slice((currentPage - 1) * pageSize, currentPage * pageSize)
	);
	let totalPages = $derived(Math.ceil(filteredCallLogs.length / pageSize) || 1);

	// Allocated contacts filter inside finder modal
	let filteredAllocatedContactsModal = $derived.by(() => {
		if (!contactFinderSearch.trim()) return allocatedContacts;
		const q = contactFinderSearch.toLowerCase();
		return allocatedContacts.filter(ac => {
			const c = ac.contact;
			if (!c) return false;
			return (
				c.fullName?.toLowerCase().includes(q) ||
				c.companyName?.toLowerCase().includes(q) ||
				c.mobileNo?.toLowerCase().includes(q) ||
				c.city?.toLowerCase().includes(q)
			);
		});
	});

	// Filtered available users list inside user filter modal
	let filteredUsersModal = $derived.by(() => {
		if (!userFilterSearch.trim()) return availableUsers;
		const q = userFilterSearch.toLowerCase();
		return availableUsers.filter(u => u.toLowerCase().includes(q));
	});

	// Outcome styling badges
	function getOutcomeVariant(outcomeStr: string): { bg: string; text: string; icon: string } {
		const norm = outcomeStr.toLowerCase();
		if (norm.includes('active reminder') || norm.includes('reminder')) {
			return { bg: 'bg-amber-500/15 border-amber-500/40 text-amber-600 dark:text-amber-400 font-bold', icon: 'bell', text: outcomeStr };
		}
		if (norm.includes('not interested') || norm.includes('wrong') || norm.includes('rejected') || norm.includes('no interest') || norm.includes('lost')) {
			return { bg: 'bg-red-500/15 border-red-500/40 text-red-600 dark:text-red-400 font-bold', icon: 'circle-x', text: outcomeStr };
		}
		if (norm.includes('interested') || norm.includes('sale') || norm.includes('completed') || norm.includes('order')) {
			return { bg: 'bg-emerald-500/15 border-emerald-500/40 text-emerald-600 dark:text-emerald-400 font-bold', icon: 'circle-check', text: outcomeStr };
		}
		if (norm.includes('callback') || norm.includes('follow') || norm.includes('busy')) {
			return { bg: 'bg-amber-500/15 border-amber-500/40 text-amber-600 dark:text-amber-400 font-bold', icon: 'clock', text: outcomeStr };
		}
		if (norm.includes('no answer') || norm.includes('unreachable') || norm.includes('switched off')) {
			return { bg: 'bg-sky-500/15 border-sky-500/40 text-sky-600 dark:text-sky-400 font-bold', icon: 'phone-off', text: outcomeStr };
		}
		return { bg: 'bg-primary/10 border-primary/20 text-primary font-bold', icon: 'phone-call', text: outcomeStr };
	}

	function formatDate(dtStr: string): string {
		if (!dtStr) return 'N/A';
		try {
			const d = new Date(dtStr);
			return d.toLocaleString('en-IN', {
				day: '2-digit',
				month: 'short',
				year: 'numeric',
				hour: '2-digit',
				minute: '2-digit',
				hour12: true
			});
		} catch {
			return dtStr;
		}
	}

	function openReminderDetails(reminder: CrmCallReminderInfo) {
		selectedReminder = reminder;
		showReminderModal = true;
	}

	function selectAllocatedContact(ac: CrmAgentContactInfo) {
		selectedAllocatedContact = ac;
		showContactFinderModal = false;
		currentPage = 1;
		loadCallLogs();
	}

	function clearSelectedContact() {
		selectedAllocatedContact = null;
		currentPage = 1;
		loadCallLogs();
	}

	// User selection helpers
	function toggleUserSelection(user: string) {
		if (selectedUsers.includes(user)) {
			selectedUsers = selectedUsers.filter(u => u !== user);
		} else {
			selectedUsers = [...selectedUsers, user];
		}
		currentPage = 1;
		loadAllocatedContacts().then(() => loadCallLogs());
	}

	function selectOnlyMe() {
		const currentUser = $authStore.username;
		if (currentUser) {
			selectedUsers = [currentUser];
		} else {
			selectedUsers = [];
		}
		currentPage = 1;
		loadAllocatedContacts().then(() => loadCallLogs());
	}

	function selectAllUsers() {
		selectedUsers = [...availableUsers];
		currentPage = 1;
		loadAllocatedContacts().then(() => loadCallLogs());
	}

	function clearUserSelection() {
		selectedUsers = [];
		currentPage = 1;
		loadAllocatedContacts().then(() => loadCallLogs());
	}

	function removeUserPill(user: string) {
		selectedUsers = selectedUsers.filter(u => u !== user);
		currentPage = 1;
		loadAllocatedContacts().then(() => loadCallLogs());
	}

	// Quick actions
	function makeCall(mobile?: string | null) {
		if (!mobile) {
			toast.error('No phone number available');
			return;
		}
		window.open(`tel:${mobile}`, '_self');
	}

	function sendWhatsapp(mobile?: string | null, name?: string) {
		if (!mobile) {
			toast.error('No phone number available');
			return;
		}
		const cleanNum = mobile.replace(/\D/g, '');
		const formattedNum = cleanNum.length === 10 ? `91${cleanNum}` : cleanNum;
		const msg = encodeURIComponent(`Hello ${name || 'Customer'}, following up from Tyresoles.`);
		window.open(`https://wa.me/${formattedNum}?text=${msg}`, '_blank');
	}

	const isOnlyMeSelected = $derived(
		selectedUsers.length === 1 && selectedUsers[0] === $authStore.username
	);
</script>

<div class="min-h-screen bg-background text-foreground pb-24 pt-2 px-3 max-w-7xl mx-auto space-y-6">
	<!-- Page Header -->
	<PageHeading
		title="Call Logs"
		description="View, search, and filter call history, active reminders, and contacts allocated to you"
		icon="phone-call"
		backHref="/menu"
		backLabel="Back to Menu"
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<a
					href="/crm-masters?tab=ACTIVITY_OUTCOME"
					target="_blank"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-2xl border border-primary/20 bg-card text-xs font-medium text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors shadow-2xs"
					title="Configure Call Outcomes and Pipeline in CRM Masters"
				>
					<Icon name="database" class="size-3.5 text-primary" />
					<span class="hidden sm:inline">Manage Outcomes</span>
				</a>

				<Button
					variant="outline"
					size="sm"
					class="gap-2 rounded-2xl border-primary/20 hover:bg-primary/10"
					onclick={() => {
						showContactFinderModal = true;
					}}
				>
					<Icon name="user-check" class="size-4 text-primary" />
					<span class="hidden sm:inline">Find Allocated Contacts</span>
					{#if allocatedContacts.length > 0}
						<Badge variant="secondary" class="ml-1 rounded-xl px-1.5 py-0 text-[10px] bg-primary/20 text-primary">
							{allocatedContacts.length}
						</Badge>
					{/if}
				</Button>

				<Button
					variant="default"
					size="sm"
					class="gap-2 rounded-2xl shadow-md"
					onclick={() => loadCallLogs()}
					disabled={isLoading}
				>
					<Icon name="refresh-cw" class={`size-4 ${isLoading ? 'animate-spin' : ''}`} />
					<span class="hidden sm:inline">Refresh</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- Metrics Overview Cards (Clickable for Instant Filter) -->
	<div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
		<Card class="border-border/60 bg-card/60 backdrop-blur-sm rounded-3xl shadow-sm hover:border-primary/30 transition-all">
			<CardContent class="p-4 flex items-center justify-between">
				<div>
					<p class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Total Calls</p>
					<h3 class="text-2xl font-black tracking-tight mt-1">{totalLogsCount}</h3>
				</div>
				<div class="p-3 rounded-2xl bg-primary/10 text-primary">
					<Icon name="phone-call" class="size-6" />
				</div>
			</CardContent>
		</Card>

		<Card class="border-border/60 bg-card/60 backdrop-blur-sm rounded-3xl shadow-sm hover:border-emerald-500/30 transition-all">
			<CardContent class="p-4 flex items-center justify-between">
				<div>
					<p class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Interested / Done</p>
					<h3 class="text-2xl font-black tracking-tight mt-1 text-emerald-600 dark:text-emerald-400">{interestedCount}</h3>
				</div>
				<div class="p-3 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:text-emerald-400">
					<Icon name="circle-check" class="size-6" />
				</div>
			</CardContent>
		</Card>

		<!-- Callbacks & Active Reminders Card (Clickable) -->
		<Card
			onclick={() => {
				remindersOnly = !remindersOnly;
				currentPage = 1;
			}}
			class={`border-border/60 backdrop-blur-sm rounded-3xl shadow-sm cursor-pointer transition-all ${
				remindersOnly ? 'bg-amber-500/15 border-amber-500 ring-2 ring-amber-500/30' : 'bg-card/60 hover:border-amber-500/40'
			}`}
		>
			<CardContent class="p-4 flex items-center justify-between">
				<div>
					<p class="text-xs font-semibold text-amber-700 dark:text-amber-300 uppercase tracking-wider flex items-center gap-1">
						<span>Callbacks / Reminders</span>
						{#if remindersMap.size > 0}
							<Badge variant="secondary" class="rounded-xl px-1.5 py-0 text-[10px] bg-amber-500/20 text-amber-600 dark:text-amber-400">
								{remindersMap.size} Active
							</Badge>
						{/if}
					</p>
					<h3 class="text-2xl font-black tracking-tight mt-1 text-amber-600 dark:text-amber-400">{callbacksCount}</h3>
				</div>
				<div class="p-3 rounded-2xl bg-amber-500/10 text-amber-600 dark:text-amber-400">
					<Icon name="bell" class={`size-6 ${remindersMap.size > 0 ? 'animate-bounce' : ''}`} />
				</div>
			</CardContent>
		</Card>

		<Card class="border-border/60 bg-card/60 backdrop-blur-sm rounded-3xl shadow-sm hover:border-sky-500/30 transition-all">
			<CardContent class="p-4 flex items-center justify-between">
				<div>
					<p class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Contacts Reached</p>
					<h3 class="text-2xl font-black tracking-tight mt-1 text-sky-600 dark:text-sky-400">{uniqueContactsCount}</h3>
				</div>
				<div class="p-3 rounded-2xl bg-sky-500/10 text-sky-600 dark:text-sky-400">
					<Icon name="users" class="size-6" />
				</div>
			</CardContent>
		</Card>
	</div>

	<!-- Filter Control Panel -->
	<Card class="border-border/80 bg-card shadow-md rounded-3xl p-4 space-y-4">
		<!-- Top Bar: Search, Multi-User Filter Button, Reminders Toggle, Outcome Dropdown -->
		<div class="flex flex-wrap items-center justify-between gap-3">
			<!-- Text Search Input -->
			<div class="relative flex-1 min-w-[240px]">
				<Icon name="search" class="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
				<Input
					type="text"
					placeholder="Search by contact name, company, mobile, notes..."
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

			<!-- Filter Buttons -->
			<div class="flex flex-wrap items-center gap-2">
				<!-- Dedicated Active Reminders Only Filter Button -->
				<Button
					variant="outline"
					size="sm"
					class={`rounded-2xl gap-2 text-xs font-bold transition-all border ${
						remindersOnly
							? 'bg-amber-500 text-white border-amber-600 shadow-sm'
							: 'bg-background border-border/60 text-muted-foreground hover:bg-muted'
					}`}
					onclick={() => {
						remindersOnly = !remindersOnly;
						currentPage = 1;
					}}
				>
					<Icon name="bell" class={`size-4 ${remindersOnly ? 'animate-bounce' : 'text-amber-500'}`} />
					<span>Active Reminders</span>
					{#if remindersMap.size > 0}
						<Badge variant="secondary" class={`rounded-xl px-1.5 py-0 text-[10px] ${remindersOnly ? 'bg-white/20 text-white' : 'bg-amber-500/20 text-amber-600 dark:text-amber-300'}`}>
							{remindersMap.size}
						</Badge>
					{/if}
				</Button>

				<!-- Multi-Select User Filter Button -->
				<Button
					variant="outline"
					size="sm"
					class={`rounded-2xl gap-2 text-xs font-bold transition-all border ${
						selectedUsers.length > 0
							? 'bg-primary/10 border-primary/40 text-primary shadow-sm'
							: 'bg-background border-border/60 text-muted-foreground'
					}`}
					onclick={() => (showUserFilterModal = true)}
				>
					<Icon name="users" class="size-4 text-primary" />
					<span>
						{#if isOnlyMeSelected}
							Logged In User (Only Me)
						{:else if selectedUsers.length === 0}
							All Users
						{:else if selectedUsers.length === 1}
							User: {selectedUsers[0]}
						{:else}
							Users ({selectedUsers.length})
						{/if}
					</span>
					<Badge variant="secondary" class="rounded-xl px-1.5 py-0 text-[10px]">
						{selectedUsers.length > 0 ? selectedUsers.length : 'All'}
					</Badge>
				</Button>

				<!-- Find Allocated Contact Button -->
				<Button
					variant="outline"
					size="sm"
					class="rounded-2xl border-dashed border-primary/40 gap-2 text-xs font-semibold"
					onclick={() => (showContactFinderModal = true)}
				>
					<Icon name="search" class="size-3.5 text-primary" />
					<span>Find Contact ({allocatedContacts.length})</span>
				</Button>

				<!-- Outcome Filter Dropdown -->
				<select
					bind:value={selectedOutcome}
					onchange={() => {
						if (selectedOutcome === 'REMINDERS_ONLY') {
							remindersOnly = true;
						}
						currentPage = 1;
						loadCallLogs();
					}}
					class="px-3 py-2 rounded-2xl border border-border/60 bg-background text-xs font-semibold focus:ring-2 focus:ring-primary/20 outline-none"
				>
					<option value="ALL">All Outcomes</option>
					<option value="REMINDERS_ONLY">🔔 Active Reminders ({remindersMap.size})</option>
					{#each outcomes as item}
						<option value={item.name}>{item.name}</option>
					{/each}
				</select>
			</div>
		</div>

		<!-- Active Filters Pills -->
		{#if selectedUsers.length > 0 || selectedAllocatedContact || remindersOnly}
			<div class="flex flex-wrap items-center gap-2 pt-1">
				<span class="text-xs font-semibold text-muted-foreground">Active Filters:</span>

				<!-- Active Reminders Filter Pill -->
				{#if remindersOnly}
					<div class="flex items-center gap-1.5 px-2.5 py-1 rounded-xl bg-amber-500/15 border border-amber-500/40 text-xs font-bold text-amber-600 dark:text-amber-400 shadow-sm">
						<Icon name="bell" class="size-3 animate-pulse" />
						<span>Active Reminders Only ({remindersMap.size})</span>
						<button
							onclick={() => {
								remindersOnly = false;
								if (selectedOutcome === 'REMINDERS_ONLY') selectedOutcome = 'ALL';
								currentPage = 1;
							}}
							class="hover:text-destructive transition-colors ml-0.5"
							title="Clear active reminders filter"
						>
							<Icon name="x" class="size-3" />
						</button>
					</div>
				{/if}

				<!-- Quick reset to Only Me -->
				{#if !isOnlyMeSelected}
					<button
						onclick={selectOnlyMe}
						class="px-2.5 py-1 rounded-xl bg-primary/10 border border-primary/30 text-[11px] font-bold text-primary hover:bg-primary/20 transition-colors"
					>
						Reset to Only Me
					</button>
				{/if}

				<!-- User Pills -->
				{#each selectedUsers as usr (usr)}
					<div class="flex items-center gap-1.5 px-2.5 py-1 rounded-xl bg-muted border border-border/60 text-xs font-semibold text-foreground">
						<Icon name="user-check" class="size-3 text-primary" />
						<span>{usr === $authStore.username ? `${usr} (You)` : usr}</span>
						<button
							onclick={() => removeUserPill(usr)}
							class="hover:text-destructive transition-colors ml-0.5"
							title="Remove user filter"
						>
							<Icon name="x" class="size-3" />
						</button>
					</div>
				{/each}

				<!-- Selected Allocated Contact Filter Pill -->
				{#if selectedAllocatedContact}
					<div class="flex items-center gap-1.5 px-2.5 py-1 rounded-xl bg-sky-500/10 border border-sky-500/30 text-xs font-bold text-sky-600 dark:text-sky-400">
						<Icon name="user" class="size-3" />
						<span>Contact: {selectedAllocatedContact.contact?.fullName || 'Selected'}</span>
						<button
							onclick={clearSelectedContact}
							class="hover:text-destructive transition-colors ml-0.5"
							title="Clear contact filter"
						>
							<Icon name="x" class="size-3" />
						</button>
					</div>
				{/if}
			</div>
		{/if}

		<!-- Date Presets & Custom Range -->
		<div class="flex flex-wrap items-center justify-between gap-3 pt-2 border-t border-border/40">
			<!-- Date Presets -->
			<div class="flex flex-wrap items-center gap-1.5">
				<span class="text-xs font-semibold text-muted-foreground mr-1">Date:</span>
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

			<!-- VenUI Date Range Picker -->
			<div class="flex items-center gap-2">
				<div class="w-full sm:w-[260px]">
					<DatePicker
						mode="range"
						bind:value={dateRange}
						placeholder="Select date range"
						presets={callLogPresets}
						onValueChange={handleDateRangeChange}
					/>
				</div>
				{#if dateRange?.start || dateRange?.end}
					<Button
						variant="ghost"
						size="sm"
						class="h-9 px-2.5 rounded-2xl text-xs text-muted-foreground hover:text-foreground"
						onclick={() => setPreset('all')}
						title="Clear date filter"
					>
						<Icon name="x" class="size-3.5 mr-1" />
						<span>Clear</span>
					</Button>
				{/if}
			</div>
		</div>
	</Card>

	<!-- Call Logs Feed Table -->
	<Card class="border-border/80 bg-card shadow-lg rounded-3xl overflow-hidden">
		{#if isLoading}
			<div class="flex flex-col items-center justify-center p-16 space-y-4">
				<Loader2 class="size-8 animate-spin text-primary" />
				<p class="text-sm font-semibold text-muted-foreground">Loading call logs & active reminders...</p>
			</div>
		{:else if filteredCallLogs.length === 0}
			<div class="flex flex-col items-center justify-center p-16 space-y-3 text-center">
				<div class="p-4 rounded-3xl bg-muted/60 text-muted-foreground">
					<Icon name="phone-off" class="size-8 opacity-60" />
				</div>
				<h3 class="text-lg font-bold">No Records Found</h3>
				<p class="text-sm text-muted-foreground max-w-md">
					{#if remindersOnly}
						No active reminders match your current user or date filter selection.
					{:else}
						No call logs match your current user selection, date range, or filter criteria.
					{/if}
				</p>
				<div class="flex flex-wrap items-center gap-2 mt-2">
					{#if remindersOnly}
						<Button
							variant="outline"
							size="sm"
							class="rounded-2xl"
							onclick={() => (remindersOnly = false)}
						>
							Show All Call Logs
						</Button>
					{/if}
					<Button
						variant="outline"
						size="sm"
						class="rounded-2xl"
						onclick={selectOnlyMe}
					>
						Show My Call Logs
					</Button>
					<Button
						variant="outline"
						size="sm"
						class="rounded-2xl"
						onclick={() => setPreset('all')}
					>
						Clear Date Filters
					</Button>
				</div>
			</div>
		{:else}
			<div class="overflow-x-auto">
				<table class="w-full text-left text-sm border-collapse">
					<thead>
						<tr class="border-b border-border/60 bg-muted/40 text-xs uppercase font-bold text-muted-foreground tracking-wider">
							<th class="py-3.5 px-4">Date & Time</th>
							<th class="py-3.5 px-4">Contact Details</th>
							<th class="py-3.5 px-4">Outcome</th>
							<th class="py-3.5 px-4">Notes Logged</th>
							<th class="py-3.5 px-4">Logged By</th>
							<th class="py-3.5 px-4 text-right">Actions</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border/40">
						{#each pagedCallLogs as log (log.id)}
							{@const outcomeStyle = getOutcomeVariant(log.outcome)}
							{@const activeReminder = remindersMap.get(log.contactId)}
							<tr
								onclick={() => {
									if (['Interested', 'Order Placed'].includes(log.outcome)) {
										openCallLogDetails(log);
									}
								}}
								class={`transition-colors ${
									['Interested', 'Order Placed'].includes(log.outcome)
										? 'hover:bg-muted/50 cursor-pointer'
										: 'hover:bg-muted/20'
								} group`}
								title={['Interested', 'Order Placed'].includes(log.outcome) ? 'Click to link sales invoices' : ''}
							>
								<!-- Date -->
								<td class="py-3.5 px-4 whitespace-nowrap">
									<div class="flex items-center gap-2">
										<Icon name="calendar" class="size-4 text-muted-foreground/70" />
										<span class="font-semibold text-foreground/90">{formatDate(log.callDate)}</span>
									</div>
								</td>

								<!-- Contact -->
								<td class="py-3.5 px-4">
									{#if log.contact}
										<div class="space-y-1">
											<div class="flex items-center gap-2">
												<button
													onclick={(e) => {
														e.stopPropagation();
														goto(`/crm-calling?contactId=${log.contactId}`);
													}}
													class="font-bold text-foreground hover:text-primary transition-colors text-left block"
													title="Go to calling workspace"
												>
													{log.contact.fullName}
												</button>

												<!-- Active Reminder Badge / Icon -->
												{#if activeReminder}
													<button
														onclick={(e) => {
															e.stopPropagation();
															openReminderDetails(activeReminder);
														}}
														class="inline-flex items-center gap-1 px-2 py-0.5 rounded-xl border bg-amber-500/15 border-amber-500/40 text-amber-600 dark:text-amber-400 font-bold text-[11px] hover:bg-amber-500/25 transition-all shadow-sm group/rem"
														title={`Active Reminder: ${formatDate(activeReminder.reminderDate)} - Click for details`}
													>
														<Icon name="bell" class="size-3 animate-pulse text-amber-600 dark:text-amber-400" />
														<span>Reminder</span>
													</button>
												{/if}
											</div>

											<div class="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
												{#if log.contact.companyName}
													<span>{log.contact.companyName}</span>
												{/if}
												{#if log.contact.city}
													<span class="inline-flex items-center gap-0.5">
														<Icon name="map-pin" class="size-3" />
														{log.contact.city}
													</span>
												{/if}
												{#if log.contact.mobileNo}
													<span class="font-mono text-[11px] text-muted-foreground/80">{log.contact.mobileNo}</span>
												{/if}
											</div>
										</div>
									{:else}
										<span class="text-muted-foreground italic text-xs">Contact ID: {log.contactId.slice(0, 8)}...</span>
									{/if}
								</td>

								<!-- Outcome -->
								<td class="py-3.5 px-4 whitespace-nowrap">
									<div class={`inline-flex items-center gap-1.5 px-3 py-1 rounded-2xl border text-xs font-bold ${outcomeStyle.bg}`}>
										<Icon name={outcomeStyle.icon} class="size-3.5" />
										<span>{outcomeStyle.text}</span>
									</div>
								</td>

								<!-- Notes -->
								<td class="py-3.5 px-4 max-w-xs">
									{#if log.notes}
										<p class="text-xs text-foreground/80 line-clamp-2 leading-relaxed" title={log.notes}>
											{log.notes}
										</p>
									{:else}
										<span class="text-muted-foreground/50 text-xs italic">No notes</span>
									{/if}
									{#if log.invoiceNos}
										{@const invList = log.invoiceNos.split(',').map((s) => s.trim()).filter(Boolean)}
										{#if invList.length > 0}
											<div class="mt-1 flex flex-wrap items-center gap-1">
												<Badge variant="outline" class="gap-1 font-mono text-[10px] text-emerald-700 dark:text-emerald-300 border-emerald-500/30 py-0 px-1.5 rounded-lg bg-emerald-500/10">
													<Icon name="receipt" class="size-2.5" />
													<span>{invList.length} {invList.length === 1 ? 'Invoice' : 'Invoices'}</span>
													{#if log.invoiceAmount != null && log.invoiceAmount > 0}
														<span>• {formatCurrency(log.invoiceAmount)}</span>
													{/if}
													{#if log.tyreQuantity != null && log.tyreQuantity > 0}
														<span>• {log.tyreQuantity} {log.tyreQuantity === 1 ? 'Tyre' : 'Tyres'}</span>
													{/if}
												</Badge>
											</div>
										{/if}
									{/if}
								</td>

								<!-- Logged By -->
								<td class="py-3.5 px-4 whitespace-nowrap text-xs">
									<div class="flex items-center gap-1.5 font-medium text-muted-foreground">
										<Icon name="user-check" class="size-3.5 text-primary/70" />
										<span>{log.createdBy || 'System'}</span>
									</div>
								</td>

								<!-- Actions -->
								<td class="py-3.5 px-4 whitespace-nowrap text-right">
									<div class="flex items-center justify-end gap-1">
										<!-- Active Reminder Action Button -->
										{#if activeReminder}
											<button
												onclick={(e) => {
													e.stopPropagation();
													openReminderDetails(activeReminder);
												}}
												class="p-2 rounded-xl bg-amber-500/15 text-amber-600 dark:text-amber-400 hover:bg-amber-500 hover:text-white transition-all shadow-sm relative group/btn"
												title="View Active Reminder Details"
											>
												<Icon name="bell" class="size-3.5 animate-bounce" />
											</button>
										{/if}

										<!-- Call action -->
										<button
											onclick={(e) => {
												e.stopPropagation();
												makeCall(log.contact?.mobileNo);
											}}
											class="p-2 rounded-xl bg-emerald-500/10 text-emerald-600 hover:bg-emerald-500 hover:text-white transition-all shadow-sm"
											title="Call Contact"
										>
											<Icon name="phone" class="size-3.5" />
										</button>

										<!-- WhatsApp action -->
										<button
											onclick={(e) => {
												e.stopPropagation();
												sendWhatsapp(log.contact?.mobileNo, log.contact?.fullName);
											}}
											class="p-2 rounded-xl bg-emerald-500/10 text-emerald-600 hover:bg-emerald-600 hover:text-white transition-all shadow-sm"
											title="WhatsApp Message"
										>
											<Icon name="message-square" class="size-3.5" />
										</button>

										<!-- Link Sales Invoices (only for Interested & Order Placed) -->
										{#if ['Interested', 'Order Placed'].includes(log.outcome)}
											<button
												onclick={(e) => {
													e.stopPropagation();
													openCallLogDetails(log);
												}}
												class="p-2 rounded-xl bg-primary/10 text-primary hover:bg-primary hover:text-primary-foreground transition-all shadow-sm"
												title="Link Sales Invoices"
											>
												<Icon name="receipt" class="size-3.5" />
											</button>
										{/if}
									</div>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>

			<!-- Pagination Footer -->
			<div class="flex flex-wrap items-center justify-between gap-4 p-4 border-t border-border/60 bg-muted/20 text-xs">
				<div class="text-muted-foreground font-medium">
					Showing <strong>{pagedCallLogs.length}</strong> of <strong>{totalLogsCount}</strong> records
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

<!-- Modal: Filter Users (Multi-Select) -->
<Dialog.Root bind:open={showUserFilterModal}>
	<Dialog.Content class="sm:max-w-md max-h-[85vh] flex flex-col rounded-3xl p-6">
		<Dialog.Header>
			<Dialog.Title class="text-xl font-bold flex items-center gap-2">
				<Icon name="users" class="size-5 text-primary" />
				<span>Filter by Users / Agents</span>
			</Dialog.Title>
			<Dialog.Description class="text-xs text-muted-foreground">
				Select one or multiple users to view their allocated contacts and call logs
			</Dialog.Description>
		</Dialog.Header>

		<!-- Quick Action Buttons -->
		<div class="flex items-center justify-between gap-2 mt-2 pt-2 border-t border-border/40">
			<Button
				variant="secondary"
				size="sm"
				class="rounded-xl text-xs flex-1 font-bold"
				onclick={selectOnlyMe}
			>
				Only Me ({$authStore.username})
			</Button>
			<Button
				variant="outline"
				size="sm"
				class="rounded-xl text-xs font-medium"
				onclick={selectAllUsers}
			>
				Select All
			</Button>
			<Button
				variant="ghost"
				size="sm"
				class="rounded-xl text-xs font-medium"
				onclick={clearUserSelection}
			>
				Clear All
			</Button>
		</div>

		<!-- Search Input in User Modal -->
		<div class="relative mt-3">
			<Icon name="search" class="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
			<Input
				type="text"
				placeholder="Search users..."
				bind:value={userFilterSearch}
				class="pl-10 pr-4 py-2 rounded-2xl text-sm"
			/>
		</div>

		<!-- Users Checklist List -->
		<div class="flex-1 overflow-y-auto mt-3 space-y-1 pr-1 max-h-[45vh]">
			{#if isLoadingUsers}
				<div class="flex items-center justify-center p-8">
					<Loader2 class="size-6 animate-spin text-primary" />
				</div>
			{:else if filteredUsersModal.length === 0}
				<div class="text-center p-8 text-muted-foreground text-sm">
					No users found.
				</div>
			{:else}
				{#each filteredUsersModal as user (user)}
					{@const isSelected = selectedUsers.includes(user)}
					{@const isCurrentUser = user === $authStore.username}
					<button
						type="button"
						onclick={() => toggleUserSelection(user)}
						class={`w-full text-left px-3.5 py-3 rounded-2xl border transition-all flex items-center justify-between gap-3 ${
							isSelected
								? 'bg-primary/10 border-primary text-primary font-bold shadow-sm'
								: 'bg-card border-border/50 hover:bg-muted/40 text-foreground font-medium'
						}`}
					>
						<div class="flex items-center gap-2.5">
							<div class={`size-4 rounded-md border flex items-center justify-center transition-colors ${
								isSelected ? 'bg-primary border-primary text-primary-foreground' : 'border-border bg-background'
							}`}>
								{#if isSelected}
									<Icon name="check" class="size-3" />
								{/if}
							</div>
							<span class="text-sm">{user}</span>
							{#if isCurrentUser}
								<Badge variant="secondary" class="rounded-xl px-1.5 py-0 text-[10px] bg-primary/20 text-primary">
									You
								</Badge>
							{/if}
						</div>
					</button>
				{/each}
			{/if}
		</div>

		<Dialog.Footer class="mt-4 pt-3 border-t border-border/50">
			<Button
				variant="default"
				size="sm"
				class="w-full rounded-2xl font-bold"
				onclick={() => (showUserFilterModal = false)}
			>
				Apply Filter ({selectedUsers.length} Selected)
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Modal: Find Contacts Allocated to Selected Users -->
<Dialog.Root bind:open={showContactFinderModal}>
	<Dialog.Content class="sm:max-w-xl max-h-[85vh] flex flex-col rounded-3xl p-6">
		<Dialog.Header>
			<Dialog.Title class="text-xl font-bold flex items-center gap-2">
				<Icon name="user-check" class="size-5 text-primary" />
				<span>Contacts Allocated to Selected Users</span>
			</Dialog.Title>
			<Dialog.Description class="text-xs text-muted-foreground">
				Select an allocated contact to filter all their recorded call logs
			</Dialog.Description>
		</Dialog.Header>

		<!-- Search Input in Modal -->
		<div class="relative mt-3">
			<Icon name="search" class="absolute left-3.5 top-1/2 -translate-y-1/2 size-4 text-muted-foreground" />
			<Input
				type="text"
				placeholder="Search allocated contacts by name, company, city, phone..."
				bind:value={contactFinderSearch}
				class="pl-10 pr-4 py-2 rounded-2xl text-sm"
			/>
		</div>

		<!-- Allocated Contacts List -->
		<div class="flex-1 overflow-y-auto mt-4 space-y-2 pr-1 max-h-[50vh]">
			{#if isLoadingAllocated}
				<div class="flex items-center justify-center p-8">
					<Loader2 class="size-6 animate-spin text-primary" />
				</div>
			{:else if filteredAllocatedContactsModal.length === 0}
				<div class="text-center p-8 text-muted-foreground text-sm">
					No allocated contacts found for the selected users.
				</div>
			{:else}
				{#each filteredAllocatedContactsModal as ac (ac.id)}
					{@const c = ac.contact}
					{#if c}
						<button
							onclick={() => selectAllocatedContact(ac)}
							class={`w-full text-left p-3.5 rounded-2xl border transition-all flex items-center justify-between gap-3 group ${
								selectedAllocatedContact?.id === ac.id
									? 'bg-primary/10 border-primary shadow-sm'
									: 'bg-card border-border/50 hover:border-primary/40 hover:bg-muted/30'
							}`}
						>
							<div class="space-y-0.5">
								<h4 class="font-bold text-sm text-foreground group-hover:text-primary transition-colors">
									{c.fullName}
								</h4>
								<div class="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
									{#if c.companyName}
										<span>{c.companyName}</span>
									{/if}
									{#if c.city}
										<span>• {c.city}</span>
									{/if}
									{#if c.mobileNo}
										<span class="font-mono text-[11px]">• {c.mobileNo}</span>
									{/if}
									<span class="text-[11px] text-primary/80">• Assigned to: {ac.agentUsername}</span>
								</div>
							</div>

							<div class="flex items-center gap-2">
								<Badge variant="outline" class="rounded-xl px-2 py-0.5 text-[10px] font-semibold">
									{ac.callCount} calls
								</Badge>
								<Icon name="chevron-right" class="size-4 text-muted-foreground group-hover:text-primary transition-colors" />
							</div>
						</button>
					{/if}
				{/each}
			{/if}
		</div>

		<Dialog.Footer class="mt-4 pt-3 border-t border-border/50">
			<Button
				variant="ghost"
				size="sm"
				class="rounded-2xl text-xs"
				onclick={() => {
					clearSelectedContact();
					showContactFinderModal = false;
				}}
			>
				Show All Logs for Selected Users
			</Button>
			<Button
				variant="outline"
				size="sm"
				class="rounded-2xl text-xs"
				onclick={() => (showContactFinderModal = false)}
			>
				Close
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>

<!-- Modal: Active Reminder Details -->
<Dialog.Root bind:open={showReminderModal}>
	<Dialog.Content class="sm:max-w-md max-h-[90dvh] sm:max-h-[85vh] p-0 overflow-hidden flex flex-col rounded-3xl border border-border shadow-2xl">
		{#if selectedReminder}
			<Dialog.Header class="px-6 py-4 border-b border-border/50 shrink-0 bg-background pr-12">
				<Dialog.Title class="text-lg font-bold flex items-center gap-2 text-amber-600 dark:text-amber-400">
					<Icon name="bell" class="size-5 text-amber-600 dark:text-amber-400 animate-pulse" />
					<span>Active Call Reminder</span>
				</Dialog.Title>
				<Dialog.Description class="text-xs text-muted-foreground">
					Follow-up reminder scheduled for this contact
				</Dialog.Description>
			</Dialog.Header>

			<div class="p-6 overflow-y-auto flex-1 min-h-0 space-y-3 text-sm custom-scrollbar">
				<!-- Contact Info -->
				<div class="p-3.5 rounded-2xl bg-amber-500/10 border border-amber-500/20 space-y-1">
					<p class="text-xs font-semibold text-amber-700 dark:text-amber-300 uppercase tracking-wider">Contact</p>
					<h4 class="font-bold text-base text-foreground">{selectedReminder.contact?.fullName || 'Contact'}</h4>
					{#if selectedReminder.contact?.companyName}
						<p class="text-xs text-muted-foreground">{selectedReminder.contact.companyName}</p>
					{/if}
					{#if selectedReminder.contact?.mobileNo}
						<p class="text-xs font-mono text-primary font-bold">{selectedReminder.contact.mobileNo}</p>
					{/if}
				</div>

				<!-- Reminder Scheduled Date & Time -->
				<div class="p-3.5 rounded-2xl bg-muted/40 border border-border/60 space-y-1">
					<p class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Scheduled Date & Time</p>
					<div class="flex items-center gap-2 font-bold text-sm text-foreground">
						<Icon name="clock" class="size-4 text-amber-600 dark:text-amber-400" />
						<span>{formatDate(selectedReminder.reminderDate)}</span>
					</div>
				</div>

				<!-- Reminder Notes -->
				<div class="p-3.5 rounded-2xl bg-muted/30 border border-border/40 space-y-1">
					<p class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Follow-up Notes</p>
					<p class="text-xs text-foreground/90 whitespace-pre-wrap leading-relaxed">
						{selectedReminder.notes || 'No specific notes recorded for this reminder.'}
					</p>
				</div>

				<!-- Created Info -->
				<div class="p-3 rounded-2xl bg-muted/20 border border-border/30 text-xs text-muted-foreground flex justify-between">
					<span>Scheduled By: <strong class="text-foreground">{selectedReminder.createdBy}</strong></span>
					<span>Set On: {formatDate(selectedReminder.createdAt)}</span>
				</div>
			</div>

			<Dialog.Footer class="px-6 py-3.5 border-t border-border/50 shrink-0 bg-muted/10 flex flex-col sm:flex-row gap-2 mt-auto">
				<Button
					variant="default"
					size="sm"
					class="w-full sm:flex-1 rounded-2xl bg-amber-600 hover:bg-amber-700 text-white font-bold gap-2"
					onclick={() => handleCompleteReminder(selectedReminder!.id)}
					disabled={isCompletingReminder}
				>
					{#if isCompletingReminder}
						<Loader2 class="size-4 animate-spin" />
					{:else}
						<Icon name="check-circle" class="size-4" />
					{/if}
					<span>Mark Complete</span>
				</Button>

				<Button
					variant="outline"
					size="sm"
					class="rounded-2xl gap-1 text-emerald-600 border-emerald-500/30 hover:bg-emerald-500/10"
					onclick={() => makeCall(selectedReminder?.contact?.mobileNo)}
				>
					<Icon name="phone" class="size-3.5" />
					<span>Call</span>
				</Button>

				<Button
					variant="outline"
					size="sm"
					class="rounded-2xl text-xs"
					onclick={() => (showReminderModal = false)}
				>
					Close
				</Button>
			</Dialog.Footer>
		{/if}
	</Dialog.Content>
</Dialog.Root>

<!-- Modal: Call Log Full Detail -->
<Dialog.Root bind:open={showDetailModal}>
	<Dialog.Content class="sm:max-w-lg max-h-[90dvh] sm:max-h-[85vh] p-0 overflow-hidden flex flex-col rounded-3xl border border-border shadow-2xl">
		{#if selectedCallLog && ['Interested', 'Order Placed'].includes(selectedCallLog.outcome)}
			{@const outcomeStyle = getOutcomeVariant(selectedCallLog.outcome)}
			<Dialog.Header class="px-6 py-4 border-b border-border/50 shrink-0 bg-background pr-12">
				<Dialog.Title class="text-lg font-bold flex items-center gap-2">
					<Icon name="receipt" class="size-5 text-primary" />
					<span>Link Sales Invoices</span>
				</Dialog.Title>
				<Dialog.Description class="sr-only">Call log details, notes and linked sales invoices</Dialog.Description>
			</Dialog.Header>

			<div class="p-6 overflow-y-auto flex-1 min-h-0 space-y-3 text-sm custom-scrollbar">
				<div class="p-3.5 rounded-2xl bg-muted/40 border border-border/60 space-y-1">
					<p class="text-xs font-semibold text-muted-foreground uppercase">Contact</p>
					<h4 class="font-bold text-base">{selectedCallLog.contact?.fullName || 'Unknown Contact'}</h4>
					{#if selectedCallLog.contact?.companyName}
						<p class="text-xs text-muted-foreground">{selectedCallLog.contact.companyName}</p>
					{/if}
					{#if selectedCallLog.contact?.mobileNo}
						<p class="text-xs font-mono text-primary">{selectedCallLog.contact.mobileNo}</p>
					{/if}
				</div>

				<div class="grid grid-cols-2 gap-3">
					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="text-[11px] font-semibold text-muted-foreground uppercase">Call Date</p>
						<p class="font-semibold text-xs mt-1">{formatDate(selectedCallLog.callDate)}</p>
					</div>

					<div class="p-3 rounded-2xl bg-muted/30 border border-border/40">
						<p class="text-[11px] font-semibold text-muted-foreground uppercase">Outcome</p>
						<div class={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-xl border text-xs font-bold mt-1 ${outcomeStyle.bg}`}>
							<span>{selectedCallLog.outcome}</span>
						</div>
					</div>
				</div>

				<div class="p-3.5 rounded-2xl bg-muted/30 border border-border/40 space-y-1">
					<p class="text-[11px] font-semibold text-muted-foreground uppercase">Call Notes</p>
					<p class="text-xs text-foreground/90 whitespace-pre-wrap leading-relaxed">
						{selectedCallLog.notes || 'No notes were recorded for this call log.'}
					</p>
				</div>

				<!-- Sales Invoice Selection (Last 30 Days - BEL) -->
				<div class="p-3.5 rounded-2xl bg-muted/30 border border-border/40 space-y-2">
					<div class="flex items-center justify-between">
						<span class="text-[11px] font-semibold text-muted-foreground uppercase flex items-center gap-1.5">
							<Icon name="file-text" class="size-3.5 text-primary" />
							<span>Sales Invoices (Last 30 Days &bull; BEL)</span>
						</span>
						<div class="flex items-center gap-1.5">
							{#if isLoadingInvoices}
								<div class="flex items-center gap-1 text-[11px] text-muted-foreground">
									<Loader2 class="size-3 animate-spin text-primary" />
									<span>Loading...</span>
								</div>
							{:else if salesInvoices.length > 0}
								{#if selectedInvoiceNos.length > 0}
									<Badge variant="default" class="rounded-xl px-2 py-0 text-[10px] font-semibold bg-primary text-primary-foreground">
										{selectedInvoiceNos.length} selected
									</Badge>
								{/if}
								<Badge variant="secondary" class="rounded-xl px-1.5 py-0 text-[10px] font-normal">
									{salesInvoices.length} invoices
								</Badge>
							{/if}
							{#if hasInvoiceChanges}
								<Button
									variant="default"
									size="sm"
									class="h-6 px-2.5 text-[11px] rounded-lg gap-1 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold shadow-xs"
									onclick={saveCallLogInvoices}
									disabled={isSavingInvoices}
								>
									{#if isSavingInvoices}
										<Loader2 class="size-3 animate-spin" />
									{:else}
										<Icon name="check" class="size-3" />
									{/if}
									<span>Save</span>
								</Button>
							{/if}
						</div>
					</div>

					<Select
						options={salesInvoices}
						bind:value={selectedInvoiceNos}
						multiple={true}
						showSelectAll={false}
						columns={invoiceColumns}
						valueKey="no"
						labelKey="no"
						placeholder={isLoadingInvoices ? "Loading BEL invoices..." : "Select sales invoices..."}
						searchPlaceholder="Search invoice no, customer, date..."
						class="w-full rounded-xl bg-background"
						contentClass="w-[calc(100vw-2rem)] sm:w-[480px]"
						clearable={true}
						disabled={isLoadingInvoices}
					/>

					{#if selectedInvoices.length > 0}
						<div class="space-y-1.5 pt-1">
							<div class="flex items-center justify-between px-1 text-[11px] text-muted-foreground font-semibold">
								<span>Selected ({selectedInvoices.length})</span>
								<div class="flex items-center gap-2 font-mono">
									{#if totalSelectedInvoicesTyres > 0}
										<span class="text-amber-600 dark:text-amber-400 font-bold">
											{totalSelectedInvoicesTyres} {totalSelectedInvoicesTyres === 1 ? 'Tyre' : 'Tyres'}
										</span>
										<span>&bull;</span>
									{/if}
									<span class="text-emerald-600 dark:text-emerald-400 font-bold">
										Total: {formatCurrency(totalSelectedInvoicesAmount)}
									</span>
								</div>
							</div>
							<div class="max-h-[160px] overflow-y-auto space-y-1.5 pr-0.5 custom-scrollbar">
								{#each selectedInvoices as inv (inv.no)}
									<div class="p-2.5 rounded-xl bg-background/90 border border-border/60 text-xs flex items-center justify-between gap-2 shadow-xs group">
										<div class="min-w-0 flex-1 space-y-0.5">
											<div class="flex items-center justify-between gap-2">
												<span class="font-mono font-bold text-foreground flex items-center gap-1 text-xs">
													<Icon name="receipt" class="size-3 text-primary" />
													{inv.no}
												</span>
												<div class="flex items-center gap-2 font-mono text-xs">
													{#if inv.quantity != null && inv.quantity > 0}
														<span class="text-amber-600 dark:text-amber-400 font-semibold">
															{inv.quantity} {inv.quantity === 1 ? 'tyre' : 'tyres'}
														</span>
													{/if}
													<span class="font-bold text-emerald-600 dark:text-emerald-400">
														{formatCurrency(inv.amount)}
													</span>
												</div>
											</div>
											<div class="flex items-center justify-between text-muted-foreground text-[11px]">
												<span class="truncate max-w-[220px]" title={inv.name}>{inv.name}</span>
												<span>{formatInvoiceDate(inv.date)}</span>
											</div>
										</div>
										<button
											type="button"
											onclick={() => removeSelectedInvoice(inv.no)}
											class="p-1 rounded-lg text-muted-foreground hover:text-destructive hover:bg-destructive/10 transition-colors shrink-0"
											title="Remove invoice"
										>
											<Icon name="x" class="size-3.5" />
										</button>
									</div>
								{/each}
							</div>
						</div>
					{/if}
				</div>

				<div class="p-3 rounded-2xl bg-muted/20 border border-border/30 text-xs text-muted-foreground flex justify-between">
					<span>Logged By: <strong class="text-foreground">{selectedCallLog.createdBy}</strong></span>
				</div>
			</div>

			<Dialog.Footer class="px-6 py-3.5 border-t border-border/50 shrink-0 bg-muted/10 flex flex-col sm:flex-row gap-2 mt-auto">
				{#if hasInvoiceChanges}
					<Button
						variant="default"
						size="sm"
						class="w-full sm:flex-1 rounded-2xl bg-emerald-600 hover:bg-emerald-700 text-white font-bold gap-2 shadow-sm"
						onclick={saveCallLogInvoices}
						disabled={isSavingInvoices}
					>
						{#if isSavingInvoices}
							<Loader2 class="size-4 animate-spin" />
						{:else}
							<Icon name="check" class="size-4" />
						{/if}
						<span>Save Invoices ({selectedInvoiceNos.length})</span>
					</Button>
				{/if}

				<Button
					variant={hasInvoiceChanges ? "outline" : "default"}
					size="sm"
					class="w-full sm:w-auto rounded-2xl px-5"
					onclick={() => (showDetailModal = false)}
				>
					Close
				</Button>
			</Dialog.Footer>
		{/if}
	</Dialog.Content>
</Dialog.Root>

<style>
	.custom-scrollbar::-webkit-scrollbar {
		width: 6px;
	}
	.custom-scrollbar::-webkit-scrollbar-track {
		background: transparent;
	}
	.custom-scrollbar::-webkit-scrollbar-thumb {
		background-color: hsl(var(--muted-foreground) / 0.3);
		border-radius: 20px;
	}
</style>
