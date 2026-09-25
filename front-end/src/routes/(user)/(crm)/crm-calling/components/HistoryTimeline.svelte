<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { Textarea } from '$lib/components/ui/textarea';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import EmptyState from '$lib/components/venUI/emptyState/EmptyState.svelte';
	import { authStore } from '$lib/stores/auth';
	import type { CallLog, CallReminder } from '../queries';

	let {
		type,
		callLogs,
		reminders,
		loadingHistory,
		onUndoCallLog,
		isUndoingLog,
		onCompleteReminder,
		completingReminderId = null,
		onRefreshHistory,
		onUpdateCallLogNotes,
		salesUsersList = []
	}: {
		type: 'history' | 'reminders';
		callLogs: CallLog[];
		reminders: CallReminder[];
		loadingHistory: boolean;
		onUndoCallLog: (id: string) => void;
		isUndoingLog: string | null;
		onCompleteReminder: (id: string) => void;
		completingReminderId?: string | null;
		onRefreshHistory?: () => void;
		onUpdateCallLogNotes?: (callLogId: string, notes: string | null) => Promise<boolean>;
		salesUsersList?: { userName: string; fullName: string }[];
	} = $props();

	let editingLogId = $state<string | null>(null);
	let editingNotes = $state<string>('');
	let isSavingNotes = $state(false);

	let sortedLogs = $derived(
		[...callLogs].sort((a, b) => new Date(b.callDate).getTime() - new Date(a.callDate).getTime())
	);

	function formatDate(dateStr: string) {
		if (!dateStr) return '—';
		let normalizedStr = dateStr;
		if (!dateStr.endsWith('Z') && !dateStr.includes('+') && !/-\d{2}:\d{2}$/.test(dateStr)) {
			normalizedStr = dateStr + 'Z';
		}
		const date = new Date(normalizedStr);
		return date.toLocaleString('en-IN', {
			day: '2-digit',
			month: 'short',
			year: 'numeric',
			hour: '2-digit',
			minute: '2-digit',
			hour12: true
		});
	}

	function getSalesUserName(userId?: string | null): string {
		if (!userId) return '';
		const clean = userId.replace(/^tyresoles\\/i, '').trim().toLowerCase();
		const found = salesUsersList?.find(u => 
			u.userName.toLowerCase() === clean || 
			u.userName.toLowerCase() === userId.toLowerCase()
		);
		if (found?.fullName && found.fullName.trim()) {
			return found.fullName.trim();
		}
		return userId.replace(/^tyresoles\\/i, '').trim();
	}
</script>

