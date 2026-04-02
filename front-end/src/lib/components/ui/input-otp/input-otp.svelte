<script lang="ts">
  import { PinInput } from "bits-ui";
  import { cn } from "$lib/utils.js";

  let {
    ref = $bindable(null),
    value = $bindable(""),
    class: className,
    children,
    ...restProps
  }: PinInput.RootProps & { class?: string } = $props();
</script>

<PinInput.Root
  bind:ref
  bind:value
  class={cn("flex items-center gap-2 disabled:cursor-not-allowed", className)}
  {...restProps}
>
  {#snippet children({ cells })}
    {@render children?.({ cells })}
  {/snippet}
</PinInput.Root>
