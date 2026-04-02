<script lang="ts">
import Icon from "../icon/icon.svelte";
import NotificationDropdown from "./NotificationDropdown.svelte";
import { authStore } from "$lib/stores/auth";
let {title} = $props();
</script>
  <header 
    class="relative rounded-xl border bg-card p-3 shadow-sm transition-all hover:shadow-md"
  >
    
    <div class="flex items-center justify-between gap-4">
      <div class="flex items-center gap-3">
        <div class="hidden sm:flex items-center justify-center w-10 h-10 rounded-full bg-primary/10 border border-primary/20 text-primary">
          <Icon name="layout-dashboard" class="w-5 h-5" />
        </div>
        <div class="flex flex-col">
          <div class="flex items-center gap-2">
            <h1 class="text-lg font-bold tracking-tight text-foreground leading-none">
              {title}
            </h1>            
          </div>
          <p class="text-[11px] text-muted-foreground mt-0.5">
            Welcome back, <span class="font-bold text-foreground italic">{$authStore.user?.fullName || 'User'}</span>
          </p>
        </div>
      </div>

      <div class="flex items-center gap-4">
        <div class="hidden md:flex flex-col items-end">
          <span class="text-[9px] font-bold text-muted-foreground uppercase opacity-40">System Status</span>
          <span class="text-[10px] font-semibold flex items-center gap-1 text-emerald-500">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
            Operational
          </span>
        </div>
        <div class="w-px h-6 bg-border hidden sm:block"></div>
        <NotificationDropdown />
      </div>
    </div>
  </header>
<style>
  :global(.font-style-italic) {
    font-style: italic;
  }
</style>