<script lang="ts">
	import { onMount } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Switch } from '$lib/components/ui/switch';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { DatePicker } from '$lib/components/venUI/date-picker';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import HistoryTimeline from '../../../crm-calling/components/HistoryTimeline.svelte';
	import {
		GetCrmCallLogsDocument,
		GetCrmCallRemindersDocument,
		LogCrmCallDocument,
		UndoCrmCallDocument,
		UpdateCrmCallLogNotesDocument,
		CompleteCrmReminderDocument,
		GetSalesUsersDocument,
		GetCrmMasterItemsDocument,
		type CallLog,
		type CallReminder
	} from '../../../crm-calling/queries';

	let {
		contactId,
		contact,
		callLogsCount = $bindable(0),
		onCallLogged
	}: {
		contactId: string;
		contact: any;
		callLogsCount?: number;
		onCallLogged?: (data: { outcome: string; notes?: string | null; salesUserId?: string | null }) => void;
	} = $props();

	// Call Logger state
	let outcome = $state('Answered');
	let salesUserId = $state('');
	let notes = $state('');
	let scheduleFollowUp = $state(false);
	let followUpDate = $state('');
	let followUpNotes = $state('');
	let isSavingLog = $state(false);

	let loadingOutcomes = $state(true);
	let loadingSalesUsers = $state(true);
	let outcomes = $state<{ value: string; label: string; isPositive: boolean }[]>([
		{ value: 'Answered', label: 'Answered', isPositive: true }
	]);
	let salesUsers = $state<{ value: string; label: string }[]>([]);

	// Call History & Reminders state
	let callLogs = $state<CallLog[]>([]);
	let reminders = $state<CallReminder[]>([]);
	let loadingHistory = $state(true);
	let isUndoingLog = $state<string | null>(null);
	let completingReminderId = $state<string | null>(null);
	let salesUsersList = $state<{ userName: string; fullName: string }[]>([]);
	let activeSubTab = $state<'history' | 'reminders'>('history');

	async function loadHistory() {
		loadingHistory = true;
		try {
			const [logsRes, remRes] = await Promise.all([
				graphqlQuery<any>(GetCrmCallLogsDocument, {
					variables: { contactId },
					skipCache: true
				}),
				graphqlQuery<any>(GetCrmCallRemindersDocument, {
					variables: { contactId, includeCompleted: true },
					skipCache: true
				})
			]);

			if (logsRes.data?.crmCallLogs) {
				callLogs = logsRes.data.crmCallLogs;
				callLogsCount = callLogs.length;
			}
			if (remRes.data?.crmCallReminders) {
				reminders = remRes.data.crmCallReminders;
			}
		} catch (err) {
			console.error('Failed to load contact call history', err);
			toast.error('Failed to load call history.');
		} finally {
			loadingHistory = false;
		}
	}

	async function loadOutcomes() {
		loadingOutcomes = true;
		try {
			const typesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
				variables: { type: 'ACTIVITY_TYPE', where: { name: { eq: 'Phone Call' } } }
			});
			const phoneCallType = typesRes.data?.crmMasterItems?.[0];
			if (phoneCallType?.id) {
				const outcomesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
					variables: { type: 'ACTIVITY_OUTCOME', where: { parentId: { eq: phoneCallType.id } } }
				});
				if (outcomesRes.data?.crmMasterItems?.length) {
					outcomes = outcomesRes.data.crmMasterItems.map((o: any) => ({
						value: o.name,
						label: o.name,
						isPositive: o.isPositive ?? true
					}));
					if (outcomes.length > 0 && !outcomes.find(o => o.value === outcome)) {
						outcome = outcomes[0].value;
					}
				}
			}
		} catch (e) {
			console.error('Failed to load activity outcomes', e);
		} finally {
			loadingOutcomes = false;
		}
	}

	async function loadSalesUsers() {
		loadingSalesUsers = true;
		try {
			const usersRes = await graphqlQuery<any>(GetSalesUsersDocument, {
				variables: {
					where: {
						and: [
							{ state: { eq: 0 } },
							{ userType: { eq: 'SALES' } }
						]
					},
					take: 200,
					respCenter: contact?.respCenter?.trim() || null
				}
			});
			if (usersRes.data?.users?.items) {
				salesUsers = usersRes.data.users.items.map((u: any) => ({
					value: u.userName,
					label: u.fullName ? `${u.fullName} (${u.userName})` : u.userName
				}));
			}
		} catch (e) {
			console.error('Failed to load sales users', e);
		} finally {
			loadingSalesUsers = false;
		}
	}

	async function loadSalesUsersList() {
		try {
			const res = await graphqlQuery<any>(GetSalesUsersDocument, {
				variables: {
					where: { userType: { eq: 'SALES' } },
					take: 200
				},
				silent: true
			});
			if (res.data?.users?.items) {
				salesUsersList = res.data.users.items;
			}
		} catch (e) {
			console.error('Failed to load all sales users', e);
		}
	}

	async function handleSaveCallLog() {
		if (!outcome) {
			toast.error('Please select a call outcome.');
			return;
		}

		isSavingLog = true;
		try {
			const selectedOutcome = outcomes.find(o => o.value === outcome);
			const res = await graphqlMutation<{ logCrmCall: { success: boolean; message: string } }>(LogCrmCallDocument, {
				variables: {
					contactId,
					outcome,
					notes: notes.trim() || null,
					followUpDate: scheduleFollowUp && followUpDate ? new Date(followUpDate).toISOString() : null,
					followUpNotes: scheduleFollowUp ? (followUpNotes.trim() || null) : null,
					contactIsActive: true,
					salesUserId: salesUserId || null
				}
			});

			if (res.success && res.data?.logCrmCall?.success) {
				toast.success(res.data.logCrmCall.message || 'Call log saved successfully!');
				onCallLogged?.({
					outcome,
					notes: notes.trim() || null,
					salesUserId: salesUserId || null
				});

				// Reset form
				outcome = outcomes[0]?.value || 'Answered';
				salesUserId = '';
				notes = '';
				scheduleFollowUp = false;
				followUpDate = '';
				followUpNotes = '';

				// Refresh call logs and reminders
				await loadHistory();
			} else {
				toast.error(res.data?.logCrmCall?.message || res.error || 'Failed to save call log.');
			}
		} catch (err: any) {
			console.error('Error saving call log', err);
			toast.error(err.message || 'An error occurred while saving call log.');
		} finally {
			isSavingLog = false;
		}
	}

	async function handleUndoCallLog(callLogId: string) {
		isUndoingLog = callLogId;
		try {
			const res = await graphqlMutation<{ undoCrmCall: { success: boolean; message: string } }>(UndoCrmCallDocument, {
				variables: { callLogId }
			});
			if (res.success && res.data?.undoCrmCall?.success) {
				toast.info(res.data.undoCrmCall.message || 'Call log undone successfully.');
				await loadHistory();
			} else {
				toast.error(res.data?.undoCrmCall?.message || res.error || 'Failed to undo call log.');
			}
		} catch (err: any) {
			console.error('Error undoing call log', err);
			toast.error(err.message || 'An error occurred while undoing call log.');
		} finally {
			isUndoingLog = null;
		}
	}

	async function handleUpdateCallLogNotes(callLogId: string, updatedNotes: string | null): Promise<boolean> {
		try {
			const res = await graphqlMutation<{ updateCrmCallLogNotes: { success: boolean; message: string } }>(UpdateCrmCallLogNotesDocument, {
				variables: {
					callLogId,
					notes: updatedNotes
				}
			});
			if (res.success && res.data?.updateCrmCallLogNotes?.success) {
				toast.success(res.data.updateCrmCallLogNotes.message || 'Comment updated.');
				return true;
			} else {
				toast.error(res.data?.updateCrmCallLogNotes?.message || res.error || 'Failed to update comment.');
				return false;
			}
		} catch (err: any) {
			console.error('Error updating call log comment', err);
			toast.error(err.message || 'Failed to update comment.');
			return false;
		}
	}

	async function handleCompleteReminder(reminderId: string) {
		completingReminderId = reminderId;
		try {
			const res = await graphqlMutation<{ completeCrmReminder: { success: boolean; message: string } }>(CompleteCrmReminderDocument, {
				variables: { reminderId }
			});
			if (res.success && res.data?.completeCrmReminder?.success) {
				toast.success('Reminder marked as completed.');
				await loadHistory();
			} else {
				toast.error(res.data?.completeCrmReminder?.message || res.error || 'Failed to complete reminder.');
			}
		} catch (err: any) {
			console.error('Error completing reminder', err);
			toast.error(err.message || 'Failed to complete reminder.');
		} finally {
			completingReminderId = null;
		}
	}

	onMount(() => {
		loadHistory();
		loadOutcomes();
		loadSalesUsers();
		loadSalesUsersList();
	});
