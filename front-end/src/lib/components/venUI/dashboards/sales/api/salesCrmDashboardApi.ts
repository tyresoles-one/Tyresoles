import { buildQuery, graphqlQuery } from '$lib/services/graphql';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import type {
	DashboardTimeframe,
	CallTargetRule,
	CallLogRecord,
	KpiMetrics,
	ActivityBucket,
	FunnelStage,
	OutcomeStat,
	SubordinateRep,
	FocusActionItem,
	DateRangeBounds,
	EmployeeHierarchyItem,
	SalesHierarchySummary,
	SubordinateSalesperson
} from './types';

// ============================================================================
// GraphQL Document Definitions
// ============================================================================

export const GetAllCrmCallLogsDocument = buildQuery`
	query GetAllCrmCallLogs($skip: Int, $take: Int, $where: CrmCallLogFilterInput, $order: [CrmCallLogSortInput!]) {
		crmCallLogs: getAllCrmCallLogs(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				contactId
				callDate
				outcome
				notes
				createdBy
				salesUserId
				invoiceNos
				invoiceAmount
				tyreQuantity
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					state
					respCenter
					erpCustomerNos
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<{
	crmCallLogs: { items: CallLogRecord[]; totalCount: number };
}, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetCrmSettingDocument = buildQuery`
	query GetCrmSetting($key: String!) {
		getCrmSetting(key: $key) {
			key
			value
			description
		}
	}
` as unknown as TypedDocumentNode<{
	getCrmSetting: { key: string; value: string; description?: string | null } | null;
}, { key: string }>;

export const GetCrmAgentSummaryReportDocument = buildQuery`
	query GetCrmAgentSummaryReport {
		getCrmAgentSummaryReport {
			agentUsername
			totalAllocated
			activeAllocated
			totalCalls
		}
	}
