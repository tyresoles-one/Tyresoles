<script lang="ts">  
  import { getUser } from '$lib/stores/auth';
  import { BalanceWidget } from '$lib/components/venUI/balanceWidget';
  import Header from './Header.svelte';
  import SalesTiles from './SalesTiles.svelte';
  import { fade } from 'svelte/transition';
  import SalesChart from './SalesChart.svelte'; 
  import { WeeklyEventDashboardWidget } from '$lib/components/venUI/calendar-view';

  const user = getUser();    
</script>

<div class="space-y-3 max-w-[1600px] mx-auto p-1" in:fade={{ duration: 400 }}>
    <Header title="Sales Dashboard" />
    <SalesTiles />
    <SalesChart class="h-full" />
    <WeeklyEventDashboardWidget />
</div>