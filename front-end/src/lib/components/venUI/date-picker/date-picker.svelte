<script lang="ts">
  import {
    DateFormatter,
    type DateValue,
    getLocalTimeZone,
    today,
    CalendarDate,
    CalendarDateTime,
    Time,
    parseDate,
    parseDateTime,
    startOfWeek,
    endOfWeek,
    getWeeksInMonth,
    fromDate,
    toCalendar,
    toCalendarDate,
    toCalendarDateTime,
  } from "@internationalized/date";
  import { format as formatDate, parse as parseDateFns } from "date-fns";
  import { type Snippet } from "svelte";
  import { type PresetEntry } from "./types";
  import * as Popover from "$lib/components/ui/popover";
  import { Button } from "$lib/components/ui/button";
  import { cn } from "$lib/utils";
  import { Icon } from "$lib/components/venUI/icon";
  import * as Select from "$lib/components/ui/select";
  import { Calendar } from "$lib/components/ui/calendar";
  import { RangeCalendar } from "$lib/components/ui/range-calendar";
  import TimePicker from "./time-picker.svelte";
  import DateInput from "./date-input.svelte";
  import { ScrollArea } from "$lib/components/ui/scroll-area";
  import { fade } from "svelte/transition";
  import * as PresetLib from "./presets";

  type PickerMode = "date" | "week" | "month" | "quarter" | "year";
  type SelectionMode = "single" | "range";
  type ValueType = "text" | "date" | "calendar"; // text (ISO string), date (JS Date), calendar (CalendarDate/Time)

  type Props = {
    value?: any;
    placeholder?: string;
    disabled?: boolean;
    showTime?: boolean;
    picker?: PickerMode;
    mode?: SelectionMode;
    valueType?: ValueType;
    valueFormat?: string; // e.g. 'dd-MMM-yy'
    displayFormat?: string;
    presets?: PresetEntry[];
    presetKeys?: string; // Comma separated keys like 'today,yesterday,thisMonth'
    onValueChange?: (val: any) => void;
    presetsContent?: Snippet<[PresetEntry[]]>;
    fiscal?: boolean;
    workdate?: any; // Reference date for presets
    "aria-invalid"?: boolean | "true" | "false";
  };

  let {
    value = $bindable(),
    placeholder = "Select date",
    disabled = false,
    showTime = false,
    picker = "date",
    mode = "single",
    valueType = "calendar",
    valueFormat,
    displayFormat,
    presets = [],
    presetKeys,
    onValueChange,
    presetsContent,
    fiscal = false,
    workdate,
    "aria-invalid": ariaInvalid,
  }: Props = $props();

  const refDate = $derived.by(() => {
    if (!workdate) return today(getLocalTimeZone());
    if (workdate instanceof CalendarDate) return workdate;
    if (workdate instanceof Date) return fromDate(workdate, getLocalTimeZone());
    if (typeof workdate === "string") {
      try {
        if (workdate.includes("T")) {
          try {
            return toCalendarDate(parseDateTime(workdate.substring(0, 19)));
          } catch(e) {
            return toCalendarDate(fromDate(new Date(workdate), getLocalTimeZone()));
          }
        } else {
          try {
            return parseDate(workdate);
          } catch(e) {
            return toCalendarDate(fromDate(new Date(workdate), getLocalTimeZone()));
          }
        }
      } catch (e) {
        return today(getLocalTimeZone());
      }
    }
    return today(getLocalTimeZone());
  });

  const resolvedPresets = $derived.by(() => {
    if (presets.length > 0) return presets;
    if (!presetKeys) return [];

    const keys = presetKeys.split(",").map((k) => k.trim());
    const result: PresetEntry[] = [];

    keys.forEach((key) => {
      // Check in range lib first if mode is range
      if (mode === "range" && (PresetLib.range as any)[key]) {
        result.push((PresetLib.range as any)[key](refDate));
      } else if (mode === "single" && (PresetLib.single as any)[key]) {
        result.push((PresetLib.single as any)[key](refDate));
      }
      // Fallback: check either
      else if ((PresetLib.range as any)[key]) {
        result.push((PresetLib.range as any)[key](refDate));
      } else if ((PresetLib.single as any)[key]) {
        result.push((PresetLib.single as any)[key](refDate));
      }
    });

    return result;
  });

  let open = $state(false);

  // Internal state
  let calendarValue = $state<any>();
  let viewDate = $state(today(getLocalTimeZone()));

  // The "Current View" determining which grid is shown (year, month, date)
  // Should initialize to 'picker'
  let currentView = $state<PickerMode>(picker);

  // Watch for open change to reset view
  $effect(() => {
    if (open) {
      // Reset view to top-level picker mode when reopening
      if (currentView !== picker) currentView = picker;
    }
  });

  // Time State
  let timeHour = $state(0);
  let timeMinute = $state(0);
  let timeSecond = $state(0);
  let calendarHeight = $state(0);
  let calendarWidth = $state(0);

  function isPresetActive(preset: PresetEntry) {
    if (!calendarValue) return false;

    const resolved =
      typeof preset.value === "function" ? preset.value() : preset.value;
    if (!resolved) return false;

    if (mode === "range") {
      if (!calendarValue.start || !calendarValue.end || !resolved.start || !resolved.end) return false;
      return (
        calendarValue.start.toString() === resolved.start.toString() &&
        calendarValue.end.toString() === resolved.end.toString()
      );
    } else {
      return calendarValue.toString() === resolved.toString();
    }
  }

  const df = new DateFormatter("en-US", { dateStyle: "medium" });
  const monthNameFmt = new DateFormatter("en-US", { month: "short" });
  const yearFmt = new DateFormatter("en-US", { year: "numeric" });

  // --- Parsing / Formatting helpers ---
  $effect(() => {
    if (!value) {
      calendarValue = undefined;
    } else {
      if (mode === "range" && typeof value === "object") {
        // Handle Range input
        if (valueFormat && valueType === "text") {
          // Check for empty strings to handle clearing
          if (!value.start && !value.end) {
            calendarValue = undefined;
          } else {
            try {
              // If partial strings, try parse. If empty, strictly undefined?
              // parseDateFns throws on empty string usually.
              const start = value.start
                ? parseDateFns(value.start, valueFormat, new Date())
                : undefined;
              const end = value.end
                ? parseDateFns(value.end, valueFormat, new Date())
                : undefined;

              calendarValue = {
                start: start ? fromDate(start, getLocalTimeZone()) : undefined,
                end: end ? fromDate(end, getLocalTimeZone()) : undefined,
              };
            } catch (e) {
              console.error("Failed to parse range with format", e);
              // If parse fails, maybe we shouldn't keep old value?
              // But keeping old value is safer than crashing.
            }
          }
        } else {
          calendarValue = value;
        }
      } else if (mode === "single") {
        let parsed: any = value;

        // Handle JS Date
        if (value instanceof Date) {
          parsed = fromDate(value, getLocalTimeZone());
        }
        // Handle String
        else if (typeof value === "string") {
          try {
            if (valueFormat) {
              const d = parseDateFns(value, valueFormat, new Date());
              if (!isNaN(d.getTime())) {
                parsed = fromDate(d, getLocalTimeZone());
              }
            } else {
              if (value.includes("T")) parsed = parseDateTime(value);
              else parsed = parseDate(value);
            }
          } catch (e) {}
        }

        // If still valid
        if (parsed) {
          calendarValue = parsed;
        }

        // Sync time using parsed result (local) instead of calendarValue (state)
        if (parsed && "hour" in parsed) {
          timeHour = parsed.hour;
          timeMinute = parsed.minute;
          timeSecond = parsed.second;
        }
      }
    }
  });

  // Sync viewDate to the selected value to ensure it's visible
  $effect(() => {
    // This effect depends on calendarValue
    if (calendarValue) {
      if (mode === "single" && "day" in calendarValue) {
        viewDate = calendarValue;
      } else if (mode === "range" && calendarValue.start) {
        viewDate = calendarValue.start;
      }
    } else {
      viewDate = refDate;
    }
  });

  function formatDisplay(val: DateValue | Date): string {
    if (!val) return "";
    let dateObj: Date;
    if (val instanceof Date) dateObj = val;
    else dateObj = val.toDate(getLocalTimeZone());

    if (showTime) {
      return new DateFormatter("en-US", {
        dateStyle: "medium",
        timeStyle: "short",
      }).format(dateObj);
    }
    return df.format(dateObj);
  }

  // Format short date for presets (e.g., "Feb 1 - Feb 6")
  function formatShortDate(val: DateValue | Date): string {
    if (!val) return "";
    let dateObj: Date;
    if (val instanceof Date) dateObj = val;
    else dateObj = val.toDate(getLocalTimeZone());

    const shortFmt = new DateFormatter("en-US", {
      month: "short",
      day: "numeric",
    });
    return shortFmt.format(dateObj);
  }

  function getPresetDateRange(preset: PresetEntry): string {
    const resolved =
      typeof preset.value === "function" ? preset.value() : preset.value;

    if (!resolved) return "";

    // Range mode
    if (mode === "range" && resolved.start && resolved.end) {
      const startStr = formatShortDate(resolved.start);
      const endStr = formatShortDate(resolved.end);

      // Check if same year
      const startDate = resolved.start.toDate(getLocalTimeZone());
      const endDate = resolved.end.toDate(getLocalTimeZone());

      if (startDate.getFullYear() !== endDate.getFullYear()) {
        const yearFmt = new DateFormatter("en-US", {
          month: "short",
          day: "numeric",
          year: "numeric",
        });
        return `${yearFmt.format(startDate).replace(",", "")} - ${yearFmt.format(endDate).replace(",", "")}`;
      }

      return `${startStr} - ${endStr}`;
    }

    // Single mode
    return formatShortDate(resolved);
  }

  // --- Actions ---

  function emitValue(newVal: any) {
    if (!newVal) {
      value = undefined;
      onValueChange?.(undefined);
      return;
    }

    // Apply Time Logic for Single
    let finalVal = newVal;
    if (mode === "single" && showTime && "day" in newVal) {
      // DateValue
      finalVal = new CalendarDateTime(
        newVal.year,
        newVal.month,
        newVal.day,
        timeHour,
        timeMinute,
        timeSecond,
      );
    }

    if (mode === "range") finalVal = newVal;

    // Convert Output based on valueType
    let output = finalVal;

    // Helper to convert single val
    const convert = (v: any) => {
      if (!v) return undefined;
      const jsDate = v.toDate ? v.toDate(getLocalTimeZone()) : v;
      if (valueType === "date") return jsDate;
      if (valueType === "text") {
        if (valueFormat) {
          return formatDate(jsDate, valueFormat);
        }
        return v.toString();
      }
      return v;
    };

    if (mode === "range") {
      output = { start: convert(finalVal.start), end: convert(finalVal.end) };
    } else {
      output = convert(finalVal);
    }

    value = output;
    onValueChange?.(value);
  }

  function handleSelect(newVal: any) {
    calendarValue = newVal;
    if (mode === "single") {
      if (picker === "week") {
        // If week picker, we select the whole week range?
        // AntD week picker value is usually a specific date representing the week, but display shows range.
        // Let's store the date clicked, but formatDisplay shows week?
        // Or better: store the range of the week?
        // User requirement: "Week" view.
        // Let's assume value is the single date clicked, but we highlight week.
        emitValue(newVal);
        if (!showTime) open = false;
      } else {
        emitValue(newVal);
        if (!showTime) open = false;
      }
    } else {
      // Range
      emitValue(newVal);
    }
  }

  // --- Header Navigation Logic ---

  function nextMonth() {
    viewDate = viewDate.add({ months: 1 });
  }
  function prevMonth() {
    viewDate = viewDate.subtract({ months: 1 });
  }
  function nextYear() {
    viewDate = viewDate.add({ years: 1 });
  }
  function prevYear() {
    viewDate = viewDate.subtract({ years: 1 });
  }
  function nextDecade() {
    viewDate = viewDate.add({ years: 10 });
  }
  function prevDecade() {
    viewDate = viewDate.subtract({ years: 10 });
  }

  // Drill Down handlers
  function onYearTitleClick() {
    currentView = "year";
  }
  function onMonthTitleClick() {
    currentView = "month";
  }

  // Selection in Pivots
  function onYearPick(y: number) {
    viewDate = new CalendarDate(y, viewDate.month, 1); // Update view
    if (picker === "year") {
      handleSelect(new CalendarDate(y, 1, 1)); // Select if that was the target
    } else {
      currentView = "month"; // Drill up
    }
  }

  function onMonthPick(m: number) {
    viewDate = new CalendarDate(viewDate.year, m, 1);
    if (picker === "month") {
      handleSelect(new CalendarDate(viewDate.year, m, 1));
    } else if (picker === "quarter") {
      // Quarter picker typically stays in quarter view, but if we were in month view...
      // Wait, Quarter picker uses a specific Quarter Grid.
      // If we are in 'month' view (drilled down from something?), we go to Date.
      currentView = "date";
    } else {
      currentView = "date"; // Drill up
    }
  }

  function onQuarterPick(q: number) {
    let m = (q - 1) * 3 + 1;
    let y = viewDate.year;

    if (fiscal) {
      // Q1: Apr(4), Q2: Jul(7), Q3: Oct(10), Q4: Jan(1 next year)
      if (q === 1) m = 4;
      else if (q === 2) m = 7;
      else if (q === 3) m = 10;
      else if (q === 4) {
        m = 1;
        y++;
      }
    }

    const val = new CalendarDate(y, m, 1);
    handleSelect(val);
  }

  function getQuarterIndex(date: any, isFiscal: boolean) {
    if (!date) return -1;
    const m = date.month;
    if (!isFiscal) return Math.ceil(m / 3);

    // Fiscal: 4,5,6->1; 7,8,9->2; 10,11,12->3; 1,2,3->4
    if (m >= 4) return Math.ceil((m - 3) / 3);
    return 4;
  }

  // --- Data Grids ---
  const monthsData = Array.from({ length: 12 }, (_, i) => ({
    index: i + 1,
    label: monthNameFmt.format(new Date(2000, i, 1)),
  }));
  let quartersData = $derived(
    fiscal
      ? [
          { index: 1, label: "Q1 (Apr-Jun)" },
          { index: 2, label: "Q2 (Jul-Sep)" },
          { index: 3, label: "Q3 (Oct-Dec)" },
          { index: 4, label: "Q4 (Jan-Mar)" },
        ]
      : [
          { index: 1, label: "Q1" },
          { index: 2, label: "Q2" },
          { index: 3, label: "Q3" },
          { index: 4, label: "Q4" },
        ],
  );

  function getYearsData(centerYear: number) {
    const start = Math.floor(centerYear / 10) * 10 - 1;
    return Array.from({ length: 12 }, (_, i) => start + i);
  }

  // Year range for month/year selects (e.g. ±30 years from current view)
  const yearSelectRange = $derived.by(() => {
    const y = viewDate.year;
    const start = y - 30;
    return Array.from({ length: 41 }, (_, i) => start + i);
  });

  // --- Week Logic ---
  // If picker='week', we need to highlight the row.
  // shadcn Calendar 'class' prop can handle modifiers, but it's tricky to inject dynamic "hover row" classes.
  // We will rely on built-in hover for date cells and maybe custom css for week rows if possible.
  // For now, standard date picker behavior for week is acceptable if we return week numbers.
