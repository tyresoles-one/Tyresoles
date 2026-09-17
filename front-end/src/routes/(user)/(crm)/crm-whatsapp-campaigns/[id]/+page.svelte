<script lang="ts">
	import { onMount } from 'svelte';
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import {
		GET_WHATSAPP_CAMPAIGN_DETAILS,
		GET_WHATSAPP_CAMPAIGN_RECIPIENTS,
		PAUSE_WHATSAPP_CAMPAIGN,
		RESUME_WHATSAPP_CAMPAIGN,
		CANCEL_WHATSAPP_CAMPAIGN,
		type CrmWhatsappCampaign,
		type CrmWhatsappCampaignRecipient
	} from '../whatsappQueries';

	const campaignId = $derived($page.params.id);

	let campaign = $state<CrmWhatsappCampaign | null>(null);
	let recipients = $state<CrmWhatsappCampaignRecipient[]>([]);
	let loading = $state(true);
	let loadingRecipients = $state(false);
	let activeView = $state<'INBOX' | 'LEDGER'>('INBOX');

	let recipientFilter = $state('ALL');
	let recipientSearch = $state('');
	let skip = $state(0);
	const take = 50;

	async function loadCampaignDetails() {
		if (!campaignId || !isGuid(campaignId)) return;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappCampaignDetails: CrmWhatsappCampaign }>(
				GET_WHATSAPP_CAMPAIGN_DETAILS,
				{
					variables: { id: campaignId }
				}
			);
			if (res.success && res.data?.getCrmWhatsappCampaignDetails) {
				campaign = res.data.getCrmWhatsappCampaignDetails;
			}
		} catch (e: any) {
			toast.error('Failed to load campaign: ' + e.message);
		}
	}

	async function loadRecipients() {
		if (!campaignId || !isGuid(campaignId)) return;
		loadingRecipients = true;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappCampaignRecipients: CrmWhatsappCampaignRecipient[] }>(
				GET_WHATSAPP_CAMPAIGN_RECIPIENTS,
				{
					variables: {
						campaignId: campaignId,
						status: recipientFilter === 'ALL' ? null : recipientFilter,
						skip: skip,
						take: take
					}
				}
			);
			if (res.success && res.data?.getCrmWhatsappCampaignRecipients) {
				recipients = res.data.getCrmWhatsappCampaignRecipients;
			}
		} catch (e: any) {
			console.error('Failed to load recipients', e);
		} finally {
			loadingRecipients = false;
		}
	}

	const isGuid = (val: string) =>
		/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(val);

	onMount(async () => {
		if (campaignId === 'inbox') {
			goto('/crm-whatsapp-campaigns/inbox');
			return;
		}
		if (!campaignId || !isGuid(campaignId)) {
			loading = false;
			return;
		}
		loading = true;
		await Promise.all([loadCampaignDetails(), loadRecipients()]);
		loading = false;
	});

	const filteredRecipients = $derived(
		recipients.filter((r) => {
			if (!recipientSearch.trim()) return true;
			const q = recipientSearch.toLowerCase();
			return (
				r.phoneNumber.includes(q) ||
				r.fullName.toLowerCase().includes(q) ||
				(r.companyName && r.companyName.toLowerCase().includes(q)) ||
				(r.replyMessageText && r.replyMessageText.toLowerCase().includes(q))
			);
		})
	);

	const repliedRecipients = $derived(
		recipients.filter((r) => r.status === 'Replied' || !!r.replyMessageText)
	);

	function exportResponsesCsv() {
		if (repliedRecipients.length === 0) {
			toast.error('No customer responses to export yet.');
			return;
		}

		const headers = ['Phone Number', 'Full Name', 'Company', 'Customer Response', 'Replied At', 'Sent At', 'Delivered At'];
		const rows = repliedRecipients.map((r) => [
			r.phoneNumber,
			`"${(r.fullName || '').replace(/"/g, '""')}"`,
			`"${(r.companyName || '').replace(/"/g, '""')}"`,
			`"${(r.replyMessageText || '').replace(/"/g, '""')}"`,
			r.repliedAt ? new Date(r.repliedAt).toISOString() : '',
			r.sentAt ? new Date(r.sentAt).toISOString() : '',
			r.deliveredAt ? new Date(r.deliveredAt).toISOString() : ''
		]);

		const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n');
		const encodedUri = encodeURI(csvContent);
		const link = document.createElement('a');
		link.setAttribute('href', encodedUri);
		link.setAttribute('download', `whatsapp_campaign_${campaign?.name || 'export'}_inbound_leads.csv`);
		document.body.appendChild(link);
		link.click();
		document.body.removeChild(link);
	}

	function copyToClipboard(text: string, label = 'Copied') {
		if (navigator.clipboard) {
			navigator.clipboard.writeText(text);
			toast.success(`${label} copied to clipboard!`);
		}
	}

	async function handlePause() {
		const res = await graphqlMutation(PAUSE_WHATSAPP_CAMPAIGN, {
			variables: { campaignId }
		});
		if (res.success) {
			toast.success('Campaign paused.');
			loadCampaignDetails();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to pause campaign.';
			toast.error(errMsg);
		}
	}

	async function handleResume() {
		const res = await graphqlMutation(RESUME_WHATSAPP_CAMPAIGN, {
			variables: { campaignId }
		});
		if (res.success) {
			toast.success('Campaign resumed.');
			loadCampaignDetails();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to resume campaign.';
			toast.error(errMsg);
		}
	}

	async function handleCancel() {
		if (!confirm('Are you sure you want to cancel the remaining queued messages?')) return;
		const res = await graphqlMutation(CANCEL_WHATSAPP_CAMPAIGN, {
			variables: { campaignId }
		});
		if (res.success) {
			toast.success('Campaign cancelled.');
			loadCampaignDetails();
			loadRecipients();
		} else {
			const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to cancel campaign.';
			toast.error(errMsg);
		}
	}

	function exportCsv() {
		if (recipients.length === 0) {
			toast.error('No recipients to export.');
			return;
		}

		const headers = ['Phone Number', 'Full Name', 'Company', 'Status', 'Sent At', 'Delivered At', 'Read At', 'Replied At', 'Customer Reply', 'Error Reason', 'Meta Message ID'];
		const rows = recipients.map((r) => [
			r.phoneNumber,
			`"${(r.fullName || '').replace(/"/g, '""')}"`,
			`"${(r.companyName || '').replace(/"/g, '""')}"`,
			r.status,
			r.sentAt ? new Date(r.sentAt).toISOString() : '',
			r.deliveredAt ? new Date(r.deliveredAt).toISOString() : '',
			r.readAt ? new Date(r.readAt).toISOString() : '',
			r.repliedAt ? new Date(r.repliedAt).toISOString() : '',
			`"${(r.replyMessageText || '').replace(/"/g, '""')}"`,
			`"${(r.errorMessage || '').replace(/"/g, '""')}"`,
			r.metaMessageId || ''
		]);

		const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n');
		const encodedUri = encodeURI(csvContent);
		const link = document.createElement('a');
		link.setAttribute('href', encodedUri);
		link.setAttribute('download', `whatsapp_campaign_${campaign?.name || 'export'}_recipients.csv`);
		document.body.appendChild(link);
		link.click();
		document.body.removeChild(link);
	}

	function getRecipientBadge(status: string) {
		switch (status) {
			case 'Sent':
				return 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300';
			case 'Delivered':
				return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300';
			case 'Read':
				return 'bg-indigo-100 text-indigo-700 dark:bg-indigo-950 dark:text-indigo-300 font-semibold';
			case 'Replied':
				return 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 font-bold';
			case 'Failed':
				return 'bg-red-100 text-red-700 dark:bg-red-950 dark:text-red-300';
			case 'Suppressed':
				return 'bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300';
			default:
				return 'bg-muted text-muted-foreground';
		}
	}
