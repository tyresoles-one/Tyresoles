<script lang="ts">
	import { onMount } from 'svelte';
	import { fade, slide } from 'svelte/transition';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { authStore } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery } from '$lib/services/graphql';
	import { GetSalesHierarchyDocument, cleanUsername } from '../api/salesCrmDashboardApi';
	import type {
		SalesHierarchySummary,
		SubordinateSalesperson,
		SubordinateRep
	} from '../api/types';

	let {
		employeeCode = '',
		initialData = null,
		leaderboardReps = [],
		onInspectRep,
		onSelectRep,
		class: className = ''
	}: {
		employeeCode?: string;
		initialData?: SalesHierarchySummary | null;
		leaderboardReps?: SubordinateRep[];
		onInspectRep?: (rep: SubordinateRep) => void;
		onSelectRep?: (code: string) => void;
		class?: string;
	} = $props();

	// Effective base user
	const myCleanUsername = $derived(cleanUsername($authStore.username || ''));
	const baseEmployeeCode = $derived(employeeCode ? cleanUsername(employeeCode) : myCleanUsername);

	// Navigation & Root state
	let currentRootCode = $state<string>('');
	let breadcrumbTrail = $state<Array<{ code: string; name: string; roleName: string }>>([]);

	// Data state
	let hierarchyData = $state<SalesHierarchySummary | null>(null);
	let loading = $state<boolean>(false);
	let error = $state<string | null>(null);

	// View filters & controls
	let searchQuery = $state<string>('');
	let selectedRoleFilter = $state<number | 'all'>('all');
	let viewMode = $state<'tree' | 'teams' | 'grid'>('tree');
	let collapsedTiers = $state<Record<string, boolean>>({});

	// Quick Lookup for Leaderboard CRM metrics
	const repMetricsMap = $derived.by(() => {
		const map = new Map<string, SubordinateRep>();
		for (const rep of leaderboardReps) {
			map.set(rep.cleanUsername.toLowerCase(), rep);
			if (rep.employeeNo) map.set(rep.employeeNo.toLowerCase(), rep);
		}
		return map;
	});

	// Role Metadata definition
	const ROLE_CONFIG: Record<
		number,
		{
			name: string;
			label: string;
			badgeClass: string;
			accentClass: string;
			icon: string;
			tierOrder: number;
		}
	> = {
		4: {
			name: 'UnitHead',
			label: 'Unit Head',
			badgeClass: 'bg-purple-500/15 text-purple-700 dark:text-purple-300 border-purple-500/30',
			accentClass: 'border-l-purple-500',
			icon: 'shield',
			tierOrder: 0
		},
		3: {
			name: 'RegionManager',
			label: 'Region Manager',
			badgeClass: 'bg-blue-500/15 text-blue-700 dark:text-blue-300 border-blue-500/30',
			accentClass: 'border-l-blue-500',
			icon: 'map-pin',
			tierOrder: 1
		},
		2: {
			name: 'ZoneManager',
			label: 'Zone Manager',
			badgeClass: 'bg-cyan-500/15 text-cyan-700 dark:text-cyan-300 border-cyan-500/30',
			accentClass: 'border-l-cyan-500',
			icon: 'compass',
			tierOrder: 2
		},
		1: {
			name: 'AreaManager',
			label: 'Area Manager',
			badgeClass: 'bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30',
			accentClass: 'border-l-amber-500',
			icon: 'briefcase',
			tierOrder: 3
		},
		0: {
			name: 'Salesman',
			label: 'Sales Executive',
			badgeClass: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30',
			accentClass: 'border-l-emerald-500',
			icon: 'user-check',
			tierOrder: 4
		}
	};

	function getRoleConfig(roleType: number) {
		return (
			ROLE_CONFIG[roleType] || {
				name: 'Unknown',
				label: 'Sales Role',
				badgeClass: 'bg-slate-500/15 text-slate-700 dark:text-slate-300 border-slate-500/30',
				accentClass: 'border-l-slate-500',
				icon: 'user',
				tierOrder: 5
			}
		);
	}

	// Fetch Hierarchy for an employee code
	async function fetchHierarchy(code: string) {
		const targetCode = cleanUsername(code);
		if (!targetCode) return;

		// Use initialData if matching base user
		if (initialData && targetCode.toLowerCase() === baseEmployeeCode.toLowerCase()) {
			hierarchyData = initialData;
			return;
		}

		loading = true;
		error = null;
		try {
			const res = await graphqlQuery<{ salesHierarchy: SalesHierarchySummary }>(
				GetSalesHierarchyDocument,
				{ variables: { employeeCode: targetCode } }
			);

			if (res.success && res.data?.salesHierarchy) {
				hierarchyData = res.data.salesHierarchy;
			} else {
				error = res.errors?.[0]?.message || 'Failed to load organizational hierarchy';
				toast.error(error!);
			}
		} catch (err: any) {
			error = err.message || 'Error querying sales hierarchy';
			toast.error(error!);
		} finally {
			loading = false;
		}
	}

	// Drill down into a subordinate manager's tree
	function drillDown(sub: SubordinateSalesperson) {
		if (!hierarchyData) return;
		breadcrumbTrail = [
			...breadcrumbTrail,
			{
				code: hierarchyData.supervisorCode,
				name: hierarchyData.supervisorName,
				roleName: hierarchyData.supervisorRoleName
			}
		];
		currentRootCode = sub.code;
		fetchHierarchy(sub.code);
	}

	// Navigate back to a breadcrumb item
	function navigateToBreadcrumb(targetIndex: number) {
		if (targetIndex < 0) {
			// Reset to base root
			breadcrumbTrail = [];
			currentRootCode = baseEmployeeCode;
			fetchHierarchy(baseEmployeeCode);
			return;
		}

		const target = breadcrumbTrail[targetIndex];
		breadcrumbTrail = breadcrumbTrail.slice(0, targetIndex);
		currentRootCode = target.code;
		fetchHierarchy(target.code);
	}

	// Reset tree back to initial user
	function resetToTop() {
		breadcrumbTrail = [];
		currentRootCode = baseEmployeeCode;
		fetchHierarchy(baseEmployeeCode);
	}

	// Toggle collapsible tier
	function toggleTier(tierKey: string) {
		collapsedTiers[tierKey] = !collapsedTiers[tierKey];
	}

	// Filtered subordinates
	const filteredSubordinates = $derived.by(() => {
		if (!hierarchyData?.subordinates) return [];
		let list = hierarchyData.subordinates;

		if (selectedRoleFilter !== 'all') {
			list = list.filter((s) => s.roleType === selectedRoleFilter);
		}

		if (searchQuery.trim()) {
			const q = searchQuery.trim().toLowerCase();
			list = list.filter(
				(s) =>
					s.name.toLowerCase().includes(q) ||
					s.code.toLowerCase().includes(q) ||
					(s.jobTitle && s.jobTitle.toLowerCase().includes(q)) ||
					(s.mobilePhoneNo && s.mobilePhoneNo.includes(q)) ||
					s.sharedTeamCodes.some((t) => t.toLowerCase().includes(q))
			);
		}

		return list;
	});

	// Grouping by Role Tier
	const subordinatesByTier = $derived.by(() => {
		const groups = new Map<number, SubordinateSalesperson[]>();
		for (const sub of filteredSubordinates) {
			const arr = groups.get(sub.roleType) || [];
			arr.push(sub);
			groups.set(sub.roleType, arr);
		}

		// Sort tiers by hierarchy level (highest manager first: 3 -> 2 -> 1 -> 0)
		return Array.from(groups.entries())
			.sort(([a], [b]) => b - a)
			.map(([roleType, items]) => ({
				roleType,
				config: getRoleConfig(roleType),
				items
			}));
	});

	// Grouping by Shared Team
	const subordinatesByTeam = $derived.by(() => {
		const groups = new Map<string, SubordinateSalesperson[]>();
		for (const sub of filteredSubordinates) {
			const teams = sub.sharedTeamCodes.length > 0 ? sub.sharedTeamCodes : ['Unassigned'];
			for (const t of teams) {
				const arr = groups.get(t) || [];
				if (!arr.some((x) => x.code === sub.code)) {
					arr.push(sub);
				}
				groups.set(t, arr);
			}
		}

		return Array.from(groups.entries())
			.sort(([a], [b]) => a.localeCompare(b))
			.map(([teamCode, items]) => ({
				teamCode,
				items
			}));
	});

	// Hierarchy Summary Stats
	const hierarchyStats = $derived.by(() => {
		const subs = hierarchyData?.subordinates || [];
		const managersCount = subs.filter((s) => s.roleType > 0).length;
		const repsCount = subs.filter((s) => s.roleType === 0).length;
		const teamsSet = new Set<string>();
		subs.forEach((s) => s.sharedTeamCodes.forEach((t) => teamsSet.add(t)));

		return {
			total: subs.length,
			managers: managersCount,
			salesmen: repsCount,
			teamsCount: teamsSet.size
		};
	});

	function handleInspect(sub: SubordinateSalesperson) {
		const match = repMetricsMap.get(sub.code.toLowerCase());
		if (match && onInspectRep) {
			onInspectRep(match);
		} else if (onInspectRep) {
			// Construct synthetic rep for inspector
			onInspectRep({
				username: sub.code,
				cleanUsername: sub.code,
				displayName: sub.name,
				employeeNo: sub.code,
				jobTitle: sub.jobTitle || sub.displayTitle,
				isDirectReport: true,
				totalCalls: 0,
				connectedCalls: 0,
				connectRate: 0,
				positiveCalls: 0,
				positiveRate: 0,
				revenueGenerated: 0,
				activeAllocations: 0,
				dailyTarget: 30,
				weeklyTarget: 150,
				monthlyTarget: 600,
				effectiveTarget: 30,
				attainmentPct: 0,
				streakDays: 0,
				rank: 0,
				roleType: sub.roleType,
				roleName: sub.roleName
			});
		}
	}

	onMount(() => {
		currentRootCode = baseEmployeeCode;
		if (initialData) {
			hierarchyData = initialData;
		} else {
			fetchHierarchy(baseEmployeeCode);
		}
	});

	$effect(() => {
		if (baseEmployeeCode && !currentRootCode) {
			currentRootCode = baseEmployeeCode;
			fetchHierarchy(baseEmployeeCode);
		}
	});
