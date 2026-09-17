<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';

	let currentStep = $state(1);

	// Step 1: Details
	let name = $state('');
	let subject = $state('');
	let previewText = $state('');
	let fromName = $state('Tyresoles Fleet Team');
	let fromEmail = $state('updates@tyresoles.in');
	let replyToEmail = $state('sales@tyresoles.in');
	let contentType = $state('Html'); // Html or PlainText

	// Step 2: Audience Filter
	let selectedCategory = $state('');
	let selectedState = $state('');
	let selectedCity = $state('');
	let minQualityScore = $state<number | null>(null);
	let audienceEstimate = $state({
		totalMatchingContacts: 0,
		withValidEmail: 0,
		suppressedCount: 0,
		eligibleRecipients: 0
	});
	let estimatingAudience = $state(false);

	// Step 3: Content & Spam
	let bodyHtml = $state(
`<div style="font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;">
    <h2 style="color: #1e3a8a;">Tyresoles Commercial Fleet Tyre Care</h2>
    <p>Dear {{FullName}},</p>
    <p>We are reaching out from Tyresoles regarding commercial tyre retreading and mileage optimization for <strong>{{CompanyName}}</strong>.</p>
    <p>Our premium retreads deliver up to 85% of new tyre mileage at only a fraction of the cost, significantly lowering your fleet's cost-per-kilometer (CPK).</p>
    <div style="margin: 24px 0; text-align: center;">
        <a href="https://tyresoles.in" style="background-color: #2563eb; color: #ffffff; padding: 12px 24px; text-decoration: none; border-radius: 6px; font-weight: bold; display: inline-block;">Schedule Fleet Inspection</a>
    </div>
    <p>Our technical team is ready to inspect your tyres at your local depot in {{City}}.</p>
    <p>Best regards,<br><strong>Tyresoles Fleet Solutions</strong><br>Tyresoles (India) Pvt. Ltd.</p>
    <hr style="border: none; border-top: 1px solid #e2e8f0; margin: 24px 0;" />
    <p style="font-size: 11px; color: #64748b; text-align: center;">
        Tyresoles (India) Pvt. Ltd., Mumbai, India.<br>
        To manage your preferences or unsubscribe from future service updates, <a href="{{UnsubscribeLink}}" style="color: #64748b; text-decoration: underline;">click here to unsubscribe</a>.
    </p>
</div>`
	);
	let bodyText = $state('');
	let spamScore = $state<{ score: number; rating: string; warnings: string[]; recommendations: string[] }>({
		score: 100,
		rating: 'Excellent',
		warnings: [],
		recommendations: []
	});
	let checkingSpam = $state(false);
	let previewActive = $state(false);

	// Step 4: Schedule & Test
	let testRecipientEmail = $state('');
	let sendingTest = $state(false);
	let scheduleOption = $state('NOW'); // NOW or LATER
	let scheduledDate = $state('');
	let scheduledTime = $state('10:00');
	let submitting = $state(false);

	// GraphQL Definitions
	const ESTIMATE_AUDIENCE = buildQuery`
		query EstimateAudience($filter: AudienceFilterInput) {
			estimateCampaignAudience(filter: $filter) {
				totalMatchingContacts
				withValidEmail
				suppressedCount
				eligibleRecipients
			}
		}
	`;

	const CHECK_SPAM = buildQuery`
		query CheckSpam($subject: String!, $bodyHtml: String, $bodyText: String) {
			checkEmailSpamScore(subject: $subject, bodyHtml: $bodyHtml, bodyText: $bodyText) {
				score
				rating
				warnings
				recommendations
			}
		}
	`;

	const SAVE_CAMPAIGN = buildMutation`
		mutation SaveCampaign($input: SaveCrmEmailCampaignInput!) {
			saveCrmEmailCampaign(input: $input) {
				id
				name
				status
			}
		}
	`;

	const SCHEDULE_CAMPAIGN = buildMutation`
		mutation ScheduleCampaign($id: UUID!, $scheduledAt: DateTime) {
			scheduleCrmEmailCampaign(campaignId: $id, scheduledAt: $scheduledAt) {
				id
				status
				totalRecipients
			}
		}
	`;

	const SEND_TEST = buildMutation`
		mutation SendTest($id: UUID!, $email: String!) {
			sendTestCampaignEmail(campaignId: $id, targetEmail: $email)
		}
	`;

	async function updateAudienceEstimate() {
		estimatingAudience = true;
		try {
			const res = await graphqlQuery<{ estimateCampaignAudience: any }>(ESTIMATE_AUDIENCE, {
				variables: {
					filter: {
						contactCategory: selectedCategory || null,
						state: selectedState || null,
						city: selectedCity || null,
						minQualityScore: minQualityScore || null
					}
				}
			});
			if (res.success && res.data?.estimateCampaignAudience) {
				audienceEstimate = res.data.estimateCampaignAudience;
			}
		} catch (e) {
			// silent fallback
		} finally {
			estimatingAudience = false;
		}
	}

	async function runSpamCheck() {
		checkingSpam = true;
		try {
			const res = await graphqlQuery<{ checkEmailSpamScore: any }>(CHECK_SPAM, {
				variables: {
					subject: subject || 'No Subject',
					bodyHtml: contentType === 'Html' ? bodyHtml : null,
					bodyText: contentType === 'PlainText' ? bodyText : null
				}
			});
			if (res.success && res.data?.checkEmailSpamScore) {
				spamScore = res.data.checkEmailSpamScore;
			}
		} catch (e) {
			// silent fallback
		} finally {
			checkingSpam = false;
		}
	}

	function insertTag(tag: string) {
		if (contentType === 'Html') {
			bodyHtml += tag;
		} else {
			bodyText += tag;
		}
		runSpamCheck();
	}

	onMount(() => {
		updateAudienceEstimate();
		runSpamCheck();
	});

	async function handleSendTest() {
		if (!testRecipientEmail || !testRecipientEmail.includes('@')) {
			toast.error('Please enter a valid recipient email for testing.');
			return;
		}

		sendingTest = true;
		try {
			// First save draft
			const saveRes = await graphqlMutation<{ saveCrmEmailCampaign: { id: string } }>(SAVE_CAMPAIGN, {
				variables: {
					input: {
						name: name || 'Test Campaign',
						subject: subject || 'Test Subject',
						previewText,
						fromName,
						fromEmail,
						replyToEmail,
						contentType,
						bodyHtml: contentType === 'Html' ? bodyHtml : null,
						bodyText: contentType === 'PlainText' ? bodyText : null
					}
				}
			});

			if (saveRes.success && saveRes.data?.saveCrmEmailCampaign?.id) {
				const campaignId = saveRes.data.saveCrmEmailCampaign.id;
				const testRes = await graphqlMutation<{ sendTestCampaignEmail: boolean }>(SEND_TEST, {
					variables: {
						id: campaignId,
						email: testRecipientEmail.trim()
					}
				});

				if (testRes.success && testRes.data?.sendTestCampaignEmail) {
					toast.success(`Test email dispatched successfully to ${testRecipientEmail}!`);
				} else {
					toast.error('Test send failed. Please verify SMTP settings in backend.');
				}
			}
		} catch (e: any) {
			toast.error(e.message || 'Error dispatching test email');
		} finally {
			sendingTest = false;
		}
	}

	async function handleFinalSubmit() {
		if (!name.trim()) {
			toast.error('Campaign name is required.');
			currentStep = 1;
			return;
		}
		if (!subject.trim()) {
			toast.error('Subject line is required.');
			currentStep = 1;
			return;
		}

		submitting = true;
		try {
			// 1. Save Campaign
			const saveRes = await graphqlMutation<{ saveCrmEmailCampaign: { id: string } }>(SAVE_CAMPAIGN, {
				variables: {
					input: {
						name: name.trim(),
						subject: subject.trim(),
						previewText: previewText.trim() || null,
						fromName: fromName.trim(),
						fromEmail: fromEmail.trim(),
						replyToEmail: replyToEmail.trim() || null,
						contentType,
						bodyHtml: contentType === 'Html' ? bodyHtml : null,
						bodyText: contentType === 'PlainText' ? bodyText : null,
						audienceFilter: {
							contactCategory: selectedCategory || null,
							state: selectedState || null,
							city: selectedCity || null,
							minQualityScore: minQualityScore || null
						}
					}
				}
			});

			if (!saveRes.success || !saveRes.data?.saveCrmEmailCampaign?.id) {
				throw new Error('Failed to save campaign record.');
			}

			const campaignId = saveRes.data.saveCrmEmailCampaign.id;

			// 2. Schedule or Send Now
			let scheduleTimeUtc: string | null = null;
			if (scheduleOption === 'LATER' && scheduledDate) {
				scheduleTimeUtc = new Date(`${scheduledDate}T${scheduledTime}:00`).toISOString();
			}

			const schedRes = await graphqlMutation<{ scheduleCrmEmailCampaign: { id: string; totalRecipients: number } }>(SCHEDULE_CAMPAIGN, {
				variables: {
					id: campaignId,
					scheduledAt: scheduleTimeUtc
				}
			});

			if (schedRes.success) {
				const count = schedRes.data?.scheduleCrmEmailCampaign?.totalRecipients ?? 0;
				toast.success(`Campaign scheduled! Queued ${count} eligible contacts.`);
				goto('/crm-campaigns');
			}
		} catch (e: any) {
			toast.error(e.message || 'Failed to finalize campaign schedule.');
		} finally {
			submitting = false;
		}
	}
