<script lang="ts">
	/**
	 * Skeleton grid for loading states
	 * 
	 * @example
	 * ```svelte
	 * {#if loading}
	 *   <SkeletonGrid count={8} columns={4} />
	 * {:else}
	 *   <!-- Actual content -->
	 * {/if}
	 * ```
	 */
	import { Skeleton } from '$lib/components/ui/skeleton';

	type Props = {
		/** Number of skeleton items */
		count?: number;
		/** Number of columns (responsive) */
		columns?: number;
		/** Additional CSS classes */
		class?: string;
	};

	let { count = 8, columns = 4, class: className = '' }: Props = $props();

	const gridClass = $derived(
		`grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-${columns} ${className}`
	);
</script>

<div class={gridClass}>
	{#each Array(count) as _}
		<div class="border rounded-lg p-4 space-y-3 bg-card">
			<div class="flex gap-3">
				<Skeleton class="size-10 rounded-lg shrink-0" />
				<div class="flex-1 space-y-2">
					<Skeleton class="h-4 w-3/4" />
					<Skeleton class="h-3 w-1/2" />
				</div>
			</div>
			<div class="space-y-2">
				<Skeleton class="h-3 w-full" />
				<Skeleton class="h-3 w-2/3" />
			</div>
		</div>
	{/each}
</div>
