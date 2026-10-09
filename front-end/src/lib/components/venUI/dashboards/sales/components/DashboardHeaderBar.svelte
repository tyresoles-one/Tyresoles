<script lang="ts">
	import { goto } from '$app/navigation';
	import { Icon } from '$lib/components/venUI/icon';
	import { Select } from '$lib/components/venUI/select';
	import { Button } from '$lib/components/ui/button';
	import { authStore } from '$lib/stores/auth';
	import type { DashboardTimeframe, ViewPerspective, SubordinateRep } from '../api/types';

	let {
		perspective = $bindable<'self' | 'team'>('self'),
		teamSubView = $bindable<'leaderboard' | 'tree'>('leaderboard'),
		timeframe = $bindable<DashboardTimeframe>('7d'),
		selectedSubordinate = $bindable<string>('all'),
		selectedRespCenters = $bindable<string[]>([]),
		availableLocations = [],
		subordinates = [],
		hasSubordinates = true,
		loading = false,
		onRefresh
	}: {
		perspective: 'self' | 'team';
		teamSubView?: 'leaderboard' | 'tree';
		timeframe: DashboardTimeframe;
		selectedSubordinate: string;
		selectedRespCenters: string[];
		availableLocations: any[];
		subordinates: SubordinateRep[];
		hasSubordinates?: boolean;
		loading: boolean;
		onRefresh: () => void;
	} = $props();

	const timeframeOptions: Array<{ id: DashboardTimeframe; label: string }> = [
		{ id: 'today', label: 'Today' },
		{ id: '7d', label: '7 Days' },
		{ id: 'month', label: 'Month' }
	];

	const subordinateOptions = $derived([
		{ id: 'all', label: `All Subordinates (${subordinates.length})` },
		...subordinates.map((s) => ({
			id: s.cleanUsername,
			label: s.roleName ? `${s.displayName} (${s.roleName})` : s.displayName
		}))
	]);
</script>

<header class="relative rounded-xl border bg-card p-3 shadow-xs">
	<div class="flex flex-col lg:flex-row items-start lg:items-center justify-between gap-3">
		<!-- Left: Standard Header Identity -->
		<div class="flex items-center gap-3">
			<div class="hidden sm:flex items-center justify-center w-10 h-10 rounded-full bg-primary/10 border border-primary/20 text-primary shrink-0">
				<Icon name="phone-call" class="w-5 h-5" />
			</div>
			<div class="flex flex-col">
				<div class="flex items-center gap-2">
					<h1 class="text-lg font-bold tracking-tight text-foreground leading-none">
						Sales Dashboard
					</h1>
					<span class="text-[10px] font-semibold flex items-center gap-1 text-emerald-500 bg-emerald-500/10 px-2 py-0.5 rounded-full">
						<span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
						Live CRM
					</span>
				</div>
				<p class="text-[11px] text-muted-foreground mt-0.5">
					Welcome back, <span class="font-bold text-foreground">{$authStore.user?.fullName || 'Sales Executive'}</span>
				</p>
			</div>
		</div>

		<!-- Right: Unified Interactive Controls -->
		<div class="flex flex-wrap items-center gap-2 w-full lg:w-auto">
			<!-- Self vs Team Perspective Toggle -->
			{#if hasSubordinates}
				<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/40 text-xs">
					<button
						type="button"
						onclick={() => (perspective = 'self')}
						class="flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-semibold transition-all {perspective === 'self'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="user" class="size-3.5" />
						<span>My Progress</span>
					</button>

					<button
						type="button"
						onclick={() => (perspective = 'team')}
						class="flex items-center gap-1.5 px-3 py-1 rounded-md text-xs font-semibold transition-all {perspective === 'team'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
					>
						<Icon name="users" class="size-3.5" />
						<span>Team / Subordinates ({subordinates.length})</span>
					</button>
				</div>
			{/if}

			<!-- Team Sub-View Switcher: Metrics vs Hierarchy Tree -->
			{#if perspective === 'team' && hasSubordinates}
				<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/40 text-xs">
					<button
						type="button"
						onclick={() => (teamSubView = 'leaderboard')}
						class="flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold transition-all {teamSubView === 'leaderboard'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
						title="Metrics & Leaderboard"
					>
						<Icon name="trophy" class="size-3.5" />
						<span class="hidden sm:inline">Metrics</span>
					</button>

					<button
						type="button"
						onclick={() => (teamSubView = 'tree')}
						class="flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold transition-all {teamSubView === 'tree'
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
						title="Visual Hierarchy Tree"
					>
						<Icon name="network" class="size-3.5" />
						<span class="hidden sm:inline">Tree</span>
					</button>
				</div>
			{/if}

			<!-- Subordinate Selector (when in Team mode) -->
			{#if perspective === 'team' && subordinates.length > 0}
				<div class="w-40">
					<select
						bind:value={selectedSubordinate}
						class="w-full h-8 px-2 rounded-lg border border-border bg-background text-xs font-medium focus:outline-none focus:ring-1 focus:ring-primary"
					>
						{#each subordinateOptions as opt}
							<option value={opt.id}>{opt.label}</option>
						{/each}
					</select>
				</div>
			{/if}

			<!-- Location Filter if multiple exist -->
			{#if availableLocations.length > 1}
				<div class="w-36">
					<Select
						options={availableLocations}
						bind:value={selectedRespCenters}
						valueKey="code"
						labelKey="name"
						multiple={true}
						placeholder="All Locations"
						class="w-full h-8 text-xs font-medium bg-background"
					/>
				</div>
			{/if}

			<!-- Timeframe Preset Buttons -->
			<div class="flex items-center p-0.5 rounded-lg bg-muted/60 border border-border/40">
				{#each timeframeOptions as opt}
					<button
						type="button"
						onclick={() => (timeframe = opt.id)}
						class="px-2.5 py-1 rounded-md text-xs font-medium transition-all {timeframe === opt.id
							? 'bg-background text-foreground shadow-xs font-bold'
							: 'text-muted-foreground hover:text-foreground'}"
					>
						{opt.label}
					</button>
				{/each}
			</div>

			<!-- Primary Action: Dial Contacts -->
			<Button
				size="sm"
				class="h-8 rounded-lg px-3 bg-primary hover:bg-primary/90 text-primary-foreground text-xs font-semibold gap-1.5 shadow-xs"
				onclick={() => goto('/crm-calling')}
			>
				<Icon name="phone" class="size-3.5" />
				<span>Start Calling</span>
			</Button>

			<!-- Refresh Button -->
			<button
				type="button"
				onclick={onRefresh}
				disabled={loading}
				class="inline-flex size-8 items-center justify-center rounded-lg border border-border bg-background hover:bg-muted/40 transition-all text-muted-foreground hover:text-foreground disabled:opacity-50"
				title="Refresh Data"
			>
				<Icon name="refresh-cw" class="size-3.5 {loading ? 'animate-spin' : ''}" />
			</button>
		</div>
	</div>
</header>
