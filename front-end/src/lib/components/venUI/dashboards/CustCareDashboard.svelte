<script lang="ts">
  import { getUser } from "$lib/stores/auth";
  import Header from "./Header.svelte";
  import SalesOverview from "./SalesOverview.svelte";
  import { WeeklyEventDashboardWidget } from "$lib/components/venUI/calendar-view";
  import { fade } from "svelte/transition";

  const user = getUser();
</script>

<div class="space-y-6 max-w-[1600px] mx-auto p-2" in:fade={{ duration: 400 }}>
  <Header title="Customer Care Dashboard" />

  <div class="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">
    <div class="xl:col-span-12">
      <SalesOverview class="h-full min-h-[500px]" />
    </div>

    <div class="xl:col-span-8 flex flex-col gap-6">
      <WeeklyEventDashboardWidget />
    </div>
  </div>
</div>
