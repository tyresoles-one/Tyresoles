<script lang="ts">
	/**
	 * No permission display: shown when user is logged in but lacks required permission.
	 */
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { goto } from '$app/navigation';

	type Props = {
		/** The permission that was required (shown in message) */
		requiredPermission: string;
		/** Optional action to go back / home */
		showBackAction?: boolean;
		/** Path for back button (default: /) */
		backPath?: string;
		/** Additional CSS classes */
		class?: string;
	};

	let {
		requiredPermission,
		showBackAction = true,
		backPath = '/',
		class: className = '',
	}: Props = $props();
</script>

<div
	class="flex flex-col items-center justify-center min-h-[280px] px-4 py-12 {className}"
	role="alert"
	aria-live="polite"
>
	<div class="rounded-full bg-destructive/10 p-6 mb-4">
		<Icon name="shield-off" class="size-12 text-destructive" />
	</div>

	<h2 class="text-lg font-semibold mb-2">Access denied</h2>

	<p class="text-sm text-muted-foreground text-center mb-6 max-w-md">
		You don't have permission to access this page.
		{#if requiredPermission}
			Required permission: <span class="font-medium text-foreground">{requiredPermission}</span>
		{/if}
	</p>

	{#if showBackAction}
		<Button onclick={() => goto(backPath)} variant="outline">
			<Icon name="arrow-left" class="size-4 mr-2" />
			Go back
		</Button>
	{/if}
</div>
