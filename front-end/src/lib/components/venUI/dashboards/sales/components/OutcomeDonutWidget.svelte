<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import type { OutcomeStat } from '../api/types';

	let {
		outcomes = [],
		title = 'Outcome Distribution',
		subtitle = 'Categorical call results'
	}: {
		outcomes: OutcomeStat[];
		title?: string;
		subtitle?: string;
	} = $props();

	const totalCount = $derived(outcomes.reduce((acc, curr) => acc + curr.count, 0));

	// SVG donut geometry
	const size = 140;
	const strokeWidth = 18;
	const radius = (size - strokeWidth) / 2;
	const circumference = 2 * Math.PI * radius;

	// Calculate slice offsets
	const slices = $derived.by(() => {
		if (totalCount === 0) return [];
		let accumulated = 0;
		return outcomes.map((o) => {
			const strokeLength = (o.count / totalCount) * circumference;
			const strokeOffset = circumference - strokeLength;
			const rotation = (accumulated / totalCount) * 360 - 90;
			accumulated += o.count;
			return {
				...o,
				strokeLength,
				strokeOffset,
				rotation
			};
		});
	});
</script>

<div class="h-full rounded-2xl border border-border/60 bg-card p-4 sm:p-5 shadow-xs flex flex-col justify-between">
	<!-- Header -->
	<div class="flex items-start justify-between gap-2 mb-2">
		<div class="space-y-0.5">
			<div class="flex items-center gap-1.5">
				<div class="p-1.5 rounded-lg bg-primary/10 text-primary">
					<Icon name="pie-chart" class="size-4" />
				</div>
				<h3 class="text-sm font-bold text-foreground tracking-tight">{title}</h3>
			</div>
			<p class="text-[11px] text-muted-foreground">{subtitle}</p>
		</div>
	</div>

	<!-- Donut + Legend Layout -->
	<div class="flex flex-col sm:flex-row items-center justify-center gap-4 my-auto">
		<!-- SVG Donut -->
		<div class="relative size-[140px] shrink-0 flex items-center justify-center">
			{#if totalCount === 0}
				<div class="size-full rounded-full border-4 border-dashed border-border/40 flex items-center justify-center text-center p-2">
					<span class="text-[10px] text-muted-foreground">No calls logged</span>
				</div>
			{:else}
				<svg width={size} height={size} class="overflow-visible">
					<!-- Base ring -->
					<circle
						cx={size / 2}
						cy={size / 2}
						r={radius}
						fill="transparent"
						stroke="currentColor"
						class="text-muted/20"
						stroke-width={strokeWidth}
					/>

					<!-- Segment arcs -->
					{#each slices as slice}
						<circle
							cx={size / 2}
							cy={size / 2}
							r={radius}
							fill="transparent"
							stroke={slice.color}
							stroke-width={strokeWidth}
							stroke-dasharray="{slice.strokeLength} {circumference}"
							stroke-linecap="round"
							transform="rotate({slice.rotation} {size / 2} {size / 2})"
							class="transition-all duration-500 ease-out hover:opacity-80"
						/>
					{/each}
				</svg>

				<!-- Center Text -->
				<div class="absolute inset-0 flex flex-col items-center justify-center pointer-events-none text-center">
					<span class="text-2xl font-black font-mono text-foreground leading-none">
						{totalCount}
					</span>
					<span class="text-[9px] font-semibold text-muted-foreground uppercase tracking-wider mt-0.5">
						Outcomes
					</span>
				</div>
			{/if}
		</div>

		<!-- Outcomes Legend List -->
		<div class="flex-1 w-full space-y-1.5 overflow-hidden">
			{#each outcomes.slice(0, 4) as o}
				<div class="flex items-center justify-between text-xs gap-2">
					<div class="flex items-center gap-1.5 min-w-0">
						<span class="size-2 rounded-full shrink-0" style="background-color: {o.color}"></span>
						<span class="text-[11px] font-medium text-foreground truncate" title={o.outcome}>
							{o.outcome}
						</span>
					</div>
					<div class="flex items-center gap-1.5 font-mono text-[11px] shrink-0">
						<span class="font-bold text-foreground">{o.count}</span>
						<span class="text-muted-foreground text-[10px]">({o.percentage}%)</span>
					</div>
				</div>
			{/each}
			{#if outcomes.length > 4}
				<div class="text-[10px] text-muted-foreground text-right pt-0.5">
					+{outcomes.length - 4} more outcomes
				</div>
			{/if}
		</div>
	</div>
</div>
