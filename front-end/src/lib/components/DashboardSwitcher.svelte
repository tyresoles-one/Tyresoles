<script module lang="ts">
  export type DashboardSwitcherOption = {
    /** Stable id; becomes the bound `value` when selected */
    id: string;
    label: string;
    /** venUI Icon `name` (optional; icon-only on the smallest breakpoints) */
    icon?: string;
    no?: number;
  };
</script>

<script lang="ts">
  import type { Snippet } from "svelte";
  import { tick } from "svelte";
  import { Icon } from "$lib/components/venUI/icon";

  let {
    options = [],
    value = $bindable(""),
    trailing,
    class: className = "",
    groupLabel = "Dashboard view",
  }: {
    options: DashboardSwitcherOption[];
    value?: string;
    trailing?: Snippet;
    class?: string;
    /** Accessible name for the segmented control */
    groupLabel?: string;
  } = $props();

  const selectedIndex = $derived.by(() => {
    const i = options.findIndex((o) => o.id === value);
    return i >= 0 ? i : 0;
  });

  const effectiveValue = $derived(options[selectedIndex]?.id ?? value);

  let trackEl = $state<HTMLDivElement | null>(null);
  let sliderLeft = $state(0);
  let sliderWidth = $state(0);

  async function layoutSlider() {
    await tick();
    const track = trackEl;
    if (!track) return;
    const btn = track.querySelectorAll<HTMLButtonElement>(".ds-option").item(selectedIndex);
    if (!btn) return;
    sliderLeft = btn.offsetLeft;
    sliderWidth = btn.offsetWidth;
  }

  $effect(() => {
    if (options.length === 0) return;
    if (!options.some((o) => o.id === value)) {
      value = options[0]!.id;
    }
  });

  $effect(() => {
    const el = trackEl;
    if (!el) return;
    const ro = new ResizeObserver(() => {
      void layoutSlider();
    });
    ro.observe(el);
    void layoutSlider();
    return () => ro.disconnect();
  });

  $effect(() => {
    selectedIndex;
    options.map((o) => `${o.id}:${o.label}`).join("|");
    void layoutSlider();
  });

  function select(id: string) {
    value = id;
  }
</script>

<div
  class="dashboard-switcher-root flex w-full flex-col items-stretch justify-between gap-4 sm:flex-row sm:items-center {className}"
>
  {#if options.length > 0}
    <div
      class="ds-bar shadow-xl shadow-primary/5"
      role="group"
      aria-label={groupLabel}
    >
      <div bind:this={trackEl} class="ds-track flex relative">
        <span
          class="ds-slider bg-linear-to-br from-primary to-primary/80"
          style:left="{sliderLeft}px"
          style:width="{`${Math.max(sliderWidth, 0)}px`}"
          style:opacity={sliderWidth > 0 ? 1 : 0}
        ></span>
        {#each options as opt (opt.id)}
          <button
            type="button"
            class="ds-option {opt.id === effectiveValue ? 'ds-option--active' : ''}"
            onclick={() => select(opt.id)}
            aria-pressed={opt.id === effectiveValue}
          >
            {#if opt.icon}
              <span class="ds-icon-wrap">
                <Icon name={opt.icon} class="ds-icon" />
              </span>
            {/if}
            <span class="ds-option-text uppercase tracking-tighter">{opt.label}</span>
          </button>
        {/each}
      </div>
    </div>
  {/if}

  {#if trailing}
    <div class="ds-trailing shrink-0 self-start sm:self-center">
      {@render trailing()}
    </div>
  {/if}
</div>

<style>
  /* Mobile-first segmented control */
  .ds-bar {
    position: relative;
    display: flex;
    width: 100%;
    min-width: 0;
    max-width: 100%;
    align-items: stretch;
    overflow-x: auto;
    overscroll-behavior-x: contain;
    scrollbar-width: none;
    border-radius: 999px;
    border: 1px solid var(--border);
    background: var(--muted);
    padding: 0.1875rem;
    box-shadow: 0 1px 3px color-mix(in oklch, var(--foreground) 4%, transparent);
  }

  .ds-bar::-webkit-scrollbar {
    display: none;
  }

  .ds-track {
    position: relative;
    display: flex;
    width: 100%;
    min-width: min-content;
    flex: 1 1 auto;
    align-items: stretch;
  }

  .ds-option {
    position: relative;
    z-index: 1;
    flex: 1 1 auto;
    min-width: max-content;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 0.375rem;
    padding: 0.4375rem 0.5rem;
    border: none;
    border-radius: 999px;
    background: transparent;
    font-size: 0.7rem;
    font-weight: 600;
    color: var(--muted-foreground);
    cursor: pointer;
    white-space: nowrap;
    transition:
      color 0.25s ease,
      transform 0.1s ease;
    -webkit-tap-highlight-color: transparent;
    user-select: none;
  }

  .ds-option:active {
    transform: scale(0.96);
  }

  .ds-option--active {
    color: var(--primary-foreground);
  }

  .ds-icon-wrap {
    display: flex;
    flex-shrink: 0;
    align-items: center;
    justify-content: center;
  }

  :global(.ds-icon) {
    width: 0.8rem;
    height: 0.8rem;
    color: inherit;
  }

  .ds-option-text {
    display: none;
    flex-shrink: 0;
    white-space: nowrap;
  }

  @media (min-width: 360px) {
    .ds-option-text {
      display: inline-block;
    }

    .ds-option {
      padding: 0.375rem 0.65rem;
    }
  }

  .ds-slider {
    position: absolute;
    top: 0.1875rem;
    left: 0;
    min-width: 2rem;
    height: calc(100% - 0.375rem);
    border-radius: 999px;
    background: var(--primary);
    box-shadow:
      0 1px 4px color-mix(in oklch, var(--primary) 30%, transparent),
      inset 0 1px 0 color-mix(in oklch, white 12%, transparent);
    transition:
      left 0.3s cubic-bezier(0.4, 0, 0.2, 1),
      width 0.3s cubic-bezier(0.4, 0, 0.2, 1),
      box-shadow 0.3s ease;
    pointer-events: none;
  }

  @media (hover: hover) {
    .ds-option:not(.ds-option--active):hover {
      color: var(--foreground);
    }

    .ds-bar:hover .ds-slider {
      box-shadow:
        0 2px 8px color-mix(in oklch, var(--primary) 40%, transparent),
        inset 0 1px 0 color-mix(in oklch, white 12%, transparent);
    }
  }

  @media (min-width: 480px) {
    .ds-option {
      padding: 0.375rem 0.85rem;
      font-size: 0.75rem;
    }

    :global(.ds-icon) {
      width: 0.875rem;
      height: 0.875rem;
    }
  }

  @media (min-width: 640px) {
    .ds-option {
      padding: 0.375rem 1rem;
    }
  }
</style>
