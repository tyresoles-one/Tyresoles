<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { fade, slide } from 'svelte/transition';
	import { authStore, getUser } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { Icon } from '$lib/components/venUI/icon';
	import { Badge } from '$lib/components/ui/badge';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';

	import {
		fetchSalesPerformanceData,
		clearPerformanceCache,
		parseTeamTerritoryInfo,
		calculateWorkingDays,
		formatINR,
		formatLakhs,
		formatCompact,
		type SalesPerformanceTrackerData,
		type SalesPerformanceMetrics,
		type PerformanceStatus
	} from '../api/salesPerformanceApi';

	let { class: className = '' }: { class?: string } = $props();

	const user = getUser();
	const userWorkDate = $derived($authStore.user?.workDate);
	const userRespCenter = $derived($authStore.user?.respCenter || 'BEL');
	const userEntityCode = $derived($authStore.user?.entityCode || '');
	const currentUser = $derived({
		entityCode: $authStore.user?.entityCode,
		entityType: $authStore.user?.entityType,
		department: $authStore.user?.department,
		username: $authStore.username,
		userId: $authStore.user?.userId,
		respCenter: $authStore.user?.respCenter,
		fullName: $authStore.user?.fullName,
		userType: $authStore.user?.userType
	});

	// Perspective & View Mode State
	let perspective = $state<'my' | 'team'>('my');
	let teamViewMode = $state<'grid' | 'table'>('grid'); // Default to responsive Card Grid
	let selectedRespCenter = $state<string>('BEL');
	let selectedTeamCode = $state<string>('');
	let statusFilter = $state<'all' | 'active' | 'ahead' | 'on-track' | 'behind' | 'inactive'>('active');
	let searchQuery = $state<string>('');
	let sortBy = $state<'target' | 'achieved' | 'rrr' | 'attainment'>('target');
	let sortAsc = $state<boolean>(false);

	let loading = $state<boolean>(true);
	let error = $state<string | null>(null);
	let data = $state<SalesPerformanceTrackerData | null>(null);
	let abortCtrl: AbortController | null = null;

	// Active Focus Metrics for "My Sales"
	const activeFocusMetrics = $derived.by<SalesPerformanceMetrics | null>(() => {
		if (!data) return null;
		if (perspective === 'team') return data.teamSummary;
		if (selectedTeamCode) {
			const match = data.teams.find((t) => t.teamCode.toUpperCase() === selectedTeamCode.toUpperCase());
			if (match) return match;
		}
		return data.myPerformance || data.teamSummary;
	});

	// Dynamic counts for status filter tabs
	const teamCounts = $derived.by(() => {
		const teams = data?.teams || [];
		const total = teams.length;
		const active = teams.filter((t) => t.status !== 'inactive').length;
		const inactive = teams.filter((t) => t.status === 'inactive').length;
		const ahead = teams.filter((t) => t.status === 'ahead').length;
		const onTrack = teams.filter((t) => t.status === 'on-track').length;
		const behind = teams.filter((t) => t.status === 'behind' || t.status === 'critical').length;
		return { total, active, inactive, ahead, onTrack, behind };
	});

	// Filtered & Sorted Teams for "Team Overview"
	const filteredTeams = $derived.by(() => {
		if (!data?.teams) return [];
		let list = [...data.teams];

		// Status filter
		if (statusFilter === 'active') {
			list = list.filter((t) => t.status !== 'inactive');
		} else if (statusFilter === 'inactive') {
			list = list.filter((t) => t.status === 'inactive');
		} else if (statusFilter === 'ahead') {
			list = list.filter((t) => t.status === 'ahead');
		} else if (statusFilter === 'on-track') {
			list = list.filter((t) => t.status === 'on-track');
		} else if (statusFilter === 'behind') {
			list = list.filter((t) => t.status === 'behind' || t.status === 'critical');
		}

		// Search query (matches team code, team name, rep name)
		if (searchQuery.trim()) {
			const q = searchQuery.toLowerCase().trim();
			list = list.filter(
				(t) =>
					t.teamCode.toLowerCase().includes(q) ||
					t.teamName.toLowerCase().includes(q) ||
					(t.assignedPersonName && t.assignedPersonName.toLowerCase().includes(q))
			);
		}

		// Sorting
		list.sort((a, b) => {
			let valA = 0;
			let valB = 0;
			switch (sortBy) {
				case 'target':
					valA = a.target;
					valB = b.target;
					break;
				case 'achieved':
					valA = a.currentSale;
					valB = b.currentSale;
					break;
				case 'rrr':
					valA = a.requiredRunRate;
					valB = b.requiredRunRate;
					break;
				case 'attainment':
					valA = a.achievementPct;
					valB = b.achievementPct;
					break;
			}
			return sortAsc ? valA - valB : valB - valA;
		});

		return list;
	});

	// Teams with active targets or sales
	const activeTeamsList = $derived(
		data?.teams?.filter((t) => t.target > 0 || t.currentSale > 0) || []
	);

	// Load Data with caching & optional force refresh
	async function loadPerformanceData(forceRefresh = false) {
		if (abortCtrl) abortCtrl.abort();
		abortCtrl = new AbortController();

		if (forceRefresh) {
			clearPerformanceCache();
		}

		loading = true;
		error = null;

		try {
			const res = await fetchSalesPerformanceData({
				respCenter: selectedRespCenter,
				workDate: userWorkDate,
				preferredTeamCode: selectedTeamCode || userEntityCode,
				user: currentUser,
				forceRefresh,
				signal: abortCtrl.signal
			});

			if (res.success && res.data) {
				data = res.data;
				if (res.data.teams.length > 0) {
					const isCurrentValid = res.data.teams.some(
						(t) => t.teamCode.toUpperCase() === selectedTeamCode?.toUpperCase()
					);
					if (!isCurrentValid && res.data.myPerformance) {
						selectedTeamCode = res.data.myPerformance.teamCode;
					}
				} else {
					selectedTeamCode = '';
				}
			} else if (res.error) {
				error = res.error;
			}
		} catch (e: any) {
			if (e.name !== 'AbortError') {
				error = e.message || 'Failed to load sales targets and pacing.';
				toast.error(error!);
			}
		} finally {
			loading = false;
		}
	}

	function handleSort(column: 'target' | 'achieved' | 'rrr' | 'attainment') {
		if (sortBy === column) {
			sortAsc = !sortAsc;
		} else {
			sortBy = column;
			sortAsc = false;
		}
	}

	function selectTeamToFocus(teamCode: string) {
		selectedTeamCode = teamCode;
		perspective = 'my';
	}

	onMount(() => {
		if (userRespCenter) {
			selectedRespCenter = userRespCenter;
		}
		loadPerformanceData();
	});

	$effect(() => {
		const _rc = selectedRespCenter;
		untrack(() => {
			loadPerformanceData();
		});
	});
