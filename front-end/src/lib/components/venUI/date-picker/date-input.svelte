<script lang="ts">
    import { cn } from "$lib/utils";
    import { Icon } from "$lib/components/venUI/icon";

    type Props = {
        value?: any; // Single value or {start, end}
        placeholder?: string;
        disabled?: boolean;
        mode?: 'single' | 'range';
        formatDisplay: (val: any) => string;
        onClear?: () => void;
        isActive?: boolean; // Is the popover open?
        id?: string;
        "aria-invalid"?: boolean | "true" | "false";
    };

    let { 
        value, 
        placeholder = "Select date", 
        disabled = false, 
        mode = 'single', 
        formatDisplay,
        onClear,
        isActive = false,
        id,
        "aria-invalid": ariaInvalid
    }: Props = $props();

    let isHovered = $state(false);
    let showClear = $derived(isHovered && value && !disabled);

    // Range placeholders
    let startPlaceholder = $derived(mode === 'range' ? "Start date" : placeholder);
    let endPlaceholder = $derived(mode === 'range' ? "End date" : "");

    // Display text
    let startText = $derived(mode === 'range' ? (value?.start ? formatDisplay(value.start) : "") : (value ? formatDisplay(value) : ""));
    let endText = $derived(mode === 'range' ? (value?.end ? formatDisplay(value.end) : "") : "");

</script>

<!-- 
    Ant Design Style Input 
    - Base: Bordered, rounded-md, bg-background, px-3 py-2
    - Focus/Active: Ring, Border-primary
    - Hover: Border-primary
-->
<div
  class={cn(
    "border-input bg-background dark:bg-input/30 ring-offset-background placeholder:text-muted-foreground flex h-9 w-full min-w-0 items-center justify-between rounded-md border px-3 py-1 text-base shadow-xs transition-[color,box-shadow] outline-none md:text-sm",
    "group-focus-visible:border-ring group-focus-visible:ring-ring/50 group-focus-visible:ring-[3px]",
    "aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive",
    isActive && "border-ring ring-ring/50 ring-[3px]",
    disabled && "cursor-not-allowed opacity-50",
    "cursor-pointer relative",
  )}
  onmouseenter={() => (isHovered = true)}
  onmouseleave={() => (isHovered = false)}
  aria-disabled={disabled}
  aria-invalid={ariaInvalid}
>
  {#if mode === "single"}
    <!-- Single Input Layout -->
    <div class="flex-1 overflow-hidden text-ellipsis whitespace-nowrap">
      {#if value}
        <span class="text-foreground">{startText}</span>
      {:else}
        <span class="text-muted-foreground">{placeholder}</span>
      {/if}
    </div>
  {:else}
    <!-- Range Input Layout -->
    <div class="flex items-center gap-1.5 flex-1 min-w-0 overflow-hidden">
      <span
        class={cn(
          "truncate transition-opacity",
          !startText && "text-muted-foreground",
          startText && "text-foreground",
        )}
      >
        {startText || startPlaceholder}
      </span>

      <span class="text-muted-foreground shrink-0">
        <Icon name="arrow-right" class="size-3" />
      </span>

      <span
        class={cn(
          "truncate transition-opacity",
          !endText && "text-muted-foreground",
          endText && "text-foreground",
        )}
      >
        {endText || endPlaceholder}
      </span>
    </div>
  {/if}

  <!-- Icons -->
  <div class="ml-2 flex flex-col justify-center">
    <!-- Clear Button (Absolute or Overlay) -->
    {#if showClear}
      <button
        tabindex="-1"
        class="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-0.5 hover:bg-muted text-muted-foreground hover:text-foreground transition-colors z-10 bg-background"
        onclick={(e) => {
          e.stopPropagation();
          onClear?.();
        }}
      >
        <Icon name="x" class="size-3" />
      </button>
    {:else}
      <!-- Default Calendar Icon -->
      <Icon
        name="calendar"
        class="size-4 text-muted-foreground pointer-events-none"
      />
    {/if}
  </div>
</div>