</script>

<div class="space-y-4 {className}">
	<!-- 1. Header Toolbar: Breadcrumbs, Search, View Toggles & Summary -->
	<div class="rounded-xl border border-border bg-card p-3 sm:p-4 shadow-xs">
		<div class="flex flex-col gap-3">
			<!-- Top Row: Title, Breadcrumbs & Reset -->
			<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2 border-b border-border/50 pb-3">
				<div class="flex items-center gap-2.5 flex-wrap">
					<div class="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary border border-primary/20 shrink-0">
						<Icon name="network" class="size-4.5" />
					</div>
					<div>
						<div class="flex items-center gap-2">
							<h2 class="text-base font-bold text-foreground tracking-tight">
								Reporting Hierarchy Tree
							</h2>
							<span class="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-muted text-muted-foreground border border-border">
								NAV Team Salesperson
							</span>
						</div>
						<!-- Mobile & Desktop Breadcrumbs -->
						{#if breadcrumbTrail.length > 0}
							<div class="flex items-center gap-1.5 text-xs text-muted-foreground mt-1 flex-wrap">
								<button
									type="button"
									onclick={() => navigateToBreadcrumb(-1)}
									class="hover:text-primary underline font-medium cursor-pointer"
								>
									Root
								</button>
								{#each breadcrumbTrail as b, idx}
									<span>/</span>
									{#if idx < breadcrumbTrail.length - 1}
										<button
											type="button"
											onclick={() => navigateToBreadcrumb(idx)}
											class="hover:text-primary underline font-medium cursor-pointer"
										>
											{b.name || b.code}
										</button>
									{:else}
										<span class="font-bold text-foreground">{b.name || b.code}</span>
									{/if}
								{/each}
								<span>/</span>
								<span class="font-bold text-primary">{hierarchyData?.supervisorName || currentRootCode}</span>
							</div>
						{:else}
							<p class="text-xs text-muted-foreground mt-0.5">
								Organizational chain of command and subordinate field team
							</p>
						{/if}
					</div>
				</div>

				<div class="flex items-center gap-2 self-end sm:self-auto">
					{#if breadcrumbTrail.length > 0}
						<Button
							variant="outline"
							size="sm"
							class="h-7 text-xs gap-1.5"
							onclick={resetToTop}
						>
							<Icon name="corner-down-left" class="size-3" />
							<span>Reset to My Org</span>
						</Button>
					{/if}

					<button
						type="button"
						onclick={() => fetchHierarchy(currentRootCode)}
						disabled={loading}
						class="inline-flex size-8 items-center justify-center rounded-lg border border-border bg-background hover:bg-muted/40 text-muted-foreground hover:text-foreground transition-all disabled:opacity-50"
						title="Refresh Hierarchy"
					>
						<Icon name="refresh-cw" class="size-3.5 {loading ? 'animate-spin' : ''}" />
					</button>
				</div>
			</div>

			<!-- Middle Row: Interactive Controls (Search + View Modes + Role Filter) -->
			<div class="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-2.5">
				<!-- Search Input (Mobile First full width) -->
				<div class="relative flex-1 min-w-[200px]">
					<Icon name="search" class="absolute left-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
					<input
						type="text"
						bind:value={searchQuery}
						placeholder="Search subordinate by name, code, phone, or team..."
						class="w-full h-8 pl-8 pr-3 text-xs rounded-lg border border-border bg-background placeholder:text-muted-foreground/70 focus:outline-none focus:ring-1 focus:ring-primary"
					/>
					{#if searchQuery}
						<button
							type="button"
							onclick={() => (searchQuery = '')}
							class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
						>
							<Icon name="x" class="size-3" />
						</button>
					{/if}
				</div>

				<!-- Right Controls: Role filter & View mode toggle -->
				<div class="flex items-center gap-2 flex-wrap">
					<!-- Role Filter Pills -->
					<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/50 text-xs">
						<button
							type="button"
							onclick={() => (selectedRoleFilter = 'all')}
							class="px-2.5 py-1 rounded-md text-[11px] font-medium transition-all {selectedRoleFilter === 'all'
								? 'bg-background text-foreground shadow-xs font-bold'
								: 'text-muted-foreground hover:text-foreground'}"
						>
							All ({hierarchyStats.total})
						</button>
						{#if hierarchyStats.managers > 0}
							<button
								type="button"
								onclick={() => (selectedRoleFilter = selectedRoleFilter === 1 ? 'all' : 1)}
								class="px-2 py-1 rounded-md text-[11px] font-medium transition-all {selectedRoleFilter === 1
									? 'bg-background text-amber-600 dark:text-amber-400 shadow-xs font-bold'
									: 'text-muted-foreground hover:text-foreground'}"
							>
								Managers ({hierarchyStats.managers})
							</button>
						{/if}
						<button
							type="button"
							onclick={() => (selectedRoleFilter = selectedRoleFilter === 0 ? 'all' : 0)}
							class="px-2 py-1 rounded-md text-[11px] font-medium transition-all {selectedRoleFilter === 0
								? 'bg-background text-emerald-600 dark:text-emerald-400 shadow-xs font-bold'
								: 'text-muted-foreground hover:text-foreground'}"
						>
							Field Reps ({hierarchyStats.salesmen})
						</button>
					</div>

					<!-- View Mode: Tree vs Teams -->
					<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/50 text-xs">
						<button
							type="button"
							onclick={() => (viewMode = 'tree')}
							class="flex items-center gap-1 px-2.5 py-1 rounded-md text-[11px] font-medium transition-all {viewMode === 'tree'
								? 'bg-background text-foreground shadow-xs font-bold'
								: 'text-muted-foreground hover:text-foreground'}"
							title="Hierarchy Level Tree View"
						>
							<Icon name="git-branch" class="size-3" />
							<span>Levels</span>
						</button>
						<button
							type="button"
							onclick={() => (viewMode = 'teams')}
							class="flex items-center gap-1 px-2.5 py-1 rounded-md text-[11px] font-medium transition-all {viewMode === 'teams'
								? 'bg-background text-foreground shadow-xs font-bold'
								: 'text-muted-foreground hover:text-foreground'}"
							title="NAV Teams Grouping"
						>
							<Icon name="users" class="size-3" />
							<span>Teams</span>
						</button>
					</div>
				</div>
			</div>

			<!-- Bottom Row: Quick Hierarchy Metric Chips -->
			<div class="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 border-t border-border/40 text-xs">
				<div class="flex items-center gap-2 p-2 rounded-lg bg-muted/30 border border-border/30">
					<div class="size-6 rounded-md bg-primary/10 text-primary flex items-center justify-center font-bold text-xs">
						{hierarchyStats.total}
					</div>
					<div class="flex flex-col">
						<span class="text-[10px] text-muted-foreground uppercase font-semibold">Total Reports</span>
						<span class="text-xs font-bold text-foreground">{hierarchyStats.total} Subordinates</span>
					</div>
				</div>

				<div class="flex items-center gap-2 p-2 rounded-lg bg-muted/30 border border-border/30">
					<div class="size-6 rounded-md bg-amber-500/10 text-amber-600 dark:text-amber-400 flex items-center justify-center font-bold text-xs">
						{hierarchyStats.managers}
					</div>
					<div class="flex flex-col">
						<span class="text-[10px] text-muted-foreground uppercase font-semibold">Leadership Tier</span>
						<span class="text-xs font-bold text-foreground">{hierarchyStats.managers} Managers</span>
					</div>
				</div>

				<div class="flex items-center gap-2 p-2 rounded-lg bg-muted/30 border border-border/30">
					<div class="size-6 rounded-md bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 flex items-center justify-center font-bold text-xs">
						{hierarchyStats.salesmen}
					</div>
					<div class="flex flex-col">
						<span class="text-[10px] text-muted-foreground uppercase font-semibold">Field Frontline</span>
						<span class="text-xs font-bold text-foreground">{hierarchyStats.salesmen} Reps</span>
					</div>
				</div>

				<div class="flex items-center gap-2 p-2 rounded-lg bg-muted/30 border border-border/30">
					<div class="size-6 rounded-md bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 flex items-center justify-center font-bold text-xs">
						{hierarchyStats.teamsCount}
					</div>
					<div class="flex flex-col">
						<span class="text-[10px] text-muted-foreground uppercase font-semibold">Shared Units</span>
						<span class="text-xs font-bold text-foreground">{hierarchyStats.teamsCount} NAV Teams</span>
					</div>
				</div>
			</div>
		</div>
	</div>

	<!-- 2. Tree Root Node: The Focal Supervisor Card -->
	{#if hierarchyData}
		{@const rootConfig = getRoleConfig(hierarchyData.supervisorMaxRoleType)}
		<div class="relative rounded-2xl border-2 border-primary/30 bg-card p-4 shadow-sm overflow-hidden">
			<!-- Subtle corner accent banner -->
			<div class="absolute top-0 right-0 h-16 w-16 bg-gradient-to-bl from-primary/15 to-transparent pointer-events-none"></div>

			<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
				<div class="flex items-center gap-3.5">
					<!-- Supervisor Initials Avatar -->
					<div class="relative size-12 rounded-2xl bg-primary/10 border-2 border-primary/20 text-primary flex items-center justify-center font-black text-base shadow-xs shrink-0">
						{(hierarchyData.supervisorName || hierarchyData.supervisorCode).slice(0, 2).toUpperCase()}
						<span class="absolute -bottom-1 -right-1 size-4 rounded-full bg-emerald-500 border-2 border-card flex items-center justify-center" title="Active">
							<span class="size-1.5 rounded-full bg-white"></span>
						</span>
					</div>

					<div class="flex flex-col">
						<div class="flex items-center gap-2 flex-wrap">
							<h3 class="text-base font-bold text-foreground">
								{hierarchyData.supervisorName || 'Leader'}
							</h3>
							<span class="text-[11px] font-bold px-2 py-0.5 rounded-full border {rootConfig.badgeClass}">
								{hierarchyData.supervisorRoleName || rootConfig.label}
							</span>
							{#if hierarchyData.supervisorCode === baseEmployeeCode}
								<span class="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-primary text-primary-foreground">
									You
								</span>
							{/if}
						</div>
						<div class="flex items-center gap-2 text-xs text-muted-foreground mt-0.5">
							<span class="font-mono font-medium">{hierarchyData.supervisorCode}</span>
							<span>•</span>
							<span>Level {hierarchyData.supervisorMaxRoleType} Leadership</span>
							<span>•</span>
							<span class="text-foreground font-semibold">
								{hierarchyData.totalSubordinatesCount} direct & downline reportees
							</span>
						</div>
					</div>
				</div>

				<!-- Quick Filter Action: View Self in Dashboard -->
				{#if onSelectRep && hierarchyData.supervisorCode === baseEmployeeCode}
					<Button
						variant="outline"
						size="sm"
						class="h-8 text-xs gap-1.5 rounded-lg border-border"
						onclick={() => onSelectRep?.('all')}
					>
						<Icon name="users" class="size-3.5" />
						<span>View Full Team Scope</span>
					</Button>
				{/if}
			</div>
		</div>

		<!-- Vertical Line Connector leading to Subordinate Branches -->
		<div class="flex justify-center -my-2 relative z-0">
			<div class="w-0.5 h-6 bg-primary/40"></div>
		</div>
	{/if}

	<!-- 3. Subordinates Tree Spine & Branch Cards (Mobile-First Responsive) -->
	{#if loading && !hierarchyData}
		<div class="rounded-xl border border-border bg-card p-8 flex flex-col items-center justify-center gap-3">
			<Icon name="loader-circle" class="size-7 text-primary animate-spin" />
			<p class="text-xs text-muted-foreground">Loading sales reporting hierarchy...</p>
		</div>
	{:else if !hierarchyData || hierarchyData.totalSubordinatesCount === 0}
		<!-- Zero subordinates state -->
		<div class="rounded-xl border border-dashed border-border bg-card/60 p-8 text-center flex flex-col items-center justify-center gap-2">
			<div class="size-12 rounded-full bg-muted flex items-center justify-center text-muted-foreground">
				<Icon name="user" class="size-6" />
			</div>
			<h4 class="text-sm font-bold text-foreground">Individual Contributor / No Subordinates</h4>
			<p class="text-xs text-muted-foreground max-w-md">
				No downline reportees were found in Microsoft Dynamics NAV `[Team Salesperson]` for employee code <span class="font-mono font-bold text-foreground">{currentRootCode}</span>.
			</p>
			{#if breadcrumbTrail.length > 0}
				<Button
					variant="outline"
					size="sm"
					class="mt-2 text-xs"
					onclick={resetToTop}
				>
					Return to My Organization
				</Button>
			{/if}
		</div>
	{:else}
		<!-- Subordinates Container -->
		<div class="relative pl-3 sm:pl-6 border-l-2 border-border/80 ml-4 sm:ml-6 space-y-6">
			{#if filteredSubordinates.length === 0}
				<div class="rounded-xl border border-dashed border-border bg-card/60 p-6 text-center text-xs text-muted-foreground">
					No subordinates match the current search or filter.
					<button
						type="button"
						class="underline text-primary ml-1 font-semibold cursor-pointer"
						onclick={() => {
							searchQuery = '';
							selectedRoleFilter = 'all';
						}}
					>
						Clear filters
					</button>
				</div>
			{:else if viewMode === 'tree'}
				<!-- VIEW MODE 1: HIERARCHY LEVELS (Tier-by-Tier Top-Down) -->
				{#each subordinatesByTier as tier (tier.roleType)}
					{@const isCollapsed = collapsedTiers[`tier-${tier.roleType}`]}
					<div class="relative space-y-3" in:fade={{ duration: 150 }}>
						<!-- Elbow Connector to Level Header -->
						<div class="absolute -left-[13px] sm:-left-[25px] top-4 w-3 sm:w-6 h-0.5 bg-border/80"></div>

						<!-- Tier Header Accordion Bar -->
						<div
							role="button"
							tabindex="0"
							onclick={() => toggleTier(`tier-${tier.roleType}`)}
							onkeydown={(e) => e.key === 'Enter' && toggleTier(`tier-${tier.roleType}`)}
							class="flex items-center justify-between p-2 sm:p-2.5 rounded-xl border border-border/80 bg-muted/40 hover:bg-muted/70 transition-all cursor-pointer select-none group"
						>
							<div class="flex items-center gap-2.5">
								<span class="size-6 rounded-md flex items-center justify-center text-xs font-bold border {tier.config.badgeClass}">
									<Icon name={tier.config.icon} class="size-3.5" />
								</span>
								<div class="flex items-center gap-2">
									<h4 class="text-xs sm:text-sm font-bold text-foreground">
										{tier.config.label}
									</h4>
									<span class="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-background border border-border text-foreground">
										{tier.items.length} {tier.items.length === 1 ? 'member' : 'members'}
									</span>
								</div>
							</div>

							<div class="flex items-center gap-2">
								<span class="text-[11px] text-muted-foreground group-hover:text-foreground hidden sm:inline">
									{isCollapsed ? 'Expand' : 'Collapse'}
								</span>
								<Icon
									name={isCollapsed ? 'chevron-right' : 'chevron-down'}
									class="size-4 text-muted-foreground transition-transform"
								/>
							</div>
						</div>

						<!-- Subordinate Member Cards inside this Tier -->
						{#if !isCollapsed}
							<div class="space-y-2.5 pl-2 sm:pl-4 border-l border-border/40 ml-2" transition:slide={{ duration: 200 }}>
								{#each tier.items as sub (sub.code)}
									{@const repMetrics = repMetricsMap.get(sub.code.toLowerCase())}
									<div class="relative group" in:fade={{ duration: 150 }}>
										<!-- Horizontal connector tick -->
										<div class="absolute -left-[9px] sm:-left-[17px] top-6 w-2 sm:w-4 h-0.5 bg-border/40"></div>

										<!-- Individual Subordinate Node Card (Mobile First) -->
										<div class="rounded-xl border border-border bg-card p-3 sm:p-4 hover:border-primary/50 hover:shadow-xs transition-all {tier.config.accentClass} border-l-4">
											<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
												<!-- Left: Identification & Role Info -->
												<div class="flex items-center gap-3">
													<!-- Initials circle -->
													<div class="size-10 rounded-xl bg-muted/80 text-foreground font-bold text-xs flex items-center justify-center border border-border shrink-0">
														{sub.name.slice(0, 2).toUpperCase()}
													</div>

													<div class="flex flex-col">
														<div class="flex items-center gap-2 flex-wrap">
															<span class="text-sm font-bold text-foreground leading-snug">
																{sub.name}
															</span>
															<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted text-muted-foreground border border-border/60">
																{sub.code}
															</span>
														</div>

														<div class="flex items-center gap-2 text-xs text-muted-foreground mt-0.5 flex-wrap">
															<span>{sub.jobTitle || tier.config.label}</span>
															{#if sub.sharedTeamCodes.length > 0}
																<span>•</span>
																<div class="flex items-center gap-1 flex-wrap">
																	{#each sub.sharedTeamCodes as team}
																		<span class="text-[10px] font-semibold px-1.5 py-0.2 rounded bg-primary/5 text-primary border border-primary/20">
																			{team}
																		</span>
																	{/each}
																</div>
															{/if}
														</div>
													</div>
												</div>

												<!-- Right: Mobile-friendly Action Buttons -->
												<div class="flex items-center gap-1.5 self-end sm:self-auto flex-wrap">
													<!-- Phone Dial Link (1-tap on mobile) -->
													{#if sub.mobilePhoneNo}
														<a
															href="tel:{sub.mobilePhoneNo}"
															class="inline-flex size-8 items-center justify-center rounded-lg border border-border bg-background hover:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 hover:border-emerald-500/30 transition-all"
															title="Call {sub.mobilePhoneNo}"
														>
															<Icon name="phone" class="size-3.5" />
														</a>
													{/if}

													<!-- Email Link -->
													{#if sub.companyEmail}
														<a
															href="mailto:{sub.companyEmail}"
															class="inline-flex size-8 items-center justify-center rounded-lg border border-border bg-background hover:bg-primary/10 text-primary hover:border-primary/30 transition-all"
															title="Email {sub.companyEmail}"
														>
															<Icon name="mail" class="size-3.5" />
														</a>
													{/if}

													<!-- CRM Performance Stats if available -->
													{#if repMetrics}
														<div class="hidden md:flex flex-col items-end px-2 text-right">
															<span class="text-xs font-bold text-foreground">
																{repMetrics.connectedCalls} connected
															</span>
															<span class="text-[10px] text-muted-foreground">
																{repMetrics.attainmentPct}% target
															</span>
														</div>
													{/if}

													<!-- Inspect in CRM Sheet -->
													{#if onInspectRep}
														<Button
															variant="outline"
															size="sm"
															class="h-8 text-xs gap-1 rounded-lg px-2.5"
															onclick={() => handleInspect(sub)}
															title="Inspect call activity & targets"
														>
															<Icon name="user-check" class="size-3.5" />
															<span class="hidden sm:inline">Inspect</span>
														</Button>
													{/if}

													<!-- Filter CRM Dashboard to this Rep -->
													{#if onSelectRep}
														<button
															type="button"
															onclick={() => onSelectRep?.(sub.code)}
															class="inline-flex h-8 items-center gap-1 px-2.5 text-xs font-medium rounded-lg border border-border bg-background hover:bg-muted text-foreground transition-all"
															title="Filter CRM Dashboard"
														>
															<Icon name="filter" class="size-3" />
															<span class="hidden sm:inline">Scope</span>
														</button>
													{/if}

													<!-- Drill Down Action: For Subordinate Managers with their own downline -->
													{#if sub.roleType > 0}
														<Button
															variant="default"
															size="sm"
															class="h-8 text-xs gap-1 rounded-lg px-2.5 bg-primary text-primary-foreground font-semibold shadow-xs"
															onclick={() => drillDown(sub)}
															title="Explore downline reports of {sub.name}"
														>
															<span>Downline</span>
															<Icon name="chevron-right" class="size-3.5" />
														</Button>
													{/if}
												</div>
											</div>
										</div>
									</div>
								{/each}
							</div>
						{/if}
					</div>
				{/each}
			{:else}
				<!-- VIEW MODE 2: GROUPED BY SHARED NAV TEAMS -->
				{#each subordinatesByTeam as team (team.teamCode)}
					{@const isCollapsed = collapsedTiers[`team-${team.teamCode}`]}
					<div class="relative space-y-3" in:fade={{ duration: 150 }}>
						<!-- Elbow Connector to Team Header -->
						<div class="absolute -left-[13px] sm:-left-[25px] top-4 w-3 sm:w-6 h-0.5 bg-border/80"></div>

						<!-- Team Accordion Bar -->
						<div
							role="button"
							tabindex="0"
							onclick={() => toggleTier(`team-${team.teamCode}`)}
							onkeydown={(e) => e.key === 'Enter' && toggleTier(`team-${team.teamCode}`)}
							class="flex items-center justify-between p-2 sm:p-2.5 rounded-xl border border-border/80 bg-muted/40 hover:bg-muted/70 transition-all cursor-pointer select-none group"
						>
							<div class="flex items-center gap-2.5">
								<span class="size-6 rounded-md flex items-center justify-center text-xs font-bold bg-primary/10 text-primary border border-primary/20">
									<Icon name="users" class="size-3.5" />
								</span>
								<div class="flex items-center gap-2">
									<h4 class="text-xs sm:text-sm font-bold text-foreground">
										Team {team.teamCode}
									</h4>
									<span class="text-[11px] font-semibold px-2 py-0.5 rounded-full bg-background border border-border text-foreground">
										{team.items.length} {team.items.length === 1 ? 'rep' : 'reps'}
									</span>
								</div>
							</div>

							<div class="flex items-center gap-2">
								<span class="text-[11px] text-muted-foreground group-hover:text-foreground hidden sm:inline">
									{isCollapsed ? 'Expand' : 'Collapse'}
								</span>
								<Icon
									name={isCollapsed ? 'chevron-right' : 'chevron-down'}
									class="size-4 text-muted-foreground transition-transform"
								/>
							</div>
						</div>

						<!-- Team Members -->
						{#if !isCollapsed}
							<div class="space-y-2.5 pl-2 sm:pl-4 border-l border-border/40 ml-2" transition:slide={{ duration: 200 }}>
								{#each team.items as sub (sub.code)}
									{@const roleCfg = getRoleConfig(sub.roleType)}
									<div class="relative group" in:fade={{ duration: 150 }}>
										<div class="absolute -left-[9px] sm:-left-[17px] top-6 w-2 sm:w-4 h-0.5 bg-border/40"></div>

										<div class="rounded-xl border border-border bg-card p-3 sm:p-4 hover:border-primary/50 transition-all {roleCfg.accentClass} border-l-4">
											<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3">
												<div class="flex items-center gap-3">
													<div class="size-10 rounded-xl bg-muted/80 text-foreground font-bold text-xs flex items-center justify-center border border-border shrink-0">
														{sub.name.slice(0, 2).toUpperCase()}
													</div>

													<div class="flex flex-col">
														<div class="flex items-center gap-2 flex-wrap">
															<span class="text-sm font-bold text-foreground">
																{sub.name}
															</span>
															<span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-muted text-muted-foreground">
																{sub.code}
															</span>
															<span class="text-[10px] font-bold px-2 py-0.5 rounded-full border {roleCfg.badgeClass}">
																{sub.roleName}
															</span>
														</div>

														<div class="text-xs text-muted-foreground mt-0.5">
															<span>{sub.jobTitle || roleCfg.label}</span>
														</div>
													</div>
												</div>

												<div class="flex items-center gap-1.5 self-end sm:self-auto flex-wrap">
													{#if sub.mobilePhoneNo}
														<a
															href="tel:{sub.mobilePhoneNo}"
															class="inline-flex size-8 items-center justify-center rounded-lg border border-border bg-background hover:bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 transition-all"
															title="Call {sub.mobilePhoneNo}"
														>
															<Icon name="phone" class="size-3.5" />
														</a>
													{/if}

													{#if onInspectRep}
														<Button
															variant="outline"
															size="sm"
															class="h-8 text-xs gap-1 rounded-lg px-2.5"
															onclick={() => handleInspect(sub)}
														>
															<Icon name="user-check" class="size-3.5" />
															<span class="hidden sm:inline">Inspect</span>
														</Button>
													{/if}

													{#if sub.roleType > 0}
														<Button
															variant="default"
															size="sm"
															class="h-8 text-xs gap-1 rounded-lg px-2.5 bg-primary text-primary-foreground font-semibold"
															onclick={() => drillDown(sub)}
														>
															<span>Downline</span>
															<Icon name="chevron-right" class="size-3.5" />
														</Button>
													{/if}
												</div>
											</div>
										</div>
									</div>
								{/each}
							</div>
						{/if}
					</div>
				{/each}
			{/if}
		</div>
	{/if}
</div>