` as unknown as TypedDocumentNode<{
	getCrmAgentSummaryReport: Array<{
		agentUsername: string;
		totalAllocated: number;
		activeAllocated: number;
		totalCalls: number;
	}>;
}, {}>;

export const GetAllCrmCallRemindersDocument = buildQuery`
	query GetAllCrmCallReminders($skip: Int, $take: Int, $where: CrmCallReminderFilterInput, $order: [CrmCallReminderSortInput!]) {
		crmCallReminders: getAllCrmCallReminders(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				id
				contactId
				reminderDate
				notes
				isCompleted
				createdAt
				createdBy
				contact {
					id
					fullName
					companyName
					mobileNo
					city
					state
					respCenter
				}
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<{
	crmCallReminders: {
		items: Array<{
			id: string;
			contactId: string;
			reminderDate: string;
			notes?: string | null;
			isCompleted: boolean;
			createdAt: string;
			createdBy: string;
			contact?: {
				id: string;
				fullName: string;
				companyName?: string | null;
				mobileNo?: string | null;
				city?: string | null;
				state?: string | null;
				respCenter?: string | null;
			} | null;
		}>;
		totalCount: number;
	};
}, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetEmployeesDocument = buildQuery`
	query GetEmployees($skip: Int, $take: Int, $where: EmployeeFilterInput, $order: [EmployeeSortInput!]) {
		employees(skip: $skip, take: $take, where: $where, order: $order) {
			items {
				no
				firstName
				lastName
				initials
				jobTitle
				managerNo
				salespersPurchCode
				mobilePhoneNo
				eMail
				salesTeamType
				status
			}
			totalCount
		}
	}
` as unknown as TypedDocumentNode<{
	employees: { items: EmployeeHierarchyItem[]; totalCount: number };
}, { skip?: number; take?: number; where?: any; order?: any }>;

export const GetSalesHierarchyDocument = buildQuery`
	query GetSalesHierarchy($employeeCode: String!) {
		salesHierarchy(employeeCode: $employeeCode) {
			supervisorCode
			supervisorName
			supervisorMaxRoleType
			supervisorRoleName
			supervisorDisplayTitle
			totalSubordinatesCount
			subordinateCodes
			subordinates {
				code
				name
				roleType
				roleName
				displayTitle
				jobTitle
				mobilePhoneNo
				companyEmail
				sharedTeamsCount
				sharedTeamCodes
			}
		}
	}
` as unknown as TypedDocumentNode<{
	salesHierarchy: SalesHierarchySummary;
}, { employeeCode: string }>;

export const GetSubordinateEmployeeCodesDocument = buildQuery`
	query GetSubordinateEmployeeCodes($employeeCode: String!) {
		subordinateEmployeeCodes(employeeCode: $employeeCode)
	}
` as unknown as TypedDocumentNode<{
	subordinateEmployeeCodes: string[];
}, { employeeCode: string }>;

// ============================================================================
// Utilities & Outcome Classifiers
// ============================================================================

export function cleanUsername(raw?: string | null): string {
	if (!raw) return '';
	return raw
		.replace(/^(tyresoles[\\/]|domain[\\/])/i, '')
		.replace(/^[\\/]+/, '')
		.trim();
}

export function toLocalDateStr(d: Date | string | null | undefined): string {
	if (!d) return '';
	const date = typeof d === 'string' ? new Date(d) : d;
	if (isNaN(date.getTime())) return '';
	const y = date.getFullYear();
	const m = String(date.getMonth() + 1).padStart(2, '0');
	const day = String(date.getDate()).padStart(2, '0');
	return `${y}-${m}-${day}`;
}

export function isPositiveOutcome(outcomeStr?: string | null): boolean {
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

export function isUnreachableOutcome(outcomeStr?: string | null): boolean {
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

export function isFollowupOutcome(outcomeStr?: string | null): boolean {
	if (!outcomeStr) return false;
	const norm = outcomeStr.toLowerCase();
	return (
		norm.includes('follow') ||
		norm.includes('callback') ||
		norm.includes('reminder') ||
		norm.includes('reschedule')
	);
}

export function classifyOutcomeCategory(outcomeStr?: string | null): 'positive' | 'followup' | 'unreachable' | 'neutral' | 'lost' {
	if (!outcomeStr) return 'neutral';
	const norm = outcomeStr.toLowerCase();
	if (isPositiveOutcome(norm)) return 'positive';
	if (isFollowupOutcome(norm)) return 'followup';
	if (isUnreachableOutcome(norm)) return 'unreachable';
	if (norm.includes('not interested') || norm.includes('lost') || norm.includes('rejected')) return 'lost';
	return 'neutral';
}

// ============================================================================
// Date Bounds Calculator
// ============================================================================

export function getDateRangeBounds(
	tf: DashboardTimeframe,
	customStart?: string,
	customEnd?: string
): DateRangeBounds {
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
			fullLabel: `Today (${dateStr})`,
			daysCount: 1
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
			fullLabel: `Yesterday (${dateStr})`,
			daysCount: 1
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
			fullLabel: `Past 7 Days (${startShort} – ${endShort})`,
			daysCount: 7
		};
	} else if (tf === 'month') {
		const start = new Date(now.getFullYear(), now.getMonth(), 1, 0, 0, 0, 0);
		const end = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59, 999);
		const monthLong = start.toLocaleDateString('en-IN', { month: 'long', year: 'numeric' });
		const monthCompact = start.toLocaleDateString('en-IN', { month: 'short', year: 'numeric' });
		const currentDayOfMonth = Math.min(now.getDate(), end.getDate());
		return {
			start,
			end,
			label: `This Month (${monthLong})`,
			compactLabel: monthCompact,
			fullLabel: `This Month (${monthLong})`,
			daysCount: currentDayOfMonth
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
		const days = Math.max(1, Math.round((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1);
		return {
			start,
			end,
			label: isSame ? `Custom (${sShort})` : `Custom (${sShort} – ${eShort})`,
			compactLabel: compact,
			fullLabel: isSame
				? `Custom Date: ${start.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })}`
				: `Custom Range: ${start.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })} – ${end.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })}`,
			daysCount: days
		};
	}
}

// ============================================================================
// Metrics & Attainment Calculations
// ============================================================================

export function calculateStreakDays(logs: CallLogRecord[]): number {
	if (logs.length === 0) return 0;
	const activeDates = new Set<string>();
	for (const l of logs) {
		if (l.callDate) {
			const dStr = toLocalDateStr(l.callDate);
			if (dStr) activeDates.add(dStr);
		}
	}
	let streak = 0;
	const checkDate = new Date();
	for (let i = 0; i < 30; i++) {
		const key = toLocalDateStr(checkDate);
		if (activeDates.has(key)) {
			streak++;
			checkDate.setDate(checkDate.getDate() - 1);
		} else {
			if (i === 0) {
				// If today has no call yet, allow streak to continue from yesterday
				checkDate.setDate(checkDate.getDate() - 1);
				continue;
			}
			break;
		}
	}
	return streak;
}

export function calculateKpiMetrics(
	logs: CallLogRecord[],
	targetRule: CallTargetRule | undefined,
	bounds: DateRangeBounds,
	timeframe: DashboardTimeframe,
	rank?: number | null,
	totalRepsCount?: number
): KpiMetrics {
	const totalCalls = logs.length;
	let positiveCalls = 0;
	let unreachableCalls = 0;
	let followupCalls = 0;
	let revenueGenerated = 0;
	let tyresConverted = 0;
	const uniqueContactSet = new Set<string>();

	for (const l of logs) {
		if (l.contactId) uniqueContactSet.add(l.contactId);
		if (isPositiveOutcome(l.outcome)) positiveCalls++;
		if (isUnreachableOutcome(l.outcome)) unreachableCalls++;
		if (isFollowupOutcome(l.outcome)) followupCalls++;
		if (l.invoiceAmount && l.invoiceAmount > 0) revenueGenerated += l.invoiceAmount;
		if (l.tyreQuantity && l.tyreQuantity > 0) tyresConverted += l.tyreQuantity;
	}

	const connectedCalls = Math.max(0, totalCalls - unreachableCalls);
	const connectRate = totalCalls > 0 ? parseFloat(((connectedCalls / totalCalls) * 100).toFixed(1)) : 0;
	const positiveRate = totalCalls > 0 ? parseFloat(((positiveCalls / totalCalls) * 100).toFixed(1)) : 0;

	// Target determination based on timeframe
	let targetCalls = 30;
	if (timeframe === 'today' || timeframe === 'yesterday') {
		targetCalls = targetRule?.dailyTarget ?? 30;
	} else if (timeframe === '7d') {
		targetCalls = targetRule?.weeklyTarget ?? 150;
	} else if (timeframe === 'month') {
		targetCalls = targetRule?.monthlyTarget ?? 600;
	} else {
		// Custom
		if (bounds.daysCount <= 1) targetCalls = targetRule?.dailyTarget ?? 30;
		else if (bounds.daysCount <= 7) targetCalls = targetRule?.weeklyTarget ?? 150;
		else targetCalls = Math.round((targetRule?.dailyTarget ?? 30) * bounds.daysCount);
	}

	const attainmentPct = targetCalls > 0 ? Math.round((totalCalls / targetCalls) * 100) : 0;

	// Projected pace calculation (run-rate vs target)
	const daysPassed = Math.max(1, bounds.daysCount);
	const dailyRate = totalCalls / daysPassed;
	let totalPeriodDays = bounds.daysCount;
	if (timeframe === 'month') {
		const now = new Date();
		const totalDaysInMonth = new Date(now.getFullYear(), now.getMonth() + 1, 0).getDate();
		totalPeriodDays = totalDaysInMonth;
	}
	const paceProjectedCalls = Math.round(dailyRate * totalPeriodDays);

	let paceStatus: 'ahead' | 'on_track' | 'behind' = 'on_track';
	if (paceProjectedCalls >= targetCalls * 1.1) paceStatus = 'ahead';
	else if (paceProjectedCalls < targetCalls * 0.85) paceStatus = 'behind';

	const streakDays = calculateStreakDays(logs);

	return {
		totalCalls,
		connectedCalls,
		connectRate,
		positiveCalls,
		positiveRate,
		followupCalls,
		unreachableCalls,
		uniqueContacts: uniqueContactSet.size,
		revenueGenerated,
		tyresConverted,
		targetCalls,
		attainmentPct,
		paceProjectedCalls,
		paceStatus,
		streakDays,
		rank: rank ?? null,
		totalRepsCount: totalRepsCount ?? 0
	};
}

// ============================================================================
// LayerChart / Visual Bucketing
// ============================================================================

export function calculateActivityBuckets(
	logs: CallLogRecord[],
	bounds: DateRangeBounds,
	timeframe: DashboardTimeframe
): ActivityBucket[] {
	const buckets: ActivityBucket[] = [];
	const todayKey = toLocalDateStr(new Date());

	if (timeframe === 'today' || timeframe === 'yesterday') {
		// Hourly blocks from 8 AM to 7 PM (standard calling hours)
		const targetDate = bounds.start;
		const targetDateKey = toLocalDateStr(targetDate);
		const isToday = targetDateKey === todayKey;

		const hours = [8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19];
		for (const h of hours) {
			const label = `${h > 12 ? h - 12 : h}${h >= 12 ? 'pm' : 'am'}`;
			const subLabel = `${label} - ${h + 1 > 12 ? h + 1 - 12 : h + 1}${h + 1 >= 12 ? 'pm' : 'am'}`;

			const hourLogs = logs.filter((l) => {
				if (!l.callDate) return false;
				const d = new Date(l.callDate);
				return toLocalDateStr(d) === targetDateKey && d.getHours() === h;
			});

			const calls = hourLogs.length;
			const positive = hourLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = hourLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, calls - unreachable);
			const revenue = hourLogs.reduce((acc, curr) => acc + (curr.invoiceAmount || 0), 0);

			const currentHour = new Date().getHours();
			const isCurrent = isToday && currentHour === h;

			buckets.push({
				label,
				subLabel,
				dateKey: `${targetDateKey}_h${h}`,
				isCurrent,
				calls,
				connected,
				positive,
				unreachable,
				revenue
			});
		}
	} else if (timeframe === '7d') {
		// Daily blocks for 7 rolling days
		const dayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
		for (let i = 0; i < 7; i++) {
			const d = new Date(bounds.start);
			d.setDate(bounds.start.getDate() + i);
			const dateKey = toLocalDateStr(d);
			const isCurrent = dateKey === todayKey;

			const dayLogs = logs.filter((l) => {
				if (!l.callDate) return false;
				return toLocalDateStr(l.callDate) === dateKey;
			});

			const calls = dayLogs.length;
			const positive = dayLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = dayLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, calls - unreachable);
			const revenue = dayLogs.reduce((acc, curr) => acc + (curr.invoiceAmount || 0), 0);

			buckets.push({
				label: dayNames[d.getDay()],
				subLabel: `${d.getDate()} ${d.toLocaleDateString('en-IN', { month: 'short' })}`,
				dateKey,
				isCurrent,
				calls,
				connected,
				positive,
				unreachable,
				revenue
			});
		}
	} else if (timeframe === 'month') {
		// Weekly intervals within the month
		let curr = new Date(bounds.start);
		let weekIndex = 1;
		while (curr <= bounds.end) {
			const bStart = new Date(curr);
			const bEnd = new Date(curr);
			bEnd.setDate(bEnd.getDate() + 6);
			if (bEnd > bounds.end) bEnd.setTime(bounds.end.getTime());

			const sStr = toLocalDateStr(bStart);
			const eStr = toLocalDateStr(bEnd);
			const isCurrent = todayKey >= sStr && todayKey <= eStr;

			const blockLogs = logs.filter((l) => {
				if (!l.callDate) return false;
				const lStr = toLocalDateStr(l.callDate);
				return lStr >= sStr && lStr <= eStr;
			});

			const calls = blockLogs.length;
			const positive = blockLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = blockLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, calls - unreachable);
			const revenue = blockLogs.reduce((acc, curr) => acc + (curr.invoiceAmount || 0), 0);

			buckets.push({
				label: `Wk ${weekIndex}`,
				subLabel: `${bStart.getDate()}-${bEnd.getDate()} ${bStart.toLocaleDateString('en-IN', { month: 'short' })}`,
				dateKey: `${sStr}_to_${eStr}`,
				isCurrent,
				calls,
				connected,
				positive,
				unreachable,
				revenue
			});

			curr.setDate(curr.getDate() + 7);
			weekIndex++;
		}
	} else {
		// Custom timeframe: up to 14 discrete day or interval buckets
		const dayCount = bounds.daysCount;
		const step = Math.max(1, Math.ceil(dayCount / 10));

		let curr = new Date(bounds.start);
		while (curr <= bounds.end) {
			const bStart = new Date(curr);
			const bEnd = new Date(curr);
			bEnd.setDate(bEnd.getDate() + step - 1);
			if (bEnd > bounds.end) bEnd.setTime(bounds.end.getTime());

			const sStr = toLocalDateStr(bStart);
			const eStr = toLocalDateStr(bEnd);
			const isCurrent = todayKey >= sStr && todayKey <= eStr;

			const blockLogs = logs.filter((l) => {
				if (!l.callDate) return false;
				const lStr = toLocalDateStr(l.callDate);
				return lStr >= sStr && lStr <= eStr;
			});

			const calls = blockLogs.length;
			const positive = blockLogs.filter((l) => isPositiveOutcome(l.outcome)).length;
			const unreachable = blockLogs.filter((l) => isUnreachableOutcome(l.outcome)).length;
			const connected = Math.max(0, calls - unreachable);
			const revenue = blockLogs.reduce((acc, curr) => acc + (curr.invoiceAmount || 0), 0);

			buckets.push({
				label: step === 1 ? bStart.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) : `${bStart.getDate()}-${bEnd.getDate()}`,
				subLabel: `${sStr}`,
				dateKey: `${sStr}_${eStr}`,
				isCurrent,
				calls,
				connected,
				positive,
				unreachable,
				revenue
			});

			curr.setDate(curr.getDate() + step);
		}
	}

	return buckets;
}

