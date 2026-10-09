<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import * as Card from '$lib/components/ui/card';
	import type { SubordinateRep } from '../api/types';

	let {
		reps = [],
		currentUsername = '',
		onInspectSubordinate,
		title = 'Subordinates & Team Leaderboard'
	}: {
		reps: SubordinateRep[];
		currentUsername?: string;
		onInspectSubordinate?: (rep: SubordinateRep) => void;
		title?: string;
	} = $props();

	let searchQuery = $state('');

	const filteredReps = $derived.by(() => {
		let list = reps;
		if (searchQuery.trim()) {
			const q = searchQuery.toLowerCase().trim();
			list = list.filter(
				(r) =>
					r.displayName.toLowerCase().includes(q) ||
					r.cleanUsername.toLowerCase().includes(q) ||
					(r.jobTitle && r.jobTitle.toLowerCase().includes(q))
			);
		}
		return list;
	});

	function getMedalBadge(rank: number) {
		if (rank === 1) return { label: '1st', bg: 'bg-amber-500/10 text-amber-600 border-amber-500/30 font-bold' };
		if (rank === 2) return { label: '2nd', bg: 'bg-slate-400/10 text-slate-600 border-slate-400/30 font-bold' };
		if (rank === 3) return { label: '3rd', bg: 'bg-orange-500/10 text-orange-600 border-orange-500/30 font-bold' };
		return { label: `#${rank}`, bg: 'bg-muted text-muted-foreground border-border/50' };
	}
</script>

<Card.Root class="overflow-hidden border shadow-xs">
	<Card.Header class="flex flex-col sm:flex-row items-start sm:items-center justify-between px-4 py-3 border-b border-border/40 gap-2">
		<div class="flex items-center gap-2">
			<Card.Title class="text-sm font-bold tracking-tight">{title}</Card.Title>
			<span class="text-[10px] font-bold px-2 py-0.5 rounded-full bg-muted text-muted-foreground">
				{reps.length} Reps
			</span>
		</div>

		<!-- Compact Search -->
		<div class="relative w-full sm:w-48">
			<Icon name="search" class="absolute left-2.5 top-2 size-3 text-muted-foreground" />
			<input
				type="text"
				bind:value={searchQuery}
				placeholder="Filter subordinate..."
				class="w-full h-7 pl-7 pr-2.5 text-xs rounded-lg border border-border bg-background focus:outline-none focus:ring-1 focus:ring-primary"
			/>
		</div>
	</Card.Header>

	<Card.Content class="p-0">
		<div class="overflow-x-auto">
			<table class="w-full text-xs text-left">
				<thead class="bg-muted/30 text-muted-foreground font-semibold uppercase text-[10px] border-b border-border/40">
					<tr>
						<th class="px-3.5 py-2.5 w-12 text-center">Rank</th>
						<th class="px-3.5 py-2.5">Subordinate</th>
						<th class="px-3.5 py-2.5 text-center">Calls / Target</th>
						<th class="px-3.5 py-2.5 text-center">Connect %</th>
						<th class="px-3.5 py-2.5 text-center">Won</th>
						<th class="px-3.5 py-2.5 text-center">Streak</th>
						<th class="px-3.5 py-2.5 text-right">Action</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-border/40">
					{#if filteredReps.length === 0}
						<tr>
							<td colspan="7" class="text-center py-8 text-muted-foreground text-xs">
								No subordinates found.
							</td>
						</tr>
					{:else}
						{#each filteredReps as rep (rep.cleanUsername)}
							{@const medal = getMedalBadge(rep.rank)}
							{@const isCurrent = rep.cleanUsername.toLowerCase() === currentUsername.toLowerCase()}

							<tr class="hover:bg-muted/20 transition-colors {isCurrent ? 'bg-primary/5 font-medium' : ''}">
								<!-- Rank Badge -->
								<td class="px-3.5 py-2.5 text-center">
									<span class="inline-flex items-center justify-center size-5 rounded-md text-[10px] border {medal.bg}">
										{medal.label}
									</span>
								</td>

								<!-- Rep Profile -->
								<td class="px-3.5 py-2.5">
									<div class="flex items-center gap-2">
										<div class="size-6 rounded-full bg-primary/10 text-primary font-bold text-[9px] flex items-center justify-center shrink-0">
											{rep.displayName.slice(0, 2).toUpperCase()}
										</div>
										<div class="min-w-0">
											<div class="font-bold text-xs text-foreground truncate flex items-center gap-1.5">
												<span>{rep.displayName}</span>
												{#if rep.roleName}
													<span class="text-[9px] font-semibold px-1.5 py-0.2 rounded-md bg-muted text-muted-foreground border border-border/40 shrink-0">
														{rep.roleName}
													</span>
												{/if}
												{#if isCurrent}
													<span class="text-[8px] bg-primary text-primary-foreground px-1 py-0.2 rounded font-bold uppercase shrink-0">
														You
													</span>
												{/if}
											</div>
											<span class="text-[10px] text-muted-foreground block truncate">
												{rep.jobTitle || rep.cleanUsername}
											</span>
										</div>
									</div>
								</td>

								<!-- Calls / Target Progress -->
								<td class="px-3.5 py-2.5 text-center">
									<div class="flex flex-col items-center gap-1">
										<div class="flex items-baseline gap-1 font-mono text-[11px]">
											<span class="font-bold text-foreground">{rep.totalCalls}</span>
											<span class="text-[9px] text-muted-foreground">/ {rep.effectiveTarget}</span>
											<span class="text-[9px] font-semibold text-emerald-600 dark:text-emerald-400">
												({rep.attainmentPct}%)
											</span>
										</div>
										<div class="w-16 h-1 rounded-full bg-muted overflow-hidden">
											<div
												class="h-full rounded-full transition-all duration-300 {rep.attainmentPct >= 100 ? 'bg-emerald-500' : 'bg-primary'}"
												style="width: {Math.min(100, rep.attainmentPct)}%;"
											></div>
										</div>
									</div>
								</td>

								<!-- Connect Rate -->
								<td class="px-3.5 py-2.5 text-center font-mono">
									<span class="font-bold text-xs text-foreground">{rep.connectRate}%</span>
									<span class="text-[9px] text-muted-foreground block">
										{rep.connectedCalls} reached
									</span>
								</td>

								<!-- Positive Won -->
								<td class="px-3.5 py-2.5 text-center font-mono">
									<span class="font-bold text-xs text-emerald-600 dark:text-emerald-400">
										{rep.positiveCalls}
									</span>
								</td>

								<!-- Streak -->
								<td class="px-3.5 py-2.5 text-center font-mono">
									{#if rep.streakDays > 0}
										<span class="text-[10px] font-bold text-amber-500">
											🔥 {rep.streakDays}d
										</span>
									{:else}
										<span class="text-muted-foreground text-[10px]">—</span>
									{/if}
								</td>

								<!-- Action -->
								<td class="px-3.5 py-2.5 text-right">
									<Button
										variant="ghost"
										size="sm"
										class="h-6 text-[11px] px-2 rounded-md text-primary hover:bg-primary/10 gap-0.5"
										onclick={() => onInspectSubordinate?.(rep)}
									>
										<span>Inspect</span>
										<Icon name="chevron-right" class="size-3" />
									</Button>
								</td>
							</tr>
						{/each}
					{/if}
				</tbody>
			</table>
		</div>
	</Card.Content>
</Card.Root>
