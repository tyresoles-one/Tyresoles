<script lang="ts">
	import { cn } from '$lib/utils';
	import type { HTMLAttributes } from 'svelte/elements';
    import type { Snippet } from 'svelte';

	interface Props extends HTMLAttributes<HTMLElement> {
		orientation?: 'horizontal' | 'vertical';
        children: Snippet;
	}

	let { class: className, orientation = 'horizontal', children, ...props }: Props = $props();
</script>

<nav
  class={cn(
    "flex items-center gap-1 text-sm font-medium",
    orientation === "vertical" && "flex-col items-start gap-2 mt-2",
    className,
  )}
  {...props}
>
  {@render children()}
</nav>
