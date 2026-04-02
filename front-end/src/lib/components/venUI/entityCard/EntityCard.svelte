<script lang="ts" generics="T">
	/**
	 * Reusable entity card component for grid views
	 * 
	 * @example
	 * ```svelte
	 * <EntityCard
	 *   icon="store"
	 *   title={dealer.name}
	 *   subtitle={dealer.code}
	 *   metadata={[
	 *     { icon: 'map-pin', label: 'City', value: dealer.city },
	 *     { icon: 'phone', label: 'Phone', value: dealer.phoneNo }
	 *   ]}
	 *   onclick={() => goto(`/dealers/${dealer.code}`)}
	 * />
	 * ```
	 */
	import { Card, CardContent } from '$lib/components/ui/card';
	import { Icon } from '$lib/components/venUI/icon';

	type Metadata = {
		icon: string;
		label: string;
		value: string;
	};

	type Props = {
		/** Icon name for the card */
		icon: string;
		/** Main title */
		title: string;
		/** Subtitle (usually code/ID) */
		subtitle?: string;
		/** Metadata rows to display */
		metadata?: Metadata[];
		/** Click handler */
		onclick?: () => void;
		/** Additional CSS classes */
		class?: string;
	};

	let { icon, title, subtitle, metadata = [], onclick, class: className = '' }: Props = $props();
</script>

<Card
	class="h-full group relative overflow-hidden transition-all duration-300 hover:shadow-md hover:border-primary/50 bg-card/50 backdrop-blur-sm border-border/40 cursor-pointer {className}"
	role="button"
	tabindex={0}
	{onclick}
	onkeydown={(e) => e.key === 'Enter' && onclick?.()}
>
	<CardContent class="p-3">
		<div class="flex items-start gap-3">
			<div
				class="relative shrink-0 flex size-10 items-center justify-center rounded-lg ring-1 ring-border shadow-sm bg-primary/10 text-primary transition-transform group-hover:scale-105"
			>
				<Icon name={icon} class="size-5" />
			</div>

			<div class="flex flex-col min-w-0 flex-1">
				<h3
					class="font-semibold text-sm truncate text-foreground group-hover:text-primary transition-colors"
				>
					{title}
				</h3>

				{#if subtitle}
					<p class="text-xs text-muted-foreground truncate font-mono mt-0.5">
						{subtitle}
					</p>
				{/if}

				{#if metadata.length > 0}
					<div class="mt-3 grid gap-1.5">
						{#each metadata as meta}
							<div class="flex items-center gap-1.5 text-xs">
								<Icon name={meta.icon} class="size-3 text-muted-foreground shrink-0" />
								<span class="text-muted-foreground">{meta.label}:</span>
								<span class="font-medium truncate">{meta.value}</span>
							</div>
						{/each}
					</div>
				{/if}
			</div>
		</div>
	</CardContent>
</Card>
