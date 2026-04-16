<script lang="ts">
	import { cn } from '$lib/utils';
    import { Icon } from '$lib/components/venUI/icon';
    import type { Snippet } from 'svelte';

	type Props = {
		label: string;
		value?: string | number | null;
        icon?: string;
		class?: string;
        children?: Snippet;
	};

	let { label, value, icon, class: className, children }: Props = $props();
</script>

<div class={cn("flex flex-col gap-1", className)}>
	<span class="text-xs font-medium text-muted-foreground flex items-center gap-1.5">
        {#if icon}
            <Icon name={icon} class="size-3" />
        {/if}
        {label}
    </span>
    {#if children}
        {@render children()}
    {:else}
	    <span class="font-medium text-foreground text-sm">{value ?? '—'}</span>
    {/if}
</div>
