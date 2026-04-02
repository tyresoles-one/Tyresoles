import { secureFetch } from "$lib/services/api";
import type { SalesChartParams, MonthlySalesRow } from "./types";

export async function fetchSalesChart(
  params: SalesChartParams,
  signal?: AbortSignal,
): Promise<{ success: boolean; data?: MonthlySalesRow[]; error?: string }> {
  try {
    const url = `/api/dashboard/saleschart`;
    const res = await secureFetch(url, {
      method: "POST",
      body: JSON.stringify(params),
      signal,
    });

    if (!res.ok) {
      return { success: false, error: `Server error: ${res.status}` };
    }

    const json = await res.json();
    const rawData = json.data ?? json.Data ?? json;
    const rows = Array.isArray(rawData) ? rawData.map((raw: any) => ({
      month: String(raw.month ?? raw.Month ?? ""),
      sale: Number(raw.sale ?? raw.Sale ?? 0),
      unit: raw.unit ?? raw.Unit
    })) : [];
    
    return { success: true, data: rows };
  } catch (err: unknown) {
    if (err && typeof err === "object" && "name" in err && (err as { name: string }).name === "AbortError") {
      return { success: false };
    }
    return { success: false, error: err instanceof Error ? err.message : "Network error" };
  }
}
