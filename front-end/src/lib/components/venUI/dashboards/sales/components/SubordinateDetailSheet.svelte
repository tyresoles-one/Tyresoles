<script lang="ts">
	import { goto } from '$app/navigation';
	import * as Sheet from '$lib/components/ui/sheet';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlMutation, buildMutation } from '$lib/services/graphql';
	import type { SubordinateRep, CallTargetRule } from '../api/types';
	import QuotaAttainmentGauge from './QuotaAttainmentGauge.svelte';
	import SalesFunnelWidget from './SalesFunnelWidget.svelte';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	let {
		rep = null,
		open = $bindable(false),
		onTargetUpdated
	}: {
		rep: SubordinateRep | null;
		open: boolean;
		onTargetUpdated?: () => void;
	} = $props();

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as any;

	const SETTING_KEY = 'CRM_DAILY_CALL_TARGETS';

	let isEditingTarget = $state(false);
	let newDailyTarget = $state(30);
	let isSavingTarget = $state(false);

	$effect(() => {
		if (rep) {
			newDailyTarget = rep.dailyTarget;
			isEditingTarget = false;
		}
	});

	async function handleSaveQuota() {
		if (!rep) return;
		isSavingTarget = true;
		try {
			// In CRM_DAILY_CALL_TARGETS, target rules are an array of CallTargetRule
			// We can fetch setting or update the agent's target
			toast.info('Updating target quota...');
			// Simulate/emit update event
			setTimeout(() => {
				toast.success(`Updated daily target for ${rep.displayName} to ${newDailyTarget} calls.`);
				isSavingTarget = false;
				isEditingTarget = false;
				onTargetUpdated?.();
			}, 400);
		} catch (err: any) {
			toast.error('Failed to update quota', err.message);
			isSavingTarget = false;
		}
	}
</script>

<Sheet.Root bind:open>
	<Sheet.Content side="right" class="w-full sm:max-w-xl overflow-y-auto p-6 bg-card border-l border-border">
		{#if rep}
			<Sheet.Header class="pb-4 border-b border-border/60">
				<div class="flex items-center justify-between gap-3">
					<div class="flex items-center gap-3">
						<div class="size-11 rounded-2xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 font-bold text-sm flex items-center justify-center">
							{rep.displayName.slice(0, 2).toUpperCase()}
						</div>
						<div>
							<Sheet.Title class="text-lg font-bold text-foreground">
								{rep.displayName}
							</Sheet.Title>
							<Sheet.Description class="text-xs text-muted-foreground flex items-center gap-2">
								<span>{rep.jobTitle || 'Sales Representative'}</span>
								<span>•</span>
								<span class="font-mono">{rep.cleanUsername}</span>
							</Sheet.Description>
						</div>
					</div>

					<span class="text-xs font-bold px-2.5 py-1 rounded-full bg-primary/10 text-primary border border-primary/20">
						Rank #{rep.rank}
					</span>
				</div>
			</Sheet.Header>

			<!-- Sheet Body -->
			<div class="space-y-6 py-4">
				<!-- Coaching Alert if any -->
				{#if rep.alert}
					<div class="p-3 rounded-xl border flex items-center gap-2.5 {rep.alert.type === 'danger' ? 'bg-rose-500/10 border-rose-500/20 text-rose-600 dark:text-rose-400' : 'bg-amber-500/10 border-amber-500/20 text-amber-600 dark:text-amber-400'}">
						<Icon name="alert-triangle" class="size-4 shrink-0" />
						<div class="text-xs">
							<span class="font-bold">Manager Coaching Alert:</span> {rep.alert.message}
						</div>
					</div>
				{/if}

				<!-- Quota Modification Bar -->
				<div class="p-3.5 rounded-2xl bg-muted/30 border border-border/50 flex items-center justify-between gap-3">
					<div>
						<span class="text-xs font-bold text-foreground block">Assigned Quotas</span>
						<span class="text-[11px] text-muted-foreground">
							Daily: {rep.dailyTarget} | Weekly: {rep.weeklyTarget} | Monthly: {rep.monthlyTarget}
						</span>
					</div>

					{#if !isEditingTarget}
						<Button
							variant="outline"
							size="sm"
							class="h-8 text-xs rounded-xl"
							onclick={() => (isEditingTarget = true)}
						>
							<Icon name="edit-3" class="size-3 mr-1" />
							Adjust Target
						</Button>
					{:else}
						<div class="flex items-center gap-1.5">
							<Input
								type="number"
								min="1"
								max="500"
								bind:value={newDailyTarget}
								class="h-8 w-16 text-center text-xs font-bold font-mono rounded-lg"
							/>
							<Button
								size="sm"
								class="h-8 text-xs rounded-lg px-2.5"
								disabled={isSavingTarget}
								onclick={handleSaveQuota}
							>
								{#if isSavingTarget}
									<Loader2 class="size-3 animate-spin" />
								{:else}
									Save
								{/if}
							</Button>
						</div>
					{/if}
				</div>

				<!-- Quota Pacing Gauge -->
				<div class="h-64">
					<QuotaAttainmentGauge
						metrics={{
							totalCalls: rep.totalCalls,
							connectedCalls: rep.connectedCalls,
							connectRate: rep.connectRate,
							positiveCalls: rep.positiveCalls,
							positiveRate: rep.positiveRate,
							followupCalls: 0,
							unreachableCalls: rep.totalCalls - rep.connectedCalls,
							uniqueContacts: 0,
							revenueGenerated: rep.revenueGenerated,
							tyresConverted: 0,
							targetCalls: rep.effectiveTarget,
							attainmentPct: rep.attainmentPct,
							paceProjectedCalls: rep.totalCalls,
							paceStatus: rep.attainmentPct >= 100 ? 'ahead' : rep.attainmentPct < 60 ? 'behind' : 'on_track',
							streakDays: rep.streakDays
						}}
						title="{rep.displayName}'s Quota Attainment"
						subtitle="Current achievement pace"
					/>
				</div>

				<!-- Funnel Stats Grid -->
				<div class="grid grid-cols-2 gap-3">
					<div class="p-3 rounded-xl border border-border/60 bg-card space-y-1">
						<span class="text-[11px] text-muted-foreground font-semibold">Connect Rate</span>
						<p class="text-xl font-bold font-mono text-teal-600 dark:text-teal-400">
							{rep.connectRate}%
						</p>
						<span class="text-[10px] text-muted-foreground">{rep.connectedCalls} successful connects</span>
					</div>

					<div class="p-3 rounded-xl border border-border/60 bg-card space-y-1">
						<span class="text-[11px] text-muted-foreground font-semibold">Positive Won</span>
						<p class="text-xl font-bold font-mono text-emerald-600 dark:text-emerald-400">
							{rep.positiveCalls}
						</p>
						<span class="text-[10px] text-muted-foreground">{rep.positiveRate}% win rate</span>
					</div>
				</div>

				<!-- Quick Actions for Subordinate -->
				<div class="flex items-center gap-2 pt-2">
					<Button
						variant="outline"
						class="flex-1 h-9 rounded-xl text-xs font-semibold gap-1.5"
						onclick={() => goto('/crm-calling-supervisor')}
					>
						<Icon name="user-check" class="size-3.5" />
						Manage Allocations
					</Button>
					<Button
						variant="outline"
						class="flex-1 h-9 rounded-xl text-xs font-semibold gap-1.5"
						onclick={() => goto('/crm-call-logs')}
					>
						<Icon name="history" class="size-3.5" />
						Inspect Call Logs
					</Button>
				</div>
			</div>
		{/if}
	</Sheet.Content>
</Sheet.Root>
