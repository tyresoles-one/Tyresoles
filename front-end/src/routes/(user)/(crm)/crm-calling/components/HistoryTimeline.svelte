<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
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
		completingReminderId = null
	}: {
		type: 'history' | 'reminders';
		callLogs: CallLog[];
		reminders: CallReminder[];
		loadingHistory: boolean;
		onUndoCallLog: (id: string) => void;
		isUndoingLog: string | null;
		onCompleteReminder: (id: string) => void;
		completingReminderId?: string | null;
	} = $props();

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
</script>

{#if type === 'history'}
	{#if loadingHistory}
		<div class="flex justify-center py-6">
			<Loader2 class="size-5 animate-spin text-primary" />
		</div>
	{:else if callLogs.length === 0}
		<EmptyState
			icon="phone-off"
			title="No Call Logs"
			description="No call logs found for this contact."
			class="py-4"
		/>
	{:else}
		<div class="relative border-l border-border ml-2.5 space-y-3 pb-2">
			{#each callLogs as log (log.id)}
				<div class="relative pl-5">
					<div class="absolute -left-2 top-1.5 size-4 rounded-full border border-card flex items-center justify-center bg-indigo-600 text-white shadow-xs">
						<Icon name="phone" class="size-2" />
					</div>
					
					<div class="space-y-1 bg-muted/10 border border-border/40 rounded-lg p-2.5 sm:p-3 hover:bg-muted/20 transition-all">
						<div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-1">
							<div class="flex items-center gap-2">
								<span class="font-bold text-[11px] bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 px-2 py-0.5 rounded-full uppercase tracking-wider">
									{log.outcome}
								</span>
								<span class="text-xs text-muted-foreground font-medium">by {log.createdBy}</span>
							</div>
							<div class="flex items-center gap-2">
								{#if log.createdBy === $authStore.username && new Date(log.callDate).toDateString() === new Date().toDateString()}
									<button
										type="button"
										onclick={() => onUndoCallLog(log.id)}
										disabled={isUndoingLog === log.id}
										class="text-[10px] bg-red-500/10 text-red-600 hover:bg-red-500/20 px-2 py-0.5 rounded-full font-bold transition-colors flex items-center gap-1"
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
						
						{#if log.notes}
							<p class="text-xs leading-relaxed text-foreground/85 pt-1 whitespace-pre-wrap">{log.notes}</p>
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
		<EmptyState
			icon="calendar"
			title="No Reminders"
			description="No reminders scheduled for this contact."
			class="py-4"
		/>
	{:else}
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
