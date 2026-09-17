<script lang="ts">
	import { Icon } from '$lib/components/venUI/icon';

	type Props = {
		lastCallDate?: string | null;
		outcome?: string | null;
		variant?: 'avatar' | 'pill' | 'banner' | 'icon';
		class?: string;
	};

	let {
		lastCallDate,
		outcome,
		variant = 'pill',
		class: customClass = ''
	}: Props = $props();

	function formatExactDate(dateStr: string): string {
		try {
			let normalized = dateStr;
			if (!normalized.endsWith('Z') && !normalized.includes('+') && !/-\d{2}:\d{2}$/.test(normalized)) {
				normalized += 'Z';
			}
			const d = new Date(normalized);
			return d.toLocaleString('en-IN', {
				day: '2-digit',
				month: 'short',
				year: 'numeric',
				hour: '2-digit',
				minute: '2-digit',
				hour12: true
			});
		} catch {
			return dateStr;
		}
	}

	type StatusConfig = {
		category: 'positive' | 'followup' | 'unreachable' | 'negative' | 'connected' | 'fresh' | 'overdue';
		icon: string;
		label: string;
		relativeTime: string;
		isRecent7Days: boolean;
		tooltip: string;
		// Color styles
		avatarBg: string;
		avatarBorder: string;
		avatarText: string;
		pillBg: string;
		pillBorder: string;
		pillText: string;
		bannerBg: string;
		bannerBorder: string;
		badgeDot: string;
	};

	const status = $derived.by<StatusConfig>(() => {
		if (!lastCallDate) {
			return {
				category: 'fresh',
				icon: 'sparkles',
				label: 'New Lead',
				relativeTime: 'Not called yet',
				isRecent7Days: false,
				tooltip: 'Fresh contact — Never called yet',
				avatarBg: 'bg-slate-100/90 dark:bg-slate-800/80 group-hover:bg-slate-200/80 transition-colors',
				avatarBorder: 'border-slate-200/90 dark:border-slate-700/80',
				avatarText: 'text-slate-400 dark:text-slate-500',
				pillBg: 'bg-slate-100/80 dark:bg-slate-800/60',
				pillBorder: 'border-slate-200/80 dark:border-slate-700/60',
				pillText: 'text-slate-600 dark:text-slate-400',
				bannerBg: 'bg-slate-50 dark:bg-slate-900/40',
				bannerBorder: 'border-slate-200 dark:border-slate-800',
				badgeDot: 'bg-slate-400'
			};
		}

		let normalized = lastCallDate;
		if (!normalized.endsWith('Z') && !normalized.includes('+') && !/-\d{2}:\d{2}$/.test(normalized)) {
			normalized += 'Z';
		}
		const callTime = new Date(normalized).getTime();
		const nowTime = Date.now();
		const diffMs = Math.max(0, nowTime - callTime);
		const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
		const exactFormatted = formatExactDate(lastCallDate);

		// Compute friendly relative time
		let relTime = '';
		const callDateObj = new Date(normalized);
		const nowDateObj = new Date();
		const isToday = callDateObj.toDateString() === nowDateObj.toDateString();
		const yesterdayObj = new Date(nowDateObj);
		yesterdayObj.setDate(yesterdayObj.getDate() - 1);
		const isYesterday = callDateObj.toDateString() === yesterdayObj.toDateString();

		if (isToday) {
			relTime = 'Today';
		} else if (isYesterday) {
			relTime = 'Yesterday';
		} else if (diffDays <= 7) {
			relTime = `${diffDays}d ago`;
		} else {
			relTime = `${diffDays}d ago`;
		}

		// Check past 7 days window (7 days = 7 * 24 * 60 * 60 * 1000 ms)
		const isRecent7Days = diffMs <= 7 * 24 * 60 * 60 * 1000;

		if (!isRecent7Days) {
			return {
				category: 'overdue',
				icon: 'history',
				label: outcome || 'Due for Call',
				relativeTime: `Called ${relTime}`,
				isRecent7Days: false,
				tooltip: `Last called on ${exactFormatted} (> 7 days ago). Due for another call.`,
				avatarBg: 'bg-amber-50 dark:bg-amber-950/30 group-hover:bg-amber-100/70 transition-colors',
				avatarBorder: 'border-amber-200/80 dark:border-amber-800/40',
				avatarText: 'text-amber-600 dark:text-amber-400',
				pillBg: 'bg-amber-500/10 dark:bg-amber-950/30',
				pillBorder: 'border-amber-500/20 dark:border-amber-800/40',
				pillText: 'text-amber-700 dark:text-amber-400',
				bannerBg: 'bg-amber-50/70 dark:bg-amber-950/20',
				bannerBorder: 'border-amber-200 dark:border-amber-900/40',
				badgeDot: 'bg-amber-500'
			};
		}

		// Within past 7 days: evaluate outcome category
		const normOutcome = (outcome || '').toLowerCase();

		// 1. Follow-up Required / Callback / Busy / Reminder
		if (
			normOutcome.includes('follow') ||
			normOutcome.includes('callback') ||
			normOutcome.includes('busy') ||
			normOutcome.includes('reminder') ||
			normOutcome.includes('reschedule')
		) {
			return {
				category: 'followup',
				icon: 'calendar-clock',
				label: outcome || 'Follow-Up Required',
				relativeTime: relTime,
				isRecent7Days: true,
				tooltip: `Follow-Up Required • Called ${relTime} (${exactFormatted})`,
				avatarBg: 'bg-gradient-to-br from-purple-50 to-indigo-50 dark:from-purple-950/40 dark:to-indigo-950/30 group-hover:from-purple-100 group-hover:to-indigo-100 transition-colors',
				avatarBorder: 'border-purple-200/80 dark:border-purple-800/50 shadow-2xs',
				avatarText: 'text-purple-600 dark:text-purple-400',
				pillBg: 'bg-purple-500/10 dark:bg-purple-950/40',
				pillBorder: 'border-purple-500/25 dark:border-purple-800/50',
				pillText: 'text-purple-700 dark:text-purple-300',
				bannerBg: 'bg-purple-50/80 dark:bg-purple-950/30',
				bannerBorder: 'border-purple-200 dark:border-purple-800/60',
				badgeDot: 'bg-purple-500'
			};
		}

		// 2. Positive / Interested / Order / Sale / Won
		if (
			!normOutcome.includes('not interested') &&
			(normOutcome.includes('interested') ||
				normOutcome.includes('sale') ||
				normOutcome.includes('order') ||
				normOutcome.includes('won') ||
				normOutcome.includes('completed') ||
				normOutcome.includes('converted'))
		) {
			return {
				category: 'positive',
				icon: 'circle-check',
				label: outcome || 'Interested / Converted',
				relativeTime: relTime,
				isRecent7Days: true,
				tooltip: `Positive Result: ${outcome} • Called ${relTime} (${exactFormatted})`,
				avatarBg: 'bg-gradient-to-br from-emerald-50 to-teal-50 dark:from-emerald-950/40 dark:to-teal-950/30 group-hover:from-emerald-100 group-hover:to-teal-100 transition-colors',
				avatarBorder: 'border-emerald-200/80 dark:border-emerald-800/50 shadow-2xs',
				avatarText: 'text-emerald-600 dark:text-emerald-400',
				pillBg: 'bg-emerald-500/10 dark:bg-emerald-950/40',
				pillBorder: 'border-emerald-500/25 dark:border-emerald-800/50',
				pillText: 'text-emerald-700 dark:text-emerald-300',
				bannerBg: 'bg-emerald-50/80 dark:bg-emerald-950/30',
				bannerBorder: 'border-emerald-200 dark:border-emerald-800/60',
				badgeDot: 'bg-emerald-500'
			};
		}

		// 3. Unreachable / No Answer / Ringing / Switched Off
		if (
			normOutcome.includes('unreachable') ||
			normOutcome.includes('no answer') ||
			normOutcome.includes('ringing') ||
			normOutcome.includes('switched off') ||
			normOutcome.includes('not reachable') ||
			normOutcome.includes('missed')
		) {
			return {
				category: 'unreachable',
				icon: 'phone-missed',
				label: outcome || 'Unreachable',
				relativeTime: relTime,
				isRecent7Days: true,
				tooltip: `Unreachable: ${outcome} • Called ${relTime} (${exactFormatted})`,
				avatarBg: 'bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-950/40 dark:to-orange-950/30 group-hover:from-amber-100 group-hover:to-orange-100 transition-colors',
				avatarBorder: 'border-amber-200/80 dark:border-amber-800/50 shadow-2xs',
				avatarText: 'text-amber-600 dark:text-amber-400',
				pillBg: 'bg-amber-500/10 dark:bg-amber-950/40',
				pillBorder: 'border-amber-500/25 dark:border-amber-800/50',
				pillText: 'text-amber-700 dark:text-amber-400',
				bannerBg: 'bg-amber-50/80 dark:bg-amber-950/30',
				bannerBorder: 'border-amber-200 dark:border-amber-800/60',
				badgeDot: 'bg-amber-500'
			};
		}

		// 4. Negative / Not Interested / Wrong Number / Rejected / Lost
		if (
			normOutcome.includes('not interested') ||
			normOutcome.includes('wrong') ||
			normOutcome.includes('rejected') ||
			normOutcome.includes('lost') ||
			normOutcome.includes('no interest') ||
			normOutcome.includes('inactive')
		) {
			return {
				category: 'negative',
				icon: 'circle-x',
				label: outcome || 'Not Interested',
				relativeTime: relTime,
				isRecent7Days: true,
				tooltip: `Not Interested / Rejected: ${outcome} • Called ${relTime} (${exactFormatted})`,
				avatarBg: 'bg-gradient-to-br from-rose-50 to-red-50 dark:from-rose-950/40 dark:to-red-950/30 group-hover:from-rose-100 group-hover:to-red-100 transition-colors',
				avatarBorder: 'border-rose-200/80 dark:border-rose-800/50 shadow-2xs',
				avatarText: 'text-rose-600 dark:text-rose-400',
				pillBg: 'bg-rose-500/10 dark:bg-rose-950/40',
				pillBorder: 'border-rose-500/25 dark:border-rose-800/50',
				pillText: 'text-rose-700 dark:text-rose-300',
				bannerBg: 'bg-rose-50/80 dark:bg-rose-950/30',
				bannerBorder: 'border-rose-200 dark:border-rose-800/60',
				badgeDot: 'bg-rose-500'
			};
		}

		// 5. Default Connected / Answered / Spoken
		return {
			category: 'connected',
			icon: 'phone-call',
			label: outcome || 'Connected / Answered',
			relativeTime: relTime,
			isRecent7Days: true,
			tooltip: `Connected Call: ${outcome} • Called ${relTime} (${exactFormatted})`,
			avatarBg: 'bg-gradient-to-br from-sky-50 to-indigo-50 dark:from-sky-950/40 dark:to-indigo-950/30 group-hover:from-sky-100 group-hover:to-indigo-100 transition-colors',
			avatarBorder: 'border-sky-200/80 dark:border-sky-800/50 shadow-2xs',
			avatarText: 'text-sky-600 dark:text-sky-400',
			pillBg: 'bg-sky-500/10 dark:bg-sky-950/40',
			pillBorder: 'border-sky-500/25 dark:border-sky-800/50',
			pillText: 'text-sky-700 dark:text-sky-300',
			bannerBg: 'bg-sky-50/80 dark:bg-sky-950/30',
			bannerBorder: 'border-sky-200 dark:border-sky-800/60',
			badgeDot: 'bg-sky-500'
		};
	});
