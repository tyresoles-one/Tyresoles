<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { usePaginatedList } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { Checkbox } from '$lib/components/ui/checkbox';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery, graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import Loader2 from '@lucide/svelte/icons/loader-2';

	type CrmContact = {
		fullName: string;
		companyName?: string | null;
		mobileNo?: string | null;
	};

	type CrmAgentContact = {
		id: string;
		agentUsername: string;
		contactId: string;
		contact?: CrmContact | null;
		allocatedAt: string;
		deallocatedAt?: string | null;
		deallocatedBy?: string | null;
		lastCallOutcome?: string | null;
		lastCallDate?: string | null;
		lastCallNotes?: string | null;
		callCount: number;
	};

	type CrmAgentSummary = {
		agentUsername: string;
		totalAllocated: number;
		activeAllocated: number;
		totalCalls: number;
	};

	type CrmSetting = {
		key: string;
		value: string;
		description?: string | null;
	};

	type GetCrmAgentContactsResult = {
		crmAgentContacts: {
			items: CrmAgentContact[];
			totalCount: number;
		};
	};

	const GetCrmAgentContactsDocument = buildQuery`
		query GetCrmAgentContacts($skip: Int, $take: Int, $where: CrmAgentContactFilterInput, $order: [CrmAgentContactSortInput!]) {
			crmAgentContacts: getCrmAgentContacts(skip: $skip, take: $take, where: $where, order: $order) {
				items {
					id
					agentUsername
					contactId
					allocatedAt
					deallocatedAt
					deallocatedBy
					lastCallOutcome
					lastCallDate
					lastCallNotes
					callCount
					contact {
						fullName
						companyName
						mobileNo
					}
				}
				totalCount
			}
		}
	` as unknown as TypedDocumentNode<GetCrmAgentContactsResult, { skip?: number; take?: number; where?: any; order?: any }>;

	const GetCrmAgentSummaryReportDocument = buildQuery`
		query GetCrmAgentSummaryReport {
			getCrmAgentSummaryReport {
				agentUsername
				totalAllocated
				activeAllocated
				totalCalls
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmAgentSummaryReport: CrmAgentSummary[] }, {}>;

	const GetCrmSettingsDocument = buildQuery`
		query GetCrmSettings {
			getCrmSettings {
				key
				value
				description
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmSettings: CrmSetting[] }, {}>;

	const DeallocateCrmContactDocument = buildMutation`
		mutation DeallocateCrmContact($contactId: UUID!) {
			deallocateCrmContact(contactId: $contactId) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ deallocateCrmContact: { success: boolean; message: string } }, { contactId: string }>;

	const DeallocateCrmContactsDocument = buildMutation`
		mutation DeallocateCrmContacts($contactIds: [UUID!]!) {
			deallocateCrmContacts(contactIds: $contactIds) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ deallocateCrmContacts: { success: boolean; message: string } }, { contactIds: string[] }>;

	const DeallocateUnattendedAgentContactsDocument = buildMutation`
		mutation DeallocateUnattendedAgentContacts($agentUsername: String!) {
			deallocateUnattendedAgentContacts(agentUsername: $agentUsername) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ deallocateUnattendedAgentContacts: { success: boolean; message: string } }, { agentUsername: string }>;

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmSetting: { success: boolean; message: string } }, { key: string; value: string; description?: string | null }>;

	// Workspace Tabs
	let activeTab = $state<'summary' | 'allocations' | 'settings'>('summary');

	// Summary Data State
	let summaries = $state<CrmAgentSummary[]>([]);
	let loadingSummary = $state(false);

	// Settings Data State
	let settings = $state<CrmSetting[]>([]);
	let loadingSettings = $state(false);
	let savingSettingKey = $state('');

	// Allocation Filters
	let filterAgent = $state('');
	let filterStatus = $state<'all' | 'active' | 'deallocated'>('all');
	let isDeallocating = $state<string | null>(null);
	let selectedIds = $state<string[]>([]);

	function buildWhereClause(agent: string, status: string) {
		const andConds: any[] = [];

		if (agent.trim()) {
			andConds.push({
				agentUsername: { contains: agent.trim() }
			});
		}

		if (status === 'active') {
			andConds.push({
				deallocatedAt: { eq: null }
			});
		} else if (status === 'deallocated') {
			andConds.push({
				deallocatedAt: { neq: null }
			});
		}

		return andConds.length > 1 ? { and: andConds } : (andConds[0] ?? null);
	}

	const list = usePaginatedList<CrmAgentContact>({
		query: GetCrmAgentContactsDocument,
		dataPath: 'crmAgentContacts',
		itemsPath: 'crmAgentContacts.items',
		countPath: 'crmAgentContacts.totalCount',
		strategy: 'server',
		pageSize: 25,
		mapSearchToVariables: (term) => {
			const baseWhere = buildWhereClause(filterAgent, filterStatus);
			if (!term) return { where: baseWhere, order: [{ allocatedAt: 'DESC' }] };

			const termCond = {
				or: [
					{ agentUsername: { contains: term } },
					{ lastCallOutcome: { contains: term } }
				]
			};

			const where = baseWhere ? { and: [baseWhere, termCond] } : termCond;
			return { where, order: [{ allocatedAt: 'DESC' }] };
		},
		serverVariableAllowlist: ['where', 'order', 'skip', 'take']
	});

	$effect(() => {
		const agent = filterAgent;
		const status = filterStatus;

		untrack(() => {
			const term = list.searchQuery.value;
			const where = buildWhereClause(agent, status);

			const finalWhere = term 
				? { and: [where || {}, { or: [{ agentUsername: { contains: term } }, { lastCallOutcome: { contains: term } }] }] }
				: where;

			list.pagination.setVariables({ 
				where: finalWhere,
				order: [{ allocatedAt: 'DESC' }]
			});
			list.onRefresh();
		});
	});

	async function loadSummary() {
		loadingSummary = true;
		try {
			const res = await graphqlQuery<{ getCrmAgentSummaryReport: CrmAgentSummary[] }>(GetCrmAgentSummaryReportDocument);
			if (res.success && res.data) {
				summaries = res.data.getCrmAgentSummaryReport;
			}
		} catch (err) {
			console.error('Failed to load agent summaries', err);
			toast.error('Failed to load agent summaries.');
		} finally {
			loadingSummary = false;
		}
	}

	async function loadSettings() {
		loadingSettings = true;
		try {
			const res = await graphqlQuery<{ getCrmSettings: CrmSetting[] }>(GetCrmSettingsDocument);
			if (res.success && res.data) {
				let fetchedSettings = res.data.getCrmSettings;
				
				// Ensure default settings are visible even if not seeded in DB yet
				const expectedSettings = [
					{ key: 'ContactsPerAgent', value: '10', description: 'Maximum active allocated contacts per calling agent' },
					{ key: 'ContactsRecentSalesDaysCooldown', value: '30', description: 'Days from latest invoice to cool down contact from allocation' }
				];

				for (const def of expectedSettings) {
					if (!fetchedSettings.find(s => s.key === def.key)) {
						fetchedSettings.push(def);
					}
				}

				settings = fetchedSettings;
			}
		} catch (err) {
			console.error('Failed to load settings', err);
			toast.error('Failed to load CRM settings.');
		} finally {
			loadingSettings = false;
		}
	}

	async function handleSaveSetting(key: string, value: string, description?: string | null) {
		savingSettingKey = key;
		try {
			const res = await graphqlMutation<{ saveCrmSetting: { success: boolean; message: string } }>(
				SaveCrmSettingDocument,
				{ variables: { key, value, description } }
			);

			if (res.success && res.data?.saveCrmSetting.success) {
				toast.success('Setting updated successfully.');
				await loadSettings();
			} else {
				toast.error(res.error || 'Failed to save setting.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			savingSettingKey = '';
		}
	}

	async function handleDeallocate(contactId: string) {
		if (!confirm('Are you sure you want to deallocate this contact from the agent?')) return;
		isDeallocating = contactId;
		try {
			const res = await graphqlMutation<{ deallocateCrmContact: { success: boolean; message: string } }>(
				DeallocateCrmContactDocument,
				{ variables: { contactId } }
			);

			if (res.success && res.data?.deallocateCrmContact.success) {
				toast.success('Contact deallocated successfully.');
				selectedIds = selectedIds.filter(id => id !== contactId);
				await list.onRefresh();
				await loadSummary();
			} else {
				toast.error(res.error || 'Failed to deallocate contact.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			isDeallocating = null;
		}
	}

	async function handleBulkDeallocate() {
		if (selectedIds.length === 0) return;
		if (!confirm(`Are you sure you want to deallocate ${selectedIds.length} contacts?`)) return;
		
		isBulkDeallocating = true;
		try {
			const res = await graphqlMutation<{ deallocateCrmContacts: { success: boolean; message: string } }>(
				DeallocateCrmContactsDocument,
				{ variables: { contactIds: selectedIds } }
			);

			if (res.success && res.data?.deallocateCrmContacts.success) {
				toast.success(res.data.deallocateCrmContacts.message || 'Contacts deallocated successfully.');
				selectedIds = []; // clear selection
				await list.onRefresh();
				await loadSummary();
			} else {
				toast.error(res.error || 'Failed to bulk deallocate contacts.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			isBulkDeallocating = false;
		}
	}

	let isDeallocatingUnattended = $state<string | null>(null);

	async function handleDeallocateUnattended(agentUsername: string) {
		if (!confirm(`Are you sure you want to deallocate all UNATTENDED contacts (0 calls) for agent: ${agentUsername}?`)) return;
		
		isDeallocatingUnattended = agentUsername;
		try {
			const res = await graphqlMutation<{ deallocateUnattendedAgentContacts: { success: boolean; message: string } }>(
				DeallocateUnattendedAgentContactsDocument,
				{ variables: { agentUsername } }
			);

			if (res.success && res.data?.deallocateUnattendedAgentContacts.success) {
				toast.success(res.data.deallocateUnattendedAgentContacts.message || 'Unattended contacts deallocated successfully.');
				await list.onRefresh();
				await loadSummary();
			} else {
				toast.error(res.error || 'Failed to deallocate unattended contacts.');
			}
		} catch (err: any) {
			toast.error(err.message || 'An error occurred.');
		} finally {
			isDeallocatingUnattended = null;
		}
	}

	onMount(() => {
		loadSummary();
		loadSettings();
	});

	function formatDate(dateStr?: string | null) {
		if (!dateStr) return '—';
		let normalizedStr = dateStr;
		if (!dateStr.endsWith('Z') && !dateStr.includes('+') && !/-\d{2}:\d{2}$/.test(dateStr)) {
			normalizedStr = dateStr + 'Z';
		}
		const date = new Date(normalizedStr);
		return date.toLocaleString('en-IN', {
			day: '2-digit',
			month: 'short',
			year: 'numeric',
			hour: '2-digit',
			minute: '2-digit',
			hour12: true
		});
	}

	function formatAgentName(name: string) {
		if (!name) return name;
		return name.replace(/^TYRESOLES\\/i, 'TS: ');
	}
</script>

<svelte:head>
	<title>CRM Supervisor Dashboard | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-background text-foreground pb-20 selection:bg-primary/20">
	<div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pt-8">
		<!-- Page Header -->
		<div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
			<div class="space-y-1">
				<div class="flex items-center gap-3">
					<div class="p-3 rounded-2xl bg-indigo-500/10 text-indigo-600 dark:bg-indigo-950/40 dark:text-indigo-400">
						<Icon name="users" class="size-6" />
					</div>
					<h1 class="text-3xl font-extrabold tracking-tight">CRM Calling Supervisor</h1>
				</div>
				<p class="text-muted-foreground text-sm pl-1">
					Monitor calling agent distributions, review aggregated stats, and adjust distribution settings.
				</p>
			</div>
		</div>

		<!-- Tab Bar -->
		<div class="flex border-b border-border mb-6">
			<button
				onclick={() => (activeTab = 'summary')}
				class="py-3 px-6 font-semibold text-sm border-b-2 transition-all flex items-center gap-2 {activeTab === 'summary' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
			>
				<Icon name="activity" class="size-4" />
				Agent Summaries
			</button>
			<button
				onclick={() => (activeTab = 'allocations')}
				class="py-3 px-6 font-semibold text-sm border-b-2 transition-all flex items-center gap-2 {activeTab === 'allocations' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
			>
				<Icon name="history" class="size-4" />
				Allocation History
			</button>
			<button
				onclick={() => (activeTab = 'settings')}
				class="py-3 px-6 font-semibold text-sm border-b-2 transition-all flex items-center gap-2 {activeTab === 'settings' ? 'border-primary text-primary bg-background' : 'border-transparent text-muted-foreground hover:text-foreground'}"
			>
				<Icon name="settings" class="size-4" />
				CRM Settings
			</button>
		</div>

		<!-- Tab Contents -->
		{#if activeTab === 'summary'}
			<!-- Agent Summaries Tab -->
			{#if loadingSummary}
				<div class="flex flex-col items-center justify-center py-20 gap-3">
					<Loader2 class="size-8 animate-spin text-primary" />
					<span class="text-sm text-muted-foreground">Loading summaries...</span>
				</div>
			{:else if summaries.length === 0}
				<div class="bg-card border border-border rounded-2xl p-12 text-center text-muted-foreground">
					<Icon name="users" class="size-12 mx-auto text-muted-foreground/40 mb-3" />
					<p class="font-medium text-foreground">No active allocations yet</p>
					<p class="text-sm mt-1">When agents visit their call center page, their allocations and summaries will appear here.</p>
				</div>
			{:else}
				<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
					{#each summaries as sum}
						<div class="bg-card border border-border hover:border-indigo-500/20 rounded-2xl p-6 shadow-xs transition-all duration-300 relative group overflow-hidden">
							<div class="absolute inset-0 bg-gradient-to-br from-indigo-500/0 to-indigo-500/2.5 opacity-0 group-hover:opacity-100 transition-opacity"></div>
							
							<div class="flex items-center justify-between gap-4 mb-4 relative z-10">
								<div class="space-y-1 w-[calc(100%-40px)]">
									<h3 class="font-bold text-lg text-foreground group-hover:text-primary transition-colors truncate" title={sum.agentUsername}>
										{formatAgentName(sum.agentUsername)}
									</h3>
									<span class="text-xs bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 px-2 py-0.5 rounded font-medium">
										Calling Agent
									</span>
								</div>
								<div class="flex items-center gap-2">
									<Button 
										variant="ghost" 
										size="icon" 
										class="h-9 w-9 text-muted-foreground hover:text-destructive hover:bg-destructive/10" 
										title="Deallocate Unattended Contacts"
										onclick={() => handleDeallocateUnattended(sum.agentUsername)}
										disabled={isDeallocatingUnattended === sum.agentUsername}
									>
										{#if isDeallocatingUnattended === sum.agentUsername}
											<Loader2 class="size-4 animate-spin" />
										{:else}
											<Icon name="user-minus" class="size-4" />
										{/if}
									</Button>
									<div class="p-3 bg-muted rounded-xl text-muted-foreground">
										<Icon name="phone" class="size-5" />
									</div>
								</div>
							</div>

							<div class="grid grid-cols-3 gap-3 border-t border-border pt-4 relative z-10">
								<div class="text-center">
									<p class="text-xs text-muted-foreground">Active</p>
									<p class="text-xl font-bold text-emerald-600 dark:text-emerald-400 mt-1">{sum.activeAllocated}</p>
								</div>
								<div class="text-center border-x border-border">
									<p class="text-xs text-muted-foreground">Total</p>
									<p class="text-xl font-bold text-foreground mt-1">{sum.totalAllocated}</p>
								</div>
								<div class="text-center">
									<p class="text-xs text-muted-foreground">Calls</p>
									<p class="text-xl font-bold text-indigo-600 dark:text-indigo-400 mt-1">{sum.totalCalls}</p>
								</div>
							</div>
						</div>
					{/each}
				</div>
			{/if}

		{:else if activeTab === 'allocations'}
			<!-- Allocation History Tab -->
			<div class="space-y-4">
				<!-- Filters Panel -->
				<div class="bg-card border border-border rounded-2xl p-4 flex flex-col md:flex-row gap-4 items-center justify-between">
					<div class="flex flex-col md:flex-row gap-3 w-full md:w-auto flex-1">
						<div class="relative w-full md:max-w-xs">
							<Icon name="search" class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
							<Input
								placeholder="Search outcomes..."
								bind:value={list.searchQuery.value}
								class="pl-9 rounded-xl h-9 bg-muted/30 border-none shadow-none text-sm"
							/>
						</div>
						
						<div class="relative w-full md:max-w-xs">
							<Icon name="user" class="absolute left-3 top-2.5 size-4 text-muted-foreground" />
							<Input
								placeholder="Filter Agent..."
								bind:value={filterAgent}
								class="pl-9 rounded-xl h-9 bg-muted/30 border-none shadow-none text-sm"
							/>
						</div>

						<select
							bind:value={filterStatus}
							class="h-9 border border-input rounded-xl bg-transparent px-3 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring focus-visible:border-ring transition-colors shadow-none w-full md:w-40"
						>
							<option value="all">All Statuses</option>
							<option value="active">Active Only</option>
							<option value="deallocated">Deallocated Only</option>
						</select>
					</div>

					<div class="flex items-center gap-2 shrink-0">
						{#if selectedIds.length > 0}
							<Button 
								variant="destructive" 
								size="sm" 
								class="h-9 rounded-xl px-4 text-xs font-semibold shrink-0"
								onclick={handleBulkDeallocate}
								disabled={isDeallocating === 'bulk'}
							>
								{#if isDeallocating === 'bulk'}
									<Loader2 class="size-4 animate-spin mr-1" />
								{/if}
								Deallocate Selected ({selectedIds.length})
							</Button>
						{/if}

						<Button 
							variant="outline" 
							size="sm" 
							class="h-9 rounded-xl px-4 text-xs font-semibold shrink-0"
							onclick={() => {
								list.searchQuery.value = '';
								filterAgent = '';
								filterStatus = 'all';
								selectedIds = [];
							}}
						>
							Clear Filters
						</Button>
					</div>
				</div>

				<!-- Allocation History Table -->
				<div class="bg-card border border-border rounded-2xl overflow-hidden shadow-xs">
					<div class="overflow-x-auto">
						<table class="w-full text-left border-collapse text-sm">
							<thead>
								<tr class="bg-muted/30 border-b border-border text-muted-foreground text-xs uppercase font-bold">
									<th class="p-4 w-12">
										<Checkbox
											checked={list.items.length > 0 && list.items.filter(i => !i.deallocatedAt).every(i => selectedIds.includes(i.contactId))}
											onCheckedChange={(checked) => {
												if (checked) {
													const visibleIds = list.items.filter(i => !i.deallocatedAt).map(i => i.contactId);
													selectedIds = [...new Set([...selectedIds, ...visibleIds])];
												} else {
													const visibleIds = list.items.map(i => i.contactId);
													selectedIds = selectedIds.filter(id => !visibleIds.includes(id));
												}
											}}
										/>
									</th>
									<th class="p-4">Agent</th>
									<th class="p-4">Contact</th>
									<th class="p-4">Allocated At</th>
									<th class="p-4">Calls</th>
									<th class="p-4">Last Call Outcome</th>
									<th class="p-4">Status / Deallocated</th>
									<th class="p-4 text-right">Actions</th>
								</tr>
							</thead>
							<tbody class="divide-y divide-border">
								{#if list.loading && list.items.length === 0}
									<tr>
										<td colspan="8" class="p-12 text-center">
											<Loader2 class="size-6 animate-spin mx-auto text-primary mb-2" />
											<span class="text-xs text-muted-foreground">Loading allocations...</span>
										</td>
									</tr>
								{:else if list.items.length === 0}
									<tr>
										<td colspan="8" class="p-12 text-center text-muted-foreground">
											No allocation records found.
										</td>
									</tr>
								{:else}
									{#each list.items as item (item.id)}
										<tr class="hover:bg-muted/10 transition-colors">
											<td class="p-4 w-12 text-center">
												{#if !item.deallocatedAt}
													<Checkbox
														checked={selectedIds.includes(item.contactId)}
														onCheckedChange={(c) => {
															if (c) selectedIds = [...selectedIds, item.contactId];
															else selectedIds = selectedIds.filter(id => id !== item.contactId);
														}}
													/>
												{/if}
											</td>
											<td class="p-4 font-bold text-foreground" title={item.agentUsername}>{formatAgentName(item.agentUsername)}</td>
											<td class="p-4">
												{#if item.contact}
													<div class="space-y-0.5">
														<p class="font-semibold text-foreground">{item.contact.fullName}</p>
														{#if item.contact.companyName}
															<p class="text-[11px] text-muted-foreground">{item.contact.companyName}</p>
														{/if}
														{#if item.contact.mobileNo}
															<p class="text-[11px] text-muted-foreground font-mono">{item.contact.mobileNo}</p>
														{/if}
													</div>
												{:else}
													<span class="text-muted-foreground text-xs italic">Unknown Contact</span>
												{/if}
											</td>
											<td class="p-4 text-xs text-muted-foreground font-mono">{formatDate(item.allocatedAt)}</td>
											<td class="p-4">
												<span class="bg-primary/10 text-primary font-bold px-2 py-0.5 rounded text-xs">
													{item.callCount} calls
												</span>
											</td>
											<td class="p-4">
												{#if item.lastCallOutcome}
													<div class="space-y-0.5">
														<span class="text-xs font-semibold px-2 py-0.5 rounded bg-muted text-foreground">
															{item.lastCallOutcome}
														</span>
														{#if item.lastCallDate}
															<p class="text-[10px] text-muted-foreground font-mono">{formatDate(item.lastCallDate)}</p>
														{/if}
													</div>
												{:else}
													<span class="text-muted-foreground text-xs">No calls</span>
												{/if}
											</td>
											<td class="p-4">
												{#if item.deallocatedAt}
													<div class="space-y-0.5 text-rose-600 dark:text-rose-400">
														<span class="text-xs font-medium bg-rose-500/10 px-2 py-0.5 rounded">
															Deallocated
														</span>
														<p class="text-[10px] text-muted-foreground font-mono">{formatDate(item.deallocatedAt)}</p>
														{#if item.deallocatedBy}
															<p class="text-[9px] text-muted-foreground font-medium">By: {item.deallocatedBy}</p>
														{/if}
													</div>
												{:else}
													<span class="text-xs font-medium bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 px-2 py-0.5 rounded">
														Active
													</span>
												{/if}
											</td>
											<td class="p-4 text-right">
												{#if !item.deallocatedAt}
													<Button
														variant="outline"
														size="sm"
														onclick={() => handleDeallocate(item.contactId)}
														disabled={isDeallocating === item.contactId}
														class="text-rose-600 border-rose-100 hover:bg-rose-50 h-8 text-xs font-semibold"
													>
														{#if isDeallocating === item.contactId}
															<Loader2 class="size-3 animate-spin mr-1" />
															Deallocating...
														{:else}
															<Icon name="user-minus" class="size-3.5 mr-1" />
															Deallocate
														{/if}
													</Button>
												{:else}
													<span class="text-muted-foreground/50 text-xs">—</span>
												{/if}
											</td>
										</tr>
									{/each}
								{/if}
							</tbody>
						</table>
					</div>

					<!-- Pagination / Load More -->
					{#if list.hasMore}
						<div class="p-4 text-center border-t border-border bg-card">
							<Button 
								variant="outline" 
								size="sm" 
								class="w-full max-w-xs text-xs rounded-xl h-9 font-semibold" 
								onclick={() => list.onLoadMore()}
								disabled={list.loadingMore}
							>
								{#if list.loadingMore}
									<Loader2 class="size-3 animate-spin mr-2" />
									Loading...
								{:else}
									Load More Records
								{/if}
							</Button>
						</div>
					{/if}
				</div>
			</div>

		{:else}
			<!-- CRM Settings Tab -->
			<div class="bg-card border border-border rounded-2xl p-6 max-w-2xl mx-auto space-y-6">
				<div>
					<h2 class="text-lg font-bold text-foreground">CRM Configuration Options</h2>
					<p class="text-xs text-muted-foreground">Configure thresholds and automated rules for lead and contact calling agents.</p>
				</div>

				{#if loadingSettings}
					<div class="flex items-center justify-center py-10 gap-2">
						<Loader2 class="size-5 animate-spin text-primary" />
						<span class="text-sm text-muted-foreground">Loading settings...</span>
					</div>
				{:else if settings.length === 0}
					<div class="p-6 text-center text-muted-foreground text-sm border border-dashed rounded-xl">
						No configuration settings found. Setting defaults are active.
					</div>
				{:else}
					<div class="divide-y divide-border">
						{#each settings as setting (setting.key)}
							<div class="py-4 first:pt-0 last:pb-0 flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
								<div class="space-y-1 flex-1">
									<span class="font-semibold text-sm text-foreground font-mono">{setting.key}</span>
									{#if setting.description}
										<p class="text-xs text-muted-foreground max-w-md">{setting.description}</p>
									{/if}
								</div>
								
								<div class="flex items-center gap-3 w-full sm:w-auto shrink-0">
									<Input
										bind:value={setting.value}
										class="w-full sm:w-28 text-center rounded-xl bg-muted/20 border-border text-sm h-9"
									/>
									
									<Button
										size="sm"
										onclick={() => handleSaveSetting(setting.key, setting.value, setting.description)}
										disabled={savingSettingKey === setting.key}
										class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl font-medium shrink-0 h-9 px-4"
									>
										{#if savingSettingKey === setting.key}
											<Loader2 class="size-3.5 animate-spin" />
										{:else}
											Save
										{/if}
									</Button>
								</div>
							</div>
						{/each}
					</div>
				{/if}
			</div>
		{/if}
	</div>
</div>

<style>
	:global(.animate-spin) {
		animation: spin 1s linear infinite;
	}
	@keyframes spin {
		from {
			transform: rotate(0deg);
		}
		to {
			transform: rotate(360deg);
		}
	}
</style>