// ============================================================================
// Funnel & Outcome Distribution
// ============================================================================

export function calculateFunnelStages(logs: CallLogRecord[]): FunnelStage[] {
	const totalDialed = logs.length;
	let connectedCount = 0;
	let meaningfulCount = 0;
	let positiveCount = 0;

	for (const l of logs) {
		const isUnreachable = isUnreachableOutcome(l.outcome);
		const isPos = isPositiveOutcome(l.outcome);
		const isFollow = isFollowupOutcome(l.outcome);

		if (!isUnreachable) {
			connectedCount++;
			if (isPos || isFollow || (l.notes && l.notes.trim().length > 5)) {
				meaningfulCount++;
			}
		}
		if (isPos) {
			positiveCount++;
		}
	}

	const calcPct = (val: number, base: number) => (base > 0 ? parseFloat(((val / base) * 100).toFixed(1)) : 0);

	return [
		{
			key: 'dialed',
			label: 'Calls Dialed',
			count: totalDialed,
			conversionFromPrevious: 100,
			conversionFromTotal: 100,
			color: 'oklch(0.65 0.2 260)', // Indigo
			icon: 'phone-outgoing'
		},
		{
			key: 'connected',
			label: 'Connected / Reached',
			count: connectedCount,
			conversionFromPrevious: calcPct(connectedCount, totalDialed),
			conversionFromTotal: calcPct(connectedCount, totalDialed),
			color: 'oklch(0.68 0.18 190)', // Teal
			icon: 'phone-incoming'
		},
		{
			key: 'engaged',
			label: 'Engaged Dialogue',
			count: meaningfulCount,
			conversionFromPrevious: calcPct(meaningfulCount, connectedCount),
			conversionFromTotal: calcPct(meaningfulCount, totalDialed),
			color: 'oklch(0.75 0.16 85)', // Amber
			icon: 'message-square'
		},
		{
			key: 'converted',
			label: 'Positive Won / Order',
			count: positiveCount,
			conversionFromPrevious: calcPct(positiveCount, meaningfulCount),
			conversionFromTotal: calcPct(positiveCount, totalDialed),
			color: 'oklch(0.7 0.2 145)', // Emerald
			icon: 'trophy'
		}
	];
}

