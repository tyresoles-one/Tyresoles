<script lang="ts">
  import { authStore, getUser } from '$lib/stores/auth';
  import Header from './Header.svelte';
  import SalesTiles from './SalesTiles.svelte';
  import SalesChart from './SalesChart.svelte';
  import { WeeklyEventDashboardWidget } from '$lib/components/venUI/calendar-view';
  import { Icon } from '$lib/components/venUI/icon';
  import { goto } from '$app/navigation';
  import { fade } from 'svelte/transition';

  const user = getUser();
</script>

<div class="space-y-6 max-w-[1600px] mx-auto p-2" in:fade={{ duration: 400 }}>
    <Header title="Administrator Dashboard" />
    
    <div class="grid grid-cols-1 xl:grid-cols-12 gap-6 items-start">
        <div class="xl:col-span-12">
            <SalesTiles />
        </div>

        <div class="xl:col-span-8 flex flex-col gap-6">
            <SalesChart class="h-full min-h-[500px]" />
            <WeeklyEventDashboardWidget />
        </div>

        <div class="xl:col-span-4 space-y-6">
             <!-- Admin Tools Card -->
             <div class="bg-card rounded-2xl border border-border/60 shadow-xl shadow-black/5 overflow-hidden flex flex-col h-full">
                <div class="p-5 border-b border-border/40 bg-muted/20 flex items-center justify-between">
                    <h3 class="font-bold text-sm uppercase tracking-wider flex items-center gap-2 text-primary">
                        <Icon name="shield-check" class="size-4" />
                        Admin Controls
                    </h3>
                </div>
                
                <div class="p-5 space-y-4 flex-1">
                    <button 
                        onclick={() => goto('/gstincheck')}
                        class="w-full group relative flex items-center gap-4 p-4 rounded-xl border border-border/40 bg-gradient-to-br from-card to-muted/30 hover:to-orange-500/5 transition-all duration-300 hover:border-orange-500/30 hover:shadow-lg active:scale-[0.98]"
                    >
                        <div class="p-3 rounded-lg bg-orange-500/10 text-orange-500 group-hover:bg-orange-500 group-hover:text-white transition-colors shadow-sm">
                            <Icon name="search-code" class="size-6" />
                        </div>
                        <div class="flex-1 text-left">
                            <div class="font-bold text-sm group-hover:text-orange-500 transition-colors">GSTIN Master Check</div>
                            <div class="text-[10px] text-muted-foreground font-medium uppercase mt-0.5">Master Data Verification</div>
                        </div>
                        <Icon name="chevron-right" class="size-4 text-muted-foreground/40 group-hover:text-orange-500 group-hover:translate-x-1 transition-all" />
                    </button>

                    <div class="grid grid-cols-2 gap-3">
                        <button class="flex flex-col items-center justify-center p-4 rounded-xl border border-dashed border-border/60 hover:border-primary/40 hover:bg-primary/5 transition-all group opacity-60 hover:opacity-100">
                             <Icon name="user-cog" class="size-5 mb-2 text-muted-foreground group-hover:text-primary" />
                             <span class="text-[11px] font-bold">Manage Users</span>
                        </button>
                        <button 
                            onclick={() => goto('/vendors')}
                            class="flex flex-col items-center justify-center p-4 rounded-xl border border-dashed border-border/60 hover:border-orange-500/40 hover:bg-orange-500/5 transition-all group active:scale-95"
                        >
                             <Icon name="truck" class="size-5 mb-2 text-muted-foreground group-hover:text-orange-500 transition-colors" />
                             <span class="text-[11px] font-bold group-hover:text-orange-500 transition-colors">Vendors</span>
                        </button>
                    </div>
                </div>

                <div class="px-5 py-4 bg-muted/10 border-t border-border/40">
                    <div class="flex items-center gap-2 text-xs text-muted-foreground font-medium">
                        <Icon name="info" class="size-3.5" />
                        Last Activity: Today, 10:45 AM
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>
