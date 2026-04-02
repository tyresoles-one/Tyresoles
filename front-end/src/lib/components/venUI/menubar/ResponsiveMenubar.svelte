<script lang="ts">
  import {
    Menubar,
    MenubarCheckboxItem,
    MenubarContent,
    MenubarItem,
    MenubarMenu,
    MenubarRadioGroup,
    MenubarRadioItem,
    MenubarSeparator,
    MenubarShortcut,
    MenubarSub,
    MenubarSubContent,
    MenubarSubTrigger,
    MenubarTrigger,
    MenubarPortal,
  } from "$lib/components/ui/menubar";
  import { Icon } from "$lib/components/venUI/icon";
  import { cn } from "$lib/utils";
  import type { MenuItem } from "./types";
  import { goto } from "$app/navigation";
  import { onMount, onDestroy } from "svelte";

  const TRIGGER_GAP = 4;
  const MORE_RESERVE_WIDTH = 56;

  let {
    menus = [],
    moreLabel = "More",
    class: className,
  }: {
    menus: MenuItem[];
    moreLabel?: string;
    class?: string;
  } = $props();

  let containerRef = $state<HTMLDivElement | null>(null);
  let rulerRef = $state<HTMLDivElement | null>(null);
  let containerWidth = $state(0);
  let visibleCountState = $state(0);
  let visibleCount = $derived(
    containerWidth === 0 ? menus.length : visibleCountState,
  );

  let resizeObserver: ResizeObserver | null = null;
  let resizeTimeout: ReturnType<typeof setTimeout> | null = null;

  function measure() {
    if (!containerRef || !rulerRef || !menus.length) return;
    const w = containerRef.clientWidth;
    if (w <= 0) {
      return;
    }
    containerWidth = w;
    const items = rulerRef.querySelectorAll<HTMLElement>("[data-ruler-item]");
    if (items.length === 0) {
      visibleCountState = menus.length;
      return;
    }
    const widths = Array.from(items).map((el) => el.offsetWidth);
    let sum = 0;
    let count = 0;
    for (let i = 0; i < widths.length; i++) {
      const need = sum + widths[i] + (i > 0 ? TRIGGER_GAP : 0);
      if (need > w - MORE_RESERVE_WIDTH) break;
      sum += widths[i] + (i > 0 ? TRIGGER_GAP : 0);
      count += 1;
    }
    visibleCountState = Math.min(Math.max(0, count), menus.length);
  }

  function scheduleMeasure() {
    if (resizeTimeout) clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
      measure();
      resizeTimeout = null;
    }, 80);
  }

  $effect(() => {
    menus;
    if (containerWidth === 0 && containerRef) scheduleMeasure();
  });

  onMount(() => {
    if (!containerRef) return;
    resizeObserver = new ResizeObserver(() => scheduleMeasure());
    resizeObserver.observe(containerRef);
    scheduleMeasure();
  });

  onDestroy(() => {
    if (resizeObserver && containerRef) {
      resizeObserver.disconnect();
      resizeObserver = null;
    }
    if (resizeTimeout) {
      clearTimeout(resizeTimeout);
      resizeTimeout = null;
    }
  });

  let visibleMenus = $derived(menus.slice(0, visibleCount));
  let overflowMenus = $derived(menus.slice(visibleCount));

  function handleItemClick(item: MenuItem) {
    if (item.disabled) return;
    if (item.onClick) item.onClick();
    if (item.href) goto(item.href);
  }

  type GroupedItem =
    | { type: "item"; data: MenuItem }
    | { type: "radio-group"; items: MenuItem[] };

  function groupItems(items: MenuItem[]): GroupedItem[] {
    if (!items) return [];
    const res: GroupedItem[] = [];
    let currentRadioGroup: MenuItem[] = [];
    for (const item of items) {
      if (item.type === "radio") {
        currentRadioGroup.push(item);
      } else {
        if (currentRadioGroup.length > 0) {
          res.push({ type: "radio-group", items: [...currentRadioGroup] });
          currentRadioGroup = [];
        }
        res.push({ type: "item", data: item });
      }
    }
    if (currentRadioGroup.length > 0) {
      res.push({ type: "radio-group", items: [...currentRadioGroup] });
    }
    return res;
  }

  const contentClass =
    "bg-background/95 backdrop-blur-xl border-border/40 shadow-xl rounded-md";
  const triggerClass =
    "data-[state=open]:bg-primary/10 data-[state=open]:text-primary focus:bg-primary/10 focus:text-primary transition-all duration-200";
</script>

<div
  bind:this={containerRef}
  class="flex w-full min-w-0"
  style="max-width: 100%;"
