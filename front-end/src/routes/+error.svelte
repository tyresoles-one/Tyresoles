<script lang="ts">
  /**
   * Dynamic error screen: 404 (not found) vs internal/5xx errors.
   * Updated with richer UX, removing technical details and adding a repeating background.
   */
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { fly } from "svelte/transition";
  import { cubicOut } from "svelte/easing";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";

  const status = $derived($page.status ?? 500);
  const error = $derived($page.error);

  const isNotFound = $derived(status === 404);
  const isServerError = $derived(status >= 500);

  const config = $derived({
    icon: isNotFound
      ? "map-pin-off"
      : isServerError
        ? "server-crash"
        : "alert-triangle",
    title: isNotFound
      ? "Page Not Found"
      : isServerError
        ? "Server Error"
        : "Error",
    description: isNotFound
      ? "We couldn't find the page you're looking for. It might have been moved, deleted, or never existed."
      : isServerError
        ? "Something went wrong on our end. We're working to fix it. Please try again later."
        : "An unexpected error occurred. Please try refreshing the page.",
  });
  const svgBackground = $derived(
    `data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='100' height='100' viewBox='0 0 100 100'%3E%3Ctext x='50' y='50' font-family='sans-serif' font-weight='bold' font-size='30' text-anchor='middle' dominant-baseline='middle' fill='%2394a3b8' opacity='0.2'%3E${status}%3C/text%3E%3C/svg%3E`,
  );
</script>

<div
  class="bg-background text-foreground relative flex min-h-screen flex-col items-center justify-center overflow-hidden p-4"
>
  <!-- Repeating Background Pattern -->
  <div
    class="pointer-events-none absolute inset-0 -z-10"
    style="background-image: url('{svgBackground}'); background-size: 100px 100px;"
    aria-hidden="true"
  ></div>

  <!-- Main Content -->
  <div
    in:fly={{ y: 20, duration: 600, easing: cubicOut }}
    class="z-10 flex max-w-lg flex-col items-center text-center"
  >
    <div
      class="bg-muted mb-6 flex size-24 items-center justify-center rounded-full shadow-sm ring-8 ring-muted/20"
    >
      <Icon name={config.icon} class="text-primary size-12" />
    </div>

    <h1 class="mb-2 text-4xl font-extrabold tracking-tight lg:text-5xl">
      {config.title}
    </h1>

    <p class="text-muted-foreground mb-8 text-lg">
      {config.description}
    </p>

    <div class="flex flex-wrap justify-center gap-4">
      <Button size="lg" onclick={() => goto("/")} class="gap-2">
        <Icon name="home" class="size-4" />
        Go to Home
      </Button>
      <Button
        variant="outline"
        size="lg"
        onclick={() => window.history.back()}
        class="gap-2"
      >
        <Icon name="arrow-left" class="size-4" />
        Go Back
      </Button>
    </div>
  </div>
</div>
