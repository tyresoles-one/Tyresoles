<script lang="ts">
  import { Icon } from "$lib/components/venUI/icon";
  import type { HTMLAttributes } from "svelte/elements";
  import { cn } from "$lib/utils.js";

  let {
    class: className,
    ...restProps
  }: HTMLAttributes<HTMLDivElement> = $props();
</script>

<div role="separator" class={cn("mx-2", className)} {...restProps}>
  <Icon name="minus" class="h-4 w-4" />
</div>
