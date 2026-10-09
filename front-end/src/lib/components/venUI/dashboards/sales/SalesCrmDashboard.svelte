<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { fade } from 'svelte/transition';
	import { authStore, getUser } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery } from '$lib/services/graphql';

	// API & Calculation functions
	import {
		GetAllCrmCallLogsDocument,
		GetCrmSettingDocument,
		GetCrmAgentSummaryReportDocument,
		GetAllCrmCallRemindersDocument,
		GetEmployeesDocument,
		GetSalesHierarchyDocument,
		cleanUsername,
		getDateRangeBounds,
		calculateKpiMetrics,
		calculateActivityBuckets,
		calculateFunnelStages,
		calculateLeaderboard
	} from './api/salesCrmDashboardApi';

	import type {
		DashboardTimeframe,
		ViewPerspective,
		CallTargetRule,
		CallLogRecord,
		EmployeeHierarchyItem,
		SubordinateRep,
		FocusActionItem,
		SalesHierarchySummary
	} from './api/types';

	// Visual subcomponents
	import { Icon } from '$lib/components/venUI/icon';
	import DashboardHeaderBar from './components/DashboardHeaderBar.svelte';
	import KpiMetricsCards from './components/KpiMetricsCards.svelte';
	import ActivityTimelineChart from './components/ActivityTimelineChart.svelte';
	import TeamLeaderboardWidget from './components/TeamLeaderboardWidget.svelte';
	import SubordinateDetailSheet from './components/SubordinateDetailSheet.svelte';
	import FocusActionQueue from './components/FocusActionQueue.svelte';
	import SalesHierarchyTree from './components/SalesHierarchyTree.svelte';

	let { class: className = '' }: { class?: string } = $props();

	const user = getUser();
	const myRawUsername = $derived($authStore.username || '');
	const myCleanUsername = $derived(cleanUsername(myRawUsername));

	// State
	let perspective = $state<ViewPerspective>('self');
	let teamSubView = $state<'leaderboard' | 'tree'>('leaderboard');
	let timeframe = $state<DashboardTimeframe>('7d');
	let selectedSubordinate = $state<string>('all');
	let selectedRespCenters = $state<string[]>([]);
	let loading = $state<boolean>(true);
	let error = $state<string | null>(null);

	// Raw Data
	let callLogs = $state<CallLogRecord[]>([]);
	let targetRules = $state<CallTargetRule[]>([]);
	let agentSummaries = $state<Array<{ agentUsername: string; totalAllocated: number; activeAllocated: number; totalCalls: number }>>([]);
	let employees = $state<EmployeeHierarchyItem[]>([]);
	let reminders = $state<FocusActionItem[]>([]);
	let salesHierarchy = $state<SalesHierarchySummary | null>(null);
	const hasSubordinates = $derived((salesHierarchy?.totalSubordinatesCount ?? 0) > 0);

	// Subordinate Inspector Sheet State
	let inspectedRep = $state<SubordinateRep | null>(null);
	let inspectorOpen = $state<boolean>(false);

	let abortCtrl: AbortController | null = null;

	// Computed available locations
	const availableLocations = $derived.by(() => {
		const locs = $authStore.locations;
		if (locs && locs.length > 0) {
			return locs.filter((l) => (l as any).sale === 1);
		}
		return [];
	});

	// Date Bounds
	const bounds = $derived(getDateRangeBounds(timeframe));

	// Active targets for current user
	const myTargetRule = $derived.by(() => {
		const match = targetRules.find((t) => cleanUsername(t.agentUsername).toLowerCase() === myCleanUsername.toLowerCase() && t.isActive !== false);
		if (match) return match;
		return targetRules.find((t) => {
			const ag = (t.agentUsername || '').toUpperCase();
			return ag === 'DEFAULT' || ag === 'GLOBAL_DEFAULT';
		}) || { agentUsername: 'DEFAULT', dailyTarget: 30, weeklyTarget: 150, monthlyTarget: 600, isActive: true };
	});

	// Active Allocations Map
	const activeAllocationsMap = $derived.by(() => {
		const map = new Map<string, number>();
		for (const a of agentSummaries) {
			map.set(cleanUsername(a.agentUsername).toLowerCase(), a.activeAllocated);
		}
		return map;
	});

	const salesUsersMap = $derived.by(() => {
		const map = new Map<string, string>();
		for (const e of employees) {
			map.set(cleanUsername(e.initials || e.firstName).toLowerCase(), `${e.firstName} ${e.lastName}`.trim());
		}
		return map;
	});

	// Leaderboard Reps across the entire team
	const leaderboardReps = $derived(
		calculateLeaderboard(
			callLogs,
			employees,
			targetRules,
			bounds.daysCount,
			timeframe,
			activeAllocationsMap,
			salesUsersMap,
			salesHierarchy?.subordinates
		)
	);

	// Current Rep Rank
	const myRepRank = $derived.by(() => {
		const found = leaderboardReps.find((r) => r.cleanUsername.toLowerCase() === myCleanUsername.toLowerCase());
		return found ? found.rank : null;
	});

	// Filtered Logs based on Perspective & Subordinate Filter
	const displayedLogs = $derived.by(() => {
		let logs = callLogs;
		if (selectedRespCenters.length > 0) {
			logs = logs.filter((l) => !l.contact?.respCenter || selectedRespCenters.includes(l.contact.respCenter));
		}
		if (perspective === 'self') {
			return logs.filter((l) => cleanUsername(l.createdBy).toLowerCase() === myCleanUsername.toLowerCase());
		} else {
			// Team perspective
			if (selectedSubordinate !== 'all') {
				return logs.filter((l) => cleanUsername(l.createdBy).toLowerCase() === selectedSubordinate.toLowerCase());
			}
			// When viewing all subordinates, filter logs strictly to true subordinates + self
			if (salesHierarchy && salesHierarchy.subordinateCodes.length > 0) {
				const allowedSet = new Set(salesHierarchy.subordinateCodes.map((c) => c.toLowerCase()));
				allowedSet.add(myCleanUsername.toLowerCase());
				return logs.filter((l) => allowedSet.has(cleanUsername(l.createdBy).toLowerCase()));
			}
			return logs;
		}
	});

	// Target rule for displayed logs
	const activeTargetRule = $derived.by(() => {
		if (perspective === 'self') return myTargetRule;
		if (selectedSubordinate !== 'all') {
			const subMatch = leaderboardReps.find((r) => r.cleanUsername.toLowerCase() === selectedSubordinate.toLowerCase());
			if (subMatch) {
				return {
					agentUsername: subMatch.cleanUsername,
					dailyTarget: subMatch.dailyTarget,
					weeklyTarget: subMatch.weeklyTarget,
					monthlyTarget: subMatch.monthlyTarget,
					isActive: true
				};
			}
		}
		// Aggregate team target
		const repCount = Math.max(1, leaderboardReps.length);
		return {
			agentUsername: 'TEAM_AGGREGATE',
			dailyTarget: (myTargetRule?.dailyTarget ?? 30) * repCount,
			weeklyTarget: (myTargetRule?.weeklyTarget ?? 150) * repCount,
			monthlyTarget: (myTargetRule?.monthlyTarget ?? 600) * repCount,
			isActive: true
		};
	});

	// Primary Metrics & Calculations
	const kpiMetrics = $derived(
		calculateKpiMetrics(
			displayedLogs,
			activeTargetRule,
			bounds,
			timeframe,
			perspective === 'self' ? myRepRank : null,
			leaderboardReps.length
		)
	);

	const activityBuckets = $derived(
		calculateActivityBuckets(displayedLogs, bounds, timeframe)
	);

	const funnelStages = $derived(
		calculateFunnelStages(displayedLogs)
	);

	// Load Data Orchestrator
	async function loadDashboardData() {
		if (abortCtrl) abortCtrl.abort();
		abortCtrl = new AbortController();

		loading = true;
		error = null;

		const startIso = bounds.start.toISOString();
		const endIso = bounds.end.toISOString();

		try {
			const [logsRes, targetsRes, summariesRes, remindersRes, employeesRes, hierarchyRes] = await Promise.allSettled([
				// 1. All Call Logs in range
				graphqlQuery<{ crmCallLogs: { items: CallLogRecord[]; totalCount: number } }>(
					GetAllCrmCallLogsDocument,
					{
						variables: {
							take: 1000,
							where: { callDate: { gte: startIso, lte: endIso } },
							order: [{ callDate: 'DESC' }]
						}
					}
				),

				// 2. Daily Call Targets
				graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(
					GetCrmSettingDocument,
					{ variables: { key: 'CRM_DAILY_CALL_TARGETS' } }
				),

				// 3. Agent Summary Report
				graphqlQuery<{ getCrmAgentSummaryReport: Array<{ agentUsername: string; totalAllocated: number; activeAllocated: number; totalCalls: number }> }>(
					GetCrmAgentSummaryReportDocument,
					{}
				),

				// 4. Upcoming Reminders
				graphqlQuery<{ crmCallReminders: { items: any[]; totalCount: number } }>(
					GetAllCrmCallRemindersDocument,
					{
						variables: {
							take: 30,
							where: { isCompleted: { eq: false } },
							order: [{ reminderDate: 'ASC' }]
						}
					}
				),

				// 5. Employees list for reporting hierarchy
				graphqlQuery<{ employees: { items: EmployeeHierarchyItem[]; totalCount: number } }>(
					GetEmployeesDocument,
					{
						variables: {
							take: 200,
							where: { status: { eq: 0 } }
						}
					}
				),

				// 6. Sales Hierarchy Subordinates
				graphqlQuery<{ salesHierarchy: SalesHierarchySummary }>(
					GetSalesHierarchyDocument,
					{
						variables: { employeeCode: myCleanUsername }
					}
				)
			]);

			// Parse Call Logs
			if (logsRes.status === 'fulfilled' && logsRes.value.success && logsRes.value.data?.crmCallLogs?.items) {
				callLogs = logsRes.value.data.crmCallLogs.items;
			} else {
				callLogs = [];
			}

			// Parse Target Rules
			if (targetsRes.status === 'fulfilled' && targetsRes.value.success && targetsRes.value.data?.getCrmSetting?.value) {
				try {
					const parsed = JSON.parse(targetsRes.value.data.getCrmSetting.value);
					if (Array.isArray(parsed)) targetRules = parsed;
				} catch {
					targetRules = [];
				}
			}

			// Parse Agent Summaries
			if (summariesRes.status === 'fulfilled' && summariesRes.value.success && summariesRes.value.data?.getCrmAgentSummaryReport) {
				agentSummaries = summariesRes.value.data.getCrmAgentSummaryReport;
			}

			// Parse Reminders
			if (remindersRes.status === 'fulfilled' && remindersRes.value.success && remindersRes.value.data?.crmCallReminders?.items) {
				const now = new Date();
				reminders = remindersRes.value.data.crmCallReminders.items.map((r: any) => {
					const rDate = new Date(r.reminderDate);
					const isOverdue = rDate.getTime() < now.getTime();
					const timeFormatted = rDate.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true });

					return {
						id: r.id,
						type: 'reminder',
						contactId: r.contactId,
						contactName: r.contact?.fullName || 'Contact',
						companyName: r.contact?.companyName,
						mobileNo: r.contact?.mobileNo,
						timeFormatted,
						isOverdue,
						notes: r.notes,
						dateIso: r.reminderDate
					};
				});
			}

			// Parse Employees
			if (employeesRes.status === 'fulfilled' && employeesRes.value.success && employeesRes.value.data?.employees?.items) {
				employees = employeesRes.value.data.employees.items;
			}

			// Parse Sales Hierarchy
			if (hierarchyRes.status === 'fulfilled' && hierarchyRes.value.success && hierarchyRes.value.data?.salesHierarchy) {
				salesHierarchy = hierarchyRes.value.data.salesHierarchy;
				if (salesHierarchy.totalSubordinatesCount === 0 && perspective === 'team') {
					perspective = 'self';
				}
			}
		} catch (e: any) {
			if (e.name !== 'AbortError') {
				error = e.message || 'Failed to load sales CRM data';
				toast.error(error!);
			}
		} finally {
			loading = false;
		}
	}

	function handleInspectSubordinate(rep: SubordinateRep) {
		inspectedRep = rep;
		inspectorOpen = true;
	}

	$effect(() => {
		const _tf = timeframe;
		const _sub = selectedSubordinate;
		untrack(() => {
			loadDashboardData();
		});
	});

	onMount(() => {
		loadDashboardData();
	});
