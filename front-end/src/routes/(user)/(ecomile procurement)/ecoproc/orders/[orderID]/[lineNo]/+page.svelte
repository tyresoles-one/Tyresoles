<script lang="ts">
  import { get } from "svelte/store";
  import { onMount, untrack } from "svelte";
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import { z } from "zod";
  import { CreateForm, FormGenerator, type FormSchema } from "$lib/components/venUI/form";
  import type { FormNode } from "$lib/components/venUI/form/types";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";
  import { Skeleton } from "$lib/components/ui/skeleton";
  import { toast } from "$lib/components/venUI/toast";
  import { graphqlQuery, graphqlMutation, buildQuery } from "$lib/services/graphql";
  import { ensureFetchParams, fetchParamsStore, orderStore, orderLinesStore } from "$lib/managers/stores";
  import { toFetchParamsInput } from "../../../logic";
  import { newOrderLine } from "../logic.svelte.ts";
  import { ensureOrderLinesStoreForOrder } from "../order-lines-sync";
  import { dialogShow } from "$lib/components";
  import type { OrderLine } from "$lib/business/models";
  import type { FetchParamsInput } from "$lib/services/graphql/generated/graphql";
  import {
    GetProductionProcurementOrderLinesDocument,
    GetProductionItemNosDocument,
    GetProductionInspectorCodeNamesDocument,
    UpdateProductionProcurementOrderLineDocument,
    DeleteProductionProcurementOrderLineDocument,
    type GetProductionProcurementOrderLinesQuery,
    type GetProductionProcurementOrderLinesQueryVariables,
    type GetProductionItemNosQuery,
    type GetProductionInspectorCodeNamesQuery,
    type OrderLineInput,
    type OrderInfoInput,
  } from "$lib/services/graphql/generated/graphql";

  const orderID = $derived($page.params.orderID);
  /** Integer line no — `Number("new")` is NaN and breaks GraphQL `IntOperationFilterInput.eq`. */
  const lineNoNumeric = $derived(Number.parseInt(String($page.params.lineNo ?? ""), 10));

  /** Mirrors `OrderInfoInput` for `productionProcurementOrderLines` (same shape as order page). */
  function orderInfoInputForLines(orderNo: string): OrderInfoInput {
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

  // ── GraphQL ────────────────────────────────────────────────────
  /** Header + line in one round-trip (mirrors Live: order context for save / newOrderLine stores). */
  const GetProcLinePageQuery = buildQuery`
    query GetProcLinePage($hdrWhere: PurchaseHeaderFilterInput!, $lineWhere: PurchaseLineFilterInput!) {
      procurementOrders(first: 1, where: $hdrWhere) {
        nodes {
          no
          buyFromVendorNo
          responsibilityCenter
        }
      }
      procurementOrderLines(where: $lineWhere) {
        nodes {
          documentNo
          lineNo
          no
          serialNo
          make
          model
          inspection
          inspector
          orderStatus
          dispatchOrderNo
          dispatchDate
          dispatchDestination
          dispatchVehicleNo
          dispatchMobileNo
          dispatchTransporter
          newSerialNo
          orderDate
          amount
          subMake
          buyFromVendorNo
        }
      }
    }
  `;

  // ── Types ──────────────────────────────────────────────────────
  type LineValues = {
    /** NAV item no. (casing / tyre item). */
    no: string;
    serialNo: string;
    make: string;
    model: string;
    inspection: string;
    inspector: string;
    amount: string;
  };

  // ── State ──────────────────────────────────────────────────────
  let loading = $state(true);
  let saving = $state(false);
  let rawLine = $state<Record<string, any> | null>(null);
  /** Order header RC — scopes `purchaseItemNos` query / legacy ItemNos (Group Category Type 9). */
  let orderRespCenter = $state<string | null>(null);
  /** From casing item master — amount must stay within [minRate, maxRate] (Live parity). */
  let minRate = $state(0);
  let maxRate = $state(10000);
  const isPosted = $derived(rawLine?.orderStatus === 1);

  /** Live: hide inspector list when user department is Production. */
  const hideInspector = $derived(get(fetchParamsStore)?.userDepartment === "Production");

  const purchaseItemParam = $derived.by((): FetchParamsInput | null => {
    ensureFetchParams();
    const fp = get(fetchParamsStore);
    if (!fp) return null;
    return toFetchParamsInput({
      ...fp,
      regions: ["CASING"],
      type: "FromGroupDetail",
      respCenters: orderRespCenter ? [orderRespCenter] : fp.respCenters ?? [],
    });
  });

  /** Query.cs GetProductionMakes — TYREMAKE + casing filter (ProductionService). */
  const productionMakesParam = $derived.by((): FetchParamsInput | null => {
    ensureFetchParams();
    const fp = get(fetchParamsStore);
    if (!fp) return null;
    return toFetchParamsInput({
      ...fp,
      regions: ["TYREMAKE"],
      type: "casing",
      respCenters: orderRespCenter ? [orderRespCenter] : fp.respCenters ?? [],
    });
  });

  /**
   * GetProductionMakeSubMake / GetMakeSubMakeAsync — `param.type` = `"itemNo,make"`
   * (e.g. `1000R20,APOLLO`), same as legacy `makesSubMakes` body.
   */
  const productionMakeSubMakeParam = $derived.by((): FetchParamsInput | null => {
    const base = productionMakesParam;
    const itemNo = String(form.values.no ?? "").trim();
    const make = String(form.values.make ?? "").trim();
    if (!base || !itemNo || !make) return null;
    return { ...base, type: `${itemNo},${make}` };
  });

  /** Live: Sub Make row only after Tyre Size + Make (same gate as legacy `fetchMakeSubMake`). */
  const showSubMakeField = $derived(productionMakeSubMakeParam != null);

  /** GetProductionInspectorCodeNames — not Factory: Ecomile procurement inspectors. */
  const productionInspectorParam = $derived.by((): FetchParamsInput | null => {
    ensureFetchParams();
    const fp = get(fetchParamsStore);
    if (!fp) return null;
    return toFetchParamsInput({
      ...fp,
      type: "",
      respCenters: orderRespCenter ? [orderRespCenter] : fp.respCenters ?? [],
    });
  });

  /** GetProductionProcurementInspection — static list; param required by schema only. */
  const productionInspectionParam = $derived.by((): FetchParamsInput | null => {
    ensureFetchParams();
    const fp = get(fetchParamsStore);
    if (!fp) return null;
    return toFetchParamsInput({ ...fp });
  });

  // ── Form ────────────────────────────────────────────────────────
  /** Rates from `GetPurchaseItemNos` / `productionItemNos` for the selected tyre size (`form.values.no`). */
  function createLineSchema(minR: number, maxR: number) {
    return z.object({
      no: z.string().min(1, "Tyre Size is required"),
      serialNo: z.string().min(1, "Serial No. is required"),
      make: z.string().min(1, "Make is required"),
      model: z.string().default(""),
      inspection: z.string().min(1, "Inspection is required"),
      inspector: z.string().default(""),
      /** `input type="number"` + Svelte `bind:value` may store a number; bounds match selected item master. */
      amount: z.coerce
        .string()
        .refine((v) => {
          const n = parseFloat(String(v));
          return !isNaN(n) && n >= 0;
        }, "Enter a valid amount")
        .refine((v) => {
          const n = parseFloat(String(v));
          if (isNaN(n)) return false;
          return n >= minR && n <= maxR;
        }, `Amount must be between ${minR} and ${maxR}`),
    });
  }

  const form = CreateForm<LineValues>({
    /** Default band; `$effect` below applies rates from the selected purchase item (`GetPurchaseItemNos`). */
    schema: createLineSchema(0, 10000),
    initialValues: {
      no: "",
      serialNo: "",
      make: "",
      model: "",
      inspection: "",
      inspector: "",
      amount: "",
    },
    onSubmit: async () => {
      await saveLine({ closeAfter: true });
    }
  });

  /** Same param shape as `productionInspectorParam` — used to resolve code → name for `OrderLineInput.inspector`. */
  function getInspectorMasterParam(): FetchParamsInput | null {
    ensureFetchParams();
    const fp = get(fetchParamsStore);
    if (!fp) return null;
    return toFetchParamsInput({
      ...fp,
      type: "",
      respCenters: orderRespCenter ? [orderRespCenter] : fp.respCenters ?? [],
    });
  }

  async function resolveInspectorNameForCode(code: string): Promise<string> {
    const c = String(code ?? "").trim();
    if (!c) return "";
    const param = getInspectorMasterParam();
    if (!param) return "";
    const res = await graphqlQuery<GetProductionInspectorCodeNamesQuery>(GetProductionInspectorCodeNamesDocument, {
      variables: { param },
      skipLoading: true,
      skipCache: true,
    });
    if (!res.success || !res.data?.productionInspectorCodeNames) return "";
    const row = res.data.productionInspectorCodeNames.find((x) => x.code === c);
    return String(row?.name ?? "").trim();
  }

  /** `inspectorCode` = master code (matches MasterSelect value); `inspector` = display name for NAV. */
  async function buildOrderLineInputAsync(): Promise<OrderLineInput> {
    const vendor = String(rawLine?.buyFromVendorNo ?? "");
    const amt = parseFloat(form.values.amount) || 0;
    let inspectorCode = "";
    let inspectorName = "";
    if (hideInspector) {
      inspectorCode = String(rawLine?.inspectorCode ?? "").trim();
      inspectorName = String(rawLine?.inspectorName ?? rawLine?.inspector ?? "").trim();
    } else {
      inspectorCode = String(form.values.inspector ?? "").trim();
      if (inspectorCode) {
        inspectorName = await resolveInspectorNameForCode(inspectorCode);
        if (!inspectorName) {
          inspectorName = String(rawLine?.inspectorName ?? rawLine?.inspector ?? "").trim();
        }
        if (!inspectorName) inspectorName = inspectorCode;
      }
    }
    return {
      no: orderID ?? "",
      lineNo: lineNoNumeric,
      vendorNo: vendor,
      itemNo: form.values.no,
      make: form.values.make,
      subMake: form.values.model,
      serialNo: form.values.serialNo,
      amount: amt as unknown as OrderLineInput["amount"],
      inspection: form.values.inspection,
      inspector: inspectorName,
      inspectorCode,
      /** `PurchaseLine` has no `sortNo` in GraphQL; mutation input still allows empty. */
      sortNo: "",
    };
  }

  /** Live `validateOrderLine` — inspection (+ inspector when shown). Amount band is enforced in `createLineSchema` + `form.validate()`. */
  function validateLine(): boolean {
    const ins = String(form.values.inspection ?? "").trim();
    if (ins === "" || ins === " ") {
      toast.error("Please select an inspection");
      return false;
    }
    if (!hideInspector && !String(form.values.inspector ?? "").trim()) {
      toast.error("Please select an inspector");
      return false;
    }
    return true;
  }

  async function syncProcurementStores(opts?: { skipCache?: boolean }) {
    const orderNo = orderID;
    if (!orderNo || !rawLine) return;
    const vendor = String(rawLine.buyFromVendorNo ?? "");
    orderStore.set({ orderNo, supplierCode: vendor });
    const res = await graphqlQuery<GetProductionProcurementOrderLinesQuery>(
      GetProductionProcurementOrderLinesDocument,
      {
        variables: { param: orderInfoInputForLines(orderNo) },
        skipLoading: true,
        skipCache: opts?.skipCache ?? false,
      }
    );
    if (!res.success || !res.data?.productionProcurementOrderLines) return;
    const mapped = res.data.productionProcurementOrderLines.map((l) => ({
      no: orderNo,
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
    }));
    orderLinesStore.set(mapped as any);
  }

  async function saveLine(opts: { closeAfter: boolean; saveAndNew?: boolean }) {
    for (const key of Object.keys(form.values) as (keyof LineValues)[]) {
      form.touched[key as string] = true;
    }
    if (!form.validate()) return;
    if (!validateLine()) return;
    saving = true;
    try {
      const res = await graphqlMutation(UpdateProductionProcurementOrderLineDocument, {
        variables: { order: await buildOrderLineInputAsync() },
        skipLoading: true,
        silent: true,
      });
      if (!res.success) {
        toast.error(res.error || "Failed to save line.");
        return;
      }
      toast.success("Line saved successfully.");
      if (opts.saveAndNew) {
        await syncProcurementStores({ skipCache: true });
        newOrderLine();
        return;
      }
      if (opts.closeAfter) {
        goto(`/ecoproc/orders/${orderID}`);
      }
    } catch (e: any) {
      toast.error(e?.message || "Failed to save.");
    } finally {
      saving = false;
    }
  }

  function confirmDeleteLine() {
    dialogShow({
      title: "Confirm deletion",
      description: "Are you sure you want to delete this tyre line?",
      actionLabel: "Delete",
      onAction: async () => {
        saving = true;
        try {
          const res = await graphqlMutation(DeleteProductionProcurementOrderLineDocument, {
            variables: { order: await buildOrderLineInputAsync() },
            skipLoading: true,
            silent: true,
          });
          if (res.success) {
            toast.success("Tyre line deleted.");
            goto(`/ecoproc/orders/${orderID}`);
          } else {
            toast.error(res.error || "Failed to delete.");
          }
        } catch (e: any) {
          toast.error(e?.message || "Failed to delete.");
        } finally {
          saving = false;
        }
      },
    });
  }

  function draftLine(docNo: string, ln: number): Record<string, unknown> {
    return {
      documentNo: docNo,
      lineNo: ln,
      no: "",
      serialNo: "",
      make: "",
      model: "",
      inspection: "",
      inspector: "",
      orderStatus: 0,
      amount: 0,
      subMake: "",
      newSerialNo: null,
      buyFromVendorNo: "",
      sortNo: "",
      orderDate: null,
      dispatchOrderNo: null,
      dispatchDate: null,
      dispatchDestination: null,
      dispatchVehicleNo: null,
      dispatchMobileNo: null,
      dispatchTransporter: null,
    };
  }

  function applyLineToForm(row: Record<string, unknown>) {
    form.values.no = String(row.no ?? "");
    form.values.serialNo = String(row.serialNo ?? "");
    form.values.make = String(row.make ?? "");
    form.values.model = String(row.model ?? "");
    form.values.inspection = String(row.inspection ?? "");
    /** MasterSelect value must be inspector **code** (`productionInspectorCodeNames`). */
    form.values.inspector = String(row.inspectorCode ?? row.inspector ?? "").trim();
    form.values.amount = String(row.amount ?? "");
  }

  /** Prefer first non-empty string (PurchaseLine often omits inspection; OrderLine from production API has it). */
  function coalesceStr(a: unknown, b: unknown) {
    const s = String(a ?? "").trim();
    if (s) return s;
    return String(b ?? "").trim();
  }

  /**
   * Row appended by `newOrderLine` after Save & New: inspection/make/serial empty; only itemNo + inspector filled.
   * Nav may still return a full `PurchaseLine` from the API (copy of previous line) — we must not apply that wholesale.
   */
  function isSparseCarryFromSaveNew(row: OrderLine | undefined): boolean {
    if (!row) return false;
    return (
      String(row.inspection ?? "").trim() === "" &&
      String(row.make ?? "").trim() === "" &&
      String(row.serialNo ?? "").trim() === ""
    );
  }

  /** Same line list as the order page — fills inspection/inspector when `procurementOrderLines` leaves them blank. */
  async function fetchProductionLineRow(orderNo: string, lineNo: number) {
    const res = await graphqlQuery<
      GetProductionProcurementOrderLinesQuery,
      GetProductionProcurementOrderLinesQueryVariables
    >(GetProductionProcurementOrderLinesDocument, {
      variables: { param: orderInfoInputForLines(orderNo) },
      skipLoading: true,
      skipCache: true,
    });
    if (!res.success || !res.data?.productionProcurementOrderLines) return undefined;
    return res.data.productionProcurementOrderLines.find((l) => l.lineNo === lineNo);
  }

  // ── Fetch ──────────────────────────────────────────────────────
  async function fetchLine() {
    loading = true;
    try {
      if (!Number.isInteger(lineNoNumeric) || lineNoNumeric < 1) {
        toast.error("Invalid tyre line.");
        goto(`/ecoproc/orders/${orderID}`);
        return;
      }

      const res = await graphqlQuery<any>(GetProcLinePageQuery, {
        variables: {
          hdrWhere: { no: { eq: orderID } },
          lineWhere: {
            and: [{ documentNo: { eq: orderID } }, { lineNo: { eq: lineNoNumeric } }],
          },
        },
        skipLoading: true,
      });

      if (!res.success || !res.data) {
        toast.error(res.error || "Line not found.");
        goto(`/ecoproc/orders/${orderID}`);
        return;
      }

      const hdrNode = res.data.procurementOrders?.nodes?.[0];
      if (!hdrNode) {
        toast.error("Order not found.");
        goto("/ecoproc/orders");
        return;
      }
      if (hdrNode?.responsibilityCenter) {
        orderRespCenter = String(hdrNode.responsibilityCenter).trim() || null;
      } else {
        orderRespCenter = null;
      }

      const vendorHint = String(hdrNode?.buyFromVendorNo ?? "");
      orderStore.set({ orderNo: orderID, supplierCode: vendorHint });
      const ensured = await ensureOrderLinesStoreForOrder(orderID!, vendorHint);
      if (!ensured.success && ensured.error) {
        toast.error(ensured.error);
      }

      const lineNode = res.data.procurementOrderLines?.nodes?.[0];
      const fromStore = get(orderLinesStore)?.find((l) => l.lineNo === lineNoNumeric) as OrderLine | undefined;

      if (lineNode) {
        if (fromStore && isSparseCarryFromSaveNew(fromStore)) {
          const code = String(fromStore.inspectorCode ?? fromStore.inspector ?? "").trim();
          const name = String(fromStore.inspector ?? "").trim();
          rawLine = {
            ...lineNode,
            no: String(fromStore.itemNo ?? lineNode.no ?? "").trim(),
            serialNo: "",
            make: "",
            model: "",
            inspection: "",
            inspector: name,
            inspectorName: name,
            inspectorCode: code,
            amount: "",
          };
          applyLineToForm(rawLine as Record<string, unknown>);
          await syncProcurementStores();
        } else {
          const prodLine = await fetchProductionLineRow(orderID!, lineNoNumeric);
          const inspectorName = coalesceStr(lineNode.inspector, prodLine?.inspector);
          const inspectorCode = String(prodLine?.inspectorCode ?? "").trim();
          rawLine = {
            ...lineNode,
            inspection: coalesceStr(lineNode.inspection, prodLine?.inspection),
            inspector: inspectorName,
            inspectorName,
            inspectorCode,
          };
          applyLineToForm(rawLine as Record<string, unknown>);
          await syncProcurementStores();
        }
      } else {
        const draft = draftLine(orderID!, lineNoNumeric);
        draft.buyFromVendorNo = hdrNode?.buyFromVendorNo ?? "";
        const storeLines = get(orderLinesStore);
        const fromStore = storeLines?.find((l) => l.lineNo === lineNoNumeric);
        if (fromStore) {
          draft.no = String(fromStore.itemNo ?? "").trim();
          draft.inspector = String(fromStore.inspector ?? "").trim();
          draft.inspectorName = String(fromStore.inspector ?? "").trim();
          draft.inspectorCode = String(fromStore.inspectorCode ?? "").trim();
          draft.serialNo = "";
          draft.make = "";
          draft.model = "";
          draft.inspection = "";
          draft.amount = 0;
        }
        rawLine = draft;
        applyLineToForm(rawLine);
        await syncProcurementStores();
      }
    } catch {
      toast.error("Failed to load line.");
    } finally {
      loading = false;
    }
  }

  onMount(() => { fetchLine(); });

  /** Sub-make list key is `itemNo,make` — clear model when item or make changes. */
  let prevMakeSubMakeKey = $state<string | null>(null);
  $effect(() => {
    const itemNo = String(form.values.no ?? "").trim();
    const make = String(form.values.make ?? "").trim();
    const key = itemNo && make ? `${itemNo},${make}` : "";
    if (prevMakeSubMakeKey !== null && key !== prevMakeSubMakeKey) {
      form.values.model = "";
    }
    prevMakeSubMakeKey = key || null;
  });

  /** Live: tyre item selection sets min/max for amount validation (`itemsStore` equivalent). */
  $effect(() => {
    const code = String(form.values.no ?? "").trim();
    const param = purchaseItemParam;
    if (!code || !param) {
      minRate = 0;
      maxRate = 10000;
      return;
    }
    let cancelled = false;
    void (async () => {
      const res = await graphqlQuery<GetProductionItemNosQuery>(GetProductionItemNosDocument, {
        variables: { param },
        skipLoading: true,
        skipCache: true,
      });
      if (cancelled || !res.success || !res.data?.productionItemNos) return;
      const row = res.data.productionItemNos.find((x: { code: string }) => x.code === code);
      if (row) {
        minRate = parseFloat(String(row.minRate)) || 0;
        maxRate = parseFloat(String(row.maxRate)) || 10000;
      } else {
        minRate = 0;
        maxRate = 10000;
      }
    })();
    return () => {
      cancelled = true;
    };
  });

  /** Keep Zod amount bounds in sync with `minRate` / `maxRate` from the selected purchase item row. */
  $effect(() => {
    const mn = minRate;
    const mx = maxRate;
    form.setSchema(createLineSchema(mn, mx));
    untrack(() => {
      const raw = form.values.amount;
      const hasValue =
        raw !== "" && raw !== undefined && raw !== null && String(raw).trim() !== "";
      if (form.touched.amount || hasValue) {
        form.validate("amount");
      }
    });
  });

  // ── Form Schema (venUI) — Tyre Size → Make → Sub Make → Serial → Amount → Inspection → Inspector. ──
  const formSchema = $derived.by<FormSchema>(() => {
    const itemRow: FormNode = isPosted
      ? {
          type: "field",
          name: "no",
          label: "Tyre Size",
          placeholder: "—",
          disabled: true,
          colSpan: 2,
          class: "w-full",
        }
      : {
          type: "custom",
          component: MasterSelect,
          colSpan: 2,
          class: "w-full",
          orientation: "vertical",
          props: {
            fieldName: "no",
            masterType: "purchaseItems",
            label: "Tyre Size",
            placeholder: purchaseItemParam ? "Search casing / tyre size…" : "Loading scope…",
            singleSelect: true,
            disabled: !purchaseItemParam,
            purchaseItemParam,
          },
        };

    /** Live: flex `md:flex-row` groups fields in flow order; we use the same sequence in a 2-col grid. */
    const fields: FormNode[] = [
      itemRow,
      {
        type: "custom",
        component: MasterSelect,
        class: "w-full",
        orientation: "vertical",
        props: {
          fieldName: "make",
          masterType: "productionMakes",
          label: "Make",
          placeholder: productionMakesParam ? "Search make…" : "Loading…",
          singleSelect: true,
          disabled: isPosted || !productionMakesParam,
          productionFetchParam: productionMakesParam,
        },
      },
      ...(showSubMakeField
        ? ([
            {
              type: "custom",
              component: MasterSelect,
              class: "w-full",
              orientation: "vertical",
              props: {
                fieldName: "model",
                masterType: "productionMakeSubMake",
                label: "Sub Make",
                placeholder: productionMakeSubMakeParam ? "Search sub make…" : "Loading…",
                singleSelect: true,
                disabled: isPosted || !productionMakeSubMakeParam,
                productionFetchParam: productionMakeSubMakeParam,
              },
            },
          ] satisfies FormNode[])
        : []),
      {
        type: "field",
        name: "serialNo",
        label: "Serial No",
        placeholder: "e.g. ABC123456",
        required: true,
        disabled: isPosted,
      },
      {
        type: "field",
        name: "amount",
        label: "Amount",
        inputType: "number",
        placeholder: "0.00",
        required: true,
        disabled: isPosted,
      },
      {
        type: "custom",
        component: MasterSelect,
        class: "w-full",
        orientation: "vertical",
        props: {
          fieldName: "inspection",
          masterType: "productionProcurementInspection",
          label: "Inspection",
          placeholder: productionInspectionParam ? "Select inspection…" : "Loading…",
          singleSelect: true,
          disabled: isPosted || !productionInspectionParam,
          productionFetchParam: productionInspectionParam,
        },
      },
      ...(hideInspector
        ? []
        : ([
            {
              type: "custom",
              component: MasterSelect,
              class: "w-full",
              orientation: "vertical",
              props: {
                fieldName: "inspector",
                masterType: "productionInspectorCodeNames",
                label: "Inspector",
                placeholder: productionInspectorParam ? "Search inspector…" : "Loading…",
                singleSelect: true,
                disabled: isPosted || !productionInspectorParam,
                productionFetchParam: productionInspectorParam,
              },
            },
          ] satisfies FormNode[])),
    ];

    return [
      {
        type: "set",
        class: "w-full min-w-0 space-y-4",
        children: [
          {
            type: "grid",
            mobileCols: 1,
            cols: 2,
            gap: 4,
            class: "w-full items-start gap-x-4 gap-y-4",
            children: fields,
          },
        ],
      },
    ];
  });

  // ── Helpers ────────────────────────────────────────────────────
  function formatDate(iso: string | null | undefined) {
    if (!iso) return "—";
    const d = new Date(iso);
    return isNaN(d.getTime()) ? "—" : d.toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
  }

  const statusLabel: Record<number, string> = { 0: "Open", 1: "Posted", 2: "Rejected" };

  function getStatusColor(s: number) {
    if (s === 1) return "bg-green-100 text-green-700 border-green-200";
    if (s === 2) return "bg-red-100 text-red-700 border-red-200";
    return "bg-amber-100 text-amber-700 border-amber-200";
  }
</script>

<svelte:head>
  <title>Line {lineNoNumeric} — Order {orderID} | Ecomile Procurement</title>
</svelte:head>

<div class="min-h-screen bg-muted/30 pb-20">
  <PageHeading
    backHref="/ecoproc/orders/{orderID}"
    backLabel="Order {orderID}"
    icon="layers"
    class="border-b bg-background"
  >
    {#snippet title()}
      Tyre Line — {lineNoNumeric}
    {/snippet}
    {#snippet description()}
      {#if rawLine}
        <span class="inline-flex items-center gap-2 text-sm">
          <span class="inline-flex items-center rounded border px-2 py-0.5 text-xs font-semibold {getStatusColor(rawLine.orderStatus)}">
            {statusLabel[rawLine.orderStatus] ?? "Unknown"}
          </span>
          {#if rawLine.serialNo}
            <span class="text-muted-foreground font-mono text-xs">{rawLine.serialNo}</span>
          {/if}
        </span>
      {/if}
    {/snippet}
    {#snippet actions()}
      <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:justify-end">
        <Button
          variant="outline"
          size="sm"
          class="w-full min-h-11 sm:min-h-9 sm:w-auto"
          onclick={() => goto(`/ecoproc/orders/${orderID}`)}
        >
          <Icon name="arrow-left" class="mr-2 size-4 shrink-0" />
          Back
        </Button>
        {#if !isPosted}
          <Button
            size="sm"
            class="w-full min-h-11 sm:min-h-9 sm:w-auto"
            onclick={() => saveLine({ closeAfter: true })}
            disabled={form.isSubmitting || saving}
            loading={form.isSubmitting || saving}
          >
            <Icon name="save" class="mr-2 size-4 shrink-0" />
            Save
          </Button>
        {/if}
      </div>
    {/snippet}
  </PageHeading>

  <div class="container mx-auto max-w-4xl space-y-4 px-4 py-4 sm:space-y-6 sm:py-6 md:px-6">

    {#if loading}
      <div class="space-y-4 rounded-xl border bg-card p-4 shadow-sm sm:p-6">
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {#each { length: 6 } as _}
            <div class="space-y-2">
              <Skeleton class="h-3 w-24" />
              <Skeleton class="h-11 w-full sm:h-9" />
            </div>
          {/each}
        </div>
      </div>

    {:else if rawLine}

      <!-- Dispatch Info (read-only) -->
      {#if rawLine.dispatchOrderNo}
        <div class="rounded-xl border bg-card shadow-sm overflow-hidden">
          <div class="flex items-center gap-3 px-5 py-3.5 border-b bg-muted/30">
            <Icon name="truck" class="size-4 text-muted-foreground" />
            <h2 class="text-sm font-semibold">Dispatch Information</h2>
          </div>
          <div class="grid grid-cols-1 gap-4 p-4 text-sm sm:grid-cols-2 sm:p-5 lg:grid-cols-3">
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Dispatch Order No</p>
              <p class="mt-0.5 font-mono">{rawLine.dispatchOrderNo || "—"}</p>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Dispatch Date</p>
              <p class="mt-0.5">{formatDate(rawLine.dispatchDate)}</p>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Destination</p>
              <p class="mt-0.5">{rawLine.dispatchDestination || "—"}</p>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Vehicle No</p>
              <p class="mt-0.5 font-mono">{rawLine.dispatchVehicleNo || "—"}</p>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Mobile</p>
              <p class="mt-0.5">{rawLine.dispatchMobileNo || "—"}</p>
            </div>
            <div>
              <p class="text-xs font-semibold uppercase tracking-wide text-muted-foreground">Transporter</p>
              <p class="mt-0.5">{rawLine.dispatchTransporter || "—"}</p>
            </div>
          </div>
        </div>
      {/if}

      <!-- Edit form: single venUI FormGenerator (focus manager + Enter/Tab order includes Item No. master select) -->
      <div class="overflow-hidden rounded-xl border bg-card shadow-sm">
        <div class="flex items-center gap-3 border-b px-4 py-3.5 sm:px-5">
          <div class="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary sm:size-8">
            <Icon name="square-pen" class="size-4" />
          </div>
          <h2 class="min-w-0 text-sm font-semibold leading-tight">
            {isPosted ? "Line details (read only)" : "Edit tyre line"}
          </h2>
        </div>

        <div class="min-w-0 space-y-6 p-4 sm:p-6">
          <FormGenerator
            {form}
            schema={formSchema}
            root={true}
            autoFocus={!loading && !isPosted}
          />

          {#if !isPosted}
            <!-- Live ecoproc line form action order: Save & New → Save & Close → Cancel → Delete -->
            <div
              class="flex flex-col gap-2 border-t pt-4 sm:flex-row sm:flex-wrap sm:justify-end sm:gap-3 sm:pt-6"
            >
              <Button
                variant="outline"
                class="min-h-11 w-full min-w-40 sm:min-h-9 sm:w-auto"
                onclick={() => saveLine({ closeAfter: false, saveAndNew: true })}
                disabled={form.isSubmitting || saving}
              >
                {#if saving}
                  <Icon name="loader-2" class="mr-2 size-4 shrink-0 animate-spin" />
                  Saving…
                {:else}
                  <Icon name="save" class="mr-2 size-4 shrink-0" />
                  Save &amp; New
                {/if}
              </Button>
              <Button
                class="min-h-11 w-full min-w-40 sm:min-h-9 sm:w-auto"
                onclick={() => saveLine({ closeAfter: true })}
                disabled={form.isSubmitting || saving}
              >
                {#if form.isSubmitting || saving}
                  <Icon name="loader-2" class="mr-2 size-4 shrink-0 animate-spin" />
                  Saving…
                {:else}
                  <Icon name="save" class="mr-2 size-4 shrink-0" />
                  Save &amp; Close
                {/if}
              </Button>
              <Button
                variant="outline"
                class="min-h-11 w-full sm:min-h-9 sm:w-auto"
                onclick={() => goto(`/ecoproc/orders/${orderID}`)}
              >
                Cancel
              </Button>
              <Button
                variant="destructive"
                class="min-h-11 w-full sm:min-h-9 sm:w-auto"
                onclick={confirmDeleteLine}
                disabled={saving}
              >
                <Icon name="trash-2" class="mr-2 size-4 shrink-0" />
                Delete
              </Button>
            </div>
          {/if}
        </div>
      </div>

      <!-- New Serial (if applicable) — list markup avoids nested {#if}/<div> parse edge cases -->
      {#if rawLine.newSerialNo}
        <div class="rounded-xl border bg-amber-50 border-amber-200 shadow-sm overflow-hidden">
          <div class="flex items-center gap-3 px-5 py-3.5 border-b border-amber-200">
            <Icon name="alert-triangle" class="size-4 text-amber-600" />
            <h2 class="text-sm font-semibold text-amber-800">Factory Inspection Notes</h2>
          </div>
          <ul class="m-0 grid list-none grid-cols-1 gap-4 p-5 text-sm sm:grid-cols-2">
            <li class="min-w-0">
              <p class="text-xs font-semibold uppercase tracking-wide text-amber-700">New Serial No</p>
              <p class="mt-0.5 font-mono font-semibold">{rawLine.newSerialNo}</p>
            </li>
          </ul>
        </div>
      {/if}

    {/if}
  </div>
</div>
