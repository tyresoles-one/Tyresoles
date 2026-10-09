/**
 * Sales Performance & Run Rate Tracker API & Calculation Utilities
 * Calculates working days in month (excluding Sundays, respecting user workDate),
 * per-day projected target, month-to-date sales achieved, and required run rate (RRR).
 */

import { graphqlQuery } from '$lib/services/graphql/client';
import { GetSalesHierarchyDocument, cleanUsername } from './salesCrmDashboardApi';
import type { SalesHierarchySummary } from './types';

export interface WorkingDaysInfo {
	year: number;
	month: number; // 0-indexed (0 = Jan, 9 = Oct)
	monthName: string;
	workDateStr: string;
	totalCalendarDays: number;
	totalWorkingDays: number;
	daysSpent: number;
	daysRemaining: number;
	workDaysElapsedPct: number;
}

export type PerformanceStatus = 'achieved' | 'ahead' | 'on-track' | 'behind' | 'critical' | 'inactive';

export interface TerritoryPersonInfo {
	personCode: string;
	personName: string;
	roleName?: string;
	isSelf: boolean;
}

export interface AuthorizedTerritoryScope {
	isRestricted: boolean;
	supervisorCode: string;
	myDirectTeams: string[];
	myDirectAreas: string[];
	subordinateCodes: string[];
	subordinateTeams: string[];
	allAllowedTeams: Set<string>;
	allAllowedAreas: Set<string>;
	teamAssignments: Map<string, TerritoryPersonInfo>;
}

export interface UserScopeOptions {
	entityCode?: string | null;
	entityType?: string | null;
	department?: string | null;
	username?: string | null;
	userId?: string | null;
	respCenter?: string | null;
	fullName?: string | null;
	userType?: string | null;
}

export interface SalesPerformanceMetrics {
	teamCode: string;
	teamName: string;
	respCenter: string;

	// Person Attribution & Subordinate Tagging
	assignedPersonName?: string;
	assignedPersonCode?: string;
	roleTitle?: string;
	isSelf?: boolean;

	// Targets & Actuals
	target: number;
	currentSale: number;
	achievementPct: number;

	// Projected Daily Sales
	dailyTarget: number; // target / totalWorkingDays
	expectedToDate: number; // dailyTarget * daysSpent
	paceDelta: number; // currentSale - expectedToDate

	// Run Rates
	remainingTarget: number; // max(0, target - currentSale)
	currentRunRate: number; // currentSale / max(1, daysSpent)
	requiredRunRate: number; // remainingTarget / max(1, daysRemaining)
	runRatePressure: number; // requiredRunRate / max(1, dailyTarget)

	// Projected End-Of-Month at current pace
	projectedEomSale: number;

	status: PerformanceStatus;
}

export interface TeamSalesItemRaw {
	teamCode: string;
	teamName: string;
	respCenter: string;
	currentSale: number;
	existingTarget: number;
	existingTargetEndDate?: string | null;
	nextTarget?: number;
}

export interface SalesPerformanceTrackerData {
	workingDays: WorkingDaysInfo;
	respCenter: string;
	isRestricted: boolean;
	accessibleTeamsCount: number;
	authorizedScope: AuthorizedTerritoryScope;
	myPerformance: SalesPerformanceMetrics | null;
	teamSummary: SalesPerformanceMetrics;
	teams: SalesPerformanceMetrics[];
}

/**
 * Calculates working days for the month of the provided date.
 * Working days = Monday through Saturday (Sundays excluded).
 * Respects the user's workDate for elapsed days calculation.
 */
