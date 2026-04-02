<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import * as Sheet from '$lib/components/ui/sheet';
	import { cn } from '$lib/utils';
	import { setContext } from 'svelte';
	import { NAVBAR_CTX_KEY } from './navbar-context';
    import type { Snippet } from 'svelte';

	let { class: className, children, title = "Menu" }: { class?: string, children: Snippet, title?: string } = $props();

	let open = $state(false);

	function closeMobileMenu() {
		open = false;
	}

	setContext(NAVBAR_CTX_KEY, { closeMobileMenu });
</script>

<Sheet.Root bind:open>
  <Sheet.Trigger class={cn("md:hidden", className)}>
    {#snippet child({ props })}
      <Button variant="ghost" size="icon" class="md:hidden" {...props}>
        <Icon name="menu" class="h-6 w-6" />
        <span class="sr-only">Toggle Menu</span>
      </Button>
    {/snippet}
  </Sheet.Trigger>
  <Sheet.Content side="left" class="pr-0">
    <Sheet.Header class="px-1 text-left">
      <Sheet.Title>{title}</Sheet.Title>
    </Sheet.Header>
    <div class="my-4 pb-10 pl-6 pr-6 overflow-y-auto">
      <div class="flex flex-col space-y-3">
        {@render children()}
      </div>
    </div>
  </Sheet.Content>
</Sheet.Root>
