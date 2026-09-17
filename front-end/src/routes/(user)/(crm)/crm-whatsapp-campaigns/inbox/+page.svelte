<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import {
		GET_WHATSAPP_INBOUND_MESSAGES,
		UPDATE_WHATSAPP_INBOUND_FOLLOWUP_STATUS,
		GET_WHATSAPP_CAMPAIGNS,
		GET_CRM_WHATSAPP_WEBHOOK_LOGS,
		SIMULATE_WHATSAPP_WEBHOOK,
		type CrmWhatsappInboundMessageDto,
		type CrmWhatsappInboundMessagesResult,
		type CrmWhatsappCampaign,
		type CrmWhatsappWebhookLogDto,
		type CrmWhatsappWebhookLogsResult,
		type SimulateWhatsappWebhookResult
	} from '../whatsappQueries';

	let messages = $state<CrmWhatsappInboundMessageDto[]>([]);
	let totalCount = $state(0);
	let pendingCount = $state(0);
	let contactedCount = $state(0);
	let convertedCount = $state(0);
	let loading = $state(true);

	let searchQuery = $state('');
	let statusFilter = $state('ALL');
	let campaignFilter = $state<string | null>(null);
	let campaigns = $state<Array<{ id: string; name: string }>>([]);

	let skip = $state(0);
	const take = 25;

	let updatingId = $state<string | null>(null);

	async function loadInboundMessages() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappInboundMessages: CrmWhatsappInboundMessagesResult }>(
				GET_WHATSAPP_INBOUND_MESSAGES,
				{
					variables: {
						campaignId: campaignFilter || null,
						followupStatus: statusFilter === 'ALL' ? null : statusFilter,
						search: searchQuery.trim() || null,
						skip: skip,
						take: take
					}
				}
			);

			if (res.success && res.data?.getCrmWhatsappInboundMessages) {
				const data = res.data.getCrmWhatsappInboundMessages;
				messages = data.items;
				totalCount = data.totalCount;
				pendingCount = data.pendingCount;
				contactedCount = data.contactedCount;
				convertedCount = data.convertedCount;
			}
		} catch (e: any) {
			toast.error('Failed to load inbound messages: ' + e.message);
		} finally {
			loading = false;
		}
	}

	async function loadCampaignsList() {
		try {
			const res = await graphqlQuery<{ getCrmWhatsappCampaigns: CrmWhatsappCampaign[] }>(GET_WHATSAPP_CAMPAIGNS, {
				variables: { skip: 0, take: 100 }
			});
			if (res.success && res.data?.getCrmWhatsappCampaigns) {
				campaigns = res.data.getCrmWhatsappCampaigns.map((c) => ({ id: c.id, name: c.name }));
			}
		} catch (e) {
			console.error('Failed to load campaigns for filter', e);
		}
	}

	let searchDebounce: any = null;
	function handleSearchInput() {
		clearTimeout(searchDebounce);
		searchDebounce = setTimeout(() => {
			skip = 0;
			loadInboundMessages();
		}, 350);
	}

	function handleFilterChanged() {
		skip = 0;
		loadInboundMessages();
	}

	async function updateFollowupStatus(msgId: string, newStatus: string) {
		updatingId = msgId;
		try {
			const res = await graphqlMutation<{ updateWhatsappInboundFollowupStatus: boolean }>(
				UPDATE_WHATSAPP_INBOUND_FOLLOWUP_STATUS,
				{
					variables: {
						id: msgId,
						status: newStatus
					}
				}
			);

			if (res.success && res.data?.updateWhatsappInboundFollowupStatus) {
				toast.success(`Follow-up status marked as '${newStatus}'`);
				// Update locally
				const target = messages.find((m) => m.id === msgId);
				if (target) {
					target.followupStatus = newStatus;
				}
				loadInboundMessages();
			} else {
				toast.error('Failed to update status.');
			}
		} catch (e: any) {
			toast.error('Error updating status: ' + e.message);
		} finally {
			updatingId = null;
		}
	}

	function copyToClipboard(text: string, label = 'Phone number') {
		if (navigator.clipboard) {
			navigator.clipboard.writeText(text);
			toast.success(`${label} copied to clipboard!`);
		}
	}

	// Webhook Diagnostics & Simulation State
	let showWebhookModal = $state(false);
	let showJsonModal = $state(false);
	let selectedRawJson = $state('');

	let webhookLogs = $state<CrmWhatsappWebhookLogDto[]>([]);
	let loadingLogs = $state(false);
	let logsTotal = $state(0);
	let logsProcessed = $state(0);
	let logsSimulated = $state(0);
	let logsErrors = $state(0);
	let logFilterType = $state('ALL');
	let logFilterStatus = $state('ALL');

	// Simulator State
	let simEventType = $state('messages');
	let simPhone = $state('919880334191');
	let simCustomerName = $state('Ramesh Logistics');
	let simMessageText = $state('Hello, please send tyre retread price list.');
	let simButtonPayload = $state('BTN_INTERESTED');
	let simCampaignId = $state<string | null>(null);
	let simulating = $state(false);

	let webhookHealthStatus = $state<{ active: boolean; message: string; checkedAt: string } | null>(null);
	let checkingHealth = $state(false);

	async function loadWebhookLogs() {
		loadingLogs = true;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappWebhookLogs: CrmWhatsappWebhookLogsResult }>(
				GET_CRM_WHATSAPP_WEBHOOK_LOGS,
				{
					variables: {
						eventType: logFilterType === 'ALL' ? null : logFilterType,
						processingStatus: logFilterStatus === 'ALL' ? null : logFilterStatus,
						skip: 0,
						take: 50
					}
				}
			);
			if (res.success && res.data?.getCrmWhatsappWebhookLogs) {
				const data = res.data.getCrmWhatsappWebhookLogs;
				webhookLogs = data.items;
				logsTotal = data.totalCount;
				logsProcessed = data.processedCount;
				logsSimulated = data.simulatedCount;
				logsErrors = data.errorCount;
			}
		} catch (e: any) {
			toast.error('Failed to load webhook logs: ' + e.message);
		} finally {
			loadingLogs = false;
		}
	}

	async function checkWebhookHealth() {
		checkingHealth = true;
		try {
			const endpoint = typeof window !== 'undefined' ? `${window.location.origin}/api/campaigns/webhooks/whatsapp` : 'https://app.tyresoles.in/api/campaigns/webhooks/whatsapp';
			const resp = await fetch(endpoint, { method: 'GET' });
			if (resp.ok) {
				const json = await resp.json();
				webhookHealthStatus = {
					active: json.status === 'active',
					message: json.message || 'Webhook is active and listening',
					checkedAt: new Date().toLocaleTimeString()
				};
				toast.success('Webhook endpoint is ACTIVE and healthy!');
			} else {
				toast.error(`Webhook endpoint returned HTTP ${resp.status}`);
			}
		} catch (e: any) {
			toast.error('Health check failed: ' + e.message);
		} finally {
			checkingHealth = false;
		}
	}

	async function runSimulation() {
		simulating = true;
		try {
			const res = await graphqlMutation<{ simulateWhatsappWebhook: SimulateWhatsappWebhookResult }>(
				SIMULATE_WHATSAPP_WEBHOOK,
				{
					variables: {
						input: {
							eventType: simEventType,
							fromPhoneNumber: simPhone.trim() || '919880334191',
							customerName: simCustomerName.trim() || 'Simulated Customer',
							messageText: simMessageText.trim() || 'Hi from simulation test',
							buttonPayload: simEventType === 'button' ? simButtonPayload : null,
							campaignId: simCampaignId || null
						}
					}
				}
			);

			if (res.success && res.data?.simulateWhatsappWebhook?.success) {
				toast.success(res.data.simulateWhatsappWebhook.message || 'Webhook event simulated successfully!');
				await loadWebhookLogs();
				await loadInboundMessages();
			} else {
				toast.error(res.errors?.[0]?.message || 'Simulation failed.');
			}
		} catch (e: any) {
			toast.error('Simulation error: ' + e.message);
		} finally {
			simulating = false;
		}
	}

	function openPayloadModal(payload: string) {
		try {
			selectedRawJson = JSON.stringify(JSON.parse(payload), null, 2);
		} catch {
			selectedRawJson = payload;
		}
		showJsonModal = true;
	}

	function copyJsonToClipboard() {
		navigator.clipboard.writeText(selectedRawJson);
		toast.success('Payload copied to clipboard!');
	}

	function exportCsv() {
		if (messages.length === 0) {
			toast.error('No inbound messages to export.');
			return;
		}

		const headers = [
			'Received At',
			'Phone Number',
			'WhatsApp Profile',
			'CRM Contact Name',
			'Company',
			'City',
			'Message Type',
			'Customer Message',
			'Button Payload',
			'Followup Status',
			'Campaign'
		];

		const rows = messages.map((m) => [
			new Date(m.receivedAt).toISOString(),
			m.fromPhoneNumber,
			`"${(m.profileName || '').replace(/"/g, '""')}"`,
			`"${(m.contactFullName || '').replace(/"/g, '""')}"`,
			`"${(m.companyName || '').replace(/"/g, '""')}"`,
			`"${(m.city || '').replace(/"/g, '""')}"`,
			m.messageType,
			`"${(m.messageBody || '').replace(/"/g, '""')}"`,
			`"${(m.buttonPayload || '').replace(/"/g, '""')}"`,
			m.followupStatus,
			`"${(m.campaignName || 'Direct Message').replace(/"/g, '""')}"`
		]);

		const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n');
		const encodedUri = encodeURI(csvContent);
		const link = document.createElement('a');
		link.setAttribute('href', encodedUri);
		link.setAttribute('download', `whatsapp_inbound_leads_${new Date().toISOString().slice(0, 10)}.csv`);
		document.body.appendChild(link);
		link.click();
		document.body.removeChild(link);
	}

	function getStatusBadge(status: string) {
		switch (status) {
			case 'Pending':
				return 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300 font-semibold border-amber-300';
			case 'Contacted':
				return 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300 font-semibold border-blue-300';
			case 'Converted':
				return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-bold border-emerald-300';
			case 'Dismissed':
				return 'bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300 border-slate-300';
			default:
				return 'bg-muted text-muted-foreground border-border';
		}
	}

	onMount(() => {
		loadInboundMessages();
		loadCampaignsList();
	});
