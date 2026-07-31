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
		onCompleteReminder
	}: {
		type: 'history' | 'reminders';
		callLogs: CallLog[];
		reminders: CallReminder[];
		loadingHistory: boolean;
		onUndoCallLog: (id: string) => void;
		isUndoingLog: string | null;
		onCompleteReminder: (id: string) => void;
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
		<div class="flex justify-center py-12">
			<Loader2 class="size-6 animate-spin text-primary" />
		</div>
	{:else if callLogs.length === 0}
		<EmptyState
			icon="phone-off"
			title="No Call Logs"
			description="No call logs found for this contact."
			class="py-8"
		/>
	{:else}
		<div class="relative border-l border-border ml-3 space-y-8 pb-4">
			{#each callLogs as log (log.id)}
				<div class="relative pl-6">
					<div class="absolute -left-2 top-1.5 size-4 rounded-full border border-card flex items-center justify-center bg-indigo-600 text-white shadow-xs">
						<Icon name="phone" class="size-2" />
					</div>
					
					<div class="space-y-1 bg-muted/10 border border-border/40 rounded-xl p-4 hover:bg-muted/20 transition-all">
						<div class="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-1.5">
							<div class="flex items-center gap-2">
								<span class="font-bold text-xs bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 px-2.5 py-0.5 rounded-full uppercase tracking-wider">
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
								<span class="text-xs text-muted-foreground flex items-center gap-1 font-medium">
									<Icon name="clock" class="size-3 text-muted-foreground/60" />
									{formatDate(log.callDate)}
								</span>
							</div>
						</div>
						
						{#if log.notes}
							<p class="text-sm leading-relaxed text-foreground/80 pt-1.5 whitespace-pre-wrap">{log.notes}</p>
						{/if}
					</div>
				</div>
			{/each}
		</div>
	{/if}
{:else if type === 'reminders'}
	{#if loadingHistory}
		<div class="flex justify-center py-12">
			<Loader2 class="size-6 animate-spin text-primary" />
		</div>
	{:else if reminders.length === 0}
		<EmptyState
			icon="calendar"
			title="No Reminders"
			description="No reminders scheduled for this contact."
			class="py-8"
		/>
	{:else}
		<div class="space-y-4">
			{#each reminders as rem (rem.id)}
				<div class="border border-border bg-card hover:bg-muted/5 rounded-xl p-4 flex items-center justify-between gap-4 transition-all {rem.isCompleted ? 'opacity-60 bg-muted/10' : ''}">
					<div class="space-y-1 min-w-0">
						<div class="flex items-center gap-2">
							{#if rem.isCompleted}
								<span class="text-xs bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 px-2.5 py-0.5 rounded-full font-bold flex items-center gap-1">
									<Icon name="circle-check" class="size-3" />
									Completed
								</span>
							{:else}
								<span class="text-xs bg-rose-500/10 text-rose-600 dark:text-rose-400 px-2.5 py-0.5 rounded-full font-bold flex items-center gap-1">
									<Icon name="clock" class="size-3" />
									Pending
								</span>
							{/if}
							<span class="text-xs text-muted-foreground font-semibold">
								Scheduled for: {formatDate(rem.reminderDate)}
							</span>
						</div>
						{#if rem.notes}
							<p class="text-sm text-foreground/85 line-clamp-2 pt-1 font-medium">{rem.notes}</p>
						{/if}
						<p class="text-[10px] text-muted-foreground">Created by {rem.createdBy} on {formatDate(rem.createdAt)}</p>
					</div>

					<div class="shrink-0 flex items-center gap-2">
						{#if !rem.isCompleted}
							<Button
								size="sm"
								variant="outline"
								onclick={() => onCompleteReminder(rem.id)}
								class="h-8 rounded-lg text-emerald-600 hover:text-emerald-500 hover:bg-emerald-500/5 font-semibold text-xs gap-1 border-emerald-500/20"
							>
								<Icon name="circle-check" class="size-3.5" />
								<span>Complete</span>
							</Button>
						{/if}
					</div>
				</div>
			{/each}
		</div>
	{/if}
{/if}
