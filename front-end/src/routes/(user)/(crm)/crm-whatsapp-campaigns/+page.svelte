<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import {
		GET_WHATSAPP_CAMPAIGNS,
		GET_WHATSAPP_ACCOUNT_STATUS,
		PAUSE_WHATSAPP_CAMPAIGN,
		RESUME_WHATSAPP_CAMPAIGN,
		CANCEL_WHATSAPP_CAMPAIGN,
		DELETE_WHATSAPP_CAMPAIGN,
		type CrmWhatsappCampaign,
		type WabaHealthStatus
	} from './whatsappQueries';

	let campaigns = $state<CrmWhatsappCampaign[]>([]);
	let loading = $state(true);
	let filterStatus = $state('ALL');
	let searchQuery = $state('');

	let accountStatus = $state<WabaHealthStatus | null>(null);
	let loadingStatus = $state(false);

	async function loadData() {
		loading = true;
		try {
			const [campRes, statRes] = await Promise.all([
				graphqlQuery<{ getCrmWhatsappCampaigns: CrmWhatsappCampaign[] }>(GET_WHATSAPP_CAMPAIGNS, {
					variables: { status: filterStatus === 'ALL' ? null : filterStatus }
				}),
				graphqlQuery<{ getWhatsappAccountStatus: WabaHealthStatus }>(GET_WHATSAPP_ACCOUNT_STATUS)
			]);

			if (campRes.success && campRes.data?.getCrmWhatsappCampaigns) {
				campaigns = campRes.data.getCrmWhatsappCampaigns;
			}
			if (statRes.success && statRes.data?.getWhatsappAccountStatus) {
				accountStatus = statRes.data.getWhatsappAccountStatus;
			}
		} catch (e: any) {
			toast.error('Failed to load WhatsApp campaigns: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadData();
	});

	const filteredCampaigns = $derived(
		campaigns.filter((c) => {
			const matchesStatus = filterStatus === 'ALL' || c.status === filterStatus;
			const matchesSearch =
				!searchQuery.trim() ||
				c.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
				(c.templateName && c.templateName.toLowerCase().includes(searchQuery.toLowerCase()));
			return matchesStatus && matchesSearch;
		})
	);

	const stats = $derived.by(() => {
		const totalCampaigns = campaigns.length;
		const totalRecipients = campaigns.reduce((sum, c) => sum + c.totalRecipients, 0);
		const totalSent = campaigns.reduce((sum, c) => sum + c.sentCount, 0);
		const totalDelivered = campaigns.reduce((sum, c) => sum + c.deliveredCount, 0);
		const totalRead = campaigns.reduce((sum, c) => sum + c.readCount, 0);
		const totalReplied = campaigns.reduce((sum, c) => sum + c.repliedCount, 0);
		const totalFailed = campaigns.reduce((sum, c) => sum + c.failedCount, 0);
		const totalCost = campaigns.reduce((sum, c) => sum + (c.actualCost ?? (c.deliveredCount * c.costPerMessage)), 0);

		const deliveryRate = totalSent > 0 ? Math.round((totalDelivered / totalSent) * 100) : 0;
		const readRate = totalDelivered > 0 ? Math.round((totalRead / totalDelivered) * 100) : 0;
		const replyRate = totalDelivered > 0 ? Math.round((totalReplied / totalDelivered) * 100) : 0;

		return {
			totalCampaigns,
			totalRecipients,
			totalSent,
			totalDelivered,
			totalRead,
			totalReplied,
			totalFailed,
			totalCost,
			deliveryRate,
			readRate,
			replyRate
		};
	});

	async function handlePause(id: string) {
		const res = await graphqlMutation(PAUSE_WHATSAPP_CAMPAIGN, {
			variables: { campaignId: id }
		});
		if (res.success) {
			toast.success('Campaign paused.');
			loadData();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to pause campaign.';
			toast.error(errMsg);
		}
	}

	async function handleResume(id: string) {
		const res = await graphqlMutation(RESUME_WHATSAPP_CAMPAIGN, {
			variables: { campaignId: id }
		});
		if (res.success) {
			toast.success('Campaign resumed.');
			loadData();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to resume campaign.';
			toast.error(errMsg);
		}
	}

	async function handleCancel(id: string) {
		if (!confirm('Are you sure you want to cancel this campaign?')) return;
		const res = await graphqlMutation(CANCEL_WHATSAPP_CAMPAIGN, {
			variables: { campaignId: id }
		});
		if (res.success) {
			toast.success('Campaign cancelled.');
			loadData();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to cancel campaign.';
			toast.error(errMsg);
		}
	}

	async function handleDelete(id: string) {
		if (!confirm('Are you sure you want to delete this campaign? This will remove all recipient stats.')) return;
		const res = await graphqlMutation(DELETE_WHATSAPP_CAMPAIGN, {
			variables: { campaignId: id }
		});
		if (res.success) {
			toast.success('Campaign deleted.');
			loadData();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to delete campaign.';
			toast.error(errMsg);
		}
	}

	function getStatusBadge(status: string) {
		switch (status) {
			case 'Draft':
				return 'bg-slate-100 text-slate-700 border-slate-300 dark:bg-slate-800 dark:text-slate-300';
			case 'Scheduled':
				return 'bg-blue-100 text-blue-700 border-blue-300 dark:bg-blue-950 dark:text-blue-300';
			case 'InProgress':
				return 'bg-amber-100 text-amber-700 border-amber-300 animate-pulse dark:bg-amber-950 dark:text-amber-300';
			case 'Completed':
				return 'bg-emerald-100 text-emerald-700 border-emerald-300 dark:bg-emerald-950 dark:text-emerald-300';
			case 'Paused':
				return 'bg-purple-100 text-purple-700 border-purple-300 dark:bg-purple-950 dark:text-purple-300';
			case 'Cancelled':
			case 'Failed':
				return 'bg-red-100 text-red-700 border-red-300 dark:bg-red-950 dark:text-red-300';
			default:
				return 'bg-muted text-foreground border-border';
		}
	}
</script>

<div class="space-y-6 pb-12">
	<!-- Page Header -->
	<PageHeading
		backHref="/crm-contacts"
		backLabel="Back to CRM"
		icon="message-circle"
		title="WhatsApp Marketing Campaigns"
		description="Automated Meta WhatsApp Business Cloud API broadcast campaigns, real-time read tracking, and lead responses."
	>
		{#snippet actions()}
			<div class="flex flex-wrap items-center gap-2">
				<a
					href="/crm-masters?category=media"
					target="_blank"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-border bg-background text-xs font-medium text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors shadow-2xs"
					title="Configure WhatsApp Templates, Media & Catalog in CRM Masters"
				>
					<Icon name="database" class="w-4 h-4 text-emerald-600" />
					<span>WhatsApp Masters</span>
				</a>
				<Button
					variant="outline"
					size="sm"
					class="gap-1.5"
					onclick={() => goto('/crm-whatsapp-campaigns/inbox')}
				>
					<Icon name="inbox" class="w-4 h-4 text-amber-500" />
					<span>Inbound Messages</span>
				</Button>
				<Button
					variant="outline"
					size="sm"
					class="gap-1.5"
					onclick={() => goto('/crm-whatsapp-campaigns/templates')}
				>
					<Icon name="layout-template" class="w-4 h-4 text-emerald-600" />
					<span>Templates Studio</span>
				</Button>
				<Button
					variant="outline"
					size="sm"
					class="gap-1.5"
					onclick={() => goto('/crm-whatsapp-campaigns/settings')}
				>
					<Icon name="settings" class="w-4 h-4 text-muted-foreground" />
					<span>WABA & Health</span>
				</Button>
				<Button
					size="sm"
					class="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white"
					onclick={() => goto('/crm-whatsapp-campaigns/new')}
				>
					<Icon name="plus" class="w-4 h-4" />
					<span>New Campaign</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- Live WABA Health Banner -->
	{#if accountStatus}
		<div class="bg-card border border-border rounded-2xl p-4 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
			<div class="flex items-center gap-3">
				<div class="w-10 h-10 rounded-xl bg-emerald-500/10 text-emerald-600 flex items-center justify-center font-bold text-xl">
					<Icon name="message-circle" class="w-6 h-6" />
				</div>
				<div>
					<div class="flex items-center gap-2">
						<span class="font-semibold text-sm">{accountStatus.verifiedName || 'WhatsApp Business Account'}</span>
						{#if accountStatus.qualityRating === 'GREEN'}
							<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
								<span class="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> Quality: High
							</span>
						{:else if accountStatus.qualityRating === 'YELLOW'}
							<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300">
								<span class="w-1.5 h-1.5 rounded-full bg-amber-500"></span> Quality: Medium
							</span>
						{:else}
							<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300">
								<span class="w-1.5 h-1.5 rounded-full bg-red-500"></span> Quality: Warning
							</span>
						{/if}
					</div>
					<div class="text-xs text-muted-foreground mt-0.5">
						Sender: <span class="font-medium text-foreground">{accountStatus.displayPhoneNumber || 'Configured via WABA'}</span> • Tier: <span class="uppercase font-medium text-foreground">{accountStatus.messagingLimitTier.replace('_', ' ')}</span>
					</div>
				</div>
			</div>

			<!-- Daily Quota Gauge -->
			<div class="flex items-center gap-4 border-t md:border-t-0 md:border-l border-border pt-3 md:pt-0 md:pl-6">
				<div class="text-right">
					<div class="text-xs text-muted-foreground">Today's Tier Quota Usage</div>
					<div class="text-sm font-bold">
						{accountStatus.currentDayUsage.toLocaleString()} / {accountStatus.dailyLimit.toLocaleString()}
					</div>
				</div>
				<div class="w-28 bg-muted rounded-full h-2.5 overflow-hidden">
					<div
						class="bg-emerald-500 h-2.5 rounded-full transition-all duration-500"
						style="width: {Math.min(100, Math.round((accountStatus.currentDayUsage / (accountStatus.dailyLimit || 1)) * 100))}%"
					></div>
				</div>
			</div>
		</div>
	{/if}

	<!-- KPI Metric Cards -->
	<div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Campaigns</div>
			<div class="text-2xl font-bold mt-1">{stats.totalCampaigns}</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">Total Created</div>
		</div>

		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Dispatched</div>
			<div class="text-2xl font-bold text-blue-600 mt-1">{stats.totalSent.toLocaleString()}</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">Messages Sent</div>
		</div>

		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Delivery Rate</div>
			<div class="text-2xl font-bold text-emerald-600 mt-1">{stats.deliveryRate}%</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">{stats.totalDelivered.toLocaleString()} Delivered</div>
		</div>

		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Read Rate (Blue Ticks)</div>
			<div class="text-2xl font-bold text-indigo-600 mt-1">{stats.readRate}%</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">{stats.totalRead.toLocaleString()} Opened & Read</div>
		</div>

		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Customer Replies</div>
			<div class="text-2xl font-bold text-amber-600 mt-1">{stats.replyRate}%</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">{stats.totalReplied.toLocaleString()} Inbound Inquiries</div>
		</div>

		<div class="bg-card border border-border rounded-xl p-3.5 shadow-sm">
			<div class="text-xs font-medium text-muted-foreground">Est. Spend</div>
			<div class="text-2xl font-bold text-foreground mt-1">₹{Math.round(stats.totalCost).toLocaleString()}</div>
			<div class="text-[11px] text-muted-foreground mt-0.5">@ ₹0.80/delivered</div>
		</div>
	</div>

	<!-- Controls & Search -->
	<div class="flex flex-col sm:flex-row items-center justify-between gap-3 bg-card border border-border p-3 rounded-xl">
		<div class="flex items-center gap-2 w-full sm:w-auto">
			<div class="relative w-full sm:w-72">
				<Icon name="search" class="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
				<input
					type="text"
					bind:value={searchQuery}
					placeholder="Search campaigns or templates..."
					class="w-full pl-9 pr-3 py-1.5 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary"
				/>
			</div>

			<select
				bind:value={filterStatus}
				class="py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary"
			>
				<option value="ALL">All Statuses</option>
				<option value="Draft">Draft</option>
				<option value="Scheduled">Scheduled</option>
				<option value="InProgress">In Progress</option>
				<option value="Completed">Completed</option>
				<option value="Paused">Paused</option>
				<option value="Cancelled">Cancelled</option>
			</select>
		</div>

		<div class="text-xs text-muted-foreground self-end sm:self-center">
			Showing <span class="font-medium text-foreground">{filteredCampaigns.length}</span> of {campaigns.length} campaigns
		</div>
	</div>

	<!-- Campaigns Data Table -->
	<div class="bg-card border border-border rounded-2xl overflow-hidden shadow-sm">
		{#if loading}
			<div class="p-12 text-center text-muted-foreground flex flex-col items-center gap-2">
				<Icon name="loader-2" class="w-6 h-6 animate-spin text-primary" />
				<span class="text-sm">Loading WhatsApp campaigns...</span>
			</div>
		{:else if filteredCampaigns.length === 0}
			<div class="p-16 text-center text-muted-foreground flex flex-col items-center gap-3">
				<div class="w-12 h-12 rounded-2xl bg-muted flex items-center justify-center text-muted-foreground">
					<Icon name="message-square" class="w-6 h-6 opacity-60" />
				</div>
				<h3 class="font-semibold text-foreground">No WhatsApp campaigns found</h3>
				<p class="text-xs max-w-sm">Create your first automated marketing broadcast to connect with fleet customers directly on WhatsApp.</p>
				<Button
					size="sm"
					class="mt-2 bg-emerald-600 hover:bg-emerald-700 text-white"
					onclick={() => goto('/crm-whatsapp-campaigns/new')}
				>
					<Icon name="plus" class="w-4 h-4 mr-1" />
					Create WhatsApp Campaign
				</Button>
			</div>
		{:else}
			<div class="overflow-x-auto">
				<table class="w-full text-xs text-left border-collapse">
					<thead class="bg-muted/50 border-b border-border text-muted-foreground uppercase text-[10px] tracking-wider">
						<tr>
							<th class="py-3 px-4 font-semibold">Campaign Name</th>
							<th class="py-3 px-4 font-semibold">Meta Template</th>
							<th class="py-3 px-4 font-semibold">Status</th>
							<th class="py-3 px-4 font-semibold">Delivery Progress</th>
							<th class="py-3 px-4 font-semibold text-center">Read (Blue Ticks)</th>
							<th class="py-3 px-4 font-semibold text-center">Replies</th>
							<th class="py-3 px-4 font-semibold">Date</th>
							<th class="py-3 px-4 font-semibold text-right">Actions</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border">
						{#each filteredCampaigns as c (c.id)}
							<tr class="hover:bg-muted/30 transition-colors group">
								<td class="py-3 px-4">
									<div class="font-semibold text-foreground hover:text-primary cursor-pointer flex items-center gap-1.5"
										onclick={() => goto(`/crm-whatsapp-campaigns/${c.id}`)}>
										<Icon name="message-circle" class="w-3.5 h-3.5 text-emerald-600 shrink-0" />
										<span>{c.name}</span>
									</div>
									<div class="text-[10px] text-muted-foreground mt-0.5">
										Recipients: {c.totalRecipients.toLocaleString()} contacts
									</div>
								</td>

								<td class="py-3 px-4">
									<span class="inline-flex items-center gap-1 font-mono text-[11px] bg-muted px-2 py-0.5 rounded border border-border/60">
										{c.template?.name || c.templateName || 'custom'}
									</span>
								</td>

								<td class="py-3 px-4">
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-medium border {getStatusBadge(c.status)}">
										{c.status}
									</span>
									{#if c.failureReason}
										<div class="text-[10px] text-red-500 mt-1 max-w-xs truncate" title={c.failureReason}>
											{c.failureReason}
										</div>
									{/if}
								</td>

								<td class="py-3 px-4 w-44">
									<div class="space-y-1">
										<div class="flex items-center justify-between text-[10px]">
											<span class="text-muted-foreground">{c.deliveredCount} / {c.totalRecipients || c.sentCount}</span>
											<span class="font-medium">
												{c.sentCount > 0 ? Math.round((c.deliveredCount / c.sentCount) * 100) : 0}%
											</span>
										</div>
										<div class="w-full bg-muted rounded-full h-1.5 overflow-hidden">
											<div
												class="bg-emerald-500 h-1.5 rounded-full"
												style="width: {c.sentCount > 0 ? Math.min(100, (c.deliveredCount / c.sentCount) * 100) : 0}%"
											></div>
										</div>
									</div>
								</td>

								<td class="py-3 px-4 text-center">
									<div class="font-semibold text-indigo-600 dark:text-indigo-400">
										{c.readCount}
									</div>
									<div class="text-[10px] text-muted-foreground">
										{c.deliveredCount > 0 ? Math.round((c.readCount / c.deliveredCount) * 100) : 0}% read
									</div>
								</td>

								<td class="py-3 px-4 text-center">
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold {c.repliedCount > 0 ? 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300' : 'text-muted-foreground'}">
										{c.repliedCount}
									</span>
								</td>

								<td class="py-3 px-4 text-muted-foreground text-[11px]">
									{#if c.scheduledAt}
										<div>{new Date(c.scheduledAt).toLocaleDateString()}</div>
										<div class="text-[10px]">{new Date(c.scheduledAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</div>
									{:else}
										<div>{new Date(c.createdAt).toLocaleDateString()}</div>
									{/if}
								</td>

								<td class="py-3 px-4 text-right">
									<div class="flex items-center justify-end gap-1">
										<Button
											variant="ghost"
											size="sm"
											class="h-7 px-2 text-xs"
											onclick={() => goto(`/crm-whatsapp-campaigns/${c.id}`)}
											title="View Campaign Analytics & Delivery Ledger"
										>
											<Icon name="bar-chart-2" class="w-3.5 h-3.5 mr-1" />
											Analytics
										</Button>

										{#if c.status === 'InProgress'}
											<Button
												variant="ghost"
												size="sm"
												class="h-7 px-2 text-xs text-amber-600 hover:text-amber-700"
												onclick={() => handlePause(c.id)}
												title="Pause Campaign Dispatch"
											>
												<Icon name="pause" class="w-3.5 h-3.5" />
											</Button>
										{:else if c.status === 'Paused'}
											<Button
												variant="ghost"
												size="sm"
												class="h-7 px-2 text-xs text-emerald-600 hover:text-emerald-700"
												onclick={() => handleResume(c.id)}
												title="Resume Campaign Dispatch"
											>
												<Icon name="play" class="w-3.5 h-3.5" />
											</Button>
										{/if}

										{#if c.status === 'Scheduled' || c.status === 'InProgress'}
											<Button
												variant="ghost"
												size="sm"
												class="h-7 px-2 text-xs text-red-500 hover:text-red-600"
												onclick={() => handleCancel(c.id)}
												title="Cancel Campaign"
											>
												<Icon name="x-circle" class="w-3.5 h-3.5" />
											</Button>
										{/if}

										{#if c.status === 'Draft' || c.status === 'Completed' || c.status === 'Cancelled'}
											<Button
												variant="ghost"
												size="sm"
												class="h-7 px-2 text-xs text-muted-foreground hover:text-red-600"
												onclick={() => handleDelete(c.id)}
												title="Delete Campaign"
											>
												<Icon name="trash-2" class="w-3.5 h-3.5" />
											</Button>
										{/if}
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
