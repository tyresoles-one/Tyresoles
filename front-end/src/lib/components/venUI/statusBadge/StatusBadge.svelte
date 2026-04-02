<script lang="ts">
	/**
	 * Status badge component with consistent styling
	 * 
	 * @example
	 * ```svelte
	 * <StatusBadge status={0} /> <!-- Shows "Active" -->
	 * <StatusBadge status={1} /> <!-- Shows "Inactive" -->
	 * <StatusBadge status={0} context="ACTIVE_DISABLED" /> <!-- Shows "Active" -->
	 * ```
	 */
	import { Badge } from '$lib/components/ui/badge';
	import { getStatusConfig, getStatusLabelWithContext } from '$lib/utils/status';
	import type { STATUS_LABELS } from '$lib/utils/status';

	type Props = {
		/** Status code (0 = active, 1 = inactive) */
		status: number;
		/** Label context for custom labels */
		context?: keyof typeof STATUS_LABELS;
		/** Additional CSS classes */
		class?: string;
	};

	let { status, context, class: className = '' }: Props = $props();

	const config = $derived(getStatusConfig(status));
	const label = $derived(
		context ? getStatusLabelWithContext(status, context) : config.label
	);
</script>

<Badge variant={config.variant} class={className}>
	{label}
</Badge>