</script>

<Popover.Root bind:open>
  <Popover.Trigger
    class="w-full text-left outline-none group"
    id="date-picker-trigger"
  >
    <DateInput
      value={calendarValue}
      {placeholder}
      {disabled}
      {mode}
      {formatDisplay}
      isActive={open}
      aria-invalid={ariaInvalid}
      onClear={() => emitValue(undefined)}
    />
  </Popover.Trigger>

  <Popover.Content
    class="w-auto p-0 max-w-[calc(100vw-10px)]"
    align="start"
    side="bottom"
  >
    <div
      class="flex flex-col sm:flex-row items-start shadow-lg rounded-md overflow-hidden bg-popover border text-popover-foreground w-fit"
    >
      {#if resolvedPresets.length > 0 || presetsContent}
        <div 
          class="border-b sm:border-b-0 sm:border-r bg-muted/20 flex flex-col shrink-0 w-full sm:w-[210px] sm:h-[var(--cal-height)] h-auto"
          style="--cal-height: {calendarHeight > 0 ? calendarHeight + 'px' : 'auto'}; max-width: {calendarWidth > 0 ? calendarWidth + 'px' : 'none'}"
        >
          <div class="w-full h-full overflow-x-auto sm:overflow-y-auto no-scrollbar custom-scrollbar">
            <div
              class="p-2 flex flex-row sm:flex-col gap-2 w-full sm:min-w-[210px]"
            >
              {#if presetsContent}
                {@render presetsContent(resolvedPresets)}
              {:else}
                {#each resolvedPresets as preset (preset.label)}
                  {@const active = isPresetActive(preset)}
                  <Button
                    variant="ghost"
                    size="sm"
                    class={cn(
                      "group relative justify-start h-auto py-2 sm:py-2.5 px-3 text-left flex flex-col items-start gap-0.5 sm:gap-1 transition-all duration-300 shrink-0",
                      "hover:bg-accent/80 sm:hover:pl-4 active:scale-[0.97] overflow-hidden",
                      active && "bg-primary/10 hover:bg-primary/15 shadow-sm",
                      "min-w-[120px] sm:min-w-0"
                    )}
                    onclick={() => {
                      const resolved =
                        typeof preset.value === "function"
                          ? preset.value()
                          : preset.value;
                      handleSelect(resolved);
                      if (mode === "single" && !showTime) open = false;
                    }}
                  >
                    {#if active}
                      <div 
                        transition:fade={{ duration: 200 }}
                        class="absolute left-0 top-0 bottom-0 w-1 bg-primary shadow-[0_0_8px_hsl(var(--primary)/0.4)] hidden sm:block"
                      ></div>
                      <div 
                        transition:fade={{ duration: 200 }}
                        class="absolute left-0 right-0 bottom-0 h-0.5 bg-primary sm:hidden"
                      ></div>
                    {/if}
                    <span class={cn(
                      "text-[11px] sm:text-xs font-semibold transition-colors truncate w-full",
                      active ? "text-primary" : "text-foreground"
                    )}>
                      {preset.label}
                    </span>
                    <span
                      class={cn(
                        "text-[9px] sm:text-[10px] font-medium leading-tight opacity-70 transition-colors truncate w-full",
                        active ? "text-primary/80" : "text-muted-foreground"
                      )}
                    >
                      {getPresetDateRange(preset)}
                    </span>
                  </Button>
                {/each}
              {/if}
            </div>
          </div>
        </div>
      {/if}

      <div class="flex flex-col min-w-0">
        <div class="flex">
          <div 
            bind:clientHeight={calendarHeight}
            bind:clientWidth={calendarWidth}
            class={cn("p-2 sm:p-3 flex flex-col gap-1.5 sm:gap-2 min-w-0")}
          >
            <!-- CUSTOM HEADER -->
            <div
              class="flex items-center justify-between px-0.5 sm:px-1 pb-1.5 sm:pb-2 border-b mb-1.5 sm:mb-2"
            >
              <div class="flex gap-1">
                {#if currentView === "date" || currentView === "week" || currentView === "month" || currentView === "quarter"}
                  <button
                    class="hover:text-primary p-1"
                    onclick={prevYear}
                    aria-label="Prev Year"
                    ><Icon name="chevrons-left" class="size-4" /></button
                  >
                {/if}
                {#if currentView === "date" || currentView === "week"}
                  <button
                    class="hover:text-primary p-1"
                    onclick={prevMonth}
                    aria-label="Prev Month"
                    ><Icon name="chevron-left" class="size-4" /></button
                  >
                {/if}
                {#if currentView === "year"}
                  <button
                    class="hover:text-primary p-1"
                    onclick={prevDecade}
                    aria-label="Prev Decade"
                    ><Icon name="chevrons-left" class="size-4" /></button
                  >
                {/if}
              </div>

              <!-- TITLE: month/year as editable selects in date/week view -->
              <div
                class="font-semibold text-sm flex items-center gap-1.5 min-w-0"
              >
                {#if currentView === "date" || currentView === "week"}
                  <Select.Root
                    type="single"
                    value={String(viewDate.month)}
                    onValueChange={(v) => {
                      if (v != null)
                        viewDate = new CalendarDate(
                          viewDate.year,
                          Number(v),
                          1,
                        );
                    }}
                  >
                    <Select.Trigger
                      class="h-7 px-2 text-xs font-semibold border-0 shadow-none bg-transparent min-w-0 max-w-[4.5rem]"
                    >
                      {monthNameFmt.format(viewDate.toDate(getLocalTimeZone()))}
                    </Select.Trigger>
                    <Select.Content>
                      {#each monthsData as m}
                        <Select.Item value={String(m.index)} label={m.label}>
                          {m.label}
                        </Select.Item>
                      {/each}
                    </Select.Content>
                  </Select.Root>
                  <Select.Root
                    type="single"
                    value={String(viewDate.year)}
                    onValueChange={(v) => {
                      if (v != null)
                        viewDate = new CalendarDate(
                          Number(v),
                          viewDate.month,
                          1,
                        );
                    }}
                  >
                    <Select.Trigger
                      class="h-7 px-2 text-xs font-semibold border-0 shadow-none bg-transparent min-w-0 max-w-[3.5rem]"
                    >
                      {viewDate.year}
                    </Select.Trigger>
                    <Select.Content>
                      {#each yearSelectRange as y}
                        <Select.Item value={String(y)} label={String(y)}>
                          {y}
                        </Select.Item>
                      {/each}
                    </Select.Content>
                  </Select.Root>
                {:else if currentView === "month" || currentView === "quarter"}
                  <button
                    class="hover:text-primary hover:underline underline-offset-4"
                    onclick={onYearTitleClick}
                    >{yearFmt.format(
                      viewDate.toDate(getLocalTimeZone()),
                    )}</button
                  >
                {:else if currentView === "year"}
                  <span
                    >{getYearsData(viewDate.year)[1]} - {getYearsData(
                      viewDate.year,
                    )[10]}</span
                  >
                {/if}
              </div>

              <div class="flex gap-1">
                {#if currentView === "date" || currentView === "week"}
                  <button class="hover:text-primary p-1" onclick={nextMonth}
                    ><Icon name="chevron-right" class="size-4" /></button
                  >
                {/if}
                {#if currentView === "date" || currentView === "week" || currentView === "month" || currentView === "quarter"}
                  <button class="hover:text-primary p-1" onclick={nextYear}
                    ><Icon name="chevrons-right" class="size-4" /></button
                  >
                {/if}
                {#if currentView === "year"}
                  <button class="hover:text-primary p-1" onclick={nextDecade}
                    ><Icon name="chevrons-right" class="size-4" /></button
                  >
                {/if}
              </div>
            </div>

            <!-- VIEW GRIDS -->
            <!-- Date / Week -->
            {#if currentView === "date" || currentView === "week"}
              {#if mode === "range"}
                <RangeCalendar
                  value={calendarValue}
                  bind:placeholder={viewDate}
                  class="rounded-md border-0 p-0 [&_nav]:hidden [&_header]:hidden"
                  onValueChange={handleSelect}
                />
              {:else}
                <Calendar
                  value={calendarValue}
                  type="single"
                  bind:placeholder={viewDate}
                  class="rounded-md border-0 p-0 [&_nav]:hidden [&_header]:hidden"
                  onValueChange={handleSelect}
                />
              {/if}

              <!-- Month -->
            {:else if currentView === "month"}
              <div class="grid grid-cols-3 gap-4 py-4">
                {#each monthsData as m}
                  <Button
                    variant={viewDate.month === m.index ? "default" : "ghost"}
                    class="h-10"
                    onclick={() => onMonthPick(m.index)}
                  >
                    {m.label}
                  </Button>
                {/each}
              </div>

              <!-- Quarter -->
            {:else if currentView === "quarter"}
              <div class="grid grid-cols-2 gap-4 py-4">
                {#each quartersData as q}
                  <Button
                    variant={getQuarterIndex(viewDate, fiscal) === q.index
                      ? "default"
                      : "outline"}
                    class="h-16 text-lg"
                    onclick={() => onQuarterPick(q.index)}
                  >
                    {q.label}
                  </Button>
                {/each}
              </div>

              <!-- Year -->
            {:else if currentView === "year"}
              <div class="grid grid-cols-3 gap-4 py-4">
                {#each getYearsData(viewDate.year) as y}
                  <Button
                    variant={viewDate.year === y ? "default" : "ghost"}
                    class={cn("h-10", y === viewDate.year && "font-bold")}
                    onclick={() => onYearPick(y)}
                  >
                    {y}
                  </Button>
                {/each}
              </div>
            {/if}
          </div>

          <!-- Time Picker Side Panel -->
          {#if showTime && mode === "single" && currentView === "date"}
            <div class="border-t sm:border-t-0 sm:border-l py-3 pr-3 pl-3">
              <div
                class="text-xs font-medium text-center text-muted-foreground pb-2 border-b mb-1"
              >
                Time
              </div>
              <TimePicker
                hour={timeHour}
                minute={timeMinute}
                class="h-[200px] sm:h-[280px]"
                onChange={(h, m, s) => {
                  timeHour = h;
                  timeMinute = m;
                  timeSecond = 0; // Reset seconds
                  if (calendarValue) emitValue(calendarValue);
                }}
              />
            </div>
          {/if}
        </div>

        <!-- Footer / Time (Deprecated block removed) -->

        <!-- Footer Actions -->
        {#if currentView === "date" && (mode !== "range" || showTime)}
          <div class="border-t p-2 flex justify-between items-center text-xs">
            {#if mode !== "range"}
              <button
                class="text-primary hover:underline font-medium"
                onclick={() => {
                  const now = today(getLocalTimeZone());
                  emitValue(now);
                  viewDate = now;
                  if (!showTime) open = false;
                }}
              >
                Today
              </button>
            {/if}
            {#if showTime}
              <Button size="sm" class="h-7 px-3" onclick={() => (open = false)}
                >Ok</Button
              >
            {/if}
          </div>
        {/if}
      </div>
    </div>
  </Popover.Content>
</Popover.Root>

<style>
  .custom-scrollbar::-webkit-scrollbar {
    width: 5px;
  }
  .custom-scrollbar::-webkit-scrollbar-track {
    background: transparent;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb {
    background: hsl(var(--muted-foreground) / 0.2);
    border-radius: 10px;
  }
  .custom-scrollbar::-webkit-scrollbar-thumb:hover {
    background: hsl(var(--muted-foreground) / 0.4);
  }
  /* Firefox */
  .custom-scrollbar {
    scrollbar-width: thin;
    scrollbar-color: hsl(var(--muted-foreground) / 0.2) transparent;
  }
  .no-scrollbar::-webkit-scrollbar {
    display: none;
  }
  .no-scrollbar {
    -ms-overflow-style: none;
    scrollbar-width: none;
  }
</style>
