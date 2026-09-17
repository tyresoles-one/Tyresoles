<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';

	interface CrmEmailCampaign {
		id: string;
		name: string;
		subject: string;
		fromName: string;
		fromEmail: string;
		status: string;
		totalRecipients: number;
		sentCount: number;
		deliveredCount: number;
		openedCount: number;
		uniqueOpenedCount: number;
		clickedCount: number;
		uniqueClickedCount: number;
		bouncedCount: number;
		unsubscribedCount: number;
		scheduledAt?: string | null;
		createdAt: string;
	}

	let campaigns = $state<CrmEmailCampaign[]>([]);
	let loading = $state(true);
	let filterStatus = $state('ALL');

	const GET_CAMPAIGNS = buildQuery`
		query GetCrmEmailCampaigns {
			getCrmEmailCampaigns {
				id
				name
				subject
				fromName
				fromEmail
				status
				totalRecipients
				sentCount
				deliveredCount
				openedCount
				uniqueOpenedCount
				clickedCount
				uniqueClickedCount
				bouncedCount
				unsubscribedCount
				scheduledAt
				createdAt
			}
		}
	`;

	const PAUSE_CAMPAIGN = buildMutation`
		mutation PauseCampaign($id: UUID!) {
			pauseCrmEmailCampaign(campaignId: $id) {
				id
				status
			}
		}
	`;

	const RESUME_CAMPAIGN = buildMutation`
		mutation ResumeCampaign($id: UUID!) {
			resumeCrmEmailCampaign(campaignId: $id) {
				id
				status
			}
		}
	`;

	async function loadCampaigns() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmEmailCampaigns: CrmEmailCampaign[] }>(GET_CAMPAIGNS);
			if (res.success && res.data?.getCrmEmailCampaigns) {
				campaigns = res.data.getCrmEmailCampaigns;
			}
		} catch (e: any) {
			toast.error('Failed to load campaigns: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadCampaigns();
	});

	const filteredCampaigns = $derived(
		filterStatus === 'ALL' ? campaigns : campaigns.filter((c) => c.status === filterStatus)
	);

	const stats = $derived({
		totalCampaigns: campaigns.length,
		totalSent: campaigns.reduce((acc, c) => acc + c.sentCount, 0),
		totalOpens: campaigns.reduce((acc, c) => acc + c.uniqueOpenedCount, 0),
		totalClicks: campaigns.reduce((acc, c) => acc + c.uniqueClickedCount, 0),
		avgOpenRate:
			campaigns.reduce((acc, c) => acc + c.sentCount, 0) > 0
				? (
						(campaigns.reduce((acc, c) => acc + c.uniqueOpenedCount, 0) /
							campaigns.reduce((acc, c) => acc + c.sentCount, 0)) *
						100
					).toFixed(1)
				: '0.0'
	});

	async function togglePause(campaign: CrmEmailCampaign) {
		try {
			if (campaign.status === 'InProgress' || campaign.status === 'Scheduled') {
				const res = await graphqlMutation(PAUSE_CAMPAIGN, { variables: { id: campaign.id } });
				if (res.success) {
					toast.success('Campaign paused');
					await loadCampaigns();
				}
			} else if (campaign.status === 'Paused') {
				const res = await graphqlMutation(RESUME_CAMPAIGN, { variables: { id: campaign.id } });
				if (res.success) {
					toast.success('Campaign resumed');
					await loadCampaigns();
				}
			}
		} catch (e: any) {
			toast.error(e.message);
		}
	}

	function getStatusBadge(status: string) {
		switch (status) {
			case 'Completed':
				return 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20';
			case 'InProgress':
				return 'bg-blue-500/10 text-blue-600 border-blue-500/20 animate-pulse';
			case 'Scheduled':
				return 'bg-amber-500/10 text-amber-600 border-amber-500/20';
			case 'Paused':
				return 'bg-purple-500/10 text-purple-600 border-purple-500/20';
			case 'Cancelled':
				return 'bg-rose-500/10 text-rose-600 border-rose-500/20';
			default:
				return 'bg-slate-500/10 text-slate-600 border-slate-500/20';
		}
	}
</script>

<svelte:head>
	<title>Email Campaigns | Tyresoles CRM</title>
</svelte:head>

<PageHeading
	backHref="/"
	backLabel="Back to Home"
	icon="mail"
	title="Email Campaigns"
	description="Run high-converting, spam-protected marketing and fleet outreach via tyresoles.in"