</script>

<div class="space-y-6 pb-16">
	<!-- Page Heading -->
	<PageHeading
		backHref="/crm-whatsapp-campaigns"
		backLabel="Campaigns"
		icon="bar-chart-2"
		title={campaign ? campaign.name : 'WhatsApp Campaign Analytics'}
		description="Real-time delivery rates, double-blue-tick read conversions, error diagnostics, and customer replies."
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				{#if campaign?.status === 'InProgress'}
					<Button variant="outline" size="sm" class="gap-1.5 text-amber-600" onclick={handlePause}>
						<Icon name="pause" class="w-4 h-4" />
						<span>Pause</span>
					</Button>
				{:else if campaign?.status === 'Paused'}
					<Button variant="outline" size="sm" class="gap-1.5 text-emerald-600" onclick={handleResume}>
						<Icon name="play" class="w-4 h-4" />
						<span>Resume</span>
					</Button>
				{/if}

				{#if campaign?.status === 'InProgress' || campaign?.status === 'Scheduled'}
					<Button variant="outline" size="sm" class="gap-1.5 text-red-500" onclick={handleCancel}>
						<Icon name="x-circle" class="w-4 h-4" />
						<span>Cancel</span>
					</Button>
				{/if}

				<Button variant="outline" size="sm" class="gap-1.5" onclick={exportCsv}>
					<Icon name="download" class="w-4 h-4" />
					<span>Export CSV</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	{#if loading}
		<div class="p-12 text-center text-muted-foreground flex flex-col items-center gap-2">
			<Icon name="loader-2" class="w-6 h-6 animate-spin text-primary" />
			<span class="text-sm">Loading campaign details...</span>
		</div>
	{:else if campaign}
		<!-- Overview Banner -->
		<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
			<div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-border pb-4">
				<div>
					<div class="flex items-center gap-2.5">
						<h2 class="text-lg font-bold text-foreground">{campaign.name}</h2>
						<span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
							{campaign.status}
						</span>
					</div>
					<div class="text-xs text-muted-foreground mt-1 flex flex-wrap items-center gap-3">
						<span>Template: <strong class="text-foreground">{campaign.template?.name || campaign.templateName}</strong></span>
						<span>•</span>
						<span>Created: <strong>{new Date(campaign.createdAt).toLocaleDateString()}</strong></span>
						{#if campaign.completedAt}
							<span>•</span>
							<span>Completed: <strong>{new Date(campaign.completedAt).toLocaleTimeString()}</strong></span>
						{/if}
					</div>
				</div>

				<div class="text-right">
					<div class="text-xs text-muted-foreground">Actual Campaign Cost</div>
					<div class="text-xl font-bold text-foreground">
						₹{((campaign.actualCost ?? (campaign.deliveredCount * campaign.costPerMessage))).toFixed(2)}
					</div>
					<div class="text-[10px] text-muted-foreground">@ ₹{campaign.costPerMessage.toFixed(2)}/delivered</div>
				</div>
			</div>

			<!-- Visual Funnel Metrics -->
			<!-- Visual Funnel Metrics -->
			<div class="grid grid-cols-2 md:grid-cols-5 gap-3 text-center">
				<button
					type="button"
					onclick={() => {
						activeView = 'LEDGER';
						recipientFilter = 'ALL';
						loadRecipients();
					}}
					class="p-3 rounded-xl border text-center transition-all hover:scale-[1.02] {activeView === 'LEDGER' && recipientFilter === 'ALL' ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'bg-muted/40 border-border'}"
				>
					<div class="text-xs text-muted-foreground">1. Total Audience</div>
					<div class="text-2xl font-bold mt-1 text-foreground">{campaign.totalRecipients.toLocaleString()}</div>
					<div class="text-[10px] text-muted-foreground">Queued</div>
				</button>

				<button
					type="button"
					onclick={() => {
						activeView = 'LEDGER';
						recipientFilter = 'Sent';
						loadRecipients();
					}}
					class="p-3 rounded-xl border text-center transition-all hover:scale-[1.02] {activeView === 'LEDGER' && recipientFilter === 'Sent' ? 'border-blue-500 bg-blue-500/10 ring-1 ring-blue-500' : 'bg-blue-50/50 dark:bg-blue-950/30 border-blue-200 dark:border-blue-900'}"
				>
					<div class="text-xs font-medium text-blue-700 dark:text-blue-300">2. Dispatched</div>
					<div class="text-2xl font-bold text-blue-600 mt-1">{campaign.sentCount.toLocaleString()}</div>
					<div class="text-[10px] text-muted-foreground">
						{campaign.totalRecipients > 0 ? Math.round((campaign.sentCount / campaign.totalRecipients) * 100) : 0}% sent
					</div>
				</button>

				<button
					type="button"
					onclick={() => {
						activeView = 'LEDGER';
						recipientFilter = 'Delivered';
						loadRecipients();
					}}
					class="p-3 rounded-xl border text-center transition-all hover:scale-[1.02] {activeView === 'LEDGER' && recipientFilter === 'Delivered' ? 'border-emerald-500 bg-emerald-500/10 ring-1 ring-emerald-500' : 'bg-emerald-50/50 dark:bg-emerald-950/30 border-emerald-200 dark:border-emerald-900'}"
				>
					<div class="text-xs font-medium text-emerald-700 dark:text-emerald-300">3. Delivered</div>
					<div class="text-2xl font-bold text-emerald-600 mt-1">{campaign.deliveredCount.toLocaleString()}</div>
					<div class="text-[10px] text-muted-foreground">
						{campaign.sentCount > 0 ? Math.round((campaign.deliveredCount / campaign.sentCount) * 100) : 0}% delivered
					</div>
				</button>

				<button
					type="button"
					onclick={() => {
						activeView = 'LEDGER';
						recipientFilter = 'Read';
						loadRecipients();
					}}
					class="p-3 rounded-xl border text-center transition-all hover:scale-[1.02] {activeView === 'LEDGER' && recipientFilter === 'Read' ? 'border-indigo-500 bg-indigo-500/10 ring-1 ring-indigo-500' : 'bg-indigo-50/50 dark:bg-indigo-950/30 border-indigo-200 dark:border-indigo-900'}"
				>
					<div class="text-xs font-medium text-indigo-700 dark:text-indigo-300">4. Read (Blue Ticks)</div>
					<div class="text-2xl font-bold text-indigo-600 mt-1">{campaign.readCount.toLocaleString()}</div>
					<div class="text-[10px] text-muted-foreground">
						{campaign.deliveredCount > 0 ? Math.round((campaign.readCount / campaign.deliveredCount) * 100) : 0}% read rate
					</div>
				</button>

				<button
					type="button"
					onclick={() => {
						activeView = 'INBOX';
					}}
					class="p-3 rounded-xl border text-center transition-all hover:scale-[1.02] {activeView === 'INBOX' ? 'border-amber-500 bg-amber-500/10 ring-1 ring-amber-500' : 'bg-amber-50/50 dark:bg-amber-950/30 border-amber-200 dark:border-amber-900'}"
				>
					<div class="text-xs font-medium text-amber-700 dark:text-amber-300">5. Replies (Leads)</div>
					<div class="text-2xl font-bold text-amber-600 mt-1">{campaign.repliedCount.toLocaleString()}</div>
					<div class="text-[10px] text-muted-foreground">
						{campaign.deliveredCount > 0 ? Math.round((campaign.repliedCount / campaign.deliveredCount) * 100) : 0}% responses
					</div>
				</button>
			</div>

			<!-- Error Alert if Failures Exist -->
			{#if campaign.failedCount > 0}
				<div class="p-3 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-xl text-xs text-red-700 dark:text-red-300 flex items-center justify-between">
					<div class="flex items-center gap-2">
						<Icon name="alert-circle" class="w-4 h-4 shrink-0" />
						<span>{campaign.failedCount} messages failed to deliver (e.g. inactive WhatsApp numbers or user blocking).</span>
					</div>
					<button
						class="font-semibold underline text-red-800 dark:text-red-200 cursor-pointer"
						onclick={() => {
							activeView = 'LEDGER';
							recipientFilter = 'Failed';
							loadRecipients();
						}}
					>
						View Failures
					</button>
				</div>
			{/if}
		</div>

		<!-- Main Workspace: Response Collection Inbox vs Full Activity Ledger -->
		<div class="space-y-4">
			<!-- Tab Selector -->
			<div class="flex items-center gap-2 border-b border-border pb-2">
				<button
					type="button"
					onclick={() => (activeView = 'INBOX')}
					class="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all {activeView === 'INBOX' ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'}"
				>
					<Icon name="message-square" class="w-4 h-4" />
					<span>Inbound Responses & Leads</span>
					{#if repliedRecipients.length > 0}
						<span class="px-1.5 py-0.2 rounded-full text-[10px] font-bold bg-amber-500 text-white">
							{repliedRecipients.length}
						</span>
					{/if}
				</button>

				<button
					type="button"
					onclick={() => (activeView = 'LEDGER')}
					class="flex items-center gap-2 px-4 py-2 rounded-xl text-xs font-semibold transition-all {activeView === 'LEDGER' ? 'bg-primary text-primary-foreground shadow-sm' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'}"
				>
					<Icon name="list" class="w-4 h-4" />
					<span>Complete Delivery Ledger</span>
					<span class="text-[10px] opacity-75">({campaign.totalRecipients})</span>
				</button>
			</div>

			<!-- TAB 1: RESPONSE COLLECTION & INBOUND LEADS INBOX -->
			{#if activeView === 'INBOX'}
				<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
					<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-border pb-4">
						<div>
							<h3 class="text-base font-bold text-foreground flex items-center gap-2">
								<Icon name="inbox" class="w-5 h-5 text-amber-500" />
								Customer Inbound Response Collection Hub
							</h3>
							<p class="text-xs text-muted-foreground mt-0.5">
								Direct replies and interactive button clicks captured live from WhatsApp with 1-click follow-up actions.
							</p>
						</div>

						<div class="flex items-center gap-2">
							<Button variant="outline" size="sm" class="h-8 text-xs gap-1.5" onclick={exportResponsesCsv}>
								<Icon name="download" class="w-3.5 h-3.5" />
								<span>Export Responses CSV</span>
							</Button>
						</div>
					</div>

					<!-- Leads KPI Summary Strip -->
					<div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
						<div class="bg-amber-50/50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-xl p-3.5">
							<div class="text-xs text-amber-800 dark:text-amber-300 font-medium">Inbound Leads Captured</div>
							<div class="text-2xl font-black text-amber-600 dark:text-amber-400 mt-1">
								{repliedRecipients.length}
							</div>
							<div class="text-[10px] text-muted-foreground mt-0.5">Customers who replied to this campaign</div>
						</div>

						<div class="bg-muted/30 border border-border rounded-xl p-3.5">
							<div class="text-xs text-muted-foreground font-medium">Reply Conversion Rate</div>
							<div class="text-2xl font-black text-foreground mt-1">
								{campaign.deliveredCount > 0 ? ((repliedRecipients.length / campaign.deliveredCount) * 100).toFixed(1) : '0.0'}%
							</div>
							<div class="text-[10px] text-muted-foreground mt-0.5">Replies per delivered message</div>
						</div>

						<div class="bg-muted/30 border border-border rounded-xl p-3.5">
							<div class="text-xs text-muted-foreground font-medium">Lead Routing</div>
							<div class="text-sm font-semibold text-emerald-600 mt-2 flex items-center gap-1.5">
								<Icon name="check-circle-2" class="w-4 h-4" />
								<span>Auto-logged via Webhook</span>
							</div>
							<div class="text-[10px] text-muted-foreground mt-0.5">Mapped to Tyresoles CRM Contacts</div>
						</div>
					</div>

					<!-- Inbound Responses List -->
					{#if loadingRecipients}
						<div class="p-12 text-center text-xs text-muted-foreground flex items-center justify-center gap-2">
							<Icon name="loader-2" class="w-5 h-5 animate-spin text-primary" />
							<span>Loading responses...</span>
						</div>
					{:else if repliedRecipients.length === 0}
						<div class="p-12 border-2 border-dashed border-border rounded-2xl text-center space-y-3">
							<div class="w-12 h-12 rounded-full bg-amber-100 dark:bg-amber-950/60 text-amber-600 mx-auto flex items-center justify-center">
								<Icon name="message-square" class="w-6 h-6" />
							</div>
							<h4 class="text-sm font-bold text-foreground">Awaiting Inbound WhatsApp Responses</h4>
							<p class="text-xs text-muted-foreground max-w-md mx-auto">
								When customers text back or click buttons like "Yes I'm Interested" or "Contact Depot", Tyresoles automatically captures their response via WhatsApp Webhooks and lists them here for immediate follow-up.
							</p>
						</div>
					{:else}
						<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
							{#each repliedRecipients as r (r.id)}
								<div class="border border-border rounded-xl p-4 bg-background shadow-sm hover:shadow transition-shadow space-y-3 flex flex-col justify-between">
									<div class="space-y-2">
										<div class="flex items-start justify-between gap-2">
											<div class="flex items-center gap-2.5">
												<div class="w-9 h-9 rounded-full bg-amber-100 dark:bg-amber-950 text-amber-700 dark:text-amber-300 font-bold text-xs flex items-center justify-center">
													{r.fullName ? r.fullName.charAt(0).toUpperCase() : 'C'}
												</div>
												<div>
													<div class="font-bold text-xs text-foreground">{r.fullName}</div>
													{#if r.companyName}
														<div class="text-[11px] text-muted-foreground">{r.companyName}</div>
													{/if}
												</div>
											</div>

											<div class="text-right">
												<span class="text-[10px] text-muted-foreground">
													{r.repliedAt ? new Date(r.repliedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Replied'}
												</span>
											</div>
										</div>

										<!-- WhatsApp Reply Message Bubble -->
										<div class="bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800/60 rounded-2xl rounded-tl-sm p-3 relative space-y-1">
											<div class="text-[10px] font-semibold text-emerald-800 dark:text-emerald-300 flex items-center gap-1">
												<Icon name="message-circle" class="w-3 h-3" />
												<span>Customer Response:</span>
											</div>
											<div class="text-xs font-medium text-emerald-950 dark:text-emerald-100 leading-relaxed whitespace-pre-wrap">
												"{r.replyMessageText}"
											</div>
										</div>
									</div>

									<!-- Action Bar: WhatsApp Chat, Call, Copy -->
									<div class="pt-2 border-t border-border flex items-center justify-between gap-2">
										<div class="font-mono text-xs text-muted-foreground flex items-center gap-1">
											<span>+{r.phoneNumber}</span>
											<button
												type="button"
												class="text-muted-foreground hover:text-foreground p-1"
												title="Copy Phone Number"
												onclick={() => copyToClipboard(r.phoneNumber, 'Phone number')}
											>
												<Icon name="copy" class="w-3 h-3" />
											</button>
										</div>

										<div class="flex items-center gap-1.5">
											<a
												href="tel:+{r.phoneNumber}"
												class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold bg-muted hover:bg-muted/80 text-foreground border border-border transition-colors"
											>
												<Icon name="phone" class="w-3 h-3 text-primary" />
												<span>Call</span>
											</a>

											<a
												href="https://wa.me/{r.phoneNumber}?text={encodeURIComponent(`Hello ${r.fullName}, thank you for responding to our Tyresoles update! How can we assist your fleet today?`)}"
												target="_blank"
												rel="noopener noreferrer"
												class="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm transition-colors"
											>
												<Icon name="message-circle" class="w-3.5 h-3.5" />
												<span>Chat on WhatsApp</span>
											</a>
										</div>
									</div>
								</div>
							{/each}
						</div>
					{/if}
				</div>
			{/if}

			<!-- TAB 2: COMPLETE DELIVERY LEDGER -->
			{#if activeView === 'LEDGER'}
				<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-4">
					<div class="flex flex-col sm:flex-row items-center justify-between gap-3">
						<h3 class="text-sm font-semibold text-foreground flex items-center gap-2">
							<Icon name="list" class="w-4 h-4 text-primary" />
							Recipient Activity & Delivery Audit Ledger
						</h3>

						<div class="flex items-center gap-2 w-full sm:w-auto">
							<div class="relative flex-1 sm:w-60">
								<Icon name="search" class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
								<input
									type="text"
									bind:value={recipientSearch}
									placeholder="Search phone or name..."
									class="w-full pl-8 pr-3 py-1.5 text-xs bg-background border border-input rounded-lg focus:outline-none"
								/>
							</div>

							<select
								bind:value={recipientFilter}
								onchange={() => {
									skip = 0;
									loadRecipients();
								}}
								class="py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
							>
								<option value="ALL">All Recipients</option>
								<option value="Sent">Sent</option>
								<option value="Delivered">Delivered</option>
								<option value="Read">Read (Blue Ticks)</option>
								<option value="Replied">Replied (Responses)</option>
								<option value="Failed">Failed</option>
								<option value="Suppressed">Suppressed</option>
							</select>

							<Button variant="outline" size="sm" class="h-8 text-xs gap-1" onclick={exportCsv}>
								<Icon name="download" class="w-3.5 h-3.5" />
								<span>Export</span>
							</Button>
						</div>
					</div>

					<!-- Recipients Table -->
					{#if loadingRecipients}
						<div class="p-8 text-center text-xs text-muted-foreground flex items-center justify-center gap-2">
							<Icon name="loader-2" class="w-4 h-4 animate-spin text-primary" />
							<span>Loading activity ledger...</span>
						</div>
					{:else if filteredRecipients.length === 0}
						<div class="p-8 text-center text-xs text-muted-foreground">
							No recipients found matching this filter.
						</div>
					{:else}
						<div class="overflow-x-auto">
							<table class="w-full text-xs text-left border-collapse">
								<thead class="bg-muted/50 border-b border-border text-muted-foreground uppercase text-[10px]">
									<tr>
										<th class="py-2.5 px-3 font-semibold">Recipient</th>
										<th class="py-2.5 px-3 font-semibold">Phone Number</th>
										<th class="py-2.5 px-3 font-semibold">Status</th>
										<th class="py-2.5 px-3 font-semibold">Delivered</th>
										<th class="py-2.5 px-3 font-semibold">Read (Blue Ticks)</th>
										<th class="py-2.5 px-3 font-semibold">Customer Response</th>
										<th class="py-2.5 px-3 font-semibold">Diagnostic Details</th>
									</tr>
								</thead>
								<tbody class="divide-y divide-border">
									{#each filteredRecipients as r (r.id)}
										<tr class="hover:bg-muted/30">
											<td class="py-2.5 px-3">
												<div class="font-medium text-foreground">{r.fullName}</div>
												{#if r.companyName}
													<div class="text-[10px] text-muted-foreground">{r.companyName}</div>
												{/if}
											</td>

											<td class="py-2.5 px-3 font-mono text-[11px]">
												+{r.phoneNumber}
											</td>

											<td class="py-2.5 px-3">
												<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] {getRecipientBadge(r.status)}">
													{r.status}
												</span>
											</td>

											<td class="py-2.5 px-3 text-muted-foreground text-[11px]">
												{r.deliveredAt ? new Date(r.deliveredAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-'}
											</td>

											<td class="py-2.5 px-3 font-medium text-indigo-600 dark:text-indigo-400 text-[11px]">
												{r.readAt ? new Date(r.readAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '-'}
											</td>

											<td class="py-2.5 px-3 max-w-xs">
												{#if r.replyMessageText}
													<div class="bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 rounded p-1.5 text-[11px] text-amber-900 dark:text-amber-200 font-medium">
														"{r.replyMessageText}"
													</div>
												{:else}
													<span class="text-muted-foreground text-[10px]">-</span>
												{/if}
											</td>

											<td class="py-2.5 px-3 text-[10px] text-muted-foreground max-w-xs truncate">
												{#if r.errorMessage}
													<span class="text-red-500 font-medium" title={r.errorMessage}>
														{r.errorCode ? `[${r.errorCode}] ` : ''}{r.errorMessage}
													</span>
												{:else if r.metaMessageId}
													<span class="font-mono text-[9px] opacity-70" title={r.metaMessageId}>
														{r.metaMessageId.substring(0, 18)}...
													</span>
												{:else}
													-
												{/if}
											</td>
										</tr>
									{/each}
								</tbody>
							</table>
						</div>
					{/if}
				</div>
			{/if}
		</div>
	{/if}
</div>
