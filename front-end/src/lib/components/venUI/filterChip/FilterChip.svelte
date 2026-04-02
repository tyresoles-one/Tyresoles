<script lang="ts">
	/**
	 * Filter chip component for filter bars
	 * 
	 * @example
	 * ```svelte
	 * <FilterChip label="All users" />
	 * <FilterSeparator />
	 * <FilterChip label="Active only" />
	 * ```
	 */
	type Props = {
		/** Filter label text */
		label: string;
		/** Additional CSS classes */
		class?: string;
	};

	let { label, class: className = '' }: Props = $props();
</script>

<div
	class="hidden sm:flex items-center gap-1.5 p-1 bg-muted/30 rounded-lg border border-border/20 {className}"
>
	<span class="px-2.5 py-1 text-xs font-medium text-muted-foreground">
		{label}
	</span>
</div>
