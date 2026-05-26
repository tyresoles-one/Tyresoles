<script lang="ts">
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import ServiceChecker from "$lib/components/ServiceChecker.svelte";
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
  </PageHeading>

  <main class="page-main">
    <ServiceChecker refreshInterval={30000} showHeader={true} />
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
</style>
