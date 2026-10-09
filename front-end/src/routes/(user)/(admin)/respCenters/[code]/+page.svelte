<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { useEntityDetail } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Icon } from '$lib/components/venUI/icon';
	import { Input } from '$lib/components/ui/input';
	import { Badge } from '$lib/components/ui/badge';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql/client';
	import { gql } from 'graphql-request';
	const GetResponsibilityCenterByCodeDocument: any = gql`query GetRespCenterMock { __typename }`;
	type GetResponsibilityCenterByCodeQuery = { responsibilityCenterByCode: Record<string, any> };

	const code = $derived(decodeURIComponent($page.params.code ?? '').trim());

	const rcDetail = useEntityDetail<
		GetResponsibilityCenterByCodeQuery,
		GetResponsibilityCenterByCodeQuery['responsibilityCenterByCode']
	>({
		id: () => code,
		query: GetResponsibilityCenterByCodeDocument,
		dataPath: 'responsibilityCenterByCode',
		cacheKey: (id) => `resp-center-${id}`
	});

	const rc = $derived(rcDetail.entity.value);

	let targetMultiplier = $state<number>(0);
	let isSavingMultiplier = $state(false);
	let isMultiplierLoaded = $state(false);

	const GET_MULTIPLIERS_QUERY = `
		query GetRespCenterTargetMultipliers {
			getRespCenterTargetMultipliers {
				code
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

	$effect(() => {
		if (code && !isMultiplierLoaded) {
			loadMultiplier();
		}
	});

	async function loadMultiplier() {
		try {
			const res = await graphqlQuery<{
				getRespCenterTargetMultipliers: Array<{ code: string; targetMultiplier: number }>;
			}>(GET_MULTIPLIERS_QUERY, { skipCache: true });
			if (res.success && res.data?.getRespCenterTargetMultipliers) {
				const match = res.data.getRespCenterTargetMultipliers.find(
					(m) => m.code.toUpperCase() === code.toUpperCase()
				);
				if (match) {
					targetMultiplier = match.targetMultiplier;
				}
				isMultiplierLoaded = true;
			}
		} catch (e) {
			console.error(e);
		}
	}

	async function saveMultiplier() {
		isSavingMultiplier = true;
		try {
			const res = await graphqlMutation<{
				updateRespCenterTargetMultiplier: { success: boolean; message: string };
			}>(UPDATE_MULTIPLIER_MUTATION, {
				variables: {
					respCenter: code,
					targetMultiplier: Number(targetMultiplier) || 0
				}
			});
			if (res.success && res.data?.updateRespCenterTargetMultiplier?.success) {
				toast.success(res.data.updateRespCenterTargetMultiplier.message || 'Target multiplier updated.');
			} else {
				toast.error(res.data?.updateRespCenterTargetMultiplier?.message || 'Failed to update multiplier.');
			}
		} catch (e: any) {
			toast.error(e.message || 'Error updating multiplier.');
		} finally {
			isSavingMultiplier = false;
		}
	}
</script>

<div class="space-y-6 p-4 md:p-6">
	<div class="flex flex-wrap items-center gap-3">
		<Button variant="ghost" size="sm" class="gap-2" onclick={() => goto('/respCenters')}>
			<Icon name="arrow-left" class="size-4" />
			Back to list
		</Button>
	</div>

	{#if !code}
		<Card class="border-destructive/30">
			<CardContent class="pt-6">
				<p class="text-sm text-muted-foreground">No responsibility center code in URL.</p>
				<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/respCenters')}>
					Back to list
				</Button>
			</CardContent>
		</Card>
	{:else if rcDetail.loading}
		<Card class="border-border/40">
			<CardHeader>
				<Skeleton class="h-6 w-64" />
			</CardHeader>
			<CardContent class="space-y-3">
				<Skeleton class="h-4 w-full" />
				<Skeleton class="h-4 w-3/4" />
				<Skeleton class="h-4 w-1/2" />
			</CardContent>
		</Card>
	{:else if rcDetail.error}
		<Card class="border-destructive/30">
			<CardContent class="pt-6">
				<p class="text-sm text-destructive">{rcDetail.error}</p>
				<Button variant="outline" size="sm" class="mt-3" onclick={() => rcDetail.reload()}>
					Retry
				</Button>
			</CardContent>
		</Card>
	{:else if !rc}
		<Card class="border-border/40">
			<CardContent class="pt-6">
				<p class="text-sm text-muted-foreground">
					Responsibility center <code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{code}</code> not found.
				</p>
				<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/respCenters')}>
					Back to list
				</Button>
			</CardContent>
		</Card>
	{:else}
		<Card class="border-border/40">
			<CardHeader>
				<CardTitle class="flex items-center gap-2 text-lg">
					<Icon name="building-2" class="size-5 text-primary" />
					{rc.name || rc.code}
				</CardTitle>
				{#if rc.name && rc.code}
					<p class="text-sm text-muted-foreground font-mono mt-1">{rc.code}</p>
				{/if}
			</CardHeader>
			<CardContent class="space-y-4">
				{#if rc.city?.trim()}
					<div class="flex items-center gap-2 text-sm">
						<Icon name="map-pin" class="size-4 shrink-0 text-muted-foreground" />
						<span>{rc.city}</span>
					</div>
				{/if}
				{#if rc.contact?.trim()}
					<div class="flex items-center gap-2 text-sm">
						<Icon name="user" class="size-4 shrink-0 text-muted-foreground" />
						<span>{rc.contact}</span>
					</div>
				{/if}
				{#if rc.phoneNo?.trim()}
					<div class="flex items-center gap-2 text-sm">
						<Icon name="phone" class="size-4 shrink-0 text-muted-foreground" />
						<a href="tel:{rc.phoneNo}" class="text-primary hover:underline">{rc.phoneNo}</a>
					</div>
				{/if}
				{#if !rc.city?.trim() && !rc.contact?.trim() && !rc.phoneNo?.trim()}
					<p class="text-sm text-muted-foreground">No contact details.</p>
				{/if}
			</CardContent>
		</Card>

		<!-- Sales Target Multiplier Card -->
		<Card class="border-border/70 shadow-sm">
			<CardHeader class="pb-3 border-b bg-muted/20">
				<div class="flex items-center justify-between">
					<CardTitle class="flex items-center gap-2 text-base">
						<Icon name="target" class="size-4 text-primary" />
						Sales Target Configuration
					</CardTitle>
					{#if targetMultiplier == 0 || targetMultiplier == 1}
						<Badge variant="outline" class="text-xs">1.00x (Flat / 100%)</Badge>
					{:else if targetMultiplier > 1}
						<Badge variant="secondary" class="text-xs bg-emerald-500/10 text-emerald-600 border-emerald-500/20">
							+{((targetMultiplier - 1) * 100).toFixed(1)}% Growth Multiplier
						</Badge>
					{:else}
						<Badge variant="outline" class="text-xs text-amber-600">
							-{((1 - targetMultiplier) * 100).toFixed(1)}%
						</Badge>
					{/if}
				</div>
			</CardHeader>
			<CardContent class="space-y-4 pt-4">
				<p class="text-xs text-muted-foreground">
					This multiplier is used by the <strong>Admin Tool Team Sales Target Generator</strong> to project next month targets for all teams under responsibility center <code class="font-mono text-foreground font-semibold">{code}</code>.
				</p>
				<div class="flex flex-col sm:flex-row sm:items-center gap-3">
					<div class="flex items-center gap-2">
						<label for="rc-target-multiplier" class="text-xs font-medium text-foreground whitespace-nowrap">
							Target Multiplier:
						</label>
						<Input
							id="rc-target-multiplier"
							type="number"
							step="0.01"
							min="0"
							max="10"
							bind:value={targetMultiplier}
							disabled={isSavingMultiplier}
							class="w-32 h-9 font-mono text-center text-xs"
							placeholder="1.00"
						/>
					</div>
					<Button
						variant="default"
						size="sm"
						onclick={saveMultiplier}
						disabled={isSavingMultiplier}
						class="gap-1.5 shadow-sm h-9"
					>
						{#if isSavingMultiplier}
							<Icon name="loader-2" class="size-3.5 animate-spin" />
							<span>Saving...</span>
						{:else}
							<Icon name="save" class="size-3.5" />
							<span>Save Multiplier</span>
						{/if}
					</Button>
				</div>
				<p class="text-[11px] text-muted-foreground">
					Note: If 0.00 or 1.00, the effective multiplier applied is 1.00 (100% of current sales). For a 5% increase, set to 1.05.
				</p>
			</CardContent>
		</Card>
	{/if}
</div>