export function calculateWorkingDays(refDateInput?: Date | string | null): WorkingDaysInfo {
	let refDate: Date;
	if (!refDateInput) {
		refDate = new Date();
	} else if (typeof refDateInput === 'string') {
		const parsed = new Date(refDateInput);
		refDate = isNaN(parsed.getTime()) ? new Date() : parsed;
	} else {
		refDate = isNaN(refDateInput.getTime()) ? new Date() : refDateInput;
	}

	const year = refDate.getFullYear();
	const month = refDate.getMonth();
	const monthName = refDate.toLocaleDateString('en-IN', { month: 'short', year: 'numeric' });
	const totalCalendarDays = new Date(year, month + 1, 0).getDate();
	const currentDay = Math.min(refDate.getDate(), totalCalendarDays);

	const yyyy = year;
	const mm = String(month + 1).padStart(2, '0');
	const dd = String(currentDay).padStart(2, '0');
	const workDateStr = `${yyyy}-${mm}-${dd}`;

	let totalWorkingDays = 0;
	let daysSpent = 0;

	for (let d = 1; d <= totalCalendarDays; d++) {
		const dateObj = new Date(year, month, d);
		const isSunday = dateObj.getDay() === 0;
		if (!isSunday) {
			totalWorkingDays++;
			if (d <= currentDay) {
				daysSpent++;
			}
		}
	}

	const daysRemaining = Math.max(0, totalWorkingDays - daysSpent);
	const workDaysElapsedPct = totalWorkingDays > 0 ? (daysSpent / totalWorkingDays) * 100 : 0;

	return {
		year,
		month,
		monthName,
		workDateStr,
		totalCalendarDays,
		totalWorkingDays,
		daysSpent,
		daysRemaining,
		workDaysElapsedPct
	};
}

/**
 * Calculates sales performance metrics for a single team or aggregated total.
 */
export function calculatePerformanceMetrics(
	item: {
		teamCode: string;
		teamName: string;
		respCenter: string;
		target: number;
		currentSale: number;
		assignedPersonName?: string;
		assignedPersonCode?: string;
		roleTitle?: string;
		isSelf?: boolean;
	},
	workingDays: WorkingDaysInfo
): SalesPerformanceMetrics {
	const { totalWorkingDays, daysSpent, daysRemaining } = workingDays;
	const target = Math.max(0, Number(item.target) || 0);
	const currentSale = Math.max(0, Number(item.currentSale) || 0);

	// Daily projected plan
	const dailyTarget = totalWorkingDays > 0 ? target / totalWorkingDays : 0;
	const expectedToDate = dailyTarget * daysSpent;
	const paceDelta = currentSale - expectedToDate;

	// Attainment
	const achievementPct = target > 0 ? (currentSale / target) * 100 : currentSale > 0 ? 100 : 0;

	// Run rates
	const remainingTarget = Math.max(0, target - currentSale);
	const currentRunRate = daysSpent > 0 ? currentSale / daysSpent : 0;
	const requiredRunRate = daysRemaining > 0 ? remainingTarget / daysRemaining : remainingTarget > 0 ? remainingTarget : 0;
	const runRatePressure = dailyTarget > 0 ? requiredRunRate / dailyTarget : 0;

	// Projected EOM at current pace
	const projectedEomSale = currentSale + currentRunRate * daysRemaining;

	// Health status
	let status: PerformanceStatus = 'on-track';
	if (target <= 0 && currentSale <= 0) {
		status = 'inactive';
	} else if (currentSale >= target && target > 0) {
		status = 'achieved';
	} else if (target > 0 && (paceDelta >= 0 || (dailyTarget > 0 && currentRunRate >= requiredRunRate))) {
		status = 'ahead';
	} else if (target <= 0 && currentSale > 0) {
		status = 'ahead';
	} else if (runRatePressure <= 1.25) {
		status = 'on-track';
	} else if (runRatePressure <= 1.75) {
		status = 'behind';
	} else {
		status = 'critical';
	}

	return {
		teamCode: item.teamCode,
		teamName: item.teamName,
		respCenter: item.respCenter,
		assignedPersonName: item.assignedPersonName,
		assignedPersonCode: item.assignedPersonCode,
		roleTitle: item.roleTitle,
		isSelf: item.isSelf,
		target,
		currentSale,
		achievementPct,
		dailyTarget,
		expectedToDate,
		paceDelta,
		remainingTarget,
		currentRunRate,
		requiredRunRate,
		runRatePressure,
		projectedEomSale,
		status
	};
}

const PREVIEW_TARGETS_QUERY = `
	query PreviewTeamSalesTargets($request: PreviewTeamSalesTargetsRequestInput!) {
		previewTeamSalesTargets(request: $request) {
			success
			message
			respCenter
			fromDate
			toDate
			totalCurrentSale
			totalNextTarget
			items {
				teamCode
				teamName
				respCenter
				currentSale
				targetMultiplier
				existingTarget
				existingTargetEndDate
				nextTarget
			}
		}
	}
`;

