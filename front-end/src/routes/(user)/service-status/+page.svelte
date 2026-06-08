<script lang="ts">
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import ServiceChecker from "$lib/components/ServiceChecker.svelte";

  let refreshFn = $state<() => Promise<void>>();
  let isRefreshing = $state(false);
  let runningCount = $state(0);
  let stoppedCount = $state(0);
  let unknownCount = $state(0);
</script>

<svelte:head>
  <title>Service Status — Tyresoles</title>
  <meta name="description" content="Monitor and manage Windows Server services for the Tyresoles platform." />
</svelte:head>

<div class="page-root">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="shield-check"
    class="border-b bg-background"
  >
    {#snippet title()}
      Service Status
    {/snippet}
    {#snippet description()}
      Monitor and control Windows services on this server
    {/snippet}
    {#snippet actions()}
      <div class="flex items-center gap-2">
        <div class="flex items-center gap-2 mr-2 text-[0.72rem] font-semibold uppercase tracking-wider">
          <span class="px-2.5 py-1 rounded-full border border-emerald-500/20 bg-emerald-500/10 text-emerald-500">
            {runningCount} Running
          </span>
          {#if stoppedCount > 0}
            <span class="px-2.5 py-1 rounded-full border border-rose-500/20 bg-rose-500/10 text-rose-500">
              {stoppedCount} Stopped
            </span>
          {/if}
          {#if unknownCount > 0}
            <span class="px-2.5 py-1 rounded-full border border-slate-500/20 bg-slate-500/10 text-slate-500">
              {unknownCount} Unknown
            </span>
          {/if}
        </div>
        <button
          class="flex items-center justify-center size-9 rounded-lg border border-border text-muted-foreground hover:bg-muted hover:text-foreground transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          onclick={() => refreshFn?.()}
          disabled={isRefreshing}
          title="Refresh"
          aria-label="Refresh service status"
        >
          <svg
            class:animate-spin={isRefreshing}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            class="size-4"
          >
            <path d="M23 4v6h-6" />
            <path d="M1 20v-6h6" />
            <path
              d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15"
            />
          </svg>
        </button>
      </div>
    {/snippet}
  </PageHeading>

  <main class="page-main">
    <ServiceChecker 
      refreshInterval={30000} 
      showHeader={false} 
      bind:refreshFn 
      bind:isRefreshing 
      bind:runningCount 
      bind:stoppedCount 
      bind:unknownCount 
    />
  </main>
</div>

<style>
  .page-root {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    padding-bottom: 80px;
  }

  .page-main {
    max-width: 1100px;
    margin: 0 auto;
    padding: 32px 20px;
  }
</style>
