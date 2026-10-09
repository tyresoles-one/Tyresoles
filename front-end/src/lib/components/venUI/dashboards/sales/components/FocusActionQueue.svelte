<script lang="ts">
	import { goto } from '$app/navigation';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import * as Card from '$lib/components/ui/card';
	import type { FocusActionItem, FunnelStage } from '../api/types';

	let {
		items = [],
		funnelStages = [],
		title = "Today's Action Queue"
	}: {
		items: FocusActionItem[];
		funnelStages?: FunnelStage[];
		title?: string;
	} = $props();

	function handleLaunchDialer(contactId?: string) {
		if (contactId) {
			goto(`/crm-calling?contactId=${encodeURIComponent(contactId)}`);
		} else {
			goto('/crm-calling');
		}
	}
</script>

<Card.Root class="overflow-hidden border shadow-xs h-full flex flex-col">
	<Card.Header class="flex items-center justify-between px-4 py-3 border-b border-border/40">
		<div class="flex items-center gap-2">
			<Card.Title class="text-sm font-bold tracking-tight">{title}</Card.Title>
			<span class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-muted text-muted-foreground">
				{items.length} Pending
			</span>
		</div>

		<Button
			variant="ghost"
			size="sm"
			class="h-7 text-xs text-primary hover:bg-primary/10 gap-1 px-2 font-medium"
			onclick={() => handleLaunchDialer()}
		>
			<span>Open Calling</span>
			<Icon name="arrow-up-right" class="size-3" />
		</Button>
	</Card.Header>

	<Card.Content class="p-3 sm:p-4 flex-1 flex flex-col justify-between gap-4">
		<!-- Reminders List -->
		<div class="space-y-2 overflow-y-auto max-h-[190px] pr-1">
			{#if items.length === 0}
				<div class="text-center py-6 text-muted-foreground text-xs space-y-1 my-auto">
					<Icon name="check-circle" class="size-6 mx-auto opacity-30 text-emerald-500 mb-1" />
					<p class="font-semibold text-foreground">All caught up!</p>
					<p class="text-[11px]">No overdue callbacks or reminders scheduled for today.</p>
				</div>
			{:else}
				{#each items as item (item.id)}
					<div class="p-2.5 rounded-xl border border-border/50 bg-muted/20 hover:bg-muted/40 transition-colors flex items-center justify-between gap-2.5">
						<div class="space-y-0.5 min-w-0 flex-1">
							<div class="flex items-center gap-1.5">
								<span class="font-bold text-xs text-foreground truncate">
									{item.contactName}
								</span>
								{#if item.isOverdue}
									<span class="text-[9px] font-bold px-1.5 py-0.2 rounded bg-rose-500/10 text-rose-600 border border-rose-500/20">
										Overdue
									</span>
								{/if}
							</div>
							<div class="flex items-center gap-1.5 text-[10px] text-muted-foreground truncate">
								{#if item.companyName}
									<span class="truncate">{item.companyName}</span>
									<span>•</span>
								{/if}
								<span class="font-mono">{item.timeFormatted}</span>
							</div>
						</div>

						<Button
							size="sm"
							class="h-7 px-2.5 rounded-lg text-xs font-semibold bg-primary hover:bg-primary/90 text-primary-foreground shrink-0 gap-1"
							onclick={() => handleLaunchDialer(item.contactId)}
						>
							<Icon name="phone" class="size-3" />
							<span>Call</span>
						</Button>
					</div>
				{/each}
			{/if}
		</div>

		<!-- Compact Integrated Conversion Funnel Mini-Bar -->
		{#if funnelStages.length > 0}
			<div class="border-t border-border/40 pt-3 space-y-2">
				<div class="flex items-center justify-between text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">
					<span>Funnel Conversion</span>
					<span class="font-mono text-emerald-600 dark:text-emerald-400">
						{funnelStages[0].count > 0 ? ((funnelStages[funnelStages.length - 1].count / funnelStages[0].count) * 100).toFixed(0) : 0}% win rate
					</span>
				</div>

				<div class="grid grid-cols-4 gap-1.5 text-center">
					{#each funnelStages as stage}
						<div class="p-1.5 rounded-lg bg-muted/40 border border-border/40">
							<span class="text-[9px] text-muted-foreground block truncate">
								{stage.key === 'dialed' ? 'Dialed' : stage.key === 'connected' ? 'Connected' : stage.key === 'engaged' ? 'Engaged' : 'Won'}
							</span>
							<span class="text-xs font-bold font-mono text-foreground">{stage.count}</span>
						</div>
					{/each}
				</div>
			</div>
		{/if}
	</Card.Content>
</Card.Root>
