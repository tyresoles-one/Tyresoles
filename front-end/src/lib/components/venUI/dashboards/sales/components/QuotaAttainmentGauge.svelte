<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import type { KpiMetrics } from '../api/types';

	let {
		metrics,
		title = 'Call Quota Pacing',
		subtitle = 'Live attainment vs target'
	}: {
		metrics: KpiMetrics;
		title?: string;
		subtitle?: string;
	} = $props();

	// SVG circle geometry
	const size = 160;
	const strokeWidth = 14;
	const radius = (size - strokeWidth) / 2;
	const circumference = 2 * Math.PI * radius;

	// Clamp between 0% and 100% for circle visual stroke
	const progressClamped = $derived(Math.min(100, Math.max(0, metrics.attainmentPct)));
	const strokeDashoffset = $derived(circumference - (progressClamped / 100) * circumference);

	// Status colors
	const paceConfig = $derived.by(() => {
		if (metrics.attainmentPct >= 100) {
			return {
				color: 'oklch(0.68 0.2 145)', // Emerald
				badgeBg: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
				label: 'Target Met 🎯',
				icon: 'check-circle-2'
			};
		}
		if (metrics.paceStatus === 'ahead') {
			return {
				color: 'oklch(0.65 0.18 190)', // Teal
				badgeBg: 'bg-teal-500/10 text-teal-600 dark:text-teal-400 border-teal-500/20',
				label: 'Ahead of Pace ⚡',
				icon: 'trending-up'
			};
		}
		if (metrics.paceStatus === 'behind') {
			return {
				color: 'oklch(0.65 0.22 25)', // Rose / Red
				badgeBg: 'bg-rose-500/10 text-rose-600 dark:text-rose-400 border-rose-500/20',
				label: 'Behind Pace ⚠️',
				icon: 'alert-circle'
			};
		}
		return {
			color: 'oklch(0.72 0.16 85)', // Amber
			badgeBg: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
			label: 'On Track ⏱️',
			icon: 'clock'
		};
	});
</script>

<div class="h-full rounded-2xl border border-border/60 bg-card p-4 sm:p-5 shadow-xs flex flex-col justify-between">
	<!-- Header -->
	<div class="flex items-start justify-between gap-2">
		<div class="space-y-0.5">
			<div class="flex items-center gap-1.5">
				<div class="p-1.5 rounded-lg bg-primary/10 text-primary">
					<Icon name="crosshair" class="size-4" />
				</div>
				<h3 class="text-sm font-bold text-foreground tracking-tight">{title}</h3>
			</div>
			<p class="text-[11px] text-muted-foreground">{subtitle}</p>
		</div>

		<span class="inline-flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded-full border {paceConfig.badgeBg}">
			<Icon name={paceConfig.icon} class="size-3" />
			{paceConfig.label}
		</span>
	</div>

	<!-- Radial Visual -->
	<div class="flex items-center justify-center my-2 relative">
		<svg width={size} height={size} class="rotate-[-90deg] drop-shadow-xs">
			<!-- Background track -->
			<circle
				cx={size / 2}
				cy={size / 2}
				r={radius}
				fill="transparent"
				stroke="currentColor"
				class="text-muted/30"
				stroke-width={strokeWidth}
			/>

			<!-- Progress track -->
			<circle
				cx={size / 2}
				cy={size / 2}
				r={radius}
				fill="transparent"
				stroke={paceConfig.color}
				stroke-width={strokeWidth}
				stroke-dasharray={circumference}
				stroke-dashoffset={strokeDashoffset}
				stroke-linecap="round"
				class="transition-all duration-700 ease-out"
			/>
		</svg>

		<!-- Center Stat Display -->
		<div class="absolute inset-0 flex flex-col items-center justify-center text-center">
			<span class="text-3xl font-black font-mono tracking-tight text-foreground">
				{metrics.attainmentPct}%
			</span>
			<span class="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">
				Attained
			</span>
		</div>
	</div>

	<!-- Bottom Quota Breakdown Grid -->
	<div class="grid grid-cols-3 gap-2 border-t border-border/50 pt-3 text-center">
		<div class="space-y-0.5">
			<span class="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Completed</span>
			<p class="text-base font-bold font-mono text-foreground leading-none">{metrics.totalCalls}</p>
			<span class="text-[9px] text-muted-foreground">calls logged</span>
		</div>

		<div class="space-y-0.5 border-x border-border/50 px-1">
			<span class="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Target</span>
			<p class="text-base font-bold font-mono text-foreground leading-none">{metrics.targetCalls}</p>
			<span class="text-[9px] text-muted-foreground">calls quota</span>
		</div>

		<div class="space-y-0.5">
			<span class="text-[10px] font-medium text-muted-foreground uppercase tracking-wider">Pace Run-Rate</span>
			<p class="text-base font-bold font-mono leading-none" style="color: {paceConfig.color}">
				{metrics.paceProjectedCalls}
			</p>
			<span class="text-[9px] text-muted-foreground">expected total</span>
		</div>
	</div>
</div>
