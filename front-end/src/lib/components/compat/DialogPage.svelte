<script lang="ts">
	import * as Dialog from "$lib/components/ui/dialog";
	import { Button } from "$lib/components/ui/button";
	import { cn } from "$lib/utils";

	type Props = {
		open: boolean;
		onOpenChange?: (open: boolean) => void;
		title?: string;
		description?: string;
		actionLabel?: string;
		onAction?: () => void;
		children?: import("svelte").Snippet;
        class?: string;
	};

	let {
		open = $bindable(false),
		onOpenChange,
		title,
		description,
		actionLabel,
		onAction,
		children,
        class: className
	}: Props = $props();
</script>

<Dialog.Root bind:open onOpenChange={onOpenChange}>
	<Dialog.Content class={cn("max-w-4xl max-h-[90vh] overflow-y-auto", className)}>
		<Dialog.Header>
			{#if title}
				<Dialog.Title>{title}</Dialog.Title>
			{/if}
			{#if description}
				<Dialog.Description>{description}</Dialog.Description>
			{/if}
		</Dialog.Header>

		<div class="py-4">
			{@render children?.()}
		</div>

		{#if actionLabel || onAction}
			<Dialog.Footer>
				<Button variant="outline" onclick={() => (open = false)}>Cancel</Button>
				{#if actionLabel}
					<Button onclick={onAction}>{actionLabel}</Button>
				{/if}
			</Dialog.Footer>
		{/if}
	</Dialog.Content>
</Dialog.Root>
