import { getStore, goto, pageStore } from "$lib";
import { toast } from "$lib/components";
import { orderStore, orderLinesStore, fetchParamsStore } from "$lib/managers/stores";

/** Live `newOrderLine`: append next line client-side and navigate. Tyresoles: `return` after blank line; `routeLineNo` in max when store empty. */
export const newOrderLine = () => {
    const order = getStore(orderStore);
    const orderLines = getStore(orderLinesStore);
    const fetchParams = getStore(fetchParamsStore);
    const page = getStore(pageStore);
    if (!order || !order.supplierCode) {
        toast.error("Please select a supplier first");
        return;
    }
    const blankLine = orderLines?.find((l) => l.itemNo === "" && (l.lineNo ?? 0) > 0);
    if (blankLine) {
        goto(`/ecoproc/orders/${page.params.orderID}/${blankLine.lineNo}`);
        return;
    }
    const routeLineNo = Number.parseInt(String(page.params.lineNo ?? ""), 10);
    let lineNums: number[] =
        orderLines && orderLines.length > 0
            ? orderLines.map((c) => c.lineNo ?? 0)
            : [0];
    const maxLineNo = Math.max(
        ...lineNums,
        Number.isInteger(routeLineNo) && routeLineNo > 0 ? routeLineNo : 0,
    );
    const lineNo = maxLineNo + 10000;

    const prevOrderLine = orderLines?.find((c) => c.lineNo === maxLineNo);
    orderLinesStore.update((s) => [
        ...(s ?? []),
        {
            no: String(order.orderNo ?? ""),
            date: new Date(),
            lineNo,
            sortNo: "",
            vendorNo: String(order.supplierCode ?? ""),
            itemNo: prevOrderLine?.itemNo ?? "",
            serialNo: "",
            make: "",
            subMake: "",
            amount: 0,
            inspection: "",
            inspector:
                prevOrderLine?.inspector ??
                (fetchParams?.userDepartment === "Production" ? fetchParams?.userName : ""),
            inspectorCode:
                prevOrderLine?.inspectorCode ??
                (fetchParams?.userDepartment === "Production" ? fetchParams?.userCode : ""),
        },
    ]);
    goto(`/ecoproc/orders/${page.params.orderID}/${lineNo}`);
};
