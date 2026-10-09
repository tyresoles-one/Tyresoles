<script lang="ts">
	import { onMount, untrack } from 'svelte';
	import { authStore } from '$lib/stores/auth';
	import { Icon } from '$lib/components/venUI/icon';
	import { Button } from '$lib/components/ui/button';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation, buildMutation, buildQuery } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
	import {
		GetAllCrmCallLogsDocument,
		GetCrmAgentSummaryReportDocument,
		type DetailedCallLog,
		type CrmAgentSummary
	} from '../queries';

	let {
		filterCallDate = $bindable('pending'),
		onSelectContactById,
		salesUsersList = []
	}: {
		filterCallDate?: string;
		onSelectContactById?: (contactId: string) => void;
		salesUsersList?: { userName: string; fullName: string }[];
	} = $props();

	type KpiFilter = 'all' | 'connected' | 'positive' | 'followup' | null;
	let activeKpiFilter = $state<KpiFilter>(null);
	let showRecordPanel = $state(false);

	function toggleKpiFilter(filter: 'all' | 'connected' | 'positive' | 'followup') {
		if (activeKpiFilter === filter && showRecordPanel) {
			activeKpiFilter = null;
			showRecordPanel = false;
			filterCallDate = 'pending';
		} else {
			activeKpiFilter = filter;
			showRecordPanel = true;
			if (filter === 'all') filterCallDate = 'recent_7d';
			else if (filter === 'connected') filterCallDate = 'connected';
			else if (filter === 'positive') filterCallDate = 'positive';
			else if (filter === 'followup') filterCallDate = 'followup';
		}
	}

	function setFilter(filter: 'all' | 'connected' | 'positive' | 'followup') {
		activeKpiFilter = filter;
		showRecordPanel = true;
		if (filter === 'all') filterCallDate = 'recent_7d';
		else if (filter === 'connected') filterCallDate = 'connected';
		else if (filter === 'positive') filterCallDate = 'positive';
		else if (filter === 'followup') filterCallDate = 'followup';
	}

	$effect(() => {
		const current = filterCallDate;
		untrack(() => {
			if (current === 'recent_7d') activeKpiFilter = 'all';
			else if (current === 'connected') activeKpiFilter = 'connected';
			else if (current === 'positive') activeKpiFilter = 'positive';
			else if (current === 'followup') activeKpiFilter = 'followup';
			else if (current === 'today' || current === 'pending' || current === 'all' || current === 'not_called_7d') {
				activeKpiFilter = null;
			}
		});
	});

	type Timeframe = '7d' | 'today' | 'yesterday' | 'month' | 'custom';

	const SETTING_KEY = 'CRM_DAILY_CALL_TARGETS';

	const GetCrmSettingDocument = buildQuery`
		query GetCrmSetting($key: String!) {
			getCrmSetting(key: $key) {
				key
				value
			}
		}
	` as unknown as TypedDocumentNode<{ getCrmSetting: { key: string; value: string } | null }, { key: string }>;

	const SaveCrmSettingDocument = buildMutation`
		mutation SaveCrmSetting($key: String!, $value: String!, $description: String) {
			saveCrmSetting(key: $key, value: $value, description: $description) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<{ saveCrmSetting: { success: boolean; message: string } }, { key: string; value: string; description?: string }>;

	type CallTargetRule = {
		id?: string;
		agentUsername: string;
		dailyTarget: number;
		weeklyTarget: number;
		monthlyTarget: number;
		isActive?: boolean;
		notes?: string | null;
	};

	let isCollapsed = $state<boolean>(true);
	let timeframe = $state<Timeframe>('7d');
	let customStartDate = $state<string>(new Date().toISOString().slice(0, 10));
	let customEndDate = $state<string>(new Date().toISOString().slice(0, 10));
	let isCustomDateOpen = $state<boolean>(false);
	let loading = $state<boolean>(false);
	let callLogs = $state<DetailedCallLog[]>([]);
	let allTargets = $state<CallTargetRule[]>([]);

	// Real targets loaded from database
	let dailyTarget = $state<number>(30);
	let weeklyTarget = $state<number>(150);
	let monthlyTarget = $state<number>(600);

	let isEditingTarget = $state<boolean>(false);
	let tempTargetInput = $state<number>(150);

	// Tooltip state for chart
	let hoveredBar = $state<{
		label: string;
		subLabel: string;
		dateStr: string;
		total: number;
		positive: number;
		connected: number;
		unreachable: number;
	} | null>(null);

	function cleanUsername(raw?: string | null): string {
		if (!raw) return '';
		return raw
			.replace(/^(tyresoles[\\/]|domain[\\/])/i, '')
			.replace(/^[\\/]+/, '')
			.trim();
	}

	function toLocalDateStr(d: Date | string | null | undefined): string {
		if (!d) return '';
		const date = typeof d === 'string' ? new Date(d) : d;
		if (isNaN(date.getTime())) return '';
		const y = date.getFullYear();
		const m = String(date.getMonth() + 1).padStart(2, '0');
		const day = String(date.getDate()).padStart(2, '0');
		return `${y}-${m}-${day}`;
	}

	type DateRangeResult = {
		start: Date;
		end: Date;
		label: string;
		compactLabel: string;
		fullLabel: string;
	};

	function getRange(tf: Timeframe, customStart?: string, customEnd?: string): DateRangeResult {
		const now = new Date();
		if (tf === 'today') {
			const start = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 0, 0, 0, 0);
			const end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
			const dateStr = now.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
			const compact = now.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			return {
				start,
				end,
				label: `Today (${dateStr})`,
				compactLabel: compact,
				fullLabel: `Today (${dateStr})`
			};
		} else if (tf === 'yesterday') {
			const yDate = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 1);
			const start = new Date(yDate.getFullYear(), yDate.getMonth(), yDate.getDate(), 0, 0, 0, 0);
			const end = new Date(yDate.getFullYear(), yDate.getMonth(), yDate.getDate(), 23, 59, 59, 999);
			const dateStr = yDate.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });
			const compact = yDate.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			return {
				start,
				end,
				label: `Yesterday (${dateStr})`,
				compactLabel: compact,
				fullLabel: `Yesterday (${dateStr})`
			};
		} else if (tf === '7d') {
			const start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6, 0, 0, 0, 0);
			const end = new Date(now.getFullYear(), now.getMonth(), now.getDate(), 23, 59, 59, 999);
			const startShort = start.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			const endShort = end.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			return {
				start,
				end,
				label: `Past 7 Days (${startShort} – ${endShort})`,
				compactLabel: `${startShort} – ${endShort}`,
				fullLabel: `Past 7 Days (${startShort} – ${endShort})`
			};
		} else if (tf === 'month') {
			const start = new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0);
			const end = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59, 999);
			const monthLong = start.toLocaleDateString('en-IN', { month: 'long', year: 'numeric' });
			const monthCompact = start.toLocaleDateString('en-IN', { month: 'short', year: 'numeric' });
			return {
				start,
				end,
				label: `This Month (${monthLong})`,
				compactLabel: monthCompact,
				fullLabel: `This Month (${monthLong})`
			};
		} else {
			// Custom
			const sStr = customStart || now.toISOString().slice(0, 10);
			const eStr = customEnd || sStr;
			const [sY, sM, sD] = sStr.split('-').map(Number);
			const [eY, eM, eD] = eStr.split('-').map(Number);
			const start = new Date(sY, sM - 1, sD, 0, 0, 0, 0);
			const end = new Date(eY, eM - 1, eD, 23, 59, 59, 999);
			const isSame = sStr === eStr;
			const sShort = start.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			const eShort = end.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' });
			const compact = isSame ? sShort : `${sShort} – ${eShort}`;
			return {
				start,
				end,
				label: isSame ? `Custom Date (${sShort})` : `Custom Range (${sShort} – ${eShort})`,
				compactLabel: compact,
				fullLabel: isSame
					? `Custom Date: ${start.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })}`
					: `Custom Range: ${start.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })} – ${end.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })}`
			};
		}
	}

	let activeDateRange = $derived(getRange(timeframe, customStartDate, customEndDate));

	let periodDays = $derived.by(() => {
		if (timeframe === 'today' || timeframe === 'yesterday') return 1;
		if (timeframe === '7d') return 7;
		const now = new Date();
		const start = activeDateRange.start;
		const effectiveEnd = activeDateRange.end > now ? now : activeDateRange.end;
		
		const s = new Date(start.getFullYear(), start.getMonth(), start.getDate());
		const e = new Date(effectiveEnd.getFullYear(), effectiveEnd.getMonth(), effectiveEnd.getDate());
		const days = Math.max(1, Math.round((e.getTime() - s.getTime()) / (1000 * 60 * 60 * 24)) + 1);
		return days;
	});

	let currentTarget = $derived.by(() => {
		if (timeframe === 'today' || timeframe === 'yesterday') return dailyTarget;
		if (timeframe === '7d') return weeklyTarget;
		if (timeframe === 'month') return monthlyTarget;
		const diffDays = Math.max(1, Math.round((activeDateRange.end.getTime() - activeDateRange.start.getTime()) / (1000 * 60 * 60 * 24)));
		if (diffDays <= 1) return dailyTarget;
		if (diffDays <= 7) return weeklyTarget;
		return monthlyTarget;
	});

	// Load target configuration from CRM_DAILY_CALL_TARGETS table in database
	async function loadDatabaseTargets() {
		try {
			const res = await graphqlQuery<{ getCrmSetting: { key: string; value: string } | null }>(GetCrmSettingDocument, {
				variables: { key: SETTING_KEY }
			});
			if (res.success && res.data?.getCrmSetting?.value) {
				const parsed = JSON.parse(res.data.getCrmSetting.value);
				if (Array.isArray(parsed) && parsed.length > 0) {
					allTargets = parsed;

					const myUser = ($authStore.username || '').toLowerCase().trim();
					const cleanMyUser = cleanUsername(myUser).toLowerCase();

					// Search for specific agent rule
					const match = parsed.find((t: CallTargetRule) => {
						if (t.isActive === false) return false;
						const ag = (t.agentUsername || '').toLowerCase().trim();
						return ag === myUser || cleanUsername(ag).toLowerCase() === cleanMyUser;
					});

					// Fallback to DEFAULT rule
					const defaultRule = parsed.find((t: CallTargetRule) => {
						const ag = (t.agentUsername || '').toUpperCase();
						return ag === 'DEFAULT' || ag === 'GLOBAL_DEFAULT';
					});

					const activeRule = match || defaultRule;
					if (activeRule) {
						dailyTarget = activeRule.dailyTarget || 30;
						weeklyTarget = activeRule.weeklyTarget || 150;
						monthlyTarget = activeRule.monthlyTarget || 600;
					}
				}
			}
		} catch (err) {
			console.error('Failed to load database targets', err);
		}
	}

	// Fetch real call logs
	export async function loadData() {
		loading = true;
		try {
			const { start, end } = activeDateRange;
			const startIso = start.toISOString();
			const endIso = end.toISOString();

			const res = await graphqlQuery<any>(GetAllCrmCallLogsDocument, {
				variables: {
					take: 1000,
					where: {
						callDate: { gte: startIso, lte: endIso }
					},
					order: [{ callDate: 'DESC' }]
				}
			});

			if (res.success && res.data?.crmCallLogs?.items) {
				let allLogs = [...res.data.crmCallLogs.items];
				const total = res.data.crmCallLogs.totalCount || allLogs.length;

				// Guarantee 100% complete records: fetch remaining batches if records exceed 1000
				if (total > allLogs.length) {
					const batchSize = 1000;
					const pages = Math.ceil((total - allLogs.length) / batchSize);
					const promises = [];
					for (let p = 1; p <= pages; p++) {
						promises.push(
							graphqlQuery<any>(GetAllCrmCallLogsDocument, {
								variables: {
									skip: p * batchSize,
									take: batchSize,
									where: {
										callDate: { gte: startIso, lte: endIso }
									},
									order: [{ callDate: 'DESC' }]
								}
							})
						);
					}
					const results = await Promise.all(promises);
					for (const r of results) {
						if (r.success && r.data?.crmCallLogs?.items) {
							allLogs = allLogs.concat(r.data.crmCallLogs.items);
						}
					}
				}

				callLogs = allLogs;
			} else {
				callLogs = [];
			}
		} catch (err) {
			console.error('Failed to load calling stats', err);
			callLogs = [];
		} finally {
			loading = false;
		}
	}

	function isPositiveOutcome(outcomeStr?: string | null): boolean {
		if (!outcomeStr) return false;
		const norm = outcomeStr.toLowerCase();
		return (
			!norm.includes('not interested') &&
			(norm.includes('interested') ||
				norm.includes('sale') ||
				norm.includes('order') ||
				norm.includes('won') ||
				norm.includes('completed') ||
				norm.includes('converted') ||
				norm.includes('ready'))
		);
	}

	function isUnreachableOutcome(outcomeStr?: string | null): boolean {
		if (!outcomeStr) return false;
		const norm = outcomeStr.toLowerCase();
		return (
			norm.includes('unreachable') ||
			norm.includes('no answer') ||
			norm.includes('ringing') ||
			norm.includes('switched off') ||
			norm.includes('not reachable') ||
			norm.includes('missed') ||
			norm.includes('busy')
		);
	}

	function isFollowupOutcome(outcomeStr?: string | null): boolean {
		if (!outcomeStr) return false;
		const norm = outcomeStr.toLowerCase();
		return norm.includes('follow') || norm.includes('callback') || norm.includes('reminder') || norm.includes('reschedule');
	}

	// Current authenticated username
	let currentRawUsername = $derived(($authStore.username || '').trim());
	let currentCleanUsername = $derived(cleanUsername(currentRawUsername));

	// Personal statistics for current user
	let myLogs = $derived.by(() => {
		if (!currentCleanUsername) return [];
		return callLogs.filter((l) => {
			const creator = cleanUsername(l.createdBy).toLowerCase();
			return creator === currentCleanUsername.toLowerCase();
		});
	});

	let myTotalCalls = $derived(myLogs.length);

	let myPositiveCalls = $derived.by(() => {
		return myLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
	});

	let myPositiveRate = $derived(
		myTotalCalls > 0 ? ((myPositiveCalls / myTotalCalls) * 100).toFixed(1) : '0.0'
	);

	let myUnreachableCalls = $derived.by(() => {
		return myLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
	});

	let myConnectedCalls = $derived(Math.max(0, myTotalCalls - myUnreachableCalls));

	let myConnectRate = $derived(
		myTotalCalls > 0 ? ((myConnectedCalls / myTotalCalls) * 100).toFixed(1) : '0.0'
	);

	let myFollowupCalls = $derived.by(() => {
		return myLogs.filter((l) => isFollowupOutcome(l.outcome)).length;
	});

	let myUniqueContacts = $derived.by(() => {
		const set = new Set<string>();
		for (const l of myLogs) {
			if (l.contactId) set.add(l.contactId);
		}
		return set.size;
	});

	let filteredMyLogs = $derived.by(() => {
		if (!activeKpiFilter) return [];
		if (activeKpiFilter === 'all') return myLogs;
		if (activeKpiFilter === 'positive') return myLogs.filter((l) => isPositiveOutcome(l.outcome));
		if (activeKpiFilter === 'connected') return myLogs.filter((l) => !isUnreachableOutcome(l.outcome));
		if (activeKpiFilter === 'followup') return myLogs.filter((l) => isFollowupOutcome(l.outcome));
		return myLogs;
	});

	let attainmentPct = $derived(
		currentTarget > 0 ? Math.min(100, Math.round((myTotalCalls / currentTarget) * 100)) : 0
	);

	// Consecutive active calling days streak
	let streakDays = $derived.by(() => {
		if (myLogs.length === 0) return 0;
		const activeDates = new Set<string>();
		for (const l of myLogs) {
			if (l.callDate) {
				const d = new Date(l.callDate);
				activeDates.add(`${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`);
			}
		}
		let streak = 0;
		const checkDate = new Date();
		for (let i = 0; i < 30; i++) {
			const key = `${checkDate.getFullYear()}-${checkDate.getMonth() + 1}-${checkDate.getDate()}`;
			if (activeDates.has(key)) {
				streak++;
				checkDate.setDate(checkDate.getDate() - 1);
			} else {
				if (i === 0) {
					// Allow yesterday's streak to carry forward if no calls logged yet today
					checkDate.setDate(checkDate.getDate() - 1);
					continue;
				}
				break;
			}
		}
		return streak;
	});

	// Team analytics and leaderboard ranking - ONLY REAL ACTIVE AGENTS WITH > 0 CALLS
	type AgentRankItem = {
		rawUsername: string;
		displayUsername: string;
		calls: number;
		dailyAvg: number;
		activeDaysCount: number;
		positiveCount: number;
		positiveRate: number;
		connectedCount: number;
		isCurrentUser: boolean;
		rank: number;
	};

	let leaderboard = $derived.by(() => {
		const map = new Map<string, { raw: string; display: string; calls: number; positive: number; connected: number; dates: Set<string> }>();

		for (const log of callLogs) {
			const raw = log.createdBy?.trim() || '';
			if (!raw) continue;
			const clean = cleanUsername(raw);
			const norm = clean.toLowerCase();

			if (!map.has(norm)) {
				const userMatch = salesUsersList?.find(
					(u) => cleanUsername(u.userName).toLowerCase() === norm
				);
				const display = userMatch?.fullName?.trim() || clean;
				map.set(norm, { raw, display, calls: 0, positive: 0, connected: 0, dates: new Set<string>() });
			}
			const entry = map.get(norm)!;
			entry.calls++;
			if (isPositiveOutcome(log.outcome)) entry.positive++;
			if (!isUnreachableOutcome(log.outcome)) entry.connected++;
			if (log.callDate) {
				const dStr = toLocalDateStr(log.callDate);
				if (dStr) entry.dates.add(dStr);
			}
		}

		const days = periodDays;

		// Convert to list - only agents who actually made calls!
		const list: AgentRankItem[] = Array.from(map.entries())
			.filter(([_, data]) => data.calls > 0)
			.map(([norm, data]) => {
				const avg = days > 0 ? Math.round(data.calls / days) : data.calls;
				return {
					rawUsername: data.raw,
					displayUsername: data.display,
					calls: data.calls,
					dailyAvg: avg,
					activeDaysCount: data.dates.size,
					positiveCount: data.positive,
					positiveRate: data.calls > 0 ? (data.positive / data.calls) * 100 : 0,
					connectedCount: data.connected,
					isCurrentUser: norm === currentCleanUsername.toLowerCase(),
					rank: 1
				};
			});

		// Multi-tier deterministic sorting:
		// 1. Total Calls DESC
		// 2. Positive Leads Won DESC
		// 3. Connected Calls DESC
		// 4. Display Name ASC (stable deterministic tie-breaking)
		list.sort(
			(a, b) =>
				b.calls - a.calls ||
				b.positiveCount - a.positiveCount ||
				b.connectedCount - a.connectedCount ||
				a.displayUsername.localeCompare(b.displayUsername)
		);

		// 100% correct competition ranking: identical records share the exact same rank
		for (let i = 0; i < list.length; i++) {
			if (i === 0) {
				list[0].rank = 1;
			} else {
				const prev = list[i - 1];
				const curr = list[i];
				if (
					curr.calls === prev.calls &&
					curr.positiveCount === prev.positiveCount &&
					curr.connectedCount === prev.connectedCount
				) {
					curr.rank = prev.rank;
				} else {
					curr.rank = i + 1;
				}
			}
		}

		return list;
	});

	let teamTotalCalls = $derived(callLogs.length);
	let activeTeamRepsCount = $derived(leaderboard.length);
	let teamAvgCalls = $derived(activeTeamRepsCount > 0 ? Math.round(teamTotalCalls / activeTeamRepsCount) : 0);

	let teamAvgPositiveRate = $derived.by(() => {
		if (teamTotalCalls === 0) return 0;
		const totalPos = callLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
		return parseFloat(((totalPos / teamTotalCalls) * 100).toFixed(1));
	});

	let myDailyAvg = $derived(periodDays > 0 ? Math.round(myTotalCalls / periodDays) : 0);
	let teamDailyAvg = $derived(
		activeTeamRepsCount > 0 && periodDays > 0
			? Math.round(teamTotalCalls / activeTeamRepsCount / periodDays)
			: 0
	);

	let topPerformer = $derived<AgentRankItem | null>(
		leaderboard.length > 0 && leaderboard[0].calls > 0 ? leaderboard[0] : null
	);

	let myAgentItem = $derived(leaderboard.find((a) => a.isCurrentUser));
	let myRank = $derived.by(() => {
		if (myAgentItem) return myAgentItem.rank;
		if (myTotalCalls === 0) return null;
		return leaderboard.length + 1;
	});

	let isTiedForFirst = $derived.by(() => {
		return leaderboard.filter((a) => a.rank === 1).length > 1;
	});

	let gapToLeader = $derived.by(() => {
		if (!topPerformer) return 0;
		return Math.max(0, topPerformer.calls - myTotalCalls);
	});

	let leadOverSecond = $derived.by(() => {
		if (myRank === 1) {
			const nextRankAgent = leaderboard.find((a) => a.rank > 1);
			if (nextRankAgent) {
				return myTotalCalls - nextRankAgent.calls;
			}
		}
		return 0;
	});

	// Real dynamic summary message based on authentic data
	let summaryHeadline = $derived.by(() => {
		if (myTotalCalls > 0) {
			if (myRank === 1) {
				return {
					title: isTiedForFirst ? `Tied for #1 (${currentCleanUsername})` : `Team Leader (${currentCleanUsername})`,
					detail: isTiedForFirst
						? `Co-leading the team with ${myTotalCalls} calls and ${myPositiveCalls} positive outcomes.`
						: leadOverSecond > 0
						? `Leading the team by +${leadOverSecond} calls with ${myPositiveCalls} positive outcomes.`
						: `Ranked #1 on the team with ${myTotalCalls} calls in this period.`,
					badge: isTiedForFirst ? 'Rank #1 Co-Leader' : 'Rank #1 Champion',
					badgeClass: 'bg-amber-100 dark:bg-amber-950/70 text-amber-950 dark:text-amber-200 border-2 border-amber-400 dark:border-amber-500 font-extrabold shadow-2xs',
					bannerAccent: 'from-amber-500/15 via-yellow-500/10 to-amber-500/5 border-amber-500/30',
					icon: 'trophy',
					iconColor: 'bg-amber-400/20 text-amber-600 dark:text-amber-400 border-amber-400/40',
					rankEmoji: '👑'
				};
			}
			if (myRank === 2 && topPerformer) {
				return {
					title: `Rank #2 Contender (${currentCleanUsername})`,
					detail: `${myTotalCalls} calls logged (${gapToLeader} behind ${topPerformer.displayUsername}).`,
					badge: 'Rank #2 Contender',
					badgeClass: 'bg-slate-100 dark:bg-slate-800 text-slate-800 dark:text-slate-200 border-2 border-slate-400 dark:border-slate-500 font-extrabold shadow-2xs',
					bannerAccent: 'from-slate-500/15 via-zinc-500/10 to-transparent border-slate-400/30',
					icon: 'medal',
					iconColor: 'bg-slate-300/30 text-slate-700 dark:text-slate-200 border-slate-400/40',
					rankEmoji: '🥈'
				};
			}
			if (myRank === 3) {
				return {
					title: `Top 3 Performer (${currentCleanUsername})`,
					detail: `Ranked #3 on team with ${myTotalCalls} calls logged • ${myPositiveCalls} leads won (${myPositiveRate}%).`,
					badge: 'Rank #3 on Team',
					badgeClass: 'bg-orange-50 dark:bg-orange-950/60 text-orange-950 dark:text-orange-200 border-2 border-orange-400 dark:border-orange-500 font-extrabold shadow-2xs',
					bannerAccent: 'from-orange-500/15 via-amber-500/10 to-orange-500/5 border-orange-500/30',
					icon: 'award',
					iconColor: 'bg-orange-500/20 text-orange-600 dark:text-orange-400 border-orange-400/40',
					rankEmoji: '🥉'
				};
			}
			return {
				title: `${myTotalCalls} Calls Logged (${currentCleanUsername})`,
				detail: `Ranked #${myRank} of ${activeTeamRepsCount} active callers • ${myPositiveCalls} leads won (${myPositiveRate}%).`,
				badge: `Rank #${myRank}`,
				badgeClass: 'bg-indigo-50 dark:bg-indigo-950/50 text-indigo-800 dark:text-indigo-300 border-2 border-indigo-300 dark:border-indigo-700 font-bold shadow-2xs',
				bannerAccent: 'from-primary/10 via-primary/5 to-muted/20 border-primary/20',
				icon: 'phone-call',
				iconColor: 'bg-primary/20 text-primary border-primary/30',
				rankEmoji: '🏅'
			};
		} else {
			return {
				title: `No Calls Logged Yet (${currentCleanUsername})`,
				detail: `Showing ${activeDateRange.label}. ${teamTotalCalls} total calls logged by the team.`,
				badge: 'Pending Activity',
				badgeClass: 'bg-muted text-muted-foreground border border-border font-medium',
				bannerAccent: 'from-muted/40 via-muted/20 to-transparent border-border',
				icon: 'clock',
				iconColor: 'bg-muted text-muted-foreground border-border',
				rankEmoji: '⏳'
			};
		}
	});

	// Daily / Interval Histogram for Interactive SVG Bar Chart
	type ChartBar = {
		label: string;
		subLabel: string;
		dateStr: string;
		isCurrent: boolean;
		total: number;
		positive: number;
		connected: number;
		unreachable: number;
	};

	let chartBars = $derived.by(() => {
		const { start, end } = activeDateRange;
		const bars: ChartBar[] = [];
		const todayKey = toLocalDateStr(new Date());

		if (timeframe === 'today') {
			// Single day: show today with total calls
			const total = myLogs.length;
			const positive = myLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = myLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, total - unreachable);

			bars.push({
				label: 'Today',
				subLabel: start.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }),
				dateStr: todayKey,
				isCurrent: true,
				total,
				positive,
				connected,
				unreachable
			});
		} else if (timeframe === 'yesterday') {
			// Single day: show yesterday with total calls
			const yesterdayKey = toLocalDateStr(start);
			const total = myLogs.length;
			const positive = myLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = myLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, total - unreachable);

			bars.push({
				label: 'Yesterday',
				subLabel: start.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }),
				dateStr: yesterdayKey,
				isCurrent: true,
				total,
				positive,
				connected,
				unreachable
			});
		} else if (timeframe === '7d') {
			// 7 rolling days from start to end
			const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
			for (let i = 0; i < 7; i++) {
				const d = new Date(start);
				d.setDate(start.getDate() + i);
				const dateStr = toLocalDateStr(d);
				const isCurrent = dateStr === todayKey;

				const dayLogs = myLogs.filter((l) => {
					if (!l.callDate) return false;
					return toLocalDateStr(l.callDate) === dateStr;
				});

				const total = dayLogs.length;
				const positive = dayLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
				const unreachable = dayLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
				const connected = Math.max(0, total - unreachable);

				bars.push({
					label: dayNames[d.getDay()],
					subLabel: `${d.getDate()} ${d.toLocaleDateString('en-IN', { month: 'short' })}`,
					dateStr,
					isCurrent,
					total,
					positive,
					connected,
					unreachable
				});
			}
		} else if (timeframe === 'month') {
			// Monthly: weekly blocks of the month
			const curr = new Date(start);
			let weekNum = 1;
			while (curr <= end) {
				const blockStart = new Date(curr);
				const blockEnd = new Date(curr);
				blockEnd.setDate(blockEnd.getDate() + 6);
				if (blockEnd > end) blockEnd.setTime(end.getTime());

				const blockStartStr = toLocalDateStr(blockStart);
				const blockEndStr = toLocalDateStr(blockEnd);
				const isCurrent = todayKey >= blockStartStr && todayKey <= blockEndStr;

				const blockLogs = myLogs.filter((l) => {
					if (!l.callDate) return false;
					const lDate = toLocalDateStr(l.callDate);
					return lDate >= blockStartStr && lDate <= blockEndStr;
				});

				const total = blockLogs.length;
				const positive = blockLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
				const unreachable = blockLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
				const connected = Math.max(0, total - unreachable);

				bars.push({
					label: `W${weekNum}`,
					subLabel: `${blockStart.getDate()}-${blockEnd.getDate()}`,
					dateStr: `${blockStartStr} to ${blockEndStr}`,
					isCurrent,
					total,
					positive,
					connected,
					unreachable
				});

				curr.setDate(curr.getDate() + 7);
				weekNum++;
			}
		} else {
			// Custom
			const diffDays = Math.max(1, Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)));
			if (diffDays <= 1) {
				const total = myLogs.length;
				const positive = myLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
				const unreachable = myLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
				const connected = Math.max(0, total - unreachable);

				bars.push({
					label: 'Custom',
					subLabel: start.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }),
					dateStr: toLocalDateStr(start),
					isCurrent: true,
					total,
					positive,
					connected,
					unreachable
				});
			} else if (diffDays <= 14) {
				const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
				for (let i = 0; i < diffDays; i++) {
					const d = new Date(start);
					d.setDate(start.getDate() + i);
					const dateStr = toLocalDateStr(d);
					const isCurrent = dateStr === todayKey;

					const dayLogs = myLogs.filter((l) => {
						if (!l.callDate) return false;
						return toLocalDateStr(l.callDate) === dateStr;
					});

					const total = dayLogs.length;
					const positive = dayLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
					const unreachable = dayLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
					const connected = Math.max(0, total - unreachable);

					bars.push({
						label: dayNames[d.getDay()],
						subLabel: `${d.getDate()} ${d.toLocaleDateString('en-IN', { month: 'short' })}`,
						dateStr,
						isCurrent,
						total,
						positive,
						connected,
						unreachable
					});
				}
			} else {
				const curr = new Date(start);
				let weekNum = 1;
				while (curr <= end) {
					const blockStart = new Date(curr);
					const blockEnd = new Date(curr);
					blockEnd.setDate(blockEnd.getDate() + 6);
					if (blockEnd > end) blockEnd.setTime(end.getTime());

					const blockStartStr = toLocalDateStr(blockStart);
					const blockEndStr = toLocalDateStr(blockEnd);
					const isCurrent = todayKey >= blockStartStr && todayKey <= blockEndStr;

					const blockLogs = myLogs.filter((l) => {
						if (!l.callDate) return false;
						const lDate = toLocalDateStr(l.callDate);
						return lDate >= blockStartStr && lDate <= blockEndStr;
					});

					const total = blockLogs.length;
					const positive = blockLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
					const unreachable = blockLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
					const connected = Math.max(0, total - unreachable);

					bars.push({
						label: `W${weekNum}`,
						subLabel: `${blockStart.getDate()}-${blockEnd.getDate()}`,
						dateStr: `${blockStartStr} to ${blockEndStr}`,
						isCurrent,
						total,
						positive,
						connected,
						unreachable
					});

					curr.setDate(curr.getDate() + 7);
					weekNum++;
				}
			}
		}

		return bars;
	});

	let maxBarVal = $derived.by(() => {
		const m = Math.max(...chartBars.map((b) => b.total), 5);
		return Math.ceil(m * 1.2);
	});

	onMount(async () => {
		try {
			const savedCollapsed = localStorage.getItem('crm_stats_collapsed_v2');
			if (savedCollapsed !== null) {
				isCollapsed = savedCollapsed === 'true';
			} else {
				isCollapsed = true;
			}

			const savedTf = localStorage.getItem('crm_stats_timeframe') as Timeframe;
			if (savedTf && ['7d', 'today', 'yesterday', 'month', 'custom'].includes(savedTf)) {
				timeframe = savedTf;
			}

			const savedStart = localStorage.getItem('crm_stats_custom_start');
			const savedEnd = localStorage.getItem('crm_stats_custom_end');
			if (savedStart) customStartDate = savedStart;
			if (savedEnd) customEndDate = savedEnd;
		} catch (e) {
			console.error(e);
		}

		await Promise.all([loadDatabaseTargets(), loadData()]);
	});

	function toggleCollapse() {
		isCollapsed = !isCollapsed;
		try {
			localStorage.setItem('crm_stats_collapsed_v2', String(isCollapsed));
		} catch (e) {
			console.error(e);
		}
	}

	function handleTimeframeChange(tf: Timeframe) {
		timeframe = tf;
		try {
			localStorage.setItem('crm_stats_timeframe', tf);
		} catch (e) {
			console.error(e);
		}
		loadData();
	}

	function applyCustomDate() {
		timeframe = 'custom';
		isCustomDateOpen = false;
		try {
			localStorage.setItem('crm_stats_timeframe', 'custom');
			localStorage.setItem('crm_stats_custom_start', customStartDate);
			localStorage.setItem('crm_stats_custom_end', customEndDate);
		} catch (e) {
			console.error(e);
		}
		loadData();
	}

	function startEditTarget() {
		tempTargetInput = currentTarget;
		isEditingTarget = true;
	}

	async function saveTarget() {
		if (tempTargetInput > 0) {
			if (timeframe === 'today' || timeframe === 'yesterday') {
				dailyTarget = tempTargetInput;
			} else if (timeframe === '7d') {
				weeklyTarget = tempTargetInput;
			} else {
				monthlyTarget = tempTargetInput;
			}

			// Update setting in database
			try {
				const myUser = currentRawUsername || 'DEFAULT';
				let updated = [...allTargets];
				const idx = updated.findIndex((t) => (t.agentUsername || '').toLowerCase() === myUser.toLowerCase());
				if (idx !== -1) {
					updated[idx] = {
						...updated[idx],
						dailyTarget,
						weeklyTarget,
						monthlyTarget
					};
				} else {
					updated.push({
						agentUsername: myUser,
						dailyTarget,
						weeklyTarget,
						monthlyTarget,
						isActive: true,
						notes: 'Agent target updated from calling bar'
					});
				}
				allTargets = updated;
				await graphqlMutation(SaveCrmSettingDocument, {
					variables: {
						key: SETTING_KEY,
						value: JSON.stringify(updated),
						description: 'Configured Daily, Weekly, and Monthly call targets per agent and default'
					}
				});
				toast.success('Target updated successfully in database');
			} catch (err) {
				console.error('Failed to sync target to database', err);
			}
		}
		isEditingTarget = false;
	}

	function cancelEditTarget() {
		isEditingTarget = false;
	}
