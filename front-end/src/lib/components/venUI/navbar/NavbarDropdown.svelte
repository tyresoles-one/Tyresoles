<script lang="ts">
	import { Dropdown } from '$lib/components/venUI/dropdowns';
    import type { DropdownItem } from '$lib/components/venUI/dropdowns/types';
	import { cn } from '$lib/utils';
    import { Icon } from '$lib/components/venUI/icon';
	import type { ComponentProps } from 'svelte';

    // We accept Dropdown props but override trigger to be our custom layout
    // We can't really extend DropdownProps easily because we want to intercept 'trigger'
    
    interface Props {
        icon?: string;
        label: string;
        items: DropdownItem[];
        class?: string;
        isActive?: boolean;
    }

	let { label, icon, items, class: className, isActive = false, ...rest }: Props = $props();
</script>

<Dropdown {items} trigger={{ label }} {...rest} class={className}>
  {#snippet children({ props })}
    <button
      type="button"
      class={cn(
        "group flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition-all duration-200 ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
        isActive
          ? "bg-primary/10 text-primary"
          : "text-muted-foreground hover:bg-accent hover:text-accent-foreground",
        props["data-state"] === "open" && "bg-accent/50 text-accent-foreground",
        className,
      )}
      {...props}
    >
      {#if icon}
        <Icon name={icon} class="size-4" />
      {/if}
      {label}
      <Icon
        name="chevron-down"
        class={cn(
          "ml-1 size-3 opacity-50 transition-transform duration-200",
          props["data-state"] === "open" && "rotate-180",
        )}
      />
    </button>
  {/snippet}
</Dropdown>
