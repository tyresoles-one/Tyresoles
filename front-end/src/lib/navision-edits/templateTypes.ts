/** Mirrors backend FieldsTemplate / FieldDef / ApprovalDef / ConditionDef (NavEditService.cs). */

export type ConditionOp = 'eq' | 'neq' | 'in' | 'nin';

export interface NavEditConditionDef {
  column: string;
  op: ConditionOp | '';
  /** Used when op is eq or neq */
  value?: string;
  /** Used when op is in or nin */
  values?: string[];
}

export interface NavEditApprovalStep {
  userIds: string[];
  when: NavEditConditionDef[];
}

export interface NavEditFieldDef {
  column: string;
  label: string;
  type: string;
  options?: string[];
}

/** How to render this column in lookup results (JSON `displayColumns`). */
export type NavEditDisplayColumnFormat = 'date' | 'number' | '';

export interface NavEditDisplayColumnSpec {
  column: string;
  /** Empty / omitted = plain text. */
  format?: NavEditDisplayColumnFormat;
}

/** Static filters ANDed onto every find-record Nav query (before user search). */
export type NavEditLookupFilterOp =
  | 'eq'
  | 'neq'
  | 'in'
  | 'nin'
  | 'contains'
  | 'notcontains'
  | 'starts'
  | 'ends'
  | 'isnull'
  | 'isnotnull';

export interface NavEditLookupFilterCondition {
  column: string;
  op: NavEditLookupFilterOp | '';
  /** Single value (eq, neq, contains, …) */
  value?: string;
  /** List values (in / nin) */
  values?: string[];
}

/** NAV WebServe Req* when admin marks a request processed (server-side). */
export type NavEditConnectorProcess = 'none' | 'reqCustEdit' | 'reqUserSetup' | 'reqGlEntry';

/** One logical Req* parameter key mapped to a Nav column name in `connectorParamColumns` (server: ResolveConnectorParam). */
export interface NavEditConnectorParamRow {
  key: string;
  label: string;
  hint?: string;
}

/** UI rows for the template designer (keys must match backend NavEditService.ResolveConnectorParam). */
export const CONNECTOR_PARAM_ROWS: Record<
  Exclude<NavEditConnectorProcess, 'none'>,
  NavEditConnectorParamRow[]
> = {
  reqCustEdit: [
    {
      key: 'customerNo',
      label: 'Customer No.',
      hint: 'Usually the record key; set only if your Nav column differs.',
    },
    { key: 'dealerCode', label: 'Dealer code' },
    { key: 'areaCode', label: 'Area code' },
  ],
  reqUserSetup: [
    { key: 'userid', label: 'User ID', hint: 'Often the record key.' },
    { key: 'respCenter', label: 'Responsibility center' },
    { key: 'fromDate', label: 'From date' },
    { key: 'toDate', label: 'To date' },
  ],
  reqGlEntry: [
    { key: 'entryNo', label: 'Entry No.' },
    { key: 'glAccountNo', label: 'G/L account No.' },
    { key: 'respCenter', label: 'Responsibility center' },
    { key: 'amount', label: 'Amount' },
    { key: 'postingDate', label: 'Posting date' },
  ],
};

export interface NavEditFieldsTemplate {
  itAdminUserId: string;
  /** When not `none`, the API calls the matching NAV WebServe Req* method before marking processed. */
  connectorProcess: NavEditConnectorProcess;
  /** Optional: logical param name (e.g. <c>dealerCode</c>) → Nav column name in submitted changes. */
  connectorParamColumns: Record<string, string>;
  fields: NavEditFieldDef[];
  displayColumns: NavEditDisplayColumnSpec[];
  /** Nav columns used for find-record search (substring OR). Empty = primary key only. */
  searchColumns: string[];
  /** Fixed table filters (AND). Applied in addition to the live search box. */
  lookupFilters: NavEditLookupFilterCondition[];
  /** When true, users can request a new row (new PK) and optionally copy field values from an existing row. */
  allowNewRecordCreate: boolean;
  approvals: NavEditApprovalStep[];
}

export function defaultEmptyTemplate(): NavEditFieldsTemplate {
  return {
    itAdminUserId: '',
    connectorProcess: 'none',
    connectorParamColumns: {},
    fields: [{ column: '', label: '', type: 'text' }],
    displayColumns: [],
    searchColumns: [],
    lookupFilters: [],
    allowNewRecordCreate: false,
    approvals: [],
  };
}

