<script lang="ts">
	import { onMount } from 'svelte';
	import { graphqlQuery, graphqlMutation, buildQuery, buildMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';

	interface SuppressionItem {
		id: string;
		emailAddress: string;
		reason: string;
		diagnosticCode?: string | null;
		createdAt: string;
	}

	let suppressionList = $state<SuppressionItem[]>([]);
	let loading = $state(true);
	let searchQuery = $state('');
	let filterReason = $state('ALL');

	// Manual Add State
	let newEmail = $state('');
	let newReason = $state('Manual');
	let isAdding = $state(false);

	const GET_SUPPRESSION_LIST = buildQuery`
		query GetSuppressionList($search: String, $reason: String) {
			getCrmSuppressionList(search: $search, reason: $reason) {
				id
				emailAddress
				reason
				diagnosticCode
				createdAt
			}
		}
	`;

	const ADD_SUPPRESSION = buildMutation`
		mutation AddSuppression($email: String!, $reason: String!) {
			addSuppressionEmail(email: $email, reason: $reason) {
				id
				emailAddress
				reason
			}
		}
	`;

	const REMOVE_SUPPRESSION = buildMutation`
		mutation RemoveSuppression($email: String!) {
			removeSuppressionEmail(email: $email)
		}
	`;

	async function loadSuppressionList() {
		loading = true;
		try {
			const res = await graphqlQuery<{ getCrmSuppressionList: SuppressionItem[] }>(GET_SUPPRESSION_LIST, {
				variables: {
					search: searchQuery.trim() || null,
					reason: filterReason === 'ALL' ? null : filterReason
				}
			});
			if (res.success && res.data?.getCrmSuppressionList) {
				suppressionList = res.data.getCrmSuppressionList;
			}
		} catch (e: any) {
			toast.error('Failed to load suppression list: ' + e.message);
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		loadSuppressionList();
	});

	async function handleAddManual() {
		if (!newEmail.trim() || !newEmail.includes('@')) {
			toast.error('Please enter a valid email address.');
			return;
		}

		try {
			const res = await graphqlMutation(ADD_SUPPRESSION, {
				variables: {
					email: newEmail.trim(),
					reason: newReason
				}
			});

			if (res.success) {
				toast.success(`Added ${newEmail} to suppression list.`);
				newEmail = '';
				isAdding = false;
				await loadSuppressionList();
			}
		} catch (e: any) {
			toast.error(e.message || 'Failed to add suppression.');
		}
	}

	async function handleRemove(email: string) {
		if (!confirm(`Are you sure you want to unblock ${email}? They will become eligible for future marketing campaigns.`)) {
			return;
		}

		try {
			const res = await graphqlMutation(REMOVE_SUPPRESSION, {
				variables: { email }
			});

			if (res.success) {
				toast.success(`Unblocked ${email}.`);
				await loadSuppressionList();
			}
		} catch (e: any) {
			toast.error(e.message || 'Failed to unblock email.');
		}
	}

	function getReasonBadge(reason: string) {
		switch (reason) {
			case 'HardBounce':
				return 'bg-rose-500/10 text-rose-600 border-rose-500/20';
			case 'SpamComplaint':
				return 'bg-purple-500/10 text-purple-600 border-purple-500/20';
			case 'Unsubscribe':
				return 'bg-amber-500/10 text-amber-600 border-amber-500/20';
			default:
				return 'bg-slate-500/10 text-slate-600 border-slate-500/20';
		}
	}
</script>

<svelte:head>
	<title>Suppression List | Tyresoles CRM</title>
</svelte:head>

<PageHeading
	backHref="/crm-campaigns"
	backLabel="Back to Campaigns"
	icon="shield-alert"
	title="Global Suppression List"
	description="Permanent deliverability shield preventing sends to bounced, unsubscribed, or spam-reported addresses"
>
	{#snippet actions()}
		<Button size="sm" onclick={() => (isAdding = !isAdding)} class="gap-1.5">
			<Icon name={isAdding ? 'x' : 'plus'} class="w-4 h-4" />
			{isAdding ? 'Cancel' : 'Add to Suppression'}
		</Button>
	{/snippet}
</PageHeading>

<div class="p-6 max-w-6xl mx-auto space-y-6">
	<!-- Add Form Drawer/Card -->
	{#if isAdding}
		<div class="p-5 bg-card border border-primary/30 rounded-2xl shadow-sm space-y-4">
			<div class="flex items-center gap-2">
				<Icon name="shield-plus" class="w-5 h-5 text-primary" />
				<h3 class="text-sm font-bold text-foreground">Add Email to Permanent Suppression List</h3>
			</div>

			<div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
				<div class="sm:col-span-2 space-y-1">
					<label for="suppress-email" class="text-xs font-medium text-foreground">Email Address</label>
					<Input id="suppress-email" bind:value={newEmail} placeholder="e.g. client@example.com" />
				</div>
				<div class="space-y-1">
					<label for="suppress-reason" class="text-xs font-medium text-foreground">Reason</label>
					<select id="suppress-reason" bind:value={newReason} class="w-full h-9 px-3 text-xs rounded-md border border-input bg-background">
						<option value="Manual">Manual Block / Do-Not-Contact</option>
						<option value="HardBounce">Hard Bounce</option>
						<option value="Unsubscribe">Unsubscribed by Request</option>
						<option value="SpamComplaint">Spam Complaint</option>
					</select>
				</div>
			</div>

			<div class="flex justify-end gap-2 pt-2">
				<Button variant="outline" size="sm" onclick={() => (isAdding = false)}>Cancel</Button>
				<Button size="sm" onclick={handleAddManual} class="gap-1.5">
					<Icon name="check" class="w-4 h-4" />
					Save Suppression
				</Button>
			</div>
		</div>
	{/if}

	<!-- Search & Filters -->
	<div class="flex flex-col sm:flex-row items-center justify-between gap-3 bg-card p-4 border border-border/70 rounded-2xl">
		<div class="relative w-full sm:w-80">
			<Icon name="search" class="w-4 h-4 text-muted-foreground absolute left-3 top-1/2 -translate-y-1/2" />
			<Input
				bind:value={searchQuery}
				oninput={loadSuppressionList}
				placeholder="Search suppressed emails..."
				class="pl-9 text-xs"
			/>
		</div>

		<div class="flex items-center gap-2 w-full sm:w-auto">
			{#each ['ALL', 'HardBounce', 'Unsubscribe', 'SpamComplaint', 'Manual'] as reason}
				<button
					onclick={() => {
						filterReason = reason;
						loadSuppressionList();
					}}
					class="px-2.5 py-1 text-xs font-medium rounded-lg transition-colors {filterReason === reason
						? 'bg-primary text-primary-foreground shadow-sm'
						: 'text-muted-foreground hover:bg-muted'}"
				>
					{reason === 'ALL' ? 'All' : reason}
				</button>
			{/each}
		</div>
	</div>

	<!-- Suppression Table -->
	<div class="p-6 bg-card border border-border/70 rounded-2xl space-y-4">
		<div class="flex items-center justify-between">
			<div class="text-xs text-muted-foreground">
				Showing {suppressionList.length} suppressed addresses
			</div>
			<div class="text-[11px] text-emerald-600 font-medium flex items-center gap-1">
				<Icon name="shield-check" class="w-3.5 h-3.5" />
				All campaign sends automatically cross-reference this list
			</div>
		</div>

		<div class="overflow-x-auto border border-border/50 rounded-xl">
			<table class="w-full text-xs text-left">
				<thead class="bg-muted/50 text-muted-foreground border-b border-border/50 font-semibold">
					<tr>
						<th class="p-3">Email Address</th>
						<th class="p-3">Reason</th>
						<th class="p-3">Diagnostic Code</th>
						<th class="p-3">Added Date</th>
						<th class="p-3 text-right">Actions</th>
					</tr>
				</thead>
				<tbody class="divide-y divide-border/40">
					{#if loading}
						<tr>
							<td colspan="5" class="p-6 text-center text-muted-foreground">
								<Icon name="loader-2" class="w-5 h-5 animate-spin mx-auto mb-1" />
								Loading suppression list...
							</td>
						</tr>
					{:else if suppressionList.length === 0}
						<tr>
							<td colspan="5" class="p-6 text-center text-muted-foreground">
								No suppressed email addresses found.
							</td>
						</tr>
					{:else}
						{#each suppressionList as item (item.id)}
							<tr class="hover:bg-muted/20 transition-colors">
								<td class="p-3 font-mono font-medium text-foreground">{item.emailAddress}</td>
								<td class="p-3">
									<span class="px-2 py-0.5 text-[11px] font-semibold rounded-full border {getReasonBadge(item.reason)}">
										{item.reason}
									</span>
								</td>
								<td class="p-3 text-muted-foreground max-w-xs truncate">
									{item.diagnosticCode || '-'}
								</td>
								<td class="p-3 text-muted-foreground">
									{new Date(item.createdAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })}
								</td>
								<td class="p-3 text-right">
									<Button
										variant="ghost"
										size="sm"
										class="h-7 text-xs text-rose-600 hover:text-rose-700 hover:bg-rose-50"
										onclick={() => handleRemove(item.emailAddress)}
									>
										<Icon name="trash-2" class="w-3.5 h-3.5 mr-1" />
										Unblock
									</Button>
								</td>
							</tr>
						{/each}
					{/if}
				</tbody>
			</table>
		</div>
	</div>
</div>
