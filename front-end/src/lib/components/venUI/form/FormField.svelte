<script lang="ts">
	import type { Snippet } from 'svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { cn } from '$lib/utils';
    
	type Props = {
		id?: string;
		label?: string;
		icon?: string;
		required?: boolean;
		error?: string;
		class?: string;
		children: Snippet;
	};

	let { id, label, icon, required = false, error, class: className, children }: Props = $props();
</script>

<div class={cn("space-y-1.5", className)}>
	{#if label}
		<label for={id} class="text-xs font-medium text-muted-foreground flex items-center gap-1">
			{#if icon}
				<Icon name={icon} class="size-3" />
			{/if}
			{label}
			{#if required}
				<span class="text-destructive" aria-hidden="true">*</span>
			{/if}
		</label>
	{/if}
	
	{@render children()}

	{#if error}
		<p id={id ? `${id}-error` : undefined} class="text-xs text-destructive" role="alert">{error}</p>
	{/if}
</div>
