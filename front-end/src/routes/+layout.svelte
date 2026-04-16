<script lang="ts">
  import "./layout.css";
  import favicon from "$lib/assets/favicon.ico";
  import { ModeWatcher } from "mode-watcher";
  import { DialogRenderer } from "$lib/components/venUI/dialog";
  import { Toaster } from "$lib/components/ui/sonner";
  import Main from "$lib/components/venUI/Main.svelte";
  import { QueryClient, QueryClientProvider } from "@tanstack/svelte-query";
  import BottomNav from "$lib/components/BottomNav.svelte";
  import UpdateChecker from "$lib/components/UpdateChecker.svelte";
  import { initAppConfig } from "$lib/config/runtime";

  import { onMount, onDestroy } from "svelte";
  import { initIdleTimer, cleanupIdleTimer } from "$lib/services/auth/idle-timer";

  let { children } = $props();

  onMount(() => {
    initIdleTimer();
  });

  onDestroy(() => {
    cleanupIdleTimer();
  });

  // Load runtime config once before any API/GraphQL calls
  const configPromise = initAppConfig();

  // GraphQL errors are handled by the global handler (config + client/queryClient); no duplicate toasts here
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        refetchOnWindowFocus: false,
        retry: 1,
      },
    },
  });
</script>

<svelte:head><link rel="icon" href={favicon} /></svelte:head>

{#await configPromise}
  <div class="flex min-h-screen items-center justify-center bg-background">
    <p class="text-muted-foreground">Loading…</p>
  </div>
{:then}
  <ModeWatcher />
  <UpdateChecker />
  <DialogRenderer />
  <Toaster />
  <QueryClientProvider client={queryClient}>
    <Main>
      {@render children()}
      <BottomNav />
    </Main>
  </QueryClientProvider>
{/await}
