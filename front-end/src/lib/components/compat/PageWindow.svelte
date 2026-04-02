<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { cn } from '$lib/utils';
	import type { ButtonProps } from '../custom/types';

	type Props = {
		actions?: ButtonProps[];
		loading?: boolean;
		children?: import('svelte').Snippet;
		class?: string;
	};

	let { actions = [], loading = false, children, class: className }: Props = $props();

	function iconToKebab(s: string): string {
		return s
			.replace(/([a-z0-9])([A-Z])/g, '$1-$2')
			.replace(/_/g, '-')
			.toLowerCase();
	}
</script>

<div class={cn('space-y-4', className)}>
	{#if actions.length > 0}
		<div class="flex flex-wrap items-center gap-2">
			{#each actions as a (a.label)}
				<Button
					type="button"
					variant={a.variant ?? 'default'}
					class={a.class}
					onclick={(e) => a.onclick?.(e)}
				>
					{#if a.icon}
						<Icon name={iconToKebab(a.icon)} class="size-4 mr-1.5" />
					{/if}
					{a.label ?? ''}
				</Button>
			{/each}
		</div>
	{/if}

	{#if loading}
		<div class="flex items-center gap-2 text-sm text-muted-foreground py-6">
			<Icon name="loader-2" class="size-4 animate-spin" />
			<span>Loading…</span>
		</div>
	{:else}
		{@render children?.()}
	{/if}
</div>