</script>

<div class="rounded-2xl border border-border/70 bg-card/95 backdrop-blur-md shadow-xs overflow-hidden transition-all {className}">
	<!-- ─── 1. MOBILE-FIRST RESPONSIVE HEADER BAR ───────────────────────────────── -->
	<div class="px-3.5 py-3 sm:px-5 sm:py-3.5 border-b border-border/60 bg-muted/20 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
		<!-- Left: Title, Live Workdate & Badge Indicator -->
		<div class="flex items-center justify-between sm:justify-start gap-2.5">
			<div class="flex items-center gap-2.5">
				<div class="size-8 rounded-xl bg-primary/10 text-primary flex items-center justify-center font-bold shadow-2xs">
					<Icon name="crosshair" class="size-4 text-primary" />
				</div>
				<div>
					<div class="flex items-center gap-2">
						<h3 class="text-sm sm:text-base font-bold tracking-tight text-foreground leading-none">
							Sales Target & Run Rate Tracker
						</h3>
						{#if loading}
							<Icon name="loader-2" class="size-3.5 animate-spin text-primary" />
						{/if}
					</div>
					{#if data?.workingDays}
						<p class="text-[11px] text-muted-foreground mt-1 flex items-center gap-1.5 font-medium">
							<span>{data.workingDays.monthName}</span>
							<span>•</span>
							<span class="text-foreground/80 font-semibold">{data.workingDays.daysRemaining} working days left</span>
						</p>
					{/if}
				</div>
			</div>
		</div>

		<!-- Right: View Mode Perspective Switcher + Hard Refresh -->
		<div class="flex items-center justify-between sm:justify-end gap-2 shrink-0">
			<!-- Segmented Perspective Switcher (My Sales vs Team Sales) -->
			<div class="flex items-center p-1 rounded-xl bg-muted/80 border border-border/70 text-xs font-semibold shadow-2xs w-full sm:w-auto">
				<button
					type="button"
					onclick={() => (perspective = 'my')}
					class="flex-1 sm:flex-initial flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg transition-all text-xs font-bold {perspective === 'my'
						? 'bg-background text-foreground shadow-xs'
						: 'text-muted-foreground hover:text-foreground'}"
				>
					<Icon name="user" class="size-3.5 text-primary" />
					<span>My Sales</span>
				</button>
				<button
					type="button"
					onclick={() => (perspective = 'team')}
					class="flex-1 sm:flex-initial flex items-center justify-center gap-1.5 px-3 py-1.5 rounded-lg transition-all text-xs font-bold {perspective === 'team'
						? 'bg-background text-foreground shadow-xs'
						: 'text-muted-foreground hover:text-foreground'}"
				>
					<Icon name="users" class="size-3.5 text-primary" />
					<span>Team Sales ({data?.teams?.length || 0})</span>
				</button>
			</div>

			<!-- Fast Reload Button (clears cache & refetches instantly) -->
			<Button
				variant="outline"
				size="sm"
				class="h-8.5 w-8.5 p-0 shrink-0 rounded-xl"
				onclick={() => loadPerformanceData(true)}
				disabled={loading}
				title="Fast Refresh Sales Targets & Actuals"
			>
				<Icon name="rotate-ccw" class="size-3.5 {loading ? 'animate-spin text-primary' : ''}" />
			</Button>
		</div>
	</div>

	<!-- ─── 2. ACTIVE TERRITORY CARD STRIP (MY VIEW ONLY) ───────────────────────── -->
	{#if perspective === 'my' && (data?.teams?.length || 0) > 0}
		<div class="px-3.5 py-2.5 sm:px-5 sm:py-3 border-b border-border/40 bg-muted/10 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
			<!-- Territory Info & Selector -->
			<div class="flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-3 flex-1 min-w-0">
				<span class="text-muted-foreground font-semibold uppercase tracking-wider text-[11px] shrink-0">
					Active Territory:
				</span>

				<div class="relative w-full max-w-xl">
					<select
						bind:value={selectedTeamCode}
						class="w-full h-8.5 pl-3 pr-8 text-xs rounded-xl border border-input bg-background font-medium text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 appearance-none truncate cursor-pointer shadow-2xs"
					>
						{#each (data?.teams || []) as t}
							{@const parsed = parseTeamTerritoryInfo(t.teamCode, t.teamName, t.assignedPersonName)}
							<option value={t.teamCode}>
								{parsed.cleanTownTitle || t.teamName} ({t.teamCode}) — {parsed.personName} • {t.status === 'inactive' ? 'Inactive' : `Quota: ${formatLakhs(t.target)}`}
							</option>
						{/each}
					</select>
					<Icon
						name="chevron-down"
						class="size-3.5 text-muted-foreground absolute right-2.5 top-1/2 -translate-y-1/2 pointer-events-none"
					/>
				</div>
			</div>

			<!-- Quick Target vs Billed Summary Badges -->
			{#if activeFocusMetrics}
				<div class="flex flex-wrap items-center gap-2 sm:gap-3 font-mono text-[11px] shrink-0">
					<div class="px-2.5 py-1 rounded-lg bg-background border border-border/60 shadow-2xs flex items-center gap-1.5">
						<span class="text-muted-foreground font-sans">Monthly Quota:</span>
						<strong class="text-foreground">{formatINR(activeFocusMetrics.target)}</strong>
					</div>
					<div class="px-2.5 py-1 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-emerald-700 dark:text-emerald-400 font-semibold flex items-center gap-1.5 shadow-2xs">
						<span class="font-sans">Billed:</span>
						<span>{formatINR(activeFocusMetrics.currentSale)}</span>
					</div>
				</div>
			{/if}
		</div>
	{/if}

	<!-- ─── 3. HIGH-DENSITY, BEGINNER-FRIENDLY KPI COCKPIT (4 CARDS) ─────────────── -->
	<div class="p-3.5 sm:p-5 bg-background">
		{#if activeFocusMetrics}
			<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4">
				<!-- Card 1: Monthly Target & Planned Daily Pace -->
				<div class="rounded-2xl border border-border/70 bg-card p-4 shadow-xs flex flex-col justify-between hover:border-border transition-all">
					<div>
						<div class="flex items-center justify-between gap-1.5">
							<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
								<span class="text-base">🎯</span>
								Monthly Target
							</span>
							<Badge variant="outline" class="font-mono text-[10px] px-2 py-0.5 font-bold bg-muted/60">
								{formatLakhs(activeFocusMetrics.target)}
							</Badge>
						</div>

						<div class="my-3">
							<div class="text-2xl sm:text-3xl font-extrabold font-mono tracking-tight text-foreground">
								{formatINR(activeFocusMetrics.target)}
							</div>
							<p class="text-[11px] text-muted-foreground mt-0.5">
								Full month quota for {data?.workingDays?.monthName || 'month'}
							</p>
						</div>
					</div>

					<div class="pt-2.5 border-t border-border/40 text-[11px] text-muted-foreground flex items-center justify-between">
						<span>Daily Base Target:</span>
						<span class="font-mono font-bold text-foreground">
							{formatCompact(activeFocusMetrics.dailyTarget)} / day
						</span>
					</div>
				</div>

				<!-- Card 2: Sales Achieved So Far & Pacing Delta -->
				<div class="rounded-2xl border border-border/70 bg-card p-4 shadow-xs flex flex-col justify-between hover:border-border transition-all">
					<div>
						<div class="flex items-center justify-between gap-1.5">
							<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
								<span class="text-base">💰</span>
								Sales Achieved (MTD)
							</span>
							<Badge
								variant="secondary"
								class="font-mono text-[10px] px-2 py-0.5 font-bold {activeFocusMetrics.achievementPct >= (data?.workingDays?.workDaysElapsedPct || 0)
									? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/20'
									: 'bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/20'}"
							>
								{activeFocusMetrics.achievementPct.toFixed(1)}% Billed
							</Badge>
						</div>

						<div class="my-3">
							<div class="text-2xl sm:text-3xl font-extrabold font-mono tracking-tight text-emerald-600 dark:text-emerald-400">
								{formatINR(activeFocusMetrics.currentSale)}
							</div>
							<p class="text-[11px] text-muted-foreground mt-0.5">
								Invoiced sales achieved so far
							</p>
						</div>
					</div>

					<div class="pt-2.5 border-t border-border/40 text-[11px] text-muted-foreground flex items-center justify-between font-mono">
						<span>Pace vs Plan:</span>
						{#if activeFocusMetrics.status === 'inactive'}
							<span class="font-semibold text-muted-foreground">No Target Active</span>
						{:else if activeFocusMetrics.paceDelta >= 0}
							<span class="font-bold text-emerald-600 dark:text-emerald-400 flex items-center gap-0.5">
								<Icon name="arrow-up-right" class="size-3.5" />
								+{formatCompact(activeFocusMetrics.paceDelta)} ahead
							</span>
						{:else}
							<span class="font-bold text-rose-500 flex items-center gap-0.5">
								<Icon name="arrow-down-right" class="size-3.5" />
								{formatCompact(activeFocusMetrics.paceDelta)} behind
							</span>
						{/if}
					</div>
				</div>

				<!-- Card 3: Required Run Rate (RRR) - The Central Hero Milestone -->
				<div class="rounded-2xl border-2 border-primary/30 bg-primary/[0.03] p-4 shadow-xs flex flex-col justify-between relative overflow-hidden hover:border-primary/50 transition-all">
					<div>
						<div class="flex items-center justify-between gap-1.5">
							<span class="text-[11px] font-bold uppercase tracking-wider text-primary flex items-center gap-1.5">
								<span class="text-base">⚡</span>
								Daily Required Pace
							</span>
							{#if activeFocusMetrics.status === 'inactive'}
								<Badge variant="outline" class="bg-muted text-muted-foreground border-border/80 text-[10px] px-2 py-0.5 font-bold">
									Inactive
								</Badge>
							{:else if activeFocusMetrics.status === 'achieved'}
								<Badge variant="secondary" class="bg-emerald-500 text-white text-[10px] px-2 py-0.5 font-bold shadow-2xs">
									Met 🎯
								</Badge>
							{:else if activeFocusMetrics.status === 'ahead'}
								<Badge variant="secondary" class="bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30 text-[10px] px-2 py-0.5 font-bold">
									Ahead ⚡
								</Badge>
							{:else if activeFocusMetrics.status === 'on-track'}
								<Badge variant="outline" class="bg-teal-500/10 text-teal-600 dark:text-teal-400 border-teal-500/30 text-[10px] px-2 py-0.5 font-bold">
									On Track
								</Badge>
							{:else if activeFocusMetrics.status === 'behind'}
								<Badge variant="outline" class="bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30 text-[10px] px-2 py-0.5 font-bold">
									Needs Push
								</Badge>
							{:else}
								<Badge variant="outline" class="bg-rose-500/15 text-rose-600 border-rose-500/30 text-[10px] px-2 py-0.5 font-bold">
									High Gap
								</Badge>
							{/if}
						</div>

						<div class="my-3">
							<div class="text-2xl sm:text-3xl font-extrabold font-mono tracking-tight text-foreground flex items-baseline gap-1">
								<span>{formatINR(activeFocusMetrics.requiredRunRate)}</span>
								<span class="text-xs font-semibold text-muted-foreground font-sans">/ day</span>
							</div>
							<p class="text-[11px] text-muted-foreground mt-0.5">
								Must invoice daily for remaining {data?.workingDays?.daysRemaining || 0} days
							</p>
						</div>
					</div>

					<div class="pt-2.5 border-t border-primary/20 text-[11px] text-muted-foreground flex items-center justify-between font-mono">
						<span>Current Pace:</span>
						<span class="font-bold text-foreground">
							{formatCompact(activeFocusMetrics.currentRunRate)} / day
						</span>
					</div>
				</div>

				<!-- Card 4: Working Days Timeline & Visual Progress Track -->
				<div class="rounded-2xl border border-border/70 bg-card p-4 shadow-xs flex flex-col justify-between hover:border-border transition-all">
					<div>
						<div class="flex items-center justify-between gap-1.5">
							<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
								<span class="text-base">📅</span>
								Timeline & Quota
							</span>
							<span class="font-mono text-[10px] font-bold text-primary px-2 py-0.5 rounded-md bg-primary/10">
								{data?.workingDays?.daysRemaining} days left
							</span>
						</div>

						<!-- Dual Progress Bars: Days Spent vs Quota Achieved -->
						<div class="my-3 space-y-2">
							<div class="space-y-1">
								<div class="flex justify-between text-[10px] font-mono text-muted-foreground">
									<span>Working Days Elapsed:</span>
									<span class="font-semibold text-foreground">{data?.workingDays?.workDaysElapsedPct.toFixed(0)}%</span>
								</div>
								<div class="h-2 w-full rounded-full bg-muted/60 overflow-hidden">
									<div
										class="h-full bg-sky-500 rounded-full transition-all duration-500"
										style="width: {Math.min(100, data?.workingDays?.workDaysElapsedPct || 0)}%"
									></div>
								</div>
							</div>

							<div class="space-y-1">
								<div class="flex justify-between text-[10px] font-mono text-muted-foreground">
									<span>Sales Target Billed:</span>
									<span class="font-bold text-foreground">{activeFocusMetrics.achievementPct.toFixed(1)}%</span>
								</div>
								<div class="h-2 w-full rounded-full bg-muted/60 overflow-hidden">
									<div
										class="h-full rounded-full transition-all duration-500 {activeFocusMetrics.achievementPct >= (data?.workingDays?.workDaysElapsedPct || 0)
											? 'bg-emerald-500'
											: 'bg-amber-500'}"
										style="width: {Math.min(100, activeFocusMetrics.achievementPct)}%"
									></div>
								</div>
							</div>
						</div>
					</div>

					<div class="pt-2.5 border-t border-border/40 text-[11px] text-muted-foreground flex items-center justify-between font-mono">
						<span>Remaining Target:</span>
						<strong class="text-foreground">{formatCompact(activeFocusMetrics.remainingTarget)}</strong>
					</div>
				</div>
			</div>
		{/if}

		<!-- ─── 4. VIEW PERSPECTIVE CONTENT ──────────────────────────────────────── -->
		{#if perspective === 'my' && activeFocusMetrics}
			<!-- MY SALES: BEGINNER-FRIENDLY ACTIONABLE COACHING BANNER -->
			<div class="mt-4 p-4 rounded-2xl border border-border/60 bg-muted/20 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
				<div class="flex flex-wrap items-center gap-2.5">
					<div class="size-6 rounded-lg bg-primary/10 text-primary flex items-center justify-center font-bold">
						<Icon name="trending-up" class="size-3.5 text-primary" />
					</div>

					<span class="font-bold text-foreground">Action Guidance:</span>

					<!-- Clear, human status pill -->
					{#if activeFocusMetrics.status === 'inactive'}
						<span class="px-2.5 py-1 rounded-lg font-bold bg-muted text-muted-foreground border border-border/80">
							💤 Inactive Team: Currently no monthly quota set. Select an active territory above to track live run rates.
						</span>
					{:else if activeFocusMetrics.status === 'achieved'}
						<span class="px-2.5 py-1 rounded-lg font-bold bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/25">
							🎉 Target 100% Achieved! Extra invoicing counts towards bonus growth.
						</span>
					{:else if activeFocusMetrics.status === 'ahead'}
						<span class="px-2.5 py-1 rounded-lg font-bold bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/25 font-mono">
							⚡ Running Ahead (+{formatCompact(activeFocusMetrics.paceDelta)} surplus). Maintain pace to finish early!
						</span>
					{:else if activeFocusMetrics.status === 'on-track'}
						<span class="px-2.5 py-1 rounded-lg font-bold bg-teal-500/15 text-teal-600 dark:text-teal-400 border border-teal-500/25 font-mono">
							⏱️ Healthy Pacing: Keep billing {formatCompact(activeFocusMetrics.requiredRunRate)}/day to comfortably reach quota.
						</span>
					{:else}
						<span class="px-2.5 py-1 rounded-lg font-bold bg-amber-500/15 text-amber-600 dark:text-amber-400 border border-amber-500/25 font-mono">
							⚠️ Step Up Needed: Target {formatINR(activeFocusMetrics.requiredRunRate)}/day over remaining {data?.workingDays?.daysRemaining || 0} days to bridge the {formatCompact(Math.abs(activeFocusMetrics.paceDelta))} gap.
						</span>
					{/if}
				</div>

				<div class="flex items-center gap-4 font-mono text-[11px] text-muted-foreground shrink-0">
					<div>
						<span>Projected Month-End:</span>
						<strong class="ml-1 text-foreground">{formatCompact(activeFocusMetrics.projectedEomSale)}</strong>
					</div>
					<div>
						<span>Speedup Needed:</span>
						<strong class="ml-1 text-primary">
							{activeFocusMetrics.runRatePressure.toFixed(2)}x
						</strong>
					</div>
				</div>
			</div>
		{:else if perspective === 'team'}
			<!-- ─── 5. TEAM SALES: RESPONSIVE CARD GRID + SEARCH TOOLBAR ──────────────── -->
			<div class="mt-5 space-y-4">
				<!-- Mobile-First Filter & Search Bar -->
				<div class="p-3 bg-muted/20 border border-border/70 rounded-2xl flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
					<!-- Search & Status Chips -->
					<div class="flex flex-wrap items-center gap-2 flex-1">
						<div class="relative w-full sm:w-64">
							<Icon name="search" class="size-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
							<Input
								type="text"
								placeholder="Search team, town, salesperson..."
								bind:value={searchQuery}
								class="h-8.5 pl-8.5 text-xs font-medium rounded-xl"
							/>
						</div>

						<!-- Status Filter Chips (With clear Active vs Inactive separation) -->
						<div class="flex flex-wrap items-center gap-1 text-[11px]">
							<button
								type="button"
								onclick={() => (statusFilter = 'active')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'active'
									? 'bg-foreground text-background border-foreground font-bold'
									: 'bg-background border-border/70 text-muted-foreground hover:text-foreground'}"
							>
								Active ({teamCounts.active})
							</button>
							<button
								type="button"
								onclick={() => (statusFilter = 'all')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'all'
									? 'bg-foreground text-background border-foreground font-bold'
									: 'bg-background border-border/70 text-muted-foreground hover:text-foreground'}"
							>
								All ({teamCounts.total})
							</button>
							<button
								type="button"
								onclick={() => (statusFilter = 'ahead')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'ahead'
									? 'bg-emerald-600 text-white border-emerald-600 font-bold'
									: 'bg-background border-border/70 text-emerald-600 hover:bg-emerald-500/10'}"
							>
								Ahead ⚡ ({teamCounts.ahead})
							</button>
							<button
								type="button"
								onclick={() => (statusFilter = 'on-track')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'on-track'
									? 'bg-teal-600 text-white border-teal-600 font-bold'
									: 'bg-background border-border/70 text-teal-600 hover:bg-teal-500/10'}"
							>
								On Track ({teamCounts.onTrack})
							</button>
							<button
								type="button"
								onclick={() => (statusFilter = 'behind')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'behind'
									? 'bg-amber-600 text-white border-amber-600 font-bold'
									: 'bg-background border-border/70 text-amber-600 hover:bg-amber-500/10'}"
							>
								Needs Push ({teamCounts.behind})
							</button>
							<button
								type="button"
								onclick={() => (statusFilter = 'inactive')}
								class="px-2.5 py-1 rounded-lg border text-xs font-semibold transition-all {statusFilter === 'inactive'
									? 'bg-muted-foreground/80 text-background border-muted-foreground font-bold'
									: 'bg-background border-border/70 text-muted-foreground hover:text-foreground'}"
							>
								Inactive ({teamCounts.inactive})
							</button>
						</div>
					</div>

					<!-- Sort & View Switcher -->
					<div class="flex items-center justify-between md:justify-end gap-2 shrink-0">
						<!-- Sort Select -->
						<select
							bind:value={sortBy}
							class="h-8 px-2.5 text-xs rounded-xl border border-input bg-background font-medium focus:outline-none focus:ring-1 focus:ring-primary"
						>
							<option value="target">Sort: Highest Target</option>
							<option value="achieved">Sort: Most Billed</option>
							<option value="attainment">Sort: Attainment %</option>
							<option value="rrr">Sort: Run Rate Needed</option>
						</select>

						<!-- View Mode Toggle (Grid vs Table) -->
						<div class="flex items-center p-0.5 rounded-xl bg-muted/80 border border-border/70">
							<button
								type="button"
								onclick={() => (teamViewMode = 'grid')}
								class="p-1.5 rounded-lg transition-all {teamViewMode === 'grid'
									? 'bg-background text-foreground shadow-2xs font-bold'
									: 'text-muted-foreground hover:text-foreground'}"
								title="Card Grid View (Recommended for Mobile)"
							>
								<Icon name="layout-grid" class="size-3.5" />
							</button>
							<button
								type="button"
								onclick={() => (teamViewMode = 'table')}
								class="p-1.5 rounded-lg transition-all {teamViewMode === 'table'
									? 'bg-background text-foreground shadow-2xs font-bold'
									: 'text-muted-foreground hover:text-foreground'}"
								title="Compact Spreadsheet Table"
							>
								<Icon name="table" class="size-3.5" />
							</button>
						</div>
					</div>
				</div>

				<!-- ─── 5A. MOBILE-FIRST CARD GRID VIEW ───────────────────────────────── -->
				{#if teamViewMode === 'grid'}
					{#if filteredTeams.length === 0}
						<div class="p-12 text-center border border-dashed border-border/70 rounded-2xl bg-muted/10">
							<div class="size-10 rounded-full bg-muted/60 mx-auto flex items-center justify-center text-muted-foreground mb-3">
								<Icon name="search-x" class="size-5" />
							</div>
							<h4 class="text-sm font-bold text-foreground">No Teams Match Your Filter</h4>
							<p class="text-xs text-muted-foreground mt-1 max-w-sm mx-auto">
								Try switching to "All" or choosing another status filter above.
							</p>
						</div>
					{:else}
						<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-3.5">
							{#each filteredTeams as t}
								{@const parsed = parseTeamTerritoryInfo(t.teamCode, t.teamName, t.assignedPersonName)}
								<div
									role="button"
									tabindex="0"
									onclick={() => selectTeamToFocus(t.teamCode)}
									onkeydown={(e) => e.key === 'Enter' && selectTeamToFocus(t.teamCode)}
									class="group text-left rounded-2xl border border-border/70 bg-card p-4 shadow-2xs hover:border-primary/50 hover:shadow-md hover:-translate-y-0.5 transition-all cursor-pointer flex flex-col justify-between {t.status === 'inactive' ? 'opacity-75 bg-card/60 hover:opacity-100' : ''} {t.teamCode === selectedTeamCode ? 'ring-2 ring-primary border-primary' : ''}"
								>
									<!-- Card Header: Team Name (Title), Territory Avatar & Person Badge -->
									<div>
										<div class="flex items-start justify-between gap-2">
											<div class="flex items-center gap-2.5 min-w-0">
												<!-- Territory Initials Avatar -->
												<div class="size-9 rounded-xl {t.status === 'inactive' ? 'bg-muted text-muted-foreground border border-border/80' : t.isSelf ? 'bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/30' : 'bg-primary/10 text-primary border border-primary/20'} flex items-center justify-center font-bold text-xs shrink-0 font-mono">
													{parsed.teamInitials || t.teamCode.slice(0, 2)}
												</div>
												<div class="min-w-0">
													<!-- PRIMARY CARD TITLE: TEAM / TERRITORY NAME -->
													<div class="text-xs sm:text-sm font-extrabold text-foreground truncate group-hover:text-primary transition-colors flex items-center gap-1.5" title={parsed.fullTowns || t.teamName}>
														<span class="truncate">{parsed.cleanTownTitle || t.teamName}</span>
														<span class="text-[10px] px-1 py-0.2 rounded bg-muted/80 text-muted-foreground font-mono font-medium shrink-0">{t.teamCode}</span>
													</div>
													<!-- SUBTITLE: ASSIGNED SALESPERSON -->
													<div class="text-[11px] text-muted-foreground truncate flex items-center gap-1.5 mt-0.5">
														<Icon name="user" class="size-3 text-muted-foreground/70 shrink-0" />
														<span class="truncate font-medium text-foreground/80">{parsed.personName || 'Unassigned'}</span>
														{#if t.isSelf}
															<span class="text-[9px] px-1.5 py-0.2 rounded bg-emerald-500/15 text-emerald-600 font-bold border border-emerald-500/25 shrink-0">ME</span>
														{:else if parsed.personName}
															<span class="text-[9px] px-1.5 py-0.2 rounded bg-indigo-500/15 text-indigo-600 font-bold border border-indigo-500/25 shrink-0">REP</span>
														{/if}
													</div>
												</div>
											</div>

											<!-- Status Chip -->
											{#if t.status === 'inactive'}
												<Badge variant="outline" class="bg-muted text-muted-foreground border-border/80 text-[9px] px-1.5 py-0 font-bold shrink-0">
													Inactive
												</Badge>
											{:else if t.status === 'achieved'}
												<Badge variant="secondary" class="bg-emerald-500 text-white text-[9px] px-1.5 py-0 font-bold shrink-0">
													Met 🎯
												</Badge>
											{:else if t.status === 'ahead'}
												<Badge variant="secondary" class="bg-emerald-500/15 text-emerald-600 dark:text-emerald-400 border border-emerald-500/25 text-[9px] px-1.5 py-0 font-bold shrink-0">
													Ahead ⚡
												</Badge>
											{:else if t.status === 'on-track'}
												<Badge variant="outline" class="bg-teal-500/10 text-teal-600 dark:text-teal-400 border-teal-500/30 text-[9px] px-1.5 py-0 font-bold shrink-0">
													On Track
												</Badge>
											{:else if t.status === 'behind' || t.status === 'critical'}
												<Badge variant="outline" class="bg-amber-500/15 text-amber-600 dark:text-amber-400 border-amber-500/30 text-[9px] px-1.5 py-0 font-bold shrink-0">
													Push Needed
												</Badge>
											{/if}
										</div>

										<!-- Quota Progress Section: Inactive vs Active -->
										{#if t.status === 'inactive'}
											<div class="my-3 space-y-1.5 opacity-60">
												<div class="flex items-center justify-between text-[11px] font-mono text-muted-foreground">
													<span class="font-sans">Target & Billing:</span>
													<span class="font-medium italic">No Target Set</span>
												</div>
												<div class="h-2 w-full rounded-full bg-muted/70 overflow-hidden">
													<div class="h-full bg-muted-foreground/20 rounded-full" style="width: 0%"></div>
												</div>
												<div class="flex items-center justify-between text-[11px] font-mono text-muted-foreground">
													<span>₹0 Billed</span>
													<span>Target: ₹0</span>
												</div>
											</div>
										{:else}
											<div class="my-3 space-y-1.5">
												<div class="flex items-center justify-between text-[11px] font-mono">
													<span class="text-muted-foreground font-sans">Quota Billed:</span>
													<span class="font-bold text-foreground">{t.achievementPct.toFixed(1)}%</span>
												</div>
												<div class="h-2 w-full rounded-full bg-muted/60 overflow-hidden">
													<div
														class="h-full rounded-full transition-all duration-500 {t.achievementPct >= (data?.workingDays?.workDaysElapsedPct || 0)
															? 'bg-emerald-500'
															: 'bg-amber-500'}"
														style="width: {Math.min(100, t.achievementPct)}%"
													></div>
												</div>
												<div class="flex items-center justify-between text-[11px] font-mono text-muted-foreground">
													<span class="font-bold text-emerald-600 dark:text-emerald-400">{formatINR(t.currentSale)}</span>
													<span>of {formatLakhs(t.target)}</span>
												</div>
											</div>
										{/if}
									</div>

									<!-- Card Footer: Daily Run Rate Required vs Current -->
									{#if t.status === 'inactive'}
										<div class="pt-2.5 border-t border-border/40 text-[11px] font-mono text-muted-foreground flex items-center justify-between opacity-60">
											<div>
												<span class="text-[10px] block font-sans">Daily Needed:</span>
												<span>—</span>
											</div>
											<div class="text-right">
												<span class="text-[10px] block font-sans">Current Speed:</span>
												<span>—</span>
											</div>
										</div>
									{:else}
										<div class="pt-2.5 border-t border-border/40 text-[11px] font-mono flex items-center justify-between">
											<div>
												<span class="text-[10px] text-muted-foreground block font-sans">Daily Needed:</span>
												<span class="font-bold text-primary">{formatCompact(t.requiredRunRate)}/d</span>
											</div>
											<div class="text-right">
												<span class="text-[10px] text-muted-foreground block font-sans">Current Speed:</span>
												<span class="font-semibold text-foreground">{formatCompact(t.currentRunRate)}/d</span>
											</div>
										</div>
									{/if}
								</div>
							{/each}
						</div>
					{/if}

				<!-- ─── 5B. COMPACT SPREADSHEET TABLE VIEW (OPTIONAL ALTERNATE) ───────── -->
				{:else}
					<div class="border border-border/70 rounded-2xl overflow-hidden shadow-2xs">
						<div class="max-h-[420px] overflow-auto">
							<table class="w-full text-left text-xs border-collapse">
								<thead class="sticky top-0 bg-muted/95 backdrop-blur z-10 border-b border-border/60 text-[11px] font-semibold text-muted-foreground">
									<tr>
										<th class="py-2.5 px-3">Team / Salesperson</th>
										<th class="py-2.5 px-3 text-right cursor-pointer hover:text-foreground" onclick={() => handleSort('target')}>
											Monthly Quota {sortBy === 'target' ? (sortAsc ? '▲' : '▼') : ''}
										</th>
										<th class="py-2.5 px-3 text-right cursor-pointer hover:text-foreground" onclick={() => handleSort('achieved')}>
											Billed MTD {sortBy === 'achieved' ? (sortAsc ? '▲' : '▼') : ''}
										</th>
										<th class="py-2.5 px-3 text-center min-w-[120px] cursor-pointer hover:text-foreground" onclick={() => handleSort('attainment')}>
											Attainment % {sortBy === 'attainment' ? (sortAsc ? '▲' : '▼') : ''}
										</th>
										<th class="py-2.5 px-3 text-right">Current Speed</th>
										<th class="py-2.5 px-3 text-right cursor-pointer hover:text-foreground" onclick={() => handleSort('rrr')}>
											Daily Needed (RRR) {sortBy === 'rrr' ? (sortAsc ? '▲' : '▼') : ''}
										</th>
										<th class="py-2.5 px-3 text-center w-24">Status</th>
									</tr>
								</thead>
								<tbody class="divide-y divide-border/40 font-mono">
									{#if filteredTeams.length === 0}
										<tr>
											<td colspan={7} class="py-10 text-center text-muted-foreground font-sans">
												No teams found matching query.
											</td>
										</tr>
									{:else}
										{#each filteredTeams as t}
											{@const parsed = parseTeamTerritoryInfo(t.teamCode, t.teamName, t.assignedPersonName)}
											<tr
												class="hover:bg-muted/40 transition-colors cursor-pointer group {t.teamCode === selectedTeamCode ? 'bg-primary/5 font-semibold' : ''} {t.status === 'inactive' ? 'opacity-70' : ''}"
												onclick={() => selectTeamToFocus(t.teamCode)}
											>
												<!-- Team Name & Rep Subtitle -->
												<td class="py-2.5 px-3 font-sans">
													<div class="font-bold text-foreground group-hover:text-primary transition-colors flex items-center gap-1.5">
														<span class="truncate">{parsed.cleanTownTitle || t.teamName}</span>
														<span class="text-[10px] px-1 py-0.2 rounded bg-muted/80 text-muted-foreground font-mono">{t.teamCode}</span>
													</div>
													<div class="text-[11px] text-muted-foreground truncate flex items-center gap-1 mt-0.5">
														<Icon name="user" class="size-3 text-muted-foreground/70" />
														<span class="text-foreground/80 font-medium">{parsed.personName || 'Unassigned'}</span>
														{#if t.isSelf}
															<span class="text-[9px] px-1.5 py-0.2 rounded bg-emerald-500/15 text-emerald-600 font-bold border border-emerald-500/25">ME</span>
														{:else if parsed.personName}
															<span class="text-[9px] px-1.5 py-0.2 rounded bg-indigo-500/15 text-indigo-600 font-bold border border-indigo-500/25">REP</span>
														{/if}
													</div>
												</td>

												<td class="py-2.5 px-3 text-right font-medium">
													<div>{formatINR(t.target)}</div>
													<div class="text-[10px] text-muted-foreground font-sans">{formatLakhs(t.target)}</div>
												</td>

												<td class="py-2.5 px-3 text-right font-semibold text-emerald-600 dark:text-emerald-400">
													<div>{formatINR(t.currentSale)}</div>
													<div class="text-[10px] text-muted-foreground font-sans">{formatLakhs(t.currentSale)}</div>
												</td>

												<td class="py-2.5 px-3 text-center">
													{#if t.status === 'inactive'}
														<span class="text-xs text-muted-foreground">—</span>
													{:else}
														<div class="flex items-center gap-2 justify-center">
															<div class="w-16 h-1.5 rounded-full bg-muted/60 overflow-hidden">
																<div
																	class="h-full rounded-full {t.achievementPct >= (data?.workingDays?.workDaysElapsedPct || 0)
																		? 'bg-emerald-500'
																		: 'bg-amber-500'}"
																	style="width: {Math.min(100, t.achievementPct)}%"
																></div>
															</div>
															<span class="text-xs font-bold w-11 text-right">{t.achievementPct.toFixed(1)}%</span>
														</div>
													{/if}
												</td>

												<td class="py-2.5 px-3 text-right text-muted-foreground">
													{t.status === 'inactive' ? '—' : `${formatCompact(t.currentRunRate)}/d`}
												</td>

												<td class="py-2.5 px-3 text-right font-bold text-primary">
													{t.status === 'inactive' ? '—' : `${formatCompact(t.requiredRunRate)}/d`}
												</td>

												<td class="py-2.5 px-3 text-center font-sans">
													{#if t.status === 'inactive'}
														<span class="text-[10px] px-2 py-0.5 rounded-full font-bold bg-muted text-muted-foreground border border-border/80">
															Inactive
														</span>
													{:else if t.status === 'achieved'}
														<span class="text-[10px] px-2 py-0.5 rounded-full font-bold bg-emerald-500/15 text-emerald-600 border border-emerald-500/25">
															Met 🎯
														</span>
													{:else if t.status === 'ahead'}
														<span class="text-[10px] px-2 py-0.5 rounded-full font-bold bg-emerald-500/15 text-emerald-600 border border-emerald-500/25">
															Ahead ⚡
														</span>
													{:else if t.status === 'on-track'}
														<span class="text-[10px] px-2 py-0.5 rounded-full font-bold bg-teal-500/15 text-teal-600 border border-teal-500/25">
															On Track
														</span>
													{:else if t.status === 'behind' || t.status === 'critical'}
														<span class="text-[10px] px-2 py-0.5 rounded-full font-bold bg-amber-500/15 text-amber-600 border border-amber-500/25">
															Push ⚠️
														</span>
													{/if}
												</td>
											</tr>
										{/each}
									{/if}
								</tbody>
							</table>
						</div>
					</div>
				{/if}

				<!-- Combined Team Overview Strip -->
				{#if data?.teamSummary}
					<div class="px-4 py-3 bg-muted/30 border border-border/70 rounded-2xl flex flex-wrap items-center justify-between gap-3 text-xs font-mono shadow-2xs">
						<span class="font-sans font-bold text-foreground flex items-center gap-1.5">
							<Icon name="users" class="size-4 text-primary" />
							Total Authorized Team Aggregate:
						</span>
						<div class="flex flex-wrap items-center gap-4 sm:gap-6">
							<div>
								<span class="text-muted-foreground font-sans">Combined Quota:</span>
								<strong class="ml-1 text-foreground">{formatINR(data.teamSummary.target)}</strong>
							</div>
							<div>
								<span class="text-muted-foreground font-sans">Billed So Far:</span>
								<strong class="ml-1 text-emerald-600 dark:text-emerald-400">{formatINR(data.teamSummary.currentSale)}</strong>
							</div>
							<div>
								<span class="text-muted-foreground font-sans">Daily Run Rate Needed:</span>
								<strong class="ml-1 text-primary">{formatCompact(data.teamSummary.requiredRunRate)}/d</strong>
							</div>
						</div>
					</div>
				{/if}
			</div>
		{/if}
	</div>
</div>
