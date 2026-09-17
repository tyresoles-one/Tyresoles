<script lang="ts">
	import { onMount } from 'svelte';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';

	interface TemplateItem {
		id: string;
		name: string;
		category: string;
		subject: string;
		previewText?: string | null;
		bodyHtml?: string | null;
		bodyText?: string | null;
		createdAt: string;
	}

	let templates = $state<TemplateItem[]>([]);
	let loading = $state(true);
	let isEditing = $state(false);

	let editId = $state<string | null>(null);
	let editName = $state('');
	let editCategory = $state('Fleet Service');
	let editSubject = $state('');
	let editPreview = $state('');
	let editBody = $state('');

	const GET_TEMPLATES = buildQuery`
		query GetTemplates {
			getCrmEmailTemplates {
				id
				name
				category
				subject
				previewText
				bodyHtml
				bodyText
				createdAt
			}
		}
	`;

	const SAVE_TEMPLATE = buildMutation`
		mutation SaveTemplate($input: SaveCrmEmailTemplateInput!) {
			saveCrmEmailTemplate(input: $input) {
				id
				name
			}
		}
	`;

	const DELETE_TEMPLATE = buildMutation`
		mutation DeleteTemplate($id: UUID!) {
			deleteCrmEmailTemplate(id: $id)
		}
	`;

	async function loadTemplates() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmEmailTemplates: TemplateItem[] }>(GET_TEMPLATES);
			if (res.success && res.data?.getCrmEmailTemplates) {
				templates = res.data.getCrmEmailTemplates;
			}
		} catch (e: any) {
			toast.error('Failed to load templates: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadTemplates();
	});

	function openCreate() {
		editId = null;
		editName = '';
		editCategory = 'Fleet Service';
		editSubject = '';
		editPreview = '';
		editBody = `<p>Dear {{FullName}},</p>\n<p>Reaching out from Tyresoles regarding retreading schedules for {{CompanyName}}.</p>\n<p>Best regards,<br>Tyresoles Fleet Solutions</p>\n<p><small><a href="{{UnsubscribeLink}}">Unsubscribe</a></small></p>`;
		isEditing = true;
	}

	function openEdit(t: TemplateItem) {
		editId = t.id;
		editName = t.name;
		editCategory = t.category;
		editSubject = t.subject;
		editPreview = t.previewText || '';
		editBody = t.bodyHtml || t.bodyText || '';
		isEditing = true;
	}

	async function handleSave() {
		if (!editName.trim()) {
			toast.error('Template name is required.');
			return;
		}
		if (!editSubject.trim()) {
			toast.error('Subject line is required.');
			return;
		}

		try {
			const res = await graphqlMutation(SAVE_TEMPLATE, {
				variables: {
					input: {
						id: editId,
						name: editName.trim(),
						category: editCategory,
						subject: editSubject.trim(),
						previewText: editPreview.trim() || null,
						bodyHtml: editBody,
						bodyText: editBody
					}
				}
			});

			if (res.success) {
				toast.success('Template saved successfully.');
				isEditing = false;
				await loadTemplates();
			}
		} catch (e: any) {
			toast.error(e.message || 'Failed to save template.');
		}
	}

	async function handleDelete(id: string) {
		if (!confirm('Are you sure you want to delete this template?')) return;

		try {
			const res = await graphqlMutation(DELETE_TEMPLATE, { variables: { id } });
			if (res.success) {
				toast.success('Template removed.');
				await loadTemplates();
			}
		} catch (e: any) {
			toast.error(e.message);
		}
	}
</script>

<svelte:head>
	<title>Campaign Templates | Tyresoles CRM</title>
</svelte:head>

<PageHeading
	backHref="/crm-campaigns"
	backLabel="Back to Campaigns"
	icon="layout-template"
	title="Email Templates"
	description="Reusable B2B marketing, retread announcement, and cold outreach layouts"
