<script lang="ts">
	/**
	 * Empty state component for lists and data displays
	 * 
	 * @example
	 * ```svelte
	 * <EmptyState
	 *   icon="inbox"
	 *   title="No items found"
	 *   description="Try adjusting your filters or create a new item"
	 *   action={{ label: 'Create Item', onClick: () => createNew() }}
	 * />
	 * ```
	 */
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';

	type Action = {
		label: string;
		onClick: () => void;
		icon?: string;
	};

	type Props = {
		/** Icon name (default: 'inbox') */
		icon?: string;
		/** Main title */
		title: string;
		/** Optional description */
		description?: string;
		/** Optional action button */
		action?: Action;
		/** Additional CSS classes */
		class?: string;
	};

	let { icon = 'inbox', title, description, action, class: className = '' }: Props = $props();
</script>

<div class="flex flex-col items-center justify-center py-16 px-4 {className}">
	<div class="rounded-full bg-muted p-6 mb-4">
		<Icon name={icon} class="size-12 text-muted-foreground" />
	</div>

	<h3 class="text-lg font-semibold mb-2">{title}</h3>

	{#if description}
		<p class="text-sm text-muted-foreground text-center mb-6 max-w-md">
			{description}
		</p>
	{/if}

	{#if action}
		<Button onclick={action.onClick}>
			{#if action.icon}
				<Icon name={action.icon} class="size-4 mr-2" />
			{/if}
			{action.label}
		</Button>
	{/if}
</div>