</script>

<div class="space-y-6">
	<!-- Top Section: Log New Call Record Card -->
	<div class="border border-border/70 rounded-2xl bg-card shadow-xs overflow-hidden">
		<div class="px-4 sm:px-5 py-3.5 border-b border-border/70 bg-muted/20 flex items-center justify-between">
			<div class="flex items-center gap-2">
				<div class="size-7 rounded-lg bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 flex items-center justify-center">
					<Icon name="phone-call" class="size-4" />
				</div>
				<div>
					<h3 class="text-sm font-bold text-foreground">Log New Call Record</h3>
					<p class="text-[11px] text-muted-foreground">Record call outcome, conversation notes, and follow-up tasks</p>
				</div>
			</div>
			{#if contact?.respCenter}
				<span class="inline-flex items-center gap-1 text-[11px] font-semibold bg-muted px-2 py-0.5 rounded-md text-muted-foreground border border-border/50">
					<Icon name="building" class="size-3" />
					RC: {contact.respCenter}
				</span>
			{/if}
		</div>

		<div class="p-4 sm:p-5 space-y-4">
			<!-- Row 1: Outcome, Sales Person & Follow-up toggle -->
			<div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
				<div class="space-y-1">
					<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5 h-4.5">
						<span>Call Outcome <span class="text-rose-500">*</span></span>
						{#if loadingOutcomes}
							<Loader2 class="size-3 animate-spin text-primary shrink-0" />
						{/if}
					</span>
					<Select
						options={outcomes}
						bind:value={outcome}
						valueKey="value"
						labelKey="label"
						disabled={loadingOutcomes}
						placeholder="Select outcome..."
						class="rounded-xl h-9 w-full bg-background text-xs"
					/>
				</div>

				<div class="space-y-1">
					<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5 h-4.5">
						<span>Sales Person</span>
						{#if contact?.respCenter}
							<span class="text-[10px] font-normal text-muted-foreground/80 normal-case">({contact.respCenter})</span>
						{/if}
						{#if loadingSalesUsers}
							<Loader2 class="size-3 animate-spin text-primary shrink-0" />
						{/if}
					</span>
					<Select
						options={salesUsers}
						bind:value={salesUserId}
						valueKey="value"
						labelKey="label"
						disabled={loadingSalesUsers}
						clearable={true}
						placeholder={loadingSalesUsers ? "Loading sales persons..." : (salesUsers.length === 0 ? "No sales persons for this RC" : "Select sales person...")}
						class="rounded-xl h-9 w-full bg-background text-xs"
					/>
				</div>

				<div class="space-y-1">
					<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center h-4.5">
						<span>Follow-up Task</span>
					</span>
					<div class="flex items-center justify-between h-9 px-3 rounded-xl border border-border bg-muted/20">
						<label for="contact-schedule-followup" class="text-xs font-semibold cursor-pointer flex items-center gap-1.5">
							<Icon name="calendar-clock" class="size-3.5 text-primary" />
							Schedule Follow-up
						</label>
						<Switch id="contact-schedule-followup" bind:checked={scheduleFollowUp} />
					</div>
				</div>
			</div>

			<!-- Row 2: Follow-up Date/Time & Task Note (Conditional) -->
			{#if scheduleFollowUp}
				<div class="grid grid-cols-1 sm:grid-cols-2 gap-3 p-3 rounded-xl border border-primary/20 bg-primary/5 animate-in fade-in-50 duration-150">
					<div class="space-y-1">
						<span class="text-[11px] font-semibold text-muted-foreground">Reminder Date & Time</span>
						<DatePicker
							showTime
							valueType="text"
							placeholder="Select date & time..."
							bind:value={followUpDate}
						/>
					</div>
					<div class="space-y-1">
						<label for="contact-reminder-notes" class="text-[11px] font-semibold text-muted-foreground">Task Note</label>
						<Input
							id="contact-reminder-notes"
							bind:value={followUpNotes}
							placeholder="Leave blank to copy call notes..."
							class="rounded-xl h-9 text-xs"
						/>
					</div>
				</div>
			{/if}

			<!-- Row 3: Notes / Conversation Summary -->
			<div class="space-y-1">
				<label for="contact-call-notes" class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Notes / Conversation Summary</label>
				<Textarea
					id="contact-call-notes"
					bind:value={notes}
					placeholder="Type conversation summary, client requirements, or details here..."
					class="min-h-[75px] max-h-[160px] text-xs rounded-xl p-2.5 focus-visible:ring-1"
				/>
			</div>

			<!-- Row 4: Submit Button -->
			<div class="flex items-center justify-end pt-2 border-t border-border/50">
				<Button
					disabled={isSavingLog || !outcome}
					onclick={handleSaveCallLog}
					class="h-9 px-6 text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 shadow-xs cursor-pointer"
				>
					{#if isSavingLog}
						<Loader2 class="size-3.5 animate-spin shrink-0" />
					{:else}
						<Icon name="check" class="size-3.5" />
					{/if}
					Save Call Log
				</Button>
			</div>
		</div>
	</div>

	<!-- Bottom Section: Call History & Reminders Card -->
	<div class="border border-border/70 rounded-2xl bg-card shadow-xs overflow-hidden">
		<!-- Sub-tab Bar -->
		<div class="flex items-center justify-between px-3 sm:px-4 border-b border-border/70 bg-muted/20">
			<div class="flex items-center gap-1">
				<button
					type="button"
					onclick={() => (activeSubTab = 'history')}
					class="py-3 px-3.5 font-semibold text-xs border-b-2 transition-colors flex items-center gap-1.5 {activeSubTab === 'history' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
				>
					<Icon name="history" class="size-3.5" />
					Call History
					{#if callLogs.length > 0}
						<span class="text-[10px] bg-muted px-1.5 py-0.2 rounded-full font-bold">{callLogs.length}</span>
					{/if}
				</button>
				<button
					type="button"
					onclick={() => (activeSubTab = 'reminders')}
					class="py-3 px-3.5 font-semibold text-xs border-b-2 transition-colors flex items-center gap-1.5 {activeSubTab === 'reminders' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
				>
					<Icon name="calendar-clock" class="size-3.5" />
					Follow-up Reminders
					{#if reminders.length > 0}
						<span class="text-[10px] bg-muted px-1.5 py-0.2 rounded-full font-bold">{reminders.length}</span>
					{/if}
				</button>
			</div>

			<button
				type="button"
				onclick={loadHistory}
				disabled={loadingHistory}
				class="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1.5 px-2.5 py-1 rounded-lg border border-border/60 hover:bg-muted/40 transition-colors cursor-pointer"
				title="Refresh history"
			>
				<Icon name="rotate-cw" class="size-3 {loadingHistory ? 'animate-spin' : ''}" />
				<span class="hidden sm:inline">Refresh</span>
			</button>
		</div>

		<!-- Sub-tab Content -->
		<div class="p-4 sm:p-5">
			<HistoryTimeline
				type={activeSubTab}
				{callLogs}
				{reminders}
				{loadingHistory}
				onUndoCallLog={handleUndoCallLog}
				{isUndoingLog}
				onCompleteReminder={handleCompleteReminder}
				{completingReminderId}
				onRefreshHistory={loadHistory}
				onUpdateCallLogNotes={handleUpdateCallLogNotes}
				{salesUsersList}
			/>
		</div>
	</div>
</div>
