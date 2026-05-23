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
    const w = window as Window & { __tsDebugAnimateWrapped?: boolean; __tsDebugAnimateOrig?: typeof Element.prototype.animate };
    if (typeof Element !== "undefined" && !w.__tsDebugAnimateWrapped) {
      w.__tsDebugAnimateWrapped = true;
      w.__tsDebugAnimateOrig = Element.prototype.animate;
      Element.prototype.animate = function (
        keyframes: Keyframe[] | PropertyIndexedKeyframes | null,
        options?: number | KeyframeAnimationOptions,
      ): Animation {
        let safeKeyframes = keyframes;
        try {
          const sanitize = (v: unknown): unknown =>
            typeof v === "string" && v.includes("NaN") ? "0px" : v;
          if (Array.isArray(keyframes)) {
            const arr = keyframes.map((kf) => {
              const clone = { ...(kf as Record<string, unknown>) };
              if ("height" in clone) clone.height = sanitize(clone.height);
              if ("width" in clone) clone.width = sanitize(clone.width);
              return clone;
            });
            safeKeyframes = arr as Keyframe[];
          } else if (keyframes && typeof keyframes === "object") {
            const kf = { ...(keyframes as Record<string, unknown>) };
            if ("height" in kf) kf.height = sanitize(kf.height);
            if ("width" in kf) kf.width = sanitize(kf.width);
            safeKeyframes = kf as PropertyIndexedKeyframes;
          }
        } catch {
          // ignore debug wrapper failures
        }
        return w.__tsDebugAnimateOrig!.call(this, safeKeyframes as any, options as any);
      };
    }
  });

  onDestroy(() => {
    const w = window as Window & { __tsDebugAnimateWrapped?: boolean; __tsDebugAnimateOrig?: typeof Element.prototype.animate };
    if (w.__tsDebugAnimateWrapped && w.__tsDebugAnimateOrig) {
      Element.prototype.animate = w.__tsDebugAnimateOrig;
      w.__tsDebugAnimateWrapped = false;
    }
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
