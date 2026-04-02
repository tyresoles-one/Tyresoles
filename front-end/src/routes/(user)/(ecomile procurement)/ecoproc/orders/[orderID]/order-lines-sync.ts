import { get } from "svelte/store";
import { graphqlQuery } from "$lib/services/graphql";
import { ensureFetchParams, fetchParamsStore, orderStore, orderLinesStore } from "$lib/managers/stores";
import type { OrderLine } from "$lib/business/models";
import {
  GetProductionProcurementOrderLinesDocument,
  type GetProductionProcurementOrderLinesQuery,
  type GetProductionProcurementOrderLinesQueryVariables,
  type OrderInfoInput,
} from "$lib/services/graphql/generated/graphql";

/** Same `OrderInfoInput` shape as `fetchLines` on the order page (backend uses `orderNo` only). */
export function orderInfoInputForLinesQuery(orderNo: string): OrderInfoInput {
  return {
    orderNo,
    amount: 0,
    date: "",
    location: "",
    manager: "",
    managerCode: "",
    qty: 0,
    respCenter: "",
    status: 0,
    supplier: "",
    supplierCode: "",
  };
}

export function mapProductionLinesToOrderLines(
  orderId: string,
  lines: NonNullable<GetProductionProcurementOrderLinesQuery["productionProcurementOrderLines"]>,
): OrderLine[] {
  return lines.map((l) => ({
    no: orderId,
    lineNo: l.lineNo,
    itemNo: l.itemNo,
    vendorNo: l.vendorNo,
    make: l.make,
    subMake: l.subMake,
    serialNo: l.serialNo,
    amount: Number(l.amount),
    inspection: l.inspection,
    inspector: l.inspector,
    inspectorCode: l.inspectorCode,
    sortNo: l.sortNo,
  })) as OrderLine[];
}

export function hydrateOrderStoresFromLines(
  orderId: string,
  supplierCode: string,
  lines: NonNullable<GetProductionProcurementOrderLinesQuery["productionProcurementOrderLines"]>,
): void {
  const code = String(supplierCode ?? "").trim();
  orderStore.set({ orderNo: orderId, supplierCode: code });
  orderLinesStore.set(mapProductionLinesToOrderLines(orderId, lines));
}

/**
 * Loads `GetProductionProcurementOrderLines` when stores are empty or belong to another order.
 * Call after header is known so `supplierCode` can be passed (e.g. `buyFromVendorNo`).
 */
export async function ensureOrderLinesStoreForOrder(
  orderId: string,
  supplierCodeHint?: string,
): Promise<{ success: boolean; error?: string }> {
  if (!orderId) return { success: false, error: "Missing order id." };

  const existingOrder = get(orderStore) as { orderNo?: string } | null;
  const existingLines = get(orderLinesStore);
  const firstNo = existingLines?.[0] ? String((existingLines[0] as OrderLine).no ?? "").trim() : "";
  const linesMatchOrder =
    existingLines &&
    existingLines.length > 0 &&
    (firstNo === "" || firstNo === orderId);
  if (existingOrder?.orderNo === orderId && linesMatchOrder) {
    return { success: true };
  }

  ensureFetchParams();
  if (!get(fetchParamsStore)) {
    return { success: false, error: "Please re-login. [Empty Fetch Params]" };
  }

  const res = await graphqlQuery<
    GetProductionProcurementOrderLinesQuery,
    GetProductionProcurementOrderLinesQueryVariables
  >(GetProductionProcurementOrderLinesDocument, {
    variables: { param: orderInfoInputForLinesQuery(orderId) },
    skipLoading: true,
    skipCache: true,
  });

  if (!res.success || !res.data?.productionProcurementOrderLines) {
    return { success: false, error: res.error || "Failed to load order lines." };
  }

  const rows = res.data.productionProcurementOrderLines;
  const supplier =
    String(supplierCodeHint ?? "").trim() ||
    String((get(orderStore) as { supplierCode?: string } | null)?.supplierCode ?? "").trim() ||
    String(rows[0]?.vendorNo ?? "").trim();

  hydrateOrderStoresFromLines(orderId, supplier, rows);
  return { success: true };
}