</script>

<svelte:head>
	<title>New Email Campaign | Tyresoles CRM</title>
</svelte:head>

<PageHeading
	backHref="/crm-campaigns"
	backLabel="Back to Campaigns"
	icon="send"
	title="Create Email Campaign"
	description="Step-by-step wizard to craft, target, and launch high-converting B2B campaigns"
/>

<div class="p-6 max-w-5xl mx-auto space-y-8">
	<!-- Stepper Indicator -->
	<div class="grid grid-cols-4 gap-2 text-center text-xs font-semibold">
		{#each [
			{ step: 1, title: '1. Details', desc: 'Sender & Subject' },
			{ step: 2, title: '2. Audience', desc: 'Target Filtering' },
			{ step: 3, title: '3. Content', desc: 'Editor & Spam Check' },
			{ step: 4, title: '4. Schedule', desc: 'Test & Launch' }
		] as s}
			<button
				type="button"
				onclick={() => (currentStep = s.step)}
				class="p-3 rounded-xl border transition-all {currentStep === s.step
					? 'bg-primary text-primary-foreground border-primary shadow-sm'
					: currentStep > s.step
						? 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20'
						: 'bg-card text-muted-foreground border-border/50'}"
			>
				<div class="font-bold text-sm">{s.title}</div>
				<div class="text-[11px] opacity-80">{s.desc}</div>
			</button>
		{/each}
	</div>

	<!-- STEP 1: CAMPAIGN DETAILS -->
	{#if currentStep === 1}
		<div class="p-6 bg-card border border-border/70 rounded-2xl shadow-sm space-y-5">
			<div class="space-y-1">
				<h3 class="text-base font-bold text-foreground">Campaign Identity & Subject</h3>
				<p class="text-xs text-muted-foreground">Setup sender details and email subject lines.</p>
			</div>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
				<div class="space-y-1.5 md:col-span-2">
					<label for="campaign-name" class="text-xs font-medium text-foreground">Campaign Name (Internal)</label>
					<Input id="campaign-name" bind:value={name} placeholder="e.g. Q3 Commercial Fleet Retreading Announcement" />
				</div>

				<div class="space-y-1.5 md:col-span-2">
					<label for="subject-line" class="text-xs font-medium text-foreground">Subject Line</label>
					<Input id="subject-line" bind:value={subject} oninput={runSpamCheck} placeholder="e.g. Reducing CPK for your fleet: Tyresoles Retreading Solutions" />
					<p class="text-[11px] text-muted-foreground">Tip: Keep it clear and specific. Avoid ALL CAPS and multiple exclamation marks.</p>
				</div>

				<div class="space-y-1.5 md:col-span-2">
					<label for="preview-text" class="text-xs font-medium text-foreground">Preview Text / Preheader (Optional)</label>
					<Input id="preview-text" bind:value={previewText} placeholder="e.g. Extend casing life by up to 2.5x with certified retreads." />
				</div>

				<div class="space-y-1.5">
					<label for="from-name" class="text-xs font-medium text-foreground">From Name</label>
					<Input id="from-name" bind:value={fromName} placeholder="Tyresoles Fleet Team" />
				</div>

				<div class="space-y-1.5">
					<label for="from-email" class="text-xs font-medium text-foreground">From Email Address</label>
					<Input id="from-email" bind:value={fromEmail} placeholder="updates@tyresoles.in" />
					<p class="text-[11px] text-muted-foreground">Must be on verified domain: <strong>@tyresoles.in</strong></p>
				</div>

				<div class="space-y-1.5">
					<label for="reply-to" class="text-xs font-medium text-foreground">Reply-To Email</label>
					<Input id="reply-to" bind:value={replyToEmail} placeholder="sales@tyresoles.in" />
				</div>

				<div class="space-y-1.5">
					<label for="content-format" class="text-xs font-medium text-foreground">Content Format</label>
					<select id="content-format" bind:value={contentType} class="w-full h-9 px-3 text-xs rounded-md border border-input bg-background">
						<option value="Html">Rich HTML Layout (Brochure / Newsletter)</option>
						<option value="PlainText">Plain-Text B2B Direct Outreach (High Reply Rate)</option>
					</select>
				</div>
			</div>

			<div class="flex justify-end pt-4 border-t border-border/50">
				<Button onclick={() => (currentStep = 2)} class="gap-1.5">
					Next: Select Audience
					<Icon name="arrow-right" class="w-4 h-4" />
				</Button>
			</div>
		</div>
	{/if}

	<!-- STEP 2: AUDIENCE FILTER -->
	{#if currentStep === 2}
		<div class="p-6 bg-card border border-border/70 rounded-2xl shadow-sm space-y-6">
			<div class="space-y-1">
				<h3 class="text-base font-bold text-foreground">Target Audience Segmentation</h3>
				<p class="text-xs text-muted-foreground">Filter contacts from your live Tyresoles CRM database.</p>
			</div>

			<div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
				<div class="space-y-1.5">
					<label for="contact-category" class="text-xs font-medium text-foreground">Contact Category</label>
					<select id="contact-category" bind:value={selectedCategory} onchange={updateAudienceEstimate} class="w-full h-9 px-3 text-xs rounded-md border border-input bg-background">
						<option value="">All Categories</option>
						<option value="Fleet">Fleet Operator</option>
						<option value="Dealer">Tyre Dealer</option>
						<option value="Transporter">Transporter / Logistics</option>
						<option value="OEM">OEM / Corporate</option>
					</select>
				</div>

				<div class="space-y-1.5">
					<label for="filter-state" class="text-xs font-medium text-foreground">State</label>
					<Input id="filter-state" bind:value={selectedState} oninput={updateAudienceEstimate} placeholder="e.g. Maharashtra" />
				</div>

				<div class="space-y-1.5">
					<label for="filter-city" class="text-xs font-medium text-foreground">City</label>
					<Input id="filter-city" bind:value={selectedCity} oninput={updateAudienceEstimate} placeholder="e.g. Mumbai, Pune" />
				</div>

				<div class="space-y-1.5">
					<label for="min-score" class="text-xs font-medium text-foreground">Min. Quality Score (0 - 100)</label>
					<Input id="min-score" type="number" bind:value={minQualityScore} oninput={updateAudienceEstimate} placeholder="e.g. 50" />
				</div>
			</div>

			<!-- Live Audience Calculation Card -->
			<div class="p-5 bg-muted/40 rounded-2xl border border-border/60 space-y-3">
				<div class="flex items-center justify-between">
					<span class="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
						<Icon name="users" class="w-4 h-4 text-primary" />
						Real-Time Audience Calculator
					</span>
					{#if estimatingAudience}
						<Icon name="loader-2" class="w-3.5 h-3.5 animate-spin text-primary" />
					{/if}
				</div>

				<div class="grid grid-cols-2 sm:grid-cols-4 gap-4 text-center">
					<div class="p-3 bg-card rounded-xl border border-border/40">
						<div class="text-xs text-muted-foreground">Matching CRM Contacts</div>
						<div class="text-xl font-bold text-foreground">{audienceEstimate.totalMatchingContacts}</div>
					</div>
					<div class="p-3 bg-card rounded-xl border border-border/40">
						<div class="text-xs text-muted-foreground">With Valid Email</div>
						<div class="text-xl font-bold text-blue-600">{audienceEstimate.withValidEmail}</div>
					</div>
					<div class="p-3 bg-card rounded-xl border border-border/40">
						<div class="text-xs text-muted-foreground">Auto-Suppressed</div>
						<div class="text-xl font-bold text-rose-500">
							{audienceEstimate.suppressedCount}
							<span class="text-[10px] font-normal text-muted-foreground block">(Bounced / Unsub)</span>
						</div>
					</div>
					<div class="p-3 bg-primary/10 rounded-xl border border-primary/20">
						<div class="text-xs text-primary font-semibold">Net Deliverable</div>
						<div class="text-2xl font-black text-primary">{audienceEstimate.eligibleRecipients}</div>
					</div>
				</div>
				<p class="text-[11px] text-muted-foreground">
					✓ Zero-Bypass Shield: Contacts that previously hard-bounced or unsubscribed are automatically protected and excluded from dispatch.
				</p>
			</div>

			<div class="flex justify-between pt-4 border-t border-border/50">
				<Button variant="outline" onclick={() => (currentStep = 1)}>
					<Icon name="arrow-left" class="w-4 h-4 mr-1.5" />
					Back
				</Button>
				<Button onclick={() => (currentStep = 3)} class="gap-1.5">
					Next: Edit Content & Check Spam
					<Icon name="arrow-right" class="w-4 h-4" />
				</Button>
			</div>
		</div>
	{/if}

	<!-- STEP 3: CONTENT & SPAM SCORING -->
	{#if currentStep === 3}
		<div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
			<!-- Editor Section -->
			<div class="lg:col-span-2 p-6 bg-card border border-border/70 rounded-2xl shadow-sm space-y-4">
				<div class="flex items-center justify-between">
					<div>
						<h3 class="text-base font-bold text-foreground">Email Content</h3>
						<p class="text-xs text-muted-foreground">Personalize with merge variables.</p>
					</div>
					<div class="flex items-center gap-1.5">
						<Button
							variant="outline"
							size="sm"
							class="h-7 text-xs"
							onclick={() => (previewActive = !previewActive)}
						>
							<Icon name={previewActive ? 'edit-3' : 'eye'} class="w-3.5 h-3.5 mr-1" />
							{previewActive ? 'Editor' : 'Preview'}
						</Button>
					</div>
				</div>

				<!-- Merge Tag Inserters -->
				<div class="flex items-center gap-1.5 flex-wrap">
					<span class="text-[11px] text-muted-foreground font-semibold mr-1">Insert Variable:</span>
					{#each ['{{FullName}}', '{{CompanyName}}', '{{City}}', '{{Products}}', '{{UnsubscribeLink}}'] as tag}
						<button
							type="button"
							onclick={() => insertTag(tag)}
							class="px-2 py-0.5 text-[11px] font-mono bg-muted text-foreground/80 hover:bg-primary/20 hover:text-primary rounded border border-border/60 transition-colors"
						>
							{tag}
						</button>
					{/each}
				</div>

				{#if previewActive}
					<div class="p-4 bg-muted/20 border border-border/60 rounded-xl min-h-[340px] max-h-[500px] overflow-y-auto">
						{#if contentType === 'Html'}
							{@html bodyHtml
								.replace(/{{FullName}}/g, 'Rajesh Sharma')
								.replace(/{{CompanyName}}/g, 'Sharma Logistics Transport')
								.replace(/{{City}}/g, 'Pune')
								.replace(/{{UnsubscribeLink}}/g, '#')}
						{:else}
							<pre class="font-sans whitespace-pre-wrap text-sm text-foreground">
								{bodyText
									.replace(/{{FullName}}/g, 'Rajesh Sharma')
									.replace(/{{CompanyName}}/g, 'Sharma Logistics Transport')
									.replace(/{{City}}/g, 'Pune')
									.replace(/{{UnsubscribeLink}}/g, '#')}
							</pre>
						{/if}
					</div>
				{:else}
					{#if contentType === 'Html'}
						<Textarea
							bind:value={bodyHtml}
							oninput={runSpamCheck}
							class="font-mono text-xs min-h-[340px] leading-relaxed"
							placeholder="Write email HTML..."
						/>
					{:else}
						<Textarea
							bind:value={bodyText}
							oninput={runSpamCheck}
							class="text-sm min-h-[340px] leading-relaxed"
							placeholder="Write plain-text email..."
						/>
					{/if}
				{/if}

				<div class="flex justify-between pt-4 border-t border-border/50">
					<Button variant="outline" onclick={() => (currentStep = 2)}>
						<Icon name="arrow-left" class="w-4 h-4 mr-1.5" />
						Back
					</Button>
					<Button onclick={() => (currentStep = 4)} class="gap-1.5">
						Next: Review & Schedule
						<Icon name="arrow-right" class="w-4 h-4" />
					</Button>
				</div>
			</div>

			<!-- Live Spam & Deliverability Meter -->
			<div class="p-6 bg-card border border-border/70 rounded-2xl shadow-sm space-y-5 h-fit">
				<div class="space-y-1">
					<div class="flex items-center justify-between">
						<h4 class="text-sm font-bold text-foreground">Deliverability Score</h4>
						{#if checkingSpam}
							<Icon name="loader-2" class="w-3.5 h-3.5 animate-spin text-primary" />
						{/if}
					</div>
					<p class="text-xs text-muted-foreground">Real-time anti-spam compliance check.</p>
				</div>

				<div class="p-4 rounded-xl text-center border {spamScore.score >= 80 ? 'bg-emerald-500/10 border-emerald-500/30' : spamScore.score >= 60 ? 'bg-amber-500/10 border-amber-500/30' : 'bg-rose-500/10 border-rose-500/30'}">
					<div class="text-4xl font-black {spamScore.score >= 80 ? 'text-emerald-600' : spamScore.score >= 60 ? 'text-amber-600' : 'text-rose-600'}">
						{spamScore.score}/100
					</div>
					<div class="text-xs font-bold uppercase tracking-wider mt-1 {spamScore.score >= 80 ? 'text-emerald-600' : spamScore.score >= 60 ? 'text-amber-600' : 'text-rose-600'}">
						{spamScore.rating} Deliverability
					</div>
				</div>

				<!-- Warnings -->
				{#if spamScore.warnings.length > 0}
					<div class="space-y-2">
						<div class="text-xs font-bold text-rose-600 flex items-center gap-1">
							<Icon name="alert-triangle" class="w-3.5 h-3.5" />
							Warnings ({spamScore.warnings.length})
						</div>
						<ul class="space-y-1 text-xs text-muted-foreground list-disc pl-4">
							{#each spamScore.warnings as w}
								<li>{w}</li>
							{/each}
						</ul>
					</div>
				{/if}

				<!-- Recommendations -->
				{#if spamScore.recommendations.length > 0}
					<div class="space-y-2">
						<div class="text-xs font-bold text-emerald-600 flex items-center gap-1">
							<Icon name="check-circle-2" class="w-3.5 h-3.5" />
							Best Practices
						</div>
						<ul class="space-y-1 text-xs text-muted-foreground list-disc pl-4">
							{#each spamScore.recommendations as r}
								<li>{r}</li>
							{/each}
						</ul>
					</div>
				{/if}
			</div>
		</div>
	{/if}

	<!-- STEP 4: REVIEW, TEST SEND & SCHEDULE -->
	{#if currentStep === 4}
		<div class="p-6 bg-card border border-border/70 rounded-2xl shadow-sm space-y-6">
			<div class="space-y-1">
				<h3 class="text-base font-bold text-foreground">Review & Launch</h3>
				<p class="text-xs text-muted-foreground">Send an immediate test copy to your inbox before launching.</p>
			</div>

			<!-- Summary Cards -->
			<div class="grid grid-cols-1 md:grid-cols-3 gap-4">
				<div class="p-4 bg-muted/40 rounded-xl border border-border/50 space-y-1">
					<div class="text-xs text-muted-foreground font-semibold">Campaign Name</div>
					<div class="text-sm font-bold text-foreground">{name || 'Untitled Campaign'}</div>
					<div class="text-xs text-muted-foreground">Subject: {subject}</div>
				</div>

				<div class="p-4 bg-muted/40 rounded-xl border border-border/50 space-y-1">
					<div class="text-xs text-muted-foreground font-semibold">Sender Details</div>
					<div class="text-sm font-bold text-foreground">{fromName}</div>
					<div class="text-xs text-muted-foreground">{fromEmail} (tyresoles.in)</div>
				</div>

				<div class="p-4 bg-muted/40 rounded-xl border border-border/50 space-y-1">
					<div class="text-xs text-muted-foreground font-semibold">Eligible Audience</div>
					<div class="text-2xl font-black text-primary">{audienceEstimate.eligibleRecipients}</div>
					<div class="text-[11px] text-muted-foreground">Verified contacts to receive</div>
				</div>
			</div>

			<!-- Test Email Box -->
			<div class="p-5 bg-primary/5 border border-primary/20 rounded-2xl space-y-3">
				<div class="flex items-center gap-2">
					<Icon name="send" class="w-4 h-4 text-primary" />
					<h4 class="text-xs font-bold uppercase tracking-wider text-primary">Pre-Flight Test Send</h4>
				</div>
				<p class="text-xs text-muted-foreground">
					Send a live test copy of this exact email to your inbox to check how it looks in Gmail or Outlook.
				</p>
				<div class="flex items-center gap-2 max-w-md">
					<Input bind:value={testRecipientEmail} placeholder="Enter your email (e.g. tyresoles.one@gmail.com)" />
					<Button size="sm" onclick={handleSendTest} disabled={sendingTest} class="shrink-0 gap-1.5">
						{#if sendingTest}
							<Icon name="loader-2" class="w-3.5 h-3.5 animate-spin" />
							Sending...
						{:else}
							<Icon name="mail" class="w-3.5 h-3.5" />
							Send Test
						{/if}
					</Button>
				</div>
			</div>

			<!-- Scheduling Options -->
			<div class="space-y-3 pt-2">
				<h4 class="text-xs font-bold uppercase tracking-wider text-foreground">Launch Timing</h4>
				<div class="flex items-center gap-4">
					<label class="flex items-center gap-2 text-xs font-medium cursor-pointer">
						<input type="radio" bind:group={scheduleOption} value="NOW" class="text-primary" />
						Send Immediately (Paced at 10 emails/sec)
					</label>
					<label class="flex items-center gap-2 text-xs font-medium cursor-pointer">
						<input type="radio" bind:group={scheduleOption} value="LATER" class="text-primary" />
						Schedule for Later
					</label>
				</div>

				{#if scheduleOption === 'LATER'}
					<div class="flex items-center gap-3 max-w-sm pt-2">
						<Input type="date" bind:value={scheduledDate} />
						<Input type="time" bind:value={scheduledTime} />
					</div>
				{/if}
			</div>

			<!-- Final Actions -->
			<div class="flex justify-between pt-4 border-t border-border/50">
				<Button variant="outline" onclick={() => (currentStep = 3)}>
					<Icon name="arrow-left" class="w-4 h-4 mr-1.5" />
					Back
				</Button>

				<Button onclick={handleFinalSubmit} disabled={submitting} class="bg-primary text-primary-foreground font-bold px-6 gap-2">
					{#if submitting}
						<Icon name="loader-2" class="w-4 h-4 animate-spin" />
						Scheduling Campaign...
					{:else}
						<Icon name="check" class="w-4 h-4" />
						{scheduleOption === 'NOW' ? 'Launch Campaign Now' : 'Save & Schedule'}
					{/if}
				</Button>
			</div>
		</div>
	{/if}
</div>
