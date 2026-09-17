<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';

	const campaignId = $derived($page.params.id);

	let campaign = $state<any>(null);
	let recipients = $state<any[]>([]);
	let loading = $state(true);
	let pageSkip = $state(0);
	const pageSize = 50;

	const GET_CAMPAIGN_DETAILS = buildQuery`
		query GetCampaignDetails($id: UUID!, $skip: Int!, $take: Int!) {
			getCrmEmailCampaignDetails(id: $id) {
				id
				name
				subject
				previewText
				fromName
				fromEmail
				replyToEmail
				contentType
				bodyHtml
				bodyText
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
				spamComplaintCount
				scheduledAt
				startedAt
				completedAt
				createdAt
			}
			getCrmEmailCampaignRecipients(campaignId: $id, skip: $skip, take: $take) {
				id
				emailAddress
				fullName
				companyName
				status
				openCount
				clickCount
				sentAt
				openedAt
				clickedAt
				bouncedAt
				bounceReason
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

	const CANCEL_CAMPAIGN = buildMutation`
		mutation CancelCampaign($id: UUID!) {
			cancelCrmEmailCampaign(campaignId: $id) {
				id
				status
			}
		}
	`;

	async function loadDetails() {
		loading = true;
		try {
			const res = await graphqlQuery<{
				getCrmEmailCampaignDetails: any;
				getCrmEmailCampaignRecipients: any[];
			}>(GET_CAMPAIGN_DETAILS, {
				variables: {
					id: campaignId,
					skip: pageSkip,
					take: pageSize
				}
			});

			if (res.success && res.data) {
				campaign = res.data.getCrmEmailCampaignDetails;
				recipients = res.data.getCrmEmailCampaignRecipients || [];
			}
		} catch (e: any) {
			toast.error('Failed to load campaign: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadDetails();
	});

	async function handleAction(action: 'pause' | 'resume' | 'cancel') {
		try {
			let res;
			if (action === 'pause') {
				res = await graphqlMutation(PAUSE_CAMPAIGN, { variables: { id: campaignId } });
			} else if (action === 'resume') {
				res = await graphqlMutation(RESUME_CAMPAIGN, { variables: { id: campaignId } });
			} else {
				res = await graphqlMutation(CANCEL_CAMPAIGN, { variables: { id: campaignId } });
			}

			if (res.success) {
				toast.success(`Campaign ${action}d successfully.`);
				await loadDetails();
			}
		} catch (e: any) {
			toast.error(e.message);
		}
	}

	function getRecipientBadge(status: string) {
		switch (status) {
			case 'Clicked':
				return 'bg-purple-500/10 text-purple-600 border-purple-500/20';
			case 'Opened':
				return 'bg-blue-500/10 text-blue-600 border-blue-500/20';
			case 'Delivered':
				return 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20';
			case 'Sent':
				return 'bg-slate-500/10 text-slate-600 border-slate-500/20';
			case 'Bounced':
				return 'bg-rose-500/10 text-rose-600 border-rose-500/20';
			case 'Suppressed':
				return 'bg-amber-500/10 text-amber-600 border-amber-500/20';
			case 'Unsubscribed':
				return 'bg-orange-500/10 text-orange-600 border-orange-500/20';
			default:
				return 'bg-muted text-muted-foreground border-border/40';
		}
	}
</script>

<svelte:head>
	<title>{campaign ? campaign.name : 'Campaign'} | Tyresoles CRM</title>
</svelte:head>

<PageHeading
	backHref="/crm-campaigns"
	backLabel="Back to Campaigns"
	icon="bar-chart-2"
	title={campaign ? campaign.name : 'Campaign Analytics'}
	description="Live deliverability metrics, recipient status, and engagement logs"
>
	{#snippet actions()}
		<div class="flex items-center gap-2">
			{#if campaign}
				{#if campaign.status === 'InProgress' || campaign.status === 'Scheduled'}
					<Button variant="outline" size="sm" class="gap-1.5 text-amber-600" onclick={() => handleAction('pause')}>
						<Icon name="pause" class="w-4 h-4" />
						Pause
					</Button>
				{:else if campaign.status === 'Paused'}
					<Button variant="outline" size="sm" class="gap-1.5 text-emerald-600" onclick={() => handleAction('resume')}>
						<Icon name="play" class="w-4 h-4" />
						Resume
					</Button>
				{/if}

				{#if campaign.status !== 'Completed' && campaign.status !== 'Cancelled'}
					<Button variant="outline" size="sm" class="gap-1.5 text-rose-600" onclick={() => handleAction('cancel')}>
						<Icon name="x-circle" class="w-4 h-4" />
						Cancel
					</Button>
				{/if}
			{/if}

			<Button variant="outline" size="sm" onclick={loadDetails} class="gap-1.5">
				<Icon name="rotate-ccw" class="w-4 h-4" />
				Refresh
			</Button>
		</div>
	{/snippet}
</PageHeading>

<div class="p-6 max-w-7xl mx-auto space-y-6">
	{#if loading && !campaign}
		<div class="flex items-center justify-center p-16 text-muted-foreground">
			<Icon name="loader-2" class="w-6 h-6 animate-spin mr-2" />
			Loading campaign details...
		</div>
	{:else if campaign}
		<!-- Performance Funnel Metrics -->
		<div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Target Audience</div>
				<div class="text-2xl font-bold text-foreground">{campaign.totalRecipients}</div>
			</div>

			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Delivered</div>
				<div class="text-2xl font-bold text-emerald-600">{campaign.deliveredCount}</div>
				<div class="text-[10px] text-muted-foreground">
					{campaign.sentCount > 0 ? ((campaign.deliveredCount / campaign.sentCount) * 100).toFixed(1) : 0}% delivery
				</div>
			</div>

			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Unique Opens</div>
				<div class="text-2xl font-bold text-blue-600">{campaign.uniqueOpenedCount}</div>
				<div class="text-[10px] text-muted-foreground">
					{campaign.deliveredCount > 0 ? ((campaign.uniqueOpenedCount / campaign.deliveredCount) * 100).toFixed(1) : 0}% open rate
				</div>
			</div>

			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Unique Clicks</div>
				<div class="text-2xl font-bold text-purple-600">{campaign.uniqueClickedCount}</div>
				<div class="text-[10px] text-muted-foreground">
					{campaign.deliveredCount > 0 ? ((campaign.uniqueClickedCount / campaign.deliveredCount) * 100).toFixed(1) : 0}% CTR
				</div>
			</div>

			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Hard Bounces</div>
				<div class="text-2xl font-bold text-rose-600">{campaign.bouncedCount}</div>
				<div class="text-[10px] text-muted-foreground">Auto-suppressed</div>
			</div>

			<div class="p-4 bg-card border border-border/70 rounded-2xl text-center space-y-1">
				<div class="text-[11px] font-semibold text-muted-foreground uppercase">Unsubscribes</div>
				<div class="text-2xl font-bold text-amber-600">{campaign.unsubscribedCount}</div>
				<div class="text-[10px] text-muted-foreground">1-Click compliance</div>
			</div>
		</div>

		<!-- Campaign Overview Details -->
		<div class="p-5 bg-card border border-border/70 rounded-2xl space-y-3">
			<div class="flex items-center justify-between border-b border-border/50 pb-3">
				<div class="space-y-0.5">
					<div class="text-xs text-muted-foreground font-semibold">Subject Line</div>
					<div class="text-sm font-bold text-foreground">{campaign.subject}</div>
				</div>
				<span class="px-3 py-1 text-xs font-bold rounded-full border bg-muted">
					Status: {campaign.status}
				</span>
			</div>

			<div class="grid grid-cols-1 sm:grid-cols-3 gap-4 text-xs pt-1">
				<div>
					<span class="text-muted-foreground">From:</span> <strong>{campaign.fromName}</strong> &lt;{campaign.fromEmail}&gt;
				</div>
				<div>
					<span class="text-muted-foreground">Format:</span> <strong>{campaign.contentType}</strong>
				</div>
				<div>
					<span class="text-muted-foreground">Started:</span> {campaign.startedAt ? new Date(campaign.startedAt).toLocaleString('en-IN') : 'Pending'}
				</div>
			</div>
		</div>

		<!-- Recipient List Table -->
		<div class="p-6 bg-card border border-border/70 rounded-2xl space-y-4">
			<div class="flex items-center justify-between">
				<div>
					<h3 class="text-sm font-bold text-foreground">Recipient Delivery Logs</h3>
					<p class="text-xs text-muted-foreground">Per-contact audit trail with open and click timestamps.</p>
				</div>
				<div class="text-xs text-muted-foreground">
					Showing {recipients.length} recipients
				</div>
			</div>

			<div class="overflow-x-auto border border-border/50 rounded-xl">
				<table class="w-full text-xs text-left">
					<thead class="bg-muted/50 text-muted-foreground border-b border-border/50 font-semibold">
						<tr>
							<th class="p-3">Contact</th>
							<th class="p-3">Company</th>
							<th class="p-3">Status</th>
							<th class="p-3">Opens</th>
							<th class="p-3">Clicks</th>
							<th class="p-3">Sent At</th>
							<th class="p-3">Last Opened</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border/40">
						{#if recipients.length === 0}
							<tr>
								<td colspan="7" class="p-6 text-center text-muted-foreground">
									No recipient records found.
								</td>
							</tr>
						{:else}
							{#each recipients as r (r.id)}
								<tr class="hover:bg-muted/20 transition-colors">
									<td class="p-3 font-medium text-foreground">
										<div>{r.fullName}</div>
										<div class="text-[11px] text-muted-foreground font-mono">{r.emailAddress}</div>
									</td>
									<td class="p-3 text-muted-foreground">{r.companyName || '-'}</td>
									<td class="p-3">
										<span class="px-2 py-0.5 text-[11px] font-semibold rounded-full border {getRecipientBadge(r.status)}">
											{r.status}
										</span>
									</td>
									<td class="p-3 font-semibold">{r.openCount}</td>
									<td class="p-3 font-semibold">{r.clickCount}</td>
									<td class="p-3 text-muted-foreground">
										{r.sentAt ? new Date(r.sentAt).toLocaleTimeString('en-IN') : '-'}
									</td>
									<td class="p-3 text-muted-foreground">
										{r.openedAt ? new Date(r.openedAt).toLocaleTimeString('en-IN') : '-'}
									</td>
								</tr>
							{/each}
						{/if}
					</tbody>
				</table>
			</div>
		</div>
	{/if}
</div>
