<script lang="ts">
  import { cn } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";
  import { getContext } from "svelte";
  import { NAVBAR_CTX_KEY, type NavbarContext } from "./navbar-context";
  import type { Snippet } from "svelte";

  interface Props extends HTMLAttributes<HTMLAnchorElement> {
    href: string;
    isActive?: boolean;
    children: Snippet;
  }

  let {
    class: className,
    href,
    isActive = false,
    children,
    ...props
  }: Props = $props();

  // Try to get context to close mobile menu on click
  const ctx = getContext<NavbarContext>(NAVBAR_CTX_KEY);

  function handleClick() {
    if (ctx) {
      ctx.closeMobileMenu();
    }
  }
</script>

<a
  {href}
  onclick={handleClick}
  class={cn(
    "group relative flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium transition-all duration-300 ease-in-out",
    "hover:bg-primary/5 hover:text-foreground", // Hover Background added
    isActive
      ? "text-primary font-bold bg-primary/10 shadow-sm ring-1 ring-primary/20"
      : "text-muted-foreground",
    className,
  )}
  {...props}
>
  {@render children()}

  <!-- Active Indicator -->
  {#if isActive}
    <span
      class="absolute inset-x-0 -bottom-[1px] h-[2px] w-3/5 mx-auto rounded-t-full bg-gradient-to-r from-transparent via-primary to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100"
    ></span>
  {/if}
</a>
