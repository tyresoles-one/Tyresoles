import { writable, get, type Writable } from "svelte/store";
import { authStore } from "$lib/stores/auth";
import type {
	CodeName,
	FetchParams,
	OrderLine,
	Vendor
} from "$lib/business/models";

export const fetchParamsStore: Writable<FetchParams | null> = writable(null);
export const vendorStore: Writable<Vendor[] | null> = writable(null);
export const loadingStore = writable(false);
export const statesStore: Writable<CodeName[] | null> = writable(null);
export const marketsStore: Writable<CodeName[] | null> = writable(null);
export const itemsStore: Writable<any[] | null> = writable(null);
export const makesStore: Writable<CodeName[] | null> = writable(null);
export const inspectorsStore: Writable<CodeName[] | null> = writable(null);
export const procInspectionsStore: Writable<CodeName[] | null> = writable(null);
export const settingsStore = writable<Record<string, unknown>>({});
export const orderStore = writable<Record<string, unknown> | null>(null);
export const orderLinesStore: Writable<OrderLine[] | null> = writable(null);
export const postedOrderStore = writable<unknown>(null);

/** Populate `fetchParamsStore` once from persisted login user (ecoproc REST expects this). */
export function ensureFetchParams(): void {
	if (get(fetchParamsStore)) return;
	const { user } = get(authStore);
	if (!user) return;
	fetchParamsStore.set({
		respCenters: [user.respCenter],
		userDepartment: user.department,
		userCode: user.userId,
		userName: user.fullName ?? "",
		userSpecialToken: user.userSpecialToken ?? ""
	});
}
