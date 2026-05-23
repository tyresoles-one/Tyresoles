import { secureFetch } from "$lib/services/api";
import type { ClaimRatioRow } from "./types";

export type FetchClaimRatiosParams = {
  from: string;
  to: string;
  view?: string;
  respCenters?: string[];
};

export async function fetchClaimRatios(
  params: FetchClaimRatiosParams,
  signal?: AbortSignal,
): Promise<
  | { success: true; rows: ClaimRatioRow[] }
  | { success: false; error: string }
> {
  try {
    const body: Record<string, unknown> = {
      reportName: "Claim Ratios",
      from: params.from,
      to: params.to,
      view: params.view || "Claim Ratios",
    };
    if (params.respCenters?.length) {
      body.respCenters = params.respCenters;
    }

    const res = await secureFetch("/api/dashboard/claimratios", {
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
    const rows = Array.isArray(data) ? (data as ClaimRatioRow[]) : [];
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
