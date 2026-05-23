import { secureFetch } from "$lib/services/api";
import type { ProcurementRow } from "./types";

export type FetchProcurementParams = {
  from: string;
  to: string;
};

export async function fetchProcurement(
  params: FetchProcurementParams,
  signal?: AbortSignal,
): Promise<
  | { success: true; rows: ProcurementRow[] }
  | { success: false; error: string }
> {
  try {
    const body: Record<string, unknown> = {
      reportName: "Procurement",
      from: params.from,
      to: params.to,
    };

    const res = await secureFetch("/api/dashboard/procurement", {
      method: "POST",
      body: JSON.stringify(body),
      signal,
    });

    if (!res.ok) {
      let msg =
        res.status === 401
          ? "Session expired"
          : `Server error: ${res.status}`;
      try {
        const j = (await res.json()) as { error?: string; message?: string };
        msg = j.error || j.message || msg;
      } catch {
        /* ignore */
      }
      return { success: false, error: msg };
    }

    const data = (await res.json()) as unknown;
    const rows = Array.isArray(data) ? (data as ProcurementRow[]) : [];
    return { success: true, rows };
  } catch (err: unknown) {
    const e = err as { name?: string; message?: string };
    if (e?.name === "AbortError") {
      return { success: false, error: "" };
    }
    return {
      success: false,
      error: e?.message || "Network error",
    };
  }
}
