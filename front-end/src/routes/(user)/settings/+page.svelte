<script lang="ts">
  import Settings from "$lib/components/Settings.svelte";
  import { PageHeading } from "$lib/components/venUI/page-heading";
</script>

<div class="min-h-screen bg-muted/30 pb-16">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="settings"
    class="border-b bg-background"
  >
    {#snippet title()}
      Settings
    {/snippet}
    {#snippet description()}
      Configure your application environment and download the desktop client
    {/snippet}
  </PageHeading>

  <main class="container mx-auto px-4 py-8">
    <Settings />
  </main>
</div>