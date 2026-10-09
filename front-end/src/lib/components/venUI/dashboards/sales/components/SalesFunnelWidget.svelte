<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import type { FunnelStage } from '../api/types';

	let {
		stages = [],
		title = 'Sales Conversion Funnel',
		subtitle = 'Efficiency drop-off across calling pipeline'
	}: {
		stages: FunnelStage[];
		title?: string;
		subtitle?: string;
	} = $props();

	const maxCount = $derived(stages.length > 0 ? Math.max(...stages.map((s) => s.count), 1) : 1);
</script>

<div class="h-full rounded-2xl border border-border/60 bg-card p-4 sm:p-5 shadow-xs flex flex-col justify-between">
	<!-- Header -->
	<div class="flex items-start justify-between gap-2 mb-3">
		<div class="space-y-0.5">
			<div class="flex items-center gap-1.5">
				<div class="p-1.5 rounded-lg bg-primary/10 text-primary">
					<Icon name="filter" class="size-4" />
				</div>
				<h3 class="text-sm font-bold text-foreground tracking-tight">{title}</h3>
			</div>
			<p class="text-[11px] text-muted-foreground">{subtitle}</p>
		</div>
	</div>

	<!-- Funnel Stages -->
	<div class="space-y-3.5 my-auto">
		{#each stages as stage, i (stage.key)}
			{@const pctWidth = Math.max(8, Math.round((stage.count / maxCount) * 100))}
			<div class="space-y-1 group">
				<div class="flex items-center justify-between text-xs">
					<div class="flex items-center gap-2">
						<div class="size-5 rounded-md flex items-center justify-center text-[10px] font-bold" style="background-color: {stage.color}20; color: {stage.color}">
							{i + 1}
						</div>
						<span class="font-semibold text-foreground text-xs">{stage.label}</span>
					</div>

					<div class="flex items-center gap-2 font-mono">
						<span class="font-bold text-foreground text-xs">{stage.count}</span>
						{#if i > 0}
							<span class="text-[10px] text-muted-foreground font-sans">
								({stage.conversionFromPrevious}% from prev)
							</span>
						{:else}
							<span class="text-[10px] text-muted-foreground font-sans">
								(100% baseline)
							</span>
						{/if}
					</div>
				</div>

				<!-- Visual Progress Bar -->
				<div class="h-3 w-full rounded-full bg-muted/40 overflow-hidden relative">
					<div
						class="h-full rounded-full transition-all duration-700 ease-out flex items-center justify-end pr-2"
						style="width: {pctWidth}%; background-color: {stage.color};"
					></div>
				</div>
			</div>
		{/each}
	</div>

	<!-- Bottom Summary Note -->
	<div class="border-t border-border/50 pt-2.5 mt-2 flex items-center justify-between text-[11px] text-muted-foreground">
		<span>Overall Dial-to-Won:</span>
		<span class="font-bold font-mono text-emerald-600 dark:text-emerald-400">
			{stages.length > 0 && stages[0].count > 0 ? ((stages[stages.length - 1].count / stages[0].count) * 100).toFixed(1) : '0.0'}%
		</span>
	</div>
</div>