</script>

<div class="space-y-6 pb-16">
	<!-- Page Heading -->
	<PageHeading
		backHref="/crm-whatsapp-campaigns"
		backLabel="Campaigns"
		icon="inbox"
		title="WhatsApp Inbound Messages & Leads Inbox"
		description="Unified live inbox for all customer replies, button responses, and incoming WhatsApp inquiries across all campaigns."
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<Button
					variant="outline"
					size="sm"
					class="gap-1.5 border-blue-200 dark:border-blue-900 bg-blue-50/50 dark:bg-blue-950/20 text-blue-700 dark:text-blue-300 hover:bg-blue-100 dark:hover:bg-blue-900/40"
					onclick={() => {
						showWebhookModal = true;
						loadWebhookLogs();
					}}
				>
					<span class="relative flex h-2 w-2">
						<span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
						<span class="relative inline-flex rounded-full h-2 w-2 bg-emerald-500"></span>
					</span>
					<Icon name="radio" class="w-3.5 h-3.5 text-blue-600" />
					<span>Webhook Live Monitor & Tests</span>
				</Button>

				<Button variant="outline" size="sm" class="gap-1.5" onclick={loadInboundMessages} disabled={loading}>
					<Icon name="refresh-cw" class="w-3.5 h-3.5 {loading ? 'animate-spin' : ''}" />
					<span>Refresh</span>
				</Button>

				<Button variant="outline" size="sm" class="gap-1.5" onclick={exportCsv}>
					<Icon name="download" class="w-3.5 h-3.5" />
					<span>Export CSV</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- KPI Metric Cards -->
	<div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
		<button
			type="button"
			onclick={() => {
				statusFilter = 'ALL';
				handleFilterChanged();
			}}
			class="bg-card border rounded-2xl p-4 shadow-sm text-left transition-all hover:border-primary cursor-pointer {statusFilter === 'ALL' ? 'border-primary ring-1 ring-primary' : 'border-border'}"
		>
			<div class="text-xs text-muted-foreground font-medium">Total Messages</div>
			<div class="text-2xl font-black text-foreground mt-1">{totalCount.toLocaleString()}</div>
			<div class="text-[10px] text-muted-foreground mt-0.5">All customer inbound replies</div>
		</button>

		<button
			type="button"
			onclick={() => {
				statusFilter = 'Pending';
				handleFilterChanged();
			}}
			class="bg-card border rounded-2xl p-4 shadow-sm text-left transition-all hover:border-amber-500 cursor-pointer {statusFilter === 'Pending' ? 'border-amber-500 ring-1 ring-amber-500 bg-amber-50/10' : 'border-border'}"
		>
			<div class="text-xs text-amber-700 dark:text-amber-400 font-medium flex items-center gap-1.5">
				<span class="w-2 h-2 rounded-full bg-amber-500"></span>
				<span>Pending Follow-up</span>
			</div>
			<div class="text-2xl font-black text-amber-600 dark:text-amber-400 mt-1">{pendingCount.toLocaleString()}</div>
			<div class="text-[10px] text-muted-foreground mt-0.5">Awaiting sales team call</div>
		</button>

		<button
			type="button"
			onclick={() => {
				statusFilter = 'Contacted';
				handleFilterChanged();
			}}
			class="bg-card border rounded-2xl p-4 shadow-sm text-left transition-all hover:border-blue-500 cursor-pointer {statusFilter === 'Contacted' ? 'border-blue-500 ring-1 ring-blue-500 bg-blue-50/10' : 'border-border'}"
		>
			<div class="text-xs text-blue-700 dark:text-blue-400 font-medium flex items-center gap-1.5">
				<span class="w-2 h-2 rounded-full bg-blue-500"></span>
				<span>Contacted</span>
			</div>
			<div class="text-2xl font-black text-blue-600 dark:text-blue-400 mt-1">{contactedCount.toLocaleString()}</div>
			<div class="text-[10px] text-muted-foreground mt-0.5">In communication</div>
		</button>

		<button
			type="button"
			onclick={() => {
				statusFilter = 'Converted';
				handleFilterChanged();
			}}
			class="bg-card border rounded-2xl p-4 shadow-sm text-left transition-all hover:border-emerald-500 cursor-pointer {statusFilter === 'Converted' ? 'border-emerald-500 ring-1 ring-emerald-500 bg-emerald-50/10' : 'border-border'}"
		>
			<div class="text-xs text-emerald-700 dark:text-emerald-400 font-medium flex items-center gap-1.5">
				<span class="w-2 h-2 rounded-full bg-emerald-500"></span>
				<span>Converted Deals</span>
			</div>
			<div class="text-2xl font-black text-emerald-600 dark:text-emerald-400 mt-1">{convertedCount.toLocaleString()}</div>
			<div class="text-[10px] text-muted-foreground mt-0.5">Inspection / Order booked</div>
		</button>
	</div>

	<!-- Filter & Search Controls Bar -->
	<div class="bg-card border border-border rounded-2xl p-4 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-3">
		<div class="flex flex-wrap items-center gap-2">
			<!-- Follow-up Status Tabs -->
			<div class="flex items-center bg-muted/60 p-1 rounded-xl border border-border">
				{#each ['ALL', 'Pending', 'Contacted', 'Converted', 'Dismissed'] as st}
					<button
						type="button"
						onclick={() => {
							statusFilter = st;
							handleFilterChanged();
						}}
						class="px-3 py-1 rounded-lg text-xs font-medium transition-all {statusFilter === st ? 'bg-background text-foreground shadow-xs font-semibold' : 'text-muted-foreground hover:text-foreground'}"
					>
						{st === 'ALL' ? 'All' : st}
					</button>
				{/each}
			</div>

			<!-- Campaign Filter Dropdown -->
			{#if campaigns.length > 0}
				<select
					bind:value={campaignFilter}
					onchange={handleFilterChanged}
					class="py-1.5 px-3 text-xs bg-background border border-input rounded-xl focus:outline-none focus:ring-1 focus:ring-primary"
				>
					<option value={null}>All Campaigns & Direct Inbound</option>
					{#each campaigns as camp}
						<option value={camp.id}>{camp.name}</option>
					{/each}
				</select>
			{/if}
		</div>

		<!-- Live Search -->
		<div class="relative w-full md:w-72">
			<Icon name="search" class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
			<input
				type="text"
				bind:value={searchQuery}
				oninput={handleSearchInput}
				placeholder="Search phone, name, or message..."
				class="w-full pl-8 pr-3 py-1.5 text-xs bg-background border border-input rounded-xl focus:outline-none focus:ring-1 focus:ring-primary"
			/>
		</div>
	</div>

	<!-- Inbound Messages Feed -->
	{#if loading}
		<div class="p-16 text-center text-xs text-muted-foreground flex flex-col items-center justify-center gap-2">
			<Icon name="loader-2" class="w-6 h-6 animate-spin text-primary" />
			<span>Loading WhatsApp inbound messages...</span>
		</div>
	{:else if messages.length === 0}
		<div class="border-2 border-dashed border-border rounded-2xl p-16 text-center space-y-3 bg-card">
			<div class="w-14 h-14 rounded-full bg-emerald-100 dark:bg-emerald-950/50 text-emerald-600 mx-auto flex items-center justify-center">
				<Icon name="inbox" class="w-7 h-7" />
			</div>
			<h4 class="text-base font-bold text-foreground">No Inbound Messages Found</h4>
			<p class="text-xs text-muted-foreground max-w-md mx-auto">
				When customers text back or click interactive buttons on your WhatsApp campaigns, their messages will automatically stream into this inbox in real-time.
			</p>
			{#if statusFilter !== 'ALL' || searchQuery}
				<Button
					variant="outline"
					size="sm"
					class="mt-2"
					onclick={() => {
						statusFilter = 'ALL';
						searchQuery = '';
						campaignFilter = null;
						handleFilterChanged();
					}}
				>
					Reset Filters
				</Button>
			{/if}
		</div>
	{:else}
		<div class="space-y-4">
			{#each messages as m (m.id)}
				<div class="bg-card border border-border rounded-2xl p-5 shadow-xs hover:shadow-sm transition-all space-y-4">
					<!-- Top Row: Sender Info & Campaign Tag -->
					<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-border pb-3">
						<div class="flex items-center gap-3">
							<div class="w-10 h-10 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-700 dark:text-emerald-300 font-bold text-sm flex items-center justify-center border border-emerald-200 dark:border-emerald-800">
								{m.contactFullName ? m.contactFullName.charAt(0).toUpperCase() : m.profileName ? m.profileName.charAt(0).toUpperCase() : 'W'}
							</div>

							<div>
								<div class="flex items-center gap-2">
									<span class="font-bold text-sm text-foreground">
										{m.contactFullName || m.profileName || 'WhatsApp Customer'}
									</span>
									{#if m.profileName && m.contactFullName && m.profileName !== m.contactFullName}
										<span class="text-[10px] text-muted-foreground font-normal">
											(Profile: {m.profileName})
										</span>
									{/if}
									{#if m.contactId}
										<span class="px-1.5 py-0.2 rounded text-[9px] bg-primary/10 text-primary font-semibold">
											CRM Contact Matched
										</span>
									{/if}
								</div>

								<div class="text-xs text-muted-foreground flex flex-wrap items-center gap-2 mt-0.5">
									{#if m.companyName}
										<span class="font-medium text-foreground">{m.companyName}</span>
										<span>•</span>
									{/if}
									{#if m.city}
										<span>{[m.city, m.state].filter(Boolean).join(', ')}</span>
										<span>•</span>
									{/if}
									<span class="font-mono text-[11px] text-emerald-600 font-semibold">+{m.fromPhoneNumber}</span>
								</div>
							</div>
						</div>

						<div class="flex items-center gap-2.5 sm:self-start">
							{#if m.campaignName}
								<a
									href="/crm-whatsapp-campaigns/{m.campaignId}"
									class="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-blue-50 dark:bg-blue-950/60 text-blue-700 dark:text-blue-300 border border-blue-200 dark:border-blue-800 hover:underline"
								>
									<Icon name="tag" class="w-3 h-3" />
									<span>{m.campaignName}</span>
								</a>
							{:else}
								<span class="px-2.5 py-0.5 rounded-full text-[10px] font-semibold bg-muted text-muted-foreground">
									Direct WhatsApp Inquiry
								</span>
							{/if}

							<span class="text-[11px] text-muted-foreground">
								{new Date(m.receivedAt).toLocaleDateString()} {new Date(m.receivedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
							</span>
						</div>
					</div>

					<!-- Middle Row: Customer WhatsApp Message Bubble -->
					<div class="flex items-start gap-2.5">
						<div class="flex-1 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800/60 rounded-2xl rounded-tl-sm p-3.5 shadow-2xs space-y-1.5">
							<div class="flex items-center justify-between text-[11px]">
								<span class="font-semibold text-emerald-800 dark:text-emerald-300 flex items-center gap-1">
									<Icon name="message-circle" class="w-3.5 h-3.5" />
									<span>Inbound WhatsApp Message ({m.messageType}):</span>
								</span>
								{#if m.buttonPayload}
									<span class="px-2 py-0.5 rounded text-[10px] bg-emerald-200/60 dark:bg-emerald-900/60 text-emerald-900 dark:text-emerald-200 font-mono">
										Button: {m.buttonPayload}
									</span>
								{/if}
							</div>

							<div class="text-xs font-medium text-emerald-950 dark:text-emerald-50 leading-relaxed whitespace-pre-wrap">
								"{m.messageBody || m.buttonPayload || '[No text content]'}"
							</div>
						</div>
					</div>

					<!-- Bottom Row: Status Management & Action Buttons -->
					<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-2">
						<div class="flex items-center gap-2 text-xs">
							<span class="text-muted-foreground font-medium">Follow-up:</span>
							<select
								value={m.followupStatus}
								onchange={(e) => updateFollowupStatus(m.id, (e.target as HTMLSelectElement).value)}
								disabled={updatingId === m.id}
								class="py-1 px-2.5 text-xs rounded-lg border font-medium cursor-pointer focus:outline-none {getStatusBadge(m.followupStatus)}"
							>
								<option value="Pending">Pending</option>
								<option value="Contacted">Contacted</option>
								<option value="Converted">Converted</option>
								<option value="Dismissed">Dismissed</option>
							</select>
							{#if updatingId === m.id}
								<Icon name="loader-2" class="w-3.5 h-3.5 animate-spin text-primary" />
							{/if}
						</div>

						<div class="flex items-center gap-2">
							<button
								type="button"
								onclick={() => copyToClipboard(m.fromPhoneNumber, 'Phone number')}
								class="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-xl text-xs font-medium bg-muted hover:bg-muted/80 text-foreground border border-border transition-colors cursor-pointer"
								title="Copy Phone"
							>
								<Icon name="copy" class="w-3 h-3 text-muted-foreground" />
								<span>Copy Phone</span>
							</button>

							<a
								href="tel:+{m.fromPhoneNumber}"
								class="inline-flex items-center gap-1 px-3 py-1.5 rounded-xl text-xs font-semibold bg-muted hover:bg-muted/80 text-foreground border border-border transition-colors cursor-pointer"
							>
								<Icon name="phone" class="w-3 h-3 text-primary" />
								<span>Call</span>
							</a>

							<a
								href="https://wa.me/{m.fromPhoneNumber}?text={encodeURIComponent(`Hello ${m.contactFullName || m.profileName || ''}, thank you for contacting Tyresoles! How can we assist with your fleet tyres today?`)}"
								target="_blank"
								rel="noopener noreferrer"
								class="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-xl text-xs font-semibold bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs transition-colors cursor-pointer"
							>
								<Icon name="message-circle" class="w-3.5 h-3.5" />
								<span>Chat on WhatsApp</span>
							</a>
						</div>
					</div>
				</div>
			{/each}
		</div>

		<!-- Pagination Footer -->
		<div class="flex items-center justify-between pt-4 border-t border-border text-xs text-muted-foreground">
			<div>
				Showing {skip + 1} - {Math.min(skip + take, totalCount)} of {totalCount.toLocaleString()} inbound messages
			</div>

			<div class="flex items-center gap-2">
				<Button
					variant="outline"
					size="sm"
					class="h-8 text-xs"
					disabled={skip === 0 || loading}
					onclick={() => {
						skip = Math.max(0, skip - take);
						loadInboundMessages();
					}}
				>
					<Icon name="chevron-left" class="w-3.5 h-3.5 mr-1" />
					Previous
				</Button>

				<Button
					variant="outline"
					size="sm"
					class="h-8 text-xs"
					disabled={skip + take >= totalCount || loading}
					onclick={() => {
						skip += take;
						loadInboundMessages();
					}}
				>
					Next
					<Icon name="chevron-right" class="w-3.5 h-3.5 ml-1" />
				</Button>
			</div>
		</div>
	{/if}

	<!-- Webhook Live Monitor & Diagnostic Simulator Modal -->
	{#if showWebhookModal}
		<div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-background/80 backdrop-blur-sm">
			<div class="bg-card border border-border rounded-3xl shadow-2xl w-full max-w-5xl max-h-[90vh] flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-200">
				<!-- Header -->
				<div class="p-5 border-b border-border flex items-center justify-between bg-muted/30 shrink-0">
					<div class="flex items-center gap-3">
						<div class="w-10 h-10 rounded-2xl bg-blue-100 dark:bg-blue-950/60 border border-blue-200 dark:border-blue-800 flex items-center justify-center text-blue-600">
							<Icon name="radio" class="w-5 h-5" />
						</div>
						<div>
							<div class="flex items-center gap-2">
								<h2 class="text-base font-bold text-foreground">WhatsApp Webhook Live Monitor & Test Station</h2>
								<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 border border-emerald-300">
									<span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
									Active & Storing All Events
								</span>
							</div>
							<p class="text-xs text-muted-foreground mt-0.5">
								Every webhook hit from Meta is stored with its raw payload, signature check status, and delivery status audit trail.
							</p>
						</div>
					</div>

					<div class="flex items-center gap-2">
						<Button
							variant="outline"
							size="sm"
							class="text-xs gap-1.5"
							onclick={checkWebhookHealth}
							disabled={checkingHealth}
						>
							<Icon name="activity" class="w-3.5 h-3.5 {checkingHealth ? 'animate-spin' : ''}" />
							<span>{checkingHealth ? 'Checking...' : 'Check Live Health'}</span>
						</Button>

						<Button
							variant="ghost"
							size="sm"
							class="h-8 w-8 p-0 rounded-full"
							onclick={() => (showWebhookModal = false)}
						>
							<Icon name="x" class="w-4 h-4" />
						</Button>
					</div>
				</div>

				<div class="flex-1 overflow-y-auto p-6 space-y-6">
					<!-- Webhook Connection Details Banner -->
					<div class="bg-muted/40 border border-border rounded-2xl p-4 text-xs space-y-2">
						<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
							<div class="space-y-0.5">
								<span class="font-semibold text-foreground">Production Webhook Endpoint:</span>
								<div class="font-mono text-[11px] text-blue-600 dark:text-blue-400 select-all">
									https://app.tyresoles.in/api/campaigns/webhooks/whatsapp
								</div>
							</div>
							<div class="flex items-center gap-2">
								<Button
									variant="outline"
									size="sm"
									class="h-7 text-[11px] gap-1"
									onclick={() => copyToClipboard('https://app.tyresoles.in/api/campaigns/webhooks/whatsapp', 'Webhook URL')}
								>
									<Icon name="copy" class="w-3 h-3" />
									Copy URL
								</Button>
								<Button
									variant="outline"
									size="sm"
									class="h-7 text-[11px] gap-1"
									onclick={() => copyToClipboard('tyresoles_crm_wa_webhook_verify_2026', 'Verify Token')}
								>
									<Icon name="key" class="w-3 h-3" />
									Copy Token
								</Button>
							</div>
						</div>
						{#if webhookHealthStatus}
							<div class="pt-2 border-t border-border/60 flex items-center gap-2 text-emerald-600 dark:text-emerald-400 font-medium">
								<Icon name="check-circle" class="w-3.5 h-3.5 shrink-0" />
								<span>{webhookHealthStatus.message} (Probed at {webhookHealthStatus.checkedAt})</span>
							</div>
						{/if}
					</div>

					<!-- Interactive Event Simulator Box -->
					<div class="bg-card border-2 border-primary/20 rounded-2xl p-5 shadow-xs space-y-4">
						<div class="flex items-center justify-between">
							<div class="flex items-center gap-2">
								<div class="w-7 h-7 rounded-xl bg-primary/10 text-primary flex items-center justify-center">
									<Icon name="zap" class="w-4 h-4" />
								</div>
								<div>
									<h3 class="text-sm font-bold text-foreground">Interactive Webhook Event Simulator</h3>
									<p class="text-[11px] text-muted-foreground">
										Instantly simulate incoming WhatsApp payloads to test end-to-end receipt, inbox parsing, and database storage without calling Meta.
									</p>
								</div>
							</div>
						</div>

						<!-- Event Type Selector -->
						<div class="space-y-1.5">
							<label class="text-xs font-semibold text-foreground">Select Event to Simulate</label>
							<div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-2">
								{#each [
									{ id: 'messages', label: '💬 Text Reply', desc: 'Customer reply' },
									{ id: 'button', label: '🔘 Quick Reply', desc: 'Button click' },
									{ id: 'delivered', label: '✅ Delivered', desc: 'Double tick' },
									{ id: 'read', label: '👀 Read Status', desc: 'Blue tick' },
									{ id: 'failed', label: '❌ Failed', desc: 'Delivery error' },
									{ id: 'stop', label: '🛑 STOP Opt-out', desc: 'Suppression' }
								] as ev}
									<button
										type="button"
										onclick={() => {
											simEventType = ev.id;
											if (ev.id === 'stop') simMessageText = 'STOP';
											else if (ev.id === 'button') {
												simMessageText = 'Interested in Tyre Retreading';
												simButtonPayload = 'BTN_INTERESTED';
											} else if (ev.id === 'messages') {
												simMessageText = 'Hello, please send tyre retread price list.';
											}
										}}
										class="p-2.5 rounded-xl border text-left transition-all cursor-pointer {simEventType === ev.id ? 'bg-primary/10 border-primary ring-1 ring-primary text-primary font-bold' : 'bg-muted/30 border-border text-foreground hover:bg-muted/60'}"
									>
										<div class="text-xs">{ev.label}</div>
										<div class="text-[10px] text-muted-foreground font-normal">{ev.desc}</div>
									</button>
								{/each}
							</div>
						</div>

						<!-- Simulator Form Inputs -->
						<div class="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
							<div class="space-y-1">
								<label class="font-medium text-foreground">Customer Phone Number</label>
								<Input bind:value={simPhone} placeholder="e.g. 919880334191" class="text-xs font-mono h-8" />
							</div>

							<div class="space-y-1">
								<label class="font-medium text-foreground">Customer Profile Name</label>
								<Input bind:value={simCustomerName} placeholder="e.g. Ramesh Logistics" class="text-xs h-8" />
							</div>

							<div class="space-y-1">
								<label class="font-medium text-foreground">Linked Campaign (Optional)</label>
								<select
									bind:value={simCampaignId}
									class="w-full h-8 px-2.5 rounded-lg border border-border bg-background text-foreground text-xs focus:ring-1 focus:ring-primary"
								>
									<option value="">Direct Message (None)</option>
									{#each campaigns as c}
										<option value={c.id}>{c.name}</option>
									{/each}
								</select>
							</div>

							{#if simEventType === 'messages' || simEventType === 'button' || simEventType === 'stop'}
								<div class="sm:col-span-2 space-y-1">
									<label class="font-medium text-foreground">Message Body Text</label>
									<Input bind:value={simMessageText} placeholder="Incoming message text..." class="text-xs h-8" />
								</div>
							{/if}

							{#if simEventType === 'button'}
								<div class="space-y-1">
									<label class="font-medium text-foreground">Button Payload Code</label>
									<Input bind:value={simButtonPayload} placeholder="e.g. BTN_YES" class="text-xs font-mono h-8" />
								</div>
							{/if}
						</div>

						<div class="flex justify-end pt-2 border-t border-border">
							<Button
								size="sm"
								class="bg-primary hover:bg-primary/90 text-primary-foreground gap-1.5 h-8 text-xs font-semibold"
								onclick={runSimulation}
								disabled={simulating}
							>
								<Icon name="zap" class="w-3.5 h-3.5 {simulating ? 'animate-spin' : ''}" />
								<span>{simulating ? 'Processing Event...' : '⚡ Fire Test Webhook Event'}</span>
							</Button>
						</div>
					</div>

					<!-- Webhook Event Audit Logs Section -->
					<div class="space-y-3">
						<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
							<div>
								<h3 class="text-sm font-bold text-foreground flex items-center gap-2">
									<Icon name="list" class="w-4 h-4 text-muted-foreground" />
									Live Webhook Event Audit Trail
								</h3>
								<p class="text-[11px] text-muted-foreground">
									Total: <strong class="text-foreground">{logsTotal}</strong> • Processed: <strong class="text-emerald-600">{logsProcessed}</strong> • Simulated: <strong class="text-blue-600">{logsSimulated}</strong> • Errors/Mismatches: <strong class="text-red-600">{logsErrors}</strong>
								</p>
							</div>

							<div class="flex items-center gap-2">
								<select
									bind:value={logFilterType}
									onchange={loadWebhookLogs}
									class="h-7 text-[11px] px-2 rounded-lg border border-border bg-background text-foreground"
								>
									<option value="ALL">All Event Types</option>
									<option value="messages">messages</option>
									<option value="statuses">statuses</option>
									<option value="handshake">handshake</option>
								</select>

								<select
									bind:value={logFilterStatus}
									onchange={loadWebhookLogs}
									class="h-7 text-[11px] px-2 rounded-lg border border-border bg-background text-foreground"
								>
									<option value="ALL">All Statuses</option>
									<option value="Processed">Processed</option>
									<option value="Simulated">Simulated</option>
									<option value="SignatureMismatch">SignatureMismatch</option>
									<option value="Error">Error</option>
									<option value="Ignored">Ignored</option>
								</select>

								<Button
									variant="outline"
									size="sm"
									class="h-7 text-[11px] gap-1 px-2"
									onclick={loadWebhookLogs}
									disabled={loadingLogs}
								>
									<Icon name="refresh-cw" class="w-3 h-3 {loadingLogs ? 'animate-spin' : ''}" />
									<span>Refresh</span>
								</Button>
							</div>
						</div>

						<!-- Logs Table -->
						<div class="border border-border rounded-2xl overflow-hidden bg-card text-xs">
							{#if loadingLogs}
								<div class="p-8 text-center text-muted-foreground flex items-center justify-center gap-2">
									<Icon name="loader-2" class="w-4 h-4 animate-spin text-primary" />
									<span>Loading webhook event logs...</span>
								</div>
							{:else if webhookLogs.length === 0}
								<div class="p-8 text-center text-muted-foreground">
									No webhook events recorded yet for this filter. Use the simulator above to fire your first test!
								</div>
							{:else}
								<div class="overflow-x-auto">
									<table class="w-full text-left border-collapse">
										<thead>
											<tr class="bg-muted/50 border-b border-border text-[11px] font-bold text-muted-foreground uppercase tracking-wider">
												<th class="p-3">Received At</th>
												<th class="p-3">Event Type</th>
												<th class="p-3">Status</th>
												<th class="p-3">From Phone / ID</th>
												<th class="p-3">Campaign</th>
												<th class="p-3 text-right">Actions</th>
											</tr>
										</thead>
										<tbody class="divide-y divide-border/60">
											{#each webhookLogs as log}
												<tr class="hover:bg-muted/30 transition-colors">
													<td class="p-3 whitespace-nowrap text-muted-foreground">
														{new Date(log.receivedAt).toLocaleString('en-IN', {
															day: '2-digit',
															month: 'short',
															year: 'numeric',
															hour: '2-digit',
															minute: '2-digit',
															second: '2-digit'
														})}
													</td>
													<td class="p-3">
														<span class="inline-flex items-center px-2 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wider {log.eventType === 'messages' ? 'bg-blue-100 text-blue-800 dark:bg-blue-950 dark:text-blue-300' : (log.eventType === 'statuses' ? 'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300' : 'bg-slate-100 text-slate-800 dark:bg-slate-900 dark:text-slate-300')}">
															{log.eventType}
														</span>
													</td>
													<td class="p-3">
														<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-bold border {log.processingStatus === 'Processed' ? 'bg-emerald-100 text-emerald-800 border-emerald-300 dark:bg-emerald-950 dark:text-emerald-300' : (log.processingStatus === 'Simulated' ? 'bg-blue-100 text-blue-800 border-blue-300 dark:bg-blue-950 dark:text-blue-300' : (log.processingStatus === 'HandshakeSuccess' ? 'bg-teal-100 text-teal-800 border-teal-300 dark:bg-teal-950 dark:text-teal-300' : (log.processingStatus === 'SignatureMismatch' ? 'bg-amber-100 text-amber-800 border-amber-300 dark:bg-amber-950 dark:text-amber-300' : 'bg-red-100 text-red-800 border-red-300 dark:bg-red-950 dark:text-red-300')))}">
															{log.processingStatus}
														</span>
													</td>
													<td class="p-3 font-mono text-[11px] text-foreground">
														{log.fromPhoneNumber || log.metaMessageId || '—'}
													</td>
													<td class="p-3 text-muted-foreground truncate max-w-[150px]">
														{log.campaignName || '—'}
													</td>
													<td class="p-3 text-right">
														<Button
															variant="outline"
															size="sm"
															class="h-7 text-[10px] gap-1 px-2"
															onclick={() => openPayloadModal(log.rawPayload)}
														>
															<Icon name="code" class="w-3 h-3" />
															<span>View JSON</span>
														</Button>
													</td>
												</tr>
											{/each}
										</tbody>
									</table>
								</div>
							{/if}
						</div>
					</div>
				</div>
			</div>
		</div>
	{/if}

	<!-- Raw Payload JSON Viewer Modal -->
	{#if showJsonModal}
		<div class="fixed inset-0 z-60 flex items-center justify-center p-4 bg-background/80 backdrop-blur-sm">
			<div class="bg-card border border-border rounded-3xl shadow-2xl w-full max-w-2xl max-h-[80vh] flex flex-col overflow-hidden animate-in fade-in zoom-in-95 duration-150">
				<div class="p-4 border-b border-border flex items-center justify-between bg-muted/30 shrink-0">
					<div class="flex items-center gap-2">
						<Icon name="code" class="w-4 h-4 text-primary" />
						<h3 class="text-sm font-bold text-foreground">Raw Webhook JSON Payload</h3>
					</div>
					<div class="flex items-center gap-2">
						<Button variant="outline" size="sm" class="h-7 text-xs gap-1" onclick={copyJsonToClipboard}>
							<Icon name="copy" class="w-3 h-3" />
							<span>Copy JSON</span>
						</Button>
						<Button variant="ghost" size="sm" class="h-7 w-7 p-0 rounded-full" onclick={() => (showJsonModal = false)}>
							<Icon name="x" class="w-3.5 h-3.5" />
						</Button>
					</div>
				</div>

				<div class="flex-1 overflow-auto p-4 bg-muted/20 font-mono text-xs text-foreground select-all leading-relaxed whitespace-pre">
					{selectedRawJson}
				</div>
			</div>
		</div>
	{/if}
</div>
