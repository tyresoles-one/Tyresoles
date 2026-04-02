<script lang="ts">
  import { cn } from "$lib/utils";
  import {
    today,
    getLocalTimeZone,
    startOfWeek,
    endOfWeek,
    getDayOfWeek,
    isSameDay,
    parseDate,
    type DateValue,
    CalendarDate,
  } from "@internationalized/date";
  import type { CalendarEventDto } from "$lib/services/calendar/api";
  import { Icon } from "$lib/components/venUI/icon";
  import { slide, fade, fly, scale } from "svelte/transition";
  import { cubicOut } from "svelte/easing";

  let {
    events = [] as CalendarEventDto[],
    selectedDate = $bindable(today(getLocalTimeZone()) as CalendarDate),
    onSelectEvent,
    onAddEvent,
    class: className = "",
  }: {
    events?: CalendarEventDto[];
    selectedDate?: CalendarDate;
    onSelectEvent?: (event: CalendarEventDto) => void;
    onAddEvent?: () => void;
    class?: string;
  } = $props();

  // Week management
  let currentWeekStart = $state(startOfWeek(selectedDate, "en-US"));
  
  const weekDates = $derived.by(() => {
    const dates: CalendarDate[] = [];
    let d = currentWeekStart;
    for (let i = 0; i < 7; i++) {
      dates.push(d);
      d = d.add({ days: 1 }) as CalendarDate;
    }
    return dates;
  });

  const weekDays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

  // Filtering events for the selected day
  const filteredEvents = $derived(
    events.filter((e) => {
      const start = new Date(e.startUtc);
      const end = new Date(e.endUtc);
      const d = new Date(selectedDate.year, selectedDate.month - 1, selectedDate.day);
      
      const dayStart = new Date(d.setHours(0, 0, 0, 0)).getTime();
      const dayEnd = new Date(d.setHours(23, 59, 59, 999)).getTime();
      
      return start.getTime() <= dayEnd && end.getTime() >= dayStart;
    }).sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime())
  );

  function formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString(undefined, { 
      hour: "numeric", 
      minute: "2-digit",
      hour12: true 
    });
  }

  function isToday(d: CalendarDate) {
    return isSameDay(d, today(getLocalTimeZone()));
  }

  function nextWeek() {
    currentWeekStart = currentWeekStart.add({ weeks: 1 }) as CalendarDate;
    selectedDate = currentWeekStart;
  }

  function prevWeek() {
    currentWeekStart = currentWeekStart.subtract({ weeks: 1 }) as CalendarDate;
    selectedDate = currentWeekStart;
  }

  function getEventDuration(ev: CalendarEventDto): string {
    if (ev.isAllDay) return "All Day";
    const start = new Date(ev.startUtc);
    const end = new Date(ev.endUtc);
    const diffMs = end.getTime() - start.getTime();
    const diffHrs = Math.floor(diffMs / (1000 * 60 * 60));
    const diffMins = Math.round((diffMs % (1000 * 60 * 60)) / (1000 * 60));
    
    let str = "";
    if (diffHrs > 0) str += `${diffHrs}h `;
    if (diffMins > 0) str += `${diffMins}m`;
    return str.trim();
  }
</script>

