<script lang="ts">
    import { ScrollArea } from "$lib/components/ui/scroll-area";
    import { Button } from "$lib/components/ui/button";
    import { cn } from "$lib/utils";

    type Props = {
        hour?: number;
        minute?: number;
        onChange?: (h: number, m: number, s: number) => void;
        class?: string;
    };

    let { hour = 0, minute = 0, onChange, class: className }: Props = $props();

    // Derived state for 12-hour format
    let period = $derived(hour >= 12 ? 'PM' : 'AM');
    let displayHour = $derived(hour === 0 ? 12 : (hour > 12 ? hour - 12 : hour));

    // Generate arrays
    const hours12 = Array.from({ length: 12 }, (_, i) => i + 1);
    const minutes = Array.from({ length: 60 }, (_, i) => i);
    const periods = ['AM', 'PM'];

    function setTime12(h12: number, m: number, p: string) {
        let h24 = h12;
        if (p === 'PM' && h12 !== 12) h24 = h12 + 12;
        if (p === 'AM' && h12 === 12) h24 = 0;
        else if (p === 'AM' && h12 !== 12) h24 = h12; // Normal AM 1-11
        else if (p === 'PM' && h12 === 12) h24 = 12; // Noon

        onChange?.(h24, m, 0);
    }
</script>

<div class={cn("flex h-[200px] divide-x", className)}>
  <!-- Hours (1-12) -->
  <ScrollArea class="h-full w-16">
    <div class="flex flex-col p-1">
      {#each hours12 as h}
        <Button
          variant={displayHour === h ? "default" : "ghost"}
          size="sm"
          class="justify-center rounded-none"
          onclick={() => setTime12(h, minute, period)}
        >
          {h.toString().padStart(2, "0")}
        </Button>
      {/each}
    </div>
  </ScrollArea>
  <!-- Minutes -->
  <ScrollArea class="h-full w-16">
    <div class="flex flex-col p-1">
      {#each minutes as m}
        <Button
          variant={minute === m ? "default" : "ghost"}
          size="sm"
          class="justify-center rounded-none"
          onclick={() => setTime12(displayHour, m, period)}
        >
          {m.toString().padStart(2, "0")}
        </Button>
      {/each}
    </div>
  </ScrollArea>
  <!-- Period (AM/PM) -->
  <ScrollArea class="h-full w-16">
    <div class="flex flex-col p-1">
      {#each periods as p}
        <Button
          variant={period === p ? "default" : "ghost"}
          size="sm"
          class="justify-center rounded-none"
          onclick={() => setTime12(displayHour, minute, p)}
        >
          {p}
        </Button>
      {/each}
    </div>
  </ScrollArea>
</div>
