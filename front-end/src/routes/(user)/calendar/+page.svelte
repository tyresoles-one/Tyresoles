<script lang="ts">
	import { onMount } from 'svelte';
	import { CalendarView, CalendarEventSheet, type CalendarViewMode } from '$lib/components/venUI/calendar-view';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import {
		getMyCalendarEvents,
		getUpcomingReminders,
		snoozeReminder,
		exportCalendarIcs,
		type CalendarEventDto,
	} from '$lib/services/calendar/api';
	import {
		today,
		getLocalTimeZone,
		startOfMonth,
		endOfMonth,
		startOfWeek,
		endOfWeek,
		type CalendarDate,
	} from '@internationalized/date';

	// ── State ─────────────────────────────────────────────────────────────────
	let events           = $state<CalendarEventDto[]>([]);
	let loading          = $state(true);
	let currentMonth     = $state(today(getLocalTimeZone()));
	let selectedDate     = $state<CalendarDate | undefined>(undefined);
	let viewMode         = $state<CalendarViewMode>('month');

	// Sheet state
	let sheetOpen        = $state(false);
	let sheetEvent       = $state<CalendarEventDto | undefined>(undefined);
	let sheetDefaultDate = $state<string | undefined>(undefined);
	let sheetDefaultHour = $state(9);

	// Reminders
	let upcomingReminders = $state<CalendarEventDto[]>([]);
	let exportLoading     = $state(false);

	// ── Range derivation ──────────────────────────────────────────────────────
	const rangeFrom = $derived.by(() => {
		if (viewMode === 'week') {
			const start = startOfWeek(currentMonth, 'en-US');
			return new Date(start.year, start.month - 1, start.day);
		}
		if (viewMode === 'day' || viewMode === 'agenda') {
			const d = selectedDate ?? currentMonth;
			return new Date(d.year, d.month - 1, d.day);
		}
		const start = startOfMonth(currentMonth);
		return new Date(start.year, start.month - 1, start.day);
	});

	const rangeTo = $derived.by(() => {
		if (viewMode === 'week') {
			const end = endOfWeek(currentMonth, 'en-US');
			return new Date(end.year, end.month - 1, end.day, 23, 59, 59);
		}
		if (viewMode === 'day' || viewMode === 'agenda') {
			const d = selectedDate ?? currentMonth;
			return new Date(d.year, d.month - 1, d.day, 23, 59, 59);
		}
		const end = endOfMonth(currentMonth);
		return new Date(end.year, end.month - 1, end.day, 23, 59, 59);
	});

	// ── Data fetching ─────────────────────────────────────────────────────────
	async function fetchEvents() {
		loading = true;
		try {
			events = await getMyCalendarEvents(rangeFrom, rangeTo);
		} catch (e) {
			toast.error(e instanceof Error ? e.message : 'Failed to load events');
			events = [];
		} finally {
			loading = false;
		}
	}

	$effect(() => { rangeFrom; rangeTo; fetchEvents(); });

	async function fetchUpcomingReminders() {
		try {
			upcomingReminders = await getUpcomingReminders(new Date(Date.now() + 24 * 60 * 60 * 1000));
		} catch {
			upcomingReminders = [];
		}
	}

	async function handleSnooze(reminderId: string, minutes: number) {
		try {
			await snoozeReminder(reminderId, new Date(Date.now() + minutes * 60 * 1000));
			toast.success('Reminder snoozed');
			await fetchUpcomingReminders();
		} catch (e) {
			toast.error(e instanceof Error ? e.message : 'Failed to snooze');
		}
	}

	onMount(() => { fetchUpcomingReminders(); });

	// ── Sheet helpers ─────────────────────────────────────────────────────────
	function openCreate(date?: CalendarDate, hour?: number) {
		sheetEvent = undefined;
		sheetDefaultDate = date
			? `${date.year}-${String(date.month).padStart(2,'0')}-${String(date.day).padStart(2,'0')}`
			: undefined;
		sheetDefaultHour = hour ?? 9;
		sheetOpen = true;
	}

	function openEdit(ev: CalendarEventDto) {
		sheetEvent = ev;
		sheetDefaultDate = undefined;
		sheetOpen = true;
	}

	// ── Export ICS ────────────────────────────────────────────────────────────
	async function handleExportIcs() {
		exportLoading = true;
		try {
			const ics = await exportCalendarIcs(rangeFrom, rangeTo);
			const blob = new Blob([ics], { type: 'text/calendar' });
			const a = document.createElement('a');
			a.href = URL.createObjectURL(blob);
			a.download = `calendar-${rangeFrom.toISOString().slice(0, 10)}.ics`;
			a.click();
			URL.revokeObjectURL(a.href);
			toast.success('Calendar exported');
		} catch (e) {
			toast.error(e instanceof Error ? e.message : 'Export failed');
		} finally {
			exportLoading = false;
		}
	}
</script>

<svelte:head>
	<title>Calendar - Tyresoles</title>
</svelte:head>

<PageHeading title="Calendar" icon="calendar">
	{#snippet actions()}
		<Button variant="outline" size="sm" disabled={exportLoading} onclick={handleExportIcs}>
			{#if exportLoading}
				<Icon name="loader-circle" class="size-4 animate-spin" />
			{:else}
				<Icon name="download" class="size-4" />
				<span class="hidden sm:inline ml-1">Export ICS</span>
			{/if}
		</Button>
	{/snippet}
</PageHeading>

<div class="p-4">
	<!-- Upcoming reminders strip -->
	{#if upcomingReminders.length > 0}
		<div class="mb-4 flex flex-wrap items-center gap-2 rounded-xl border border-amber-500/30 bg-amber-500/5 px-4 py-2.5">
			<Icon name="bell" class="size-4 shrink-0 text-amber-500" />
			<span class="text-sm font-semibold text-amber-600 dark:text-amber-400">Upcoming reminders</span>
			{#each upcomingReminders as ev}
				{#each ev.reminders.filter((r) => !r.isSent && (!r.snoozeUntilUtc || new Date(r.snoozeUntilUtc) <= new Date())) as rem}
					<div class="flex items-center gap-2 rounded-lg bg-amber-500/10 px-2.5 py-1">
						<span class="text-xs font-medium text-foreground truncate max-w-[180px]">{ev.title}</span>
						<span class="text-xs text-muted-foreground shrink-0">
							{new Date(rem.remindAtUtc).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' })}
						</span>
						<Button variant="ghost" size="sm" class="h-6 px-2 text-xs" onclick={() => handleSnooze(rem.id, 15)}>
							Snooze
						</Button>
					</div>
				{/each}
			{/each}
		</div>
	{/if}

	<!-- Calendar grid -->
	<CalendarView
		{events}
		bind:currentMonth
		bind:selectedDate
		bind:viewMode
		class="min-h-[600px]"
		onSelectEvent={(ev: CalendarEventDto) => openEdit(ev)}
		onSelectSlot={(date: CalendarDate, hour?: number) => {
			selectedDate = date;
			openCreate(date, hour ?? 9);
		}}
		onAddEvent={(date?: CalendarDate) => openCreate(date)}
	/>
</div>

<!-- Unified Create/Edit Sheet -->
<CalendarEventSheet
	bind:open={sheetOpen}
	event={sheetEvent}
	defaultDate={sheetDefaultDate}
	defaultHour={sheetDefaultHour}
	onSaved={() => fetchEvents()}
	onDeleted={() => fetchEvents()}
/>