{#if type === 'history'}
	{#if loadingHistory}
		<div class="flex justify-center py-6">
			<Loader2 class="size-5 animate-spin text-primary" />
		</div>
	{:else if sortedLogs.length === 0}
		<div class="py-4 space-y-3 flex flex-col items-center justify-center">
			<EmptyState
				icon="phone-off"
				title="No Call Logs"
				description="No call logs found for this contact."
				class="py-2"
			/>
			{#if onRefreshHistory}
				<button
					type="button"
					onclick={onRefreshHistory}
					disabled={loadingHistory}
					class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-border bg-muted/30 hover:bg-muted text-foreground flex items-center gap-1.5 transition-colors cursor-pointer"
				>
					<Icon name="rotate-cw" class="size-3.5 {loadingHistory ? 'animate-spin' : ''}" />
					<span>Refresh History</span>
				</button>
			{/if}
		</div>
	{:else}
		{#if onRefreshHistory}
			<div class="flex items-center justify-between pb-2 mb-3 border-b border-border/40">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
					{sortedLogs.length} Call Record{sortedLogs.length === 1 ? '' : 's'}
				</span>
				<button
					type="button"
					onclick={onRefreshHistory}
					disabled={loadingHistory}
					class="text-[11px] text-muted-foreground hover:text-foreground flex items-center gap-1 px-2 py-0.5 rounded hover:bg-muted/50 transition-colors cursor-pointer"
					title="Refresh call logs"
				>
					<Icon name="rotate-cw" class="size-3 {loadingHistory ? 'animate-spin' : ''}" />
					<span>Refresh</span>
				</button>
			</div>
		{/if}
		<div class="relative border-l border-border ml-2.5 space-y-3 pb-2">
			{#each sortedLogs as log, index (log.id)}
				<div class="relative pl-5">
					<div class="absolute -left-2 top-1.5 size-4 rounded-full border border-card flex items-center justify-center bg-indigo-600 text-white shadow-xs">
						<Icon name="phone" class="size-2" />
					</div>
					
					<div class="space-y-1.5 bg-muted/10 border border-border/40 rounded-lg p-2.5 sm:p-3 hover:bg-muted/20 transition-all">
						<div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-1">
							<div class="flex flex-wrap items-center gap-2">
								<span class="font-bold text-[11px] bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 px-2 py-0.5 rounded-full uppercase tracking-wider">
									{log.outcome}
								</span>
								<span class="text-xs text-muted-foreground font-medium">by {log.createdBy}</span>
								{#if log.salesUserId}
									<span class="inline-flex items-center gap-1 text-[11px] bg-sky-500/10 text-sky-700 dark:text-sky-300 border border-sky-500/20 px-2 py-0.5 rounded-full font-medium">
										<Icon name="user-check" class="size-3 text-sky-600 dark:text-sky-400 shrink-0" />
										<span>Forwarded: <strong class="font-semibold">{getSalesUserName(log.salesUserId)}</strong></span>
									</span>
								{/if}
							</div>
							<div class="flex items-center gap-2">
								<!-- Undo button is allowed only for the most recent (1st) call log -->
								{#if index === 0 && log.createdBy === $authStore.username && new Date(log.callDate).toDateString() === new Date().toDateString()}
									<button
										type="button"
										onclick={() => onUndoCallLog(log.id)}
										disabled={isUndoingLog === log.id}
										class="text-[10px] bg-red-500/10 text-red-600 hover:bg-red-500/20 px-2 py-0.5 rounded-full font-bold transition-colors flex items-center gap-1 cursor-pointer"
									>
										{#if isUndoingLog === log.id}
											<Loader2 class="size-3 animate-spin shrink-0" />
										{:else}
											<Icon name="undo-2" class="size-3" />
										{/if}
										Undo
									</button>
								{/if}
								<span class="text-[11px] text-muted-foreground flex items-center gap-1 font-medium">
									<Icon name="clock" class="size-3 text-muted-foreground/60" />
									{formatDate(log.callDate)}
								</span>
							</div>
						</div>
						
						<!-- Comment / Conversation summary with inline edit -->
						{#if editingLogId === log.id}
							<div class="space-y-2 pt-1 mt-1 border-t border-border/40">
								<Textarea
									bind:value={editingNotes}
									placeholder="Type comment / conversation summary..."
									class="min-h-[60px] max-h-[140px] text-xs rounded-lg p-2 bg-background focus-visible:ring-1"
								/>
								<div class="flex items-center justify-end gap-2">
									<Button
										type="button"
										variant="ghost"
										size="sm"
										onclick={() => {
											editingLogId = null;
											editingNotes = '';
										}}
										disabled={isSavingNotes}
										class="h-7 px-2.5 text-xs cursor-pointer"
									>
										Cancel
									</Button>
									<Button
										type="button"
										size="sm"
										onclick={async () => {
											if (!onUpdateCallLogNotes) return;
											isSavingNotes = true;
											const ok = await onUpdateCallLogNotes(log.id, editingNotes.trim() || null);
											isSavingNotes = false;
											if (ok) {
												log.notes = editingNotes.trim() || null;
												editingLogId = null;
												editingNotes = '';
											}
										}}
										disabled={isSavingNotes}
										class="h-7 px-3 text-xs bg-indigo-600 hover:bg-indigo-500 text-white gap-1 cursor-pointer"
									>
										{#if isSavingNotes}
											<Loader2 class="size-3 animate-spin shrink-0" />
										{/if}
										Save
									</Button>
								</div>
							</div>
						{:else}
							<div class="flex items-start justify-between gap-2 group/comment pt-1 mt-0.5">
								{#if log.notes}
									<p class="text-xs leading-relaxed text-foreground/85 whitespace-pre-wrap flex-1">{log.notes}</p>
								{:else}
									<span class="text-xs text-muted-foreground/50 italic flex-1">No comment added</span>
								{/if}
								{#if onUpdateCallLogNotes}
									<button
										type="button"
										onclick={() => {
											editingLogId = log.id;
											editingNotes = log.notes || '';
										}}
										class="text-[11px] text-muted-foreground hover:text-primary opacity-60 hover:opacity-100 transition-all flex items-center gap-1 shrink-0 px-1.5 py-0.5 rounded hover:bg-muted/60 cursor-pointer"
										title="Edit comment"
									>
										<Icon name="pencil" class="size-3" />
										<span>{log.notes ? 'Edit' : 'Add comment'}</span>
									</button>
								{/if}
							</div>
						{/if}
					</div>
				</div>
			{/each}
		</div>
	{/if}
{:else if type === 'reminders'}
	{#if loadingHistory}
		<div class="flex justify-center py-6">
			<Loader2 class="size-5 animate-spin text-primary" />
		</div>
	{:else if reminders.length === 0}
		<div class="py-4 space-y-3 flex flex-col items-center justify-center">
			<EmptyState
				icon="calendar"
				title="No Reminders"
				description="No reminders scheduled for this contact."
				class="py-2"
			/>
			{#if onRefreshHistory}
				<button
					type="button"
					onclick={onRefreshHistory}
					disabled={loadingHistory}
					class="px-3 py-1.5 text-xs font-semibold rounded-lg border border-border bg-muted/30 hover:bg-muted text-foreground flex items-center gap-1.5 transition-colors cursor-pointer"
				>
					<Icon name="rotate-cw" class="size-3.5 {loadingHistory ? 'animate-spin' : ''}" />
					<span>Refresh Reminders</span>
				</button>
			{/if}
		</div>
	{:else}
		{#if onRefreshHistory}
			<div class="flex items-center justify-between pb-2 mb-3 border-b border-border/40">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">
					{reminders.length} Reminder{reminders.length === 1 ? '' : 's'}
				</span>
				<button
					type="button"
					onclick={onRefreshHistory}
					disabled={loadingHistory}
					class="text-[11px] text-muted-foreground hover:text-foreground flex items-center gap-1 px-2 py-0.5 rounded hover:bg-muted/50 transition-colors cursor-pointer"
					title="Refresh reminders"
				>
					<Icon name="rotate-cw" class="size-3 {loadingHistory ? 'animate-spin' : ''}" />
					<span>Refresh</span>
				</button>
			</div>
		{/if}
		<div class="space-y-2">
			{#each reminders as rem (rem.id)}
				<div class="border border-border bg-card hover:bg-muted/5 rounded-lg p-2.5 sm:p-3 flex items-center justify-between gap-3 transition-all {rem.isCompleted ? 'opacity-60 bg-muted/10' : ''}">
					<div class="space-y-0.5 min-w-0">
						<div class="flex items-center gap-2">
							{#if rem.isCompleted}
								<span class="text-[11px] bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 px-2 py-0.5 rounded-full font-bold flex items-center gap-1">
									<Icon name="circle-check" class="size-3" />
									Completed
								</span>
							{:else}
								<span class="text-[11px] bg-rose-500/10 text-rose-600 dark:text-rose-400 px-2 py-0.5 rounded-full font-bold flex items-center gap-1">
									<Icon name="clock" class="size-3" />
									Pending
								</span>
							{/if}
							<span class="text-xs text-muted-foreground font-semibold">
								Scheduled: {formatDate(rem.reminderDate)}
							</span>
						</div>
						{#if rem.notes}
							<p class="text-xs text-foreground/85 line-clamp-2 pt-0.5 font-medium">{rem.notes}</p>
						{/if}
						<p class="text-[10px] text-muted-foreground">By {rem.createdBy} • {formatDate(rem.createdAt)}</p>
					</div>

					<div class="shrink-0 flex items-center gap-2">
						{#if !rem.isCompleted}
							<Button
								size="sm"
								variant="outline"
								onclick={() => onCompleteReminder(rem.id)}
								disabled={completingReminderId === rem.id}
								class="h-7 px-2 rounded-lg text-emerald-600 hover:text-emerald-500 hover:bg-emerald-500/5 font-semibold text-xs gap-1 border-emerald-500/20"
							>
								{#if completingReminderId === rem.id}
									<Loader2 class="size-3 animate-spin shrink-0 text-emerald-600" />
									<span>Saving...</span>
								{:else}
									<Icon name="circle-check" class="size-3" />
									<span>Complete</span>
								{/if}
							</Button>
						{/if}
					</div>
				</div>
			{/each}
		</div>
	{/if}
{/if}
