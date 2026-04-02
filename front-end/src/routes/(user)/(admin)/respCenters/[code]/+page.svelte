<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { useEntityDetail } from '$lib/composables';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardHeader, CardTitle } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Icon } from '$lib/components/venUI/icon';
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
	{/if}
</div>
