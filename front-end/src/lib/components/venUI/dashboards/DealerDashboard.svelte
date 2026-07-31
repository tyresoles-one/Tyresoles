<script lang="ts">
  import { getUser } from "$lib/stores/auth";
  import { BalanceWidget } from "$lib/components/venUI/balanceWidget";
  import Header from "./Header.svelte";
  import SalesOverview from "./SalesOverview.svelte";
  import { fade } from "svelte/transition";
  import { WeeklyEventDashboardWidget } from "$lib/components/venUI/calendar-view";

  const user = getUser();
</script>

<div class="space-y-3 max-w-[1600px] mx-auto p-1" in:fade={{ duration: 400 }}>
  <Header title="Dealer Dashboard" />
  <SalesOverview />
  <div class="grid grid-cols-1 lg:grid-cols-3 gap-3">
    <div class="lg:col-span-1">
      <BalanceWidget
        entityCode={user?.entityCode}
        entityType={user?.entityType}
        respCenter={user?.respCenter}
      />
    </div>
  </div>
  <WeeklyEventDashboardWidget />
</div>
