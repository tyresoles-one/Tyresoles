<!--
  CalendarEventSheet.svelte
  ─────────────────────────────────────────────────────────────────────────────
  A full-featured, side-sheet form for creating **and** editing calendar events.
  Works from both the /calendar page and the Weekly Agenda widget.

  Props:
    open        — bind:open to control visibility
    event       — pass a CalendarEventDto to enter edit mode, omit for create mode
    defaultDate — optional ISO date string to pre-fill the start date on create
    defaultHour — optional hour (0-23) to pre-fill start time on create
    onSaved     — fired after a successful save (event → created/updated dto)
    onDeleted   — fired after a successful delete
-->
<script lang="ts">
  import { onMount, tick } from "svelte";
  import { fade } from "svelte/transition";
  import * as Sheet from "$lib/components/ui/sheet";
  import * as Avatar from "$lib/components/ui/avatar";
  import { Button } from "$lib/components/ui/button";
  import { Icon } from "$lib/components/venUI/icon";
  import { DatePicker } from "$lib/components/venUI/date-picker";
  import { cn } from "$lib/utils";
  import { toast } from "$lib/components/venUI/toast";
  import {
    getEventTypes,
    createCalendarEvent,
    updateCalendarEvent,
    deleteCalendarEvent,
    searchUsers,
    type CalendarEventDto,
    type CreateEventInput,
    type UpdateEventInput,
    type EventTypeDto,
    type UpdateScope,
    type UserSearchResult,
    type CalendarTaskInput,
    type CalendarTaskDto,
  } from "$lib/services/calendar/api";

  // ── Props ──────────────────────────────────────────────────────────────────
  let {
    open = $bindable(false),
    event = undefined as CalendarEventDto | undefined,
    defaultDate = undefined as string | undefined,
    defaultHour = 9 as number,
    onSaved = undefined as ((ev: CalendarEventDto | null) => void) | undefined,
    onDeleted = undefined as (() => void) | undefined,
  } = $props();

  // ── Mode ───────────────────────────────────────────────────────────────────
  const isEdit = $derived(!!event);

  // ── Reference data ────────────────────────────────────────────────────────
  let eventTypes = $state<EventTypeDto[]>([]);

  onMount(async () => {
    try {
      eventTypes = await getEventTypes();
    } catch {
      eventTypes = [];
    }
  });

  // ── Form state ────────────────────────────────────────────────────────────
  let title           = $state("");
  let description     = $state("");
  let isAllDay        = $state(false);
  let startValue      = $state<any>(null);   // DatePicker value (text ISO)
  let endValue        = $state<any>(null);   // DatePicker value (text ISO)
  let eventTypeId     = $state<number | null>(null);
  let location        = $state("");
  let meetingLink     = $state("");
  let recurrence      = $state<"none" | "daily" | "weekly" | "monthly">("none");
  let reminderMinutes = $state<"" | "5" | "15" | "30" | "60">("");
  let editScope       = $state<UpdateScope>(0);

  // Attendees
  interface AttendeeState {
    userId: string;
    fullName: string;
    userType: string;
    avatar?: number | null;
    isRequired: boolean;
  }
  let attendees = $state<AttendeeState[]>([]);
  let searchQuery = $state("");
  let searchResults = $state<UserSearchResult[]>([]);
  let isSearching = $state(false);
  let searchTimeout: any = null;

  // Topics
  let topics = $state<string[]>([]);
  let topicInput = $state("");

  // Tasks
  let tasks = $state<CalendarTaskInput[]>([]);

  // ── Populate form when opening ─────────────────────────────────────────────
  $effect(() => {
    if (!open) return;

    if (event) {
      // EDIT mode — populate from event
      title           = event.title;
      description     = event.description ?? "";
      isAllDay        = event.isAllDay;
      startValue      = event.startUtc.substring(0, 16); // "YYYY-MM-DDTHH:mm"
      endValue        = event.endUtc.substring(0, 16);
      eventTypeId     = event.eventTypeId ?? null;
      location        = event.location ?? "";
      meetingLink     = event.meetingLink ?? "";
      recurrence      = "none";
      reminderMinutes = event.reminders?.length ? "15" : "";
      editScope       = event.recurrenceRuleId ? 1 : 0;
      
      // Map attendees
      // We don't have fullName/userType/avatar in the basic attendee list from Event, 
      // but EventAttendeeDto might have them or we just rely on userId/email.
      // For now, mapping simplified:
        attendees = (event.attendees ?? []).map(a => ({
        userId: a.userId,
        fullName: a.userId, // We should ideally have fullName here
        userType: "",
        avatar: null,
        isRequired: a.isRequired
      }));

      // Map topics
      topics = (event.tags ?? [])
        .filter(t => t.tagType === 'TOPIC')
        .map(t => t.displayName || t.tagKey);

      // Map tasks
      tasks = JSON.parse(JSON.stringify(event.tasks || [])); // Deep clone
    } else {
      // CREATE mode — sensible defaults
      title           = "";
      description     = "";
      isAllDay        = false;
      eventTypeId     = null;
      location        = "";
      meetingLink     = "";
      recurrence      = "none";
      reminderMinutes = "15";
      editScope       = 0;
      attendees       = [];
      topics          = [];
      tasks           = [];

      // Build default start/end
      const base = defaultDate ? new Date(defaultDate) : new Date();
      base.setHours(defaultHour, 0, 0, 0);
      const end = new Date(base.getTime() + 60 * 60 * 1000); // +1 hour
      startValue = toLocalIso(base);
      endValue   = toLocalIso(end);
    }
  });

  // ── Search Logic ───────────────────────────────────────────────────────────
  async function performSearch() {
    if (!searchQuery.trim()) {
      searchResults = [];
      return;
    }
    isSearching = true;
    try {
      searchResults = await searchUsers(searchQuery);
    } catch {
      searchResults = [];
    } finally {
      isSearching = false;
    }
  }

  $effect(() => {
    if (searchTimeout) clearTimeout(searchTimeout);
    if (!searchQuery) {
      searchResults = [];
      return;
    }
    searchTimeout = setTimeout(performSearch, 300);
  });

  function addAttendee(u: UserSearchResult) {
    if (attendees.some(a => a.userId === u.userId)) {
      searchQuery = "";
      searchResults = [];
      return;
    }
    attendees = [...attendees, {
      userId: u.userId,
      fullName: u.fullName,
      userType: u.userType,
      avatar: u.avatar,
      isRequired: true
    }];
    searchQuery = "";
    searchResults = [];
  }

  function removeAttendee(userId: string) {
    attendees = attendees.filter(a => a.userId !== userId);
  }

  function toggleRequired(userId: string) {
    attendees = attendees.map(a => a.userId === userId ? { ...a, isRequired: !a.isRequired } : a);
  }

  // ── Topic Logic ────────────────────────────────────────────────────────────
  function addTopic(e?: KeyboardEvent) {
    if (e && e.key !== "Enter") return;
    if (e) e.preventDefault();
    
    const val = topicInput.trim();
    if (val && !topics.includes(val)) {
      topics = [...topics, val];
    }
    topicInput = "";
  }

  function removeTopic(t: string) {
    topics = topics.filter(x => x !== t);
  }

  // ── Task Logic ─────────────────────────────────────────────────────────────
  function addTask(parent?: CalendarTaskInput) {
    const newTask: CalendarTaskInput = {
      title: "",
      isCompleted: false,
      sortOrder: (parent?.subTasks?.length ?? tasks.length) + 1,
      subTasks: []
    };
    
    if (parent) {
      if (!parent.subTasks) parent.subTasks = [];
      parent.subTasks = [...parent.subTasks, newTask];
      tasks = [...tasks]; // trigger reactivity
    } else {
      tasks = [...tasks, newTask];
    }
  }

  function removeTask(id: string | null | undefined, list: CalendarTaskInput[]) {
    // This is tricky because we use nested lists.
    // Simplifying: we'll match by reference or a temp ID if needed.
    // For now, let's just use indices or pass the specific sub-list.
  }

  // Simplified task removal by passing the array and index
  function deleteTask(list: CalendarTaskInput[], index: number) {
    list.splice(index, 1);
    tasks = [...tasks]; // reactive update
  }

  // ── Helpers ────────────────────────────────────────────────────────────────
  /** Convert a JS Date to a local ISO-like string: "YYYY-MM-DDTHH:mm" */
  function toLocalIso(d: Date): string {
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  /** Parse DatePicker text value to UTC ISO string, honouring isAllDay */
  function parseToUtcIso(val: string, startOfDay = false): string {
    if (!val) return new Date().toISOString();
    // val is "YYYY-MM-DDTHH:mm" or "YYYY-MM-DD"
    const d = val.includes("T") ? new Date(val) : new Date(val + (startOfDay ? "T00:00" : "T00:00"));
    return d.toISOString();
  }

  // ── Submission ─────────────────────────────────────────────────────────────
  let saving = $state(false);
  let errors = $state<Record<string, string>>({});

  function validate(): boolean {
    const e: Record<string, string> = {};
    if (!title.trim())              e.title    = "Title is required";
    if (!startValue)                e.start    = "Start date/time is required";
    if (!endValue)                  e.end      = "End date/time is required";
    if (startValue && endValue) {
      const s = new Date(startValue).getTime();
      const en = new Date(endValue).getTime();
      if (en <= s)                  e.end      = "End must be after start";
    }
    errors = e;
    return Object.keys(e).length === 0;
  }

  async function handleSubmit() {
    if (!validate()) return;
    saving = true;
    try {
      const startUtc = isAllDay
        ? parseToUtcIso((startValue ?? "").split("T")[0] + "T00:00:00")
        : parseToUtcIso(startValue);
      const endUtc = isAllDay
        ? parseToUtcIso((endValue ?? "").split("T")[0] + "T23:59:59")
        : parseToUtcIso(endValue);

      const recurrenceInput = recurrence !== "none"
        ? { frequency: recurrence === "daily" ? 0 : recurrence === "weekly" ? 1 : 2, interval: 1 }
        : undefined;

      const remMins = reminderMinutes === "" ? 0 : Number(reminderMinutes);
      const remindersInput = remMins > 0
        ? [{ remindAtUtc: new Date(new Date(startUtc).getTime() - remMins * 60000).toISOString(), channel: 'IN_APP' as const }]
        : undefined;

      const attendeesInput = attendees.map(a => ({ userId: a.userId, isRequired: a.isRequired }));

      const tagsInput = [
        ...(event?.tags?.filter(t => t.tagType !== 'TOPIC') || []).map(t => ({ tagType: t.tagType as any === 'USER' ? 0 : t.tagType as any === 'CUSTOMER' ? 1 : 2, tagKey: t.tagKey })),
        ...topics.map(t => ({ tagType: 3, tagKey: t }))
      ];

      let saved: CalendarEventDto | null = null;

      if (isEdit && event) {
        const input: UpdateEventInput = {
          title:       title.trim(),
          description: description.trim() || undefined,
          startUtc,
          endUtc,
          isAllDay,
          location:    location.trim() || undefined,
          meetingLink: meetingLink.trim() || undefined,
          eventTypeId: eventTypeId ?? undefined,
          recurrence:  recurrenceInput,
          reminders:   remindersInput,
          attendees:   attendeesInput,
          tags:        tagsInput,
          tasks:       tasks
        };
        saved = await updateCalendarEvent(
          event.id,
          input,
          editScope,
          editScope === 1 ? event.exceptionOccurrenceStartUtc ?? event.startUtc : undefined
        );
        toast.success("Event updated");
      } else {
        const input: CreateEventInput = {
          title:       title.trim(),
          description: description.trim() || undefined,
          startUtc,
          endUtc,
          isAllDay,
          location:    location.trim() || undefined,
          meetingLink: meetingLink.trim() || undefined,
          eventTypeId: eventTypeId ?? undefined,
          recurrence:  recurrenceInput,
          reminders:   remindersInput,
          attendees:   attendeesInput,
          tags:        tagsInput,
          tasks:       tasks
        };
        saved = await createCalendarEvent(input);
        toast.success("Event created");
      }

      open = false;
      onSaved?.(saved);
    } catch (e: any) {
      toast.error(e?.message ?? "Failed to save event");
    } finally {
      saving = false;
    }
  }

  // ── Deletion ───────────────────────────────────────────────────────────────
  let deleting       = $state(false);
  let confirmDelete  = $state(false);

  async function handleDelete(scope: UpdateScope) {
    if (!event) return;
    deleting = true;
    try {
      await deleteCalendarEvent(
        event.id,
        true,
        scope,
        scope === 1 ? event.startUtc : undefined
      );
      toast.success(scope === 1 ? "Occurrence deleted" : "Event deleted");
      open = false;
      onDeleted?.();
    } catch (e: any) {
      toast.error(e?.message ?? "Failed to delete");
    } finally {
      deleting = false;
      confirmDelete = false;
    }
  }

  // ── Colour swatch for selected event type ──────────────────────────────────
  const selectedTypeColor = $derived(
    eventTypes.find((t) => t.id === eventTypeId)?.color ?? null
  );

  function getInitials(name: string) {
    return name.split(" ").map(n => n[0]).join("").toUpperCase().substring(0, 2);
  }
</script>

<Sheet.Root bind:open>
  <Sheet.Content
    side="right"
    class="flex flex-col gap-0 p-0 sm:max-w-md w-full"
  >
    <!-- ── Header ──────────────────────────────────────────────────────────── -->
    <Sheet.Header class="border-b border-border px-5 py-4">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-3">
          <!-- Coloured indicator strip -->
          <div
            class="h-7 w-1 rounded-full transition-colors"
            style="background-color: {selectedTypeColor ?? 'var(--primary)'}"
          ></div>
          <div>
            <Sheet.Title class="text-base font-semibold">
              {isEdit ? "Edit Event" : "New Event"}
            </Sheet.Title>
            <Sheet.Description class="text-xs text-muted-foreground mt-0.5">
              {isEdit ? "Update event details" : "Fill in the details below"}
            </Sheet.Description>
          </div>
        </div>
        <Sheet.Close class="rounded-md p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground transition-colors">
          <Icon name="x" class="size-4" />
        </Sheet.Close>
      </div>
    </Sheet.Header>

    <!-- ── Scrollable body ────────────────────────────────────────────────── -->
    <div class="flex-1 overflow-y-auto px-5 py-4 space-y-6">

      <!-- Title & Event Type side by side -->
      <div class="space-y-4">
        <div class="space-y-1.5">
          <label for="ce-title" class="text-sm font-medium text-foreground">
            Title <span class="text-destructive">*</span>
          </label>
          <input
            id="ce-title"
            type="text"
            bind:value={title}
            placeholder="Add title"
            class={cn(
              "w-full rounded-lg border bg-background px-3 py-2 text-base font-medium text-foreground placeholder:text-muted-foreground shadow-sm",
              "focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all",
              errors.title ? "border-destructive ring-destructive/10" : "border-input"
            )}
          />
          {#if errors.title}
            <p class="text-xs text-destructive">{errors.title}</p>
          {/if}
        </div>

        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <label for="ce-type" class="text-sm font-medium text-foreground">Type</label>
            <div class="relative">
              <select
                id="ce-type"
                bind:value={eventTypeId}
                class="w-full appearance-none rounded-lg border border-input bg-background pl-8 pr-8 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
              >
                <option value={null}>— No type —</option>
                {#each eventTypes as et}
                  <option value={et.id}>{et.name}</option>
                {/each}
              </select>
              <div
                class="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 size-3 rounded-full transition-colors"
                style="background-color: {selectedTypeColor ?? 'var(--muted-foreground)'}"
              ></div>
              <Icon name="chevron-down" class="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
            </div>
          </div>

          <div class="space-y-1.5">
            <label for="ce-remind" class="text-sm font-medium text-foreground">Reminder</label>
            <div class="relative">
              <select
                id="ce-remind"
                bind:value={reminderMinutes}
                class="w-full appearance-none rounded-lg border border-input bg-background px-3 pr-8 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
              >
                <option value="">None</option>
                <option value="5">5m before</option>
                <option value="15">15m before</option>
                <option value="30">30m before</option>
                <option value="60">1h before</option>
              </select>
              <Icon name="chevron-down" class="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
            </div>
          </div>
        </div>
      </div>

      <!-- Time & Date -->
      <div class="rounded-xl border bg-muted/30 p-4 space-y-4">
        <label class="flex cursor-pointer items-center gap-2.5 mb-2">
          <input
            type="checkbox"
            bind:checked={isAllDay}
            class="size-4 rounded border-input text-primary focus:ring-ring"
          />
          <span class="text-sm font-medium text-foreground">All day event</span>
        </label>

        <div class="grid grid-cols-2 gap-4">
          <div class="space-y-1.5">
            <label for="ce-start" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Start</label>
            <input
              id="ce-start"
              type={isAllDay ? "date" : "datetime-local"}
              bind:value={startValue}
              class={cn(
                "w-full rounded-lg border bg-background px-3 py-2 text-sm text-foreground",
                "focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all",
                errors.start ? "border-destructive" : "border-input"
              )}
            />
          </div>

          <div class="space-y-1.5">
            <label for="ce-end" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">End</label>
            <input
              id="ce-end"
              type={isAllDay ? "date" : "datetime-local"}
              bind:value={endValue}
              class={cn(
                "w-full rounded-lg border bg-background px-3 py-2 text-sm text-foreground",
                "focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all",
                errors.end ? "border-destructive" : "border-input"
              )}
            />
          </div>
        </div>
        {#if errors.start || errors.end}
          <p class="text-xs text-destructive">{errors.start || errors.end}</p>
        {/if}

        <div class="pt-1">
          <label for="ce-repeat" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider block mb-1.5">Repeat</label>
          <div class="relative">
            <select
              id="ce-repeat"
              bind:value={recurrence}
              class="w-full appearance-none rounded-lg border border-input bg-background px-3 pr-8 py-2 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all"
            >
              <option value="none">No repeat</option>
              <option value="daily">Daily</option>
              <option value="weekly">Weekly</option>
              <option value="monthly">Monthly</option>
            </select>
            <Icon name="chevron-down" class="pointer-events-none absolute right-2.5 top-1/2 -translate-y-1/2 size-3.5 text-muted-foreground" />
          </div>
        </div>
      </div>

      <!-- Attendees Section -->
      <div class="space-y-3">
        <div class="text-sm font-medium text-foreground flex items-center justify-between">
          <span>Attendees</span>
          <span class="text-xs text-muted-foreground">{attendees.length} selected</span>
        </div>
        
        <!-- Search Input -->
        <div class="relative">
          <div class="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2">
            {#if isSearching}
              <Icon name="loader-circle" class="size-4 animate-spin text-primary" />
            {:else}
              <Icon name="search" class="size-4 text-muted-foreground" />
            {/if}
          </div>
          <input
            type="text"
            bind:value={searchQuery}
            placeholder="Search by name or userId..."
            class="w-full rounded-lg border border-input bg-background pl-9 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
          />
          
          <!-- Search Results Dropdown -->
          {#if searchResults.length > 0}
            <div class="absolute z-10 top-full mt-1.5 w-full rounded-xl border bg-popover p-1.5 shadow-xl max-h-[220px] overflow-y-auto custom-scrollbar">
              {#each searchResults as u}
                <button
                  type="button"
                  class="flex w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left hover:bg-accent hover:text-accent-foreground transition-colors group"
                  onclick={() => addAttendee(u)}
                >
                  <Avatar.Root class="size-8 text-[10px] font-bold">
                    <Avatar.Fallback>{getInitials(u.fullName || u.userId)}</Avatar.Fallback>
                  </Avatar.Root>
                  <div class="flex-1 min-w-0">
                    <p class="text-sm font-semibold truncate">{u.fullName}</p>
                    <p class="text-[10px] text-muted-foreground truncate">{u.userId} · {u.userType}</p>
                  </div>
                  <Icon name="plus" class="size-4 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
                </button>
              {/each}
            </div>
          {/if}
        </div>

        <!-- Attendee List -->
        {#if attendees.length > 0}
          <div class="space-y-2 max-h-[240px] overflow-y-auto px-0.5 custom-scrollbar">
            {#each attendees as a (a.userId)}
              <div class="flex items-center gap-3 rounded-xl border bg-card p-3 shadow-xs">
                <Avatar.Root class="size-9 text-xs font-bold border">
                  <Avatar.Fallback>{getInitials(a.fullName)}</Avatar.Fallback>
                </Avatar.Root>
                <div class="flex-1 min-w-0">
                  <p class="text-sm font-semibold truncate">{a.fullName}</p>
                  <div class="flex items-center gap-2">
                    <button
                      type="button"
                      class={cn(
                        "text-[10px] font-bold px-1.5 py-0.5 rounded-md border transition-all",
                        a.isRequired 
                          ? "bg-primary/5 border-primary/20 text-primary" 
                          : "bg-muted border-transparent text-muted-foreground hover:bg-muted/80"
                      )}
                      onclick={() => toggleRequired(a.userId)}
                    >
                      {a.isRequired ? "REQUIRED" : "OPTIONAL"}
                    </button>
                  </div>
                </div>
                <button
                  type="button"
                  class="text-muted-foreground hover:text-destructive p-1.5 rounded-full hover:bg-destructive/10 transition-colors"
                  onclick={() => removeAttendee(a.userId)}
                >
                  <Icon name="user-minus" class="size-4" />
                </button>
              </div>
            {/each}
          </div>
        {:else if searchQuery === ""}
          <div class="flex flex-col items-center justify-center py-6 text-center rounded-xl border border-dashed border-muted">
            <Icon name="users" class="size-6 text-muted-foreground/40 mb-2" />
            <p class="text-xs text-muted-foreground">Add attendees to your event</p>
          </div>
        {/if}
      </div>

      <!-- Description & Location -->
      <div class="space-y-4">
        <div class="text-sm font-medium text-foreground">Additional Details</div>
        
        <div class="grid grid-cols-1 gap-4">
          <div class="relative">
            <div class="absolute left-3 top-3">
              <Icon name="align-left" class="size-4 text-muted-foreground" />
            </div>
            <textarea
              id="ce-desc"
              bind:value={description}
              placeholder="Add description..."
              rows="3"
              class="w-full rounded-lg border border-input bg-background pl-9 pr-4 py-2.5 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all resize-none shadow-sm"
            ></textarea>
          </div>

          <div class="relative">
            <div class="absolute left-3 top-1/2 -translate-y-1/2">
              <Icon name="map-pin" class="size-4 text-muted-foreground" />
            </div>
            <input
              id="ce-loc"
              type="text"
              bind:value={location}
              placeholder="Add location"
              class="w-full rounded-lg border border-input bg-background pl-9 pr-4 py-2.5 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
            />
          </div>

          <div class="relative">
            <div class="absolute left-3 top-1/2 -translate-y-1/2">
              <Icon name="video" class="size-4 text-muted-foreground" />
            </div>
            <input
              id="ce-link"
              type="url"
              bind:value={meetingLink}
              placeholder="Add meeting link"
              class="w-full rounded-lg border border-input bg-background pl-9 pr-4 py-2.5 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
            />
          </div>
        </div>
      </div>

      <!-- Topics Section -->
      <div class="space-y-3">
        <label for="ce-topics" class="text-sm font-medium text-foreground">Topics / Tags</label>
        <div class="flex flex-wrap gap-2 mb-2">
          {#each topics as t}
            <span class="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-secondary text-secondary-foreground text-xs font-medium group">
              {t}
              <button type="button" class="hover:text-destructive transition-colors" onclick={() => removeTopic(t)}>
                <Icon name="x" class="size-3" />
              </button>
            </span>
          {/each}
        </div>
        <div class="relative">
          <div class="absolute left-3 top-1/2 -translate-y-1/2">
            <Icon name="hash" class="size-4 text-muted-foreground" />
          </div>
          <input
            id="ce-topics"
            type="text"
            bind:value={topicInput}
            onkeydown={addTopic}
            placeholder="Add topic (press Enter)..."
            class="w-full rounded-lg border border-input bg-background pl-9 pr-4 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/20 focus:border-primary transition-all shadow-sm"
          />
        </div>
      </div>

      <!-- Tasks Section -->
      <div class="space-y-4">
        <div class="flex items-center justify-between">
          <label class="text-sm font-bold text-foreground flex items-center gap-2">
            <Icon name="check-square" class="size-4 text-primary" />
            Task Checklist
          </label>
          <Button variant="outline" size="sm" class="h-7 text-[10px] font-bold" onclick={() => addTask()}>
            <Icon name="plus" class="size-3 mr-1" />
            Add Task
          </Button>
        </div>

        {#if tasks.length > 0}
          <div class="space-y-3 pl-1">
            {#each tasks as task, i}
              {@render renderTaskEdit(task, tasks, i, 0)}
            {/each}
          </div>
        {:else}
          <div class="flex flex-col items-center justify-center py-6 text-center rounded-xl border border-dashed border-muted">
            <p class="text-xs text-muted-foreground">Break down this event into smaller tasks</p>
          </div>
        {/if}
      </div>

      {#snippet renderTaskEdit(task: CalendarTaskInput, parentList: CalendarTaskInput[], index: number, level: number)}
        <div class="group space-y-2">
          <div class="flex items-center gap-2" style="margin-left: {level * 16}px">
            <input
              type="checkbox"
              bind:checked={task.isCompleted}
              class="size-4 rounded border-input text-primary focus:ring-primary shadow-sm"
            />
            <input
              type="text"
              bind:value={task.title}
              placeholder="Task title..."
              class="flex-1 bg-transparent border-none p-0 text-sm focus:ring-0 focus:outline-none placeholder:text-muted-foreground/50 h-7"
            />
            <div class="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
              <button 
                type="button" 
                class="p-1 rounded hover:bg-muted text-muted-foreground hover:text-primary transition-colors" 
                title="Add subtask"
                onclick={() => addTask(task)}
              >
                <Icon name="plus" class="size-3.5" />
              </button>
              <button 
                type="button" 
                class="p-1 rounded hover:bg-destructive/10 text-muted-foreground hover:text-destructive transition-colors" 
                title="Delete task"
                onclick={() => deleteTask(parentList, index)}
              >
                <Icon name="trash-2" class="size-3.5" />
              </button>
            </div>
          </div>
          
          {#if task.subTasks && task.subTasks.length > 0}
            <div class="space-y-2">
              {#each task.subTasks as sub, si}
                {@render renderTaskEdit(sub, task.subTasks, si, level + 1)}
              {/each}
            </div>
          {/if}
        </div>
      {/snippet}

      <!-- Edit scope (only for recurring events in edit mode) -->
      {#if isEdit && event?.recurrenceRuleId}
        <div class="rounded-xl border border-amber-500/20 bg-amber-500/5 p-4 space-y-3 shadow-sm">
          <p class="text-xs font-bold text-amber-600 dark:text-amber-400 flex items-center gap-1.5 uppercase tracking-wider">
            <Icon name="repeat" class="size-4" />
            Recurring event
          </p>
          <div class="flex flex-col gap-2.5">
            {#each [
              { value: 0, label: "All events in the series" },
              { value: 1, label: "Only this occurrence" },
              { value: 2, label: "This and future events" },
            ] as opt}
              <label class="flex items-center gap-3 cursor-pointer group">
                <input
                  type="radio"
                  name="edit-scope"
                  value={opt.value}
                  bind:group={editScope}
                  class="size-4 text-primary focus:ring-ring"
                />
                <span class="text-sm font-medium text-foreground group-hover:text-primary transition-colors">{opt.label}</span>
              </label>
            {/each}
          </div>
        </div>
      {/if}

      <!-- Delete Confirm area (only in edit, triggered by toolbar) -->
      {#if isEdit && confirmDelete}
        <div class="rounded-xl border border-destructive/20 bg-destructive/5 p-4 space-y-3 shadow-sm" in:fade={{ duration: 200 }}>
          <p class="text-sm font-semibold text-destructive">
            {event?.recurrenceRuleId
              ? "Which occurrences do you want to delete?"
              : "Are you sure you want to delete this event?"}
          </p>
          {#if event?.recurrenceRuleId}
            <div class="flex flex-wrap gap-2">
              <Button size="sm" variant="outline" onclick={() => handleDelete(1)} disabled={deleting}>
                Only this
              </Button>
              <Button size="sm" variant="outline" onclick={() => handleDelete(2)} disabled={deleting}>
                This & future
              </Button>
              <Button size="sm" variant="destructive" onclick={() => handleDelete(0)} disabled={deleting}>
                {#if deleting}<Icon name="loader-circle" class="size-3.5 mr-1 animate-spin" />{/if}
                Entire series
              </Button>
            </div>
          {:else}
            <div class="flex gap-2">
              <Button size="sm" variant="outline" class="flex-1" onclick={() => (confirmDelete = false)} disabled={deleting}>
                Cancel
              </Button>
              <Button size="sm" variant="destructive" class="flex-1" onclick={() => handleDelete(0)} disabled={deleting}>
                {#if deleting}<Icon name="loader-circle" class="size-3.5 mr-1 animate-spin" />{/if}
                Delete
              </Button>
            </div>
          {/if}
        </div>
      {/if}

    </div>

    <!-- ── Footer / Actions ───────────────────────────────────────────────── -->
    <Sheet.Footer class="flex items-center justify-between gap-2 border-t border-border bg-muted/20 px-5 py-3.5">
      <!-- Left: delete (edit only) -->
      <div>
        {#if isEdit}
          <Button
            variant="ghost"
            size="sm"
            class="text-destructive hover:bg-destructive/10 hover:text-destructive font-semibold h-9"
            onclick={() => (confirmDelete = !confirmDelete)}
            disabled={saving || deleting}
          >
            <Icon name="trash-2" class="size-4 mr-2" />
            Delete
          </Button>
        {/if}
      </div>

      <!-- Right: Cancel + Save -->
      <div class="flex items-center gap-2">
        <Sheet.Close>
          <Button variant="outline" size="sm" class="h-9 px-4 font-semibold" disabled={saving}>
            Cancel
          </Button>
        </Sheet.Close>
        <Button
          size="sm"
          onclick={handleSubmit}
          disabled={saving}
          class="h-9 px-6 font-bold shadow-md shadow-primary/20 transition-all active:scale-95"
        >
          {#if saving}
            <Icon name="loader-circle" class="size-4 mr-2 animate-spin" />
            Saving...
          {:else}
            <Icon name="check" class="size-4 mr-2" />
            {isEdit ? "Update" : "Create"}
          {/if}
        </Button>
      </div>
    </Sheet.Footer>
  </Sheet.Content>
</Sheet.Root>

<style>
  .custom-scrollbar::-webkit-scrollbar {
    width: 6px;
  }
  .custom-scrollbar::-webkit-scrollbar-track {
    background: transparent;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb {
    background: hsl(var(--muted-foreground) / 0.2);
    border-radius: 10px;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb:hover {
    background: hsl(var(--muted-foreground) / 0.3);
  }
</style>
