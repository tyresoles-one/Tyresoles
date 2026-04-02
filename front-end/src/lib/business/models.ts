/** Session / API context for ecoproc REST calls (legacy Tyresoles shape). */
export type FetchParams = {
	respCenters?: string[];
	userDepartment?: string;
	userCode?: string;
	userName?: string;
	/** E.g. ECOMGR — used by production procurement GraphQL / legacy SOAP. */
	userSpecialToken?: string;
	regions?: string[];
	view?: string;
	type?: string;
	[key: string]: unknown;
};

export type CodeName = { code: string; name: string };

/** Vendor row used by ecoproc `Form` (legacy field names). */
export type Vendor = {
	no: string;
	name?: string;
	address?: string;
	address2?: string;
	city?: string;
	stateCode?: string;
	/** Market / territory (maps from `territoryCode` in GraphQL). */
	detail?: string;
	mobileNo?: string;
	bankIFSC?: string;
	bankName?: string;
	bankAccNo?: string;
	bankBranch?: string;
	panNo?: string;
	adhaarNo?: string;
	[key: string]: unknown;
};

/** Minimal order line for procurement UIs (extend as needed). */
export type OrderLine = {
	orderNo?: string;
	lineNo?: number;
	no?: string;
	sortNo?: string;
	date?: Date | string;
	vendorNo?: string;
	itemNo?: string;
	serialNo?: string;
	make?: string;
	subMake?: string;
	amount?: number;
	inspection?: string;
	inspector?: string;
	inspectorCode?: string;
	[key: string]: unknown;
};

export type OrderLineDispatch = Record<string, unknown>;
export type OrderHeader = Record<string, unknown>;
export type ShipmentInfo = Record<string, unknown>;
