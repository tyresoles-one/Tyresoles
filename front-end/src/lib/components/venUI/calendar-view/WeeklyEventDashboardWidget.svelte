<script lang="ts">
  import {
    today,
    getLocalTimeZone,
    startOfWeek,
    isSameDay,
    getDayOfWeek,
    type CalendarDate,
  } from "@internationalized/date";
  import { getMyCalendarEvents, toggleCalendarTaskStatus, type CalendarEventDto, type CalendarTaskDto } from "$lib/services/calendar/api";
  import { Icon } from "$lib/components/venUI/icon";
  import CalendarEventSheet from "./CalendarEventSheet.svelte";
  import { cn } from "$lib/utils";
  import { fade, fly } from "svelte/transition";
  import { cubicOut } from "svelte/easing";
  import { goto } from "$app/navigation";

  let { class: className = "" } = $props();

  // ── State ─────────────────────────────────────────────────────────────────
  let events       = $state<CalendarEventDto[]>([]);
  let isLoading    = $state(true);
  let error        = $state<string | null>(null);
  let selectedDate = $state(today(getLocalTimeZone()) as CalendarDate);

  // Sheet state
  let sheetOpen        = $state(false);
  let sheetEvent       = $state<CalendarEventDto | undefined>(undefined);
  let sheetDefaultDate = $state<string | undefined>(undefined);

  function openCreate(date?: CalendarDate) {
    sheetEvent = undefined;
    sheetDefaultDate = date
      ? `${date.year}-${String(date.month).padStart(2,'0')}-${String(date.day).padStart(2,'0')}`
      : undefined;
    sheetOpen = true;
  }

  function openEdit(ev: CalendarEventDto) {
    sheetEvent = ev;
    sheetDefaultDate = undefined;
    sheetOpen = true;
  }

  const weekStart = $derived(startOfWeek(selectedDate, "en-US") as CalendarDate);

  // Build 7 day objects for the ribbon
  const weekDates = $derived.by(() => {
    const dates: CalendarDate[] = [];
    let d = weekStart;
    for (let i = 0; i < 7; i++) {
      dates.push(d);
      d = d.add({ days: 1 }) as CalendarDate;
    }
    return dates;
  });

  const DAY_LABELS = ["Su", "Mo", "Tu", "We", "Th", "Fr", "Sa"];

  // Month/year header label
  const monthLabel = $derived(
    new Date(weekStart.year, weekStart.month - 1).toLocaleDateString(undefined, {
      month: "long",
      year: "numeric",
    })
  );

  // Events filtered for selected day, sorted by start time
  const dayEvents = $derived(
    events
      .filter((e) => {
        const d = new Date(selectedDate.year, selectedDate.month - 1, selectedDate.day);
        const dayStart = new Date(d).setHours(0, 0, 0, 0);
        const dayEnd   = new Date(d).setHours(23, 59, 59, 999);
        return new Date(e.startUtc).getTime() <= dayEnd && new Date(e.endUtc).getTime() >= dayStart;
      })
      .sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime())
  );

  // Event counts per day (for the dot indicator)
  const countByDay = $derived(
    Object.fromEntries(
      weekDates.map((d) => {
        const dayStart = new Date(d.year, d.month - 1, d.day).setHours(0, 0, 0, 0);
        const dayEnd   = new Date(d.year, d.month - 1, d.day).setHours(23, 59, 59, 999);
        const n = events.filter(
          (e) => new Date(e.startUtc).getTime() <= dayEnd && new Date(e.endUtc).getTime() >= dayStart
        ).length;
        return [`${d.year}-${d.month}-${d.day}`, n];
      })
    )
  );

  // ── Data Fetching ─────────────────────────────────────────────────────────
  async function loadEvents() {
    isLoading = true;
    error = null;
    try {
      const start = weekStart.subtract({ days: 1 });
      const end   = weekStart.add({ days: 8 });
      events = await getMyCalendarEvents(
        new Date(start.year, start.month - 1, start.day),
        new Date(end.year, end.month - 1, end.day)
      );
    } catch (e: any) {
      error = e?.message ?? "Failed to load events";
    } finally {
      isLoading = false;
    }
  }

  $effect(() => { const _w = weekStart; loadEvents(); });

  // ── Helpers ───────────────────────────────────────────────────────────────
  function prevWeek() { selectedDate = weekStart.subtract({ weeks: 1 }) as CalendarDate; }
  function nextWeek() { selectedDate = weekStart.add({ weeks: 1 }) as CalendarDate; }

  function formatTime(iso: string) {
    return new Date(iso).toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit", hour12: true });
  }

  function formatDuration(a: string, b: string) {
    const diff = (new Date(b).getTime() - new Date(a).getTime()) / 60000;
    if (diff < 60) return `${diff}m`;
    const h = Math.floor(diff / 60);
    const m = diff % 60;
    return m ? `${h}h ${m}m` : `${h}h`;
  }

  function dayKey(d: CalendarDate) { return `${d.year}-${d.month}-${d.day}`; }

  async function onToggleTask(taskId: string, isCompleted: boolean) {
    try {
      await toggleCalendarTaskStatus(taskId, isCompleted);
      // Update local state for immediate feedback
      events = events.map(e => ({
        ...e,
        tasks: updateTaskStatus(e.tasks, taskId, isCompleted)
      }));
    } catch (e) {
      console.error("Failed to toggle task", e);
    }
  }

  function updateTaskStatus(tasks: CalendarTaskDto[], taskId: string, isCompleted: boolean): CalendarTaskDto[] {
    return tasks.map(t => {
      if (t.id === taskId) return { ...t, isCompleted };
      if (t.subTasks.length > 0) return { ...t, subTasks: updateTaskStatus(t.subTasks, taskId, isCompleted) };
      return t;
    });
  }
