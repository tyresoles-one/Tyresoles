<script lang="ts">
	import { onMount, tick } from 'svelte';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Badge } from '$lib/components/ui/badge';
	import { Card, CardContent } from '$lib/components/ui/card';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { Icon } from '$lib/components/venUI/icon';
	import { Select } from '$lib/components/venUI/select';
	import { graphqlQuery, graphqlMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import {
		AutoExtractWebLeadsDocument,
		GetStagedLeadsDocument,
		GetCrawlCheckpointDocument,
		ResetCrawlCheckpointDocument,
		ProcessStagedLeadsDocument,
		type StagedLeadItem,
		type CrawlCheckpoint,
		type BadgeMetadata,
		type AutoExtractResult,
		type CrawlPipelineResult
	} from './queries';

	const GetMyRespCentersDocument = buildQuery`
		query GetMyRespCenters($type: String!) {
			myRespCenters(type: $type, first: 100) {
				nodes {
					code
					name
				}
			}
		}
	` as unknown as TypedDocumentNode<{ myRespCenters: { nodes: { code: string; name: string }[] } }, { type: string }>;

	const GetCrmMasterItemsDocument = buildQuery`
		query GetCrmMasterItems($type: CrmMasterType!, $where: CrmMasterItemFilterInput) {
			crmMasterItems: getCrmMasterItems(type: $type, where: $where) {
				id
				code
				name
			}
		}
	` as unknown as TypedDocumentNode<{ crmMasterItems: { id: number; code?: string | null; name: string }[] }, { type: string; where?: any }>;

	// ─── Input & Configuration State ───
	let targetUrl = $state<string>('https://transportfamily.com/listing-category/trailer-container-movement');
	let division = $state<string>('Tyresoles');
	let targetProduct = $state<string>('Commercial Retreading');
	let pagesPerRun = $state<number>(1);
	let autoIngest = $state<boolean>(true);
	let autoAdvanceBadges = $state<boolean>(true);
	let dryRun = $state<boolean>(false);

	// ─── Default CrmContact Import Attributes ───
	let defaultLeadSourceType = $state<string>('Automated');
	let defaultLeadSourceChannel = $state<string>('Web-Harvester');
	let defaultRespCenter = $state<string | null>(null);

	let leadSourceTypeOptions = $state<{ value: string; label: string }[]>([
		{ value: 'Automated', label: 'Automated (Harvester)' },
		{ value: 'Manual', label: 'Manual' },
		{ value: 'Inbound', label: 'Inbound Inquiry' },
		{ value: 'Referral', label: 'Referral' },
		{ value: 'Trade Show', label: 'Trade Show / Event' },
		{ value: 'Cold Outreach', label: 'Cold Outreach' },
		{ value: 'Campaign', label: 'Campaign (WhatsApp/Email)' },
		{ value: 'Tender', label: 'Tender / Contract' },
		{ value: 'Other', label: 'Other' }
	]);

	let leadSourceChannelOptions = $state<{ value: string; label: string }[]>([
		{ value: 'Web-Harvester', label: 'Web-Harvester' },
		{ value: 'Google-Maps', label: 'Google-Maps' },
		{ value: 'IndiaMART', label: 'IndiaMART' },
		{ value: 'TradeIndia', label: 'TradeIndia' },
		{ value: 'Justdial', label: 'Justdial' },
		{ value: 'Direct-Call', label: 'Direct-Call' },
		{ value: 'WhatsApp', label: 'WhatsApp' },
		{ value: 'Field-Visit', label: 'Field-Visit' },
		{ value: 'Website-Form', label: 'Website-Form' },
		{ value: 'Referral', label: 'Referral' },
		{ value: 'Directory', label: 'Industry Directory' }
	]);

	let respCenterOptions = $state<{ value: string; label: string }[]>([
		{ value: 'BEL', label: 'Belgaum (BEL)' },
		{ value: 'PUN', label: 'Pune (PUN)' },
		{ value: 'GOA', label: 'Goa (GOA)' },
		{ value: 'HUB', label: 'Hubli (HUB)' },
		{ value: 'MUM', label: 'Mumbai (MUM)' },
		{ value: 'KOL', label: 'Kolhapur (KOL)' }
	]);

	// ─── Execution State ───
	let isExtracting = $state<boolean>(false);
	let shouldStopLoop = $state<boolean>(false);
	let loopIterationCount = $state<number>(0);
	let currentPipelineStage = $state<number>(0); // 0: Idle, 1: Scout, 2: 8 Workers, 3: Validate, 4: Sync
	let activeWorkerCount = $state<number>(8);

	// ─── Collapsible Panel States ───
	let isSettingsExpanded = $state<boolean>(false);
	let isTerminalExpanded = $state<boolean>(false);

	// ─── Data & Buffer State ───
	let checkpoint = $state<CrawlCheckpoint | null>(null);
	let badgeInfo = $state<BadgeMetadata | null>(null);
	let stagedLeads = $state<StagedLeadItem[]>([]);
	let isLoadingBuffer = $state<boolean>(false);
	let activeTab = $state<'ALL' | 'PENDING' | 'IMPORTED' | 'DUPLICATE' | 'REJECTED'>('ALL');
	let searchFilter = $state<string>('');

	// ─── Accurate Page & Progress Derivations ───
	let totalPages = $derived(
		badgeInfo?.totalBadgePages ||
		checkpoint?.totalPagesDetected ||
		(checkpoint?.hasReachedEnd ? checkpoint.lastCrawledPage : null)
	);
	let currentPage = $derived(checkpoint?.lastCrawledPage || 0);
	let nextPage = $derived(checkpoint?.nextPageToCrawl || 1);
	let progressPercent = $derived(
		totalPages && totalPages > 0
			? Math.min(100, Math.max(0, Math.round((currentPage / totalPages) * 100)))
			: 0
	);

	// ─── Live Streaming Terminal Logs ───
	type LogEntry = {
		id: string;
		time: string;
		level: 'info' | 'success' | 'warn' | 'badge' | 'worker';
		message: string;
	};
	let terminalLogs = $state<LogEntry[]>([]);
	let terminalContainer: HTMLDivElement | null = $state(null);
	let latestLog = $derived(terminalLogs.length > 0 ? terminalLogs[terminalLogs.length - 1] : null);

	// ─── Preset Sample URLs ───
	const PRESET_URLS = [
		{
			label: 'Transport Family: Trailers',
			url: 'https://transportfamily.com/listing-category/trailer-container-movement',
			product: 'Commercial Retreading',
			division: 'Tyresoles'
		},
		{
			label: 'Transport Family: Contractors',
			url: 'https://transportfamily.com/listing-category/transport-contractors',
			product: 'Commercial Retreading',
			division: 'Tyresoles'
		},
		{
			label: 'All India Transporters Directory',
			url: 'https://www.transporters.in/directory/truck-fleet-owners',
			product: 'Radial Tubeless Retreading',
			division: 'Tyresoles'
		},
		{
			label: 'Commercial Tyres Hub',
			url: 'https://dir.indiamart.com/impcat/commercial-tyres.html',
			product: 'OTR & Mining Retreading',
			division: 'Tyresoles'
		}
	];

	function selectPreset(preset: typeof PRESET_URLS[0]) {
		targetUrl = preset.url;
		targetProduct = preset.product;
		division = preset.division;
		addLog('info', `Selected preset source: ${preset.label}`);
		loadCheckpoint();
		loadStagedLeads();
	}

	function addLog(level: LogEntry['level'], message: string) {
		const now = new Date();
		const time = now.toTimeString().split(' ')[0] + '.' + String(now.getMilliseconds()).padStart(3, '0');
		terminalLogs = [
			...terminalLogs.slice(-150),
			{ id: Math.random().toString(36).substring(2, 9), time, level, message }
		];
		tick().then(() => {
			if (terminalContainer) {
				terminalContainer.scrollTop = terminalContainer.scrollHeight;
			}
		});
	}

	function clearLogs() {
		terminalLogs = [];
	}

	// ─── Load Checkpoint & Buffer ───
	async function loadCheckpoint() {
		if (!targetUrl.trim()) return;
		try {
			const res = await graphqlQuery<{ checkpoint: CrawlCheckpoint }>(GetCrawlCheckpointDocument, {
				variables: { url: targetUrl.trim() },
				skipCache: true
			});
			if (res.success && res.data?.checkpoint) {
				checkpoint = res.data.checkpoint;
			}
		} catch (err: any) {
			console.error('Failed loading checkpoint:', err);
		}
	}

	async function loadStagedLeads() {
		if (!targetUrl.trim()) return;
		isLoadingBuffer = true;
		try {
			const res = await graphqlQuery<{ stagedLeads: StagedLeadItem[] }>(GetStagedLeadsDocument, {
				variables: {
					url: targetUrl.trim(),
					status: activeTab === 'ALL' ? undefined : activeTab,
					limit: 100
				},
				skipCache: true
			});
			if (res.success && res.data?.stagedLeads) {
				stagedLeads = res.data.stagedLeads;
			}
		} catch (err: any) {
			console.error('Failed loading staged leads:', err);
		} finally {
			isLoadingBuffer = false;
		}
	}

	// ─── Single-Action Automatic Information Extraction ───
	async function startAutoExtraction() {
		const cleanUrl = targetUrl.trim();
		if (!cleanUrl) {
			toast.error('Please enter a valid website or directory URL.');
			return;
		}
		if (!cleanUrl.startsWith('http://') && !cleanUrl.startsWith('https://')) {
			toast.error('URL must begin with http:// or https://');
			return;
		}

		isExtracting = true;
		shouldStopLoop = false;
		loopIterationCount = 0;
		addLog('info', `Initializing automatic extraction engine for ${cleanUrl}`);

		while (!shouldStopLoop) {
			loopIterationCount++;
			const pageToCrawl = checkpoint?.nextPageToCrawl || loopIterationCount;
			addLog('info', `--- Iteration #${loopIterationCount} | Targeting Page ${pageToCrawl} ---`);

			// Stage 1: Page Scouting & Badge Detection
			currentPipelineStage = 1;
			addLog('info', `[Stage 1] Scouting DOM & detecting pagination badges on page ${pageToCrawl}...`);

			// Stage 2: Detail Extraction (8 Parallel Worker Processes)
			currentPipelineStage = 2;
			addLog('worker', `[Stage 2] Spawning ${activeWorkerCount} concurrent extraction workers for fast detail resolution...`);

			try {
				const res = await graphqlMutation<{ autoExtractWebLeads: AutoExtractResult }>(AutoExtractWebLeadsDocument, {
					variables: {
						url: cleanUrl,
						pages: pagesPerRun,
						autoIngest: autoIngest,
						reset: false,
						division: division,
						targetProduct: targetProduct,
						dryRun: dryRun,
						defaultLeadSourceType: defaultLeadSourceType || 'Automated',
						defaultLeadSourceChannel: defaultLeadSourceChannel || 'Web-Harvester',
						defaultRespCenter: defaultRespCenter || null
					}
				});

				const result: AutoExtractResult | undefined = res.data?.autoExtractWebLeads;

				if (!res.success || !result) {
					addLog('warn', res.error || 'Extraction worker returned no response.');
					break;
				}

				// Stage 3: Data Quality & Geo Validation
				currentPipelineStage = 3;
				addLog('info', `[Stage 3] Validating 10-digit mobiles, addresses, and geocodes...`);

				// Stage 4: CRM Sync & Staging Buffer
				currentPipelineStage = 4;
				if (result.badgeMetadata) {
					badgeInfo = result.badgeMetadata;
					if (result.badgeMetadata.hasBadgeDetected) {
						addLog(
							'badge',
							`[Badge Detected] ${result.badgeMetadata.badgeText || `Page ${result.badgeMetadata.currentBadgePage} of ${result.badgeMetadata.totalBadgePages}`}`
						);
					}
				}

				if (result.checkpoint) {
					checkpoint = result.checkpoint;
				}

				if (result.leads && result.leads.length > 0) {
					stagedLeads = result.leads;
				} else {
					loadStagedLeads();
				}

				addLog('success', `[Stage 4 Success] ${result.message}`);
				toast.success(result.message);

				// Determine whether to continue auto-advancing
				if (!autoAdvanceBadges) {
					addLog('info', 'Auto-advance badges is disabled. Completed single batch.');
					break;
				}

				if (shouldStopLoop) {
					addLog('warn', 'Extraction loop stopped by operator.');
					break;
				}

				const totalDetected = result.checkpoint?.totalPagesDetected || badgeInfo?.totalBadgePages;
				const isAtEnd = Boolean(
					result.checkpoint?.hasReachedEnd ||
					(totalDetected && (result.checkpoint?.lastCrawledPage ?? 0) >= totalDetected)
				);

				if (isAtEnd) {
					addLog('badge', `Reached the final page (${result.checkpoint?.lastCrawledPage ?? totalDetected} of ${totalDetected ?? 'total'}). Directory harvesting complete.`);
					toast.info('Reached the end of available listings for this directory.');
					break;
				}

				if (result.pagesCrawled === 0) {
					addLog('warn', `Page ${pageToCrawl} could not be fetched due to server timeout or network drop.`);
					toast.warning(`Page ${pageToCrawl} timed out. Paused so you can click 'Start Auto Harvesting' to resume.`);
					break;
				}

				if (result.listingsDiscovered === 0) {
					if (totalDetected && (result.checkpoint?.lastCrawledPage ?? 0) < totalDetected) {
						addLog('warn', `Page ${pageToCrawl} returned 0 listings, though ${totalDetected} pages exist. Pausing auto-harvesting.`);
						toast.warning(`Page ${pageToCrawl} returned 0 listings. Pausing so you can resume.`);
						break;
					} else {
						addLog('badge', 'No further listings discovered on page.');
						toast.info('Reached the end of available listings for this directory.');
						break;
					}
				}

				addLog('info', `Next page badge detected: Page ${result.checkpoint?.nextPageToCrawl}. Auto-advancing in 1.2s...`);
				await new Promise((r) => setTimeout(r, 1200));

			} catch (err: any) {
				addLog('warn', `Extraction error: ${err?.message || err}`);
				toast.error(`Extraction failed: ${err?.message || err}`);
				break;
			}
		}

		isExtracting = false;
		currentPipelineStage = 0;
		addLog('success', `Automatic extraction completed. Processed across ${loopIterationCount} batch iteration(s).`);
		loadCheckpoint();
		loadStagedLeads();
	}

	function stopAutoExtraction() {
		shouldStopLoop = true;
		addLog('warn', 'Stop signal dispatched. Current batch will finalize safely.');
		toast.info('Stopping extraction after current batch completes...');
	}

	// ─── Reset Checkpoint ───
	async function resetCheckpoint(clearStaging: boolean) {
		if (!targetUrl.trim()) return;
		try {
			const res = await graphqlMutation<{ checkpoint: CrawlCheckpoint }>(ResetCrawlCheckpointDocument, {
				variables: {
					url: targetUrl.trim(),
					clearStaging: clearStaging
				}
			});
			if (res.success && res.data?.checkpoint) {
				checkpoint = res.data.checkpoint;
				badgeInfo = null;
				toast.success(clearStaging ? 'Checkpoint & staging buffer reset to Page 1.' : 'Checkpoint reset to Page 1.');
				addLog('warn', `Reset crawl checkpoint for ${targetUrl} (clearStaging: ${clearStaging})`);
				loadStagedLeads();
			}
		} catch (err: any) {
			toast.error(`Failed to reset: ${err?.message || err}`);
		}
	}

	// ─── Process / Sync Remaining Staged Leads ───
	async function syncPendingToCrm() {
		if (!targetUrl.trim()) return;
		try {
			addLog('info', 'Submitting pending staged leads for CRM validation and import...');
			const res = await graphqlMutation<{ processStagedLeads: CrawlPipelineResult }>(ProcessStagedLeadsDocument, {
				variables: {
					url: targetUrl.trim(),
					limit: 100,
					dryRun: dryRun,
					defaultLeadSourceType: defaultLeadSourceType || 'Automated',
					defaultLeadSourceChannel: defaultLeadSourceChannel || 'Web-Harvester',
					defaultRespCenter: defaultRespCenter || null
				}
			});
			if (res.success && res.data?.processStagedLeads) {
				const r = res.data.processStagedLeads;
				toast.success(r.message);
				addLog('success', r.message);
				if (r.checkpoint) checkpoint = r.checkpoint;
				loadStagedLeads();
			}
		} catch (err: any) {
			toast.error(`Sync failed: ${err?.message || err}`);
		}
	}

	// ─── CSV Export ───
	function exportCsv() {
		if (filteredLeads.length === 0) {
			toast.info('No leads to export in the current view.');
			return;
		}

		const headers = [
			'ID',
			'Page',
			'Company Name',
			'Contact Person',
			'Mobile No',
			'Alt Mobile',
			'Email',
			'City',
			'State',
			'Location Coordinates',
			'Website',
			'Status',
			'Source URL',
			'Created At'
		];

		const rows = filteredLeads.map((l) => [
			`"${l.id}"`,
			`"${l.pageNum}"`,
			`"${(l.companyName || '').replace(/"/g, '""')}"`,
			`"${(l.contactPerson || '').replace(/"/g, '""')}"`,
			`"${l.mobileNo || ''}"`,
			`"${l.altMobileNo || ''}"`,
			`"${l.email || ''}"`,
			`"${(l.city || '').replace(/"/g, '""')}"`,
			`"${(l.state || '').replace(/"/g, '""')}"`,
			`"${l.location || ''}"`,
			`"${l.website || ''}"`,
			`"${l.status}"`,
			`"${l.sourceUrl}"`,
			`"${l.createdAt || ''}"`
		]);

		const csvContent = 'data:text/csv;charset=utf-8,' + [headers.join(','), ...rows.map((e) => e.join(','))].join('\n');
		const encodedUri = encodeURI(csvContent);
		const link = document.createElement('a');
		link.setAttribute('href', encodedUri);
		link.setAttribute('download', `leads_export_${Date.now()}.csv`);
		document.body.appendChild(link);
		link.click();
		document.body.removeChild(link);
		toast.success(`Exported ${filteredLeads.length} lead(s) to CSV.`);
	}

	// ─── Filtered Leads Derived State ───
	let filteredLeads = $derived(
		stagedLeads.filter((lead) => {
			if (activeTab !== 'ALL' && lead.status !== activeTab) {
				return false;
			}
			if (!searchFilter.trim()) return true;
			const q = searchFilter.toLowerCase();
			return (
				(lead.companyName && lead.companyName.toLowerCase().includes(q)) ||
				(lead.contactPerson && lead.contactPerson.toLowerCase().includes(q)) ||
				(lead.mobileNo && lead.mobileNo.includes(q)) ||
				(lead.city && lead.city.toLowerCase().includes(q)) ||
				(lead.state && lead.state.toLowerCase().includes(q))
			);
		})
	);

	async function loadRespCenters() {
		try {
			const res = await graphqlQuery<any>(GetMyRespCentersDocument, { variables: { type: 'Sale' } });
			if (res.success && res.data?.myRespCenters?.nodes?.length) {
				const centers = res.data.myRespCenters.nodes.map((n: { code: string; name: string }) => ({
					value: n.code,
					label: `${n.name} (${n.code})`
				}));
				if (centers.length > 0) {
					respCenterOptions = centers;
				}
			}
		} catch (e) {
			console.warn('Could not fetch responsibility centers; using defaults', e);
		}
	}

	async function loadMasterSourcesAndChannels() {
		try {
			const [sourceRes, channelRes] = await Promise.all([
				graphqlQuery<{ crmMasterItems: { id: number; code?: string | null; name: string }[] }>(GetCrmMasterItemsDocument, { variables: { type: 'SOURCE' } }),
				graphqlQuery<{ crmMasterItems: { id: number; code?: string | null; name: string }[] }>(GetCrmMasterItemsDocument, { variables: { type: 'SOURCE_CHANNEL' } })
			]);

			if (sourceRes.success && sourceRes.data?.crmMasterItems && sourceRes.data.crmMasterItems.length > 0) {
				leadSourceTypeOptions = sourceRes.data.crmMasterItems.map((item) => ({
					value: item.name,
					label: item.name
				}));
			}

			if (channelRes.success && channelRes.data?.crmMasterItems && channelRes.data.crmMasterItems.length > 0) {
				leadSourceChannelOptions = channelRes.data.crmMasterItems.map((item) => ({
					value: item.name,
					label: item.code ? `${item.name} (${item.code})` : item.name
				}));
			}
		} catch (e) {
			console.warn('Failed to load CRM master sources and channels; using defaults', e);
		}
	}

	onMount(() => {
		loadCheckpoint();
		loadStagedLeads();
		loadRespCenters();
		loadMasterSourcesAndChannels();
	});
</script>

<div class="flex flex-col gap-6 p-6 min-h-screen bg-slate-50/50 dark:bg-slate-950/20">
	<!-- Header -->
	<PageHeading
		pageTitle="Automatic Lead Generation & Harvester | Tyresoles CRM"
		icon="bot"
	>
		{#snippet title()}
			<div class="flex items-center gap-3">
				<h1 class="text-2xl font-bold tracking-tight text-slate-900 dark:text-slate-100">
					Automatic Lead Generation & Harvester
				</h1>
				<Badge variant="outline" class="bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/30 dark:text-emerald-400 font-mono text-xs">
					Multi-Worker V2
				</Badge>
			</div>
		{/snippet}
		{#snippet description()}
			<p class="text-sm text-slate-600 dark:text-slate-400 mt-1">
				Autonomous single-process web data extraction engine with multi-worker concurrency, badge/pagination auto-detection, and direct CRM synchronization.
			</p>
		{/snippet}
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<a
					href="/crm-masters?category=leads"
					target="_blank"
					class="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md border border-slate-200 dark:border-slate-800 bg-background text-xs font-medium text-slate-700 dark:text-slate-300 hover:bg-slate-100 dark:hover:bg-slate-900 transition-colors h-9"
					title="Open CRM Masters to configure lead sources, channels, and categories"
				>
					<Icon name="database" class="size-3.5 text-indigo-600 dark:text-indigo-400" />
					<span>CRM Masters</span>
				</a>
				<Button
					variant="outline"
					size="sm"
					onclick={() => { loadCheckpoint(); loadStagedLeads(); }}
					disabled={isExtracting}
					class="text-xs h-9"
				>
					<svg class="w-3.5 h-3.5 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>
					Refresh Buffer
				</Button>
				<Button
					variant="outline"
					size="sm"
					onclick={exportCsv}
					disabled={isExtracting}
					class="text-xs h-9"
				>
					<svg class="w-3.5 h-3.5 mr-1.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>
					Export CSV
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- Control Center Card -->
	<Card class="border shadow-sm bg-card">
		<CardContent class="p-4 flex flex-col gap-3">
			<!-- Universal URL Bar -->
			<div class="flex flex-col gap-1.5">
				<div class="flex items-center justify-between">
					<label for="universal-url" class="text-xs font-semibold uppercase tracking-wider text-foreground flex items-center gap-1.5">
						<svg class="w-4 h-4 text-primary" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 01-9 9m9-9a9 9 0 00-9-9m9 9H3m9 9a9 9 0 01-9-9m9 9c1.657 0 3-4.03 3-9s-1.343-9-3-9m0 18c-1.657 0-3-4.03-3-9s1.343-9 3-9m-9 9a9 9 0 019-9"/></svg>
						Target Directory or Listing URL
					</label>
					<button
						type="button"
						class="text-xs text-primary hover:underline flex items-center gap-1 font-medium"
						onclick={() => (isSettingsExpanded = !isSettingsExpanded)}
					>
						<Icon name="settings" class="size-3.5" />
						<span>{isSettingsExpanded ? 'Hide Settings' : 'Configure Attributes & Settings'}</span>
						<svg class="w-3.5 h-3.5 transition-transform {isSettingsExpanded ? 'rotate-180' : ''}" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/></svg>
					</button>
				</div>
				<div class="flex items-center gap-2">
					<div class="relative flex-1">
						<Input
							id="universal-url"
							type="url"
							bind:value={targetUrl}
							placeholder="https://transportfamily.com/listing-category/trailer-container-movement..."
							class="h-10 font-mono text-xs pl-3 pr-8 shadow-sm border-input"
							disabled={isExtracting}
						/>
						{#if targetUrl}
							<button
								type="button"
								class="absolute right-2.5 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground text-xs"
								onclick={() => (targetUrl = '')}
								title="Clear"
							>
								✕
							</button>
						{/if}
					</div>

					<!-- Primary Extraction Action -->
					{#if isExtracting}
						<Button
							variant="destructive"
							size="default"
							class="h-10 px-5 font-semibold shadow-sm gap-2 animate-pulse"
							onclick={stopAutoExtraction}
						>
							<Loader2 class="w-4 h-4 animate-spin" />
							Stop Harvesting
						</Button>
					{:else}
						<Button
							size="default"
							class="h-10 px-6 font-semibold shadow-sm gap-2 bg-primary hover:bg-primary/90 text-primary-foreground"
							onclick={startAutoExtraction}
						>
							<svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
							Start Auto Harvesting
						</Button>
					{/if}
				</div>

				<!-- Preset Samples & Active Configuration Summary Chips -->
				<div class="flex flex-wrap items-center justify-between gap-2 pt-1 text-xs">
					<div class="flex flex-wrap items-center gap-1.5">
						<span class="text-[11px] text-muted-foreground font-medium">Quick Sources:</span>
						{#each PRESET_URLS as preset}
							<button
								type="button"
								class="text-[11px] px-2 py-0.5 rounded-full border border-border bg-muted/50 hover:bg-muted text-foreground transition-colors"
								onclick={() => selectPreset(preset)}
							>
								{preset.label}
							</button>
						{/each}
					</div>

					<div class="flex items-center gap-1.5 text-[11px] text-muted-foreground">
						<span class="px-1.5 py-0.5 rounded bg-muted font-mono">{division}</span>
						<span>•</span>
						<span class="px-1.5 py-0.5 rounded bg-muted font-mono">{targetProduct}</span>
						<span>•</span>
						<span class="px-1.5 py-0.5 rounded bg-muted font-mono">{defaultLeadSourceChannel}</span>
					</div>
				</div>
			</div>

			<!-- Collapsible Pipeline Tuning & Settings Bar -->
			{#if isSettingsExpanded}
				<div class="grid grid-cols-1 md:grid-cols-4 gap-3 pt-3 mt-1 border-t border-border bg-muted/20 p-3 rounded-lg">
					<!-- Division -->
					<div class="flex flex-col gap-1">
						<label for="division-select" class="text-xs text-muted-foreground font-medium">CRM Division</label>
						<select
							id="division-select"
							bind:value={division}
							class="h-8 px-2 bg-background border border-input rounded text-xs shadow-sm"
							disabled={isExtracting}
						>
							<option value="Tyresoles">Tyresoles</option>
							<option value="Ecoflex">Ecoflex</option>
						</select>
					</div>

					<!-- Target Product Line -->
					<div class="flex flex-col gap-1">
						<label for="product-select" class="text-xs text-muted-foreground font-medium">Target Product</label>
						<select
							id="product-select"
							bind:value={targetProduct}
							class="h-8 px-2 bg-background border border-input rounded text-xs shadow-sm"
							disabled={isExtracting}
						>
							<option value="Commercial Retreading">Commercial Retreading</option>
							<option value="Radial Tubeless Retreading">Radial Tubeless Retreading</option>
							<option value="OTR & Mining Retreading">OTR & Mining Retreading</option>
							<option value="Fleet Services">Fleet Services</option>
						</select>
					</div>

					<!-- Lead Source Type -->
					<div class="flex flex-col gap-1">
						<div class="flex items-center justify-between">
							<span class="text-xs text-muted-foreground font-medium">Lead Source Type</span>
							<a href="/crm-masters?tab=SOURCE" target="_blank" class="text-[10px] text-primary hover:underline">Manage</a>
						</div>
						<Select
							options={leadSourceTypeOptions}
							bind:value={defaultLeadSourceType}
							placeholder="Source type..."
							valueKey="value"
							labelKey="label"
							disabled={isExtracting}
							class="w-full h-8 rounded text-xs"
						/>
					</div>

					<!-- Lead Source Channel -->
					<div class="flex flex-col gap-1">
						<div class="flex items-center justify-between">
							<span class="text-xs text-muted-foreground font-medium">Lead Channel</span>
							<a href="/crm-masters?tab=SOURCE_CHANNEL" target="_blank" class="text-[10px] text-primary hover:underline">Manage</a>
						</div>
						<Select
							options={leadSourceChannelOptions}
							bind:value={defaultLeadSourceChannel}
							placeholder="Source channel..."
							valueKey="value"
							labelKey="label"
							disabled={isExtracting}
							clearable={true}
							class="w-full h-8 rounded text-xs"
						/>
					</div>

					<!-- Responsibility Center -->
					<div class="flex flex-col gap-1">
						<span class="text-xs text-muted-foreground font-medium">Resp Center</span>
						<Select
							options={respCenterOptions}
							bind:value={defaultRespCenter}
							placeholder="Auto (City-based)..."
							valueKey="value"
							labelKey="label"
							disabled={isExtracting}
							clearable={true}
							class="w-full h-8 rounded text-xs"
						/>
					</div>

					<!-- Toggles -->
					<div class="flex items-center gap-4 md:col-span-2 pt-4">
						<label class="flex items-center gap-1.5 cursor-pointer text-xs font-medium text-foreground">
							<input type="checkbox" bind:checked={autoIngest} disabled={isExtracting} class="rounded text-primary w-4 h-4" />
							<span>Auto-Sync to CRM</span>
						</label>
						<label class="flex items-center gap-1.5 cursor-pointer text-xs font-medium text-foreground" title="Automatically advance consecutive pagination badges">
							<input type="checkbox" bind:checked={autoAdvanceBadges} disabled={isExtracting} class="rounded text-primary w-4 h-4" />
							<span>Auto-Advance Pages</span>
						</label>
						<label class="flex items-center gap-1.5 cursor-pointer text-xs text-muted-foreground" title="Validate leads without writing into CRM database">
							<input type="checkbox" bind:checked={dryRun} disabled={isExtracting} class="rounded text-primary w-3.5 h-3.5" />
							<span>Dry Run</span>
						</label>
					</div>

					<!-- Reset Checkpoint -->
					<div class="flex justify-end items-center pt-4">
						<Button
							variant="ghost"
							size="sm"
							onclick={() => resetCheckpoint(false)}
							disabled={isExtracting}
							class="text-xs text-rose-600 hover:text-rose-700 hover:bg-rose-50 dark:hover:bg-rose-950/20 h-8"
							title="Reset crawler pointer to Page 1"
						>
							Reset Pointer to Page 1
						</Button>
					</div>
				</div>
			{/if}
		</CardContent>
	</Card>

	<!-- Crisp Pipeline Progress & KPI Metrics Bar -->
	<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-3">
		<!-- Page Progress -->
		<Card class="border shadow-sm bg-card p-3.5 flex flex-col justify-between">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Page Progress</span>
				{#if checkpoint?.hasReachedEnd}
					<Badge variant="outline" class="text-[10px] px-1.5 py-0 border-emerald-300 text-emerald-700 bg-emerald-50 dark:bg-emerald-950/30">Completed</Badge>
				{:else if isExtracting}
					<Badge class="text-[10px] px-1.5 py-0 bg-blue-600 text-white animate-pulse">Scanning</Badge>
				{:else}
					<Badge variant="secondary" class="text-[10px] px-1.5 py-0 font-mono">Next: Pg {nextPage}</Badge>
				{/if}
			</div>

			<div class="mt-2">
				<div class="flex items-baseline justify-between">
					<span class="text-xl font-extrabold font-mono text-foreground">
						Page {currentPage} <span class="text-sm font-normal text-muted-foreground">/ {totalPages || '—'}</span>
					</span>
					<span class="text-xs font-bold font-mono text-primary">{progressPercent}%</span>
				</div>
				<!-- Visual Progress Bar -->
				<div class="w-full bg-muted rounded-full h-1.5 mt-2 overflow-hidden">
					<div
						class="bg-blue-600 h-1.5 rounded-full transition-all duration-500 {isExtracting ? 'animate-pulse' : ''}"
						style="width: {Math.max(progressPercent, currentPage > 0 ? 3 : 0)}%"
					></div>
				</div>
			</div>

			<div class="mt-2 pt-2 border-t border-border/60 flex items-center justify-between text-[11px] text-muted-foreground">
				<span>Scanned so far:</span>
				<strong class="font-mono text-foreground">{checkpoint?.totalPagesCrawled ?? 0} pages</strong>
			</div>
		</Card>

		<!-- Discovered Listings -->
		<Card class="border shadow-sm bg-card p-3.5 flex flex-col justify-between">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Discovered</span>
				<span class="p-1 rounded bg-indigo-50 dark:bg-indigo-950/30 text-indigo-600">
					<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
				</span>
			</div>
			<div class="mt-1">
				<div class="text-2xl font-extrabold font-mono text-foreground">
					{checkpoint?.totalListingsDiscovered ?? 0}
				</div>
				<p class="text-[11px] text-muted-foreground mt-0.5">Directory listing cards scouted</p>
			</div>
			<div class="mt-2 pt-2 border-t border-border/60 flex items-center justify-between text-[11px] text-muted-foreground">
				<span>Worker concurrency:</span>
				<strong class="font-mono text-foreground">{activeWorkerCount} fetchers</strong>
			</div>
		</Card>

		<!-- Imported Contacts -->
		<Card class="border shadow-sm bg-card p-3.5 flex flex-col justify-between">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">CRM Contacts</span>
				<span class="p-1 rounded bg-emerald-50 dark:bg-emerald-950/30 text-emerald-600">
					<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
				</span>
			</div>
			<div class="mt-1">
				<div class="text-2xl font-extrabold font-mono text-emerald-600 dark:text-emerald-400">
					{checkpoint?.queueSummary?.imported ?? 0}
				</div>
				<p class="text-[11px] text-muted-foreground mt-0.5">Imported directly into CRM database</p>
			</div>
			<div class="mt-2 pt-2 border-t border-border/60 flex items-center justify-between text-[11px] text-muted-foreground">
				<span>Sync status:</span>
				<strong class="font-mono text-emerald-600">Synced Active</strong>
			</div>
		</Card>

		<!-- Duplicates Filtered -->
		<Card class="border shadow-sm bg-card p-3.5 flex flex-col justify-between">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Duplicates</span>
				<span class="p-1 rounded bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
					<svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>
				</span>
			</div>
			<div class="mt-1">
				<div class="text-2xl font-extrabold font-mono text-muted-foreground">
					{checkpoint?.queueSummary?.duplicate ?? 0}
				</div>
				<p class="text-[11px] text-muted-foreground mt-0.5">Duplicates safely skipped</p>
			</div>
			<div class="mt-2 pt-2 border-t border-border/60 flex items-center justify-between text-[11px] text-muted-foreground">
				<span>Match rule:</span>
				<strong class="font-mono text-foreground">10-digit Phone</strong>
			</div>
		</Card>

		<!-- Staging Buffer Queue -->
		<Card class="border shadow-sm bg-card p-3.5 flex flex-col justify-between">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Staging Buffer</span>
				{#if (checkpoint?.queueSummary?.pending ?? 0) > 0}
					<Badge class="text-[10px] px-1.5 py-0 bg-amber-500 text-white">Pending Sync</Badge>
				{:else}
					<Badge variant="outline" class="text-[10px] px-1.5 py-0 text-muted-foreground">Buffer Clean</Badge>
				{/if}
			</div>
			<div class="mt-1">
				<div class="text-2xl font-extrabold font-mono {(checkpoint?.queueSummary?.pending ?? 0) > 0 ? 'text-amber-600' : 'text-foreground'}">
					{checkpoint?.queueSummary?.pending ?? 0}
				</div>
				<p class="text-[11px] text-muted-foreground mt-0.5">Leads awaiting CRM validation</p>
			</div>
			<div class="mt-2 pt-2 border-t border-border/60 flex items-center justify-between text-[11px]">
				{#if (checkpoint?.queueSummary?.pending ?? 0) > 0}
					<button type="button" class="text-xs font-semibold text-amber-600 hover:underline" onclick={syncPendingToCrm}>
						Sync Now &rarr;
					</button>
				{:else}
					<span class="text-muted-foreground">Rejected invalid:</span>
					<strong class="font-mono text-muted-foreground">{checkpoint?.queueSummary?.rejected ?? 0}</strong>
				{/if}
			</div>
		</Card>
	</div>

	<!-- Collapsible Live Activity Stream (Terminal) -->
	<Card class="border shadow-sm bg-slate-950 text-slate-100 font-mono text-xs rounded-xl overflow-hidden">
		<div class="px-4 py-2 bg-slate-900 border-b border-slate-800 flex items-center justify-between gap-3">
			<div class="flex items-center gap-2 overflow-hidden">
				<span class="inline-block w-2 h-2 rounded-full bg-emerald-500 {isExtracting ? 'animate-pulse' : ''} flex-shrink-0"></span>
				<span class="font-bold text-slate-300 uppercase tracking-wider text-[11px] flex-shrink-0">Activity Stream</span>
				<!-- Status Ticker -->
				<span class="text-slate-400 text-[11px] truncate" title={latestLog ? latestLog.message : ''}>
					{latestLog ? `• ${latestLog.message}` : (isExtracting ? '• Harvesting active...' : '• Ready to extract.')}
				</span>
			</div>
			<div class="flex items-center gap-2 flex-shrink-0">
				{#if terminalLogs.length > 0}
					<button type="button" class="text-slate-400 hover:text-slate-200 text-[11px] px-2 py-0.5 rounded hover:bg-slate-800 transition-colors" onclick={clearLogs}>
						Clear
					</button>
				{/if}
				<button
					type="button"
					class="text-slate-300 hover:text-white text-[11px] px-2.5 py-0.5 rounded bg-slate-800 hover:bg-slate-700 transition-colors flex items-center gap-1 font-sans"
					onclick={() => (isTerminalExpanded = !isTerminalExpanded)}
				>
					<span>{isTerminalExpanded ? 'Collapse' : `Show Console (${terminalLogs.length})`}</span>
					<svg class="w-3 h-3 transition-transform {isTerminalExpanded ? 'rotate-180' : ''}" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7"/></svg>
				</button>
			</div>
		</div>

		{#if isTerminalExpanded}
			<div
				bind:this={terminalContainer}
				class="h-44 overflow-y-auto p-3 flex flex-col gap-1 select-text scrollbar-thin scrollbar-thumb-slate-800"
			>
				{#if terminalLogs.length === 0}
					<div class="text-slate-500 italic py-6 text-center">
						Engine idle. Click "Start Auto Harvesting" above to launch autonomous multi-process harvesting.
					</div>
				{:else}
					{#each terminalLogs as log (log.id)}
						<div class="flex items-start gap-2 leading-relaxed">
							<span class="text-slate-500 text-[10px] whitespace-nowrap">[{log.time}]</span>
							{#if log.level === 'badge'}
								<span class="text-blue-400 font-bold whitespace-nowrap">[BADGE]</span>
								<span class="text-blue-200">{log.message}</span>
							{:else if log.level === 'worker'}
								<span class="text-indigo-400 font-bold whitespace-nowrap">[WORKER]</span>
								<span class="text-indigo-200">{log.message}</span>
							{:else if log.level === 'success'}
								<span class="text-emerald-400 font-bold whitespace-nowrap">[SUCCESS]</span>
								<span class="text-emerald-200">{log.message}</span>
							{:else if log.level === 'warn'}
								<span class="text-amber-400 font-bold whitespace-nowrap">[WARN]</span>
								<span class="text-amber-200">{log.message}</span>
							{:else}
								<span class="text-slate-400 font-bold whitespace-nowrap">[INFO]</span>
								<span class="text-slate-300">{log.message}</span>
							{/if}
						</div>
					{/each}
				{/if}
			</div>
		{/if}
	</Card>

	<!-- Staged & Extracted Leads Buffer Table -->
	<Card class="border shadow-sm bg-card">
		<div class="p-4 border-b border-border flex flex-col md:flex-row items-start md:items-center justify-between gap-3">
			<!-- Tabs -->
			<div class="flex items-center gap-1.5 flex-wrap">
				<button
					type="button"
					class="px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors {activeTab === 'ALL' ? 'bg-primary text-primary-foreground shadow-sm' : 'hover:bg-muted text-muted-foreground'}"
					onclick={() => { activeTab = 'ALL'; loadStagedLeads(); }}
				>
					All Leads ({checkpoint?.queueSummary?.total ?? stagedLeads.length})
				</button>
				<button
					type="button"
					class="px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors {activeTab === 'PENDING' ? 'bg-amber-500 text-white shadow-sm' : 'hover:bg-muted text-muted-foreground'}"
					onclick={() => { activeTab = 'PENDING'; loadStagedLeads(); }}
				>
					Pending Buffer ({checkpoint?.queueSummary?.pending ?? 0})
				</button>
				<button
					type="button"
					class="px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors {activeTab === 'IMPORTED' ? 'bg-emerald-600 text-white shadow-sm' : 'hover:bg-muted text-muted-foreground'}"
					onclick={() => { activeTab = 'IMPORTED'; loadStagedLeads(); }}
				>
					Imported to CRM ({checkpoint?.queueSummary?.imported ?? 0})
				</button>
				<button
					type="button"
					class="px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors {activeTab === 'DUPLICATE' ? 'bg-slate-600 text-white shadow-sm' : 'hover:bg-muted text-muted-foreground'}"
					onclick={() => { activeTab = 'DUPLICATE'; loadStagedLeads(); }}
				>
					Duplicates ({checkpoint?.queueSummary?.duplicate ?? 0})
				</button>
				<button
					type="button"
					class="px-3 py-1.5 rounded-lg text-xs font-semibold transition-colors {activeTab === 'REJECTED' ? 'bg-rose-600 text-white shadow-sm' : 'hover:bg-muted text-muted-foreground'}"
					onclick={() => { activeTab = 'REJECTED'; loadStagedLeads(); }}
				>
					Rejected ({checkpoint?.queueSummary?.rejected ?? 0})
				</button>
			</div>

			<!-- Search Filter & Ingest Button -->
			<div class="flex items-center gap-2 w-full md:w-auto">
				<Input
					type="text"
					bind:value={searchFilter}
					placeholder="Filter by company, phone, city..."
					class="h-8 text-xs w-full md:w-60"
				/>
				{#if (checkpoint?.queueSummary?.pending ?? 0) > 0}
					<Button
						size="sm"
						onclick={syncPendingToCrm}
						disabled={isExtracting}
						class="h-8 text-xs bg-amber-600 hover:bg-amber-700 text-white whitespace-nowrap"
					>
						Sync Pending ({checkpoint?.queueSummary?.pending}) to CRM
					</Button>
				{/if}
			</div>
		</div>

		<!-- Table Content -->
		<div class="overflow-x-auto min-h-[300px]">
			{#if isLoadingBuffer}
				<div class="flex flex-col items-center justify-center p-12 text-muted-foreground gap-2">
					<Loader2 class="w-6 h-6 animate-spin text-primary" />
					<span class="text-xs">Loading staged buffer...</span>
				</div>
			{:else if filteredLeads.length === 0}
				<div class="flex flex-col items-center justify-center p-12 text-muted-foreground gap-2">
					<svg class="w-8 h-8 text-slate-300" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/></svg>
					<span class="text-xs font-medium">No leads matching the current filter or status.</span>
					<span class="text-[11px]">Run "Start Automatic Extraction" above to discover and buffer leads.</span>
				</div>
			{:else}
				<table class="w-full text-xs text-left border-collapse">
					<thead class="bg-muted/40 uppercase tracking-wider text-[11px] text-muted-foreground border-b border-border">
						<tr>
							<th class="p-3 font-semibold w-12 text-center">Pg</th>
							<th class="p-3 font-semibold">Company / Enterprise</th>
							<th class="p-3 font-semibold">Contact Person</th>
							<th class="p-3 font-semibold">Phone / Direct Call</th>
							<th class="p-3 font-semibold">City & State</th>
							<th class="p-3 font-semibold">Location Coordinates</th>
							<th class="p-3 font-semibold text-center">Status</th>
							<th class="p-3 font-semibold text-right">Source Link</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border">
						{#each filteredLeads as lead (lead.id)}
							<tr class="hover:bg-muted/30 transition-colors">
								<td class="p-3 text-center font-mono text-muted-foreground">
									#{lead.pageNum}
								</td>
								<td class="p-3">
									<div class="font-semibold text-foreground">
										{lead.companyName || 'Unknown Company'}
									</div>
									{#if lead.website}
										<a href={lead.website.startsWith('http') ? lead.website : 'https://' + lead.website} target="_blank" rel="noreferrer" class="text-[10px] text-blue-600 hover:underline flex items-center gap-1 mt-0.5">
											<svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
											{lead.website.replace(/^https?:\/\//, '')}
										</a>
									{/if}
								</td>
								<td class="p-3 text-muted-foreground">
									{lead.contactPerson || '-'}
								</td>
								<td class="p-3">
									{#if lead.mobileNo}
										<div class="flex items-center gap-1.5">
											<a
												href="tel:{lead.mobileNo}"
												class="font-mono font-semibold text-primary hover:underline flex items-center gap-1"
											>
												<svg class="w-3.5 h-3.5 text-emerald-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"/></svg>
												{lead.mobileNo}
											</a>
											<a
												href="https://wa.me/91{lead.mobileNo.replace(/[^0-9]/g, '')}"
												target="_blank"
												rel="noreferrer"
												class="text-[10px] px-1.5 py-0.5 rounded bg-emerald-50 text-emerald-700 hover:bg-emerald-100 border border-emerald-200"
												title="Open WhatsApp"
											>
												WA
											</a>
										</div>
										{#if lead.altMobileNo}
											<div class="font-mono text-[10px] text-muted-foreground mt-0.5">
												Alt: {lead.altMobileNo}
											</div>
										{/if}
									{:else}
										<span class="text-slate-400 italic">No Mobile</span>
									{/if}
								</td>
								<td class="p-3">
									<div class="font-medium text-foreground">{lead.city || '-'}</div>
									{#if lead.state}
										<div class="text-[10px] text-muted-foreground">{lead.state}</div>
									{/if}
								</td>
								<td class="p-3">
									{#if lead.location}
										<a
											href="https://www.google.com/maps?q={lead.location}"
											target="_blank"
											rel="noreferrer"
											class="font-mono text-[10px] text-blue-600 hover:underline flex items-center gap-1"
										>
											<svg class="w-3 h-3 text-rose-500" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z"/></svg>
											{lead.location}
										</a>
									{:else}
										<span class="text-slate-400">-</span>
									{/if}
								</td>
								<td class="p-3 text-center">
									{#if lead.status === 'IMPORTED'}
										<Badge class="bg-emerald-500 text-white text-[10px] px-2 py-0.5">CRM Sync</Badge>
									{:else if lead.status === 'PENDING'}
										<Badge class="bg-amber-500 text-white text-[10px] px-2 py-0.5">Pending</Badge>
									{:else if lead.status === 'DUPLICATE'}
										<Badge variant="secondary" class="text-slate-600 dark:text-slate-400 text-[10px] px-2 py-0.5">Duplicate</Badge>
									{:else}
										<Badge variant="destructive" class="text-[10px] px-2 py-0.5">{lead.status}</Badge>
									{/if}
								</td>
								<td class="p-3 text-right">
									<a
										href={lead.sourceUrl}
										target="_blank"
										rel="noreferrer"
										class="text-xs text-blue-600 hover:text-blue-800 hover:underline flex items-center justify-end gap-1 font-mono"
									>
										<span>Listing</span>
										<svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14"/></svg>
									</a>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			{/if}
		</div>
	</Card>
</div>

