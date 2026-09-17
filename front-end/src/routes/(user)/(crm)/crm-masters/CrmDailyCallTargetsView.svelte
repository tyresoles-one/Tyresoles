<script lang="ts">
	import { onMount } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import * as Dialog from '$lib/components/ui/dialog';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	export type CallTargetItem = {
		id: string;
		agentUsername: string;
		dailyTarget: number;
		weeklyTarget: number;
		monthlyTarget: number;
		isActive: boolean;
		notes?: string | null;
	};

	const SETTING_KEY = 'CRM_DAILY_CALL_TARGETS';

	const GetCrmSettingDocument = buildQuery`
		query GetCrmSetting($key: String!) {
			getCrmSetting(key: $key) {
				key
				value
				description
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmSetting: { key: string; value: string; description?: string } | null }, { key: string }>;

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmSetting: { success: boolean; message: string } }, { key: string; value: string; description?: string }>;

	const GetCrmCallLogUsersDocument = buildQuery`
		query GetCrmCallLogUsers {
			getCrmCallLogUsers
		}
	` as unknown as TypedDocumentNode<{ getCrmCallLogUsers: string[] }, {}>;

	let targets = $state<CallTargetItem[]>([]);
	let availableAgents = $state<string[]>([]);
	let loading = $state(true);
	let saving = $state(false);

	// Dialog state
	let dialogOpen = $state(false);
	let dialogMode = $state<'add' | 'edit'>('add');
	let editId = $state<string | null>(null);

	let formAgent = $state('');
	let formDaily = $state<number>(30);
	let formWeekly = $state<number>(150);
	let formMonthly = $state<number>(600);
	let formIsActive = $state(true);
	let formNotes = $state('');

	// Default target item
	let defaultTarget = $derived(
		targets.find((t) => t.agentUsername.toUpperCase() === 'DEFAULT' || t.agentUsername.toUpperCase() === 'GLOBAL_DEFAULT')
	);

	let agentSpecificTargets = $derived(
		targets.filter((t) => t.agentUsername.toUpperCase() !== 'DEFAULT' && t.agentUsername.toUpperCase() !== 'GLOBAL_DEFAULT')
	);

	function cleanUsername(raw: string): string {
		if (!raw) return '';
		return raw.replace(/^tyresoles\\/i, '').trim();
	}

	onMount(async () => {
		await Promise.all([loadTargets(), loadAgents()]);
	});

	async function loadTargets() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, {
				variables: { key: SETTING_KEY }
			});
			if (res.success && res.data?.getCrmSetting?.value) {
				try {
					const parsed = JSON.parse(res.data.getCrmSetting.value);
					if (Array.isArray(parsed)) {
						targets = parsed;
					} else {
						targets = [];
					}
				} catch {
					targets = [];
				}
			} else {
				// Seed defaults if empty
				targets = [
					{
						id: crypto.randomUUID(),
						agentUsername: 'DEFAULT',
						dailyTarget: 30,
						weeklyTarget: 150,
						monthlyTarget: 600,
						isActive: true,
						notes: 'Default target for all calling agents'
					}
				];
			}
		} catch (err: any) {
			toast.error('Failed to load targets', err.message);
		} finally {
			loading = false;
		}
	}

	async function loadAgents() {
		try {
			const res = await graphqlQuery<{ getCrmCallLogUsers: string[] }>(GetCrmCallLogUsersDocument, {});
			if (res.success && res.data?.getCrmCallLogUsers) {
				availableAgents = res.data.getCrmCallLogUsers;
			}
		} catch {
			availableAgents = [];
		}
	}

	async function saveTargetsToBackend() {
		saving = true;
		try {
			const json = JSON.stringify(targets);
			const res = await graphqlMutation(SaveCrmSettingDocument, {
				variables: {
					key: SETTING_KEY,
					value: json,
					description: 'Configured Daily, Weekly, and Monthly call targets per agent and default'
				}
			});
			if (res.data?.saveCrmSetting.success) {
				toast.success('Call targets saved successfully to database');
			} else {
				throw new Error(res.data?.saveCrmSetting.message || 'Unknown error');
			}
		} catch (err: any) {
			toast.error('Failed to save call targets', err.message);
		} finally {
			saving = false;
		}
	}

	function openAddDialog() {
		dialogMode = 'add';
		editId = null;
		formAgent = '';
		formDaily = defaultTarget?.dailyTarget ?? 30;
		formWeekly = defaultTarget?.weeklyTarget ?? 150;
		formMonthly = defaultTarget?.monthlyTarget ?? 600;
		formIsActive = true;
		formNotes = '';
		dialogOpen = true;
	}

	function openEditDialog(item: CallTargetItem) {
		dialogMode = 'edit';
		editId = item.id;
		formAgent = item.agentUsername;
		formDaily = item.dailyTarget;
		formWeekly = item.weeklyTarget;
		formMonthly = item.monthlyTarget;
		formIsActive = item.isActive;
		formNotes = item.notes || '';
		dialogOpen = true;
	}

	function handleDailyChange(e: Event) {
		const val = parseInt((e.target as HTMLInputElement).value, 10);
		if (!isNaN(val) && val > 0) {
			formDaily = val;
			formWeekly = val * 5;
			formMonthly = val * 20;
		}
	}

	function saveDialog() {
		if (!formAgent.trim()) {
			toast.error('Please select or specify an agent username');
			return;
		}

		if (dialogMode === 'add') {
			const existing = targets.find((t) => t.agentUsername.toLowerCase() === formAgent.toLowerCase().trim());
			if (existing) {
				toast.error(`A target for '${formAgent}' already exists.`);
				return;
			}
			targets = [
				...targets,
				{
					id: crypto.randomUUID(),
					agentUsername: formAgent.trim(),
					dailyTarget: formDaily,
					weeklyTarget: formWeekly,
					monthlyTarget: formMonthly,
					isActive: formIsActive,
					notes: formNotes.trim() || null
				}
			];
		} else if (dialogMode === 'edit' && editId) {
			targets = targets.map((t) => {
				if (t.id === editId) {
					return {
						...t,
						agentUsername: formAgent.trim(),
						dailyTarget: formDaily,
						weeklyTarget: formWeekly,
						monthlyTarget: formMonthly,
						isActive: formIsActive,
						notes: formNotes.trim() || null
					};
				}
				return t;
			});
		}

		dialogOpen = false;
		saveTargetsToBackend();
	}

	function removeTarget(id: string) {
		const found = targets.find((t) => t.id === id);
		if (found && (found.agentUsername.toUpperCase() === 'DEFAULT' || found.agentUsername.toUpperCase() === 'GLOBAL_DEFAULT')) {
			toast.error('The default company target cannot be deleted.');
			return;
		}
		if (!confirm('Are you sure you want to remove this target rule?')) return;
		targets = targets.filter((t) => t.id !== id);
		saveTargetsToBackend();
	}

	function toggleActive(id: string) {
		targets = targets.map((t) => {
			if (t.id === id) {
				return { ...t, isActive: !t.isActive };
			}
			return t;
		});
		saveTargetsToBackend();
	}
</script>

<div class="flex flex-col h-full bg-slate-50 dark:bg-slate-900 overflow-hidden">
	<!-- Top Bar -->
	<div class="p-6 border-b bg-white dark:bg-slate-950 flex items-center justify-between shadow-xs shrink-0">
		<div>
			<div class="flex items-center gap-2">
				<div class="p-2 rounded-xl bg-primary/10 text-primary">
					<Icon name="crosshair" class="size-5" />
				</div>
				<h2 class="text-xl font-bold tracking-tight text-foreground">Daily Call Targets</h2>
			</div>
			<p class="text-sm text-muted-foreground mt-1">
				Configure mandatory daily, weekly, and monthly calling targets per agent or company-wide default.
			</p>
		</div>

		<div class="flex items-center gap-2.5">
			<Button variant="outline" onclick={openAddDialog} class="gap-1.5 rounded-xl border-border">
				<Icon name="plus" class="size-4" />
				<span>Add Agent Target</span>
			</Button>

			<Button
				onclick={saveTargetsToBackend}
				disabled={loading || saving}
				class="gap-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium shadow-md rounded-xl px-5"
			>
				{#if saving}
					<Loader2 class="size-4 animate-spin" />
				{:else}
					<Icon name="save" class="size-4" />
				{/if}
				<span>Save Changes</span>
			</Button>
		</div>
	</div>

	<!-- Main Body -->
	<div class="flex-1 overflow-y-auto p-6">
		{#if loading}
			<div class="flex items-center justify-center py-16">
				<Loader2 class="size-7 animate-spin text-muted-foreground" />
			</div>
		{:else}
			<div class="max-w-5xl space-y-6">
				<!-- Default Global Target Card -->
				{#if defaultTarget}
					<div class="bg-white dark:bg-slate-950 rounded-xl border border-primary/20 bg-gradient-to-r from-primary/5 via-transparent to-transparent shadow-xs p-5">
						<div class="flex items-start justify-between gap-4">
							<div class="space-y-1">
								<div class="flex items-center gap-2">
									<span class="px-2 py-0.5 rounded-md text-[10px] font-black uppercase tracking-wider bg-primary text-primary-foreground">
										Global Default
									</span>
									<h3 class="text-base font-bold text-foreground">Company-Wide Default Target</h3>
								</div>
								<p class="text-xs text-muted-foreground">
									Applied to all CRM agents unless an individual target override is configured below.
								</p>
							</div>

							<Button
								variant="outline"
								size="sm"
								class="gap-1.5 rounded-lg text-xs"
								onclick={() => openEditDialog(defaultTarget)}
							>
								<Icon name="edit-3" class="size-3.5" />
								<span>Edit Default</span>
							</Button>
						</div>

						<div class="grid grid-cols-3 gap-4 mt-4 pt-4 border-t border-border/60">
							<div class="p-3 rounded-lg bg-muted/30 border border-border/50 text-center">
								<div class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">Daily Target</div>
								<div class="text-2xl font-black text-foreground mt-0.5">{defaultTarget.dailyTarget}</div>
								<div class="text-[10px] text-muted-foreground">calls / day</div>
							</div>
							<div class="p-3 rounded-lg bg-muted/30 border border-border/50 text-center">
								<div class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">Weekly Target</div>
								<div class="text-2xl font-black text-foreground mt-0.5">{defaultTarget.weeklyTarget}</div>
								<div class="text-[10px] text-muted-foreground">calls / week</div>
							</div>
							<div class="p-3 rounded-lg bg-muted/30 border border-border/50 text-center">
								<div class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider">Monthly Target</div>
								<div class="text-2xl font-black text-foreground mt-0.5">{defaultTarget.monthlyTarget}</div>
								<div class="text-[10px] text-muted-foreground">calls / month</div>
							</div>
						</div>
					</div>
				{/if}

				<!-- Agent-Specific Target Table -->
				<div class="bg-white dark:bg-slate-950 rounded-xl border border-border shadow-xs overflow-hidden">
					<div class="p-4 border-b border-border flex items-center justify-between">
						<div>
							<h3 class="font-bold text-sm text-foreground">Agent Overrides & Quotas</h3>
							<p class="text-xs text-muted-foreground">Custom targets assigned to specific sales and tele-calling representatives.</p>
						</div>
						<span class="text-xs font-semibold bg-muted px-2.5 py-1 rounded-full text-muted-foreground">
							{agentSpecificTargets.length} Agent Overrides
						</span>
					</div>

					{#if agentSpecificTargets.length === 0}
						<div class="text-center py-10 text-muted-foreground border-dashed">
							<Icon name="users" class="size-8 mx-auto mb-2 opacity-40" />
							<p class="text-xs">No specific agent overrides configured. All reps follow the Global Default.</p>
							<Button variant="outline" size="sm" onclick={openAddDialog} class="mt-3 gap-1.5 rounded-lg text-xs">
								<Icon name="plus" class="size-3.5" />
								<span>Assign Agent Target</span>
							</Button>
						</div>
					{:else}
						<div class="overflow-x-auto">
							<table class="w-full text-xs text-left">
								<thead class="bg-muted/40 text-muted-foreground font-semibold uppercase text-[10px] border-b border-border">
									<tr>
										<th class="px-4 py-3">Agent</th>
										<th class="px-4 py-3 text-center">Daily</th>
										<th class="px-4 py-3 text-center">Weekly</th>
										<th class="px-4 py-3 text-center">Monthly</th>
										<th class="px-4 py-3">Status</th>
										<th class="px-4 py-3">Notes</th>
										<th class="px-4 py-3 text-right">Actions</th>
									</tr>
								</thead>
								<tbody class="divide-y divide-border/60">
									{#each agentSpecificTargets as target (target.id)}
										<tr class="hover:bg-muted/20 transition-colors">
											<td class="px-4 py-3 font-semibold text-foreground">
												<div class="flex items-center gap-2">
													<div class="size-7 rounded-full bg-primary/10 text-primary font-bold text-[10px] flex items-center justify-center">
														{cleanUsername(target.agentUsername).slice(0, 2).toUpperCase()}
													</div>
													<div>
														<div class="font-bold text-xs">{cleanUsername(target.agentUsername)}</div>
														<div class="text-[10px] font-mono text-muted-foreground">{target.agentUsername}</div>
													</div>
												</div>
											</td>
											<td class="px-4 py-3 text-center">
												<span class="font-bold font-mono text-xs">{target.dailyTarget}</span>
												<span class="text-[10px] text-muted-foreground block">calls</span>
											</td>
											<td class="px-4 py-3 text-center">
												<span class="font-bold font-mono text-xs">{target.weeklyTarget}</span>
												<span class="text-[10px] text-muted-foreground block">calls</span>
											</td>
											<td class="px-4 py-3 text-center">
												<span class="font-bold font-mono text-xs">{target.monthlyTarget}</span>
												<span class="text-[10px] text-muted-foreground block">calls</span>
											</td>
											<td class="px-4 py-3">
												<button
													type="button"
													onclick={() => toggleActive(target.id)}
													class="px-2 py-0.5 rounded-full text-[10px] font-semibold border transition-all {target.isActive
														? 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20 hover:bg-emerald-500/20'
														: 'bg-muted text-muted-foreground border-border hover:bg-muted/80'}"
												>
													{target.isActive ? 'Active' : 'Inactive'}
												</button>
											</td>
											<td class="px-4 py-3 text-muted-foreground max-w-xs truncate">
												{target.notes || '—'}
											</td>
											<td class="px-4 py-3 text-right">
												<div class="flex items-center justify-end gap-1">
													<Button
														variant="ghost"
														size="icon"
														class="size-7 text-muted-foreground hover:text-foreground"
														onclick={() => openEditDialog(target)}
														title="Edit target"
													>
														<Icon name="edit-3" class="size-3.5" />
													</Button>
													<Button
														variant="ghost"
														size="icon"
														class="size-7 text-rose-500 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/40"
														onclick={() => removeTarget(target.id)}
														title="Delete override"
													>
														<Icon name="trash-2" class="size-3.5" />
													</Button>
												</div>
											</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					{/if}
				</div>
			</div>
		{/if}
	</div>
</div>

<!-- Add / Edit Modal Dialog -->
<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="max-w-md rounded-2xl p-6 bg-card border border-border shadow-xl">
		<Dialog.Header class="pb-3 border-b border-border/60">
			<Dialog.Title class="text-base font-bold flex items-center gap-2">
				<Icon name="crosshair" class="size-4 text-primary" />
				<span>{dialogMode === 'add' ? 'Add Call Target' : 'Edit Call Target'}</span>
			</Dialog.Title>
			<Dialog.Description class="text-xs text-muted-foreground">
				Set daily, weekly, and monthly targets for tele-calling representatives.
			</Dialog.Description>
		</Dialog.Header>

		<div class="space-y-4 py-3 text-xs">
			<!-- Agent Selection -->
			<div class="space-y-1.5">
				<label class="font-semibold text-foreground">Agent Username</label>
				{#if editId && (formAgent.toUpperCase() === 'DEFAULT' || formAgent.toUpperCase() === 'GLOBAL_DEFAULT')}
					<Input value="GLOBAL DEFAULT" disabled class="h-9 bg-muted/40 font-bold" />
				{:else}
					<div class="space-y-1">
						{#if availableAgents.length > 0}
							<select
								bind:value={formAgent}
								class="w-full h-9 rounded-xl border border-border bg-background px-3 text-xs outline-none focus:ring-1 focus:ring-primary"
							>
								<option value="">-- Select Existing Agent or Custom --</option>
								{#each availableAgents as ag}
									<option value={ag}>{cleanUsername(ag)} ({ag})</option>
								{/each}
							</select>
						{/if}
						<Input
							placeholder="Or enter custom agent username (e.g. TYRESOLES\NAME)"
							bind:value={formAgent}
							class="h-9 rounded-xl text-xs"
						/>
					</div>
				{/if}
			</div>

			<!-- Targets Grid -->
			<div class="grid grid-cols-3 gap-2.5">
				<div class="space-y-1">
					<label class="font-semibold text-foreground">Daily Target</label>
					<Input
						type="number"
						min="1"
						max="500"
						bind:value={formDaily}
						oninput={handleDailyChange}
						class="h-9 rounded-xl font-mono text-center text-xs font-bold"
					/>
					<span class="text-[10px] text-muted-foreground block text-center">calls/day</span>
				</div>
				<div class="space-y-1">
					<label class="font-semibold text-foreground">Weekly Target</label>
					<Input
						type="number"
						min="1"
						max="2500"
						bind:value={formWeekly}
						class="h-9 rounded-xl font-mono text-center text-xs font-bold"
					/>
					<span class="text-[10px] text-muted-foreground block text-center">calls/week</span>
				</div>
				<div class="space-y-1">
					<label class="font-semibold text-foreground">Monthly Target</label>
					<Input
						type="number"
						min="1"
						max="10000"
						bind:value={formMonthly}
						class="h-9 rounded-xl font-mono text-center text-xs font-bold"
					/>
					<span class="text-[10px] text-muted-foreground block text-center">calls/month</span>
				</div>
			</div>

			<!-- Active Switch -->
			<div class="flex items-center justify-between p-3 rounded-xl bg-muted/20 border border-border/50">
				<div>
					<div class="font-semibold text-xs text-foreground">Active Target</div>
					<div class="text-[10px] text-muted-foreground">Enabled for calling performance dashboards</div>
				</div>
				<input
					type="checkbox"
					bind:checked={formIsActive}
					class="size-4 accent-primary rounded cursor-pointer"
				/>
			</div>

			<!-- Notes -->
			<div class="space-y-1.5">
				<label class="font-semibold text-foreground">Notes / Purpose (Optional)</label>
				<Input
					placeholder="e.g. Standard tele-caller expectation for Western region"
					bind:value={formNotes}
					class="h-9 rounded-xl text-xs"
				/>
			</div>
		</div>

		<Dialog.Footer class="pt-3 border-t border-border/60 flex items-center justify-end gap-2">
			<Button variant="outline" onclick={() => (dialogOpen = false)} class="rounded-xl text-xs">
				Cancel
			</Button>
			<Button onclick={saveDialog} class="rounded-xl bg-primary text-primary-foreground text-xs font-semibold px-4">
				{dialogMode === 'add' ? 'Add Target' : 'Save Changes'}
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