const GET_MY_AREAS_QUERY = `
	query GetMyAreasForPerformance(
		$entityType: String
		$entityCode: String
		$department: String
		$respCenters: [String!]
		$first: Int
	) {
		myAreas(
			entityType: $entityType
			entityCode: $entityCode
			department: $department
			respCenters: $respCenters
			first: $first
		) {
			nodes {
				code
				name
				team
				responsibilityCenter
			}
			totalCount
		}
	}
`;

/**
 * Resolves the authorized territory scope for the logged-in user:
 * 1. Queries `salesHierarchy(employeeCode)` to get all subordinate salespersons and their shared team codes.
 * 2. Queries `myAreas(entityType, entityCode, department)` to get the user's direct area and team assignments.
 * 3. Builds a strict whitelist of allowed teams and assigns person attribution to each team.
 * All other territory/team codes are strictly restricted from visibility.
 */
const scopeCache = new Map<string, { scope: AuthorizedTerritoryScope; timestamp: number }>();
const SCOPE_CACHE_TTL = 10 * 60 * 1000; // 10 minutes cache for hierarchy & permissions

const dataCache = new Map<string, { data: SalesPerformanceTrackerData; timestamp: number }>();
const DATA_CACHE_TTL = 60 * 1000; // 60 seconds cache for calculated sales metrics

/**
 * Manually flushes cached data for instant hard refresh.
 */
export function clearPerformanceCache() {
	scopeCache.clear();
	dataCache.clear();
}

/**
 * Cleans and formats raw Dynamics NAV team/territory strings into beginner-friendly titles and rep names.
 * Example NAV string: "UDUPI/PADUBIDRI/KUNDAPUR/BHATKAL/BELTHANGADI/NELYA(SANTOSH A DHAMANNAVAR)"
 * Outputs: shortTowns: "Udupi • Padubidri +4", personName: "Santosh A Dhamannavar", initials: "SD"
 */
export function parseTeamTerritoryInfo(
	teamCode: string,
	teamName?: string,
	assignedPerson?: string
): {
	shortTowns: string;
	fullTowns: string;
	personName: string;
	cleanTitle: string;
	cleanTownTitle: string;
	initials: string;
	teamInitials: string;
} {
	if (!teamName) {
		return {
			shortTowns: teamCode,
			fullTowns: teamCode,
			personName: assignedPerson || 'Unassigned',
			cleanTitle: teamCode,
			cleanTownTitle: teamCode,
			initials: teamCode.slice(0, 2).toUpperCase(),
			teamInitials: teamCode.slice(0, 2).toUpperCase()
		};
	}

	// Match person in parentheses if present, e.g. "(SANTOSH A DHAMANNAVAR)"
	const personMatch = teamName.match(/\(([^)]+)\)$/);
	const rawPerson = personMatch ? personMatch[1].trim() : (assignedPerson || '');
	const personName = rawPerson
		? rawPerson
				.split(' ')
				.map((w) => w.charAt(0).toUpperCase() + w.slice(1).toLowerCase())
				.join(' ')
		: assignedPerson || '';

	const withoutPerson = teamName.replace(/\([^)]+\)$/, '').trim();

	// Towns split by '/'
	const towns = withoutPerson
		.split('/')
		.map((t) => t.trim())
		.filter(Boolean)
		.map((t) => t.charAt(0).toUpperCase() + t.slice(1).toLowerCase());

	const fullTowns = towns.length > 0 ? towns.join(' / ') : withoutPerson;
	const shortTowns =
		towns.length > 2 ? `${towns[0]} • ${towns[1]} +${towns.length - 2}` : towns.join(' • ') || teamCode;

	const cleanTitle = towns[0] || teamCode;

	// Initials for person
	const nameForInitials = personName || cleanTitle;
	const parts = nameForInitials.split(/\s+/).filter(Boolean);
	const initials =
		parts.length >= 2
			? `${parts[0][0]}${parts[1][0]}`.toUpperCase()
			: (nameForInitials.slice(0, 2) || teamCode.slice(0, 2)).toUpperCase();

	// Distinct initials for the team/territory avatar
	let teamInitials = teamCode.slice(0, 2).toUpperCase();
	if (towns.length >= 2) {
		teamInitials = `${towns[0][0]}${towns[1][0]}`.toUpperCase();
	} else if (towns.length === 1 && towns[0].length >= 2) {
		teamInitials = towns[0].slice(0, 2).toUpperCase();
	}

	return {
		shortTowns,
		fullTowns,
		personName,
		cleanTitle,
		cleanTownTitle: shortTowns || cleanTitle,
		initials,
		teamInitials
	};
}