<div class={cn(
  "relative overflow-hidden rounded-[2rem] border border-white/20 bg-slate-950/40 p-6 text-white shadow-2xl backdrop-blur-xl transition-all duration-500 hover:shadow-emerald-500/10",
  className
)}>
  <!-- Decoration Blobs -->
  <div class="pointer-events-none absolute -right-20 -top-20 h-64 w-64 rounded-full bg-emerald-500/20 blur-[80px]"></div>
  <div class="pointer-events-none absolute -bottom-20 -left-20 h-64 w-64 rounded-full bg-blue-500/10 blur-[80px]"></div>

  <!-- Header -->
  <div class="relative z-10 mb-8 flex items-center justify-between gap-4 flex-wrap">
    <div class="flex items-center gap-4">
      <div>
        <h3 class="text-2xl font-bold tracking-tight">Weekly Agenda</h3>
        <p class="text-xs font-medium text-emerald-200/60 uppercase tracking-widest mt-1">
          {new Date(currentWeekStart.year, currentWeekStart.month - 1, currentWeekStart.day).toLocaleDateString(undefined, { month: 'long', year: 'numeric' })}
        </p>
      </div>
      {#if onAddEvent}
        <button 
          onclick={onAddEvent}
          class="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-500 text-emerald-950 shadow-lg shadow-emerald-500/20 transition-all hover:scale-110 active:scale-95"
          title="Add New Event"
        >
          <Icon name="plus" class="size-6" />
        </button>
      {/if}
    </div>
    <div class="flex gap-2">
      <button 
        onclick={prevWeek}
        class="flex h-10 w-10 items-center justify-center rounded-full border border-white/10 bg-white/5 transition-all hover:bg-white/10 hover:scale-110 active:scale-95"
      >
        <Icon name="chevron-left" class="size-5" />
      </button>
      <button 
        onclick={nextWeek}
        class="flex h-10 w-10 items-center justify-center rounded-full border border-white/10 bg-white/5 transition-all hover:bg-white/10 hover:scale-110 active:scale-95"
      >
        <Icon name="chevron-right" class="size-5" />
      </button>
    </div>
  </div>

  <!-- Weekly Ribbon -->
  <div class="relative z-10 mb-8 flex justify-between gap-2 overflow-x-auto pb-2 no-scrollbar">
    {#each weekDates as date}
      <button
        onclick={() => selectedDate = date}
        class={cn(
          "group relative flex min-w-[3.5rem] flex-col items-center justify-center rounded-2xl py-3 transition-all duration-300",
          isSameDay(date, selectedDate) 
            ? "bg-emerald-500 text-emerald-950 shadow-lg shadow-emerald-500/40 scale-105" 
            : "hover:bg-white/10"
        )}
      >
        <span class={cn(
          "text-[10px] font-bold uppercase tracking-tighter opacity-70",
          isSameDay(date, selectedDate) && "opacity-100"
        )}>
          {weekDays[getDayOfWeek(date, 'en-US')].slice(0, 3)}
        </span>
        <span class="mt-0.5 text-lg font-black leading-none">
          {date.day}
        </span>
        {#if isToday(date)}
          <span class={cn(
            "absolute -bottom-1 h-1.5 w-1.5 rounded-full",
            isSameDay(date, selectedDate) ? "bg-emerald-950" : "bg-emerald-400 animate-pulse"
          )}></span>
        {/if}
      </button>
    {/each}
  </div>

  <!-- Events List -->
  <div class="relative z-10 space-y-4 max-h-[320px] overflow-y-auto pr-2 custom-scrollbar">
    {#if filteredEvents.length === 0}
      <div in:fade={{ duration: 300 }} class="flex flex-col items-center justify-center py-12 text-center">
        <div class="mb-4 flex h-16 w-16 items-center justify-center rounded-full bg-white/5 text-emerald-400">
          <Icon name="calendar-off" class="size-8 opacity-40" />
        </div>
        <p class="text-sm font-medium text-white/40">No scheduled events</p>
        <p class="text-xs text-white/20">Enjoy your free time!</p>
      </div>
    {:else}
      {#each filteredEvents as event (event.id)}
        <button
          onclick={() => onSelectEvent?.(event)}
          in:fly={{ y: 20, duration: 400, delay: 50, easing: cubicOut }}
          class="group relative w-full overflow-hidden rounded-2xl bg-white/5 p-4 text-left transition-all duration-300 hover:bg-white/10 hover:translate-x-1 active:scale-[0.98]"
        >
          <!-- Accent Border -->
          <div 
            class="absolute left-0 top-0 h-full w-1 opacity-60 transition-all duration-300 group-hover:w-1.5 group-hover:opacity-100"
            style="background-color: {event.eventTypeColor || '#10b981'}"
          ></div>

          <div class="flex items-start justify-between">
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2 text-[10px] font-bold uppercase tracking-widest text-emerald-400/80">
                <Icon name="clock" class="size-3" />
                <span>{event.isAllDay ? 'All Day' : formatTime(event.startUtc)}</span>
                <span class="opacity-40">•</span>
                <span>{getEventDuration(event)}</span>
              </div>
              <h4 class="mt-1 truncate text-base font-bold text-white group-hover:text-emerald-300 transition-colors">
                {event.title}
              </h4>
              {#if event.location}
                <div class="mt-1 flex items-center gap-1.5 text-xs text-white/50">
                  <Icon name="map-pin" class="size-3" />
                  <span class="truncate">{event.location}</span>
                </div>
              {/if}
            </div>

            <!-- Avatar group / tags -->
            <div class="ml-4 flex -space-x-2">
              {#each (event.tags || []).slice(0, 3) as tag}
                <div 
                  title={tag.displayName || tag.tagKey}
                  class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-slate-950 bg-emerald-800 text-[10px] font-bold"
                >
                  {(tag.displayName || tag.tagKey).slice(0, 1).toUpperCase()}
                </div>
              {/each}
              {#if (event.tags || []).length > 3}
                <div class="flex h-7 w-7 items-center justify-center rounded-full border-2 border-slate-950 bg-slate-900 text-[9px] font-bold">
                  +{(event.tags || []).length - 3}
                </div>
              {/if}
            </div>
          </div>
          
          {#if event.meetingLink}
            <div class="mt-3 flex gap-2">
              <a 
                href={event.meetingLink}
                target="_blank"
                rel="noopener noreferrer"
                class="flex items-center gap-2 rounded-lg bg-emerald-500/20 px-3 py-1.5 text-[10px] font-bold text-emerald-400 transition-all hover:bg-emerald-500/30"
                onclick={(e) => e.stopPropagation()}
              >
                <Icon name="video" class="size-3" />
                JOIN MEETING
              </a>
            </div>
          {/if}
        </button>
      {/each}
    {/if}
  </div>

  <!-- Gap Features Overlay (Premium Teaser) -->
  <div class="mt-6 flex items-center justify-between border-t border-white/5 pt-4">
    <div class="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-white/30">
      <Icon name="zap" class="size-3 text-emerald-500" />
      <span>AI Conflicts Check Optimized</span>
    </div>
    
    <a 
      href="/calendar"
      class="flex items-center gap-2 text-[10px] font-bold uppercase tracking-widest text-emerald-400 transition-all hover:text-emerald-300 hover:scale-105 active:scale-95"
    >
      Full Calendar
      <Icon name="arrow-right" class="size-3" />
    </a>
  </div>
</div>

<style>
  .no-scrollbar::-webkit-scrollbar {
    display: none;
  }
  .no-scrollbar {
    -ms-overflow-style: none;
    scrollbar-width: none;
  }

  .custom-scrollbar::-webkit-scrollbar {
    width: 4px;
  }
  .custom-scrollbar::-webkit-scrollbar-track {
    background: transparent;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb {
    background: rgba(255, 255, 255, 0.1);
    border-radius: 10px;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb:hover {
    background: rgba(255, 255, 255, 0.2);
  }
</style>