</script>

<div class="space-y-4 max-w-[1600px] mx-auto {className}" in:fade={{ duration: 250 }}>
	<!-- 1. Unified Single Header Bar (Zero Clutter) -->
	<DashboardHeaderBar
		bind:perspective
		bind:teamSubView
		bind:timeframe
		bind:selectedSubordinate
		bind:selectedRespCenters
		availableLocations={availableLocations}
		subordinates={leaderboardReps}
		hasSubordinates={hasSubordinates}
		{loading}
		onRefresh={loadDashboardData}
	/>

	<!-- 2. Clean 4-Column KPI Metric Tiles (Matches Tyresoles Tile Pattern) -->
	<KpiMetricsCards metrics={kpiMetrics} {perspective} />

	<!-- 3. Main Productive Workspace -->
	{#if perspective === 'self'}
		<!-- Self View: 2 Cohesive Panels (Activity Velocity Chart on Left, Focus Action Queue on Right) -->
		<div class="grid grid-cols-1 xl:grid-cols-12 gap-4 items-stretch">
			<div class="xl:col-span-8 flex flex-col">
				<ActivityTimelineChart
					buckets={activityBuckets}
					title="Calling & Win Velocity"
					class="min-h-[320px]"
				/>
			</div>

			<div class="xl:col-span-4 flex flex-col">
				<FocusActionQueue
					items={reminders}
					funnelStages={funnelStages}
				/>
			</div>
		</div>
	{:else}
		<!-- Team View: Subordinates Leaderboard & Team Pacing OR Hierarchy Tree -->
		<div class="space-y-4">
			<!-- Sub-view navigation switcher -->
			<div class="flex items-center justify-between gap-3 flex-wrap">
				<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/40 text-xs">
					<button
						type="button"
						onclick={() => (teamSubView = 'leaderboard')}
						class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-semibold transition-all {teamSubView === 'leaderboard'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="trophy" class="size-3.5" />
						<span>Leaderboard & Velocity</span>
					</button>

					<button
						type="button"
						onclick={() => (teamSubView = 'tree')}
						class="flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-semibold transition-all {teamSubView === 'tree'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="network" class="size-3.5" />
						<span>Visual Hierarchy Tree ({salesHierarchy?.totalSubordinatesCount ?? 0})</span>
					</button>
				</div>

				<div class="text-xs text-muted-foreground hidden sm:block">
					{#if teamSubView === 'tree'}
						<span>Interactive hierarchy tree • Drill down into downline or tap to call</span>
					{:else}
						<span>Team quota attainment and calling output</span>
					{/if}
				</div>
			</div>

			{#if teamSubView === 'leaderboard'}
				<div class="grid grid-cols-1 xl:grid-cols-12 gap-4 items-stretch" in:fade={{ duration: 150 }}>
					<div class="xl:col-span-8 flex flex-col">
						<TeamLeaderboardWidget
							reps={leaderboardReps}
							currentUsername={myCleanUsername}
							onInspectSubordinate={handleInspectSubordinate}
							title="Subordinates Progress & Quotas"
						/>
					</div>

					<div class="xl:col-span-4 flex flex-col">
						<ActivityTimelineChart
							buckets={activityBuckets}
							title="Team Velocity Timeline"
							class="min-h-[320px]"
						/>
					</div>
				</div>
			{:else}
				<div in:fade={{ duration: 150 }}>
					<SalesHierarchyTree
						employeeCode={myCleanUsername}
						initialData={salesHierarchy}
						leaderboardReps={leaderboardReps}
						onInspectRep={handleInspectSubordinate}
						onSelectRep={(code) => {
							selectedSubordinate = code;
							teamSubView = 'leaderboard';
							toast.info(`Filtered dashboard view to ${code}`);
						}}
					/>
				</div>
			{/if}
		</div>
	{/if}

	<!-- 4. Subordinate Detail Drawer / Sheet (Slid-out on demand) -->
	<SubordinateDetailSheet
		rep={inspectedRep}
		bind:open={inspectorOpen}
		onTargetUpdated={loadDashboardData}
	/>
</div>
