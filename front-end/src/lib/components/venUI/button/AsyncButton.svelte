<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import type { ComponentProps } from 'svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { cn } from '$lib/utils';
    
	type ButtonProps = ComponentProps<typeof Button>;

	type Props = ButtonProps & {
		loading?: boolean;
		icon?: string;
		loadingText?: string;
		class?: string;
	};

	let { loading = false, icon, loadingText = 'Saving...', class: className, children, ...rest }: Props = $props();
</script>

<Button class={cn('min-w-[100px]', className)} disabled={loading || rest.disabled} {...rest}>
	{#if loading}
		<Icon name="loader-2" class="size-4 animate-spin mr-2" />
		{loadingText}
	{:else}
		{#if icon}
			<Icon name={icon} class="size-4 mr-2" />
		{/if}
		{@render children?.()}
	{/if}
</Button>