/**
 * Resolves the authorized territory scope for the logged-in user:
 * 1. Fast cache check (0ms response if visited within 10 minutes).
 * 2. High-speed parallel query of salesHierarchy and myAreas in a single round-trip.
 * 3. Builds a strict whitelist of allowed teams and assigns person attribution to each team.
 * All other territory/team codes are strictly restricted from visibility.
 */
export async function resolveUserAuthorizedScope(
	user?: UserScopeOptions | null,
	respCenter?: string | null,
	forceRefresh: boolean = false
): Promise<AuthorizedTerritoryScope> {
	const userKey = (user?.entityCode || user?.username || user?.userId || 'GUEST').trim();
	const cacheKey = `${userKey}_${respCenter || 'ALL'}`;

	if (!forceRefresh) {
		const cached = scopeCache.get(cacheKey);
		if (cached && Date.now() - cached.timestamp < SCOPE_CACHE_TTL) {
			return cached.scope;
		}
	}

	const result: AuthorizedTerritoryScope = {
		isRestricted: false,
		supervisorCode: '',
		myDirectTeams: [],
		myDirectAreas: [],
		subordinateCodes: [],
		subordinateTeams: [],
		allAllowedTeams: new Set<string>(),
		allAllowedAreas: new Set<string>(),
		teamAssignments: new Map<string, TerritoryPersonInfo>()
	};

	if (!user) return result;

	const isSuperAdmin =
		user.userType?.toLowerCase() === 'superadmin' ||
		user.userId?.toLowerCase() === 'admin' ||
		user.username?.toLowerCase() === 'admin';

	const primaryCode = (user.entityCode || cleanUsername(user.username) || user.userId || '').trim();

	if (!primaryCode) {
		if (!isSuperAdmin) {
			result.isRestricted = true;
		}
		return result;
	}

	try {
		// Run salesHierarchy and myAreas simultaneously in parallel
		const [hierRes, areasRes] = await Promise.all([
			graphqlQuery<{ salesHierarchy: SalesHierarchySummary }>(GetSalesHierarchyDocument, {
				variables: { employeeCode: primaryCode },
				skipCache: true,
				skipLoading: true
			}),
			graphqlQuery<{
				myAreas?: {
					nodes: Array<{
						code: string;
						name: string;
						team?: string | null;
						responsibilityCenter?: string | null;
					}>;
					totalCount: number;
				};
			}>(GET_MY_AREAS_QUERY, {
				variables: {
					entityType: user.entityType || 'Employee',
					entityCode: primaryCode,
					department: user.department || 'Sales',
					respCenters: respCenter && respCenter !== 'ALL' ? [respCenter] : undefined,
					first: 100
				},
				skipCache: true,
				skipLoading: true
			})
		]);

		// 1. Process Sales Hierarchy (Subordinates & Shared Teams)
		if (hierRes.success && hierRes.data?.salesHierarchy) {
			const h = hierRes.data.salesHierarchy;
			result.supervisorCode = h.supervisorCode || primaryCode;
			result.subordinateCodes = h.subordinateCodes || [];

			for (const sub of h.subordinates || []) {
				for (const tCode of sub.sharedTeamCodes || []) {
					const cleanT = tCode.trim().toUpperCase();
					if (cleanT) {
						result.subordinateTeams.push(cleanT);
						result.allAllowedTeams.add(cleanT);
						if (!result.teamAssignments.has(cleanT)) {
							result.teamAssignments.set(cleanT, {
								personCode: sub.code,
								personName: sub.name,
								roleName: sub.roleName || sub.displayTitle,
								isSelf: false
							});
						}
					}
				}
			}
		}

		// 2. Process My Areas (User's direct teams & areas)
		if (areasRes.success && areasRes.data?.myAreas?.nodes) {
			const nodes = areasRes.data.myAreas.nodes;
			for (const a of nodes) {
				if (a.code) {
					const cleanA = a.code.trim().toUpperCase();
					result.myDirectAreas.push(cleanA);
					result.allAllowedAreas.add(cleanA);
				}
				if (a.team) {
					const cleanT = a.team.trim().toUpperCase();
					result.myDirectTeams.push(cleanT);
					result.allAllowedTeams.add(cleanT);
					// Direct team for user: prioritize isSelf: true
					const existing = result.teamAssignments.get(cleanT);
					result.teamAssignments.set(cleanT, {
						personCode: primaryCode,
						personName: user.fullName || 'Me',
						roleName: existing && !existing.isSelf ? `${existing.roleName} (Managed)` : 'My Territory',
						isSelf: true
					});
				}
			}
		}
	} catch (err) {
		console.warn('[salesPerformanceApi] Error resolving user authorized scope:', err);
	}

	// Determine lockdown restriction
	if (result.allAllowedTeams.size > 0 || !isSuperAdmin) {
		result.isRestricted = true;
	}

	// Cache result for instant retrieval
	scopeCache.set(cacheKey, { scope: result, timestamp: Date.now() });

	return result;
}