>
	{#snippet actions()}
		<div class="flex items-center gap-2">
			<a
				href="/crm-masters?category=media"
				target="_blank"
				class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-border bg-background text-xs font-medium text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors shadow-2xs"
				title="Configure CRM Products, Templates & Media in CRM Masters"
			>
				<Icon name="database" class="w-4 h-4 text-primary" />
				<span>CRM Masters</span>
			</a>
			<Button variant="outline" size="sm" class="gap-1.5 border-emerald-500/40 text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-950/40" onclick={() => goto('/crm-whatsapp-campaigns')}>
				<Icon name="message-circle" class="w-4 h-4 text-emerald-600" />
				<span>WhatsApp Campaigns</span>
			</Button>
			<Button variant="outline" size="sm" onclick={() => goto('/crm-campaigns/templates')}>
				<Icon name="layout-template" class="w-4 h-4 mr-1.5" />
				Templates
			</Button>
			<Button variant="outline" size="sm" onclick={() => goto('/crm-campaigns/suppression')}>
				<Icon name="shield-alert" class="w-4 h-4 mr-1.5" />
				Suppression List
			</Button>
			<Button size="sm" class="bg-primary text-primary-foreground shadow-sm" onclick={() => goto('/crm-campaigns/new')}>
				<Icon name="plus" class="w-4 h-4 mr-1.5" />
				New Campaign
			</Button>
		</div>
	{/snippet}
</PageHeading>

<div class="p-6 max-w-7xl mx-auto space-y-6">
	<!-- Top Metric Summary Cards -->
	<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
		<div class="p-4 bg-card border border-border/60 rounded-2xl shadow-sm space-y-1">
			<div class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
				Total Campaigns
				<Icon name="send" class="w-4 h-4 text-primary" />
			</div>
			<div class="text-2xl font-bold tracking-tight">{stats.totalCampaigns}</div>
			<p class="text-xs text-muted-foreground">Managed in Tyresoles CRM</p>
		</div>

		<div class="p-4 bg-card border border-border/60 rounded-2xl shadow-sm space-y-1">
			<div class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
				Emails Dispatched
				<Icon name="check-circle" class="w-4 h-4 text-emerald-500" />
			</div>
			<div class="text-2xl font-bold tracking-tight">{stats.totalSent.toLocaleString()}</div>
			<p class="text-xs text-muted-foreground">Through tyresoles.in SES</p>
		</div>

		<div class="p-4 bg-card border border-border/60 rounded-2xl shadow-sm space-y-1">
			<div class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
				Average Open Rate
				<Icon name="eye" class="w-4 h-4 text-blue-500" />
			</div>
			<div class="text-2xl font-bold tracking-tight">{stats.avgOpenRate}%</div>
			<p class="text-xs text-muted-foreground">{stats.totalOpens.toLocaleString()} human unique opens</p>
		</div>

		<div class="p-4 bg-card border border-border/60 rounded-2xl shadow-sm space-y-1">
			<div class="text-xs font-semibold text-muted-foreground uppercase tracking-wider flex items-center justify-between">
				Unique Clicks
				<Icon name="mouse-pointer" class="w-4 h-4 text-purple-500" />
			</div>
			<div class="text-2xl font-bold tracking-tight">{stats.totalClicks.toLocaleString()}</div>
			<p class="text-xs text-muted-foreground">Tracked engagement</p>
		</div>
	</div>

	<!-- Filter Tabs -->
	<div class="flex items-center justify-between border-b border-border/60 pb-3">
		<div class="flex items-center gap-2">
			{#each ['ALL', 'InProgress', 'Scheduled', 'Completed', 'Draft', 'Paused'] as status}
				<button
					onclick={() => (filterStatus = status)}
					class="px-3 py-1.5 text-xs font-medium rounded-lg transition-colors {filterStatus === status
						? 'bg-primary text-primary-foreground shadow-sm'
						: 'text-muted-foreground hover:bg-muted hover:text-foreground'}"
				>
					{status === 'ALL' ? 'All Campaigns' : status}
				</button>
			{/each}
		</div>

		<Button variant="ghost" size="sm" onclick={loadCampaigns} class="h-8 gap-1 text-muted-foreground">
			<Icon name="rotate-ccw" class="w-3.5 h-3.5" />
			Refresh
		</Button>
	</div>

	<!-- Campaign List -->
	{#if loading}
		<div class="flex items-center justify-center p-16 text-muted-foreground">
			<Icon name="loader-2" class="w-6 h-6 animate-spin mr-2" />
			Loading campaigns...
		</div>
	{:else if filteredCampaigns.length === 0}
		<div class="flex flex-col items-center justify-center p-16 bg-card border border-dashed border-border rounded-2xl text-center space-y-3">
			<div class="w-12 h-12 rounded-full bg-primary/10 text-primary flex items-center justify-center">
				<Icon name="mail" class="w-6 h-6" />
			</div>
			<div class="text-base font-semibold">No campaigns found</div>
			<p class="text-xs text-muted-foreground max-w-sm">
				Create your first email marketing or fleet outreach campaign to connect with customers.
			</p>
			<Button size="sm" onclick={() => goto('/crm-campaigns/new')} class="mt-2">
				<Icon name="plus" class="w-4 h-4 mr-1.5" />
				Create New Campaign
			</Button>
		</div>
	{:else}
		<div class="grid grid-cols-1 gap-3">
			{#each filteredCampaigns as campaign (campaign.id)}
				<div class="p-5 bg-card border border-border/70 rounded-2xl shadow-sm hover:border-primary/30 transition-all flex flex-col md:flex-row md:items-center justify-between gap-4">
					<div class="space-y-1.5 max-w-xl">
						<div class="flex items-center gap-2.5">
							<h3 class="text-base font-semibold text-foreground tracking-tight hover:text-primary cursor-pointer" onclick={() => goto(`/crm-campaigns/${campaign.id}`)}>
								{campaign.name}
							</h3>
							<span class="px-2.5 py-0.5 text-xs font-semibold rounded-full border {getStatusBadge(campaign.status)}">
								{campaign.status}
							</span>
						</div>
						<p class="text-xs text-muted-foreground line-clamp-1">
							<span class="font-medium text-foreground/80">Subject:</span> {campaign.subject}
						</p>
						<div class="flex items-center gap-4 text-xs text-muted-foreground pt-0.5">
							<span>From: <strong>{campaign.fromEmail}</strong></span>
							<span>Created: {new Date(campaign.createdAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })}</span>
						</div>
					</div>

					<!-- Metrics Pill Group -->
					<div class="flex items-center gap-4 sm:gap-6 bg-muted/40 px-4 py-2.5 rounded-xl border border-border/40">
						<div class="text-center">
							<div class="text-xs text-muted-foreground font-medium">Recipients</div>
							<div class="text-sm font-bold text-foreground">{campaign.totalRecipients}</div>
						</div>
						<div class="text-center">
							<div class="text-xs text-muted-foreground font-medium">Delivered</div>
							<div class="text-sm font-bold text-emerald-600">{campaign.deliveredCount}</div>
						</div>
						<div class="text-center">
							<div class="text-xs text-muted-foreground font-medium">Opens</div>
							<div class="text-sm font-bold text-blue-600">
								{campaign.uniqueOpenedCount}
								<span class="text-[10px] font-normal text-muted-foreground">
									({campaign.deliveredCount > 0 ? ((campaign.uniqueOpenedCount / campaign.deliveredCount) * 100).toFixed(0) : 0}%)
								</span>
							</div>
						</div>
						<div class="text-center">
							<div class="text-xs text-muted-foreground font-medium">Clicks</div>
							<div class="text-sm font-bold text-purple-600">
								{campaign.uniqueClickedCount}
								<span class="text-[10px] font-normal text-muted-foreground">
									({campaign.deliveredCount > 0 ? ((campaign.uniqueClickedCount / campaign.deliveredCount) * 100).toFixed(0) : 0}%)
								</span>
							</div>
						</div>
					</div>

					<!-- Actions -->
					<div class="flex items-center gap-2 self-end md:self-center">
						{#if campaign.status === 'InProgress' || campaign.status === 'Scheduled' || campaign.status === 'Paused'}
							<Button
								variant="outline"
								size="sm"
								class="h-8 text-xs gap-1.5"
								onclick={() => togglePause(campaign)}
							>
								{#if campaign.status === 'Paused'}
									<Icon name="play" class="w-3.5 h-3.5 text-emerald-600" />
									Resume
								{:else}
									<Icon name="pause" class="w-3.5 h-3.5 text-amber-600" />
									Pause
								{/if}
							</Button>
						{/if}

						<Button
							variant="outline"
							size="sm"
							class="h-8 text-xs gap-1.5"
							onclick={() => goto(`/crm-campaigns/${campaign.id}`)}
						>
							<Icon name="bar-chart-2" class="w-3.5 h-3.5" />
							View Analytics
						</Button>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>
