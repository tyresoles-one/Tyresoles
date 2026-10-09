<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import type { KpiMetrics } from '../api/types';

	let {
		metrics,
		perspective = 'self'
	}: {
		metrics: KpiMetrics;
		perspective?: 'self' | 'team';
	} = $props();
</script>

<div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
	<!-- Tile 1: Calls & Quota -->
	<div class="group relative overflow-hidden rounded-xl border bg-card p-3.5 shadow-sm transition-all hover:shadow-md hover:-translate-y-0.5">
		<div class="flex items-center justify-between gap-2">
			<div class="flex items-center gap-3 min-w-0">
				<div class="p-2 rounded-lg bg-primary/10 text-primary shrink-0">
					<Icon name="phone" class="w-4 h-4" />
				</div>
				<div class="flex flex-col min-w-0">
					<p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">
						{perspective === 'self' ? 'Calls Logged' : 'Team Calls'}
					</p>
					<div class="flex items-baseline gap-1.5">
						<h3 class="text-xl font-bold tracking-tight text-foreground tabular-nums">
							{metrics.totalCalls}
						</h3>
						<span class="text-xs text-muted-foreground font-mono">
							/ {metrics.targetCalls}
						</span>
					</div>
				</div>
			</div>

			<span class="text-[10px] font-bold tabular-nums px-2 py-0.5 rounded-md border shrink-0 {metrics.attainmentPct >= 100 ? 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20' : 'bg-muted text-muted-foreground border-border/50'}">
				{metrics.attainmentPct}% target
			</span>
		</div>
	</div>

	<!-- Tile 2: Connect Rate -->
	<div class="group relative overflow-hidden rounded-xl border bg-card p-3.5 shadow-sm transition-all hover:shadow-md hover:-translate-y-0.5">
		<div class="flex items-center justify-between gap-2">
			<div class="flex items-center gap-3 min-w-0">
				<div class="p-2 rounded-lg bg-teal-500/10 text-teal-600 dark:text-teal-400 shrink-0">
					<Icon name="phone-call" class="w-4 h-4" />
				</div>
				<div class="flex flex-col min-w-0">
					<p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">
						Connected
					</p>
					<div class="flex items-baseline gap-1.5">
						<h3 class="text-xl font-bold tracking-tight text-foreground tabular-nums">
							{metrics.connectRate}%
						</h3>
					</div>
				</div>
			</div>

			<span class="text-[10px] font-medium text-muted-foreground tabular-nums px-2 py-0.5 rounded-md bg-muted/60 shrink-0">
				{metrics.connectedCalls} reached
			</span>
		</div>
	</div>

	<!-- Tile 3: Positive Conversions Won -->
	<div class="group relative overflow-hidden rounded-xl border bg-card p-3.5 shadow-sm transition-all hover:shadow-md hover:-translate-y-0.5">
		<div class="flex items-center justify-between gap-2">
			<div class="flex items-center gap-3 min-w-0">
				<div class="p-2 rounded-lg bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 shrink-0">
					<Icon name="trophy" class="w-4 h-4" />
				</div>
				<div class="flex flex-col min-w-0">
					<p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">
						Conversions
					</p>
					<div class="flex items-baseline gap-1.5">
						<h3 class="text-xl font-bold tracking-tight text-foreground tabular-nums">
							{metrics.positiveCalls}
						</h3>
					</div>
				</div>
			</div>

			<span class="text-[10px] font-bold tabular-nums px-2 py-0.5 rounded-md bg-emerald-500/10 text-emerald-600 border border-emerald-500/20 shrink-0">
				{metrics.positiveRate}% win
			</span>
		</div>
	</div>

	<!-- Tile 4: Streak (Self) / Active Reps (Team) -->
	<div class="group relative overflow-hidden rounded-xl border bg-card p-3.5 shadow-sm transition-all hover:shadow-md hover:-translate-y-0.5">
		<div class="flex items-center justify-between gap-2">
			<div class="flex items-center gap-3 min-w-0">
				<div class="p-2 rounded-lg bg-amber-500/10 text-amber-600 dark:text-amber-400 shrink-0">
					<Icon name={perspective === 'self' ? 'flame' : 'users'} class="w-4 h-4" />
				</div>
				<div class="flex flex-col min-w-0">
					<p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">
						{perspective === 'self' ? 'Calling Streak' : 'Active Reps'}
					</p>
					<div class="flex items-baseline gap-1.5">
						<h3 class="text-xl font-bold tracking-tight text-foreground tabular-nums">
							{perspective === 'self' ? `${metrics.streakDays} Days` : `${metrics.totalRepsCount || 0} Reps`}
						</h3>
					</div>
				</div>
			</div>

			{#if perspective === 'self' && metrics.streakDays >= 1}
				<span class="text-[10px] font-bold px-2 py-0.5 rounded-md bg-amber-500/10 text-amber-600 border border-amber-500/20 shrink-0">
					🔥 Consistent
				</span>
			{:else}
				<span class="text-[10px] font-medium text-muted-foreground px-2 py-0.5 rounded-md bg-muted/60 shrink-0">
					{perspective === 'self' ? 'Daily activity' : 'Team members'}
				</span>
			{/if}
		</div>
	</div>
</div>