/**
 * Loads sales targets and month-to-date sales from Dynamics NAV via GraphQL,
 * strictly restricting visibility to "me and my team (subordinates)".
 * Uses dual-layer caching and targeted center filtering for sub-second responses.
 */
export async function fetchSalesPerformanceData(
	options: {
		respCenter?: string;
		workDate?: string | Date | null;
		preferredTeamCode?: string;
		user?: UserScopeOptions | null;
		forceRefresh?: boolean;
		signal?: AbortSignal;
	} = {}
): Promise<{ success: boolean; data?: SalesPerformanceTrackerData; error?: string }> {
	const workingDays = calculateWorkingDays(options.workDate);

	// Date range for current month MTD
	const fromDate = `${workingDays.year}-${String(workingDays.month + 1).padStart(2, '0')}-01`;
	const toDate = workingDays.workDateStr;

	const userKey = (options.user?.entityCode || options.user?.username || options.user?.userId || 'GUEST').trim();
	const cacheKey = `${userKey}_${options.respCenter || 'BEL'}_${workingDays.workDateStr}`;

	if (!options.forceRefresh) {
		const cached = dataCache.get(cacheKey);
		if (cached && Date.now() - cached.timestamp < DATA_CACHE_TTL) {
			return { success: true, data: cached.data };
		}
	}

	try {
		// 1. Resolve authorized scope (Me + Subordinates) - 0ms if cached
		const scope = await resolveUserAuthorizedScope(options.user, options.respCenter, options.forceRefresh);

		// 2. Fetch team sales data from NAV with active center scoping for fast SQL execution
		const queryRespCenter = options.respCenter === 'ALL' ? null : options.respCenter || null;

		const res = await graphqlQuery<{
			previewTeamSalesTargets: {
				success: boolean;
				message: string;
				respCenter?: string;
				totalCurrentSale: number;
				items: TeamSalesItemRaw[];
			};
		}>(PREVIEW_TARGETS_QUERY, {
			variables: {
				request: {
					respCenter: queryRespCenter,
					fromDate,
					toDate,
					saleType: 'retread-ecomile'
				}
			},
			skipCache: true
		});

		if (!res.success || !res.data?.previewTeamSalesTargets?.success) {
			return {
				success: false,
				error: res.data?.previewTeamSalesTargets?.message || res.error || 'Failed to load team sales data.'
			};
		}

		const rawItems = res.data.previewTeamSalesTargets.items || [];

		// 3. Strictly restrict to user's authorized scope
		let authorizedItems = rawItems;
		if (scope.isRestricted) {
			authorizedItems = rawItems.filter((item) =>
				scope.allAllowedTeams.has(item.teamCode.toUpperCase().trim())
			);
		}

		// Calculate metrics for individual authorized teams
		const teams = authorizedItems.map((item) => {
			const cleanCode = item.teamCode.toUpperCase().trim();
			const assignment = scope.teamAssignments.get(cleanCode);
			const isSelf = assignment?.isSelf ?? scope.myDirectTeams.includes(cleanCode);

			return calculatePerformanceMetrics(
				{
					teamCode: item.teamCode,
					teamName: item.teamName,
					respCenter: item.respCenter,
					target: item.existingTarget || 0,
					currentSale: item.currentSale || 0,
					assignedPersonName: assignment?.personName,
					assignedPersonCode: assignment?.personCode,
					roleTitle: assignment?.roleName,
					isSelf
				},
				workingDays
			);
		});

		// Aggregate for team summary (strictly across authorized teams)
		const totalTarget = teams.reduce((acc, t) => acc + t.target, 0);
		const totalCurrentSale = teams.reduce((acc, t) => acc + t.currentSale, 0);

		const teamSummaryName = scope.isRestricted
			? scope.subordinateCodes.length > 0
				? 'Team Combined Total'
				: 'My Territory Total'
			: options.respCenter === 'ALL'
				? 'All Centers Combined'
				: `${options.respCenter || 'Center'} Total`;

		const teamSummary = calculatePerformanceMetrics(
			{
				teamCode: 'TEAM_AGGREGATE',
				teamName: teamSummaryName,
				respCenter: options.respCenter || 'ALL',
				target: totalTarget,
				currentSale: totalCurrentSale
			},
			workingDays
		);

		// Find "My" team
		let myPerformance: SalesPerformanceMetrics | null = null;
		if (options.preferredTeamCode) {
			myPerformance =
				teams.find((t) => t.teamCode.toUpperCase() === options.preferredTeamCode?.toUpperCase()) || null;
		}

		// Prefer the team assigned directly to the user (isSelf)
		if (!myPerformance) {
			myPerformance = teams.find((t) => t.isSelf) || null;
		}

		// Fallback to the first team with an active target, or first team with sales, or first item
		if (!myPerformance && teams.length > 0) {
			myPerformance =
				teams.find((t) => t.target > 0 && t.currentSale > 0) ||
				teams.find((t) => t.target > 0) ||
				teams[0];
		}

		const responseData: SalesPerformanceTrackerData = {
			workingDays,
			respCenter: options.respCenter || 'BEL',
			isRestricted: scope.isRestricted,
			accessibleTeamsCount: teams.length,
			authorizedScope: scope,
			myPerformance,
			teamSummary,
			teams
		};

		// Save to cache
		dataCache.set(cacheKey, { data: responseData, timestamp: Date.now() });

		return {
			success: true,
			data: responseData
		};
	} catch (e: any) {
		if (e.name === 'AbortError') return { success: false };
		return { success: false, error: e.message || 'Error fetching sales performance data.' };
	}
}

/**
 * Format number in Indian currency format.
 */
export function formatINR(val: number | null | undefined): string {
	if (val == null || isNaN(val)) return '₹0';
	return new Intl.NumberFormat('en-IN', {
		style: 'currency',
		currency: 'INR',
		maximumFractionDigits: 0
	}).format(Math.round(val));
}

/**
 * Format in Lakhs (e.g. "12.50 L").
 */
export function formatLakhs(val: number | null | undefined): string {
	if (val == null || isNaN(val)) return '0.00 L';
	const inLakhs = val / 100000;
	return `${inLakhs.toFixed(2)} L`;
}

/**
 * Format short amount in K / Lakhs (e.g. "44.4k" or "1.25L").
 */
export function formatCompact(val: number | null | undefined): string {
	if (val == null || isNaN(val)) return '₹0';
	const abs = Math.abs(val);
	const sign = val < 0 ? '-' : '';
	if (abs >= 10000000) return `${sign}${(abs / 10000000).toFixed(2)} Cr`;
	if (abs >= 100000) return `${sign}${(abs / 100000).toFixed(1)} L`;
	if (abs >= 1000) return `${sign}${(abs / 1000).toFixed(1)}k`;
	return `${sign}₹${Math.round(abs)}`;
}