>
  <!-- Hidden ruler: same content as triggers for accurate width measurement -->
  <div
    bind:this={rulerRef}
    aria-hidden="true"
    class="invisible absolute -left-[9999px] flex h-9 items-center gap-1 overflow-hidden"
    style="pointer-events: none;"
  >
    {#each menus as menu (menu.id)}
      <div
        data-ruler-item
        class="flex items-center rounded-sm px-2 py-1 text-sm font-medium whitespace-nowrap"
      >
        {#if menu.icon}
          <span class="mr-2 size-4 shrink-0 opacity-60" aria-hidden="true">
            <Icon name={menu.icon} class="size-4" />
          </span>
        {/if}
        {menu.label}
      </div>
    {/each}
  </div>

  <Menubar
    class={cn(
      "border-none bg-transparent shadow-none flex-1 min-w-0",
      className,
    )}
  >
    {#each visibleMenus as menu (menu.id)}
      <MenubarMenu>
        <MenubarTrigger class={triggerClass}>
          {#if menu.icon}
            <Icon
              name={menu.icon}
              class="mr-2 size-4 shrink-0 opacity-60"
              aria-hidden="true"
            />
          {/if}
          {menu.label}
        </MenubarTrigger>
        <MenubarContent class={contentClass}>
          {@render menuContent(menu)}
        </MenubarContent>
      </MenubarMenu>
    {/each}

    {#if overflowMenus.length > 0}
      <MenubarMenu>
        <MenubarTrigger class={triggerClass}>
          <Icon
            name="ellipsis"
            class="mr-2 size-4 shrink-0 opacity-60"
            aria-hidden="true"
          />
          {moreLabel}
        </MenubarTrigger>
        <MenubarContent class={contentClass}>
          {#each overflowMenus as menu (menu.id)}
            <MenubarSub>
              <MenubarSubTrigger inset={!menu.icon}>
                {#if menu.icon}
                  <Icon
                    name={menu.icon}
                    class="mr-2 size-4 shrink-0 opacity-60"
                    aria-hidden="true"
                  />
                {/if}
                <span>{menu.label}</span>
              </MenubarSubTrigger>
              <MenubarPortal>
                <MenubarSubContent class={contentClass}>
                  {@render menuContent(menu)}
                </MenubarSubContent>
              </MenubarPortal>
            </MenubarSub>
          {/each}
        </MenubarContent>
      </MenubarMenu>
    {/if}
  </Menubar>
</div>

{#snippet menuContent(menu: MenuItem)}
  {#each groupItems(menu.children ?? []) as section (section.type === "item" ? section.data.id : "radio-" + (section.items[0]?.id ?? Math.random()))}
    {#if section.type === "radio-group"}
      <MenubarRadioGroup
        value={section.items.find((x) => x.checked)?.id ?? ""}
        onValueChange={(val) => {
          const target = section.items.find((i) => i.id === val);
          if (target) {
            section.items.forEach((i) => (i.checked = i.id === val));
            handleItemClick(target);
          }
        }}
      >
        {#each section.items as rItem (rItem.id)}
          <MenubarRadioItem value={rItem.id}>
            {rItem.label}
          </MenubarRadioItem>
        {/each}
      </MenubarRadioGroup>
    {:else}
      {@render menuItem(section.data)}
    {/if}
  {/each}
{/snippet}

{#snippet menuItem(item: MenuItem)}
  {#if item.separator || item.type === "separator"}
    <MenubarSeparator />
  {:else if item.children && item.children.length > 0}
    <MenubarSub>
      <MenubarSubTrigger
        inset={!item.icon}
        class={item.icon ? "!pl-1.5 pr-2 gap-2" : "gap-2"}
      >
        {#if item.icon}
          <Icon
            name={item.icon}
            class="mr-2 size-4 shrink-0 opacity-60 text-muted-foreground"
            aria-hidden="true"
          />
        {/if}
        <span>{item.label}</span>
      </MenubarSubTrigger>
      <MenubarPortal>
        <MenubarSubContent class={contentClass}>
          {#each groupItems(item.children ?? []) as section (section.type === "item" ? section.data.id : "radio-" + (section.items[0]?.id ?? Math.random()))}
            {#if section.type === "radio-group"}
              <MenubarRadioGroup
                value={section.items.find((x) => x.checked)?.id ?? ""}
                onValueChange={(val) => {
                  const target = section.items.find((i) => i.id === val);
                  if (target) {
                    section.items.forEach((i) => (i.checked = i.id === val));
                    handleItemClick(target);
                  }
                }}
              >
                {#each section.items as rItem (rItem.id)}
                  <MenubarRadioItem value={rItem.id}>
                    {rItem.label}
                  </MenubarRadioItem>
                {/each}
              </MenubarRadioGroup>
            {:else}
              {@render menuItem(section.data)}
            {/if}
          {/each}
        </MenubarSubContent>
      </MenubarPortal>
    </MenubarSub>
  {:else if item.type === "checkbox"}
    <MenubarCheckboxItem
      bind:checked={item.checked}
      onclick={() => handleItemClick(item)}
      disabled={item.disabled}
    >
      {item.label}
      {#if item.shortcut}
        <MenubarShortcut>{item.shortcut}</MenubarShortcut>
      {/if}
    </MenubarCheckboxItem>
  {:else}
    <MenubarItem
      onclick={() => handleItemClick(item)}
      disabled={item.disabled}
      inset={!item.icon}
      class={item.icon ? "!pl-1.5 pr-2" : ""}
    >
      {#if item.icon}
        <Icon
          name={item.icon}
          class="mr-2 size-4 shrink-0 opacity-60"
          aria-hidden="true"
        />
      {/if}
      {item.label}
      {#if item.shortcut}
        <MenubarShortcut>{item.shortcut}</MenubarShortcut>
      {/if}
    </MenubarItem>
  {/if}
{/snippet}
