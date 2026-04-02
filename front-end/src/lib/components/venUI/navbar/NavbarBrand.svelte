<script lang="ts">
  import { cn } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";
  import type { Snippet } from "svelte";

  let {
    class: className,
    href = "/",
    children,
    ...props
  }: HTMLAttributes<HTMLAnchorElement> & {
    href?: string;
    children: Snippet;
  } = $props();
</script>

<div class="mr-4 flex">
  <a
    {href}
    class={cn("flex items-center space-x-2 font-bold", className)}
    {...props}
  >
    {@render children()}
  </a>
</div>
