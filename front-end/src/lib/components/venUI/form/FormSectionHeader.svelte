<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { cn } from '$lib/utils';
    
	type Props = {
		title: string;
		icon?: string;
		class?: string;
	};

	let { title, icon, class: className }: Props = $props();
</script>

<div class={cn("flex items-center gap-2 mb-4", className)}>
	<div class="h-px flex-1 bg-gradient-to-r from-border/40 via-border/60 to-transparent"></div>
	<h3 class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2 px-4">
		{#if icon}
			<Icon name={icon} class="size-3.5" />
		{/if}
		{title}
	</h3>
	<div class="h-px flex-1 bg-gradient-to-l from-border/40 via-border/60 to-transparent"></div>
</div>