</script>

<!-- Top Performance Header Bar Container -->
<div class="border-b border-border bg-background/95 backdrop-blur-md sticky top-0 z-30 transition-all duration-200 shadow-2xs">
	<!-- Collapsed Glanceable Mode (Ultra-Compact Strip) -->
	<div class="px-3 md:px-5 py-1.5 flex items-center justify-between gap-3 text-xs">
		<!-- Left: Rank Badge + All 3 Stats -->
		<div class="flex items-center gap-2 md:gap-3 flex-wrap min-w-0">
			<!-- 1. Standing Rank Badge -->
			<div class="flex items-center gap-1.5 px-2.5 py-1 rounded-full {summaryHeadline.badgeClass} text-[11px] shadow-2xs shrink-0 select-none">
				<span>{summaryHeadline.rankEmoji}</span>
				<span>{summaryHeadline.badge}</span>
			</div>

			<!-- 2. Stat 1: Volume & Target Progress Meter -->
			<div class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg transition-all {activeKpiFilter === 'all' && showRecordPanel ? 'bg-sky-50 dark:bg-sky-950/60 border-2 border-sky-500 shadow-xs' : 'bg-muted/20 hover:bg-muted/30 border border-border/50'} shrink-0">
				<button
					type="button"
					onclick={() => toggleKpiFilter('all')}
					class="flex items-center gap-1 cursor-pointer"
					title="Click to view/filter all call records"
				>
					<Icon name="phone" class="size-3.5 text-primary" />
					<span class="font-semibold text-foreground text-[12px]">{myTotalCalls}</span>
				</button>
				{#if isEditingTarget}
					<div class="inline-flex items-center gap-1">
						<span class="text-muted-foreground text-[11px]">/</span>
						<input
							type="number"
							min="1"
							max="5000"
							bind:value={tempTargetInput}
							class="w-12 h-5 px-1 text-[11px] rounded bg-background border border-primary font-bold text-foreground outline-none text-right"
							onkeydown={(e) => { if (e.key === 'Enter') saveTarget(); if (e.key === 'Escape') cancelEditTarget(); }}
						/>
						<button type="button" onclick={saveTarget} class="size-4 text-emerald-600 hover:bg-emerald-50 rounded flex items-center justify-center cursor-pointer" title="Save target">
							<Icon name="check" class="size-3" />
						</button>
						<button type="button" onclick={cancelEditTarget} class="size-4 text-rose-600 hover:bg-rose-50 rounded flex items-center justify-center cursor-pointer" title="Cancel">
							<Icon name="x" class="size-3" />
						</button>
					</div>
				{:else}
					<button
						type="button"
						onclick={startEditTarget}
						class="text-muted-foreground hover:text-foreground text-[11px] inline-flex items-center gap-0.5 group cursor-pointer transition-colors"
						title="Click to edit call target"
					>
						<span>/ {currentTarget} calls</span>
						<Icon name="edit-3" class="size-2.5 opacity-40 group-hover:opacity-100 transition-opacity" />
					</button>
				{/if}
				
				<!-- Sky Blue Micro Progress Bar -->
				<div class="w-12 h-1.5 bg-muted rounded-full overflow-hidden ml-1 hidden sm:block">
					<div
						class="h-full rounded-full transition-all duration-500 bg-sky-500 dark:bg-sky-400"
						style="width: {attainmentPct}%"
					></div>
				</div>
				<span class="text-[10px] font-bold ml-0.5 text-sky-600 dark:text-sky-400">
					{attainmentPct}%
				</span>
			</div>

			<!-- 3. Stat 2: Positive Conversion Rate (Sky Blue Badge) -->
			<button
				type="button"
				onclick={() => toggleKpiFilter('positive')}
				class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg transition-all cursor-pointer {activeKpiFilter === 'positive' && showRecordPanel ? 'bg-sky-500 text-white font-bold shadow-xs ring-2 ring-sky-400' : 'bg-sky-50 dark:bg-sky-950/60 hover:bg-sky-100 dark:hover:bg-sky-900/60 text-sky-700 dark:text-sky-300 border border-sky-400/40 dark:border-sky-700/60 font-medium'}"
				title="Click to view and filter {myPositiveCalls} positive outcome calls"
			>
				<Icon name="check-circle-2" class="size-3.5 {activeKpiFilter === 'positive' && showRecordPanel ? 'text-white' : 'text-sky-600 dark:text-sky-400'}" />
				<span class="font-bold text-[11px]">{myPositiveRate}%</span>
				<span class="text-[10px] opacity-85 hidden sm:inline">Positive ({myPositiveCalls})</span>
			</button>

			<!-- 4. Stat 3: Reminders / Follow-up Indicator Badge -->
			<button
				type="button"
				onclick={() => toggleKpiFilter('followup')}
				class="flex items-center gap-1.5 px-2.5 py-1 rounded-lg transition-all cursor-pointer {activeKpiFilter === 'followup' && showRecordPanel ? 'bg-purple-600 text-white font-bold shadow-xs ring-2 ring-purple-400' : 'bg-purple-50 dark:bg-purple-950/60 hover:bg-purple-100 dark:hover:bg-purple-900/60 text-purple-700 dark:text-purple-300 border border-purple-300/60 dark:border-purple-800/60 font-medium'}"
				title="Click to view and filter {myFollowupCalls} follow-up reminders"
			>
				<Icon name="bell-ring" class="size-3.5 {activeKpiFilter === 'followup' && showRecordPanel ? 'text-white' : 'text-purple-600 dark:text-purple-400'}" />
				<span class="font-bold text-[11px]">{myFollowupCalls}</span>
				<span class="text-[10px] opacity-85 hidden sm:inline">Reminders</span>
			</button>
		</div>

		<!-- Right: Call Logs, Refresh, Icon-Only (Show Stats) -->
		<div class="flex items-center gap-1.5 shrink-0">
			<!-- Call Logs Button -->
			<Button
				variant="outline"
				size="sm"
				class="h-7 px-2.5 rounded-lg text-[11px] font-semibold flex items-center gap-1.5 transition-all {showRecordPanel ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-muted/20 border-border hover:bg-muted/40'}"
				onclick={() => {
					showRecordPanel = !showRecordPanel;
					if (showRecordPanel && !activeKpiFilter) setFilter('all');
				}}
				title="Toggle Call Logs Record Panel"
			>
				<Icon name="clipboard-list" class="size-3.5" />
				<span class="hidden sm:inline">{showRecordPanel ? 'Hide Logs' : 'Call Logs'}</span>
				{#if myTotalCalls > 0}
					<span class="px-1.5 py-0.2 rounded-full text-[10px] font-bold {showRecordPanel ? 'bg-background text-foreground' : 'bg-muted text-foreground'}">
						{activeKpiFilter ? filteredMyLogs.length : myTotalCalls}
					</span>
				{/if}
			</Button>

			<!-- Refresh Live Stats -->
			<Button
				variant="ghost"
				size="icon"
				class="size-7 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted"
				onclick={loadData}
				disabled={loading}
				title="Refresh live stats"
			>
				<Icon name="refresh-cw" class="size-3.5 {loading ? 'animate-spin text-primary' : ''}" />
			</Button>

			<!-- Icon Only: Show Stats & Trends Toggle -->
			<button
				type="button"
				class="size-7 rounded-lg bg-black text-white hover:bg-neutral-800 dark:bg-black dark:text-white dark:hover:bg-neutral-900 border border-black shadow-xs flex items-center justify-center transition-colors cursor-pointer"
				onclick={toggleCollapse}
				title={isCollapsed ? 'Show Stats & Trends' : 'Collapse stats'}
			>
				<Icon name={isCollapsed ? 'chevron-down' : 'chevron-up'} class="size-3.5 text-white transition-transform" />
			</button>
		</div>
	</div>

	<!-- Expanded Mode (Rich Performance Cockpit) -->
	{#if !isCollapsed}
		<div class="px-3 md:px-5 py-3 border-t border-border/60 bg-card/40 space-y-3 transition-all animate-in fade-in slide-in-from-top-1 duration-200">
			<!-- Call Logs Record Panel (Controlled by visibility toggle & top bar badges) -->
			{#if showRecordPanel}
				{@const filterTitle = activeKpiFilter === 'all'
					? 'All Logged Calls'
					: activeKpiFilter === 'connected'
					? 'Connected Calls'
					: activeKpiFilter === 'positive'
					? 'Positive Outcomes'
					: activeKpiFilter === 'followup'
					? 'Reminders & Follow-ups'
					: 'All Logged Calls'}

				<div class="p-4 rounded-xl bg-background border border-border/80 shadow-sm space-y-3 animate-in fade-in slide-in-from-top-1 duration-200">
					<!-- Header Toolbar -->
					<div class="flex items-center justify-between flex-wrap gap-2.5 border-b border-border/60 pb-3">
						<div class="flex items-center gap-2 flex-wrap">
							<div class="flex items-center gap-2 mr-2">
								<span class="size-2 rounded-full bg-primary animate-ping"></span>
								<span class="size-2 rounded-full bg-primary -ml-4"></span>
								<span class="font-bold text-xs text-foreground">
									Call Records: <span class="text-primary">{filterTitle}</span>
									<span class="text-muted-foreground font-normal">({filteredMyLogs.length} of {myTotalCalls})</span>
								</span>
							</div>

							<!-- Quick Filter Chips -->
							<div class="flex items-center bg-muted/40 p-0.5 rounded-lg border border-border/60 text-[11px] font-medium">
								<button
									type="button"
									onclick={() => setFilter('all')}
									class="px-2.5 py-0.5 rounded-md transition-all cursor-pointer {activeKpiFilter === 'all' || !activeKpiFilter ? 'bg-background text-foreground font-bold shadow-2xs' : 'text-muted-foreground hover:text-foreground'}"
								>
									All ({myTotalCalls})
								</button>
								<button
									type="button"
									onclick={() => setFilter('positive')}
									class="px-2.5 py-0.5 rounded-md transition-all flex items-center gap-1 cursor-pointer {activeKpiFilter === 'positive' ? 'bg-sky-500 text-white font-bold shadow-2xs' : 'text-sky-700 dark:text-sky-300 hover:text-foreground'}"
								>
									<Icon name="check-circle-2" class="size-3" />
									Positive ({myPositiveCalls})
								</button>
								<button
									type="button"
									onclick={() => setFilter('followup')}
									class="px-2.5 py-0.5 rounded-md transition-all flex items-center gap-1 cursor-pointer {activeKpiFilter === 'followup' ? 'bg-purple-600 text-white font-bold shadow-2xs' : 'text-purple-700 dark:text-purple-300 hover:text-foreground'}"
								>
									<Icon name="bell-ring" class="size-3" />
									Reminders ({myFollowupCalls})
								</button>
								<button
									type="button"
									onclick={() => setFilter('connected')}
									class="px-2.5 py-0.5 rounded-md transition-all cursor-pointer {activeKpiFilter === 'connected' ? 'bg-indigo-600 text-white font-bold shadow-2xs' : 'text-muted-foreground hover:text-foreground'}"
								>
									Connected ({myConnectedCalls})
								</button>
							</div>
						</div>

						<div class="flex items-center gap-2">
							<button
								type="button"
								onclick={() => (showRecordPanel = false)}
								class="text-[11px] font-semibold text-muted-foreground hover:text-foreground flex items-center gap-1 px-2.5 py-1 rounded-md hover:bg-muted bg-muted/40 border border-border/60 transition-colors cursor-pointer"
								title="Hide Call Records panel"
							>
								<Icon name="x" class="size-3" />
								<span>Hide Records</span>
							</button>
						</div>
					</div>

					<!-- Records List: Clean, Clear 2-Column Responsive Feed -->
					{#if loading}
						<div class="py-12 flex flex-col items-center justify-center text-xs text-muted-foreground gap-2">
							<Loader2 class="size-6 animate-spin text-primary" />
							<span>Loading call records...</span>
						</div>
					{:else if filteredMyLogs.length === 0}
						<div class="py-8 text-center text-xs text-muted-foreground">
							<Icon name="phone-off" class="size-6 mx-auto mb-1.5 opacity-40" />
							<p>No call logs found matching <strong>{filterTitle}</strong> for this timeframe ({activeDateRange.label}).</p>
						</div>
					{:else}
						<div class="grid grid-cols-1 lg:grid-cols-2 gap-2.5 max-h-72 overflow-y-auto pr-1">
							{#each filteredMyLogs as log}
								<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2.5 p-3 rounded-xl border border-border/70 bg-card hover:bg-muted/30 hover:border-primary/40 transition-all group shadow-2xs">
									<div class="flex items-start gap-2.5 min-w-0 flex-1">
										<!-- Status Icon -->
										<div class="size-8 rounded-lg flex items-center justify-center shrink-0 mt-0.5 {isPositiveOutcome(log.outcome) ? 'bg-sky-100 text-sky-800 dark:bg-sky-950/70 dark:text-sky-300' : isFollowupOutcome(log.outcome) ? 'bg-purple-100 text-purple-800 dark:bg-purple-950/70 dark:text-purple-300' : isUnreachableOutcome(log.outcome) ? 'bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-300' : 'bg-indigo-100 text-indigo-800 dark:bg-indigo-950/70 dark:text-indigo-300'}">
											<Icon name={isPositiveOutcome(log.outcome) ? 'check-circle-2' : isFollowupOutcome(log.outcome) ? 'bell-ring' : isUnreachableOutcome(log.outcome) ? 'phone-off' : 'phone'} class="size-4" />
										</div>

										<div class="min-w-0 flex-1 space-y-1">
											<div class="flex items-center gap-2 flex-wrap">
												<button
													type="button"
													onclick={() => log.contactId && onSelectContactById?.(log.contactId)}
													class="text-xs font-bold text-foreground hover:text-primary transition-colors truncate text-left cursor-pointer"
													title="Load contact into workspace"
												>
													{log.contact?.fullName || 'Contact'}
												</button>
												{#if log.contact?.companyName}
													<span class="text-[11px] text-muted-foreground truncate max-w-[180px]">({log.contact.companyName})</span>
												{/if}
											</div>

											<div class="flex items-center gap-2.5 text-[10px] text-muted-foreground">
												<span class="font-mono flex items-center gap-1">
													<Icon name="phone" class="size-2.5 opacity-60" />
													{log.contact?.mobileNo || '—'}
												</span>
												<span>•</span>
												<span class="font-medium">
													{new Date(log.callDate).toLocaleDateString('en-IN', { day: '2-digit', month: 'short' })}
												</span>
											</div>

											{#if log.notes}
												<p class="text-[11px] text-foreground/80 bg-muted/40 px-2.5 py-1 rounded-md line-clamp-2 border border-border/40 italic">
													"{log.notes}"
												</p>
											{/if}
										</div>
									</div>

									<div class="flex items-center sm:flex-col sm:items-end justify-between sm:justify-center gap-2 shrink-0 border-t sm:border-t-0 pt-2 sm:pt-0 border-border/40">
										<span class="px-2.5 py-0.5 rounded-full text-[10px] font-bold tracking-wide {isPositiveOutcome(log.outcome) ? 'bg-sky-500/15 text-sky-700 dark:text-sky-300 border border-sky-500/30' : isFollowupOutcome(log.outcome) ? 'bg-purple-500/15 text-purple-700 dark:text-purple-400 border border-purple-500/30' : isUnreachableOutcome(log.outcome) ? 'bg-slate-200 dark:bg-slate-700 text-slate-700 dark:text-slate-300 border border-slate-300 dark:border-slate-600' : 'bg-indigo-500/15 text-indigo-700 dark:text-indigo-400 border border-indigo-500/30'}">
											{log.outcome}
										</span>
										<button
											type="button"
											onclick={() => log.contactId && onSelectContactById?.(log.contactId)}
											class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-0.5 cursor-pointer"
										>
											<span>Open Workspace</span>
											<Icon name="chevron-right" class="size-3" />
										</button>
									</div>
								</div>
							{/each}
						</div>
					{/if}
				</div>
			{/if}

			<!-- Split Row: Trend Histogram (Left) & Team Leaderboard (Right) -->
			<div class="grid grid-cols-1 lg:grid-cols-12 gap-3 pt-1">
				<!-- Left (7 Cols): Daily Call Trend Chart & Comparison Matrix -->
				<div class="lg:col-span-7 bg-background p-3.5 rounded-xl border border-border/70 shadow-2xs flex flex-col justify-between space-y-3">
					<div class="flex items-center justify-between flex-wrap gap-2">
						<div class="flex items-center gap-2 flex-wrap">
							<Icon name="bar-chart-2" class="size-4 text-primary" />
							<span class="font-bold text-xs text-foreground">
								{timeframe === 'month' || (timeframe === 'custom' && chartBars.length > 7) ? 'Weekly Call Distribution' : 'Daily Call Activity'}
							</span>
							<!-- Timeframe Switcher in Expanded View -->
							<div class="flex items-center bg-muted/40 p-0.5 rounded-lg border border-border/60 text-[10px] font-medium">
								<button
									type="button"
									onclick={() => handleTimeframeChange('7d')}
									class="px-2 py-0.5 rounded-md transition-all {timeframe === '7d' ? 'bg-background shadow-2xs font-bold text-primary' : 'text-muted-foreground hover:text-foreground'}"
									title="Show past 7 rolling days"
								>
									7 Days
								</button>
								<button
									type="button"
									onclick={() => handleTimeframeChange('today')}
									class="px-2 py-0.5 rounded-md transition-all {timeframe === 'today' ? 'bg-background shadow-2xs font-bold text-primary' : 'text-muted-foreground hover:text-foreground'}"
									title="Show today's activity"
								>
									Today
								</button>
								<button
									type="button"
									onclick={() => handleTimeframeChange('yesterday')}
									class="px-2 py-0.5 rounded-md transition-all {timeframe === 'yesterday' ? 'bg-background shadow-2xs font-bold text-primary' : 'text-muted-foreground hover:text-foreground'}"
									title="Show yesterday's activity"
								>
									Yesterday
								</button>
								<button
									type="button"
									onclick={() => handleTimeframeChange('month')}
									class="px-2 py-0.5 rounded-md transition-all {timeframe === 'month' ? 'bg-background shadow-2xs font-bold text-primary' : 'text-muted-foreground hover:text-foreground'}"
									title="Show this month"
								>
									Month
								</button>
								<!-- Custom Date Button with Popover -->
								<div class="relative">
									<button
										type="button"
										onclick={() => {
											if (timeframe !== 'custom') {
												timeframe = 'custom';
												loadData();
											}
											isCustomDateOpen = !isCustomDateOpen;
										}}
										class="px-2 py-0.5 rounded-md transition-all flex items-center gap-1 {timeframe === 'custom' ? 'bg-background shadow-2xs font-bold text-primary' : 'text-muted-foreground hover:text-foreground'}"
										title="Choose a custom date or date range"
									>
										<span>Custom</span>
										<Icon name="chevron-down" class="size-2.5 opacity-60 {isCustomDateOpen ? 'rotate-180' : ''} transition-transform" />
									</button>

									{#if isCustomDateOpen}
										<!-- Backdrop to dismiss -->
										<div
											class="fixed inset-0 z-40"
											onclick={() => (isCustomDateOpen = false)}
											role="presentation"
										></div>

										<!-- Popover right under Custom button -->
										<div class="absolute left-0 top-full mt-1.5 z-50 bg-popover text-popover-foreground border border-border rounded-xl shadow-xl p-3 w-64 text-xs space-y-2.5">
											<div class="flex items-center justify-between border-b border-border pb-1.5">
												<span class="font-bold text-[11px] text-foreground flex items-center gap-1.5">
													<Icon name="calendar" class="size-3.5 text-primary" />
													Custom Date
												</span>
												<button
													type="button"
													onclick={() => (isCustomDateOpen = false)}
													class="size-5 rounded hover:bg-muted flex items-center justify-center text-muted-foreground hover:text-foreground"
												>
													<Icon name="x" class="size-3" />
												</button>
											</div>

											<div class="grid grid-cols-2 gap-2">
												<div>
													<label class="text-[10px] text-muted-foreground font-medium block mb-1">From Date</label>
													<input
														type="date"
														bind:value={customStartDate}
														onchange={() => {
															if (!customEndDate || customEndDate < customStartDate) {
																customEndDate = customStartDate;
															}
														}}
														class="w-full h-7 px-2 text-[11px] rounded-md border border-border bg-background text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
													/>
												</div>
												<div>
													<label class="text-[10px] text-muted-foreground font-medium block mb-1">To Date</label>
													<input
														type="date"
														bind:value={customEndDate}
														min={customStartDate}
														class="w-full h-7 px-2 text-[11px] rounded-md border border-border bg-background text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
													/>
												</div>
											</div>

											<!-- Actions -->
											<div class="flex items-center gap-1 pt-1 border-t border-border/50">
												<button
													type="button"
													onclick={() => {
														const today = new Date().toISOString().slice(0, 10);
														customStartDate = today;
														customEndDate = today;
														applyCustomDate();
													}}
													class="px-2 py-1 text-[10px] rounded hover:bg-muted text-muted-foreground hover:text-foreground cursor-pointer"
												>
													Single Day
												</button>
												<button
													type="button"
													onclick={applyCustomDate}
													class="ml-auto px-3 py-1 text-[11px] font-semibold bg-primary text-primary-foreground hover:bg-primary/90 rounded-md shadow-2xs cursor-pointer"
												>
													Apply
												</button>
											</div>
										</div>
									{/if}
								</div>
							</div>
							<span
								class="text-[10px] font-medium text-muted-foreground bg-muted/60 px-1.5 py-0.5 rounded-md shrink-0 whitespace-nowrap flex items-center gap-1 cursor-default"
								title={activeDateRange.fullLabel}
							>
								<Icon name="calendar" class="size-2.5 opacity-60 shrink-0" />
								<span>{activeDateRange.compactLabel}</span>
							</span>
						</div>
						<!-- Contrast Soothing Semantic Legend -->
						<div class="flex items-center gap-3 text-[10px] text-muted-foreground">
							<span class="flex items-center gap-1.5 font-medium">
								<span class="size-2.5 rounded-xs bg-amber-500 shadow-2xs"></span> Positive
							</span>
							<span class="flex items-center gap-1.5 font-medium">
								<span class="size-2.5 rounded-xs bg-indigo-600 dark:bg-indigo-500 shadow-2xs"></span> Connected
							</span>
							<span class="flex items-center gap-1.5 font-medium">
								<span class="size-2.5 rounded-xs bg-slate-400 dark:bg-slate-500 shadow-2xs"></span> Unreachable
							</span>
						</div>
					</div>

					<!-- SVG Interactive Bar Chart -->
					<div class="relative h-32 w-full pt-2 flex flex-col justify-end">
						<div class="flex items-end justify-between h-24 gap-2 px-1 border-b border-border/60">
							{#each chartBars as bar}
								{@const heightPct = bar.total > 0 ? Math.max(12, Math.round((bar.total / maxBarVal) * 100)) : 0}
								{@const posPct = bar.total > 0 ? (bar.positive / bar.total) * 100 : 0}
								{@const connPct = bar.total > 0 ? (bar.connected / bar.total) * 100 : 0}

								<!-- Bar Column -->
								<div
									class="flex-1 flex flex-col items-center justify-end h-full group relative cursor-pointer"
									onmouseenter={() => (hoveredBar = bar)}
									onmouseleave={() => (hoveredBar = null)}
									role="button"
									tabindex="0"
								>
									<!-- Hover Floating Pill -->
									{#if hoveredBar?.dateStr === bar.dateStr}
										<div class="absolute -top-14 z-20 bg-popover text-popover-foreground text-[10px] px-3 py-1.5 rounded-lg shadow-lg border border-border whitespace-nowrap pointer-events-none space-y-0.5">
											<div class="font-bold text-xs">{bar.label} • {bar.subLabel}: <span class="text-primary">{bar.total} calls</span></div>
											<div class="flex items-center gap-2 text-[10px]">
												<span class="text-amber-600 dark:text-amber-400 font-semibold">✨ {bar.positive} positive</span>
												<span class="text-indigo-600 dark:text-indigo-400 font-semibold">📞 {bar.connected} connected</span>
												<span class="text-slate-500 dark:text-slate-400 font-semibold">❌ {bar.unreachable} missed</span>
											</div>
										</div>
									{/if}

									<!-- Bar Number Label on top -->
									<span class="text-[9px] font-mono font-semibold text-muted-foreground mb-1 {bar.isCurrent ? 'text-primary font-bold' : ''}">
										{bar.total > 0 ? bar.total : ''}
									</span>

									<!-- Stacked Colored Bar with Contrast Soothing Colors -->
									<div
										class="w-full max-w-[34px] rounded-t-lg overflow-hidden transition-all duration-300 flex flex-col justify-end shadow-2xs group-hover:scale-y-[1.02] {bar.isCurrent ? 'ring-2 ring-primary ring-offset-1' : ''}"
										style="height: {heightPct}%;"
									>
										{#if bar.total > 0}
											<!-- Top: Unreachable / Missed (Distinct Solid Slate) -->
											<div
												class="w-full bg-slate-400 dark:bg-slate-500 transition-colors duration-200"
												style="height: {100 - connPct}%;"
												title="Unreachable: {bar.unreachable}"
											></div>
											<!-- Middle: Connected Calls (Rich Solid Indigo) -->
											<div
												class="w-full bg-indigo-600 dark:bg-indigo-500 transition-colors duration-200"
												style="height: {connPct - posPct}%;"
												title="Connected: {bar.connected - bar.positive}"
											></div>
											<!-- Bottom: Positive Outcomes (Warm Solid Amber Gold) -->
											<div
												class="w-full bg-amber-500 dark:bg-amber-400 transition-colors duration-200"
												style="height: {posPct}%;"
												title="Positive: {bar.positive}"
											></div>
										{:else}
											<div class="w-full h-1 bg-muted/40 rounded-t-sm"></div>
										{/if}
									</div>
								</div>
							{/each}
						</div>

						<!-- X-Axis Labels -->
						<div class="flex items-center justify-between px-1 pt-1.5 text-[10px] text-muted-foreground">
							{#each chartBars as bar}
								<div class="flex-1 text-center font-medium {bar.isCurrent ? 'text-primary font-bold' : ''}">
									<div>{bar.label}</div>
									<div class="text-[8px] opacity-70">{bar.subLabel}</div>
								</div>
							{/each}
						</div>
					</div>

					<!-- Real Benchmarks Matrix: You vs Team Avg vs Top Performer -->
					<div class="pt-2 border-t border-border/50">
						<div class="text-[10px] uppercase font-bold text-muted-foreground tracking-wider mb-2">
							Performance Benchmark Matrix
						</div>
						<div class="grid grid-cols-3 gap-2 text-center text-xs">
							<!-- Col 1: You -->
							<div class="p-2.5 rounded-xl bg-indigo-50/70 dark:bg-indigo-950/40 border border-indigo-200 dark:border-indigo-800/60 shadow-2xs">
								<div class="text-[10px] font-extrabold text-indigo-600 dark:text-indigo-400 uppercase tracking-wider">You ({currentCleanUsername})</div>
								<div class="text-lg font-black text-foreground mt-0.5">{myTotalCalls} <span class="text-[10px] font-normal text-muted-foreground">calls</span></div>
								<div class="text-[10px] text-muted-foreground font-semibold flex items-center justify-center gap-1.5 mt-0.5">
									<span class="text-sky-600 dark:text-sky-400 font-bold">{myDailyAvg}/d</span>
									<span>•</span>
									<span class="text-amber-600 dark:text-amber-400 font-bold">{myPositiveRate}% pos</span>
								</div>
							</div>

							<!-- Col 2: Team Average -->
							<div class="p-2.5 rounded-xl bg-muted/40 border border-border/60 shadow-2xs">
								<div class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider">Team Average</div>
								<div class="text-lg font-black text-foreground mt-0.5">{teamAvgCalls} <span class="text-[10px] font-normal text-muted-foreground">calls</span></div>
								<div class="text-[10px] text-muted-foreground font-semibold flex items-center justify-center gap-1.5 mt-0.5">
									<span class="text-sky-600 dark:text-sky-400 font-bold">{teamDailyAvg}/d</span>
									<span>•</span>
									<span>{teamAvgPositiveRate}% pos</span>
								</div>
							</div>

							<!-- Col 3: Top Performer -->
							<div class="p-2.5 rounded-xl bg-amber-50/70 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800/60 shadow-2xs">
								<div class="text-[10px] font-extrabold text-amber-600 dark:text-amber-400 uppercase tracking-wider truncate">
									{topPerformer ? `👑 ${topPerformer.displayUsername}` : 'Top Performer'}
								</div>
								<div class="text-lg font-black text-foreground mt-0.5">
									{topPerformer ? topPerformer.calls : 0} <span class="text-[10px] font-normal text-muted-foreground">calls</span>
								</div>
								<div class="text-[10px] text-muted-foreground font-semibold flex items-center justify-center gap-1.5 mt-0.5">
									<span class="text-sky-600 dark:text-sky-400 font-bold">{topPerformer ? `${topPerformer.dailyAvg}/d` : '—'}</span>
									<span>•</span>
									<span class="text-amber-600 dark:text-amber-400 font-bold">{topPerformer ? `${topPerformer.positiveRate.toFixed(1)}% pos` : '—'}</span>
								</div>
							</div>
						</div>
					</div>
				</div>

				<!-- Right (5 Cols): Real Team Leaderboard -->
				<div class="lg:col-span-5 bg-background p-3.5 rounded-xl border border-border/70 shadow-2xs flex flex-col justify-between space-y-2.5">
					<div class="flex items-center justify-between border-b border-border/60 pb-2">
						<div class="flex items-center gap-1.5 font-bold text-xs text-foreground">
							<Icon name="award" class="size-4 text-amber-500" />
							<span>Active Leaderboard</span>
						</div>
						<span class="text-[10px] text-muted-foreground bg-muted/50 px-2 py-0.5 rounded-full font-medium">
							{activeTeamRepsCount} Active Calling Reps
						</span>
					</div>

					<!-- Leaderboard List: Only real active callers who made calls -->
					<div class="space-y-1.5 max-h-52 overflow-y-auto pr-1">
						{#if loading}
							<div class="text-center py-10 text-muted-foreground text-xs flex flex-col items-center justify-center gap-2">
								<Loader2 class="size-5 animate-spin text-primary" />
								<span>Loading leaderboard...</span>
							</div>
						{:else if leaderboard.length === 0}
							<div class="text-center py-10 text-muted-foreground text-xs">
								<Icon name="phone-off" class="size-6 mx-auto mb-1 opacity-40" />
								<p>No calls recorded yet for {activeDateRange.label}.</p>
								<p class="text-[10px] mt-1 text-muted-foreground/80">Switch to "Past 7 Days" or "Month" to see recent activity.</p>
							</div>
						{:else}
							{#each leaderboard as agent, idx}
								<div
									class="flex items-center justify-between p-2 rounded-lg transition-colors {agent.isCurrentUser
										? 'bg-indigo-50/80 dark:bg-indigo-950/40 border-2 border-indigo-400 dark:border-indigo-600 shadow-2xs'
										: 'bg-muted/20 hover:bg-muted/40 border border-border/40'}"
								>
									<div class="flex items-center gap-2 min-w-0">
										<!-- Rank Badge: Ultra-Clear, Bold Numbers on 3D Minted Metallic Medallions -->
										{#if agent.rank === 1}
											<!-- Rank 1: Gold Champion Medallion -->
											<svg class="size-8 shrink-0 drop-shadow-[0_2px_4px_rgba(217,119,6,0.35)]" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
												<title>Rank 1 (Gold)</title>
												<defs>
													<linearGradient id="rank-gold-base" x1="0" y1="0" x2="32" y2="32" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FEF08A" />
														<stop offset="25%" stop-color="#FBBF24" />
														<stop offset="70%" stop-color="#F59E0B" />
														<stop offset="100%" stop-color="#B45309" />
													</linearGradient>
													<linearGradient id="rank-gold-inner" x1="4" y1="4" x2="28" y2="28" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FFFBEB" />
														<stop offset="50%" stop-color="#FEF3C7" />
														<stop offset="100%" stop-color="#FDE68A" />
													</linearGradient>
												</defs>
												<!-- Outer Coin Rim -->
												<circle cx="16" cy="16" r="14.5" fill="url(#rank-gold-base)" stroke="#FEF08A" stroke-width="1.2" />
												<!-- Inner Minted Surface -->
												<circle cx="16" cy="16" r="11.5" fill="url(#rank-gold-inner)" stroke="#D97706" stroke-width="0.75" />
												<!-- Specular Highlight Arc -->
												<path d="M5.5 13C6.5 8 10.5 5 16 5C21.5 5 25.5 8 26.5 13C22.5 9.5 19.5 8 16 8C12.5 8 9.5 9.5 5.5 13Z" fill="#FFFFFF" opacity="0.6" />
												<!-- Big, Bold, Crystal-Clear Rank Number 1 -->
												<text x="16" y="22" text-anchor="middle" font-size="17" font-weight="900" font-family="system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" fill="#78350F">1</text>
											</svg>
										{:else if agent.rank === 2}
											<!-- Rank 2: Silver Elite Medallion -->
											<svg class="size-8 shrink-0 drop-shadow-[0_2px_4px_rgba(100,116,139,0.3)]" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
												<title>Rank 2 (Silver)</title>
												<defs>
													<linearGradient id="rank-silver-base" x1="0" y1="0" x2="32" y2="32" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FFFFFF" />
														<stop offset="25%" stop-color="#E2E8F0" />
														<stop offset="70%" stop-color="#94A3B8" />
														<stop offset="100%" stop-color="#475569" />
													</linearGradient>
													<linearGradient id="rank-silver-inner" x1="4" y1="4" x2="28" y2="28" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FFFFFF" />
														<stop offset="50%" stop-color="#F8FAFC" />
														<stop offset="100%" stop-color="#E2E8F0" />
													</linearGradient>
												</defs>
												<!-- Outer Coin Rim -->
												<circle cx="16" cy="16" r="14.5" fill="url(#rank-silver-base)" stroke="#FFFFFF" stroke-width="1.2" />
												<!-- Inner Minted Surface -->
												<circle cx="16" cy="16" r="11.5" fill="url(#rank-silver-inner)" stroke="#64748B" stroke-width="0.75" />
												<!-- Specular Highlight Arc -->
												<path d="M5.5 13C6.5 8 10.5 5 16 5C21.5 5 25.5 8 26.5 13C22.5 9.5 19.5 8 16 8C12.5 8 9.5 9.5 5.5 13Z" fill="#FFFFFF" opacity="0.65" />
												<!-- Big, Bold, Crystal-Clear Rank Number 2 -->
												<text x="16" y="22" text-anchor="middle" font-size="17" font-weight="900" font-family="system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" fill="#0F172A">2</text>
											</svg>
										{:else if agent.rank === 3}
											<!-- Rank 3: Bronze Podium Medallion -->
											<svg class="size-8 shrink-0 drop-shadow-[0_2px_4px_rgba(194,65,12,0.3)]" viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
												<title>Rank 3 (Bronze)</title>
												<defs>
													<linearGradient id="rank-bronze-base" x1="0" y1="0" x2="32" y2="32" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FFEDD5" />
														<stop offset="25%" stop-color="#FB923C" />
														<stop offset="70%" stop-color="#EA580C" />
														<stop offset="100%" stop-color="#9A3412" />
													</linearGradient>
													<linearGradient id="rank-bronze-inner" x1="4" y1="4" x2="28" y2="28" gradientUnits="userSpaceOnUse">
														<stop offset="0%" stop-color="#FFF7ED" />
														<stop offset="50%" stop-color="#FFEDD5" />
														<stop offset="100%" stop-color="#FED7AA" />
													</linearGradient>
												</defs>
												<!-- Outer Coin Rim -->
												<circle cx="16" cy="16" r="14.5" fill="url(#rank-bronze-base)" stroke="#FFEDD5" stroke-width="1.2" />
												<!-- Inner Minted Surface -->
												<circle cx="16" cy="16" r="11.5" fill="url(#rank-bronze-inner)" stroke="#C2410C" stroke-width="0.75" />
												<!-- Specular Highlight Arc -->
												<path d="M5.5 13C6.5 8 10.5 5 16 5C21.5 5 25.5 8 26.5 13C22.5 9.5 19.5 8 16 8C12.5 8 9.5 9.5 5.5 13Z" fill="#FFFFFF" opacity="0.6" />
												<!-- Big, Bold, Crystal-Clear Rank Number 3 -->
												<text x="16" y="22" text-anchor="middle" font-size="17" font-weight="900" font-family="system-ui, -apple-system, sans-serif" fill="#7C2D12">3</text>
											</svg>
										{:else}
											<!-- Rank 4+ -->
											<div class="size-8 rounded-full flex items-center justify-center font-black text-sm shrink-0 bg-muted/60 text-muted-foreground border border-border/60" title="Rank {agent.rank}">
												{agent.rank}
											</div>
										{/if}

										<div class="min-w-0">
											<div class="flex items-center gap-1.5">
												<span class="text-xs font-semibold truncate {agent.isCurrentUser ? 'text-indigo-600 dark:text-indigo-400 font-extrabold' : 'text-foreground'}">
													{agent.displayUsername}
												</span>
												{#if agent.isCurrentUser}
													<span class="px-1.5 py-0.2 rounded text-[9px] font-black bg-indigo-600 text-white">
														YOU
													</span>
												{/if}
											</div>
											<div class="text-[10px] text-muted-foreground">
												{agent.positiveRate.toFixed(0)}% positive outcome
											</div>
										</div>
									</div>

									<div class="text-right shrink-0">
										<div class="text-xs font-mono font-bold text-foreground flex items-center justify-end gap-1.5">
											<span>{agent.calls} <span class="text-[10px] font-normal text-muted-foreground">calls</span></span>
											<span
												class="text-[10px] font-bold text-sky-600 dark:text-sky-400 bg-sky-500/10 px-1.5 py-0.2 rounded"
												title="Daily average: {agent.dailyAvg} calls/day across {periodDays} day{periodDays > 1 ? 's' : ''} in period ({agent.activeDaysCount} active day{agent.activeDaysCount > 1 ? 's' : ''})"
											>
												{agent.dailyAvg}/day
											</span>
										</div>
										<div class="text-[10px] text-emerald-600 dark:text-emerald-400 font-bold">
											{agent.positiveCount} leads won
										</div>
									</div>
								</div>
							{/each}
						{/if}
					</div>

					<!-- Leaderboard Footer Insight -->
					<div class="pt-2 border-t border-border/50 text-[11px] text-muted-foreground flex items-center justify-between">
						{#if myTotalCalls > 0 && myRank === 1}
							<span class="text-emerald-600 dark:text-emerald-400 font-semibold flex items-center gap-1">
								<Icon name="check-check" class="size-3.5" /> Setting the team pace!
							</span>
						{:else if myTotalCalls > 0 && topPerformer}
							<span class="text-amber-600 dark:text-amber-400 font-semibold flex items-center gap-1">
								<Icon name="arrow-up-right" class="size-3.5" />
								{gapToLeader} calls behind #1 ({topPerformer.displayUsername})
							</span>
						{:else}
							<span class="text-muted-foreground">
								Period Total: <strong>{teamTotalCalls}</strong> calls
							</span>
						{/if}
						<span class="text-[10px] text-muted-foreground">Team Total: <strong>{teamTotalCalls}</strong></span>
					</div>
				</div>
			</div>
		</div>
	{/if}
</div>