export function calculateOutcomeStats(logs: CallLogRecord[]): OutcomeStat[] {
	const map = new Map<string, { count: number; category: 'positive' | 'followup' | 'unreachable' | 'neutral' | 'lost' }>();

	for (const l of logs) {
		const outcome = (l.outcome || 'Unknown').trim();
		if (!map.has(outcome)) {
			map.set(outcome, { count: 0, category: classifyOutcomeCategory(outcome) });
		}
		map.get(outcome)!.count++;
	}

	const total = logs.length;
	const colorMap = {
		positive: 'oklch(0.68 0.19 145)',   // Emerald
		followup: 'oklch(0.72 0.17 85)',    // Amber
		unreachable: 'oklch(0.55 0.12 250)', // Slate/Blue
		lost: 'oklch(0.62 0.22 25)',        // Red
		neutral: 'oklch(0.6 0.05 240)'      // Gray
	};

	const stats: OutcomeStat[] = Array.from(map.entries())
		.map(([outcome, data]) => ({
			outcome,
			count: data.count,
			percentage: total > 0 ? parseFloat(((data.count / total) * 100).toFixed(1)) : 0,
			category: data.category,
			color: colorMap[data.category]
		}))
		.sort((a, b) => b.count - a.count);

	return stats;
}

// ============================================================================
// Leaderboard & Subordinate Calculations
// ============================================================================

