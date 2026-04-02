<script lang="ts">
  import { cn } from "$lib/utils";
  import type { HTMLAttributes } from "svelte/elements";
  import type { Snippet } from "svelte";

  let {
    class: className,
    children,
    ...props
  }: HTMLAttributes<HTMLElement> & { children: Snippet } = $props();

  // Scroll state for enhanced visuals
  let scrollY = $state(0);
  const isScrolled = $derived(scrollY > 10);
</script>

<svelte:window bind:scrollY />

<header
  class={cn(
    "sticky top-0 z-50 w-full transition-all duration-500 ease-in-out",
    isScrolled
      ? "bg-background/80 backdrop-blur-xl shadow-lg shadow-black/5 supports-[backdrop-filter]:bg-background/60"
      : "bg-transparent",
    className,
  )}
  {...props}
>
  <div
    class="container mx-auto flex h-16 items-center pl-0 pr-4 pl-4 md:px-6 gap-2 md:gap-6 relative z-10"
  >
    {@render children()}
  </div>

  <!-- Premium Visible Border / Gradient Line -->
  <div
    class={cn(
      "absolute bottom-0 left-0 right-0 h-[1px] w-full transition-opacity duration-500",
      isScrolled ? "opacity-100" : "opacity-0",
    )}
  >
    <div
      class="absolute inset-0 bg-gradient-to-r from-transparent via-border/50 to-transparent"
    ></div>
    <div
      class="absolute inset-0 bg-gradient-to-r from-transparent via-primary/20 to-transparent opacity-50 blur-[1px]"
    ></div>
  </div>
</header>