function normalizeField(raw: unknown): NavEditFieldDef {
  if (!raw || typeof raw !== 'object') return { column: '', label: '', type: 'text' };
  const r = raw as Record<string, unknown>;
  const options = Array.isArray(r.options)
    ? r.options.map(v => String(v ?? '')).filter(Boolean)
    : undefined;
  return {
    column: String(r.column ?? ''),
    label: String(r.label ?? ''),
    type: String(r.type ?? 'text') || 'text',
    ...(options?.length ? { options } : {}),
  };
}

function normalizeCondition(raw: unknown): NavEditConditionDef {
  if (!raw || typeof raw !== 'object') return { column: '', op: '', value: '', values: [] };
  const r = raw as Record<string, unknown>;
  const opRaw = String(r.op ?? 'eq').toLowerCase();
  const op = (['eq', 'neq', 'in', 'nin'].includes(opRaw) ? opRaw : 'eq') as ConditionOp;
  const values = Array.isArray(r.values) ? r.values.map(v => String(v ?? '')) : [];
  return {
    column: String(r.column ?? ''),
    op,
    value: r.value != null ? String(r.value) : '',
    values,
  };
}

function normalizeApproval(raw: unknown): NavEditApprovalStep {
  if (!raw || typeof raw !== 'object') return { userIds: [], when: [] };
  const r = raw as Record<string, unknown>;
  const userIds = Array.isArray(r.userIds)
    ? r.userIds.map(u => String(u ?? '').trim()).filter(Boolean)
    : [];
  const when = Array.isArray(r.when) ? r.when.map(normalizeCondition) : [];
  return { userIds, when };
}

const LOOKUP_FILTER_OPS: NavEditLookupFilterOp[] = [
  'eq',
  'neq',
  'in',
  'nin',
  'contains',
  'notcontains',
  'starts',
  'ends',
  'isnull',
  'isnotnull',
];

function normalizeLookupFilter(raw: unknown): NavEditLookupFilterCondition {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
    return { column: '', op: 'eq', value: '', values: [] };
  }
  const r = raw as Record<string, unknown>;
  const opRaw = String(r.op ?? 'eq').toLowerCase();
  const op = (LOOKUP_FILTER_OPS.includes(opRaw as NavEditLookupFilterOp)
    ? opRaw
    : 'eq') as NavEditLookupFilterOp;
  const values = Array.isArray(r.values) ? r.values.map((v) => String(v ?? '')) : [];
  return {
    column: String(r.column ?? ''),
    op,
    value: r.value != null ? String(r.value) : '',
    values,
  };
}

function normalizeDisplayColumn(raw: unknown): NavEditDisplayColumnSpec {
  if (typeof raw === 'string') {
    return { column: raw.trim(), format: '' };
  }
  if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
    const r = raw as Record<string, unknown>;
    const col = String(r.column ?? '').trim();
    const f = String(r.format ?? '').toLowerCase();
    const format: NavEditDisplayColumnFormat =
      f === 'date' || f === 'number' ? (f as NavEditDisplayColumnFormat) : '';
    return format ? { column: col, format } : { column: col, format: '' };
  }
  return { column: '', format: '' };
}

function normalizeTemplate(o: Record<string, unknown>): NavEditFieldsTemplate {
  const fieldsRaw = Array.isArray(o.fields) ? o.fields : [];
  const fields = fieldsRaw.length > 0 ? fieldsRaw.map(normalizeField) : [{ column: '', label: '', type: 'text' }];

  const displayRaw = Array.isArray(o.displayColumns) ? o.displayColumns : [];
  const displayColumns = displayRaw.map(normalizeDisplayColumn);

  const searchRaw = Array.isArray(o.searchColumns) ? o.searchColumns : [];
  const searchColumns = searchRaw
    .map((x) => String(x ?? '').trim())
    .filter(Boolean);

  const lookupRaw = Array.isArray(o.lookupFilters) ? o.lookupFilters : [];
  const lookupFilters = lookupRaw.map(normalizeLookupFilter);

  const approvalsRaw = Array.isArray(o.approvals) ? o.approvals : [];
  const approvals = approvalsRaw.map(normalizeApproval);

  const cpRaw = o.connectorProcess;
  const connectorProcess: NavEditConnectorProcess =
    cpRaw === 'reqCustEdit' || cpRaw === 'reqUserSetup' || cpRaw === 'reqGlEntry' ? cpRaw : 'none';

  let connectorParamColumns: Record<string, string> = {};
  if (o.connectorParamColumns != null && typeof o.connectorParamColumns === 'object' && !Array.isArray(o.connectorParamColumns)) {
    for (const [k, v] of Object.entries(o.connectorParamColumns as Record<string, unknown>)) {
      if (typeof v === 'string' && v.trim()) connectorParamColumns[k] = v.trim();
    }
  }

  return {
    itAdminUserId: o.itAdminUserId != null ? String(o.itAdminUserId) : '',
    connectorProcess,
    connectorParamColumns,
    fields,
    displayColumns,
    searchColumns,
    lookupFilters,
    allowNewRecordCreate: o.allowNewRecordCreate === true,
    approvals,
  };
}

