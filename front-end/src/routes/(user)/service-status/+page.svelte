<script lang="ts">
  import { appConfigStore } from "$lib/config/runtime";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import ServiceChecker from "$lib/components/ServiceChecker.svelte";
  import { get } from "svelte/store";

  // Services to monitor — tweak names to match your Windows Server 2022 setup
  const services = [
    { name: "TyrsolesApi",  canStart: true,  canStop: true },
    { name: "MSSQLSERVER",  canStart: false,  canStop: false },
    { name: "W3SVC",        canStart: false,  canStop: false },
    { name: "wuauserv",     canStart: false,  canStop: false },
  ];

  // Only show in Server mode
  const config = get(appConfigStore);
  const isServerMode = config?.mode === "Server";
</script>

<svelte:head>
  <title>Service Status — Tyresoles</title>
  <meta name="description" content="Monitor and manage Windows Server 2022 services for the Tyresoles platform." />
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
  </PageHeading>

  <main class="page-main">
    {#if !isServerMode}
      <div class="mode-notice">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="8" x2="12" y2="12" />
          <line x1="12" y1="16" x2="12.01" y2="16" />
        </svg>
        <div>
          <strong>Server Mode Required</strong>
          <p>This page is only available when the application is running in <em>Server</em> mode on Windows Server 2022.</p>
        </div>
      </div>
    {:else}
      <ServiceChecker {services} refreshInterval={30000} showHeader={true} />
    {/if}
  </main>
</div>

<style>
  .page-root {
    min-height: 100vh;
    background: hsl(220 18% 6%);
    padding-bottom: 80px;
  }

  .page-main {
    max-width: 1100px;
    margin: 0 auto;
    padding: 32px 20px;
  }

  .mode-notice {
    display: flex;
    align-items: flex-start;
    gap: 16px;
    padding: 24px 28px;
    background: hsl(220 16% 12%);
    border: 1.5px solid hsl(220 14% 20%);
    border-radius: 14px;
    color: hsl(215 20% 80%);
  }
  .mode-notice svg {
    width: 24px;
    height: 24px;
    flex-shrink: 0;
    margin-top: 2px;
    color: hsl(40 95% 60%);
  }
  .mode-notice strong {
    display: block;
    font-size: 1rem;
    margin-bottom: 4px;
    color: hsl(215 20% 95%);
  }
  .mode-notice p {
    font-size: 0.88rem;
    margin: 0;
    opacity: 0.75;
    line-height: 1.5;
  }
  .mode-notice em {
    font-style: normal;
    color: hsl(217 91% 70%);
    font-weight: 600;
  }
</style>
