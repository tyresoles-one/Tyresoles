<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import {
		GET_WHATSAPP_TEMPLATES,
		SYNC_WHATSAPP_TEMPLATES,
		SUBMIT_WHATSAPP_TEMPLATE,
		DELETE_WHATSAPP_TEMPLATE,
		type CrmWhatsappTemplate
	} from '../whatsappQueries';

	let templates = $state<CrmWhatsappTemplate[]>([]);
	let loading = $state(true);
	let syncing = $state(false);

	let filterCategory = $state('ALL');
	let filterStatus = $state('ALL');
	let searchQuery = $state('');

	// Create Template Modal
	let showCreateModal = $state(false);
	let newName = $state('');
	let newCategory = $state('MARKETING');
	let newLanguage = $state('en');
	let newHeaderType = $state('NONE');
	let newHeaderText = $state('');
	let newHeaderMediaUrl = $state('');
	let newBodyText = $state('Dear {{1}}, we are pleased to offer commercial tyre retreading for {{2}} at our {{3}} depot.');
	let newFooterText = $state('Tyresoles (India) Pvt Ltd. Reply STOP to opt out.');
	let sampleValues = $state('Rajesh Kumar, ABC Logistics, Pune Depot');
	let submitting = $state(false);

	async function loadTemplates() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappTemplates: CrmWhatsappTemplate[] }>(
				GET_WHATSAPP_TEMPLATES,
				{
					variables: {
						category: filterCategory === 'ALL' ? null : filterCategory,
						status: filterStatus === 'ALL' ? null : filterStatus
					}
				}
			);
			if (res.success && res.data?.getCrmWhatsappTemplates) {
				templates = res.data.getCrmWhatsappTemplates;
			}
		} catch (e: any) {
			toast.error('Failed to load templates: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(async () => {
		await loadTemplates();
		// Auto-sync in background so newly approved templates in Meta update seamlessly
		try {
			const syncRes = await graphqlMutation(SYNC_WHATSAPP_TEMPLATES);
			if (syncRes.success) {
				await loadTemplates();
			}
		} catch {
			// Silent background sync
		}
	});

	async function handleSyncMeta() {
		syncing = true;
		try {
			const res = await graphqlMutation(SYNC_WHATSAPP_TEMPLATES);
			if (res.success) {
				toast.success('Templates synchronized with Meta Graph API.');
				loadTemplates();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to sync with Meta.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error('Error syncing: ' + e.message);
		} finally {
			syncing = false;
		}
	}

	let deletingId = $state<string | null>(null);

	async function handleDeleteTemplate(id: string, name: string) {
		if (!confirm(`Are you sure you want to delete template "${name}"?`)) {
			return;
		}

		deletingId = id;
		try {
			const res = await graphqlMutation(DELETE_WHATSAPP_TEMPLATE, {
				variables: { id }
			});
			if (res.success) {
				toast.success(`Template "${name}" deleted successfully.`);
				await loadTemplates();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to delete template.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error('Error deleting template: ' + e.message);
		} finally {
			deletingId = null;
		}
	}

	async function handleCreateTemplate() {
		if (!newName.trim()) {
			toast.error('Template name is required.');
			return;
		}
		if (!newBodyText.trim()) {
			toast.error('Body text is required.');
			return;
		}

		submitting = true;
		try {
			const samplesList = sampleValues.split(',').map((s) => s.trim()).filter(Boolean);
			const res = await graphqlMutation(SUBMIT_WHATSAPP_TEMPLATE, {
				variables: {
					input: {
						name: newName.trim().toLowerCase().replace(/\s+/g, '_'),
						category: newCategory,
						language: newLanguage,
						headerType: newHeaderType,
						headerText: newHeaderText || null,
						headerSampleUrl: newHeaderMediaUrl || null,
						bodyText: newBodyText,
						footerText: newFooterText || null,
						bodySampleValues: samplesList
					}
				}
			});

			if (res.success) {
				toast.success(`Template ${newName} submitted to Meta for approval!`);
				showCreateModal = false;
				loadTemplates();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to submit template.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message);
		} finally {
			submitting = false;
		}
	}

	const filteredTemplates = $derived(
		templates.filter((t) => {
			const matchCat = filterCategory === 'ALL' || t.category === filterCategory;
			const matchStat = filterStatus === 'ALL' || t.status === filterStatus;
			const matchSearch =
				!searchQuery.trim() ||
				t.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
				(t.bodyText && t.bodyText.toLowerCase().includes(searchQuery.toLowerCase()));
			return matchCat && matchStat && matchSearch;
		})
	);

	function getStatusBadge(status: string) {
		switch (status) {
			case 'APPROVED':
				return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-300';
			case 'PENDING':
				return 'bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300';
			case 'REJECTED':
				return 'bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300';
			case 'PAUSED':
				return 'bg-purple-100 text-purple-800 dark:bg-purple-950 dark:text-purple-300';
			default:
				return 'bg-muted text-muted-foreground';
		}
	}
</script>

<div class="space-y-6 pb-16">
	<!-- Header -->
	<PageHeading
		backHref="/crm-whatsapp-campaigns"
		backLabel="Campaigns Hub"
		icon="layout-template"
		title="WhatsApp Template Studio"
		description="Create, preview, submit, and sync pre-approved Meta message templates for marketing broadcasts."
	>
		{#snippet actions()}
			<div class="flex items-center gap-2">
				<Button
					variant="outline"
					size="sm"
					class="gap-1.5"
					onclick={handleSyncMeta}
					disabled={syncing}
				>
					<Icon name="refresh-cw" class="w-4 h-4 {syncing ? 'animate-spin' : ''}" />
					<span>{syncing ? 'Syncing with Meta...' : 'Sync with Meta'}</span>
				</Button>

				<Button
					size="sm"
					class="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white"
					onclick={() => (showCreateModal = true)}
				>
					<Icon name="plus" class="w-4 h-4" />
					<span>Create Template</span>
				</Button>
			</div>
		{/snippet}
	</PageHeading>

	<!-- Filters & Search -->
	<div class="flex flex-col sm:flex-row items-center justify-between gap-3 bg-card border border-border p-3 rounded-xl">
		<div class="flex flex-wrap items-center gap-2 w-full sm:w-auto">
			<div class="relative w-full sm:w-64">
				<Icon name="search" class="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
				<input
					type="text"
					bind:value={searchQuery}
					placeholder="Search template name or content..."
					class="w-full pl-8 pr-3 py-1.5 text-xs bg-background border border-input rounded-lg focus:outline-none"
				/>
			</div>

			<select
				bind:value={filterCategory}
				class="py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
			>
				<option value="ALL">All Categories</option>
				<option value="MARKETING">Marketing</option>
				<option value="UTILITY">Utility</option>
				<option value="AUTHENTICATION">Authentication</option>
			</select>

			<select
				bind:value={filterStatus}
				class="py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
			>
				<option value="ALL">All Statuses</option>
				<option value="APPROVED">Approved (Ready)</option>
				<option value="PENDING">Pending Approval</option>
				<option value="REJECTED">Rejected</option>
				<option value="PAUSED">Paused</option>
			</select>
		</div>

		<div class="text-xs text-muted-foreground">
			{filteredTemplates.length} templates available
		</div>
	</div>

	<!-- Templates Grid -->
	{#if loading}
		<div class="p-12 text-center text-muted-foreground flex flex-col items-center gap-2">
			<Icon name="loader-2" class="w-6 h-6 animate-spin text-primary" />
			<span class="text-sm">Loading templates...</span>
		</div>
	{:else if filteredTemplates.length === 0}
		<div class="bg-card border border-border rounded-2xl p-12 text-center space-y-3">
			<div class="w-12 h-12 rounded-xl bg-muted flex items-center justify-center mx-auto text-muted-foreground">
				<Icon name="layout-template" class="w-6 h-6 opacity-60" />
			</div>
			<h3 class="font-semibold text-foreground">No templates found</h3>
			<p class="text-xs text-muted-foreground max-w-sm mx-auto">Sync existing templates from Meta or create a new pre-approved marketing template.</p>
			<Button size="sm" class="mt-2 bg-emerald-600 hover:bg-emerald-700 text-white" onclick={() => (showCreateModal = true)}>
				<Icon name="plus" class="w-4 h-4 mr-1" />
				Create New Template
			</Button>
		</div>
	{:else}
		<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
			{#each filteredTemplates as t (t.id)}
				<div class="bg-card border border-border rounded-2xl p-5 shadow-sm hover:shadow-md transition-shadow flex flex-col justify-between">
					<div class="space-y-3">
						<div class="flex items-start justify-between gap-2">
							<div class="space-y-0.5">
								<span class="font-mono font-bold text-sm text-foreground">{t.name}</span>
								<div class="text-[10px] text-muted-foreground">
									{t.language} ({t.languageCode || 'en'}) • {t.category}
								</div>
								{#if t.metaTemplateId}
									<div class="inline-flex items-center gap-1 text-[10px] text-emerald-600 dark:text-emerald-400 font-medium">
										<Icon name="check-circle-2" class="w-3 h-3" />
										<span>Meta ID: {t.metaTemplateId}</span>
									</div>
								{:else}
									<div class="inline-flex items-center gap-1 text-[10px] text-amber-600 dark:text-amber-400 font-medium">
										<Icon name="alert-circle" class="w-3 h-3" />
										<span>Local Only (Not on Meta)</span>
									</div>
								{/if}
							</div>
							<div class="flex flex-col items-end gap-1">
								<span class="px-2 py-0.5 rounded-full text-[10px] font-semibold {getStatusBadge(t.status)}">
									{t.status}
								</span>
								{#if t.qualityScore}
									<span class="text-[9px] px-1.5 py-0.5 rounded bg-muted text-muted-foreground">
										Quality: {t.qualityScore}
									</span>
								{/if}
							</div>
						</div>

						<!-- Header Preview if any -->
						{#if t.headerType !== 'NONE'}
							<div class="text-[10px] font-semibold text-emerald-700 dark:text-emerald-300 bg-emerald-50 dark:bg-emerald-950/40 px-2 py-1 rounded border border-emerald-200 dark:border-emerald-800">
								Header: {t.headerType} {t.headerText ? `("${t.headerText}")` : ''}
							</div>
						{/if}

						<!-- Body Text Preview -->
						<div class="text-xs text-foreground/90 whitespace-pre-wrap bg-muted/40 p-3 rounded-xl font-sans border border-border/40 text-[11px] leading-relaxed">
							{t.bodyText || t.messageText}
						</div>

						{#if t.footerText}
							<div class="text-[10px] text-muted-foreground italic">
								Footer: {t.footerText}
							</div>
						{/if}
					</div>

					<div class="pt-4 mt-4 border-t border-border flex items-center justify-between text-xs">
						<span class="text-[10px] text-muted-foreground">
							{new Date(t.createdAt).toLocaleDateString()}
						</span>
						<div class="flex items-center gap-2">
							<Button
								variant="ghost"
								size="sm"
								class="h-7 w-7 p-0 text-muted-foreground hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40"
								title="Delete Template"
								disabled={deletingId === t.id}
								onclick={() => handleDeleteTemplate(t.id, t.name)}
							>
								{#if deletingId === t.id}
									<Icon name="loader-2" class="w-3.5 h-3.5 animate-spin text-red-600" />
								{:else}
									<Icon name="trash-2" class="w-3.5 h-3.5" />
								{/if}
							</Button>

							<Button
								variant="outline"
								size="sm"
								class="h-7 text-xs gap-1"
								disabled={t.status !== 'APPROVED'}
								onclick={() => {
									goto(`/crm-whatsapp-campaigns/new?templateId=${t.id}`);
								}}
							>
								<Icon name="send" class="w-3 h-3" />
								<span>Use in Campaign</span>
							</Button>
						</div>
					</div>
				</div>
			{/each}
		</div>
	{/if}

	<!-- Create Template Modal -->
	{#if showCreateModal}
		<div class="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto">
			<div class="bg-card border border-border rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-5 my-8">
				<div class="flex items-center justify-between border-b border-border pb-3">
					<div class="flex items-center gap-2">
						<Icon name="layout-template" class="w-5 h-5 text-primary" />
						<h3 class="text-base font-bold text-foreground">Create Meta Message Template</h3>
					</div>
					<button class="text-muted-foreground hover:text-foreground" onclick={() => (showCreateModal = false)}>
						<Icon name="x" class="w-5 h-5" />
					</button>
				</div>

				<div class="space-y-4 max-h-[70vh] overflow-y-auto pr-1 text-xs">
					<div class="grid grid-cols-2 gap-3">
						<div class="space-y-1">
							<label class="font-medium text-foreground">Template Name *</label>
							<Input
								bind:value={newName}
								placeholder="e.g. monsoon_tyre_offer"
								class="text-xs"
							/>
							<p class="text-[10px] text-muted-foreground">Lower-case letters and underscores only.</p>
						</div>

						<div class="space-y-1">
							<label class="font-medium text-foreground">Category *</label>
							<select
								bind:value={newCategory}
								class="w-full py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
							>
								<option value="MARKETING">MARKETING</option>
								<option value="UTILITY">UTILITY</option>
								<option value="AUTHENTICATION">AUTHENTICATION</option>
							</select>
						</div>
					</div>

					<div class="grid grid-cols-2 gap-3">
						<div class="space-y-1">
							<label class="font-medium text-foreground">Language *</label>
							<select
								bind:value={newLanguage}
								class="w-full py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
							>
								<option value="en">English (en)</option>
								<option value="hi">Hindi (hi)</option>
								<option value="mr">Marathi (mr)</option>
								<option value="gu">Gujarati (gu)</option>
								<option value="ta">Tamil (ta)</option>
								<option value="te">Telugu (te)</option>
							</select>
						</div>

						<div class="space-y-1">
							<label class="font-medium text-foreground">Header Format</label>
							<select
								bind:value={newHeaderType}
								class="w-full py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
							>
								<option value="NONE">None</option>
								<option value="TEXT">Text Title</option>
								<option value="IMAGE">Image Media</option>
								<option value="DOCUMENT">PDF Document</option>
							</select>
						</div>
					</div>

					{#if newHeaderType === 'TEXT'}
						<div class="space-y-1">
							<label class="font-medium text-foreground">Header Text</label>
							<Input bind:value={newHeaderText} placeholder="e.g. Tyresoles Fleet Specials" class="text-xs" />
						</div>
					{:else if newHeaderType === 'IMAGE' || newHeaderType === 'DOCUMENT'}
						<div class="space-y-1">
							<label class="font-medium text-foreground">Header Sample Media URL</label>
							<Input bind:value={newHeaderMediaUrl} placeholder="https://... sample for Meta review" class="text-xs" />
						</div>
					{/if}

					<div class="space-y-1">
						<label class="font-medium text-foreground">Body Text *</label>
						<Textarea
							bind:value={newBodyText}
							rows={4}
							placeholder={'Use {{1}}, {{2}} for dynamic personalization placeholders.'}
							class="text-xs"
						/>
						<p class="text-[10px] text-muted-foreground">Example: "Dear &#123;&#123;1&#125;&#125;, get 85% tyre life for &#123;&#123;2&#125;&#125; with Tyresoles retreads in &#123;&#123;3&#125;&#125;."</p>
					</div>

					<div class="space-y-1">
						<label class="font-medium text-foreground">Footer Text (Optional)</label>
						<Input
							bind:value={newFooterText}
							placeholder="e.g. Tyresoles Fleet Care. Reply STOP to opt out."
							class="text-xs"
						/>
					</div>

					<div class="space-y-1">
						<label class="font-medium text-foreground">Sample Values for Placeholders (Comma-separated) *</label>
						<Input
							bind:value={sampleValues}
							placeholder="e.g. Rajesh Kumar, ABC Logistics, Pune"
							class="text-xs"
						/>
						<p class="text-[10px] text-muted-foreground">Mandatory: Meta AI requires realistic sample values to approve templates.</p>
					</div>
				</div>

				<div class="flex items-center justify-end gap-2 border-t border-border pt-4">
					<Button variant="outline" size="sm" onclick={() => (showCreateModal = false)}>
						Cancel
					</Button>
					<Button
						size="sm"
						class="bg-emerald-600 hover:bg-emerald-700 text-white font-semibold gap-1.5"
						onclick={handleCreateTemplate}
						disabled={submitting}
					>
						<Icon name="check" class="w-4 h-4 {submitting ? 'animate-spin' : ''}" />
						<span>{submitting ? 'Submitting to Meta...' : 'Submit Template to Meta'}</span>
					</Button>
				</div>
			</div>
		</div>
	{/if}
</div>
