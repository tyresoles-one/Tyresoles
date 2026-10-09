<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import * as Card from '$lib/components/ui/card';
	import type { ActivityBucket } from '../api/types';

	let {
		buckets = [],
		title = 'Calling & Conversion Velocity',
		class: className = ''
	}: {
		buckets: ActivityBucket[];
		title?: string;
		class?: string;
	} = $props();

	let hoveredIndex = $state<number | null>(null);

	// Headroom for bar scaling
	const maxVal = $derived.by(() => {
		if (buckets.length === 0) return 10;
		const peak = Math.max(...buckets.map((b) => Math.max(b.calls, 1)));
		return Math.ceil(peak * 1.2);
	});

	const activeHoveredBucket = $derived(hoveredIndex !== null ? buckets[hoveredIndex] : null);

	const totalCallsInPeriod = $derived(buckets.reduce((acc, curr) => acc + curr.calls, 0));
	const totalConnected = $derived(buckets.reduce((acc, curr) => acc + curr.connected, 0));
	const totalWon = $derived(buckets.reduce((acc, curr) => acc + curr.positive, 0));
</script>

<Card.Root class="overflow-hidden border shadow-xs h-full flex flex-col {className}">
	<Card.Header class="flex flex-col sm:flex-row items-start sm:items-center justify-between px-4 py-3 border-b border-border/40 gap-2">
		<div>
			<Card.Title class="text-sm font-bold tracking-tight">{title}</Card.Title>
			<Card.Description class="text-[11px] text-muted-foreground mt-0.5">
				Activity breakdown across selected timeframe
			</Card.Description>
		</div>

		<!-- Clean Minimal Legend -->
		<div class="flex items-center gap-3 text-xs">
			<div class="flex items-center gap-1.5">
				<span class="size-2 rounded-full bg-primary"></span>
				<span class="text-[11px] text-muted-foreground">Dialed</span>
			</div>
			<div class="flex items-center gap-1.5">
				<span class="size-2 rounded-full bg-teal-500"></span>
				<span class="text-[11px] text-muted-foreground">Connected</span>
			</div>
			<div class="flex items-center gap-1.5">
				<span class="size-2 rounded-full bg-emerald-500"></span>
				<span class="text-[11px] text-muted-foreground">Won</span>
			</div>
		</div>
	</Card.Header>

	<Card.Content class="p-4 flex-1 flex flex-col justify-between relative">
		<!-- Chart Area -->
		<div class="relative w-full h-[220px] flex items-end pt-4 pb-6 select-none">
			<!-- Subtle Horizontal Guide Lines -->
			<div class="absolute inset-x-0 inset-y-4 flex flex-col justify-between pointer-events-none opacity-25">
				<div class="border-b border-dashed border-border/60 w-full"></div>
				<div class="border-b border-dashed border-border/60 w-full"></div>
				<div class="border-b border-dashed border-border/60 w-full"></div>
				<div class="border-b border-border/80 w-full"></div>
			</div>

			<!-- Bars Container -->
			<div class="w-full h-full flex items-end justify-between gap-2 px-1 relative z-10">
				{#if buckets.length === 0}
					<div class="w-full h-full flex flex-col items-center justify-center text-xs text-muted-foreground gap-1.5">
						<Icon name="phone-off" class="size-6 opacity-30" />
						<span>No activity data available.</span>
					</div>
				{:else}
					{#each buckets as b, i (b.dateKey)}
						{@const callH = b.calls > 0 ? Math.max(6, Math.min(100, Math.round((b.calls / maxVal) * 100))) : 2}
						{@const connH = Math.min(100, Math.round((b.connected / maxVal) * 100))}
						{@const isHovered = hoveredIndex === i}

						<div
							class="flex-1 h-full flex flex-col items-center justify-end group cursor-pointer relative"
							onmouseenter={() => (hoveredIndex = i)}
							onmouseleave={() => (hoveredIndex = null)}
							role="group"
						>
							<!-- Bar Column -->
							<div class="w-full max-w-[40px] flex items-end justify-center h-full pb-1">
								<div
									class="w-full rounded-t-md transition-all duration-300 relative {isHovered ? 'opacity-100 ring-2 ring-primary/30' : 'opacity-85 hover:opacity-100'} {b.calls > 0 ? (b.isCurrent ? 'bg-primary' : 'bg-primary/80') : 'bg-muted/40'}"
									style="height: {callH}%;"
								>
									<!-- Connected Portion Sub-bar -->
									{#if b.connected > 0}
										<div
											class="w-full absolute bottom-0 rounded-t-xs bg-teal-500 opacity-90 transition-all duration-300"
											style="height: {Math.min(100, Math.round((b.connected / Math.max(b.calls, 1)) * 100))}%;"
										>
											<!-- Positive Conversion Sub-bar -->
											{#if b.positive > 0}
												<div
													class="w-full absolute bottom-0 rounded-t-xs bg-emerald-400 opacity-100 transition-all duration-300"
													style="height: {Math.min(100, Math.round((b.positive / Math.max(b.connected, 1)) * 100))}%;"
												></div>
											{/if}
										</div>
									{/if}
								</div>
							</div>

							<!-- X-Axis Label -->
							<div class="absolute -bottom-5 inset-x-0 text-center">
								<span class="text-[10px] font-medium truncate block transition-colors {isHovered ? 'text-primary font-bold' : 'text-muted-foreground'} {b.isCurrent ? 'text-primary font-bold' : ''}">
									{b.label}
								</span>
							</div>
						</div>
					{/each}
				{/if}
			</div>

			<!-- Hover Tooltip Popup -->
			{#if activeHoveredBucket}
				<div
					class="absolute top-2 right-2 z-30 pointer-events-none rounded-xl border border-border bg-popover/95 backdrop-blur-md p-2.5 shadow-lg text-xs space-y-1 min-w-[160px]"
				>
					<div class="flex items-center justify-between border-b border-border/40 pb-1">
						<span class="font-bold text-foreground text-[11px]">{activeHoveredBucket.label}</span>
						<span class="text-[10px] text-muted-foreground">{activeHoveredBucket.subLabel}</span>
					</div>
					<div class="space-y-0.5 pt-0.5 font-mono text-[11px]">
						<div class="flex items-center justify-between">
							<span class="text-muted-foreground flex items-center gap-1 font-sans">
								<span class="size-1.5 rounded-full bg-primary"></span> Dialed:
							</span>
							<span class="font-bold text-foreground">{activeHoveredBucket.calls}</span>
						</div>
						<div class="flex items-center justify-between">
							<span class="text-teal-600 dark:text-teal-400 flex items-center gap-1 font-sans">
								<span class="size-1.5 rounded-full bg-teal-500"></span> Connected:
							</span>
							<span class="font-bold text-foreground">{activeHoveredBucket.connected}</span>
						</div>
						<div class="flex items-center justify-between">
							<span class="text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-sans">
								<span class="size-1.5 rounded-full bg-emerald-500"></span> Won:
							</span>
							<span class="font-bold text-foreground">{activeHoveredBucket.positive}</span>
						</div>
					</div>
				</div>
			{/if}
		</div>
	</Card.Content>

	<!-- Footer Summary Bar -->
	<Card.Footer class="px-4 py-2.5 border-t border-border/40 bg-muted/10 text-xs text-muted-foreground flex items-center justify-between">
		<div class="flex items-center gap-2">
			<Icon name="check-circle" class="size-3.5 text-emerald-500" />
			<span>
				Total in period: <strong class="text-foreground">{totalCallsInPeriod} dialed</strong> ({totalConnected} connected, {totalWon} won)
			</span>
		</div>
		<span class="text-[11px] font-mono">
			{totalCallsInPeriod > 0 ? ((totalConnected / totalCallsInPeriod) * 100).toFixed(0) : 0}% connect rate
		</span>
	</Card.Footer>
</Card.Root>
