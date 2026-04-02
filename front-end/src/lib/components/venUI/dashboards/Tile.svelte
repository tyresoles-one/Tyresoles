<script lang="ts">
    import Icon from "../icon/icon.svelte";
    import Badge from "$lib/components/ui/badge/badge.svelte";
    import { cn } from "$lib/utils";
    import { fly } from "svelte/transition";
    let { label, value, trend, icon, color, delay, duration, y } = $props();
</script>
<div 
        class="group relative overflow-hidden rounded-xl border bg-card p-3 px-4 shadow-sm transition-all hover:shadow-lg hover:-translate-y-0.5"
        in:fly={{ y: y, delay: delay, duration: duration }}
      >
        <div class="flex items-center justify-between gap-2">
          <div class="flex items-center gap-3">
            <div class={cn("p-2 rounded-lg transition-transform group-hover:scale-105", color)}>
              <Icon name={icon} class="w-4 h-4" />
            </div>
            <div class="flex flex-col">
              <p class="text-[10px] font-semibold text-muted-foreground uppercase tracking-wider opacity-60">{label}</p>
              <h3 class="text-xl font-bold tracking-tight text-foreground tabular-nums">
                {value}
              </h3>
            </div>
          </div>
          <Badge variant="secondary" class="font-bold text-[9px] tabular-nums h-5 px-1.5 bg-muted/50">
            {trend}
          </Badge>
        </div>
      </div>
