<script lang="ts">
    import { DatePicker, DatePresets, type PresetEntry } from "$lib/components/venUI/date-picker";
    import { today, getLocalTimeZone, CalendarDate } from "@internationalized/date";

    let date = $state<CalendarDate>();
    let dateRange = $state<{start: CalendarDate, end: CalendarDate}>();
    let dateTime = $state();
    
    // Formatting Examples
    let isoDate = $state<string>();
    let fmtDate = $state<string>();
    let jsDate = $state<Date>();
    let fmtRange = $state<{start: string, end: string}>();

    // Custom Object Binding Example
    let userFilter = $state({
        userId: 'aziz',
        from: '01-Jan-25',
        to: '31-Jan-25'
    });

    // Presets using Utility
    const now = today(getLocalTimeZone());
    
    // Example: Pass 'now' to ensure consistency or let it default
    const simplePresets: PresetEntry[] = DatePresets.getCommonSinglePresets(now);

    const rangePresets: PresetEntry[] = [
        ...DatePresets.getCommonRangePresets(now),
        DatePresets.range.thisQuarter(now),
        DatePresets.range.lastQuarter(now),
        DatePresets.range.thisFinYear(now),
        DatePresets.range.lastFinYear(now),
    ];

</script>

<div class="p-8 space-y-8 container mx-auto">
  <h1 class="text-3xl font-bold mb-6">Date Picker Examples</h1>

  <div class="grid grid-cols-1 md:grid-cols-2 gap-8">
    <!-- Basic -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">Basic Single Date</h2>
      <div class="w-full max-w-sm">
        <DatePicker bind:value={date} />
      </div>
      <p class="text-sm text-muted-foreground">Selected: {date?.toString()}</p>
    </div>

    <!-- Range -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">Date Range</h2>
      <div class="w-full max-w-sm">
        <DatePicker mode="range" bind:value={dateRange} />
      </div>
      <p class="text-sm text-muted-foreground">
        Selected: {dateRange?.start
          ? `${dateRange.start} - ${dateRange.end}`
          : ""}
      </p>
    </div>

    <!-- Time -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">With Time</h2>
      <div class="w-full max-w-sm">
        <DatePicker showTime bind:value={dateTime} />
      </div>
      <p class="text-sm text-muted-foreground">
        Selected: {dateTime?.toString()}
      </p>
    </div>

    <!-- Presets -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">With Presets</h2>
      <div class="w-full max-w-sm">
        <DatePicker presets={simplePresets} />
      </div>
      <p class="text-sm text-muted-foreground">Try selecting a preset</p>
    </div>

    <!-- Range Presets -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">Range with Presets</h2>
      <div class="w-full max-w-sm">
        <DatePicker mode="range" presets={rangePresets} />
      </div>
      <p class="text-sm text-muted-foreground">Common ranges available</p>
    </div>

    <!-- Different Modes -->
    <div class="space-y-4 border p-4 rounded-lg">
      <h2 class="text-xl font-semibold">Picker Modes</h2>
      <div class="space-y-2 w-full max-w-sm">
        <p class="text-sm font-medium">Month Picker</p>
        <DatePicker picker="month" placeholder="Select Month" />

        <p class="text-sm font-medium pt-2">Quarter Picker (Calendar)</p>
        <DatePicker picker="quarter" placeholder="Select Quarter" />

        <p class="text-sm font-medium pt-2">Fiscal Quarter (Q1 = Apr)</p>
        <DatePicker picker="quarter" fiscal placeholder="Select FY Quarter" />

        <p class="text-sm font-medium pt-2">Year Picker</p>
        <DatePicker picker="year" placeholder="Select Year" />
      </div>
    </div>

    <!-- Value Parsing & Formatting -->
    <div class="space-y-4 border p-4 rounded-lg md:col-span-2">
      <h2 class="text-xl font-semibold">Value Type & Formatting</h2>
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
        <!-- ISO String -->
        <div class="space-y-2">
          <p class="text-sm font-medium">ISO String (Default Text)</p>
          <DatePicker valueType="text" bind:value={isoDate} />
          <div class="mt-2 p-2 bg-muted rounded text-xs font-mono break-all">
            Type: {typeof isoDate}<br />
            Value: {JSON.stringify(isoDate)}
          </div>
        </div>

        <!-- Formatted String -->
        <div class="space-y-2">
          <p class="text-sm font-medium">Formatted (dd-MMM-yyyy)</p>
          <DatePicker
            valueType="text"
            valueFormat="dd-MMM-yyyy"
            bind:value={fmtDate}
          />
          <div class="mt-2 p-2 bg-muted rounded text-xs font-mono break-all">
            Type: {typeof fmtDate}<br />
            Value: {JSON.stringify(fmtDate)}
          </div>
        </div>

        <!-- JS Date -->
        <div class="space-y-2">
          <p class="text-sm font-medium">JS Date Object</p>
          <DatePicker valueType="date" bind:value={jsDate} />
          <div class="mt-2 p-2 bg-muted rounded text-xs font-mono break-all">
            Type: {jsDate instanceof Date ? "Date" : typeof jsDate}<br />
            Value: {String(jsDate)}
          </div>
        </div>
        <!-- Range Formatted -->
        <div class="space-y-2">
          <p class="text-sm font-medium">Range (Formatted)</p>
          <DatePicker
            mode="range"
            valueType="text"
            valueFormat="yyyy-MM-dd"
            bind:value={fmtRange}
          />
          <div class="mt-2 p-2 bg-muted rounded text-xs font-mono break-all">
            Value: {JSON.stringify(fmtRange, null, 2)}
          </div>
        </div>

        <!-- Custom Object Binding -->
        <div class="space-y-2">
          <p class="text-sm font-medium">Custom Binding (from/to)</p>
          <DatePicker
            mode="range"
            valueType="text"
            valueFormat="dd-MMM-yy"
            value={{ start: userFilter.from, end: userFilter.to }}
            onValueChange={(v) => {
              if (v?.start) userFilter.from = v.start;
              if (v?.end) userFilter.to = v.end;
              if (!v) {
                userFilter.from = "";
                userFilter.to = "";
              }
            }}
          />
          <div
            class="mt-2 p-2 bg-muted rounded text-xs font-mono break-all leading-tight"
          >
            Object: {JSON.stringify(userFilter, null, 2)}
          </div>
        </div>
      </div>
    </div>
  </div>
</div>
