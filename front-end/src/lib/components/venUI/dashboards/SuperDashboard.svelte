<script lang="ts">  
  import { getUser } from '$lib/stores/auth';
  import { BalanceWidget } from '$lib/components/venUI/balanceWidget';
  import Header from './Header.svelte';
  import SalesTiles from './SalesTiles.svelte';
  import { fade, slide } from 'svelte/transition';
  import SalesChart from './SalesChart.svelte'; 
  import { Button } from '$lib/components/ui/button';
  import { Icon } from '$lib/components/venUI/icon';
  import { goto } from '$app/navigation';
  import { WeeklyEventDashboardWidget } from '$lib/components/venUI/calendar-view';

  const user = getUser();    
</script>

<div class="space-y-3 max-w-[1600px] mx-auto p-2" in:fade={{ duration: 400 }}>
    <Header title="Super Admin Dashboard" />
    
    <div class="grid grid-cols-1 xl:grid-cols-12 gap-3 items-start">
        <div class="xl:col-span-12">
            <SalesTiles />
        </div>

        <div class="xl:col-span-8 flex flex-col gap-3">
            <SalesChart class="h-full min-h-[500px]" />
            <WeeklyEventDashboardWidget />
        </div>        
    </div>
</div>