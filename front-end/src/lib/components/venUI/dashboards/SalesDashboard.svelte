<script lang="ts">
  import SalesPerformanceTracker from './sales/components/SalesPerformanceTracker.svelte';
  import SalesCrmDashboard from './sales/SalesCrmDashboard.svelte';
  import { fade } from 'svelte/transition';
</script>

<div class="space-y-4 max-w-[1600px] mx-auto p-1" in:fade={{ duration: 250 }}>
  <!-- Sales Performance & Run Rate Tracker (My & Team) -->
  <SalesPerformanceTracker />

  <!-- Sales Activity & CRM Calling Workspace -->
  <SalesCrmDashboard />
</div>