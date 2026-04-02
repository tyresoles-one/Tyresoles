<script lang="ts">
  import { authStore } from '$lib/stores/auth';
</script>

<div class="space-y-6">
  <div class="rounded-xl border bg-card p-6 shadow-sm">
    <h2 class="text-2xl font-bold tracking-tight">Production Management Dashboard</h2>
    <p class="text-muted-foreground mt-2">Welcome back, { $authStore.user?.fullName || 'User' }.</p>
  </div>
  <div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
    <div class="rounded-xl border bg-card p-6 shadow-sm h-32 flex items-center justify-center text-muted-foreground">Product Mgmt Metric 1</div>
  </div>
</div>
