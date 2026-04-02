import { getBackendBaseUrl } from "$lib/config/system";
import { getAuthToken } from "$lib/stores/auth";
import { secureFetch } from "$lib/services/api";
import type { DashboardData, FetchParams } from "./types";

export async function fetchDashboard(
  params: FetchParams,
  signal?: AbortSignal,
): Promise<{ success: boolean; data?: DashboardData; error?: string }> {
  try {
    const { reportName, ...restParams } = params;
    const url = `/api/dashboard/${reportName.toLowerCase()}`;

    const res = await secureFetch(url, {
      method: "POST",
      body: JSON.stringify(restParams),
      signal,
    });

    if (!res.ok) {
      return { success: false, error: res.status === 401 ? "Session Expired" : `Server error: ${res.status}` };
    }

    const json = await res.json();

    if (json.success === false) {
      return { success: false, error: json.message || "Request failed" };
    }

    const name = json.name ?? json.Name;
    const data = json.data ?? json.Data;
    if (name !== undefined && Array.isArray(data)) {
      return { success: true, data: { name, data } };
    }
    return { success: true, data: json.data ?? json };
  } catch (err: any) {
    if (err?.name === "AbortError") return { success: false };
    return { success: false, error: err?.message || "Network error" };
  }
}
