/**
 * Production report REST API client.
 * Report meta: Tyresoles.Web ReportsController.GetProductionReportMeta (GET api/reports/production/meta).
 * Report generation: POST api/reports/production.
 */

import { getBackendBaseUrl } from "$lib/config/system";
import { getAuthToken } from "$lib/stores/auth";
import type { ReportMeta, ReportFetchParams, ExportFormat } from "./salesReports";

export type ProductionReportSlug = string;

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
 * Fetch production report metadata (ReportsController.GetProductionReportMeta).
 */
export async function getProductionReportMeta(userId?: string | null): Promise<ReportMeta[]> {
  const base = getBackendBaseUrl();
  const reportsBase = `${base}/api/reports`;
  const url = new URL(`${reportsBase}/production/meta`);
  if (userId?.trim()) url.searchParams.set("userId", userId.trim());
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
 * Export production report as file (PDF, Excel, Word).
 */
export async function exportProductionReport(
  slug: ProductionReportSlug,
  params: ReportFetchParams,
  format?: string,
): Promise<{ blob: Blob; fileName: string }> {
  const base = getBackendBaseUrl();
  const reportsBase = `${base}/api/reports`;
  const url = `${reportsBase}/production`;

  const reportOutput = toReportOutput(format ?? params.reportOutput);
  const body = { ...params, reportName: slug, reportOutput };

  const res = await fetch(url, {
    method: "POST",
    headers: authHeaders(),
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    let msg = `Export failed (HTTP ${res.status})`;
    try {
      const json = await res.json();
      msg = json.error || json.message || msg;
    } catch {
      // ignore
    }
    const err = new Error(msg);
    (err as any).status = res.status;
    throw err;
  }
  const blob = await res.blob();
  const disposition = res.headers.get("Content-Disposition");
  const ext = reportOutput === "Excel" ? "xlsx" : reportOutput === "Word" ? "docx" : "pdf";
  let fileName = `${slug.replace(/\s+/g, "_")}_${new Date().toISOString().slice(0, 10)}.${ext}`;
  if (disposition) {
    const match = /filename="?([^";\n]+)"?/.exec(disposition);
    if (match?.[1]) fileName = match[1].trim();
  }
  return { blob, fileName };
}
