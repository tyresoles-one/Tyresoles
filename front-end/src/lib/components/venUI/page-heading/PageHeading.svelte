<script lang="ts">
	import type { Snippet } from 'svelte';
	import { goto } from '$app/navigation';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { cn } from '$lib/utils';

	let {
		/** Back navigation target. When set, shows a back button that navigates here. */
		backHref = undefined as string | undefined,
		/** Optional label for the back button (e.g. "Back to list"). */
		backLabel = 'Back',
		/** Optional string for the document <title> tags. Useful if title is a Snippet or you want a different title. */
		pageTitle = undefined as string | undefined,
		/** When true, the title area shows a skeleton instead of the title snippet. */
		loading = false,
		/** When true, header is sticky with backdrop blur. Default true. */
		sticky = true,
		/** Extra class for the header element. */
		class: className = '',
		/** Optional icon name (e.g. "store", "file-text") shown next to the title. */
		icon = undefined as string | undefined,
		/** Main heading content. Can be a string or a Snippet. */
		title = undefined as Snippet | string | undefined,
		/** Optional subtitle/description below the title (hidden on small screens). */
		description = undefined as Snippet | string | undefined,
		/** Optional actions (buttons, badges) on the right. */
		actions = undefined as Snippet | undefined,
	}: {
		backHref?: string;
		backLabel?: string;
		pageTitle?: string;
		loading?: boolean;
		sticky?: boolean;
		class?: string;
		icon?: string;
		title?: Snippet | string;
		description?: Snippet | string;
		actions?: Snippet;
	} = $props();

	function handleBack() {
		if (backHref) goto(backHref);
	}
</script>

<svelte:head>
	{#if pageTitle || typeof title === 'string'}
		<title>{pageTitle || title} - Tyresoles</title>
	{/if}
</svelte:head>

<header
	class={cn(
		'w-full border-b border-border/40 bg-gradient-to-r from-primary/5 via-background/90 to-background backdrop-blur-xl rounded-b-lg',
		sticky && 'sticky top-0 z-40',
		className,
	)}
>
	<div class="container mx-auto px-4 py-3 flex flex-col sm:flex-row sm:items-center justify-between gap-3 sm:gap-4">
		<div class="flex items-center gap-2.5 sm:gap-3 min-w-0 overflow-hidden">
			{#if backHref}
				<Button
					variant="ghost"
					size="icon"
					onclick={handleBack}
					class="shrink-0 size-8 -ml-1 sm:ml-0"
					aria-label={backLabel}
				>
					<Icon name="arrow-left" class="size-4" />
				</Button>
			{/if}
			{#if icon}
				<div
					class="shrink-0 flex size-8 sm:size-9 items-center justify-center rounded-lg bg-gradient-to-br from-primary/95 to-primary/80 text-primary-foreground shadow-md shadow-primary/10"
					aria-hidden="true"
				>
					<Icon name={icon} class="size-4 sm:size-4" />
				</div>
			{/if}
			<div class="min-w-0 flex-1 flex flex-col">
				<h1 class="text-base sm:text-lg font-extrabold tracking-tight leading-tight">
					{#if loading}
						<Skeleton class="h-6 w-32 sm:w-48" />
					{:else if typeof title === 'string'}
						<span class="truncate block">{title}</span>
					{:else if title}
						{@render title()}
					{/if}
				</h1>
				{#if description}
					<p class="text-[10px] sm:text-xs text-muted-foreground font-medium mt-0.5 max-w-md truncate sm:whitespace-normal">
						{#if typeof description === 'string'}
							{description}
						{:else}
							{@render description()}
						{/if}
					</p>
				{/if}
			</div>
		</div>
		{#if actions}
			<div class="flex items-center gap-1.5 sm:gap-2 sm:ml-auto w-full sm:w-auto overflow-x-auto pb-0.5 sm:pb-0 scrollbar-none">
				{@render actions()}
			</div>
		{/if}
	</div>
</header>
