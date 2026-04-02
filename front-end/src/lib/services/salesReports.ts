/**
 * Sales report REST API client.
 * Report meta: Tyresoles.Web ReportsController.GetSalesReportMeta (GET api/reports/sales/meta).
 * Report generation: POST api/reports/sales.
 */

import { getBackendBaseUrl } from "$lib/config/system";
import { getAuthToken } from "$lib/stores/auth";

/** Report metadata from GET api/reports/sales/meta (ReportsController.GetSalesReportMeta). */
export interface ReportMeta {
  id: number;
  code: string;
  name: string;
  datePreset: string;
  typeOptions: string[];
  viewOptions: string[];
  showType: boolean;
  showView: boolean;
  showCustomers: boolean;
  showDealers: boolean;
  showAreas: boolean;
  showRegions: boolean;
  showRespCenters: boolean;
  showNos: boolean;
  outputFormats?: string;
  requiredFields?: string;
}

/** Request body for all sales report endpoints. */
export interface ReportFetchParams {
  reportName?: string;
  reportOutput?: string;
  type?: string;
  view?: string;
  from: string; // ISO date
  to: string; // ISO date
  customers?: string[];
  dealers?: string[];
  areas?: string[];
  regions?: string[];
  respCenters?: string[];
  nos?: string[];
  entityCode?: string;
  entityType?: string;
  entityDepartment?: string;
  userSpecialToken?: string;
}

/** Report slug -> endpoint path (data and export). */
export const SALES_REPORT_ENDPOINTS = {
  "sales-and-balance": {
    data: "reports/sales-and-balance",
    export: "reports/sales-and-balance/export",
  },
  "payment-collection": {
    data: "reports/payment-collection",
    export: "reports/payment-collection/export",
  },
  "below-base-price-sales": {
    data: "reports/below-base-price-sales",
    export: "reports/below-base-price-sales/export",
  },
  "tyre-sales": {
    data: "reports/tyre-sales",
    export: "reports/tyre-sales/export",
  },
} as const;

export type SalesReportSlug = keyof typeof SALES_REPORT_ENDPOINTS | string;

/** Export format (ReportOutput). Default PDF. */
export type ExportFormat = "PDF" | "Excel" | "Word";

/** Normalize to backend ReportOutput: PDF, Excel, Word. */
function toReportOutput(format: string | undefined): string {
  if (!format) return "PDF";
  const f = format.toString().toUpperCase();
  if (f === "EXCEL" || f === "XLSX" || f === "XLS") return "Excel";
  if (f === "WORD" || f === "DOCX" || f === "DOC") return "Word";
  return "PDF";
}

function authHeaders(): HeadersInit {
  const token = getAuthToken();
  return {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };
}

/**
 * Fetch sales report metadata (ReportsController.GetSalesReportMeta).
 * @param reports Optional comma-separated report names to filter by.
 */
export async function getSalesReportMeta(reports?: string | null): Promise<ReportMeta[]> {
  const base = getBackendBaseUrl();
  const reportsBase = `${base}/api/reports`;
  const url = new URL(`${reportsBase}/sales/meta`);
  if (reports?.trim()) url.searchParams.set("reports", reports.trim());
  const res = await fetch(url.toString(), { headers: authHeaders() });
  if (!res.ok) {
    const text = await res.text();
    const err = new Error(text || `HTTP ${res.status}`);
    (err as any).status = res.status;
    throw err;
  }
  return res.json() as Promise<ReportMeta[]>;
}

/**
 * Fetch report data (JSON).
 */
export async function fetchReportData<T = unknown>(
  slug: SalesReportSlug,
  params: ReportFetchParams,
): Promise<T> {
  const base = getBackendBaseUrl();
  const salesBase = `${base}/api/sales`;
  const reportsBase = `${base}/api/reports`;
  const endpoint = (SALES_REPORT_ENDPOINTS as any)[slug];
  const url = endpoint 
    ? `${salesBase}/${endpoint.data}` 
    : `${reportsBase}/sales`; // Fallback to unified if not in slug list

  const body = endpoint ? params : { ...params, reportName: slug };

  const res = await fetch(url, {
    method: "POST",
    headers: authHeaders(),
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const text = await res.text();
    const err = new Error(
      res.status === 400 ? text || "Bad request" : text || `HTTP ${res.status}`,
    );
    (err as any).status = res.status;
    throw err;
  }
  return res.json() as Promise<T>;
}

/**
 * Export report as file (PDF, Excel, Word). Uses params.reportOutput (or format override); default PDF.
 * Returns blob and suggested filename from Content-Disposition or default.
 */
export async function exportReport(
  slug: SalesReportSlug,
  params: ReportFetchParams,
  format?: string,
): Promise<{ blob: Blob; fileName: string }> {
  const base = getBackendBaseUrl();
  const salesBase = `${base}/api/sales`;
  const reportsBase = `${base}/api/reports`;
  const endpoint = (SALES_REPORT_ENDPOINTS as any)[slug];
  const url = endpoint 
    ? `${salesBase}/${endpoint.export}` 
    : `${reportsBase}/sales`;

  const reportOutput = toReportOutput(format ?? params.reportOutput);
  const body = endpoint 
    ? { ...params, reportOutput } 
    : { ...params, reportName: slug, reportOutput };

  const res = await fetch(url, {
    method: "POST",
    headers: authHeaders(),
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    const json = await res.json().catch(() => ({}));
    const msg = json.error || json.message || `Export failed (HTTP ${res.status})`;
    const err = new Error(msg);
    (err as any).status = res.status;
    throw err;
  }
  const blob = await res.blob();
  const disposition = res.headers.get("Content-Disposition");
  const ext = reportOutput === "Excel" ? "xlsx" : reportOutput === "Word" ? "docx" : "pdf";
  let fileName = `${slug.replace(/\s+/g, '_')}_${new Date().toISOString().slice(0, 10)}.${ext}`;
  if (disposition) {
    const match = /filename="?([^";\n]+)"?/.exec(disposition);
    if (match?.[1]) fileName = match[1].trim();
  }
  return { blob, fileName };
}
