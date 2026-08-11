import { secureFetch } from "$lib/services/api";
import type { OutstandingRow, FetchOutstandingParams } from "./types";

export async function fetchOutstandingData(
  params: FetchOutstandingParams = {},
  signal?: AbortSignal
): Promise<{ success: true; rows: OutstandingRow[] } | { success: false; error: string }> {
  try {
    const body: Record<string, unknown> = {
      reportName: "Outstanding",
      to: params.asOfDate,
      search: params.search?.trim() || undefined,
      regions: params.region && params.region !== "ALL" ? [params.region] : undefined,
      product: params.product && params.product !== "ALL" ? params.product : undefined,
      type: params.product && params.product !== "ALL" ? params.product : undefined,
      respCenters: params.respCenters?.length ? params.respCenters : undefined,
    };

    const res = await secureFetch("/api/dashboard/outstanding", {
      method: "POST",
      body: JSON.stringify(body),
      signal,
    });

    if (!res.ok) {
      let msg = res.status === 401 ? "Session expired" : `Server error: ${res.status}`;
      try {
        const j = (await res.json()) as { error?: string; message?: string };
        msg = j.error || j.message || msg;
      } catch {
        /* ignore */
      }
      return { success: false, error: msg };
    }

    const json = (await res.json()) as unknown;

    let rows: OutstandingRow[] = [];
    if (Array.isArray(json)) {
      rows = json as OutstandingRow[];
    } else if (json && typeof json === "object") {
      const obj = json as Record<string, unknown>;
      if (Array.isArray(obj.rows)) {
        rows = obj.rows as OutstandingRow[];
      } else if (Array.isArray(obj.data)) {
        rows = obj.data as OutstandingRow[];
      }
    }

    return { success: true, rows };
  } catch (err: unknown) {
    const e = err as { name?: string; message?: string };
    if (e?.name === "AbortError") {
      return { success: false, error: "" };
    }
    return {
      success: false,
      error: e?.message || "Network error while connecting to live API",
    };
  }
}