>
	{#snippet actions()}
		<Button size="sm" onclick={openCreate} class="gap-1.5">
			<Icon name="plus" class="w-4 h-4" />
			New Template
		</Button>
	{/snippet}
</PageHeading>

<div class="p-6 max-w-6xl mx-auto space-y-6">
	{#if isEditing}
		<!-- Template Editor Drawer/Card -->
		<div class="p-6 bg-card border border-primary/30 rounded-2xl shadow-sm space-y-4">
			<div class="flex items-center justify-between border-b border-border/50 pb-3">
				<h3 class="text-sm font-bold text-foreground">
					{editId ? 'Edit Template' : 'Create New Email Template'}
				</h3>
				<Button variant="ghost" size="sm" onclick={() => (isEditing = false)}>
					<Icon name="x" class="w-4 h-4" />
				</Button>
			</div>

			<div class="grid grid-cols-1 md:grid-cols-2 gap-4">
				<div class="space-y-1">
					<label for="template-name" class="text-xs font-medium text-foreground">Template Name</label>
					<Input id="template-name" bind:value={editName} placeholder="e.g. Cold Fleet Outreach - CPK Focus" />
				</div>
				<div class="space-y-1">
					<label for="template-cat" class="text-xs font-medium text-foreground">Category</label>
					<select id="template-cat" bind:value={editCategory} class="w-full h-9 px-3 text-xs rounded-md border border-input bg-background">
						<option value="Fleet Service">Fleet Service & Maintenance</option>
						<option value="Retread Promo">Retreading Promotions</option>
						<option value="Invoice Reminder">Invoice & Account Notice</option>
						<option value="General">General Announcement</option>
					</select>
				</div>
				<div class="space-y-1 md:col-span-2">
					<label for="template-sub" class="text-xs font-medium text-foreground">Subject Line</label>
					<Input id="template-sub" bind:value={editSubject} placeholder={'e.g. Tyresoles Fleet Service Reminder: {{CompanyName}}'} />
				</div>
				<div class="space-y-1 md:col-span-2">
					<label for="template-prev" class="text-xs font-medium text-foreground">Preheader / Preview Text</label>
					<Input id="template-prev" bind:value={editPreview} placeholder="Short preview seen in inbox before opening" />
				</div>
				<div class="space-y-1 md:col-span-2">
					<label for="template-body" class="text-xs font-medium text-foreground">Body Content (HTML or Text)</label>
					<Textarea id="template-body" bind:value={editBody} class="min-h-[220px] font-mono text-xs" />
				</div>
			</div>

			<div class="flex justify-end gap-2 pt-2">
				<Button variant="outline" size="sm" onclick={() => (isEditing = false)}>Cancel</Button>
				<Button size="sm" onclick={handleSave} class="gap-1.5">
					<Icon name="check" class="w-4 h-4" />
					Save Template
				</Button>
			</div>
		</div>
	{/if}

	<!-- Template Cards Grid -->
	{#if loading}
		<div class="flex items-center justify-center p-16 text-muted-foreground">
			<Icon name="loader-2" class="w-5 h-5 animate-spin mr-2" />
			Loading templates...
		</div>
	{:else if templates.length === 0}
		<div class="p-12 text-center bg-card border border-dashed border-border rounded-2xl space-y-3">
			<Icon name="layout-template" class="w-8 h-8 mx-auto text-muted-foreground" />
			<div class="text-sm font-semibold">No templates yet</div>
			<p class="text-xs text-muted-foreground max-w-sm mx-auto">
				Create standardized templates for fleet outreach, warranty notices, and retread offers.
			</p>
			<Button size="sm" onclick={openCreate}>Create First Template</Button>
		</div>
	{:else}
		<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
			{#each templates as t (t.id)}
				<div class="p-5 bg-card border border-border/70 rounded-2xl shadow-sm hover:border-primary/30 transition-all flex flex-col justify-between space-y-3">
					<div class="space-y-2">
						<div class="flex items-center justify-between">
							<span class="px-2 py-0.5 text-[10px] font-semibold rounded-full bg-primary/10 text-primary border border-primary/20">
								{t.category}
							</span>
							<span class="text-[11px] text-muted-foreground">
								{new Date(t.createdAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}
							</span>
						</div>
						<h4 class="text-sm font-bold text-foreground line-clamp-1">{t.name}</h4>
						<p class="text-xs text-muted-foreground line-clamp-2">
							<span class="font-medium text-foreground/80">Subject:</span> {t.subject}
						</p>
					</div>

					<div class="flex items-center justify-end gap-2 pt-2 border-t border-border/40">
						<Button variant="ghost" size="sm" class="h-7 text-xs" onclick={() => openEdit(t)}>
							<Icon name="edit-3" class="w-3.5 h-3.5 mr-1" />
							Edit
						</Button>
						<Button variant="ghost" size="sm" class="h-7 text-xs text-rose-600 hover:text-rose-700" onclick={() => handleDelete(t.id)}>
							<Icon name="trash-2" class="w-3.5 h-3.5" />
						</Button>
					</div>
				</div>
			{/each}
		</div>
	{/if}
</div>
