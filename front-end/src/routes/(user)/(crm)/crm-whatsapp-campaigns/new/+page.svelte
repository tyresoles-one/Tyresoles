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
		GET_WHATSAPP_CAMPAIGNS,
		GET_WHATSAPP_TEMPLATES,
		ESTIMATE_WHATSAPP_AUDIENCE,
		GET_WHATSAPP_ACCOUNT_STATUS,
		SAVE_WHATSAPP_CAMPAIGN,
		SCHEDULE_WHATSAPP_CAMPAIGN,
		TEST_SEND_WHATSAPP_CAMPAIGN,
		TEST_SEND_WHATSAPP_CAMPAIGN_GROUP,
		GET_CRM_CONTACTS_FOR_WHATSAPP_PICKER,
		GET_CRM_CONTACTS_FILTER_OPTIONS,
		type CrmWhatsappCampaign,
		type CrmWhatsappTemplate,
		type AudienceEstimateResult,
		type WabaHealthStatus,
		type CrmContactWhatsappPickerItem,
		type CrmContactsWhatsappPickerResult,
		type CrmContactsFilterOptionsResult,
		type WhatsappGroupTestResult
	} from '../whatsappQueries';

	let currentStep = $state(1);

	// Step 1: Campaign Details
	let campaignName = $state('');
	let senderPhoneId = $state('');
	let lastCampaignMediaUrl = $state('');
	let headerMediaUrl = $state('');

	// Step 2: Template & Variables
	let templates = $state<CrmWhatsappTemplate[]>([]);
	let selectedTemplateId = $state<string>('');
	let selectedTemplate = $derived(templates.find((t) => t.id === selectedTemplateId) || null);
	let variableMappings = $state<Array<{ placeholder: string; fieldName: string }>>([]);

	// Step 3: Audience Filters & Batch Picker
	let selectionMode = $state<'ALL_FILTERED' | 'MANUAL'>('ALL_FILTERED');
	let onlyUniqueNumbers = $state(true); // Default to fresh contacts only
	let selectedContactIds = $state<Set<string>>(new Set());
	let pickerContacts = $state<CrmContactWhatsappPickerItem[]>([]);
	let pickerTotalCount = $state(0);
	let pickerLoading = $state(false);
	let pickerSearch = $state('');
	let pickerSkip = $state(0);
	let pickerTake = $state(300); // Default batch size 300
	let customBatchInput = $state(300);

	let isFullBatchSelected = $derived(
		pickerContacts.length > 0 && pickerContacts.every((c) => selectedContactIds.has(c.id))
	);

	// Contact filter options loaded from DB
	let filterOptions = $state<CrmContactsFilterOptionsResult>({
		contactTypes: [],
		contactCategories: [],
		respCenters: [],
		states: [],
		cities: []
	});
	let loadingFilterOptions = $state(false);

	// Filter values
	let filterContactType = $state('');
	let filterCategory = $state('');
	let filterState = $state('');
	let filterCity = $state('');
	let filterRespCenter = $state('');
	let filterTag = $state('');
	let minQualityScore = $state<number | null>(null);

	let hasActiveFilters = $derived(
		Boolean(
			pickerSearch.trim() ||
			filterContactType ||
			filterCategory ||
			filterState ||
			filterCity ||
			filterRespCenter
		)
	);

	let audienceEstimate = $state<AudienceEstimateResult>({
		totalMatchingContacts: 0,
		withValidPhone: 0,
		suppressedCount: 0,
		previouslyCampaignedCount: 0,
		eligibleRecipients: 0
	});
	let estimatingAudience = $state(false);

	// Step 4: Scheduling & Small Group Test
	let scheduleOption = $state<'NOW' | 'LATER'>('NOW');
	let scheduledDate = $state('');
	let scheduledTime = $state('10:00');
	let testPhoneNumber = $state('');
	let testNumbersInput = $state('');
	let groupTestResults = $state<WhatsappGroupTestResult[]>([]);
	let sendingGroupTest = $state(false);
	let sendingTest = $state(false);
	let submitting = $state(false);

	let accountStatus = $state<WabaHealthStatus | null>(null);
	let loadingData = $state(true);

	// Extract placeholders {{1}}, {{2}} from body text
	$effect(() => {
		if (selectedTemplate?.bodyText) {
			const matches = selectedTemplate.bodyText.match(/\{\{(\d+)\}\}/g) || [];
			const uniquePlaceholders = Array.from(new Set(matches));

			variableMappings = uniquePlaceholders.map((ph, idx) => {
				const defaultFields = ['FullName', 'CompanyName', 'City', 'RespCenter', 'Products'];
				return {
					placeholder: ph,
					fieldName: defaultFields[idx] || 'FullName'
				};
			});
		} else {
			variableMappings = [];
		}

		if (selectedTemplate) {
			const hType = selectedTemplate.headerType?.toUpperCase();
			if (hType === 'IMAGE' || hType === 'DOCUMENT' || hType === 'VIDEO') {
				if (!headerMediaUrl.trim()) {
					headerMediaUrl =
						selectedTemplate.headerMediaUrl ||
						lastCampaignMediaUrl ||
						'https://api.tyresoles.in/images/WA-temp-001.jpeg';
				}
			}
		}
	});

	// Dynamic live preview of message
	let previewMessageBody = $derived.by(() => {
		if (!selectedTemplate?.bodyText) return 'Select a template to preview your WhatsApp message.';
		let text = selectedTemplate.bodyText;
		for (const mapping of variableMappings) {
			const sampleVal =
				mapping.fieldName === 'FullName'
					? 'Rajesh Kumar'
					: mapping.fieldName === 'CompanyName'
						? 'ABC Logistics'
						: mapping.fieldName === 'City'
							? 'Pune'
							: mapping.fieldName === 'RespCenter'
								? 'Tyresoles Pune Depot'
								: 'Commercial Tyre Retreads';
			text = text.replaceAll(mapping.placeholder, sampleVal);
		}
		return text;
	});

	async function loadInitial() {
		loadingData = true;
		try {
			const [tplRes, accRes, campRes] = await Promise.all([
				graphqlQuery<{ getCrmWhatsappTemplates: CrmWhatsappTemplate[] }>(GET_WHATSAPP_TEMPLATES, {
					variables: { status: 'APPROVED' }
				}),
				graphqlQuery<{ getWhatsappAccountStatus: WabaHealthStatus }>(GET_WHATSAPP_ACCOUNT_STATUS),
				graphqlQuery<{ getCrmWhatsappCampaigns: CrmWhatsappCampaign[] }>(GET_WHATSAPP_CAMPAIGNS, {
					variables: { take: 1 }
				})
			]);

			if (tplRes.success && tplRes.data?.getCrmWhatsappTemplates) {
				templates = tplRes.data.getCrmWhatsappTemplates;
				if (templates.length > 0) {
					selectedTemplateId = templates[0].id;
				}
			}

			if (accRes.success && accRes.data?.getWhatsappAccountStatus) {
				accountStatus = accRes.data.getWhatsappAccountStatus;
				senderPhoneId = accountStatus.displayPhoneNumber || '';
			}

			if (campRes.success && campRes.data?.getCrmWhatsappCampaigns?.length) {
				const lastCamp = campRes.data.getCrmWhatsappCampaigns[0];
				if (lastCamp?.name && !campaignName) {
					// Auto-increment trailing number if present, e.g. "Reduce-Cost-0010" -> "Reduce-Cost-0011"
					const match = lastCamp.name.match(/^(.*?)(\d+)$/);
					if (match) {
						const prefix = match[1];
						const numStr = match[2];
						const nextNum = parseInt(numStr, 10) + 1;
						const padded = String(nextNum).padStart(numStr.length, '0');
						campaignName = `${prefix}${padded}`;
					} else {
						campaignName = lastCamp.name;
					}
				}
				if (lastCamp?.headerMediaUrl) {
					lastCampaignMediaUrl = lastCamp.headerMediaUrl;
					if (!headerMediaUrl) {
						headerMediaUrl = lastCamp.headerMediaUrl;
					}
				}
			}

			// Fallback defaults
			if (!campaignName) {
				campaignName = 'Reduce-Cost-0011';
			}
			if (!headerMediaUrl) {
				headerMediaUrl = 'https://api.tyresoles.in/images/WA-temp-001.jpeg';
			}
		} catch (e: any) {
			toast.error('Failed to load campaigns/templates: ' + e.message);
		} finally {
			loadingData = false;
		}
	}

	onMount(() => {
		loadInitial();
		loadFilterOptions();
		runAudienceEstimation();
		loadPickerContacts();
	});

	async function loadFilterOptions() {
		loadingFilterOptions = true;
		try {
			const res = await graphqlQuery<{ getCrmContactsFilterOptions: CrmContactsFilterOptionsResult }>(
				GET_CRM_CONTACTS_FILTER_OPTIONS
			);
			if (res.success && res.data?.getCrmContactsFilterOptions) {
				filterOptions = res.data.getCrmContactsFilterOptions;
			}
		} catch (e) {
			console.error('Failed to load contacts filter options', e);
		} finally {
			loadingFilterOptions = false;
		}
	}

	async function fetchPickerSlice(skip: number, take: number) {
		const res = await graphqlQuery<{ getCrmContactsForWhatsappPicker: CrmContactsWhatsappPickerResult }>(
			GET_CRM_CONTACTS_FOR_WHATSAPP_PICKER,
			{
				variables: {
					search: pickerSearch.trim() || null,
					contactType: filterContactType || null,
					contactCategory: filterCategory || null,
					state: filterState || null,
					city: filterCity || null,
					respCenter: filterRespCenter || null,
					onlyUniqueNumbers: onlyUniqueNumbers || null,
					skip,
					take
				}
			}
		);
		return res.success && res.data?.getCrmContactsForWhatsappPicker
			? res.data.getCrmContactsForWhatsappPicker
			: { items: [], totalCount: 0 };
	}

	async function loadPickerContacts() {
		pickerLoading = true;
		try {
			const targetTake = pickerTake;
			const firstSlice = await fetchPickerSlice(pickerSkip, targetTake);
			let items = firstSlice.items || [];
			pickerTotalCount = firstSlice.totalCount || 0;

			// If backend returned fewer than targetTake because of backend per-request cap (100)
			// and more items exist in total, fetch remaining slices in parallel
			if (targetTake > items.length && items.length === 100 && pickerTotalCount > items.length) {
				const remainingNeeded = Math.min(targetTake, pickerTotalCount - pickerSkip) - items.length;
				const numExtraSlices = Math.ceil(remainingNeeded / 100);
				const extraPromises = [];
				for (let i = 1; i <= numExtraSlices; i++) {
					const sliceSkip = pickerSkip + i * 100;
					const sliceTake = Math.min(100, targetTake - i * 100);
					if (sliceTake > 0) {
						extraPromises.push(fetchPickerSlice(sliceSkip, sliceTake));
					}
				}
				const extraResults = await Promise.all(extraPromises);
				for (const r of extraResults) {
					items = items.concat(r.items);
				}
			}

			pickerContacts = items;
		} catch (e) {
			console.error('Failed to load contacts for picker', e);
		} finally {
			pickerLoading = false;
		}
	}

	function handleUniqueNumbersToggle() {
		onlyUniqueNumbers = !onlyUniqueNumbers;
		pickerSkip = 0;
		loadPickerContacts();
		runAudienceEstimation();
	}

	function handleBatchSizeChange(newSize: number) {
		if (newSize < 1) return;
		pickerTake = newSize;
		customBatchInput = newSize;
		pickerSkip = 0;
		loadPickerContacts();
	}

	function handleCustomBatchChange(e: Event) {
		const val = parseInt((e.target as HTMLInputElement).value, 10);
		if (!isNaN(val) && val > 0) {
			handleBatchSizeChange(val);
		} else {
			customBatchInput = pickerTake;
		}
	}

	function selectFullBatch() {
		if (pickerContacts.length === 0) {
			toast.info('No contacts available in the current batch.');
			return;
		}
		selectionMode = 'MANUAL';
		const next = new Set(selectedContactIds);
		for (const c of pickerContacts) {
			next.add(c.id);
		}
		selectedContactIds = next;
		runAudienceEstimation();
		toast.success(`Selected full batch of ${pickerContacts.length} contacts!`);
	}

	function toggleSelectBatch() {
		if (isFullBatchSelected) {
			const batchIds = new Set(pickerContacts.map((c) => c.id));
			const next = new Set<string>();
			for (const id of selectedContactIds) {
				if (!batchIds.has(id)) next.add(id);
			}
			selectedContactIds = next;
			if (selectionMode === 'MANUAL') {
				runAudienceEstimation();
			}
			toast.info('Deselected contacts in current batch.');
		} else {
			selectFullBatch();
		}
	}

	let searchDebounce: any = null;
	function handleSearchInput() {
		clearTimeout(searchDebounce);
		searchDebounce = setTimeout(() => {
			pickerSkip = 0;
			loadPickerContacts();
			if (selectionMode === 'ALL_FILTERED') {
				runAudienceEstimation();
			}
		}, 350);
	}

	function handleSelectionModeChange(mode: 'ALL_FILTERED' | 'MANUAL') {
		selectionMode = mode;
		runAudienceEstimation();
	}

	function goToPreviousPickerPage() {
		if (pickerSkip >= pickerTake) {
			pickerSkip -= pickerTake;
			loadPickerContacts();
		}
	}

	function goToNextPickerPage() {
		if (pickerSkip + pickerTake < pickerTotalCount) {
			pickerSkip += pickerTake;
			loadPickerContacts();
		}
	}

	function handleFilterChanged() {
		pickerSkip = 0;
		loadPickerContacts();
		runAudienceEstimation();
	}

	function toggleSelectContact(id: string) {
		const next = new Set(selectedContactIds);
		if (next.has(id)) {
			next.delete(id);
		} else {
			next.add(id);
		}
		selectedContactIds = next;
		if (selectionMode === 'MANUAL') {
			runAudienceEstimation();
		}
	}

	function clearAllSelection() {
		selectedContactIds = new Set();
		if (selectionMode === 'MANUAL') {
			runAudienceEstimation();
		}
		toast.info('Cleared selection.');
	}

	function clearFilters() {
		pickerSearch = '';
		filterContactType = '';
		filterCategory = '';
		filterState = '';
		filterCity = '';
		filterRespCenter = '';
		pickerSkip = 0;
		loadPickerContacts();
		runAudienceEstimation();
	}

	async function runAudienceEstimation() {
		estimatingAudience = true;
		try {
			const res = await graphqlQuery<{ estimateWhatsappCampaignAudience: AudienceEstimateResult }>(
				ESTIMATE_WHATSAPP_AUDIENCE,
				{
					variables: {
						filter: {
							contactType: filterContactType || null,
							contactCategory: filterCategory || null,
							state: filterState || null,
							city: filterCity || null,
							respCenter: filterRespCenter || null,
							tag: filterTag || null,
							minQualityScore: minQualityScore,
							search: pickerSearch.trim() || null,
							selectedContactIds: selectionMode === 'MANUAL' && selectedContactIds.size > 0
								? Array.from(selectedContactIds)
								: null,
							onlyUniqueNumbers: onlyUniqueNumbers
						}
					}
				}
			);

			if (res.success && res.data?.estimateWhatsappCampaignAudience) {
				audienceEstimate = res.data.estimateWhatsappCampaignAudience;
			}
		} catch (e) {
			console.error('Failed to estimate audience', e);
		} finally {
			estimatingAudience = false;
		}
	}

	async function handleSendTestMessage() {
		if (!testPhoneNumber.trim()) {
			toast.error('Please enter a test phone number with country code (+91...)');
			return;
		}

		const hType = selectedTemplate?.headerType?.toUpperCase();
		if ((hType === 'IMAGE' || hType === 'DOCUMENT' || hType === 'VIDEO') && !headerMediaUrl.trim()) {
			toast.error(`The selected template requires an ${hType} Header URL before sending.`);
			currentStep = 2;
			return;
		}

		sendingTest = true;
		try {
			const saveRes = await graphqlMutation<{ saveCrmWhatsappCampaign: { id: string } }>(
				SAVE_WHATSAPP_CAMPAIGN,
				{
					variables: {
						input: {
							name: campaignName.trim() || 'Test Preview Campaign',
							templateId: selectedTemplateId || null,
							headerMediaUrl: headerMediaUrl || null,
							variableMappings: variableMappings
						}
					}
				}
			);

			if (saveRes.success && saveRes.data?.saveCrmWhatsappCampaign) {
				const campId = saveRes.data.saveCrmWhatsappCampaign.id;
				const testRes = await graphqlMutation<{ testSendWhatsappCampaign: { success: boolean; errorMessage?: string } }>(
					TEST_SEND_WHATSAPP_CAMPAIGN,
					{
						variables: {
							campaignId: campId,
							testPhoneNumber: testPhoneNumber
						}
					}
				);

				if (testRes.success && testRes.data?.testSendWhatsappCampaign?.success) {
					toast.success(`Test WhatsApp message sent to ${testPhoneNumber}!`);
				} else {
					const errMsg = testRes.data?.testSendWhatsappCampaign?.errorMessage ||
						(typeof testRes.error === 'string' ? testRes.error : (testRes.error as any)?.message) ||
						'Test message failed to deliver.';
					toast.error(errMsg);
				}
			} else {
				const errMsg = typeof saveRes.error === 'string' ? saveRes.error : (saveRes.error as any)?.message || 'Failed to save campaign draft.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error('Error sending test message: ' + e.message);
		} finally {
			sendingTest = false;
		}
	}

	async function handleSendGroupTest() {
		const rawList = testNumbersInput
			.split(/[\n,;]+/)
			.map((s) => s.trim())
			.filter(Boolean);

		if (rawList.length === 0) {
			toast.error('Please enter at least one test phone number (e.g. 919880334191)');
			return;
		}

		const hType = selectedTemplate?.headerType?.toUpperCase();
		if ((hType === 'IMAGE' || hType === 'DOCUMENT' || hType === 'VIDEO') && !headerMediaUrl.trim()) {
			toast.error(`The selected template requires an ${hType} Header URL before sending.`);
			currentStep = 2;
			return;
		}

		sendingGroupTest = true;
		groupTestResults = [];
		try {
			const saveRes = await graphqlMutation<{ saveCrmWhatsappCampaign: { id: string } }>(
				SAVE_WHATSAPP_CAMPAIGN,
				{
					variables: {
						input: {
							name: campaignName.trim() || 'Test Sample Campaign',
							templateId: selectedTemplateId || null,
							headerMediaUrl: headerMediaUrl || null,
							variableMappings: variableMappings
						}
					}
				}
			);

			if (saveRes.success && saveRes.data?.saveCrmWhatsappCampaign) {
				const campId = saveRes.data.saveCrmWhatsappCampaign.id;
				const testRes = await graphqlMutation<{ testSendWhatsappCampaignGroup: WhatsappGroupTestResult[] }>(
					TEST_SEND_WHATSAPP_CAMPAIGN_GROUP,
					{
						variables: {
							campaignId: campId,
							testPhoneNumbers: rawList
						}
					}
				);

				if (testRes.success && testRes.data?.testSendWhatsappCampaignGroup) {
					groupTestResults = testRes.data.testSendWhatsappCampaignGroup;
					const successCount = groupTestResults.filter((r) => r.success).length;
					if (successCount === groupTestResults.length) {
						toast.success(`All ${successCount} test WhatsApp messages delivered successfully!`);
					} else {
						toast.info(`Sent test messages: ${successCount} succeeded, ${groupTestResults.length - successCount} failed.`);
					}
				} else {
					const errMsg = typeof testRes.error === 'string' ? testRes.error : (testRes.error as any)?.message || 'Failed to dispatch group test.';
					toast.error(errMsg);
				}
			} else {
				const errMsg = typeof saveRes.error === 'string' ? saveRes.error : (saveRes.error as any)?.message || 'Failed to prepare test campaign.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error('Error in test dispatch: ' + e.message);
		} finally {
			sendingGroupTest = false;
		}
	}

	async function handleLaunchCampaign() {
		if (!campaignName.trim()) {
			toast.error('Please provide a campaign name.');
			currentStep = 1;
			return;
		}
		if (!selectedTemplateId) {
			toast.error('Please select a WhatsApp template.');
			currentStep = 2;
			return;
		}
		const launchHType = selectedTemplate?.headerType?.toUpperCase();
		if ((launchHType === 'IMAGE' || launchHType === 'DOCUMENT' || launchHType === 'VIDEO') && !headerMediaUrl.trim()) {
			toast.error(`The selected template requires an ${launchHType} Header URL.`);
			currentStep = 2;
			return;
		}
		if (selectionMode === 'MANUAL' && selectedContactIds.size === 0) {
			toast.error('Please select contacts using "Select Full Batch" or checkboxes, or switch to "Smart Bulk Broadcast" mode.');
			currentStep = 3;
			return;
		}
		if (audienceEstimate.eligibleRecipients === 0) {
			toast.error('Audience filter matches 0 eligible recipients.');
			currentStep = 3;
			return;
		}

		submitting = true;
		try {
			// 1. Save Campaign
			const saveRes = await graphqlMutation<{ saveCrmWhatsappCampaign: { id: string } }>(
				SAVE_WHATSAPP_CAMPAIGN,
				{
					variables: {
						input: {
							name: campaignName.trim(),
							templateId: selectedTemplateId,
							headerMediaUrl: headerMediaUrl.trim() || null,
							targetSegmentFilter: {
								contactType: filterContactType || null,
								contactCategory: filterCategory || null,
								state: filterState || null,
								city: filterCity || null,
								respCenter: filterRespCenter || null,
								tag: filterTag || null,
								minQualityScore: minQualityScore,
								search: pickerSearch.trim() || null,
								selectedContactIds: selectionMode === 'MANUAL' && selectedContactIds.size > 0
									? Array.from(selectedContactIds)
									: null,
								onlyUniqueNumbers: onlyUniqueNumbers
							},
							variableMappings: variableMappings
						}
					}
				}
			);

			if (!saveRes.success || !saveRes.data?.saveCrmWhatsappCampaign) {
				const errMsg = typeof saveRes.error === 'string' ? saveRes.error : (saveRes.error as any)?.message || 'Failed to save campaign';
				throw new Error(errMsg);
			}

			const campaignId = saveRes.data.saveCrmWhatsappCampaign.id;

			// 2. Schedule or Run Immediately
			let scheduleDateTime: string | null = null;
			if (scheduleOption === 'LATER') {
				if (!scheduledDate) {
					toast.error('Please pick a scheduled date.');
					submitting = false;
					return;
				}
				scheduleDateTime = new Date(`${scheduledDate}T${scheduledTime}:00Z`).toISOString();
			}

			const schedRes = await graphqlMutation(SCHEDULE_WHATSAPP_CAMPAIGN, {
				variables: {
					campaignId: campaignId,
					scheduledAt: scheduleDateTime
				}
			});

			if (schedRes.success) {
				toast.success(
					scheduleOption === 'NOW'
						? 'WhatsApp Campaign launched! Messages queued for dispatch.'
						: `Campaign scheduled for ${scheduledDate} ${scheduledTime}.`
				);
				goto(`/crm-whatsapp-campaigns/${campaignId}`);
			} else {
				const errMsg = typeof schedRes.error === 'string' ? schedRes.error : (schedRes.error as any)?.message || 'Failed to schedule campaign';
				throw new Error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message);
		} finally {
			submitting = false;
		}
	}
</script>

<div class="max-w-5xl mx-auto space-y-6 pb-20">
	<PageHeading
		backHref="/crm-whatsapp-campaigns"
		backLabel="Campaigns"
		icon="plus-circle"
		title="Create WhatsApp Marketing Campaign"
	/>

	<!-- Stepper Header -->
	<div class="bg-card border border-border rounded-2xl p-4 shadow-sm">
		<div class="grid grid-cols-4 gap-2 text-center text-xs">
			<button
				type="button"
				class="flex items-center justify-center gap-2 p-2 rounded-xl transition-colors {currentStep === 1 ? 'bg-primary text-primary-foreground font-semibold' : currentStep > 1 ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-medium' : 'text-muted-foreground'}"
				onclick={() => (currentStep = 1)}
			>
				<span class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] border border-current">1</span>
				<span>Details</span>
			</button>

			<button
				type="button"
				class="flex items-center justify-center gap-2 p-2 rounded-xl transition-colors {currentStep === 2 ? 'bg-primary text-primary-foreground font-semibold' : currentStep > 2 ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-medium' : 'text-muted-foreground'}"
				onclick={() => (currentStep = 2)}
			>
				<span class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] border border-current">2</span>
				<span>Template & Preview</span>
			</button>

			<button
				type="button"
				class="flex items-center justify-center gap-2 p-2 rounded-xl transition-colors {currentStep === 3 ? 'bg-primary text-primary-foreground font-semibold' : currentStep > 3 ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-medium' : 'text-muted-foreground'}"
				onclick={() => (currentStep = 3)}
			>
				<span class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] border border-current">3</span>
				<span>Audience Filter</span>
			</button>

			<button
				type="button"
				class="flex items-center justify-center gap-2 p-2 rounded-xl transition-colors {currentStep === 4 ? 'bg-primary text-primary-foreground font-semibold' : 'text-muted-foreground'}"
				onclick={() => (currentStep = 4)}
			>
				<span class="w-5 h-5 rounded-full flex items-center justify-center text-[10px] border border-current">4</span>
				<span>Schedule & Test</span>
			</button>
		</div>
	</div>

	<!-- Step 1: Details -->
	{#if currentStep === 1}
		<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-5">
			<h3 class="text-base font-semibold text-foreground flex items-center gap-2">
				<Icon name="file-text" class="w-5 h-5 text-primary" />
				Step 1: Campaign Overview & Identity
			</h3>

			<div class="space-y-4">
				<div class="space-y-1.5">
					<label class="text-xs font-medium text-foreground">Campaign Name *</label>
					<Input
						bind:value={campaignName}
						placeholder="e.g. Reduce-Cost-0011"
						class="text-xs"
					/>
				</div>
			</div>

			<div class="flex justify-end pt-4 border-t border-border">
				<Button
					class="gap-1.5"
					onclick={() => {
						if (!campaignName.trim()) {
							toast.error('Please enter a campaign name.');
							return;
						}
						currentStep = 2;
					}}
				>
					<span>Next: Template & Preview</span>
					<Icon name="arrow-right" class="w-4 h-4" />
				</Button>
			</div>
		</div>
	{/if}

	<!-- Step 2: Template Selection & Smartphone Mockup -->
	{#if currentStep === 2}
		<div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
			<!-- Left: Template Selection & Variable Mapper -->
			<div class="lg:col-span-7 bg-card border border-border rounded-2xl p-6 shadow-sm space-y-5">
				<h3 class="text-base font-semibold text-foreground flex items-center gap-2">
					<Icon name="layout-template" class="w-5 h-5 text-primary" />
					Step 2: Template Selection & Personalization
				</h3>

				<div class="space-y-1.5">
					<label class="text-xs font-medium text-foreground">Select Meta-Approved Template *</label>
					<select
						bind:value={selectedTemplateId}
						class="w-full py-2 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary"
					>
						{#each templates as t}
							<option value={t.id}>{t.name} ({t.category} - {t.language})</option>
						{/each}
					</select>
				</div>

				<!-- Required Header Media Configuration -->
				{#if selectedTemplate && (selectedTemplate.headerType === 'IMAGE' || selectedTemplate.headerType === 'DOCUMENT' || selectedTemplate.headerType === 'VIDEO')}
					<div class="space-y-2.5 bg-amber-500/10 border border-amber-500/30 p-3.5 rounded-xl">
						<div class="flex items-center justify-between">
							<div class="flex items-center gap-1.5 text-xs font-semibold text-amber-700 dark:text-amber-300">
								<Icon name="image" class="w-4 h-4" />
								<span>Header {selectedTemplate.headerType}</span>
							</div>
							<span class="text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full bg-amber-200 dark:bg-amber-900 text-amber-900 dark:text-amber-100">
								Required
							</span>
						</div>
						<div class="space-y-1">
							<Input
								bind:value={headerMediaUrl}
								placeholder="https://api.tyresoles.in/images/WA-temp-001.jpeg"
								class="text-xs bg-background"
							/>
						</div>
						{#if headerMediaUrl}
							<div class="text-[10px] text-emerald-600 dark:text-emerald-400 flex items-center gap-1">
								<Icon name="check" class="w-3 h-3" />
								<span>Header media URL set</span>
							</div>
						{/if}
					</div>
				{/if}

				<!-- Variable Mapper -->
				{#if variableMappings.length > 0}
					<div class="space-y-3 pt-2">
						<div class="text-xs font-semibold text-foreground flex items-center justify-between">
							<span>Map Dynamic Variables to CRM Contact Fields</span>
							<span class="text-[11px] text-muted-foreground">{variableMappings.length} variables detected</span>
						</div>

						<div class="space-y-2.5 bg-muted/40 border border-border p-3.5 rounded-xl">
							{#each variableMappings as v, idx}
								<div class="flex items-center gap-2 text-xs">
									<span class="font-mono font-bold text-primary bg-primary/10 px-2 py-1 rounded w-16 text-center">
										{v.placeholder}
									</span>
									<span class="text-muted-foreground">➔</span>
									<select
										bind:value={v.fieldName}
										class="flex-1 py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary"
									>
										<option value="FullName">Contact Full Name (e.g. Rajesh Kumar)</option>
										<option value="CompanyName">Company Name (e.g. ABC Logistics)</option>
										<option value="City">City (e.g. Pune)</option>
										<option value="State">State (e.g. Maharashtra)</option>
										<option value="RespCenter">Responsibility Center / Depot (e.g. Pune Depot)</option>
										<option value="Products">Subscribed Products / Fleet Type</option>
									</select>
								</div>
							{/each}
						</div>
					</div>
				{/if}

				<div class="flex items-center justify-between pt-4 border-t border-border">
					<Button variant="outline" size="sm" onclick={() => (currentStep = 1)}>
						<Icon name="arrow-left" class="w-4 h-4 mr-1" />
						Back
					</Button>
					<Button
						size="sm"
						class="gap-1.5"
						onclick={() => {
							const hType = selectedTemplate?.headerType?.toUpperCase();
							if ((hType === 'IMAGE' || hType === 'DOCUMENT' || hType === 'VIDEO') && !headerMediaUrl.trim()) {
								toast.error(`Please provide a Header ${hType} URL. This template cannot be sent without it.`);
								return;
							}
							currentStep = 3;
						}}
					>
						<span>Next: Audience Filter</span>
						<Icon name="arrow-right" class="w-4 h-4" />
					</Button>
				</div>
			</div>

			<!-- Right: Interactive Smartphone Simulator -->
			<div class="lg:col-span-5 flex flex-col items-center">
				<div class="text-xs font-medium text-muted-foreground mb-2 flex items-center gap-1.5">
					<Icon name="smartphone" class="w-4 h-4 text-emerald-600" />
					<span>Live WhatsApp Smartphone Preview</span>
				</div>

				<!-- Smartphone Mockup Frame -->
				<div class="w-[300px] h-[580px] bg-slate-900 rounded-[40px] p-3 shadow-2xl border-4 border-slate-700 relative flex flex-col">
					<!-- Speaker & Camera Notch -->
					<div class="absolute top-4 left-1/2 -translate-x-1/2 w-20 h-4 bg-black rounded-full z-20"></div>

					<!-- Screen Area -->
					<div class="w-full h-full bg-[#ECE5DD] dark:bg-[#0B141A] rounded-[30px] overflow-hidden flex flex-col pt-6 relative">
						<!-- WhatsApp Header -->
						<div class="bg-[#075E54] dark:bg-[#1F2C34] text-white p-2.5 flex items-center gap-2 shadow z-10">
							<Icon name="arrow-left" class="w-4 h-4 opacity-80" />
							<div class="w-7 h-7 rounded-full bg-emerald-700 flex items-center justify-center font-bold text-xs">
								T
							</div>
							<div class="flex-1 overflow-hidden">
								<div class="text-xs font-semibold truncate">Tyresoles Fleet Services</div>
								<div class="text-[9px] text-emerald-200">Official Business Account</div>
							</div>
							<Icon name="more-vertical" class="w-4 h-4 opacity-80" />
						</div>

						<!-- Chat Messages Canvas -->
						<div class="flex-1 p-3 overflow-y-auto space-y-2 text-xs">
							<!-- WhatsApp Message Bubble -->
							<div class="bg-white dark:bg-[#202C33] text-slate-800 dark:text-slate-100 rounded-2xl rounded-tl-none p-3 shadow-sm border border-black/5 space-y-2 max-w-[95%]">
								<!-- Header Media if present -->
								{#if headerMediaUrl}
									<div class="w-full h-28 bg-slate-200 dark:bg-slate-700 rounded-lg overflow-hidden flex items-center justify-center text-[10px] text-muted-foreground relative">
										<img
											src={headerMediaUrl}
											alt="Campaign Header"
											class="w-full h-full object-cover"
											onerror={(e) => ((e.target as HTMLElement).style.display = 'none')}
										/>
										<span class="absolute inset-0 flex items-center justify-center pointer-events-none">
											[Campaign Media Header]
										</span>
									</div>
								{/if}

								<!-- Body Text -->
								<div class="text-[11px] leading-relaxed whitespace-pre-wrap font-sans">
									{previewMessageBody}
								</div>

								<!-- Footer Text -->
								{#if selectedTemplate?.footerText}
									<div class="text-[9px] text-muted-foreground pt-1 border-t border-border/40">
										{selectedTemplate.footerText}
									</div>
								{/if}

								<!-- Timestamp & Single Gray Check -->
								<div class="text-[9px] text-muted-foreground text-right flex items-center justify-end gap-1">
									<span>{new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}</span>
									<span class="text-emerald-500 font-bold">✓✓</span>
								</div>
							</div>

							<!-- Action Buttons -->
							<div class="space-y-1 max-w-[95%]">
								<button class="w-full bg-white dark:bg-[#202C33] text-emerald-600 dark:text-emerald-400 font-semibold py-1.5 px-3 rounded-xl shadow-sm text-center text-[11px] border border-black/5 hover:bg-slate-50">
									📞 Contact Depot Agent
								</button>
								<button class="w-full bg-white dark:bg-[#202C33] text-blue-600 dark:text-blue-400 font-semibold py-1.5 px-3 rounded-xl shadow-sm text-center text-[11px] border border-black/5 hover:bg-slate-50">
									🌐 Book Inspection Online
								</button>
								<button class="w-full bg-white dark:bg-[#202C33] text-slate-500 font-normal py-1 px-3 rounded-xl shadow-sm text-center text-[10px] border border-black/5">
									Reply STOP to opt out
								</button>
							</div>
						</div>
					</div>
				</div>
			</div>
		</div>
	{/if}

	<!-- Step 3: Audience Segmentation & Batch Selection -->
	{#if currentStep === 3}
		<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-5">
			<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
				<h3 class="text-base font-semibold text-foreground flex items-center gap-2">
					<Icon name="users" class="w-5 h-5 text-primary" />
					Step 3: Audience Filter & Batch Selection
				</h3>

				<!-- Fresh Contacts Toggle (Clean, Sleek) -->
				<button
					type="button"
					onclick={handleUniqueNumbersToggle}
					class="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-xl border text-xs font-semibold transition-all cursor-pointer {onlyUniqueNumbers ? 'bg-emerald-50 dark:bg-emerald-950 border-emerald-300 dark:border-emerald-700 text-emerald-700 dark:text-emerald-300 shadow-sm' : 'bg-background border-border text-muted-foreground hover:text-foreground'}"
				>
					<Icon name={onlyUniqueNumbers ? 'sparkles' : 'circle'} class="w-4 h-4 {onlyUniqueNumbers ? 'text-emerald-600 dark:text-emerald-400' : 'text-muted-foreground'}" />
					<span>Show Only Fresh Contacts (Not used in any campaign)</span>
					{#if onlyUniqueNumbers}
						<span class="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
					{/if}
				</button>
			</div>

			<!-- Mode Switcher: Bulk vs Batch/Handpicked -->
			<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
				<button
					type="button"
					onclick={() => handleSelectionModeChange('ALL_FILTERED')}
					class="text-left p-3.5 rounded-xl border-2 transition-all flex items-center gap-3 cursor-pointer {selectionMode === 'ALL_FILTERED' ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'border-border bg-background hover:bg-muted/30'}"
				>
					<div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 {selectionMode === 'ALL_FILTERED' ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}">
						<Icon name="users" class="w-4 h-4" />
					</div>
					<div class="flex-1 min-w-0">
						<div class="text-xs font-bold text-foreground flex items-center justify-between">
							<span>Smart Bulk Broadcast</span>
							{#if selectionMode === 'ALL_FILTERED'}
								<span class="text-[10px] bg-primary/20 text-primary font-semibold px-1.5 py-0.2 rounded">Active</span>
							{/if}
						</div>
					</div>
				</button>

				<button
					type="button"
					onclick={() => handleSelectionModeChange('MANUAL')}
					class="text-left p-3.5 rounded-xl border-2 transition-all flex items-center gap-3 cursor-pointer {selectionMode === 'MANUAL' ? 'border-primary bg-primary/5 ring-1 ring-primary' : 'border-border bg-background hover:bg-muted/30'}"
				>
					<div class="w-8 h-8 rounded-lg flex items-center justify-center shrink-0 {selectionMode === 'MANUAL' ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}">
						<Icon name="check-square" class="w-4 h-4" />
					</div>
					<div class="flex-1 min-w-0">
						<div class="text-xs font-bold text-foreground flex items-center justify-between">
							<span>Batch / Handpicked Contacts</span>
							{#if selectionMode === 'MANUAL'}
								<span class="text-[10px] bg-emerald-600 text-white font-semibold px-1.5 py-0.2 rounded">Active</span>
							{/if}
						</div>
					</div>
				</button>
			</div>

			<!-- Filter Bar (Similar to crm-contacts) -->
			<div class="space-y-3 pt-1">
				<div class="text-xs font-semibold text-foreground flex items-center justify-between">
					<div class="flex items-center gap-2">
						<Icon name="filter" class="w-3.5 h-3.5 text-primary" />
						<span>Filter Contacts</span>
					</div>
					{#if hasActiveFilters}
						<button
							type="button"
							onclick={clearFilters}
							class="text-[11px] text-red-600 hover:text-red-700 font-medium flex items-center gap-1 cursor-pointer transition-colors"
						>
							<Icon name="x" class="w-3 h-3" />
							<span>Reset Filters</span>
						</button>
					{/if}
				</div>

				<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">Search</label>
						<div class="relative">
							<Icon name="search" class="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-muted-foreground" />
							<input
								type="text"
								bind:value={pickerSearch}
								oninput={handleSearchInput}
								placeholder="Name, mobile, city..."
								class="w-full pl-8 pr-2.5 py-1.5 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8"
							/>
						</div>
					</div>

					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">Contact Type</label>
						<select
							bind:value={filterContactType}
							onchange={handleFilterChanged}
							class="w-full px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8 cursor-pointer"
						>
							<option value="">All Types ({filterOptions.contactTypes.length})</option>
							{#each filterOptions.contactTypes as t}
								<option value={t}>{t}</option>
							{/each}
						</select>
					</div>

					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">Category</label>
						<select
							bind:value={filterCategory}
							onchange={handleFilterChanged}
							class="w-full px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8 cursor-pointer"
						>
							<option value="">All Categories ({filterOptions.contactCategories.length})</option>
							{#each filterOptions.contactCategories as cat}
								<option value={cat}>{cat}</option>
							{/each}
						</select>
					</div>

					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">Depot / RC</label>
						<select
							bind:value={filterRespCenter}
							onchange={handleFilterChanged}
							class="w-full px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8 cursor-pointer"
						>
							<option value="">All Depots ({filterOptions.respCenters.length})</option>
							{#each filterOptions.respCenters as rc}
								<option value={rc}>{rc}</option>
							{/each}
						</select>
					</div>

					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">State</label>
						<select
							bind:value={filterState}
							onchange={handleFilterChanged}
							class="w-full px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8 cursor-pointer"
						>
							<option value="">All States ({filterOptions.states.length})</option>
							{#each filterOptions.states as s}
								<option value={s}>{s}</option>
							{/each}
						</select>
					</div>

					<div class="space-y-1">
						<label class="text-[11px] font-medium text-foreground">City</label>
						<select
							bind:value={filterCity}
							onchange={handleFilterChanged}
							class="w-full px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-8 cursor-pointer"
						>
							<option value="">All Cities ({filterOptions.cities.length})</option>
							{#each filterOptions.cities as city}
								<option value={city}>{city}</option>
							{/each}
						</select>
					</div>
				</div>
			</div>

			<!-- Batch Size & One Click Selector Toolbar -->
			<div class="bg-muted/40 border border-border rounded-xl p-3.5 flex flex-col md:flex-row items-center justify-between gap-3">
				<!-- Batch Size Selector -->
				<div class="flex items-center gap-2 flex-wrap w-full md:w-auto">
					<span class="text-xs font-semibold text-foreground">Batch Size:</span>
					{#each [100, 300, 500, 1000] as size}
						<button
							type="button"
							class="px-2.5 py-1 text-xs rounded-lg font-medium border transition-colors cursor-pointer {pickerTake === size ? 'bg-primary text-primary-foreground border-primary font-semibold' : 'bg-background border-border text-muted-foreground hover:text-foreground'}"
							onclick={() => handleBatchSizeChange(size)}
						>
							{size}
						</button>
					{/each}
					<div class="flex items-center gap-1.5 ml-1">
						<span class="text-[11px] text-muted-foreground">Custom:</span>
						<input
							type="number"
							min="1"
							max="5000"
							bind:value={customBatchInput}
							onchange={handleCustomBatchChange}
							class="w-16 px-2 py-1 text-xs bg-background border border-input rounded-lg focus:outline-none focus:ring-1 focus:ring-primary h-7 text-center font-mono"
						/>
					</div>
				</div>

				<!-- One Click Selector Full Batch -->
				<div class="flex items-center gap-2 w-full md:w-auto justify-end">
					<Button
						size="sm"
						class="gap-1.5 h-8 text-xs font-semibold cursor-pointer {isFullBatchSelected ? 'bg-emerald-600 hover:bg-emerald-700 text-white' : 'bg-primary text-primary-foreground'}"
						onclick={toggleSelectBatch}
						disabled={pickerLoading || pickerContacts.length === 0}
					>
						<Icon name={isFullBatchSelected ? 'check-check' : 'zap'} class="w-3.5 h-3.5" />
						<span>{isFullBatchSelected ? `Full Batch Selected (${selectedContactIds.size})` : `Select Full Batch (${Math.min(pickerTake, pickerContacts.length)})`}</span>
					</Button>

					{#if selectedContactIds.size > 0}
						<Button
							variant="outline"
							size="sm"
							class="h-8 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 border-red-200 dark:border-red-900 cursor-pointer"
							onclick={clearAllSelection}
						>
							Clear ({selectedContactIds.size})
						</Button>
					{/if}
				</div>
			</div>

			<!-- Audience Estimator Summary Box -->
			<div class="bg-muted/30 border border-border rounded-xl p-4 space-y-3">
				<div class="flex items-center justify-between">
					<div class="flex items-center gap-2">
						<span class="text-xs font-bold uppercase tracking-wider text-foreground">
							{selectionMode === 'MANUAL' ? 'Selected Batch Summary' : 'Bulk Audience Estimate'}
						</span>
						{#if selectionMode === 'MANUAL'}
							<span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
								{selectedContactIds.size} Selected
							</span>
						{/if}
						{#if onlyUniqueNumbers}
							<span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
								Fresh Only
							</span>
						{/if}
					</div>
					<Button
						variant="ghost"
						size="sm"
						class="h-7 text-xs gap-1 text-muted-foreground cursor-pointer"
						onclick={runAudienceEstimation}
						disabled={estimatingAudience}
					>
						<Icon name="refresh-cw" class="w-3.5 h-3.5 {estimatingAudience ? 'animate-spin' : ''}" />
						Recalculate
					</Button>
				</div>

				<div class="grid grid-cols-2 sm:grid-cols-4 gap-3 text-center">
					<div class="bg-background border border-border rounded-xl p-3">
						<div class="text-[11px] text-muted-foreground">Matching Contacts</div>
						<div class="text-lg font-bold mt-0.5">
							{audienceEstimate.totalMatchingContacts.toLocaleString()}
						</div>
					</div>

					<div class="bg-background border border-border rounded-xl p-3">
						<div class="text-[11px] text-muted-foreground">Valid WhatsApp</div>
						<div class="text-lg font-bold text-blue-600 mt-0.5">
							{audienceEstimate.withValidPhone.toLocaleString()}
						</div>
					</div>

					<div class="bg-background border border-border rounded-xl p-3">
						<div class="text-[11px] text-muted-foreground">Suppressed / Excluded</div>
						<div class="text-lg font-bold text-red-500 mt-0.5">
							-{(audienceEstimate.suppressedCount + (onlyUniqueNumbers ? (audienceEstimate.previouslyCampaignedCount || 0) : 0)).toLocaleString()}
						</div>
					</div>

					<div class="bg-emerald-50 dark:bg-emerald-950/40 border border-emerald-200 dark:border-emerald-800 rounded-xl p-3">
						<div class="text-[11px] font-medium text-emerald-800 dark:text-emerald-300">
							{selectionMode === 'MANUAL' ? 'Targeted in Batch' : 'Target Recipients'}
						</div>
						<div class="text-xl font-black text-emerald-600 dark:text-emerald-400 mt-0.5">
							{audienceEstimate.eligibleRecipients.toLocaleString()}
						</div>
					</div>
				</div>
			</div>

			<!-- Interactive Contact Picker Table -->
			<div class="border border-border rounded-xl overflow-hidden bg-card space-y-0">
				<!-- Table Control Bar -->
				<div class="p-3 bg-muted/40 border-b border-border flex items-center justify-between gap-3 text-xs">
					<div class="flex items-center gap-2">
						<span class="font-semibold text-foreground">
							{onlyUniqueNumbers ? 'Fresh CRM Contacts' : 'All CRM Contacts'}
						</span>
						<span class="text-muted-foreground">({pickerTotalCount.toLocaleString()} total)</span>
					</div>

					<div class="flex items-center gap-2">
						{#if isFullBatchSelected}
							<span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300">
								All {pickerContacts.length} in Batch Selected
							</span>
						{:else if selectedContactIds.size > 0}
							<span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-primary/10 text-primary">
								{selectedContactIds.size} Selected
							</span>
						{/if}
					</div>
				</div>

				<!-- Table Element -->
				{#if pickerLoading}
					<div class="p-10 text-center text-xs text-muted-foreground flex items-center justify-center gap-2">
						<Icon name="loader-2" class="w-4 h-4 animate-spin text-primary" />
						<span>Loading contacts...</span>
					</div>
				{:else if pickerContacts.length === 0}
					<div class="p-10 text-center text-xs text-muted-foreground">
						No contacts found matching the filters.
					</div>
				{:else}
					<div class="overflow-x-auto">
						<table class="w-full text-xs text-left border-collapse">
							<thead class="bg-muted/50 border-b border-border text-muted-foreground uppercase text-[10px]">
								<tr>
									<th class="py-2 px-3 w-10 text-center">
										<input
											type="checkbox"
											checked={pickerContacts.length > 0 && pickerContacts.every((c) => selectedContactIds.has(c.id))}
											onchange={toggleSelectBatch}
											class="rounded border-border text-primary focus:ring-primary cursor-pointer"
										/>
									</th>
									<th class="py-2.5 px-3 font-semibold">Contact Name</th>
									<th class="py-2.5 px-3 font-semibold">Company</th>
									<th class="py-2.5 px-3 font-semibold">Clean WhatsApp Mobile</th>
									<th class="py-2.5 px-3 font-semibold">Location</th>
									<th class="py-2.5 px-3 font-semibold">Type / Category / Depot</th>
								</tr>
							</thead>
							<tbody class="divide-y divide-border">
								{#each pickerContacts as c (c.id)}
									<tr
										class="hover:bg-muted/30 transition-colors cursor-pointer {selectedContactIds.has(c.id) ? 'bg-primary/5' : ''}"
										onclick={() => toggleSelectContact(c.id)}
									>
										<td class="py-2 px-3 text-center" onclick={(e) => e.stopPropagation()}>
											<input
												type="checkbox"
												checked={selectedContactIds.has(c.id)}
												onchange={() => toggleSelectContact(c.id)}
												class="rounded border-border text-primary focus:ring-primary cursor-pointer"
											/>
										</td>

										<td class="py-2.5 px-3">
											<div class="font-medium text-foreground flex items-center gap-1.5">
												<span>{c.fullName}</span>
												{#if c.qualityScore != null && c.qualityScore > 0}
													<span class="text-[9px] px-1.5 py-0.2 rounded bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-300 font-semibold border border-amber-200 dark:border-amber-800">
														★ {c.qualityScore}
													</span>
												{/if}
											</div>
										</td>

										<td class="py-2.5 px-3 text-muted-foreground">
											{c.companyName || '-'}
										</td>

										<td class="py-2.5 px-3">
											{#if c.cleanWhatsappPhone}
												<div class="space-y-1">
													<div class="flex items-center gap-1.5 font-mono text-[11px] text-foreground">
														<span class="text-emerald-600 font-bold">+{c.cleanWhatsappPhone}</span>
														<span class="text-[9px] bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 px-1.5 py-0.2 rounded font-sans font-medium flex items-center gap-0.5">
															<Icon name="check" class="w-2.5 h-2.5" />
															Verified
														</span>
													</div>
													<div>
														{#if c.isUniqueNumber}
															<span class="inline-flex items-center gap-1 text-[9px] px-1.5 py-0.2 rounded bg-emerald-50 dark:bg-emerald-950/60 text-emerald-700 dark:text-emerald-300 font-semibold border border-emerald-200 dark:border-emerald-800">
																<Icon name="sparkles" class="w-2.5 h-2.5" />
																Fresh (New)
															</span>
														{:else}
															<span class="inline-flex items-center gap-1 text-[9px] px-1.5 py-0.2 rounded bg-amber-50 dark:bg-amber-950/60 text-amber-700 dark:text-amber-300 font-medium border border-amber-200 dark:border-amber-800">
																Used in {c.previousCampaignCount || 1} campaign(s)
															</span>
														{/if}
													</div>
												</div>
											{:else}
												<span class="text-red-500 font-mono text-[11px]">{c.mobileNo || c.mobileNo2 || 'No Phone'}</span>
											{/if}
										</td>

										<td class="py-2.5 px-3 text-muted-foreground">
											{[c.city, c.state].filter(Boolean).join(', ') || '-'}
										</td>

										<td class="py-2.5 px-3 text-muted-foreground">
											<div class="flex items-center gap-1 flex-wrap">
												{#if c.contactType}
													<span class="px-1.5 py-0.5 rounded bg-primary/10 text-primary text-[10px] font-medium">
														{c.contactType}
													</span>
												{/if}
												<span class="px-1.5 py-0.5 rounded bg-muted text-[10px]">
													{c.contactCategory || 'Contact'}
												</span>
											</div>
											{#if c.respCenter}
												<div class="text-[10px] text-muted-foreground mt-0.5">{c.respCenter}</div>
											{/if}
										</td>
									</tr>
								{/each}
							</tbody>
						</table>
					</div>

					<!-- Batch Pagination Controls -->
					<div class="p-3 bg-muted/20 border-t border-border flex items-center justify-between text-xs">
						<div class="text-muted-foreground">
							Showing {pickerSkip + 1} - {Math.min(pickerSkip + pickerContacts.length, pickerTotalCount)} of {pickerTotalCount.toLocaleString()} contacts
						</div>

						<div class="flex items-center gap-2">
							<Button
								variant="outline"
								size="sm"
								class="h-7 text-xs cursor-pointer"
								disabled={pickerSkip === 0 || pickerLoading}
								onclick={goToPreviousPickerPage}
							>
								<Icon name="chevron-left" class="w-3.5 h-3.5 mr-1" />
								Previous Batch
							</Button>
							<Button
								variant="outline"
								size="sm"
								class="h-7 text-xs cursor-pointer"
								disabled={pickerSkip + pickerTake >= pickerTotalCount || pickerLoading}
								onclick={goToNextPickerPage}
							>
								Next Batch
								<Icon name="chevron-right" class="w-3.5 h-3.5 ml-1" />
							</Button>
						</div>
					</div>
				{/if}
			</div>

			<div class="flex items-center justify-between pt-4 border-t border-border">
				<Button variant="outline" size="sm" onclick={() => (currentStep = 2)}>
					<Icon name="arrow-left" class="w-4 h-4 mr-1" />
					Back
				</Button>
				<Button size="sm" class="gap-1.5" onclick={() => (currentStep = 4)}>
					<span>Next: Quality Test & Schedule</span>
					<Icon name="arrow-right" class="w-4 h-4" />
				</Button>
			</div>
		</div>
	{/if}

	<!-- Step 4: Quality Testing & Scheduling -->
	{#if currentStep === 4}
		<div class="space-y-6">
			<!-- Small Group Test Dispatcher Box -->
			<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-4">
				<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-border pb-3">
					<div>
						<h3 class="text-base font-semibold text-foreground flex items-center gap-2">
							<Icon name="send" class="w-5 h-5 text-primary" />
							Small-Group Quality Testing
						</h3>
					</div>
				</div>

				<div class="space-y-3">
					<div class="space-y-1.5">
						<div class="flex items-center justify-between">
							<label class="text-xs font-medium text-foreground">Test Phone Numbers (comma or newline separated)</label>
							<button
								type="button"
								class="text-[11px] text-primary hover:underline font-medium flex items-center gap-1 cursor-pointer"
								onclick={() => {
									const sender = accountStatus?.displayPhoneNumber ? accountStatus.displayPhoneNumber.replace(/\D/g, '') : '919880334191';
									testNumbersInput = testNumbersInput ? `${testNumbersInput}, ${sender}` : sender;
								}}
							>
								<Icon name="plus" class="w-3 h-3" />
								Add Verified Sender Phone
							</button>
						</div>
						<textarea
							bind:value={testNumbersInput}
							rows={2}
							placeholder="e.g. 919880334191, 919820012345"
							class="w-full p-2.5 text-xs font-mono bg-background border border-input rounded-xl focus:outline-none focus:ring-1 focus:ring-primary"
						></textarea>
					</div>

					<div class="flex items-center gap-3">
						<Button
							size="sm"
							class="gap-1.5 bg-blue-600 hover:bg-blue-700 text-white font-semibold cursor-pointer"
							onclick={handleSendGroupTest}
							disabled={sendingGroupTest}
						>
							<Icon name="send" class="w-3.5 h-3.5 {sendingGroupTest ? 'animate-spin' : ''}" />
							<span>{sendingGroupTest ? 'Dispatching...' : 'Dispatch Test Sample'}</span>
						</Button>
					</div>

					<!-- Group Test Results Ledger -->
					{#if groupTestResults.length > 0}
						<div class="mt-4 border border-border rounded-xl overflow-hidden bg-muted/20">
							<div class="p-2.5 bg-muted/50 border-b border-border text-xs font-semibold flex items-center justify-between">
								<span>Group Test Delivery Report</span>
								<span class="text-[11px] text-muted-foreground">
									{groupTestResults.filter((r) => r.success).length} of {groupTestResults.length} delivered
								</span>
							</div>

							<div class="divide-y border-border">
								{#each groupTestResults as res}
									<div class="p-2.5 flex items-center justify-between text-xs">
										<div class="flex items-center gap-2 font-mono">
											{#if res.success}
												<span class="w-5 h-5 rounded-full bg-emerald-100 dark:bg-emerald-950 text-emerald-600 flex items-center justify-center font-bold text-[10px]">✓</span>
											{:else}
												<span class="w-5 h-5 rounded-full bg-red-100 dark:bg-red-950 text-red-600 flex items-center justify-center font-bold text-[10px]">✕</span>
											{/if}
											<span class="font-medium text-foreground">+{res.phoneNumber}</span>
										</div>

										<div>
											{#if res.success}
												<div class="flex items-center gap-2">
													<span class="px-2 py-0.5 rounded-full text-[10px] bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-semibold">
														Delivered to WhatsApp
													</span>
													{#if res.wamid}
														<span class="text-[10px] font-mono text-muted-foreground opacity-75">
															{res.wamid.substring(0, 16)}...
														</span>
													{/if}
												</div>
											{:else}
												<div class="text-right">
													<span class="px-2 py-0.5 rounded-full text-[10px] bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300 font-semibold">
														Delivery Failed
													</span>
													<div class="text-[10px] text-red-600 dark:text-red-400 mt-0.5">
														{res.errorMessage || 'Undeliverable number'}
													</div>
												</div>
											{/if}
										</div>
									</div>
								{/each}
							</div>
						</div>
					{/if}
				</div>
			</div>

			<!-- Scheduling Options -->
			<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
				<h3 class="text-base font-semibold text-foreground flex items-center gap-2">
					<Icon name="calendar" class="w-5 h-5 text-primary" />
					Campaign Dispatch Schedule
				</h3>

				<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
					<label class="flex items-center gap-3 p-4 border rounded-xl cursor-pointer transition-colors {scheduleOption === 'NOW' ? 'border-primary bg-primary/5' : 'border-border'}">
						<input type="radio" value="NOW" bind:group={scheduleOption} class="text-primary" />
						<div>
							<div class="font-semibold text-xs text-foreground">Send Immediately</div>
						</div>
					</label>

					<label class="flex items-center gap-3 p-4 border rounded-xl cursor-pointer transition-colors {scheduleOption === 'LATER' ? 'border-primary bg-primary/5' : 'border-border'}">
						<input type="radio" value="LATER" bind:group={scheduleOption} class="text-primary" />
						<div>
							<div class="font-semibold text-xs text-foreground">Schedule for Later</div>
						</div>
					</label>
				</div>

				{#if scheduleOption === 'LATER'}
					<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
						<div class="space-y-1.5">
							<label class="text-xs font-medium text-foreground">Date</label>
							<Input type="date" bind:value={scheduledDate} class="text-xs" />
						</div>
						<div class="space-y-1.5">
							<label class="text-xs font-medium text-foreground">Time (Depot Local Time)</label>
							<Input type="time" bind:value={scheduledTime} class="text-xs" />
						</div>
					</div>
				{/if}

				<!-- Summary Card -->
				<div class="border-t border-border pt-4 flex flex-col sm:flex-row items-center justify-between gap-4">
					<div>
						<div class="text-xs font-bold text-foreground flex items-center gap-2 flex-wrap">
							<span>Ready to broadcast to {audienceEstimate.eligibleRecipients.toLocaleString()} WhatsApp recipients</span>
							{#if selectionMode === 'MANUAL'}
								<span class="px-2 py-0.5 rounded text-[10px] bg-primary/10 text-primary font-semibold">
									Batch Mode ({selectedContactIds.size})
								</span>
							{/if}
							{#if onlyUniqueNumbers}
								<span class="px-2 py-0.5 rounded text-[10px] bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300 font-semibold">
									Fresh Numbers Only
								</span>
							{/if}
						</div>
						<div class="text-[11px] text-muted-foreground mt-0.5">
							Estimated Cost: ₹{(audienceEstimate.eligibleRecipients * 0.80).toFixed(2)} (@ ₹0.80 per delivered message)
						</div>
					</div>

					<div class="flex items-center gap-2">
						<Button variant="outline" size="sm" onclick={() => (currentStep = 3)}>
							<Icon name="arrow-left" class="w-4 h-4 mr-1" />
							Back
						</Button>
						<Button
							size="sm"
							class="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white font-semibold shadow-sm cursor-pointer"
							onclick={handleLaunchCampaign}
							disabled={submitting || audienceEstimate.eligibleRecipients === 0}
						>
							<Icon name="check-circle-2" class="w-4 h-4 {submitting ? 'animate-spin' : ''}" />
							<span>{submitting ? 'Launching...' : scheduleOption === 'NOW' ? 'Launch Campaign Now' : 'Confirm & Schedule'}</span>
						</Button>
					</div>
				</div>
			</div>
		</div>
	{/if}
</div>
