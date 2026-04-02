<script lang="ts">
  import type { PinInputCellProps } from "bits-ui";
  import { cn } from "$lib/utils.js";
  import { PinInput } from "bits-ui";

  let {
    class: className,
    cell,
    type = "text",
    ...restProps
  }: PinInputCellProps & { class?: string; type?: "text" | "password" } = $props();
</script>

<PinInput.Cell
  {cell}
  class={cn(
    "relative flex h-10 w-10 items-center justify-center border-y border-r border-input text-sm transition-all first:rounded-l-md first:border-l last:rounded-r-md",
    cell.isActive && "z-10 ring-2 ring-primary ring-offset-background",
    className
  )}
  {...restProps}
>
  {#if type === "password" && cell.char}
    <div class="h-2 w-2 rounded-full bg-current"></div>
  {:else}
    {cell.char}
  {/if}
  {#if cell.hasFakeCaret}
    <div
      class="pointer-events-none absolute inset-0 flex items-center justify-center"
    >
      <div
        class="h-4 w-px animate-caret-blink bg-foreground duration-1000"
      ></div>
    </div>
  {/if}
</PinInput.Cell>
