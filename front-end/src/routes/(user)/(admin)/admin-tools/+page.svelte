<script lang="ts">
	import { onMount, onDestroy } from 'svelte';
	import { graphqlMutation, graphqlQuery } from '$lib/services/graphql/client';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardHeader, CardTitle, CardContent, CardFooter, CardDescription } from '$lib/components/ui/card';
	import { Badge } from '$lib/components/ui/badge';
	import { Input } from '$lib/components/ui/input';
	import { Progress } from '$lib/components/ui/progress';
	import * as Tabs from '$lib/components/ui/tabs';
	import {
		Table,
		TableBody,
		TableCell,
		TableHead,
		TableHeader,
		TableRow
	} from '$lib/components/ui/table';

	// ==========================================
	// Team Sales Targets Automation Models & State
	// ==========================================
	interface TeamTargetItem {
		teamCode: string;
		teamName: string;
		respCenter: string;
		currentSale: number;
		targetMultiplier: number;
		nextTarget: number;
		existingTarget: number;
		existingTargetEndDate?: string | null;
		nextTargetEndDate: string;
	}

	interface PreviewResultData {
		success: boolean;
		message: string;
		respCenter?: string | null;
		respCenterMultiplier: number;
		fromDate: string;
		toDate: string;
		nextTargetEndDate: string;
		totalCurrentSale: number;
		totalNextTarget: number;
		roundingStep: number;
		effectiveGrowthPercent: number;
		items: TeamTargetItem[];
	}

	interface RespCenterItem {
		code: string;
		name: string;
		targetMultiplier: number;
	}

	function formatINR(val: number | null | undefined): string {
		if (val == null || isNaN(val)) return '₹0.00';
		return new Intl.NumberFormat('en-IN', {
			style: 'currency',
			currency: 'INR',
			maximumFractionDigits: 2
		}).format(val);
	}

	function formatLakhs(val: number | null | undefined): string {
		if (val == null || isNaN(val)) return '0.00 L';
		const inLakhs = val / 100000;
		return `${inLakhs.toFixed(2)} L`;
	}

	function formatShortLakhs(val: number | null | undefined): string {
		if (val == null || isNaN(val)) return '0L';
		const inLakhs = val / 100000;
		return inLakhs % 1 === 0 ? `${inLakhs.toFixed(0)} L` : `${inLakhs.toFixed(1)} L`;
	}

	function formatDate(d: string | Date | null | undefined): string {
		if (!d) return '—';
		const date = new Date(d);
		if (isNaN(date.getTime()) || date.getFullYear() <= 1753) return '—';
		return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
	}

	function getDefaultFromDate(): string {
		const now = new Date();
		const yyyy = now.getFullYear();
		const mm = String(now.getMonth() + 1).padStart(2, '0');
		return `${yyyy}-${mm}-01`;
	}

	function getDefaultToDate(): string {
		const now = new Date();
		const yyyy = now.getFullYear();
		const mm = String(now.getMonth() + 1).padStart(2, '0');
		const dd = String(now.getDate()).padStart(2, '0');
		return `${yyyy}-${mm}-${dd}`;
	}

	function getDefaultTargetEndDate(): string {
		const now = new Date();
		const nextMonthEnd = new Date(now.getFullYear(), now.getMonth() + 2, 0);
		const yyyy = nextMonthEnd.getFullYear();
		const mm = String(nextMonthEnd.getMonth() + 1).padStart(2, '0');
		const dd = String(nextMonthEnd.getDate()).padStart(2, '0');
		return `${yyyy}-${mm}-${dd}`;
	}

	const GET_RESP_CENTERS_QUERY = `
		query GetRespCenterTargetMultipliers {
			getRespCenterTargetMultipliers {
				code
				name
				targetMultiplier
			}
		}
	`;

	const UPDATE_MULTIPLIER_MUTATION = `
		mutation UpdateRespCenterTargetMultiplier($respCenter: String!, $targetMultiplier: Decimal!) {
			updateRespCenterTargetMultiplier(respCenter: $respCenter, targetMultiplier: $targetMultiplier) {
				success
				message
			}
		}
	`;

	const PREVIEW_TARGETS_QUERY = `
		query PreviewTeamSalesTargets($request: PreviewTeamSalesTargetsRequestInput!) {
			previewTeamSalesTargets(request: $request) {
				success
				message
				respCenter
				respCenterMultiplier
				fromDate
				toDate
				nextTargetEndDate
				totalCurrentSale
				totalNextTarget
				roundingStep
				effectiveGrowthPercent
				items {
					teamCode
					teamName
					respCenter
					currentSale
					targetMultiplier
					nextTarget
					existingTarget
					existingTargetEndDate
					nextTargetEndDate
				}
			}
		}
	`;

	const GENERATE_TARGETS_MUTATION = `
		mutation GenerateTeamSalesTargets($request: GenerateTeamSalesTargetsRequestInput!) {
			generateTeamSalesTargets(request: $request) {
				success
				message
				updatedCount
				totalGeneratedTarget
				roundingStep
				effectiveGrowthPercent
				items {
					teamCode
					teamName
					respCenter
					currentSale
					targetMultiplier
					nextTarget
					existingTarget
					existingTargetEndDate
					nextTargetEndDate
				}
			}
		}
	`;

	let activeAdminTab = $state('sales-targets');

	let respCentersList = $state<RespCenterItem[]>([]);
	let isRespCentersLoading = $state(false);
	let selectedRespCenter = $state('BEL');
	let centerMultiplier = $state<number>(0);
	let isSavingMultiplier = $state(false);

	let fromDate = $state(getDefaultFromDate());
	let toDate = $state(getDefaultToDate());
	let targetEndDate = $state(getDefaultTargetEndDate());
	let saleType = $state('retread-ecomile');
	let roundingStep = $state<number>(50000);

	let isPreviewing = $state(false);
	let previewResult = $state<PreviewResultData | null>(null);

	let teamOverrides = $state<Record<string, number>>({});
	let teamSearch = $state('');
	let filterOnlyWithSales = $state(true);
	let isCommittingTargets = $state(false);

	let effectiveTotalTarget = $derived.by(() => {
		if (!previewResult?.items) return 0;
		return previewResult.items.reduce((sum, item) => {
			const val = teamOverrides[item.teamCode] !== undefined
				? teamOverrides[item.teamCode]
				: item.nextTarget;
			return sum + (Number(val) || 0);
		}, 0);
	});

	let overridesCount = $derived(Object.keys(teamOverrides).length);

	let teamsWithSalesCount = $derived(
		previewResult?.items ? previewResult.items.filter((i) => i.currentSale > 0).length : 0
	);

	let calculatedTotalCurrentSales = $derived(
		previewResult?.items ? previewResult.items.reduce((sum, i) => sum + (i.currentSale || 0), 0) : 0
	);

	let effectiveGrowthPercent = $derived.by(() => {
		if (!calculatedTotalCurrentSales || calculatedTotalCurrentSales <= 0) return 0;
		return Number((((effectiveTotalTarget / calculatedTotalCurrentSales) - 1) * 100).toFixed(2));
	});

	let filteredItems = $derived.by(() => {
		if (!previewResult?.items) return [];
		let list = previewResult.items;
		if (filterOnlyWithSales) {
			list = list.filter((i) => i.currentSale > 0);
		}
		if (teamSearch.trim()) {
			const q = teamSearch.toLowerCase().trim();
			list = list.filter(
				(i) =>
					i.teamCode.toLowerCase().includes(q) ||
					(i.teamName && i.teamName.toLowerCase().includes(q)) ||
					(i.respCenter && i.respCenter.toLowerCase().includes(q))
			);
		}
		return list;
	});

	function handleRespCenterChange(code: string) {
		selectedRespCenter = code;
		if (code === 'ALL') {
			centerMultiplier = 0;
		} else {
			const found = respCentersList.find((c) => c.code === code);
			centerMultiplier = found ? found.targetMultiplier : 0;
		}
	}

	async function saveCenterMultiplier() {
		if (!selectedRespCenter || selectedRespCenter === 'ALL') {
			toast.error('Select an individual responsibility center to configure its target multiplier.');
			return;
		}
		isSavingMultiplier = true;
		try {
			const res = await graphqlMutation<{
				updateRespCenterTargetMultiplier: { success: boolean; message: string };
			}>(UPDATE_MULTIPLIER_MUTATION, {
				variables: {
					respCenter: selectedRespCenter,
					targetMultiplier: Number(centerMultiplier) || 0
				}
			});
			if (res.success && res.data?.updateRespCenterTargetMultiplier?.success) {
				toast.success(res.data.updateRespCenterTargetMultiplier.message || 'Target multiplier updated successfully.');
				respCentersList = respCentersList.map((c) =>
					c.code === selectedRespCenter ? { ...c, targetMultiplier: Number(centerMultiplier) || 0 } : c
				);
				if (previewResult) {
					await runPreview(false);
				}
			} else {
				toast.error(res.data?.updateRespCenterTargetMultiplier?.message || res.error || 'Failed to update multiplier.');
			}
		} catch (e: any) {
			toast.error(e.message || 'Error updating multiplier.');
		} finally {
			isSavingMultiplier = false;
		}
	}

	async function runPreview(showSuccessToast = true) {
		if (isPreviewing) return;
		isPreviewing = true;
		try {
			const res = await graphqlQuery<{
				previewTeamSalesTargets: PreviewResultData;
			}>(PREVIEW_TARGETS_QUERY, {
				variables: {
					request: {
						respCenter: selectedRespCenter === 'ALL' ? null : selectedRespCenter,
						fromDate: fromDate || null,
						toDate: toDate || null,
						targetEndDate: targetEndDate || null,
						saleType: saleType || 'retread-ecomile',
						roundingStep: Number(roundingStep)
					}
				},
				skipCache: true
			});

			if (res.success && res.data?.previewTeamSalesTargets?.success) {
				previewResult = res.data.previewTeamSalesTargets;
				teamOverrides = {};
				if (showSuccessToast) {
					toast.success(`Calculated sales & proposed targets for ${previewResult.items.length} teams.`);
				}
			} else {
				const errMsg = res.data?.previewTeamSalesTargets?.message || res.error || 'Failed to preview targets.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message || 'Error executing target preview.');
		} finally {
			isPreviewing = false;
		}
	}

	function handleTargetOverride(teamCode: string, value: string, defaultNextTarget: number) {
		const num = parseFloat(value);
		if (isNaN(num) || num === defaultNextTarget) {
			const updated = { ...teamOverrides };
			delete updated[teamCode];
			teamOverrides = updated;
		} else {
			teamOverrides = {
				...teamOverrides,
				[teamCode]: num
			};
		}
	}

	function revertOverride(teamCode: string) {
		const updated = { ...teamOverrides };
		delete updated[teamCode];
		teamOverrides = updated;
	}

	function resetAllOverrides() {
		teamOverrides = {};
		toast.info('Reverted all manual overrides to calculated targets.');
	}

	async function commitTargets() {
		if (!previewResult || isCommittingTargets) return;

		const totalCount = previewResult.items.length;
		const withSales = teamsWithSalesCount;
		const overridesCountVal = overridesCount;

		const centerLabel = selectedRespCenter === 'ALL' ? 'All Responsibility Centers' : selectedRespCenter;
		const roundingLabel =
			roundingStep === 50000 ? 'Nearest 0.50 L (₹50,000)' :
			roundingStep === 25000 ? 'Nearest 0.25 L (₹25,000)' :
			roundingStep === 10000 ? 'Nearest 0.10 L (₹10,000)' :
			roundingStep === 100000 ? 'Nearest 1.00 L (₹1,00,000)' :
			roundingStep === -1 ? 'Smart Adaptive (0.5L / 0.25L / 5k)' : 'Exact / Raw';

		const confirmMsg =
			`Are you sure you want to commit next month sales targets to Dynamics NAV?\n\n` +
			`• Center: ${centerLabel}\n` +
			`• Target End Date: ${targetEndDate}\n` +
			`• Rounding Strategy: ${roundingLabel}\n` +
			`• Total Teams: ${totalCount} (${withSales} with active sales)\n` +
			`• Total Proposed Target: ${formatINR(effectiveTotalTarget)} (${formatLakhs(effectiveTotalTarget)})\n` +
			`• Stretch Growth: +${effectiveGrowthPercent.toFixed(1)}% over current sales\n` +
			(overridesCountVal > 0 ? `• Manual Overrides: ${overridesCountVal} teams\n` : '') +
			`\nThis will update [Tyresoles (India) Pvt_ Ltd_$Team] in Dynamics NAV.`;

		if (!confirm(confirmMsg)) return;

		isCommittingTargets = true;
		try {
			const overridesList = Object.entries(teamOverrides).map(([teamCode, target]) => ({
				teamCode,
				target: Number(target) || 0
			}));

			const res = await graphqlMutation<{
				generateTeamSalesTargets: {
					success: boolean;
					message: string;
					updatedCount: number;
					totalGeneratedTarget: number;
					items: TeamTargetItem[];
				};
			}>(GENERATE_TARGETS_MUTATION, {
				variables: {
					request: {
						respCenter: selectedRespCenter === 'ALL' ? null : selectedRespCenter,
						fromDate: fromDate || null,
						toDate: toDate || null,
						targetEndDate: targetEndDate || null,
						saleType: saleType || 'retread-ecomile',
						roundingStep: Number(roundingStep),
						targetOverrides: overridesList.length > 0 ? overridesList : null
					}
				}
			});

			if (res.success && res.data?.generateTeamSalesTargets?.success) {
				const r = res.data.generateTeamSalesTargets;
				toast.success(r.message || `Saved targets for ${r.updatedCount} teams to Dynamics NAV!`);
				teamOverrides = {};
				await runPreview(false);
			} else {
				const errMsg = res.data?.generateTeamSalesTargets?.message || res.error || 'Failed to commit targets.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message || 'Error committing targets.');
		} finally {
			isCommittingTargets = false;
		}
	}

	async function loadRespCenters() {
		isRespCentersLoading = true;
		try {
			const res = await graphqlQuery<{
				getRespCenterTargetMultipliers: RespCenterItem[];
			}>(GET_RESP_CENTERS_QUERY, { skipCache: true });

			if (res.success && res.data?.getRespCenterTargetMultipliers) {
				respCentersList = res.data.getRespCenterTargetMultipliers;
				const defCenter = respCentersList.find((c) => c.code === 'BEL') || respCentersList[0];
				if (defCenter) {
					selectedRespCenter = defCenter.code;
					centerMultiplier = defCenter.targetMultiplier;
				}
			}
		} catch (e) {
			console.error('Failed to load responsibility centers', e);
		} finally {
			isRespCentersLoading = false;
		}
	}

	// ==========================================
	// Existing Admin Tools Mutations & State
	// ==========================================
	let isLoading = $state(false);
	let lastRunResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	let isImporting = $state(false);
	let lastImportResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	let isRectifying = $state(false);
	let lastRectifyResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	let isWipingCrm = $state(false);
	let lastWipeCrmResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	const SANITIZE_MUTATION = `
		mutation SanitizeMobileNumbers {
			sanitizeSalesInvoiceHeaderMobileNumbers {
				success
				message
			}
		}
	`;

	const IMPORT_CRM_MUTATION = `
		mutation ImportCrmContacts {
			importCrmContactsFromInvoices {
				success
				message
			}
		}
	`;

	const RECTIFY_LEDGERS_MUTATION = `
		mutation RectifyCustLedgers {
			rectifyCustLedgers {
				success
				message
			}
		}
	`;

	const WIPE_CRM_MUTATION = `
		mutation WipeCrmCallingRecordsTemporary {
			wipeCrmCallingRecordsTemporary {
				success
				message
			}
		}
	`;

	async function runSanitizer() {
		if (isLoading) return;
		isLoading = true;
		toast.info('Starting mobile number sanitation run...');

		try {
			const res = await graphqlMutation<{
				sanitizeSalesInvoiceHeaderMobileNumbers: { success: boolean; message: string };
			}>(SANITIZE_MUTATION);

			if (res.success && res.data?.sanitizeSalesInvoiceHeaderMobileNumbers?.success) {
				const msg = res.data.sanitizeSalesInvoiceHeaderMobileNumbers.message || 'Sanitation completed successfully.';
				toast.success(msg);
				lastRunResult = { success: true, message: msg, timestamp: new Date() };
			} else {
				const errorMsg = res.data?.sanitizeSalesInvoiceHeaderMobileNumbers?.message || res.error || 'Sanitation execution failed.';
				toast.error(errorMsg);
				lastRunResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during sanitation.';
			toast.error(errorMsg);
			lastRunResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isLoading = false;
		}
	}

	async function runCrmImport() {
		if (isImporting) return;
		isImporting = true;
		toast.info('Starting CRM contacts import from invoices...');

		try {
			const res = await graphqlMutation<{
				importCrmContactsFromInvoices: { success: boolean; message: string };
			}>(IMPORT_CRM_MUTATION);

			if (res.success && res.data?.importCrmContactsFromInvoices?.success) {
				const msg = res.data.importCrmContactsFromInvoices.message || 'Import completed successfully.';
				toast.success(msg);
				lastImportResult = { success: true, message: msg, timestamp: new Date() };
				await fetchStats();
			} else {
				const errorMsg = res.data?.importCrmContactsFromInvoices?.message || res.error || 'Import failed.';
				toast.error(errorMsg);
				lastImportResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during import.';
			toast.error(errorMsg);
			lastImportResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isImporting = false;
		}
	}

	async function runRectifyLedgers() {
		if (isRectifying) return;
		isRectifying = true;
		toast.info('Starting customer ledger rectification...');

		try {
			const res = await graphqlMutation<{
				rectifyCustLedgers: { success: boolean; message: string };
			}>(RECTIFY_LEDGERS_MUTATION);

			if (res.success && res.data?.rectifyCustLedgers?.success) {
				const msg = res.data.rectifyCustLedgers.message || 'Rectification completed successfully.';
				toast.success(msg);
				lastRectifyResult = { success: true, message: msg, timestamp: new Date() };
			} else {
				const errorMsg = res.data?.rectifyCustLedgers?.message || res.error || 'Rectification failed.';
				toast.error(errorMsg);
				lastRectifyResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during rectification.';
			toast.error(errorMsg);
			lastRectifyResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isRectifying = false;
		}
	}

	async function runWipeCrm() {
		if (isWipingCrm) return;
		
		const confirmed = confirm("CAUTION: This will permanently wipe all CRM calling allocations, logs, and reminders. Are you absolutely sure you want to proceed?");
		if (!confirmed) return;

		isWipingCrm = true;
		toast.info('Starting CRM calling records wipe...');

		try {
			const res = await graphqlMutation<{
				wipeCrmCallingRecordsTemporary: { success: boolean; message: string };
			}>(WIPE_CRM_MUTATION);

			if (res.success && res.data?.wipeCrmCallingRecordsTemporary?.success) {
				const msg = res.data.wipeCrmCallingRecordsTemporary.message || 'Wipe completed successfully.';
				toast.success(msg);
				lastWipeCrmResult = { success: true, message: msg, timestamp: new Date() };
				await fetchStats();
			} else {
				const errorMsg = res.data?.wipeCrmCallingRecordsTemporary?.message || res.error || 'Wipe failed.';
				toast.error(errorMsg);
				lastWipeCrmResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during wipe.';
			toast.error(errorMsg);
			lastWipeCrmResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isWipingCrm = false;
		}
	}

	// ==========================================
	// Contact Sanitization & Tag Enrichment State
	// ==========================================
	interface SanitizationStats {
		totalContacts: number;
		unalignedStateCount: number;
		alignedStateCount: number;
		tyresolesTagCount: number;
		cleanTagsCount: number;
		totalWebLinkCount: number;
		pendingWebEnrichmentCount: number;
		enrichedWebCount: number;
		recentEnrichedSamples: Array<{
			id: string;
			fullName: string;
			state?: string;
			tags?: string;
			sourceUrl?: string;
			modifiedAt?: string;
		}>;
	}

	interface EnrichmentItem {
		id: string;
		fullName: string;
		state?: string;
		oldTags?: string;
		newTags?: string;
		sourceUrl?: string;
		featuresFound: string[];
		success: boolean;
		error?: string;
	}

	interface BatchResult {
		success: boolean;
		message: string;
		processedCount: number;
		scrapedSuccessCount: number;
		scrapedFailedCount: number;
		totalFeaturesAdded: number;
		remainingPendingCount: number;
		processedItems: EnrichmentItem[];
	}

	let stats = $state<SanitizationStats | null>(null);
	let isStatsLoading = $state(false);

	let isAligningStates = $state(false);
	let isCleaningTags = $state(false);

	let batchCount = $state(50);
	let isBatchEnriching = $state(false);
	let isAutoRunning = $state(false);
	let autoRunTimer: any = null;

	let lastBatchResult = $state<BatchResult | null>(null);

	const STATS_QUERY = `
		query GetSanitizationStats {
			crmContactSanitizationStats {
				totalContacts
				unalignedStateCount
				alignedStateCount
				tyresolesTagCount
				cleanTagsCount
				totalWebLinkCount
				pendingWebEnrichmentCount
				enrichedWebCount
				recentEnrichedSamples {
					id
					fullName
					state
					tags
					sourceUrl
					modifiedAt
				}
			}
		}
	`;

	const ALIGN_STATES_MUTATION = `
		mutation AlignStateCodes($limit: Int) {
			alignCrmContactStateCodes(limit: $limit) {
				success
				message
			}
		}
	`;

	const CLEAN_TAGS_MUTATION = `
		mutation CleanTags($limit: Int) {
			cleanCrmContactTags(limit: $limit) {
				success
				message
			}
		}
	`;

	const ENRICH_BATCH_MUTATION = `
		mutation EnrichTagsFromWeb($batchSize: Int!) {
			enrichCrmContactTagsFromWeb(batchSize: $batchSize) {
				success
				message
				processedCount
				scrapedSuccessCount
				scrapedFailedCount
				totalFeaturesAdded
				remainingPendingCount
				processedItems {
					id
					fullName
					state
					oldTags
					newTags
					sourceUrl
					featuresFound
					success
					error
				}
			}
		}
	`;

	async function fetchStats() {
		isStatsLoading = true;
		try {
			const res = await graphqlQuery<{ crmContactSanitizationStats: SanitizationStats }>(STATS_QUERY);
			if (res.success && res.data?.crmContactSanitizationStats) {
				stats = res.data.crmContactSanitizationStats;
			}
		} catch (err: any) {
			console.error("Failed to load sanitization stats:", err);
		} finally {
			isStatsLoading = false;
		}
	}

	async function runAlignStates(limit?: number) {
		if (isAligningStates) return;
		isAligningStates = true;
		const label = limit ? `next ${limit}` : 'all';
		toast.info(`Aligning state codes for ${label} contacts...`);

		try {
			const res = await graphqlMutation<{ alignCrmContactStateCodes: { success: boolean; message: string } }>(
				ALIGN_STATES_MUTATION,
				{ variables: { limit: limit ?? null } }
			);
			if (res.success && res.data?.alignCrmContactStateCodes?.success) {
				toast.success(res.data.alignCrmContactStateCodes.message);
				await fetchStats();
			} else {
				toast.error(res.data?.alignCrmContactStateCodes?.message || res.error || 'Failed to align state codes.');
			}
		} catch (err: any) {
			toast.error(err.message || 'Error occurred while aligning states.');
		} finally {
			isAligningStates = false;
		}
	}

	async function runCleanTags(limit?: number) {
		if (isCleaningTags) return;
		isCleaningTags = true;
		const label = limit ? `next ${limit}` : 'all';
		toast.info(`Cleaning unwanted 'Tyresoles' tag for ${label} contacts...`);

		try {
			const res = await graphqlMutation<{ cleanCrmContactTags: { success: boolean; message: string } }>(
				CLEAN_TAGS_MUTATION,
				{ variables: { limit: limit ?? null } }
			);
			if (res.success && res.data?.cleanCrmContactTags?.success) {
				toast.success(res.data.cleanCrmContactTags.message);
				await fetchStats();
			} else {
				toast.error(res.data?.cleanCrmContactTags?.message || res.error || 'Failed to clean tags.');
			}
		} catch (err: any) {
			toast.error(err.message || 'Error occurred while cleaning tags.');
		} finally {
			isCleaningTags = false;
		}
	}

	async function runBatchEnrichment(): Promise<boolean> {
		if (isBatchEnriching) return false;
		isBatchEnriching = true;
		const count = Math.max(1, Math.min(500, Number(batchCount) || 50));

		try {
			const res = await graphqlMutation<{ enrichCrmContactTagsFromWeb: BatchResult }>(
				ENRICH_BATCH_MUTATION,
				{ variables: { batchSize: count } }
			);

			if (res.success && res.data?.enrichCrmContactTagsFromWeb?.success) {
				lastBatchResult = res.data.enrichCrmContactTagsFromWeb;
				toast.success(lastBatchResult.message);
				await fetchStats();

				// Check if there are still items to process
				if (lastBatchResult.remainingPendingCount <= 0 || lastBatchResult.processedCount === 0) {
					stopAutoRun();
					toast.info('All pending contacts have been enriched!');
					return false;
				}
				return true;
			} else {
				const errMsg = res.data?.enrichCrmContactTagsFromWeb?.message || res.error || 'Batch enrichment failed.';
				toast.error(errMsg);
				stopAutoRun();
				return false;
			}
		} catch (err: any) {
			toast.error(err.message || 'Error during batch web enrichment.');
			stopAutoRun();
			return false;
		} finally {
			isBatchEnriching = false;
		}
	}

	function startAutoRun() {
		if (isAutoRunning) return;
		isAutoRunning = true;
		toast.info('Starting continuous auto-batching...');
		stepAutoRun();
	}

	function stopAutoRun() {
		isAutoRunning = false;
		if (autoRunTimer) {
			clearTimeout(autoRunTimer);
			autoRunTimer = null;
		}
	}

	async function stepAutoRun() {
		if (!isAutoRunning) return;
		const hasMore = await runBatchEnrichment();
		if (hasMore && isAutoRunning) {
			autoRunTimer = setTimeout(() => {
				stepAutoRun();
			}, 1200);
		}
	}

	onMount(async () => {
		fetchStats();
		await loadRespCenters();
		await runPreview(false);
	});

	onDestroy(() => {
		stopAutoRun();
	});

	// Progress percentages
	let statePercent = $derived(
		stats && stats.totalContacts > 0
			? Math.min(100, Math.round((stats.alignedStateCount / stats.totalContacts) * 100))
			: 0
	);

	let tagsCleanPercent = $derived(
		stats && stats.totalContacts > 0
			? Math.min(100, Math.round(((stats.totalContacts - stats.tyresolesTagCount) / stats.totalContacts) * 100))
			: 0
	);

	let webEnrichPercent = $derived(
		stats && stats.totalWebLinkCount > 0
			? Math.min(100, Math.round((stats.enrichedWebCount / stats.totalWebLinkCount) * 100))
			: 0
	);
</script>

<svelte:head>
	<title>Admin Tools | Tyresoles</title>
</svelte:head>

<div class="h-full flex flex-col gap-8 max-w-7xl mx-auto py-6 px-4 sm:px-6">
	<!-- Page Header -->
	<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
		<div>
			<h1 class="text-2xl font-bold tracking-tight text-foreground flex items-center gap-2">
				<Icon name="shield-alert" class="size-6 text-primary" />
				System Administration Tools
			</h1>
			<p class="text-sm text-muted-foreground mt-1">
				Team sales targets automation, database hygiene, web scraping enrichment, and system maintenance utilities.
			</p>
		</div>
		<div class="flex items-center gap-2">
			{#if activeAdminTab === 'sales-targets'}
				<Button
					variant="outline"
					size="sm"
					onclick={() => runPreview(true)}
					disabled={isPreviewing}
					class="gap-1.5 shadow-sm"
				>
					<Icon name="refresh-cw" class="size-3.5 {isPreviewing ? 'animate-spin' : ''}" />
					<span>Recalculate Preview</span>
				</Button>
			{:else}
				<Button
					variant="outline"
					size="sm"
					onclick={fetchStats}
					disabled={isStatsLoading}
					class="gap-1.5 shadow-sm"
				>
					<Icon name="refresh-cw" class="size-3.5 {isStatsLoading ? 'animate-spin' : ''}" />
					<span>Refresh Stats</span>
				</Button>
			{/if}
		</div>
	</div>

	<!-- Main Administration Tabs -->
	<Tabs.Root bind:value={activeAdminTab} class="w-full space-y-6">
		<Tabs.List class="grid grid-cols-1 sm:grid-cols-3 max-w-xl bg-muted/60 p-1 rounded-xl border border-border/50">
			<Tabs.Trigger value="sales-targets" class="gap-2 text-xs font-medium">
				<Icon name="target" class="size-4" />
				<span>Team Sales Targets</span>
			</Tabs.Trigger>
			<Tabs.Trigger value="contact-enrichment" class="gap-2 text-xs font-medium">
				<Icon name="tags" class="size-4" />
				<span>CRM Tag Enrichment</span>
			</Tabs.Trigger>
			<Tabs.Trigger value="system-utilities" class="gap-2 text-xs font-medium">
				<Icon name="cpu" class="size-4" />
				<span>System Utilities</span>
			</Tabs.Trigger>
		</Tabs.List>

		<!-- ============================================================== -->
		<!-- TAB 1: AUTOMATIC TEAM SALES TARGET GENERATOR -->
		<!-- ============================================================== -->
		<Tabs.Content value="sales-targets" class="space-y-6 mt-0">
			<!-- Parameters & Configuration Card -->
			<Card class="border-border/70 shadow-sm">
				<CardHeader class="pb-3 border-b bg-muted/20">
					<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
						<div class="flex items-center gap-2.5">
							<div class="p-2 rounded-lg bg-primary/10 text-primary">
								<Icon name="target" class="size-5" />
							</div>
							<div>
								<CardTitle class="text-base font-semibold">Automatic Team Sales Target Generator</CardTitle>
								<CardDescription class="text-xs">
									Calculate current period sales per team using actual customer ledger entries (<code class="text-primary font-mono">GetSalesAndBalanceAsync</code>) and project next month's targets.
								</CardDescription>
							</div>
						</div>
						<Badge variant="outline" class="font-mono text-xs w-fit">
							Formula: Current Sale × Multiplier [if 0 then 1]
						</Badge>
					</div>
				</CardHeader>

				<CardContent class="pt-5 space-y-4">
					<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
						<!-- Responsibility Center Selector -->
						<div class="space-y-1.5">
							<label for="resp-center-select" class="text-xs font-medium text-foreground flex items-center gap-1.5">
								<Icon name="building-2" class="size-3.5 text-muted-foreground" />
								Responsibility Center
							</label>
							<select
								id="resp-center-select"
								class="w-full h-9 px-3 text-xs rounded-md border border-input bg-background font-medium focus:outline-none focus:ring-1 focus:ring-ring"
								value={selectedRespCenter}
								onchange={(e) => handleRespCenterChange(e.currentTarget.value)}
								disabled={isRespCentersLoading || isPreviewing}
							>
								<option value="ALL">ALL — All Centers Combined</option>
								{#each respCentersList as rc}
									<option value={rc.code}>
										{rc.code} — {rc.name}
									</option>
								{/each}
							</select>
							<p class="text-[11px] text-muted-foreground">Filters teams belonging to this factory / office</p>
						</div>

						<!-- Target Multiplier Configuration -->
						<div class="space-y-1.5">
							<div class="flex items-center justify-between">
								<label for="multiplier-input" class="text-xs font-medium text-foreground flex items-center gap-1.5">
									<Icon name="trending-up" class="size-3.5 text-muted-foreground" />
									Target Multiplier
								</label>
								{#if centerMultiplier == 0 || centerMultiplier == 1}
									<Badge variant="outline" class="text-[10px] px-1.5 py-0">1.00x (Flat)</Badge>
								{:else if centerMultiplier > 1}
									<Badge variant="secondary" class="text-[10px] px-1.5 py-0 bg-emerald-500/10 text-emerald-600 border-emerald-500/20">
										+{((centerMultiplier - 1) * 100).toFixed(1)}% Growth
									</Badge>
								{:else}
									<Badge variant="outline" class="text-[10px] px-1.5 py-0 text-amber-600">
										-{((1 - centerMultiplier) * 100).toFixed(1)}%
									</Badge>
								{/if}
							</div>
							<div class="flex items-center gap-1.5">
								<Input
									id="multiplier-input"
									type="number"
									step="0.01"
									min="0"
									max="10"
									bind:value={centerMultiplier}
									disabled={selectedRespCenter === 'ALL' || isSavingMultiplier}
									class="h-9 font-mono text-center text-xs"
									placeholder="1.00"
								/>
								<Button
									variant="outline"
									size="sm"
									class="h-9 px-2.5 gap-1 text-xs shrink-0"
									onclick={saveCenterMultiplier}
									disabled={selectedRespCenter === 'ALL' || isSavingMultiplier}
									title="Save Multiplier to NAV Responsibility Center"
								>
									{#if isSavingMultiplier}
										<Icon name="loader-2" class="size-3.5 animate-spin" />
									{:else}
										<Icon name="save" class="size-3.5" />
									{/if}
									<span class="hidden sm:inline">Save</span>
								</Button>
							</div>
							<p class="text-[11px] text-muted-foreground">
								{selectedRespCenter === 'ALL' ? 'Center-specific multiplier (disabled for ALL)' : `Saved on ${selectedRespCenter} in NAV`}
							</p>
						</div>

						<!-- Sales Calculation Date Range -->
						<div class="space-y-1.5">
							<label for="from-date" class="text-xs font-medium text-foreground flex items-center gap-1.5">
								<Icon name="calendar" class="size-3.5 text-muted-foreground" />
								Current Sales Period
							</label>
							<div class="grid grid-cols-2 gap-1.5">
								<Input
									id="from-date"
									type="date"
									bind:value={fromDate}
									class="h-9 text-xs px-2"
									title="From Date"
								/>
								<Input
									type="date"
									bind:value={toDate}
									class="h-9 text-xs px-2"
									title="To Date"
								/>
							</div>
							<p class="text-[11px] text-muted-foreground">Date range for ledger sales calculation</p>
						</div>

						<!-- Next Target End Date & Sale Type -->
						<div class="space-y-1.5">
							<label for="target-end-date" class="text-xs font-medium text-foreground flex items-center gap-1.5">
								<Icon name="calendar-check" class="size-3.5 text-muted-foreground" />
								Target End Date
							</label>
							<Input
								id="target-end-date"
								type="date"
								bind:value={targetEndDate}
								class="h-9 text-xs"
								title="Target End Date"
							/>
							<p class="text-[11px] text-muted-foreground">End date recorded on NAV Team target</p>
						</div>
					</div>

					<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-3 border-t">
						<div class="flex flex-wrap items-center gap-4">
							<div class="flex items-center gap-1.5">
								<span class="text-xs text-muted-foreground font-medium">Sale Category:</span>
								<select
									bind:value={saleType}
									class="h-8 px-2.5 text-xs rounded-md border border-input bg-background focus:outline-none"
								>
									<option value="retread-ecomile">Retreading & Ecomile (Standard)</option>
									<option value="trade-other">New Tyres & Trade</option>
									<option value="all">All Product Categories</option>
								</select>
							</div>

							<div class="flex items-center gap-1.5">
								<span class="text-xs text-muted-foreground font-medium flex items-center gap-1">
									<Icon name="sparkles" class="size-3.5 text-amber-500" />
									Target Rounding:
								</span>
								<select
									bind:value={roundingStep}
									onchange={() => { if (previewResult) runPreview(false); }}
									class="h-8 px-2.5 text-xs rounded-md border border-input bg-background font-medium focus:outline-none focus:ring-1 focus:ring-ring"
								>
									<option value={50000}>Nearest 0.50 Lakhs (₹50,000) — Best Fit</option>
									<option value={25000}>Nearest 0.25 Lakhs (₹25,000)</option>
									<option value={10000}>Nearest 0.10 Lakhs (₹10,000)</option>
									<option value={100000}>Nearest 1.00 Lakh (₹1,00,000)</option>
									<option value={-1}>Smart Adaptive (0.5L / 0.25L / 5k)</option>
									<option value={0}>Exact Raw (No Rounding)</option>
								</select>
							</div>
						</div>

						<div class="flex items-center gap-2">
							<Button
								variant="default"
								size="sm"
								onclick={() => runPreview(true)}
								disabled={isPreviewing}
								class="gap-1.5 shadow-sm"
							>
								{#if isPreviewing}
									<Icon name="loader-2" class="size-4 animate-spin" />
									<span>Calculating Ledgers...</span>
								{:else}
									<Icon name="calculator" class="size-4" />
									<span>Calculate & Preview Targets</span>
								{/if}
							</Button>
						</div>
					</div>
				</CardContent>
			</Card>

			<!-- Preview KPI Metrics (Rendered when preview is available) -->
			{#if previewResult}
				<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
					<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
						<CardHeader class="pb-2">
							<div class="flex items-center justify-between">
								<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
									Teams Calculated
								</CardDescription>
								<Badge variant="outline" class="text-[11px] font-mono">
									{teamsWithSalesCount} Active
								</Badge>
							</div>
							<CardTitle class="text-2xl font-bold tracking-tight text-foreground flex items-baseline gap-2">
								<span>{previewResult.items.length}</span>
								<span class="text-xs font-normal text-muted-foreground">teams total</span>
							</CardTitle>
						</CardHeader>
						<CardContent class="text-xs text-muted-foreground pt-0">
							{teamsWithSalesCount} teams with sales &gt; ₹0
						</CardContent>
					</Card>

					<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
						<CardHeader class="pb-2">
							<div class="flex items-center justify-between">
								<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
									Current Period Sales
								</CardDescription>
								<span class="text-[11px] font-mono font-medium text-muted-foreground">
									{formatLakhs(calculatedTotalCurrentSales)}
								</span>
							</div>
							<CardTitle class="text-2xl font-bold tracking-tight text-foreground font-mono">
								{formatINR(calculatedTotalCurrentSales)}
							</CardTitle>
						</CardHeader>
						<CardContent class="text-xs text-muted-foreground pt-0">
							Period: {formatDate(fromDate)} – {formatDate(toDate)}
						</CardContent>
					</Card>

					<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
						<CardHeader class="pb-2">
							<div class="flex items-center justify-between">
								<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
									Applied Multiplier
								</CardDescription>
								<Badge variant="outline" class="text-[11px] font-mono">
									{previewResult.respCenter || 'ALL'}
								</Badge>
							</div>
							<CardTitle class="text-2xl font-bold tracking-tight text-foreground flex items-baseline gap-2">
								<span>{previewResult.respCenterMultiplier > 0 ? `${previewResult.respCenterMultiplier.toFixed(2)}x` : '1.00x'}</span>
								{#if effectiveGrowthPercent > 0}
									<Badge variant="secondary" class="text-xs font-mono font-semibold bg-emerald-500/10 text-emerald-600 border-emerald-500/20">
										+{effectiveGrowthPercent.toFixed(1)}% Stretch
									</Badge>
								{/if}
							</CardTitle>
						</CardHeader>
						<CardContent class="text-xs text-muted-foreground pt-0">
							{previewResult.respCenterMultiplier > 1
								? `Base +${((previewResult.respCenterMultiplier - 1) * 100).toFixed(1)}% growth (Ceiled to ${roundingStep === 50000 ? '0.50L' : roundingStep === 25000 ? '0.25L' : roundingStep === 10000 ? '0.10L' : roundingStep === 100000 ? '1.00L' : roundingStep === -1 ? 'Adaptive' : 'Raw'})`
								: 'Flat growth on current sales'}
						</CardContent>
					</Card>

					<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden border-primary/30 bg-primary/[0.02]">
						<CardHeader class="pb-2">
							<div class="flex items-center justify-between">
								<CardDescription class="text-xs font-medium uppercase tracking-wider text-primary">
									Total Proposed Target
								</CardDescription>
								{#if overridesCount > 0}
									<Badge variant="default" class="bg-amber-500 hover:bg-amber-600 text-[10px] px-1.5 py-0">
										{overridesCount} Overridden
									</Badge>
								{:else}
									<span class="text-xs font-semibold px-1.5 py-0.5 rounded bg-emerald-500/10 text-emerald-600 font-mono">
										{formatLakhs(effectiveTotalTarget)}
									</span>
								{/if}
							</div>
							<CardTitle class="text-2xl font-bold tracking-tight text-foreground font-mono">
								{formatINR(effectiveTotalTarget)}
							</CardTitle>
						</CardHeader>
						<CardContent class="text-xs text-muted-foreground pt-0 flex items-center justify-between">
							<span>End: <strong class="text-foreground">{formatDate(targetEndDate)}</strong></span>
							{#if effectiveTotalTarget > calculatedTotalCurrentSales}
								<span class="text-emerald-600 font-medium font-mono text-[11px]">
									+{formatLakhs(effectiveTotalTarget - calculatedTotalCurrentSales)} (+{effectiveGrowthPercent.toFixed(1)}%)
								</span>
							{/if}
						</CardContent>
					</Card>
				</div>

				<!-- Interactive Teams Target Table -->
				<Card class="border-border/70 shadow-sm">
					<CardHeader class="pb-3 border-b bg-muted/20">
						<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
							<div class="flex items-center gap-2">
								<div class="relative w-64">
									<Icon name="search" class="size-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
									<Input
										type="text"
										placeholder="Search team code or name..."
										bind:value={teamSearch}
										class="h-8 pl-8 text-xs"
									/>
								</div>
								<label class="flex items-center gap-1.5 text-xs text-muted-foreground cursor-pointer select-none">
									<input
										type="checkbox"
										bind:checked={filterOnlyWithSales}
										class="rounded border-input text-primary focus:ring-primary size-3.5"
									/>
									<span>Only with sales ({teamsWithSalesCount})</span>
								</label>
							</div>

							<div class="flex items-center gap-2">
								{#if overridesCount > 0}
									<Button
										variant="ghost"
										size="sm"
										onclick={resetAllOverrides}
										class="gap-1.5 text-xs text-amber-600 hover:text-amber-700 h-8"
									>
										<Icon name="rotate-ccw" class="size-3.5" />
										<span>Reset {overridesCount} Overrides</span>
									</Button>
								{/if}
								<Button
									variant="default"
									size="sm"
									onclick={commitTargets}
									disabled={isCommittingTargets || previewResult.items.length === 0}
									class="gap-1.5 shadow-sm h-8 bg-emerald-600 hover:bg-emerald-700 text-white"
								>
									{#if isCommittingTargets}
										<Icon name="loader-2" class="size-3.5 animate-spin" />
										<span>Saving to NAV...</span>
									{:else}
										<Icon name="circle-check" class="size-3.5" />
										<span>Commit & Save Targets to Dynamics NAV</span>
									{/if}
								</Button>
							</div>
						</div>
					</CardHeader>

					<CardContent class="p-0">
						<div class="max-h-[520px] overflow-auto border-b">
							<Table>
								<TableHeader class="sticky top-0 bg-muted/95 backdrop-blur z-10 border-b">
									<TableRow>
										<TableHead class="w-[110px]">Team Code</TableHead>
										<TableHead>Team Name</TableHead>
										<TableHead class="w-[70px] text-center">Center</TableHead>
										<TableHead class="text-right">Current Sale (₹)</TableHead>
										<TableHead class="w-[85px] text-center">Multiplier</TableHead>
										<TableHead class="text-right min-w-[200px]">Proposed Target (₹)</TableHead>
										<TableHead class="text-right">NAV Target (₹)</TableHead>
										<TableHead class="text-center w-[110px]">NAV Target End</TableHead>
										<TableHead class="text-right w-[95px]">Growth</TableHead>
									</TableRow>
								</TableHeader>
								<TableBody>
									{#if filteredItems.length === 0}
										<TableRow>
											<TableCell colspan={9} class="h-32 text-center text-muted-foreground text-xs">
												No teams match the current filters.
											</TableCell>
										</TableRow>
									{:else}
										{#each filteredItems as item}
											{@const isOverridden = teamOverrides[item.teamCode] !== undefined}
											{@const proposedValue = isOverridden ? teamOverrides[item.teamCode] : item.nextTarget}
											{@const growthPct = item.currentSale > 0 ? ((proposedValue / item.currentSale) - 1) * 100 : 0}
											<TableRow class="hover:bg-muted/40 text-xs">
												<TableCell class="font-mono font-semibold">
													{item.teamCode}
												</TableCell>
												<TableCell class="font-medium text-foreground max-w-xs truncate" title={item.teamName}>
													{item.teamName || '—'}
												</TableCell>
												<TableCell class="text-center">
													<Badge variant="outline" class="font-mono text-[10px] px-1.5 py-0">
														{item.respCenter || '—'}
													</Badge>
												</TableCell>
												<TableCell class="text-right font-mono">
													<div>{formatINR(item.currentSale)}</div>
													{#if item.currentSale > 0}
														<div class="text-[10px] text-muted-foreground font-sans">
															{formatShortLakhs(item.currentSale)}
														</div>
													{/if}
												</TableCell>
												<TableCell class="text-center font-mono text-[11px] text-muted-foreground">
													{item.targetMultiplier > 0 ? `${item.targetMultiplier.toFixed(2)}x` : '1.00x'}
												</TableCell>
												<TableCell class="text-right">
													<div class="flex items-center gap-1.5 justify-end">
														{#if proposedValue > 0}
															<span class="text-[10px] font-mono font-semibold px-1.5 py-0.5 rounded bg-muted/80 text-foreground shrink-0" title={`${(proposedValue / 100000).toFixed(2)} Lakhs`}>
																{formatShortLakhs(proposedValue)}
															</span>
														{/if}
														<Input
															type="number"
															step={roundingStep > 0 ? roundingStep : 1000}
															min="0"
															value={isOverridden ? teamOverrides[item.teamCode] : Math.round(item.nextTarget)}
															oninput={(e) => handleTargetOverride(item.teamCode, e.currentTarget.value, Math.round(item.nextTarget))}
															class="w-32 h-7 text-right font-mono text-xs px-2 {isOverridden ? 'border-amber-500 bg-amber-500/10 font-bold text-amber-800 dark:text-amber-300' : ''}"
														/>
														{#if isOverridden}
															<button
																type="button"
																title="Revert to calculated"
																onclick={() => revertOverride(item.teamCode)}
																class="text-muted-foreground hover:text-amber-600 p-0.5 rounded"
															>
																<Icon name="rotate-ccw" class="size-3" />
															</button>
														{/if}
													</div>
												</TableCell>
												<TableCell class="text-right font-mono text-muted-foreground">
													<div>{formatINR(item.existingTarget)}</div>
													{#if item.existingTarget > 0}
														<div class="text-[10px] text-muted-foreground font-sans">
															{formatShortLakhs(item.existingTarget)}
														</div>
													{/if}
												</TableCell>
												<TableCell class="text-center font-mono text-[11px] text-muted-foreground">
													{formatDate(item.existingTargetEndDate)}
												</TableCell>
												<TableCell class="text-right">
													{#if isOverridden}
														<Badge variant="default" class="bg-amber-500 hover:bg-amber-600 text-[10px] px-1.5 py-0 font-mono">
															{growthPct >= 0 ? '+' : ''}{growthPct.toFixed(1)}% (Mod)
														</Badge>
													{:else if item.currentSale > 0 && growthPct > 0}
														<Badge variant="secondary" class="bg-emerald-500/10 text-emerald-600 border-emerald-500/20 text-[10px] px-1.5 py-0 font-mono">
															+{growthPct.toFixed(1)}%
														</Badge>
													{:else if item.currentSale > 0}
														<Badge variant="outline" class="text-[10px] px-1.5 py-0 text-muted-foreground">
															Flat
														</Badge>
													{:else}
														<span class="text-muted-foreground text-[11px]">—</span>
													{/if}
												</TableCell>
											</TableRow>
										{/each}
									{/if}
								</TableBody>
							</Table>
						</div>
					</CardContent>

					<!-- Table Summary Footer -->
					<CardFooter class="py-3 px-4 bg-muted/10 flex flex-col sm:flex-row items-center justify-between text-xs gap-2">
						<div class="text-muted-foreground">
							Showing <strong class="text-foreground">{filteredItems.length}</strong> of {previewResult.items.length} teams
							{#if overridesCount > 0}
								<span class="text-amber-600 font-medium ml-1">({overridesCount} custom overrides)</span>
							{/if}
						</div>
						<div class="flex flex-wrap items-center gap-6 font-mono text-xs">
							<div>
								<span class="text-muted-foreground">Total Current Sale:</span>
								<strong class="ml-1 text-foreground">{formatINR(calculatedTotalCurrentSales)}</strong>
								<span class="text-muted-foreground text-[11px] ml-1">({formatLakhs(calculatedTotalCurrentSales)})</span>
							</div>
							<div>
								<span class="text-muted-foreground">Total Proposed Target:</span>
								<strong class="ml-1 text-emerald-600 font-bold">{formatINR(effectiveTotalTarget)}</strong>
								<span class="text-emerald-700 font-semibold text-[11px] ml-1">({formatLakhs(effectiveTotalTarget)})</span>
								<Badge variant="secondary" class="ml-1.5 text-[10px] font-mono bg-emerald-500/10 text-emerald-600 border-emerald-500/20">
									+{effectiveGrowthPercent.toFixed(1)}% Stretch
								</Badge>
							</div>
						</div>
					</CardFooter>
				</Card>

				<!-- Bottom Notice / Action Banner -->
				<div class="p-4 rounded-xl border border-primary/20 bg-primary/[0.03] flex flex-col sm:flex-row sm:items-center justify-between gap-4">
					<div class="flex items-start gap-3">
						<div class="p-2 rounded-lg bg-primary/10 text-primary shrink-0 mt-0.5">
							<Icon name="info" class="size-4" />
						</div>
						<div class="space-y-0.5">
							<h4 class="text-xs font-semibold text-foreground">Dynamics NAV Database Synchronization</h4>
							<p class="text-xs text-muted-foreground max-w-2xl">
								Clicking <strong>Commit & Save</strong> writes the proposed targets directly to <code class="font-mono text-primary">[Tyresoles (India) Pvt_ Ltd_$Team].[Target (Sale)]</code> and updates <code class="font-mono text-primary">[Target End Date]</code> to <strong class="text-foreground">{formatDate(targetEndDate)}</strong>.
							</p>
						</div>
					</div>
					<Button
						variant="default"
						onclick={commitTargets}
						disabled={isCommittingTargets || previewResult.items.length === 0}
						class="gap-1.5 shrink-0 bg-emerald-600 hover:bg-emerald-700 text-white shadow-sm"
					>
						{#if isCommittingTargets}
							<Icon name="loader-2" class="size-4 animate-spin" />
							<span>Saving Targets...</span>
						{:else}
							<Icon name="circle-check" class="size-4" />
							<span>Commit & Save Targets</span>
						{/if}
					</Button>
				</div>
			{/if}
		</Tabs.Content>

		<!-- ============================================================== -->
		<!-- TAB 2: CONTACT SANITIZATION & WEB TAG ENRICHMENT TRACKER -->
		<!-- ============================================================== -->
		<Tabs.Content value="contact-enrichment" class="space-y-6 mt-0">
			<div class="space-y-6">
		<div class="flex items-center justify-between border-b pb-3">
			<div class="flex items-center gap-2.5">
				<div class="p-2 rounded-lg bg-primary/10 text-primary">
					<Icon name="tags" class="size-5" />
				</div>
				<div>
					<h2 class="text-lg font-semibold tracking-tight">Contact Sanitization & Web Feature Tag Enrichment</h2>
					<p class="text-xs text-muted-foreground">
						Align state codes (e.g. Karnataka → KA), strip unwanted 'Tyresoles' tags, and scrape commercial features from web links in batches.
					</p>
				</div>
			</div>
			{#if isAutoRunning}
				<Badge variant="default" class="bg-amber-500 hover:bg-amber-600 gap-1.5 animate-pulse px-3 py-1 text-xs">
					<Icon name="loader-2" class="size-3.5 animate-spin" />
					Auto-Processing Next Batches...
				</Badge>
			{/if}
		</div>

		<!-- Overview Metrics Cards -->
		<div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
			<!-- Total Contacts -->
			<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
				<div class="absolute -right-4 -bottom-4 opacity-5 pointer-events-none">
					<Icon name="users" class="size-24" />
				</div>
				<CardHeader class="pb-2">
					<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
						Total CRM Contacts
					</CardDescription>
					<CardTitle class="text-2xl font-bold tracking-tight text-foreground">
						{stats ? stats.totalContacts.toLocaleString() : '...'}
					</CardTitle>
				</CardHeader>
				<CardContent class="text-xs text-muted-foreground pt-0">
					All registered customer & lead records
				</CardContent>
			</Card>

			<!-- State Code Alignment -->
			<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
				<CardHeader class="pb-2">
					<div class="flex items-center justify-between">
						<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
							State Codes Aligned
						</CardDescription>
						<Badge variant="outline" class="text-[11px] font-mono font-medium">
							{statePercent}%
						</Badge>
					</div>
					<CardTitle class="text-2xl font-bold tracking-tight text-foreground flex items-baseline gap-2">
						<span>{stats ? stats.alignedStateCount.toLocaleString() : '...'}</span>
						<span class="text-xs font-normal text-muted-foreground">
							/ {stats ? stats.totalContacts.toLocaleString() : '...'}
						</span>
					</CardTitle>
				</CardHeader>
				<CardContent class="space-y-2 pt-0">
					<Progress value={statePercent} class="h-2" />
					<div class="flex items-center justify-between text-[11px] text-muted-foreground">
						<span>Unaligned (e.g. Karnataka):</span>
						<span class="font-semibold text-amber-500 font-mono">
							{stats ? stats.unalignedStateCount.toLocaleString() : '0'}
						</span>
					</div>
				</CardContent>
			</Card>

			<!-- Tags with 'Tyresoles' -->
			<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
				<CardHeader class="pb-2">
					<div class="flex items-center justify-between">
						<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
							Cleaned Tags
						</CardDescription>
						<Badge variant="outline" class="text-[11px] font-mono font-medium">
							{tagsCleanPercent}%
						</Badge>
					</div>
					<CardTitle class="text-2xl font-bold tracking-tight text-foreground flex items-baseline gap-2">
						<span>{stats ? (stats.totalContacts - stats.tyresolesTagCount).toLocaleString() : '...'}</span>
						<span class="text-xs font-normal text-muted-foreground">clean</span>
					</CardTitle>
				</CardHeader>
				<CardContent class="space-y-2 pt-0">
					<Progress value={tagsCleanPercent} class="h-2" />
					<div class="flex items-center justify-between text-[11px] text-muted-foreground">
						<span>With 'Tyresoles' tag:</span>
						<span class="font-semibold text-destructive font-mono">
							{stats ? stats.tyresolesTagCount.toLocaleString() : '0'}
						</span>
					</div>
				</CardContent>
			</Card>

			<!-- Web Features Enrichment -->
			<Card class="bg-card/70 border-border/70 shadow-sm relative overflow-hidden">
				<CardHeader class="pb-2">
					<div class="flex items-center justify-between">
						<CardDescription class="text-xs font-medium uppercase tracking-wider text-muted-foreground">
							Web Link Features Scraped
						</CardDescription>
						<Badge variant="outline" class="text-[11px] font-mono font-medium">
							{webEnrichPercent}%
						</Badge>
					</div>
					<CardTitle class="text-2xl font-bold tracking-tight text-foreground flex items-baseline gap-2">
						<span>{stats ? stats.enrichedWebCount.toLocaleString() : '...'}</span>
						<span class="text-xs font-normal text-muted-foreground">
							/ {stats ? stats.totalWebLinkCount.toLocaleString() : '...'}
						</span>
					</CardTitle>
				</CardHeader>
				<CardContent class="space-y-2 pt-0">
					<Progress value={webEnrichPercent} class="h-2" />
					<div class="flex items-center justify-between text-[11px] text-muted-foreground">
						<span>Pending TransportFamily:</span>
						<span class="font-semibold text-primary font-mono">
							{stats ? stats.pendingWebEnrichmentCount.toLocaleString() : '0'}
						</span>
					</div>
				</CardContent>
			</Card>
		</div>

		<!-- Interactive Batch Control Console -->
		<Card class="border-primary/20 bg-primary/[0.02] shadow-sm">
			<CardHeader class="pb-3 border-b border-border/40">
				<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
					<div>
						<CardTitle class="text-base font-semibold flex items-center gap-2">
							<Icon name="sparkles" class="size-4 text-primary" />
							Batch Scraper & Feature Tag Enricher
						</CardTitle>
						<CardDescription class="text-xs mt-0.5">
							Visits TransportFamily listing web links, extracts commercial fleet features (Booking, Fleet Owners, HCV, LCV, Open Body Truck), aligns states, and updates tags.
						</CardDescription>
					</div>
					<div class="flex items-center gap-2">
						<!-- Quick Action: Align All State Codes -->
						<Button
							variant="outline"
							size="sm"
							onclick={() => runAlignStates()}
							disabled={isAligningStates || isBatchEnriching}
							class="text-xs h-8 gap-1.5"
							title="Aligns all full state names (Karnataka -> KA, etc.) in a single fast query"
						>
							{#if isAligningStates}
								<Icon name="loader-2" class="size-3.5 animate-spin" />
								Aligning...
							{:else}
								<Icon name="map-pin" class="size-3.5 text-primary" />
								Align All State Codes
							{/if}
						</Button>

						<!-- Quick Action: Remove Tyresoles Tag from All -->
						<Button
							variant="outline"
							size="sm"
							onclick={() => runCleanTags()}
							disabled={isCleaningTags || isBatchEnriching}
							class="text-xs h-8 gap-1.5"
							title="Removes 'Tyresoles' from contact tags"
						>
							{#if isCleaningTags}
								<Icon name="loader-2" class="size-3.5 animate-spin" />
								Cleaning...
							{:else}
								<Icon name="tag" class="size-3.5 text-amber-500" />
								Remove 'Tyresoles' Tag
							{/if}
						</Button>
					</div>
				</div>
			</CardHeader>

			<CardContent class="pt-4 space-y-4">
				<div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-background/60 p-4 rounded-xl border border-border/50">
					<!-- Batch Size Selector -->
					<div class="flex flex-wrap items-center gap-3">
						<span class="text-xs font-medium text-foreground">Next Batch Count (X):</span>
						<div class="w-24">
							<Input
								type="number"
								min="1"
								max="500"
								bind:value={batchCount}
								disabled={isBatchEnriching || isAutoRunning}
								class="h-8 text-xs font-mono"
							/>
						</div>
						<div class="flex items-center gap-1">
							{#each [10, 25, 50, 100, 200] as preset}
								<button
									type="button"
									class="px-2 py-1 text-[11px] rounded-md border transition-colors {batchCount === preset ? 'bg-primary text-primary-foreground border-primary' : 'bg-muted/50 hover:bg-muted text-muted-foreground'}"
									onclick={() => (batchCount = preset)}
									disabled={isBatchEnriching || isAutoRunning}
								>
									{preset}
								</button>
							{/each}
						</div>
					</div>

					<!-- Execution Controls -->
					<div class="flex items-center gap-2">
						{#if isAutoRunning}
							<Button
								variant="destructive"
								size="sm"
								onclick={stopAutoRun}
								class="gap-1.5 h-9"
							>
								<Icon name="square" class="size-3.5" />
								Pause / Stop Auto-Run
							</Button>
						{:else}
							<Button
								variant="default"
								size="sm"
								onclick={() => runBatchEnrichment()}
								disabled={isBatchEnriching}
								class="gap-1.5 h-9 font-medium shadow-sm"
							>
								{#if isBatchEnriching}
									<Icon name="loader-2" class="size-4 animate-spin" />
									Scraping Next {batchCount}...
								{:else}
									<Icon name="play" class="size-3.5 fill-current" />
									Run Next {batchCount} Contacts
								{/if}
							</Button>

							<Button
								variant="outline"
								size="sm"
								onclick={startAutoRun}
								disabled={isBatchEnriching || (stats ? stats.pendingWebEnrichmentCount === 0 : false)}
								class="gap-1.5 h-9 border-primary/30 text-primary hover:bg-primary/10"
								title="Repeatedly runs batch after batch until all contacts are enriched or stopped"
							>
								<Icon name="repeat" class="size-3.5" />
								Continuous Auto-Run
							</Button>
						{/if}
					</div>
				</div>

				<!-- Latest Batch Result Banner -->
				{#if lastBatchResult}
					<div class="rounded-xl border p-4 bg-card/60 space-y-3">
						<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-border/40 pb-2.5">
							<div class="flex items-center gap-2">
								<div class="size-2 rounded-full {lastBatchResult.scrapedSuccessCount > 0 ? 'bg-emerald-500' : 'bg-amber-500'}"></div>
								<span class="text-xs font-semibold text-foreground">
									Latest Batch Summary:
								</span>
								<span class="text-xs text-muted-foreground">{lastBatchResult.message}</span>
							</div>
							<div class="flex items-center gap-2 text-[11px] font-mono">
								<span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-600 border border-emerald-500/20">
									✓ {lastBatchResult.scrapedSuccessCount} scraped
								</span>
								{#if lastBatchResult.scrapedFailedCount > 0}
									<span class="px-2 py-0.5 rounded bg-amber-500/10 text-amber-600 border border-amber-500/20">
										⚠ {lastBatchResult.scrapedFailedCount} no features
									</span>
								{/if}
								<span class="px-2 py-0.5 rounded bg-primary/10 text-primary border border-primary/20">
									+{lastBatchResult.totalFeaturesAdded} tags
								</span>
							</div>
						</div>

						<!-- Processed Items Table -->
						{#if lastBatchResult.processedItems.length > 0}
							<div class="max-h-80 overflow-y-auto rounded-lg border border-border/50 text-xs">
								<Table>
									<TableHeader class="bg-muted/40 sticky top-0 z-10">
										<TableRow>
											<TableHead class="w-48">Contact</TableHead>
											<TableHead class="w-16">State</TableHead>
											<TableHead>Scraped Features / New Tags</TableHead>
											<TableHead class="w-28 text-right">Web Link</TableHead>
										</TableRow>
									</TableHeader>
									<TableBody>
										{#each lastBatchResult.processedItems as item}
											<TableRow class="hover:bg-muted/30">
												<TableCell class="font-medium">
													<div class="truncate max-w-[180px]" title={item.fullName}>
														{item.fullName}
													</div>
												</TableCell>
												<TableCell>
													<Badge variant="outline" class="font-mono text-[10px] px-1.5 py-0 bg-background">
														{item.state || '-'}
													</Badge>
												</TableCell>
												<TableCell>
													<div class="flex flex-wrap gap-1 items-center max-w-xl">
														{#if item.featuresFound && item.featuresFound.length > 0}
															{#each item.featuresFound as feat}
																<Badge variant="secondary" class="text-[10px] px-1.5 py-0 bg-primary/10 text-primary border-primary/20">
																	✓ {feat}
																</Badge>
															{/each}
														{:else}
															<span class="text-muted-foreground text-[11px] italic">No features scraped</span>
														{/if}
														<span class="text-[11px] text-muted-foreground truncate block w-full mt-0.5" title={item.newTags}>
															Tags: {item.newTags || 'None'}
														</span>
													</div>
												</TableCell>
												<TableCell class="text-right">
													{#if item.sourceUrl}
														<a
															href={item.sourceUrl}
															target="_blank"
															rel="noopener noreferrer"
															class="inline-flex items-center gap-1 text-[11px] text-primary hover:underline"
															title={item.sourceUrl}
														>
															<span>Visit</span>
															<Icon name="external-link" class="size-3" />
														</a>
													{:else}
														<span class="text-muted-foreground">-</span>
													{/if}
												</TableCell>
											</TableRow>
										{/each}
									</TableBody>
								</Table>
							</div>
						{/if}
					</div>
				{/if}

				<!-- Recent Live Enriched Samples -->
				{#if stats?.recentEnrichedSamples && stats.recentEnrichedSamples.length > 0 && !lastBatchResult}
					<div class="rounded-xl border p-4 bg-card/40 space-y-2.5">
						<div class="flex items-center justify-between">
							<span class="text-xs font-semibold text-foreground flex items-center gap-1.5">
								<Icon name="history" class="size-3.5 text-muted-foreground" />
								Recently Enriched Contacts in Database:
							</span>
							<span class="text-[11px] text-muted-foreground">Showing latest samples</span>
						</div>
						<div class="grid grid-cols-1 md:grid-cols-2 gap-2 text-xs">
							{#each stats.recentEnrichedSamples as sample}
								<div class="p-2.5 rounded-lg border border-border/50 bg-background/50 flex flex-col gap-1">
									<div class="flex items-center justify-between">
										<span class="font-medium text-foreground truncate max-w-[200px]" title={sample.fullName}>
											{sample.fullName}
										</span>
										<Badge variant="outline" class="font-mono text-[10px] px-1.5 py-0">
											{sample.state || '-'}
										</Badge>
									</div>
									<div class="text-[11px] text-muted-foreground truncate" title={sample.tags}>
										{sample.tags || 'No tags'}
									</div>
									{#if sample.sourceUrl}
										<div class="text-[10px] text-primary/80 truncate">
											<a href={sample.sourceUrl} target="_blank" rel="noopener noreferrer" class="hover:underline flex items-center gap-1">
												<span>{sample.sourceUrl}</span>
												<Icon name="external-link" class="size-2.5" />
											</a>
										</div>
									{/if}
								</div>
							{/each}
						</div>
					</div>
				{/if}
			</CardContent>
		</Card>
	</div>
</Tabs.Content>

<!-- ============================================================== -->
<!-- TAB 3: SYSTEM UTILITY TOOLS SECTION -->
<!-- ============================================================== -->
<Tabs.Content value="system-utilities" class="space-y-4 mt-0">
	<div class="flex items-center gap-2">
		<Icon name="cpu" class="size-4 text-muted-foreground" />
		<h2 class="text-sm font-semibold tracking-wider uppercase text-muted-foreground">Other Administrative Operations</h2>
	</div>

		<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
			<Card>
				<CardHeader>
					<div class="flex items-center gap-2">
						<Icon name="phone" class="size-5 text-primary" />
						<CardTitle class="text-base">Mobile Sanitation</CardTitle>
					</div>
					<CardDescription class="text-xs">
						Extract valid mobile numbers from Customer Name & Address fields in invoice headers.
					</CardDescription>
				</CardHeader>
				<CardFooter class="pt-4 flex items-center">
					<Button onclick={runSanitizer} disabled={isLoading} class="gap-2 w-full text-xs">
						{#if isLoading}
							<Icon name="loader-2" class="size-4 animate-spin" /> Processing...
						{:else}
							<Icon name="sparkles" class="size-4" /> Run Sanitation Utility
						{/if}
					</Button>
				</CardFooter>
			</Card>

			<Card>
				<CardHeader>
					<div class="flex items-center gap-2">
						<Icon name="users" class="size-5 text-primary" />
						<CardTitle class="text-base">CRM Contacts Import</CardTitle>
					</div>
					<CardDescription class="text-xs">
						Harvest distinct mobile numbers and product purchase histories from invoices into CRM contacts.
					</CardDescription>
				</CardHeader>
				<CardFooter class="pt-4 flex items-center">
					<Button onclick={runCrmImport} disabled={isImporting} class="gap-2 w-full text-xs">
						{#if isImporting}
							<Icon name="loader-2" class="size-4 animate-spin" /> Importing...
						{:else}
							<Icon name="download" class="size-4" /> Run CRM Import
						{/if}
					</Button>
				</CardFooter>
			</Card>

			<Card>
				<CardHeader>
					<div class="flex items-center gap-2">
						<Icon name="file-spreadsheet" class="size-5 text-primary" />
						<CardTitle class="text-base">Ledger Rectification</CardTitle>
					</div>
					<CardDescription class="text-xs">
						Rectify customer ledger entries and payment balance discrepancies.
					</CardDescription>
				</CardHeader>
				<CardFooter class="pt-4 flex items-center">
					<Button onclick={runRectifyLedgers} disabled={isRectifying} class="gap-2 w-full text-xs">
						{#if isRectifying}
							<Icon name="loader-2" class="size-4 animate-spin" /> Rectifying...
						{:else}
							<Icon name="wrench" class="size-4" /> Run Ledger Rectification
						{/if}
					</Button>
				</CardFooter>
			</Card>

			<Card class="border-destructive/40 bg-destructive/[0.02]">
				<CardHeader>
					<div class="flex items-center gap-2">
						<Icon name="trash-2" class="size-5 text-destructive" />
						<CardTitle class="text-base text-destructive">Wipe CRM Records</CardTitle>
					</div>
					<CardDescription class="text-xs">
						Caution: Permanently wipe CRM calling allocations, call logs, and reminder entries.
					</CardDescription>
				</CardHeader>
				<CardFooter class="pt-4 flex items-center">
					<Button onclick={runWipeCrm} disabled={isWipingCrm} variant="destructive" class="gap-2 w-full text-xs">
						{#if isWipingCrm}
							<Icon name="loader-2" class="size-4 animate-spin" /> Wiping...
						{:else}
							<Icon name="triangle-alert" class="size-4" /> Wipe Calling Records
						{/if}
					</Button>
				</CardFooter>
			</Card>
		</div>
	</Tabs.Content>
</Tabs.Root>
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
