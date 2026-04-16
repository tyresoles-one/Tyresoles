<script lang="ts">
	import type { Snippet } from 'svelte';
	import { cn } from '$lib/utils';
    
	type Props = {
		class?: string;
        children: Snippet;
	};

	let { class: className, children }: Props = $props();
</script>

<div class={cn("dashboard-grid", className)}>
    {@render children()}
</div>

<style>
    .dashboard-grid {
        display: grid;
        gap: 0.875rem;
        padding: 1rem;

        /* Mobile: 1 column */
        grid-template-columns: 1fr;
    }

    /* Small tablets / large phones: 2 columns */
    @media (min-width: 480px) {
        .dashboard-grid {
            grid-template-columns: repeat(2, 1fr);
        }
    }

    /* Tablets: 3 columns */
    @media (min-width: 768px) {
        .dashboard-grid {
            grid-template-columns: repeat(3, 1fr);
            gap: 1rem;
            padding: 1.25rem;
        }
    }

    /* Desktop: 4 columns */
    @media (min-width: 1024px) {
        .dashboard-grid {
            grid-template-columns: repeat(4, 1fr);
            gap: 1.125rem;
            padding: 1.5rem;
        }
    }

    /* Wide desktop: auto-fill with min tile width */
    @media (min-width: 1280px) {
        .dashboard-grid {
            grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
        }
    }

    /* Ensure tiles passed via slot fill their grid cell */
    :global(.dashboard-grid > *) {
        width: 100%;
        min-width: 0;
    }
</style>
