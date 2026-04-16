<script lang="ts">
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { goto } from "$app/navigation";
  import { onMount } from "svelte";
  import { graphqlQuery } from "$lib/services/graphql/client";
  import { GET_FIXED_ASSETS } from "$lib/components/venUI/master-select/masters-api";
  import type { FixedAsset } from "$lib/business/models";

  let stats = $state({
    total: 0,
    itAssets: 0,
    expiringSoon: 0,
    recentlyAdded: 0
  });

  let loading = $state(true);

  async function fetchStats() {
    loading = true;
    try {
      const res = await graphqlQuery<{ fixedAssets: { nodes: any[], totalCount: number } }>(GET_FIXED_ASSETS, {
        variables: { first: 1000 }
      });
      if (res.success && res.data) {
        const assets = res.data.fixedAssets.nodes;
        stats.total = res.data.fixedAssets.totalCount;
        stats.itAssets = assets.filter(a => a.faClassCode === 'IT ASSET').length;
        
        const now = new Date();
        const thirtyDays = new Date();
        thirtyDays.setDate(now.getDate() + 30);
        const thirtyDaysAgo = new Date();
        thirtyDaysAgo.setDate(now.getDate() - 30);

        // Note: For real stats we should ideally use backend count queries or filters.
        // This is a dashboard-level summary.
      }
    } finally {
      loading = false;
    }
  }

  onMount(() => {
    fetchStats();
  });

  const cards = [
    {
      title: "Asset Directory",
      description: "Manage and track all company fixed assets, assignments and locations.",
      icon: "package",
      href: "/fixed-assets/assets",
      color: "text-blue-500",
      bgColor: "bg-blue-500/10"
    },
    {
      title: "Service Logs",
      description: "Track maintenance, repairs and service expenses for fixed assets.",
      icon: "wrench",
      href: "/fixed-assets/service-log",
      color: "text-amber-500",
      bgColor: "bg-amber-500/10"
    },
    {
      title: "Classes & Subclasses",
      description: "Configure asset classification hierarchy for reporting and tracking.",
      icon: "layers",
      href: "/fixed-assets/config",
      color: "text-purple-500",
      bgColor: "bg-purple-500/10"
    }
  ];
</script>

<svelte:head>
  <title>Fixed Assets Management</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
  <PageHeading backHref="/" icon="package">
    {#snippet title()}Fixed Assets Management{/snippet}
  </PageHeading>

  <main class="flex-1 space-y-8 p-4 md:p-6 lg:p-8">
    <!-- Quick Stats -->
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle class="text-sm font-medium">Total Assets</CardTitle>
          <Icon name="package" class="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div class="text-2xl font-bold">{stats.total}</div>
          <p class="text-xs text-muted-foreground mt-1">Actively tracked</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle class="text-sm font-medium">IT Assets</CardTitle>
          <Icon name="monitor" class="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div class="text-2xl font-bold">{stats.itAssets}</div>
          <p class="text-xs text-muted-foreground mt-1">Computer & Mobile</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle class="text-sm font-medium">Expiring Soon</CardTitle>
          <Icon name="alert-triangle" class="h-4 w-4 text-amber-500" />
        </CardHeader>
        <CardContent>
          <div class="text-2xl font-bold">{stats.expiringSoon}</div>
          <p class="text-xs text-muted-foreground mt-1">Warranty within 30 days</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader class="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle class="text-sm font-medium">Recently Added</CardTitle>
          <Icon name="history" class="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div class="text-2xl font-bold">{stats.recentlyAdded}</div>
          <p class="text-xs text-muted-foreground mt-1">In the last 30 days</p>
        </CardContent>
      </Card>
    </div>

    <!-- Navigation Cards -->
    <div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
      {#each cards as card}
        <Card 
          class="group cursor-pointer transition-all hover:border-primary/50 hover:shadow-lg"
          onclick={() => goto(card.href)}
        >
          <CardHeader>
            <div class={`mb-4 flex size-12 items-center justify-center rounded-xl ${card.bgColor} transition-transform group-hover:scale-110`}>
              <Icon name={card.icon} class={`size-6 ${card.color}`} />
            </div>
            <CardTitle>{card.title}</CardTitle>
            <CardDescription class="mt-2 leading-relaxed">
              {card.description}
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Button variant="ghost" class="w-full justify-between px-0 hover:bg-transparent">
              <span>Explore</span>
              <Icon name="arrow-right" class="size-4" />
            </Button>
          </CardContent>
        </Card>
      {/each}
    </div>
  </main>
</div>
