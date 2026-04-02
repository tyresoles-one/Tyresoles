<script lang="ts">
  import { onMount } from 'svelte';
  import Tile from './Tile.svelte';
  import { authStore, getUser } from '$lib/stores/auth';
  import { fetchDashboard } from './classic-dashboard/api';
  const user = getUser();
  let loading = $state(true);
  let summaryData = $state<any>(null);

  const respCenters = $derived.by(() => {
    const locs = $authStore.locations;
    if (locs && locs.length > 0) {
      return locs.filter(l => (l as any).sale === 1).map(l => l.code).filter(Boolean);
    }
    return user?.respCenter ? [user.respCenter] : [];
  });

  onMount(async () => {
    const res = await fetchDashboard({ 
        reportName: 'summary',
        entityCode: user?.entityCode,
        entityType: user?.entityType,
        entityDepartment: user?.department,
        workDate: user?.workDate,
        respCenters: respCenters
    } as any);
    if (res.success) {
      summaryData = res.data;
    }
    loading = false;
  });
  const stats = $derived(summaryData?.tiles || summaryData?.Tiles || []);
</script>
<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
    {#each stats as stat, i}
        <Tile {...stat} delay={i * 80} duration={400} y={10} />
    {/each}
</div>