</script>

{#if variant === 'avatar'}
	<!-- Contact Avatar Symbol: circular emblem with status icon -->
	<div
		class="relative flex items-center justify-center size-9 rounded-xl border {status.avatarBorder} {status.avatarBg} {status.avatarText} shrink-0 shadow-2xs {customClass}"
		title={status.tooltip}
	>
		<Icon name={status.icon} class="size-4.5" />
		{#if status.isRecent7Days}
			<!-- Tiny status indicator dot in top-right corner -->
			<span class="absolute -top-1 -right-1 flex size-2.5">
				<span class="animate-ping absolute inline-flex h-full w-full rounded-full opacity-60 {status.badgeDot}"></span>
				<span class="relative inline-flex rounded-full size-2.5 {status.badgeDot} ring-2 ring-card"></span>
			</span>
		{/if}
	</div>
{:else if variant === 'pill'}
	<!-- Sleek Compact Pill Tag -->
	<span
		class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-md text-[11px] font-medium border {status.pillBorder} {status.pillBg} {status.pillText} max-w-full truncate shadow-2xs {customClass}"
		title={status.tooltip}
	>
		<Icon name={status.icon} class="size-3 shrink-0" />
		<span class="truncate font-semibold tracking-tight">{status.label}</span>
		{#if status.relativeTime}
			<span class="opacity-60 text-[10px] shrink-0 font-normal">• {status.relativeTime}</span>
		{/if}
	</span>
{:else if variant === 'banner'}
	<!-- Rich Informative Banner for Selected Contact Workspace -->
	<div
		class="flex items-center justify-between gap-3 p-3 rounded-xl border {status.bannerBorder} {status.bannerBg} shadow-2xs {customClass}"
	>
		<div class="flex items-center gap-2.5 min-w-0">
			<div class="flex items-center justify-center size-8 rounded-lg border {status.avatarBorder} {status.avatarBg} {status.avatarText} shrink-0">
				<Icon name={status.icon} class="size-4" />
			</div>
			<div class="min-w-0">
				<div class="flex items-center gap-2">
					<span class="text-xs font-bold uppercase tracking-wider {status.pillText}">{status.label}</span>
					{#if status.isRecent7Days}
						<span class="inline-flex items-center px-1.5 py-0.2 rounded text-[9px] font-semibold bg-primary/10 text-primary uppercase">
							Past 7 Days
						</span>
					{/if}
				</div>
				<p class="text-xs text-muted-foreground truncate">
					{#if lastCallDate}
						Last call: {formatExactDate(lastCallDate)} ({status.relativeTime})
					{:else}
						No calling activity logged yet
					{/if}
				</p>
			</div>
		</div>
	</div>
{:else}
	<!-- Minimal Icon variant with Tooltip -->
	<span
		class="inline-flex items-center justify-center size-6 rounded-md border {status.pillBorder} {status.pillBg} {status.avatarText} {customClass}"
		title={status.tooltip}
	>
		<Icon name={status.icon} class="size-3.5" />
	</span>
{/if}
