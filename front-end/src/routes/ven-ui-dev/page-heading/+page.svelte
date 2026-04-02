<script lang="ts">
	import { goto } from '$app/navigation';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { StatusBadge } from '$lib/components/venUI/statusBadge';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';

	let showLoading = $state(false);
	let sticky = $state(true);
	let showIcon = $state(true);
</script>

<div class="min-h-screen bg-background pb-12">
	<PageHeading
		backHref="/ven-ui-dev"
		backLabel="Back to VenUI dev"
		loading={showLoading}
		{sticky}
		icon={showIcon ? 'layout-panel-top' : undefined}
	>
		{#snippet title()}
			Page Heading
		{/snippet}
		{#snippet description()}
			<code class="font-mono text-xs">ven-ui-dev/page-heading</code>
		{/snippet}
		{#snippet actions()}
			<StatusBadge status={1} class="shrink-0 hidden sm:inline-flex text-xs" />
			<Button variant="outline" size="sm">Cancel</Button>
			<Button size="sm" class="min-w-[80px]">
				<Icon name="save" class="size-4 mr-2" />
				Save
			</Button>
		{/snippet}
	</PageHeading>

	<main class="container mx-auto px-4 py-6 max-w-3xl space-y-8">
		<Card>
			<CardHeader>
				<CardTitle>Page Heading component</CardTitle>
				<CardDescription>
					Sticky page header with gradient background, optional back button, optional icon, title and description snippets, and an actions slot.
					Matches the pattern used on detail pages (e.g. dealers/[id]).
				</CardDescription>
			</CardHeader>
			<CardContent class="space-y-4">
				<div class="flex flex-wrap gap-3 items-center">
					<label class="flex items-center gap-2 text-sm">
						<input type="checkbox" bind:checked={showLoading} class="rounded" />
						Show loading (skeleton title)
					</label>
					<label class="flex items-center gap-2 text-sm">
						<input type="checkbox" bind:checked={sticky} class="rounded" />
						Sticky header
					</label>
					<label class="flex items-center gap-2 text-sm">
						<input type="checkbox" bind:checked={showIcon} class="rounded" />
						Show icon
					</label>
				</div>
			</CardContent>
		</Card>

		<Card>
			<CardHeader>
				<CardTitle>Variants</CardTitle>
				<CardDescription>Other ways to use the component.</CardDescription>
			</CardHeader>
			<CardContent class="space-y-6">
				<div>
					<p class="text-xs font-medium text-muted-foreground mb-2">With back, title only (no description or actions)</p>
					<div class="rounded-lg border bg-muted/30 overflow-hidden">
						<PageHeading backHref="/ven-ui-dev" sticky={false}>
							{#snippet title()}
								Simple title
							{/snippet}
						</PageHeading>
					</div>
				</div>
				<div>
					<p class="text-xs font-medium text-muted-foreground mb-2">No back button, title + description (with icon)</p>
					<div class="rounded-lg border bg-muted/30 overflow-hidden">
						<PageHeading sticky={false} icon="settings">
							{#snippet title()}
								Settings
							{/snippet}
							{#snippet description()}
								Manage your account and preferences
							{/snippet}
						</PageHeading>
					</div>
				</div>
				<div>
					<p class="text-xs font-medium text-muted-foreground mb-2">Back + title + single action</p>
					<div class="rounded-lg border bg-muted/30 overflow-hidden">
						<PageHeading backHref="/ven-ui-dev" backLabel="Back" sticky={false}>
							{#snippet title()}
								Edit item
							{/snippet}
							{#snippet description()}
								ID: <code class="font-mono text-xs">item-123</code>
							{/snippet}
							{#snippet actions()}
								<Button size="sm">Done</Button>
							{/snippet}
						</PageHeading>
					</div>
				</div>
			</CardContent>
		</Card>
	</main>
</div>
