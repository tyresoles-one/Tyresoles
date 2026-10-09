export type DashboardTimeframe = 'today' | 'yesterday' | '7d' | 'month' | 'custom';
export type ViewPerspective = 'self' | 'team';

export type CallTargetRule = {
	id?: string;
	agentUsername: string;
	dailyTarget: number;
	weeklyTarget: number;
	monthlyTarget: number;
	isActive?: boolean;
	notes?: string | null;
};

export type CrmContactMini = {
	id: string;
	fullName: string;
	companyName?: string | null;
	mobileNo?: string | null;
	city?: string | null;
	state?: string | null;
	respCenter?: string | null;
	erpCustomerNos?: string | null;
};

export type CallLogRecord = {
	id: string;
	contactId: string;
	callDate: string;
	outcome: string;
	notes?: string | null;
	createdBy: string;
	salesUserId?: string | null;
	invoiceNos?: string | null;
	invoiceAmount?: number | null;
	tyreQuantity?: number | null;
	contact?: CrmContactMini | null;
};

export type KpiMetrics = {
	totalCalls: number;
	connectedCalls: number;
	connectRate: number; // 0 - 100
	positiveCalls: number;
	positiveRate: number; // 0 - 100
	followupCalls: number;
	unreachableCalls: number;
	uniqueContacts: number;
	revenueGenerated: number;
	tyresConverted: number;
	targetCalls: number;
	attainmentPct: number; // 0 - 100+
	paceProjectedCalls: number;
	paceStatus: 'ahead' | 'on_track' | 'behind';
	streakDays: number;
	rank?: number | null;
	totalRepsCount?: number;
};

export type ActivityBucket = {
	label: string;
	subLabel: string;
	dateKey: string;
	isCurrent: boolean;
	calls: number;
	connected: number;
	positive: number;
	unreachable: number;
	revenue: number;
};

export type FunnelStage = {
	key: string;
	label: string;
	count: number;
	conversionFromPrevious: number; // 0 - 100
	conversionFromTotal: number; // 0 - 100
	color: string;
	icon: string;
};

export type OutcomeStat = {
	outcome: string;
	count: number;
	percentage: number;
	category: 'positive' | 'followup' | 'unreachable' | 'neutral' | 'lost';
	color: string;
};

export type SubordinateRep = {
	username: string;
	cleanUsername: string;
	displayName: string;
	employeeNo?: string;
	managerNo?: string;
	jobTitle?: string;
	isDirectReport: boolean;
	totalCalls: number;
	connectedCalls: number;
	connectRate: number;
	positiveCalls: number;
	positiveRate: number;
	revenueGenerated: number;
	activeAllocations: number;
	dailyTarget: number;
	weeklyTarget: number;
	monthlyTarget: number;
	effectiveTarget: number;
	attainmentPct: number;
	streakDays: number;
	rank: number;
	roleType?: number;
	roleName?: string;
	alert?: {
		type: 'danger' | 'warning' | 'info';
		message: string;
	} | null;
};

export type SubordinateSalesperson = {
	code: string;
	name: string;
	roleType: number;
	roleName: string;
	displayTitle: string;
	jobTitle?: string | null;
	mobilePhoneNo?: string | null;
	companyEmail?: string | null;
	sharedTeamsCount: number;
	sharedTeamCodes: string[];
};

export type SalesHierarchySummary = {
	supervisorCode: string;
	supervisorName: string;
	supervisorMaxRoleType: number;
	supervisorRoleName: string;
	supervisorDisplayTitle: string;
	totalSubordinatesCount: number;
	subordinateCodes: string[];
	subordinates: SubordinateSalesperson[];
};

export type FocusActionItem = {
	id: string;
	type: 'reminder' | 'pending_call';
	contactId: string;
	contactName: string;
	companyName?: string | null;
	mobileNo?: string | null;
	timeFormatted: string;
	isOverdue: boolean;
	notes?: string | null;
	outcome?: string | null;
	dateIso: string;
};

export type EmployeeHierarchyItem = {
	no: string;
	firstName: string;
	lastName: string;
	initials: string;
	jobTitle: string;
	managerNo: string;
	salespersPurchCode: string;
	mobilePhoneNo: string;
	eMail: string;
	salesTeamType: number;
	status: number;
};

export type DateRangeBounds = {
	start: Date;
	end: Date;
	label: string;
	compactLabel: string;
	fullLabel: string;
	daysCount: number;
};
