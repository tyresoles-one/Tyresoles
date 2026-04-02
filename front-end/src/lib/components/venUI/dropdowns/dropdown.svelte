<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import { Icon } from "$lib/components/venUI/icon";
  import {
    DropdownMenu,
    DropdownMenuCheckboxItem,
    DropdownMenuContent,
    DropdownMenuGroup,
    DropdownMenuItem,
    DropdownMenuLabel,
    DropdownMenuPortal,
    DropdownMenuRadioGroup,
    DropdownMenuRadioItem,
    DropdownMenuSeparator,
    DropdownMenuShortcut,
    DropdownMenuSub,
    DropdownMenuSubContent,
    DropdownMenuSubTrigger,
    DropdownMenuTrigger,
  } from "$lib/components/ui/dropdown-menu";
  import type { DropdownProps, DropdownItem } from "./types";
  import type { Snippet } from "svelte";

  let {
    trigger,
    items,
    align = "start",
    sideOffset = 4,
    class: className,
    children,
  }: DropdownProps & {
    children?: Snippet<[{ props: Record<string, unknown> }]>;
  } = $props();
</script>

{#snippet renderItems(items: DropdownItem[])}
  {#each items as item}
    {#if item.type === "item"}
      <DropdownMenuItem
        disabled={item.disabled}
        onclick={item.onClick}
        class={item.class ||
          (item.variant === "destructive"
            ? "text-destructive focus:text-destructive"
            : "")}
      >
        {#if item.icon}
          <Icon name={item.icon} class="mr-2 h-4 w-4 opacity-70" />
        {/if}
        <span>{item.label}</span>
        {#if item.shortcut}
          <DropdownMenuShortcut>{item.shortcut}</DropdownMenuShortcut>
        {/if}
      </DropdownMenuItem>
    {:else if item.type === "checkbox"}
      <DropdownMenuCheckboxItem
        checked={item.checked}
        onCheckedChange={item.onCheckedChange}
        disabled={item.disabled}
      >
        <!-- CheckboxItem usually renders a check icon automatically on left, so we just put label here. 
                     If user wants extra icon, we could add it, but standard is just check. -->
        {item.label}
      </DropdownMenuCheckboxItem>
    {:else if item.type === "radio-group"}
      <DropdownMenuRadioGroup
        value={item.value}
        onValueChange={item.onValueChange}
      >
        {#each item.options as option}
          <DropdownMenuRadioItem
            value={option.value}
            disabled={option.disabled}
          >
            {option.label}
          </DropdownMenuRadioItem>
        {/each}
      </DropdownMenuRadioGroup>
    {:else if item.type === "sub"}
      <DropdownMenuSub>
        <DropdownMenuSubTrigger inset={!item.icon}>
          {#if item.icon}
            <Icon name={item.icon} class="mr-2 h-4 w-4" />
          {/if}
          <span>{item.label}</span>
        </DropdownMenuSubTrigger>
        <DropdownMenuSubContent align="start" sideOffset={-38}>
          {@render renderItems(item.children)}
        </DropdownMenuSubContent>
      </DropdownMenuSub>
    {:else if item.type === "separator"}
      <DropdownMenuSeparator />
    {:else if item.type === "label"}
      <DropdownMenuLabel>{item.label}</DropdownMenuLabel>
    {:else if item.type === "group"}
      <DropdownMenuGroup>
        {@render renderItems(item.children)}
      </DropdownMenuGroup>
    {:else if item.type === "custom"}
      {@render item.render()}
    {/if}
  {/each}
{/snippet}

<DropdownMenu>
  <DropdownMenuTrigger>
    {#snippet child({ props })}
      {#if children}
        {@render children({ props })}
      {:else if trigger}
        <Button
          variant={trigger.variant ?? "outline"}
          size={trigger.size}
          disabled={trigger.disabled}
          class={trigger.class}
          {...props}
        >
          {#if trigger.iconOnly}
            <Icon name={trigger.icon ?? "more-horizontal"} class="h-4 w-4" />
            <span class="sr-only">{trigger.label}</span>
          {:else}
            {trigger.label}
            {#if trigger.showChevron !== false}
              <Icon
                name={trigger.icon ?? "chevron-down"}
                class="-me-1 opacity-60"
              />
            {/if}
          {/if}
        </Button>
      {/if}
    {/snippet}
  </DropdownMenuTrigger>
  <DropdownMenuContent {align} {sideOffset} class={className}>
    {@render renderItems(items)}
  </DropdownMenuContent>
</DropdownMenu>