export function parseTemplate(json: string): { ok: true; data: NavEditFieldsTemplate } | { ok: false; error: string } {
  let raw: unknown;
  try {
    raw = JSON.parse(json);
  } catch (e) {
    return { ok: false, error: e instanceof Error ? e.message : 'Invalid JSON' };
  }
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
    return { ok: false, error: 'Root must be a JSON object' };
  }
  return { ok: true, data: normalizeTemplate(raw as Record<string, unknown>) };
}

/** Serialize condition for backend JSON (omit empty noise). */
function serializeCondition(c: NavEditConditionDef): Record<string, unknown> {
  const op = (c.op || 'eq').toLowerCase();
  const column = c.column.trim();
  if (!column) return {};
  if (op === 'in' || op === 'nin') {
    const values = (c.values ?? []).map(v => String(v).trim()).filter(Boolean);
    return { column, op, values };
  }
  return { column, op: op || 'eq', value: c.value ?? '' };
}

function serializeLookupFilter(f: NavEditLookupFilterCondition): Record<string, unknown> | null {
  const column = f.column.trim();
  if (!column) return null;
  const op = (f.op || 'eq').toLowerCase() as NavEditLookupFilterOp;
  if (op === 'in' || op === 'nin') {
    const values = (f.values ?? []).map((v) => String(v).trim()).filter(Boolean);
    if (values.length === 0) return null;
    return { column, op, values };
  }
  if (op === 'isnull' || op === 'isnotnull') return { column, op };
  const value = f.value ?? '';
  if (
    (op === 'contains' || op === 'notcontains' || op === 'starts' || op === 'ends') &&
    !value.trim()
  ) {
    return null;
  }
  return { column, op, value };
}

function serializeApproval(a: NavEditApprovalStep): Record<string, unknown> {
  const userIds = a.userIds.map(u => u.trim()).filter(Boolean);
  const when = a.when.map(serializeCondition).filter(o => Object.keys(o).length > 0);
  return { userIds, ...(when.length ? { when } : {}) };
}

function serializeField(f: NavEditFieldDef): Record<string, unknown> {
  const o: Record<string, unknown> = {
    column: f.column,
    label: f.label,
    type: f.type || 'text',
  };
  if (f.options?.length) o.options = f.options;
  return o;
}

/** Emit compact JSON: plain string when no format, object when format is set. */
function serializeDisplayColumn(s: NavEditDisplayColumnSpec): string | Record<string, unknown> {
  const column = s.column.trim();
  const fmt = (s.format ?? '').trim().toLowerCase();
  if (fmt === 'date' || fmt === 'number') {
    return { column, format: fmt };
  }
  return column;
}

export function serializeTemplate(data: NavEditFieldsTemplate): string {
  const displayColumns = data.displayColumns
    .map(serializeDisplayColumn)
    .filter((item) => (typeof item === 'string' ? item.length > 0 : String((item as Record<string, unknown>).column ?? '').trim().length > 0));
  const lookupFilters = data.lookupFilters
    .map(serializeLookupFilter)
    .filter((o): o is Record<string, unknown> => o != null);
  const obj: Record<string, unknown> = {
    itAdminUserId: data.itAdminUserId,
    ...(data.connectorProcess && data.connectorProcess !== 'none' ? { connectorProcess: data.connectorProcess } : {}),
    ...(Object.keys(data.connectorParamColumns).length > 0 ? { connectorParamColumns: data.connectorParamColumns } : {}),
    fields: data.fields.map(serializeField),
    displayColumns,
    searchColumns: data.searchColumns.map((c) => c.trim()).filter(Boolean),
    lookupFilters,
    ...(data.allowNewRecordCreate ? { allowNewRecordCreate: true } : {}),
    approvals: data.approvals.map(serializeApproval),
  };
  return JSON.stringify(obj, null, 2);
}

/**
 * Client validation before save: if any approval step exists with no userIds, reject.
 * Empty approvals array is allowed (no approval chain).
 */
export function validateTemplateForSave(data: NavEditFieldsTemplate): string | null {
  for (let i = 0; i < data.approvals.length; i++) {
    const step = data.approvals[i];
    const ids = step.userIds.map(u => u.trim()).filter(Boolean);
    if (ids.length === 0) {
      return `Approval step ${i + 1}: add at least one user ID, or remove all approval steps.`;
    }
  }
  return null;
}