export function calculateLeaderboard(
	allLogs: CallLogRecord[],
	allSubordinates: EmployeeHierarchyItem[],
	targetsList: CallTargetRule[],
	periodDays: number,
	timeframe: DashboardTimeframe,
	activeAllocationsMap: Map<string, number>,
	salesUsersMap: Map<string, string>,
	hierarchySubordinates?: SubordinateSalesperson[]
): SubordinateRep[] {
	// Lookup map for hierarchy subordinates
	const subMap = new Map<string, SubordinateSalesperson>();
	if (hierarchySubordinates && hierarchySubordinates.length > 0) {
		for (const s of hierarchySubordinates) {
			subMap.set(cleanUsername(s.code).toLowerCase(), s);
		}
	}

	// Group logs by agent
	const agentStats = new Map<
		string,
		{
			raw: string;
			totalCalls: number;
			positiveCalls: number;
			connectedCalls: number;
			revenue: number;
			dates: Set<string>;
		}
	>();

	// Pre-seed all hierarchy subordinates so reps with 0 calls still appear on manager board
	if (subMap.size > 0) {
		for (const [code, sub] of subMap) {
			agentStats.set(code, {
				raw: sub.code,
				totalCalls: 0,
				positiveCalls: 0,
				connectedCalls: 0,
				revenue: 0,
				dates: new Set<string>()
			});
		}
	}

	for (const log of allLogs) {
		const raw = (log.createdBy || '').trim();
		if (!raw) continue;
		const clean = cleanUsername(raw).toLowerCase();

		// If a hierarchy scope exists, skip agents that are not in the supervisor's team hierarchy
		if (subMap.size > 0 && !subMap.has(clean)) {
			continue;
		}

		if (!agentStats.has(clean)) {
			agentStats.set(clean, {
				raw,
				totalCalls: 0,
				positiveCalls: 0,
				connectedCalls: 0,
				revenue: 0,
				dates: new Set<string>()
			});
		}

		const entry = agentStats.get(clean)!;
		entry.totalCalls++;
		if (isPositiveOutcome(log.outcome)) entry.positiveCalls++;
		if (!isUnreachableOutcome(log.outcome)) entry.connectedCalls++;
		if (log.invoiceAmount && log.invoiceAmount > 0) entry.revenue += log.invoiceAmount;
		if (log.callDate) {
			const dStr = toLocalDateStr(log.callDate);
			if (dStr) entry.dates.add(dStr);
		}
	}

	// Default target rule fallback
	const defaultRule = targetsList.find((t) => {
		const ag = (t.agentUsername || '').toUpperCase();
		return ag === 'DEFAULT' || ag === 'GLOBAL_DEFAULT';
	}) || { agentUsername: 'DEFAULT', dailyTarget: 30, weeklyTarget: 150, monthlyTarget: 600, isActive: true };

	// Convert map into SubordinateRep array
	const reps: SubordinateRep[] = Array.from(agentStats.entries()).map(([clean, data]) => {
		const targetRule = targetsList.find((t) => cleanUsername(t.agentUsername).toLowerCase() === clean && t.isActive !== false) || defaultRule;

		let effectiveTarget = 30;
		if (timeframe === 'today' || timeframe === 'yesterday') effectiveTarget = targetRule.dailyTarget;
		else if (timeframe === '7d') effectiveTarget = targetRule.weeklyTarget;
		else if (timeframe === 'month') effectiveTarget = targetRule.monthlyTarget;
		else effectiveTarget = targetRule.dailyTarget * Math.max(1, periodDays);

		const connectRate = data.totalCalls > 0 ? parseFloat(((data.connectedCalls / data.totalCalls) * 100).toFixed(1)) : 0;
		const positiveRate = data.totalCalls > 0 ? parseFloat(((data.positiveCalls / data.totalCalls) * 100).toFixed(1)) : 0;
		const attainmentPct = effectiveTarget > 0 ? Math.round((data.totalCalls / effectiveTarget) * 100) : 0;

		const subInfo = subMap.get(clean);
		const empMatch = allSubordinates.find((e) => cleanUsername(e.initials || e.firstName).toLowerCase() === clean);
		const displayName = subInfo?.name || (empMatch
			? `${empMatch.firstName} ${empMatch.lastName}`.trim()
			: salesUsersMap.get(clean) || clean);

		// Calculate streak
		let streakDays = 0;
		const checkDate = new Date();
		for (let i = 0; i < 30; i++) {
			const key = toLocalDateStr(checkDate);
			if (data.dates.has(key)) {
				streakDays++;
				checkDate.setDate(checkDate.getDate() - 1);
			} else {
				if (i === 0) {
					checkDate.setDate(checkDate.getDate() - 1);
					continue;
				}
				break;
			}
		}

		// Check for manager coaching alerts
		let alert: SubordinateRep['alert'] = null;
		if (data.totalCalls === 0 && (activeAllocationsMap.get(clean) || 0) > 0) {
			alert = { type: 'danger', message: 'No calls logged yet with active allocated contacts!' };
		} else if (attainmentPct < 40 && periodDays === 1) {
			alert = { type: 'warning', message: 'Below 40% daily quota pace' };
		} else if (connectRate < 45 && data.totalCalls > 10) {
			alert = { type: 'info', message: 'Low connect rate (<45%) - review contact numbers' };
		}

		return {
			username: data.raw,
			cleanUsername: clean,
			displayName,
			employeeNo: subInfo?.code || empMatch?.no,
			managerNo: empMatch?.managerNo,
			jobTitle: subInfo?.jobTitle || subInfo?.displayTitle || empMatch?.jobTitle,
			roleType: subInfo?.roleType,
			roleName: subInfo?.roleName,
			isDirectReport: Boolean(subInfo || empMatch),
			totalCalls: data.totalCalls,
			connectedCalls: data.connectedCalls,
			connectRate,
			positiveCalls: data.positiveCalls,
			positiveRate,
			revenueGenerated: data.revenue,
			activeAllocations: activeAllocationsMap.get(clean) || 0,
			dailyTarget: targetRule.dailyTarget,
			weeklyTarget: targetRule.weeklyTarget,
			monthlyTarget: targetRule.monthlyTarget,
			effectiveTarget,
			attainmentPct,
			streakDays,
			rank: 1,
			alert
		};
	});

	// Multi-tier deterministic sorting: Total Calls DESC -> Positive Won DESC -> Connect Rate DESC
	reps.sort((a, b) => b.totalCalls - a.totalCalls || b.positiveCalls - a.positiveCalls || b.connectRate - a.connectRate);

	// Proper competition ranking (identical values share same rank)
	for (let i = 0; i < reps.length; i++) {
		if (i === 0) {
			reps[0].rank = 1;
		} else {
			const prev = reps[i - 1];
			const curr = reps[i];
			if (curr.totalCalls === prev.totalCalls && curr.positiveCalls === prev.positiveCalls) {
				curr.rank = prev.rank;
			} else {
				curr.rank = i + 1;
			}
		}
	}

	return reps;
}
