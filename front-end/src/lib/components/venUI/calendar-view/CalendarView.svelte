<script lang="ts">
	import { cn } from '$lib/utils';
	import {
		today,
		getLocalTimeZone,
		startOfMonth,
		endOfMonth,
		startOfWeek,
		endOfWeek,
		getDayOfWeek,
		isSameDay,
		parseDate,
		type DateValue,
		type CalendarDate,
	} from '@internationalized/date';
	import type { CalendarEventDto } from '$lib/services/calendar/api';
	import * as Popover from '$lib/components/ui/popover';
	import { Icon } from '$lib/components/venUI/icon';
	import { fly, fade } from 'svelte/transition';
	import { cubicOut } from 'svelte/easing';

	export type CalendarViewMode = 'month' | 'week' | 'day' | 'agenda';

	let {
		events = [] as CalendarEventDto[],
		selectedDate = $bindable(undefined as CalendarDate | undefined),
		currentMonth = $bindable(today(getLocalTimeZone())),
		viewMode = $bindable('month' as CalendarViewMode),
		onSelectDay,
		onSelectEvent,
		onSelectSlot,
		onAddEvent,
		class: className = '',
	}: {
		events?: CalendarEventDto[];
		selectedDate?: CalendarDate;
		currentMonth?: CalendarDate;
		viewMode?: CalendarViewMode;
		onSelectDay?: (date: CalendarDate) => void;
		onSelectEvent?: (event: CalendarEventDto) => void;
		onSelectSlot?: (date: CalendarDate, hour?: number) => void;
		onAddEvent?: (date?: CalendarDate) => void;
		class?: string;
	} = $props();

	// ── Grid ───────────────────────────────────────────────────────────────────
	const grid = $derived.by(() => {
		const start = startOfMonth(currentMonth);
		const end = endOfMonth(currentMonth);
		const startDow = getDayOfWeek(start, 'en-US');
		const daysInMonth = end.day;
		const rows: (CalendarDate | null)[][] = [];
		let dayIndex = 1;
		for (let row = 0; row < 6; row++) {
			const week: (CalendarDate | null)[] = [];
			for (let col = 0; col < 7; col++) {
				const cellIndex = row * 7 + col;
				if (cellIndex < startDow || dayIndex > daysInMonth) {
					week.push(null);
				} else {
					const y = currentMonth.year;
					const m = String(currentMonth.month).padStart(2, '0');
					const d = String(dayIndex).padStart(2, '0');
					week.push(parseDate(`${y}-${m}-${d}`));
					dayIndex++;
				}
			}
			rows.push(week);
		}
		return rows;
	});

	// ── Week ───────────────────────────────────────────────────────────────────
	const weekStart = $derived(viewMode === 'week' ? startOfWeek(currentMonth, 'en-US') : currentMonth);
	const weekEnd = $derived(viewMode === 'week' ? endOfWeek(currentMonth, 'en-US') : currentMonth);
	const weekDates = $derived.by(() => {
		if (viewMode !== 'week') return [];
		const dates: CalendarDate[] = [];
		let d = weekStart;
		for (let i = 0; i < 7; i++) {
			dates.push(d);
			d = d.add({ days: 1 }) as CalendarDate;
		}
		return dates;
	});

	// ── Agenda / Day ───────────────────────────────────────────────────────────
	const agendaEvents = $derived.by(() => {
		if (viewMode !== 'agenda') return [];
		return [...events].sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime());
	});
	const dayDate = $derived(selectedDate ?? currentMonth);
	const dayEvents = $derived(viewMode === 'day' ? eventsForDay(dayDate) : []);

	// ── Helpers ────────────────────────────────────────────────────────────────
	const weekDaysFull = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
	const weekDaysShort = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];

	function dateToMs(d: DateValue, h: number, m: number, s: number, ms: number) {
		return new Date(d.year, d.month - 1, d.day, h, m, s, ms).getTime();
	}

	function eventsForDay(date: DateValue): CalendarEventDto[] {
		const dayStart = dateToMs(date, 0, 0, 0, 0);
		const dayEnd = dateToMs(date, 23, 59, 59, 999);
		return events.filter((e) => {
			const start = new Date(e.startUtc).getTime();
			const end = new Date(e.endUtc).getTime();
			return end >= dayStart && start <= dayEnd;
		});
	}

	function isSelected(d: DateValue) {
		return selectedDate ? isSameDay(d, selectedDate) : false;
	}

	function isToday(d: DateValue) {
		return isSameDay(d, today(getLocalTimeZone()));
	}

	function formatTime(iso: string): string {
		return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit', hour12: true });
	}

	function formatDuration(a: string, b: string): string {
		const diff = (new Date(b).getTime() - new Date(a).getTime()) / 60000;
		if (diff < 60) return `${diff}m`;
		const h = Math.floor(diff / 60);
		const m = diff % 60;
		return m ? `${h}h ${m}m` : `${h}h`;
	}

	// Navigate
	function prev() {
		if (viewMode === 'month') currentMonth = currentMonth.subtract({ months: 1 }) as CalendarDate;
		else if (viewMode === 'week') currentMonth = currentMonth.subtract({ weeks: 1 }) as CalendarDate;
		else currentMonth = currentMonth.subtract({ days: 1 }) as CalendarDate;
	}
	function next() {
		if (viewMode === 'month') currentMonth = currentMonth.add({ months: 1 }) as CalendarDate;
		else if (viewMode === 'week') currentMonth = currentMonth.add({ weeks: 1 }) as CalendarDate;
		else currentMonth = currentMonth.add({ days: 1 }) as CalendarDate;
	}
	function goToday() {
		currentMonth = today(getLocalTimeZone());
		selectedDate = today(getLocalTimeZone());
	}

	// Title label
	const titleLabel = $derived.by(() => {
		if (viewMode === 'month')
			return new Date(currentMonth.year, currentMonth.month - 1).toLocaleString('en-US', { month: 'long', year: 'numeric' });
		if (viewMode === 'week') {
			const s = new Date(weekStart.year, weekStart.month - 1, weekStart.day);
			const e = new Date(weekEnd.year, weekEnd.month - 1, weekEnd.day);
			return `${s.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })} – ${e.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })}`;
		}
		if (viewMode === 'day')
			return new Date(dayDate.year, dayDate.month - 1, dayDate.day).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' });
		return 'Agenda';
	});

	const modeIcons: Record<CalendarViewMode, string> = {
		month: 'calendar-days',
		week: 'calendar-range',
		day: 'calendar',
		agenda: 'list',
	};

	// Group agenda events by date
	const agendaGrouped = $derived.by(() => {
		const groups: { dateLabel: string; date: Date; events: CalendarEventDto[] }[] = [];
		if (viewMode !== 'agenda') return groups;
		for (const ev of agendaEvents) {
			const d = new Date(ev.startUtc);
			const key = d.toDateString();
			const last = groups[groups.length - 1];
			if (last && last.date.toDateString() === key) {
				last.events.push(ev);
			} else {
				groups.push({
					dateLabel: d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' }),
					date: d,
					events: [ev],
				});
			}
		}
		return groups;
	});

	// Hours to show in week / day view (6am–10pm default for compactness)
	const displayHours = Array.from({ length: 24 }, (_, i) => i);

	function eventsForSlot(d: DateValue, hour: number): CalendarEventDto[] {
		return events.filter((e) => {
			const start = new Date(e.startUtc);
			const end = new Date(e.endUtc);
			const slotStart = dateToMs(d, hour, 0, 0, 0);
			const slotEnd = dateToMs(d, hour + 1, 0, 0, 0);
			if (e.isAllDay) return hour === 0 && isSameDay(d, { year: start.getFullYear(), month: start.getMonth() + 1, day: start.getDate() } as CalendarDate);
			return start.getTime() < slotEnd && end.getTime() > slotStart;
		});
	}

	function eventDot(ev: CalendarEventDto) {
		return ev.eventTypeColor ?? 'var(--primary)';
	}