</script>

<!-- ──────────────────────────────────────────────────────────────────────────
     Root card — uses CSS design tokens for automatic light/dark support.
     ────────────────────────────────────────────────────────────────────────── -->
<div
  class={cn(
    "flex flex-col overflow-hidden rounded-xl border border-border bg-card text-card-foreground shadow-sm",
    className
  )}
>
  <!-- ── Header ──────────────────────────────────────────────────────────── -->
  <div class="flex items-center justify-between border-b border-border px-4 py-3">
    <div>
      <h3 class="text-sm font-semibold text-foreground leading-tight">Weekly Agenda</h3>
      <p class="text-[11px] text-muted-foreground mt-0.5">{monthLabel}</p>
    </div>
    <div class="flex items-center gap-1">
      <button
        type="button"
        onclick={prevWeek}
        class="rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        aria-label="Previous week"
      >
        <Icon name="chevron-left" class="size-4" />
      </button>
      <button
        type="button"
        onclick={nextWeek}
        class="rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        aria-label="Next week"
      >
        <Icon name="chevron-right" class="size-4" />
      </button>
      <button
        type="button"
        onclick={() => openCreate(selectedDate)}
        class="ml-0.5 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-primary/10 hover:text-primary"
        aria-label="New event"
        title="New event"
      >
        <Icon name="plus" class="size-3.5" />
      </button>
      <a
        href="/calendar"
        class="ml-1 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
        title="Open full calendar"
      >
        <Icon name="maximize-2" class="size-3.5" />
      </a>
    </div>
  </div>

  <!-- ── Week Ribbon ─────────────────────────────────────────────────────── -->
  <div class="grid grid-cols-7 border-b border-border bg-muted/20">
    {#each weekDates as d}
      {@const isSelected = isSameDay(d, selectedDate)}
      {@const isTodayDate = isSameDay(d, today(getLocalTimeZone()))}
      {@const count = countByDay[dayKey(d)] ?? 0}
      <button
        type="button"
        onclick={() => (selectedDate = d)}
        class={cn(
          "flex flex-col items-center gap-0.5 py-2 transition-colors",
          isSelected ? "bg-primary/10" : "hover:bg-muted/60"
        )}
      >
        <span class={cn(
          "text-[10px] font-semibold uppercase tracking-wide",
          isTodayDate ? "text-primary" : "text-muted-foreground"
        )}>
          {DAY_LABELS[getDayOfWeek(d, "en-US")]}
        </span>
        <span
          class={cn(
            "flex size-7 items-center justify-center rounded-full text-sm font-bold",
            isSelected && isTodayDate && "bg-primary text-primary-foreground",
            isSelected && !isTodayDate && "bg-primary/15 text-primary",
            !isSelected && isTodayDate && "text-primary"
          )}
        >
          {d.day}
        </span>
        <!-- Event dot indicator -->
        <span class={cn(
          "size-1 rounded-full transition-all",
          count > 0 ? (isSelected ? "bg-primary" : "bg-muted-foreground/40") : "bg-transparent"
        )}></span>
      </button>
    {/each}
  </div>

  <!-- ── Event List ──────────────────────────────────────────────────────── -->
  <div class="flex-1 overflow-y-auto" style="max-height: 320px;">
    {#if isLoading}
      <!-- Skeleton -->
      <div class="space-y-2 p-3 animate-pulse" in:fade={{ duration: 200 }}>
        {#each [1, 2, 3] as _}
          <div class="flex items-start gap-3 rounded-lg p-2">
            <div class="h-8 w-1 rounded-full bg-muted"></div>
            <div class="flex-1 space-y-1.5">
              <div class="h-3 w-2/3 rounded bg-muted"></div>
              <div class="h-2.5 w-1/3 rounded bg-muted"></div>
            </div>
          </div>
        {/each}
      </div>

    {:else if error}
      <!-- Error state -->
      <div class="flex flex-col items-center justify-center gap-3 p-6 text-center" in:fade={{ duration: 200 }}>
        <div class="flex size-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">
          <Icon name="wifi-off" class="size-5" />
        </div>
        <div>
          <p class="text-sm font-medium text-foreground">Failed to load</p>
          <p class="mt-0.5 text-xs text-muted-foreground">{error}</p>
        </div>
        <button
          type="button"
          onclick={loadEvents}
          class="flex items-center gap-1.5 rounded-lg border border-border px-3 py-1.5 text-xs font-medium text-foreground transition-colors hover:bg-muted"
        >
          <Icon name="refresh-cw" class="size-3.5" />
          Retry
        </button>
      </div>

    {:else if dayEvents.length === 0}
      <!-- Empty for this day -->
      <div class="flex flex-col items-center justify-center gap-2 py-10 text-center" in:fade={{ duration: 200 }}>
        <div class="flex size-10 items-center justify-center rounded-full bg-muted text-muted-foreground">
          <Icon name="calendar-off" class="size-5" />
        </div>
        <p class="text-sm text-muted-foreground">No events today</p>
        <button
          type="button"
          onclick={() => openCreate(selectedDate)}
          class="mt-1 text-xs font-medium text-primary hover:underline"
        >
          Add one →
        </button>
      </div>

    {:else}
      <!-- Events -->
      <div class="divide-y divide-border">
        {#each dayEvents as event (event.id)}
          <button
            type="button"
            onclick={() => openEdit(event)}
            class="flex w-full items-start gap-3 px-4 py-2.5 text-left transition-colors hover:bg-muted/50 active:bg-muted"
            in:fly={{ x: -8, duration: 200, easing: cubicOut }}
          >
            <!-- Colour bar -->
            <div
              class="mt-0.5 h-9 w-0.5 shrink-0 rounded-full"
              style="background-color: {event.eventTypeColor ?? 'var(--primary)'}"
            ></div>

            <!-- Content -->
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-semibold text-foreground">{event.title}</p>
              <p class="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
                <Icon name="clock" class="size-3 shrink-0" />
                {event.isAllDay
                  ? "All day"
                  : `${formatTime(event.startUtc)} · ${formatDuration(event.startUtc, event.endUtc)}`}
                {#if event.location}
                  <span class="mx-0.5 opacity-40">·</span>
                  <Icon name="map-pin" class="size-3 shrink-0" />
                  <span class="truncate">{event.location}</span>
                {/if}
              </p>

              <!-- Topic Badges -->
              {#if event.tags?.some(t => t.tagType === 'TOPIC')}
                <div class="mt-1.5 flex flex-wrap gap-1">
                  {#each event.tags.filter(t => t.tagType === 'TOPIC') as tag}
                    <span class="rounded-md bg-secondary px-1.5 py-0.5 text-[10px] font-medium text-secondary-foreground">
                      #{tag.displayName || tag.tagKey}
                    </span>
                  {/each}
                </div>
              {/if}

              <!-- Task List (Multi-level) -->
              {#if event.tasks?.length > 0}
                <div class="mt-2 space-y-1 pl-1" onclick={(e) => e.stopPropagation()}>
                  {#each event.tasks as task}
                    {@render renderTask(task, 0)}
                  {/each}
                </div>
              {/if}
            </div>

            {#snippet renderTask(task: CalendarTaskDto, level: number)}
              <div class="group flex flex-col" style="margin-left: {level * 12}px">
                <div class="flex items-center gap-2 py-0.5">
                  <input
                    type="checkbox"
                    checked={task.isCompleted}
                    onchange={(e) => onToggleTask(task.id, e.currentTarget.checked)}
                    class="size-3.5 rounded border-border text-primary focus:ring-primary shadow-sm"
                  />
                  <span class={cn(
                    "text-[12px] transition-colors",
                    task.isCompleted ? "text-muted-foreground line-through" : "text-foreground font-medium"
                  )}>
                    {task.title}
                  </span>
                </div>
                {#if task.subTasks?.length > 0}
                  <div class="flex flex-col">
                    {#each task.subTasks as sub}
                      {@render renderTask(sub, level + 1)}
                    {/each}
                  </div>
                {/if}
              </div>
            {/snippet}

            <!-- Right: type badge + meeting link -->
            <div class="flex shrink-0 flex-col items-end gap-1">
              {#if event.eventTypeName}
                <span
                  class="rounded-full px-1.5 py-0.5 text-[10px] font-semibold text-white"
                  style="background-color: {event.eventTypeColor ?? 'var(--primary)'}"
                >
                  {event.eventTypeName}
                </span>
              {/if}
              {#if event.meetingLink}
                <a
                  href={event.meetingLink}
                  target="_blank"
                  rel="noopener noreferrer"
                  onclick={(e) => e.stopPropagation()}
                  class="flex items-center gap-1 rounded px-1.5 py-0.5 text-[10px] font-semibold text-primary hover:bg-primary/10 transition-colors"
                  title="Join meeting"
                >
                  <Icon name="video" class="size-3" />
                  Join
                </a>
              {/if}
            </div>
          </button>
        {/each}
      </div>
    {/if}
  </div>

  <!-- ── Footer ──────────────────────────────────────────────────────────── -->
  <div class="flex items-center justify-between border-t border-border bg-muted/10 px-4 py-2">
    <span class="text-[11px] text-muted-foreground">
      {#if !isLoading}
        {dayEvents.length} event{dayEvents.length !== 1 ? "s" : ""}
      {/if}
    </span>
    <a
      href="/calendar"
      class="flex items-center gap-1 text-[11px] font-medium text-primary transition-colors hover:underline"
    >
      Full Calendar
      <Icon name="arrow-right" class="size-3" />
    </a>
  </div>
</div>

<!-- Inline Create/Edit Sheet — works directly from the widget without page navigation -->
<CalendarEventSheet
  bind:open={sheetOpen}
  event={sheetEvent}
  defaultDate={sheetDefaultDate}
  onSaved={() => loadEvents()}
  onDeleted={() => loadEvents()}
/>
