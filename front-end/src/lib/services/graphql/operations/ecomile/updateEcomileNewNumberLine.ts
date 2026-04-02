import { parse } from "graphql";
import type { TypedDocumentNode } from "@graphql-typed-document-node/core";
import type {
  OrderLineDispatchInput,
  ProcurementNewNumberingDto,
} from "$lib/services/graphql/generated/graphql";

/**
 * Maps a new-numbering row to SOAP UpdateProcurementOrderLine2 / OrderLineDispatchInput.
 * All string fields default to "" to satisfy non-null GraphQL inputs.
 */
export function procurementNewNumberingToOrderLineDispatchInput(
  line: ProcurementNewNumberingDto,
): OrderLineDispatchInput {
  const s = (v: unknown) => (v == null ? "" : typeof v === "string" ? v : String(v));
  const d = (v: unknown) => {
    if (v == null || v === "") return "";
    if (typeof v === "string") return v;
    try {
      return new Date(v as string).toISOString();
    } catch {
      return String(v);
    }
  };
  return {
    orderNo: s(line.orderNo),
    lineNo: line.lineNo,
    no: s(line.no),
    make: s(line.make),
    serialNo: s(line.serialNo),
    dispatchOrderNo: s(line.dispatchOrderNo),
    dispatchDate: d(line.dispatchDate),
    dispatchDestination: s(line.dispatchDestination),
    dispatchVehicleNo: s(line.dispatchVehicleNo),
    dispatchMobileNo: s(line.dispatchMobileNo),
    dispatchTransporter: s(line.dispatchTransporter),
    button: s(line.button),
    model: s(line.model),
    factInspection: s(line.factInspection),
    newSerialNo: s(line.newSerialNo),
    rejectionReason: s(line.rejectionReason),
    supplier: s(line.supplier),
    location: s(line.location),
    date: d(line.date),
    sortNo: s(line.sortNo),
    inspection: s(line.inspection),
    orderStatus: s(line.orderStatus),
    inspector: s(line.inspector),
    factInspector: s(line.factInspector),
    factInspectorFinal: s(line.factInspectorFinal),
    remark: s(line.remark),
  };
}

export type UpdateEcomileNewNumberLineMutation = {
  updateEcomileNewNumberLine: number;
};

export type UpdateEcomileNewNumberLineMutationVariables = {
  line: OrderLineDispatchInput;
};

/** Calls Tyresoles.Web Mutation.UpdateEcomileNewNumberLine → UpdateProcurementOrderLine2Async. */
export const UpdateEcomileNewNumberLineDocument: TypedDocumentNode<
  UpdateEcomileNewNumberLineMutation,
  UpdateEcomileNewNumberLineMutationVariables
> = parse(`
  mutation UpdateEcomileNewNumberLine($line: OrderLineDispatchInput!) {
    updateEcomileNewNumberLine(line: $line)
  }
`);