</script>

<!-- ═══════════════════════════════════════════════════════════════════════
     Root container — uses theme tokens so it adapts to light/dark.
     ═══════════════════════════════════════════════════════════════════════ -->
<div
	class={cn(
		'flex flex-col overflow-hidden rounded-xl border border-border bg-card text-card-foreground shadow-sm',
		className
	)}
>

	<!-- ── Toolbar ──────────────────────────────────────────────────────────── -->
	<div class="flex items-center justify-between gap-2 border-b border-border bg-card px-3 py-2 sm:px-4">
		<!-- Left: nav -->
		<div class="flex min-w-0 items-center gap-1">
			<button
				type="button"
				onclick={goToday}
				class="hidden rounded-md border border-border px-2 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-muted hover:text-foreground sm:inline-flex"
			>
				Today
			</button>
			<button
				type="button"
				onclick={prev}
				aria-label="Previous"
				class="rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
			>
				<Icon name="chevron-left" class="size-4" />
			</button>
			<button
				type="button"
				onclick={next}
				aria-label="Next"
				class="rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
			>
				<Icon name="chevron-right" class="size-4" />
			</button>
			<span class="ml-1 truncate text-sm font-semibold text-foreground sm:text-base">{titleLabel}</span>
		</div>

		<!-- Right: view switcher + add -->
		<div class="flex shrink-0 items-center gap-1.5">
			<!-- View switcher: icon-only on mobile, text on sm+ -->
			<div class="flex items-center rounded-lg border border-border p-0.5">
				{#each (['month', 'week', 'day', 'agenda'] as CalendarViewMode[]) as mode}
					<button
						type="button"
						onclick={() => (viewMode = mode)}
						title={mode.charAt(0).toUpperCase() + mode.slice(1)}
						class={cn(
							'flex items-center gap-1 rounded-md px-1.5 py-1 text-xs font-medium transition-all',
							viewMode === mode
								? 'bg-primary text-primary-foreground shadow-sm'
								: 'text-muted-foreground hover:bg-muted hover:text-foreground'
						)}
					>
						<Icon name={modeIcons[mode]} class="size-3.5 shrink-0" />
						<span class="hidden sm:inline">{mode.charAt(0).toUpperCase() + mode.slice(1)}</span>
					</button>
				{/each}
			</div>

			{#if onAddEvent}
				<button
					type="button"
					onclick={() => onAddEvent?.()}
					class="flex items-center gap-1 rounded-lg bg-primary px-2 py-1.5 text-xs font-semibold text-primary-foreground transition-all hover:opacity-90 active:scale-95"
				>
					<Icon name="plus" class="size-3.5" />
					<span class="hidden sm:inline">New</span>
				</button>
			{/if}
		</div>
	</div>

	<!-- ═══════════════════════════════════════════════════════════════════════
	     MONTH VIEW
	     ═══════════════════════════════════════════════════════════════════════ -->
	{#if viewMode === 'month'}
		<!-- Day-of-week headers -->
		<div class="grid grid-cols-7 border-b border-border bg-muted/30">
			{#each weekDaysShort as w, i}
				<div class={cn(
					'py-1.5 text-center text-[10px] font-bold uppercase tracking-wider text-muted-foreground',
					i === 0 && 'text-destructive/70',
					i === 6 && 'text-primary/70'
				)}>
					<!-- Full name on md+, short on mobile -->
					<span class="hidden md:inline">{weekDaysFull[i].slice(0, 3)}</span>
					<span class="md:hidden">{w}</span>
				</div>
			{/each}
		</div>

		<!-- Calendar grid -->
		<div class="grid flex-1 grid-rows-6" style="min-height:0">
			{#each grid as week}
				<div class="grid grid-cols-7">
					{#each week as day, col}
						{@const dayEvs = day ? eventsForDay(day) : []}
						{@const isWeekend = col === 0 || col === 6}
						<div
					class={cn(
						'group relative min-h-[72px] border-b border-r border-border p-1 transition-colors sm:min-h-[88px]',
						day && 'cursor-pointer hover:bg-muted/40',
						day && isSelected(day) && 'bg-primary/8 ring-inset ring-1 ring-primary',
						day && isToday(day) && !isSelected(day) && 'bg-accent/30',
						!day && 'bg-muted/10',
						isWeekend && day && 'bg-muted/5'
					)}
					role="gridcell"
					onclick={() => {
						if (day) { selectedDate = day; onSelectDay?.(day); }
					}}
					onkeydown={(e) => {
						if (day && (e.key === 'Enter' || e.key === ' ')) { selectedDate = day; onSelectDay?.(day); }
					}}
				>
							{#if day}
								<!-- Day number -->
								<div class="mb-0.5 flex items-center justify-between">
									<span
										class={cn(
											'inline-flex size-6 items-center justify-center rounded-full text-xs font-semibold',
											isToday(day) && 'bg-primary text-primary-foreground',
											isSelected(day) && !isToday(day) && 'bg-primary/20 text-primary font-bold',
											!isToday(day) && !isSelected(day) && 'text-foreground/80'
										)}
									>
										{day.day}
									</span>
									{#if dayEvs.length > 0 && onAddEvent}
										<button
											type="button"
											class="invisible size-4 rounded text-muted-foreground group-hover:visible hover:text-primary"
											onclick={(e) => { e.stopPropagation(); onAddEvent?.(day); }}
											title="Add event"
										>
											<Icon name="plus" class="size-3.5" />
										</button>
									{/if}
								</div>

								<!-- Events -->
								<div class="space-y-0.5">
									{#each dayEvs.slice(0, 2) as ev}
										<button
											type="button"
											class="flex w-full items-center gap-1 truncate rounded-[4px] px-1 py-0.5 text-left text-[10px] font-medium leading-tight text-white transition-opacity hover:opacity-80 sm:text-xs"
											style="background-color: {eventDot(ev)}"
											onclick={(e) => { e.stopPropagation(); onSelectEvent?.(ev); }}
										>
											{#if !ev.isAllDay}
												<span class="hidden shrink-0 sm:inline opacity-80">{formatTime(ev.startUtc)}</span>
											{/if}
											<span class="truncate">{ev.title}</span>
										</button>
									{/each}
									{#if dayEvs.length > 2}
										<Popover.Root>
											<Popover.Trigger>
												<button
													type="button"
													class="w-full rounded-[4px] px-1 py-0.5 text-left text-[10px] text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
													onclick={(e: MouseEvent) => e.stopPropagation()}
												>
													+{dayEvs.length - 2} more
												</button>
											</Popover.Trigger>
											<Popover.Content class="w-60 p-2" align="start" onclick={(e: MouseEvent) => e.stopPropagation()}>
												<p class="mb-2 text-xs font-semibold text-muted-foreground">
													{new Date(day.year, day.month - 1, day.day).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })}
												</p>
												<ul class="space-y-1">
													{#each dayEvs as ev}
														<li>
															<button
																type="button"
																class="flex w-full items-start gap-2 rounded-md px-2 py-1.5 text-left transition-colors hover:bg-muted"
																onclick={() => onSelectEvent?.(ev)}
															>
																<span
																	class="mt-1 size-2 shrink-0 rounded-full"
																	style="background-color:{eventDot(ev)}"
																></span>
																<div class="min-w-0">
																	<p class="truncate text-xs font-medium">{ev.title}</p>
																	<p class="text-[10px] text-muted-foreground">
																		{ev.isAllDay ? 'All day' : formatTime(ev.startUtc)}
																	</p>
																</div>
															</button>
														</li>
													{/each}
												</ul>
											</Popover.Content>
										</Popover.Root>
									{/if}
								</div>
							{/if}
						</div>
					{/each}
				</div>
			{/each}
		</div>
	{/if}

	<!-- ═══════════════════════════════════════════════════════════════════════
	     WEEK VIEW
	     ═══════════════════════════════════════════════════════════════════════ -->
	{#if viewMode === 'week'}
		<div class="flex flex-1 flex-col overflow-hidden">
			<!-- Day header row -->
			<div class="grid border-b border-border" style="grid-template-columns: 3rem repeat(7, 1fr)">
				<div class="border-r border-border"></div>
				{#each weekDates as d, i}
					<button
						type="button"
						class={cn(
							'flex flex-col items-center border-r border-border py-2 text-center last:border-r-0 transition-colors hover:bg-muted/50',
							isSelected(d) && 'bg-primary/8',
							isToday(d) && !isSelected(d) && 'bg-accent/30'
						)}
						onclick={() => { selectedDate = d; onSelectDay?.(d); }}
					>
						<span class={cn(
							'text-[10px] font-semibold uppercase tracking-wider',
							isToday(d) ? 'text-primary' : 'text-muted-foreground'
						)}>
							{weekDaysShort[getDayOfWeek(d, 'en-US')]}
						</span>
						<span
							class={cn(
								'mt-0.5 flex size-7 items-center justify-center rounded-full text-sm font-bold',
								isToday(d) && 'bg-primary text-primary-foreground',
								isSelected(d) && !isToday(d) && 'bg-primary/15 text-primary'
							)}
						>
							{d.day}
						</span>
					</button>
				{/each}
			</div>

			<!-- Time slots -->
			<div class="flex-1 overflow-y-auto scrollbar-none">
				{#each displayHours as hour}
					<div class="grid border-b border-border last:border-b-0" style="grid-template-columns: 3rem repeat(7, 1fr)">
						<!-- Time label -->
						<div class="sticky left-0 border-r border-border bg-card px-1 py-0.5 text-right text-[10px] text-muted-foreground">
							{hour === 0 ? '12am' : hour < 12 ? `${hour}am` : hour === 12 ? '12pm' : `${hour - 12}pm`}
						</div>
						{#each weekDates as d}
							{@const slotEvs = eventsForSlot(d, hour)}
						<div
					class="min-h-[36px] border-r border-border last:border-r-0 p-0.5 cursor-pointer hover:bg-muted/30 transition-colors"
					onclick={() => { selectedDate = d; onSelectSlot?.(d, hour); }}
					role="button"
					tabindex="0"
					onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { selectedDate = d; onSelectSlot?.(d, hour); } }}
				>
					{#each slotEvs as ev}
						<button
							type="button"
							class="mb-0.5 block w-full truncate rounded-[3px] px-1 py-0.5 text-[10px] font-medium text-white transition-opacity hover:opacity-85"
							style="background-color:{eventDot(ev)}"
							onclick={(e) => { e.stopPropagation(); onSelectEvent?.(ev); }}
						>
							{ev.title}
						</button>
					{/each}
				</div>
						{/each}
					</div>
				{/each}
			</div>
		</div>
	{/if}

	<!-- ═══════════════════════════════════════════════════════════════════════
	     DAY VIEW
	     ═══════════════════════════════════════════════════════════════════════ -->
	{#if viewMode === 'day'}
		<!-- Day summary bar -->
		<div class="flex items-center justify-between border-b border-border bg-muted/20 px-4 py-2">
			<span class="text-sm font-medium text-foreground">
				{new Date(dayDate.year, dayDate.month - 1, dayDate.day).toLocaleDateString('en-US', { weekday: 'long', month: 'long', day: 'numeric' })}
			</span>
			<span class="rounded-full bg-primary/10 px-2.5 py-0.5 text-xs font-semibold text-primary">
				{dayEvents.length} event{dayEvents.length !== 1 ? 's' : ''}
			</span>
		</div>
		<div class="flex-1 overflow-y-auto scrollbar-none">
			{#each displayHours as hour}
				{@const slotEvs = dayEvents.filter((e) => {
					if (e.isAllDay) return hour === 0;
					const start = new Date(e.startUtc);
					return start.getHours() === hour || (start.getHours() < hour && new Date(e.endUtc).getHours() > hour);
				})}
				<div
					class={cn(
						'flex w-full border-b border-border text-left transition-colors last:border-b-0',
						slotEvs.length > 0 ? 'hover:bg-muted/20' : 'hover:bg-muted/10'
					)}
					onclick={() => onSelectSlot?.(dayDate, hour)}
					role="button"
					tabindex="0"
					onkeydown={(e) => { if (e.key === 'Enter' || e.key === ' ') { onSelectSlot?.(dayDate, hour); } }}
				>
					<!-- Time gutter -->
					<div class="w-14 shrink-0 border-r border-border py-2 pr-2 text-right text-[10px] text-muted-foreground">
						{hour === 0 ? '12am' : hour < 12 ? `${hour}am` : hour === 12 ? '12pm' : `${hour - 12}pm`}
					</div>
					<!-- Events -->
					<div class={cn('flex-1 p-1', slotEvs.length === 0 && 'min-h-[40px]')}>
						{#each slotEvs as ev}
							<button
								type="button"
								class="mb-1 flex w-full items-start gap-2 rounded-lg border bg-card px-3 py-2 text-left shadow-sm transition-all hover:shadow-md active:scale-[0.99]"
								style="border-left: 3px solid {eventDot(ev)}"
								onclick={(e) => { e.stopPropagation(); onSelectEvent?.(ev); }}
							>
								<div class="min-w-0 flex-1">
									<p class="truncate text-sm font-semibold text-foreground">{ev.title}</p>
									<p class="text-xs text-muted-foreground">
										{ev.isAllDay ? 'All day' : `${formatTime(ev.startUtc)} · ${formatDuration(ev.startUtc, ev.endUtc)}`}
										{#if ev.location}<span class="ml-1">· {ev.location}</span>{/if}
									</p>
								</div>
								{#if ev.eventTypeName}
									<span
										class="mt-0.5 shrink-0 rounded-full px-1.5 py-0.5 text-[10px] font-semibold text-white"
										style="background-color:{eventDot(ev)}"
									>{ev.eventTypeName}</span>
								{/if}
							</button>
						{/each}
					</div>
				</div>
			{/each}
		</div>
	{/if}

	<!-- ═══════════════════════════════════════════════════════════════════════
	     AGENDA VIEW
	     ═══════════════════════════════════════════════════════════════════════ -->
	{#if viewMode === 'agenda'}
		<div class="flex-1 overflow-y-auto scrollbar-none">
			{#if agendaGrouped.length === 0}
				<div class="flex flex-col items-center justify-center py-16 text-center" in:fade={{ duration: 300 }}>
					<div class="mb-3 flex size-14 items-center justify-center rounded-full bg-muted text-muted-foreground">
						<Icon name="calendar-off" class="size-7" />
					</div>
					<p class="text-sm font-medium text-foreground">No events yet</p>
					<p class="mt-1 text-xs text-muted-foreground">Events you create will appear here</p>
					{#if onAddEvent}
						<button
							type="button"
							onclick={() => onAddEvent?.()}
							class="mt-4 flex items-center gap-1.5 rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground transition-all hover:opacity-90"
						>
							<Icon name="plus" class="size-3.5" />
							New Event
						</button>
					{/if}
				</div>
			{:else}
				{#each agendaGrouped as group}
					<!-- Date separator -->
					<div class="sticky top-0 z-10 flex items-center gap-3 bg-card/80 px-4 py-2 backdrop-blur-sm">
						<div class={cn(
							'flex size-9 shrink-0 flex-col items-center justify-center rounded-xl',
							group.date.toDateString() === new Date().toDateString()
								? 'bg-primary text-primary-foreground'
								: 'bg-muted text-foreground'
						)}>
							<span class="text-[9px] font-bold uppercase">{group.dateLabel.split(',')[0]}</span>
							<span class="text-base font-black leading-none">{group.date.getDate()}</span>
						</div>
						<div>
							<p class="text-xs font-semibold text-foreground">{group.dateLabel.split(',').slice(1).join(',').trim()}</p>
							<p class="text-[10px] text-muted-foreground">{group.events.length} event{group.events.length !== 1 ? 's' : ''}</p>
						</div>
					</div>

					<!-- Events for this date -->
					<div class="divide-y divide-border px-3 pb-2">
						{#each group.events as ev}
							<button
								type="button"
								class="flex w-full items-center gap-3 py-2.5 text-left transition-colors hover:bg-muted/30 active:bg-muted/50"
								onclick={() => onSelectEvent?.(ev)}
								in:fly={{ x: -12, duration: 250, easing: cubicOut }}
							>
								<!-- Color bar -->
								<div
									class="h-10 w-0.5 shrink-0 rounded-full"
									style="background-color:{eventDot(ev)}"
								></div>

								<!-- Time -->
								<div class="w-14 shrink-0 text-right">
									{#if ev.isAllDay}
										<span class="text-[10px] font-semibold uppercase text-muted-foreground">All day</span>
									{:else}
										<p class="text-xs font-semibold text-foreground">{formatTime(ev.startUtc)}</p>
										<p class="text-[10px] text-muted-foreground">{formatDuration(ev.startUtc, ev.endUtc)}</p>
									{/if}
								</div>

								<!-- Details -->
								<div class="min-w-0 flex-1">
									<p class="truncate text-sm font-semibold text-foreground">{ev.title}</p>
									{#if ev.location}
										<p class="flex items-center gap-1 truncate text-xs text-muted-foreground">
											<Icon name="map-pin" class="size-3 shrink-0" />
											{ev.location}
										</p>
									{/if}
								</div>

								<!-- Type badge -->
								{#if ev.eventTypeName}
									<span
										class="hidden shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold text-white sm:inline"
										style="background-color:{eventDot(ev)}"
									>{ev.eventTypeName}</span>
								{/if}

								<Icon name="chevron-right" class="size-4 shrink-0 text-muted-foreground/40" />
							</button>
						{/each}
					</div>
				{/each}
			{/if}
		</div>
	{/if}
</div>
