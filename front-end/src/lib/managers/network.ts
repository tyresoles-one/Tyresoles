import { secureFetch } from "$lib/services/api";

export type ApiResult<T = unknown> = {
	success: boolean;
	data?: T;
	error?: string;
};

function normalizeBody(body: RequestInit["body"]): BodyInit | undefined {
	if (body == null) return undefined;
	if (typeof body === "string" || body instanceof FormData || body instanceof Blob)
		return body;
	if (body instanceof ArrayBuffer) return body;
	if (body instanceof URLSearchParams) return body;
	return JSON.stringify(body);
}

export type ApiFetchOptions = Omit<RequestInit, "body"> & { body?: unknown };

export async function apiFetch<T = unknown>(
	url: string,
	options: ApiFetchOptions = {}
): Promise<ApiResult<T>> {
	try {
		const res = await secureFetch(url, {
			...options,
			body: normalizeBody(options.body as RequestInit["body"])
		});
		const ct = res.headers.get("content-type") ?? "";
		let parsed: unknown = null;
		const text = await res.text();
		if (ct.includes("application/json") || text.trim().startsWith("{")) {
			try {
				parsed = text ? JSON.parse(text) : null;
			} catch {
				parsed = { raw: text };
			}
		} else {
			parsed = text;
		}
		if (!res.ok) {
			const p = parsed as Record<string, unknown> | null;
			const err =
				(typeof p?.message === "string" && p.message) ||
				(typeof p?.error === "string" && p.error) ||
				res.statusText;
			return { success: false, error: err };
		}
		if (parsed && typeof parsed === "object" && "success" in (parsed as object)) {
			const o = parsed as { success?: boolean; data?: T; error?: string };
			return {
				success: o.success !== false,
				data: o.data as T,
				error: o.error
			};
		}
		return { success: true, data: parsed as T };
	} catch (e) {
		return { success: false, error: e instanceof Error ? e.message : String(e) };
	}
}

const api = (path: string) =>
	path.startsWith("/") ? path : `/${path}`;

/** REST paths (backend base URL is applied in `secureFetch`). */
export const endpoints = {
	production: {
		vendors: api("/api/production/vendors"),
		updateVendor: api("/api/production/updateVendor"),
		procOrderLinesDispatch: api("/api/production/procOrderLinesDispatch"),
		newProcShipNo: api("/api/production/newProcShipNo"),
		procShipmentsForMerger: api("/api/production/procShipmentsForMerger"),
		updateProcOrdLinesDispatch: api("/api/production/updateProcOrdLinesDispatch"),
		updateProcOrdLinesDrop: api("/api/production/updateProcOrdLinesDrop"),
		procurementDispatchOrders: api("/api/production/procurementDispatchOrders"),
		updateProcOrdLinesReceipt: api("/api/production/updateProcOrdLinesReceipt"),
		updateProcOrdLinesRemove: api("/api/production/updateProcOrdLinesRemove"),
		generateGRAs: api("/api/production/generateGRAs"),
		updateProcOrdLinesDispatch2: api("/api/production/updateProcOrdLinesDispatch2"),
		procOrderLines: api("/api/production/procOrderLines"),
		procOrderLinesNewNumbering: api("/api/production/procOrderLinesNewNumbering"),
		ecomileLastNewNumber: api("/api/production/ecomileLastNewNumber"),
		itemNos: api("/api/production/itemNos"),
		makes: api("/api/production/makes"),
		inspectorCodeNames: api("/api/production/inspectorCodeNames"),
		procInspection: api("/api/production/procInspection"),
		procMarkets: api("/api/production/procMarkets"),
		insertCasingItems: api("/api/production/insertCasingItems"),
		productionReport: api("/api/reports/production")
	},
	accounts: {
		states: api("/api/accounts/states")
	}
};

export function getURLSearchParams(obj: Record<string, string | undefined>): URLSearchParams {
	const p = new URLSearchParams();
	for (const [k, v] of Object.entries(obj)) {
		if (v !== undefined && v !== null) p.set(k, String(v));
	}
	return p;
}

/** Public IFSC lookup (Razorpay); returns bank name/branch or an error string. */
export async function fetchBankDetails(
	ifsc: string
): Promise<string | { BANK: string; BRANCH: string }> {
	const code = ifsc.trim().toUpperCase();
	if (code.length !== 11) return "Invalid IFSC length";
	try {
		const r = await fetch(`https://ifsc.razorpay.com/${encodeURIComponent(code)}`);
		if (!r.ok) return "Could not resolve IFSC";
		const j = (await r.json()) as { BANK?: string; BRANCH?: string };
		if (!j.BANK && !j.BRANCH) return "Could not resolve IFSC";
		return { BANK: j.BANK ?? "", BRANCH: j.BRANCH ?? "" };
	} catch (e) {
		return e instanceof Error ? e.message : String(e);
	}
}
