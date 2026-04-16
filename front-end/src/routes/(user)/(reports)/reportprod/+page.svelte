<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import { toast } from "$lib/components/venUI/toast";
  import { z, type ZodSchema } from "zod";
  import {
    exportProductionReport,
    getProductionReportMeta,
  } from "$lib/services/productionReports";
  import type { ReportFetchParams } from "$lib/services/salesReports";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { VenForm, type FormSchema } from "$lib/components/venUI/form";
  import { Button } from "$lib/components/ui/button";
  import ReportViewer from "$lib/components/venUI/report-viewer/ReportViewer.svelte";
  import { Icon } from "$lib/components/venUI/icon";
  import { DatePresets } from "$lib/components/venUI/date-picker";
  import {
    today,
    getLocalTimeZone,
    startOfMonth,
    startOfYear,
    parseDate,
    parseDateTime,
    toCalendarDate,
    fromDate,
  } from "@internationalized/date";
  import { authStore, getUser, clearAuth } from "$lib/stores/auth";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";

  /** Reports filter from stored auth: menu item with action "/reportprod" options (comma-separated report codes). */
  function getReportprodOptions(): string | undefined {
    const menus = authStore.get().menus ?? [];
    for (const menu of menus) {
      for (const sub of menu.subMenus ?? []) {
        for (const item of sub.items ?? []) {
          const action = (item.action ?? "").trim().replace(/\/+$/, "") || "";
          if (action === "/reportprod")
            return item.options?.trim() || undefined;
        }
      }
    }
    return undefined;
  }

  const viewOptions = [
    { value: "All", label: "All" },
    { value: "Customer", label: "Customer" },
    { value: "Dealer", label: "Dealer" },
    { value: "Region", label: "Region" },
  ];

  const workDate = $derived($authStore.user?.workDate);
  const ref = $derived.by(() => {
    if (!workDate) return today(getLocalTimeZone());
    try {
      if (typeof workDate === "string") {
        if (workDate.includes("T")) {
          try {
            // Attempt strict slicing timezones properly:
            return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
          } catch (e) {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        } else {
          try {
            return parseDate(workDate);
          } catch (e) {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        }
      }
      return today(getLocalTimeZone());
    } catch (e) {
      return today(getLocalTimeZone());
    }
  });

  const showWorkDateBadge = $derived(
    workDate && ref.toString() !== today(getLocalTimeZone()).toString(),
  );

  /** Locations where production === 1 (user can run production reports for these). */
  const productionLocations = $derived.by(() => {
    const locs = $authStore.locations ?? [];
    return locs.filter((l: { production?: unknown }) => l.production === 1);
  });
  /** Show resp. centers select only when user has more than one production location. */
  const showRespCentersSelect = $derived(productionLocations.length > 1);

  function getDateRangeForPreset(presetString: string | undefined): {
    start: any;
    end: any;
  } {
    if (!presetString) return { start: ref, end: ref };

    // Handle comma separated keys like 'thisMonth,lastMonth'
    const firstKey = presetString.split(",")[0].trim();

    // 1. Try matching keys in PresetLib directly (camelCase)
    const rangeLib = DatePresets.range as any;
    if (rangeLib[firstKey]) {
      const p = rangeLib[firstKey](ref);
      return p.value;
    }

    // 2. Legacy/Manual label matching for backward compatibility
    const normalized = firstKey.toLowerCase();
    if (normalized === "this month") {
      return { start: startOfMonth(ref), end: ref };
    }
    if (normalized === "this year") {
      return { start: startOfYear(ref), end: ref };
    }
    if (normalized === "today") {
      return { start: ref, end: ref };
    }

    return { start: ref, end: ref };
  }

  type ReportProductionForm = {
    report: string;
    dateRange: { start: unknown; end: unknown } | undefined;
    view: string;
    type: string;
    respCenters: string;
    customers: string;
    dealers: string;
    areas: string;
    regions: string;
    nos: string;
  };

  const initialValues: ReportProductionForm = {
    report: "",
    dateRange: undefined, // Will be set by effect
    view: "All",
    type: "",
    respCenters: "",
    customers: "",
    dealers: "",
    areas: "",
    regions: "",
    nos: "",
  };

  const form = new VenForm<ReportProductionForm>({ initialValues });

  let reportMetaList = $state<Awaited<
    ReturnType<typeof getProductionReportMeta>
  > | null>(null);
  let metaLoading = $state(true);
  let isUnauthorized = $state(false);

  const enrichMeta = (list: any[]) => {
    const metaMap: Record<string, any> = {
      "Ecomile Aging": { showRespCenters: true, showType: true, typeOptions: ["Casing", "Prod Not Invoice", "All"], requiredFields: "respCenters" },
      "Exchange Tyres": { showRespCenters: true, requiredFields: "respCenters" },
      "Claim & Failure": { showType: true, showView: true, showRespCenters: true, typeOptions: ["Claim", "Failure"], viewOptions: ["Tyre-Vs-Decision", "Fault-Vs-Decision", "Customer-Vs-Decision", "Dealer-Vs-Decision", "Area-Vs-Decision", "Region-Vs-Decision"], requiredFields: "dateRange, respCenters, type, view", datePreset: "thisMonth,lastMonth" },
      "Posted Proc Order": { showNos: true, requiredFields: "nos", outputFormats: "pdf" },
      "Posted Dispatch Order": { showNos: true, requiredFields: "nos", outputFormats: "pdf" },
      "Posted Dispatch Details": { showNos: true, requiredFields: "nos", outputFormats: "pdf" },
      "Casing Inspection": { showNos: true, requiredFields: "nos", outputFormats: "pdf" },
      "Casing New Numbering": { showNos: true, requiredFields: "nos", outputFormats: "pdf" },
      "Vendor Bill": { showNos: true, showType: true, typeOptions: ["Posted", "Unposted"], requiredFields: "nos, type", outputFormats: "pdf" },
      "Casing Purchase Details": { showRespCenters: true, requiredFields: "dateRange, respCenters", datePreset: "thisMonth,lastMonth" },
      "Casing Purchase Analysis": { showRespCenters: true, requiredFields: "dateRange, respCenters", datePreset: "thisMonth,lastMonth" },
      "Ecomile Inv. Sales Stat.": { showRespCenters: true, requiredFields: "dateRange, respCenters", datePreset: "thisMonth,lastMonth" },
      "Claim Analysis": { showRespCenters: true, requiredFields: "dateRange, respCenters", datePreset: "thisMonth,lastMonth" },
      "Casing Average Cost": { outputFormats: "pdf,excel" }
    };
    return list.map(m => ({ ...m, ...(metaMap[m.name] || {}) }));
  };

  onMount(() => {
    const user = authStore.get().user;
    const urlId = $page.url.searchParams.get("id");

    // Backend expects userId for permission-based production reports list
    getProductionReportMeta(user?.userId?.toString() ?? undefined)
      .then((rawList) => {
        const list = enrichMeta(rawList);
        console.log(list);
        reportMetaList = list;
        if (list?.length > 0 && !form.values.report) {
          if (urlId) {
            // Match by code first, then fallback to id or name to be safe
            const matched = list.find(
              (m) => m.code === urlId || m.id.toString() === urlId || m.name === urlId,
            );
            if (matched) {
              form.setValue("report", matched.name);
            } else {
              form.setValue("report", list[0].name);
            }
          } else {
            form.setValue("report", list[0].name);
          }
        }
      })
      .catch((err) => {
        if (err.status === 401) {
          isUnauthorized = true;
          clearAuth();
        } else {
          reportMetaList = [];
          toast.error(err.message || "Failed to load report settings");
        }
      })
      .finally(() => {
        metaLoading = false;
      });
  });

  const selectedMeta = $derived(
    reportMetaList?.find((m) => m.name === form.values.report),
  );

  // Update date range when report or workDate changes
  $effect(() => {
    if (selectedMeta) {
      const range = getDateRangeForPreset(selectedMeta.datePreset);
      form.setValue("dateRange", range);
    }
  });

  $inspect(selectedMeta);
  const schema = $derived.by<FormSchema>(() => {
    if (!reportMetaList?.length) return [] as FormSchema;

    const requiredKeys = selectedMeta?.requiredFields
      ? selectedMeta.requiredFields.split(",").map((n) => n.trim())
      : [];

    const fields: any[] = [
      {
        type: "field",
        name: "report",
        label: "Report",
        inputType: "select",
        required: true,
        options: reportMetaList.map((m) => ({ label: m.name, value: m.name })),
      },
      {
        type: "field",
        name: "dateRange",
        label: "Date Range",
        required: requiredKeys.includes("dateRange"),
        inputType: "date-picker",
        mode: "range",
        placeholder: "Select date range",
        presetKeys: selectedMeta?.datePreset,
        workdate: $authStore.user?.workDate,
      },
    ];

    if (selectedMeta?.showView || selectedMeta?.showType) {
      if (selectedMeta?.showView) {
        fields.push({
          type: "field",
          name: "view",
          label: "View",
          required: requiredKeys.includes("view"),
          inputType: "select",
          options: (selectedMeta.viewOptions?.length > 0
            ? selectedMeta.viewOptions
            : viewOptions.map((o) => o.value)
          ).map((v: string) => ({ label: v, value: v })),
        });
      }

      if (selectedMeta?.showType) {
        fields.push({
          type: "field",
          name: "type",
          label: "Type",
          required: requiredKeys.includes("type"),
          inputType: "select",
          options: (selectedMeta.typeOptions || []).map((v: string) => ({
            label: v,
            value: v,
          })),
        });
      }
    }

    if (showRespCentersSelect) {
      fields.push({
        type: "custom",
        component: MasterSelect,
        required: requiredKeys.includes("respCenters"),
        props: {
          fieldName: "respCenters",
          masterType: "respCenters",
          label: "Resp. Centers",
          respCenterType: "Sale",
        },
        colSpan: 1,
      });
    }

    if (selectedMeta?.showCustomers) {
      fields.push({
        type: "custom",
        component: MasterSelect,
        required: requiredKeys.includes("customers"),
        props: {
          fieldName: "customers",
          masterType: "customers",
          label: "Customer Nos",
        },
        colSpan: 1,
      });
    }

    if (selectedMeta?.showDealers) {
      fields.push({
        type: "custom",
        component: MasterSelect,
        required: requiredKeys.includes("dealers"),
        props: {
          fieldName: "dealers",
          masterType: "dealers",
          label: "Dealer Codes",
        },
        colSpan: 1,
      });
    }

    if (selectedMeta?.showAreas) {
      fields.push({
        type: "custom",
        component: MasterSelect,
        required: requiredKeys.includes("areas"),
        props: {
          fieldName: "areas",
          masterType: "areas",
          label: "Area Codes",
        },
        colSpan: 1,
      });
    }

    if (selectedMeta?.showRegions) {
      fields.push({
        type: "custom",
        component: MasterSelect,
        required: requiredKeys.includes("regions"),
        props: {
          fieldName: "regions",
          masterType: "regions",
          label: "Region Codes",
        },
        colSpan: 1,
      });
    }

    if (selectedMeta?.showNos) {
      fields.push({
        type: "field",
        name: "nos",
        label: "Document No(s)",
        required: requiredKeys.includes("nos"),
        inputType: "text",
        placeholder: "Enter Doc No(s) separated by comma",
      });
    }

    return [
      {
        type: "grid",
        cols: 3,
        class: "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4",
        children: fields,
      },
    ] as FormSchema;
  });

  $effect(() => {
    if (!selectedMeta) return;
    const requiredKeys = selectedMeta.requiredFields
      ? selectedMeta.requiredFields.split(",").map((s) => s.trim())
      : [];

    const zodShape: any = {
      report: z.string().min(1, "Report is required"),
      dateRange: z.any().optional(),
      view: z.string().optional(),
      type: z.string().optional(),
      respCenters: z.string().optional(),
      customers: z.string().optional(),
      dealers: z.string().optional(),
      areas: z.string().optional(),
      regions: z.string().optional(),
      nos: z.string().optional(),
    };

    if (requiredKeys.includes("dateRange"))
      zodShape.dateRange = z
        .object({ start: z.any(), end: z.any() })
        .refine((d) => d && d.start && d.end, {
          message: "Date range is required",
        });
    if (requiredKeys.includes("view"))
      zodShape.view = z.string().min(1, "View mapping is required");
    if (requiredKeys.includes("type"))
      zodShape.type = z.string().min(1, "Type selection is mandatory");
    if (requiredKeys.includes("dealers"))
      zodShape.dealers = z.string().min(1, "Please select at least one dealer");
    if (requiredKeys.includes("respCenters") && showRespCentersSelect)
      zodShape.respCenters = z
        .string()
        .min(1, "Resp Center tracking is mandatory");
    if (requiredKeys.includes("customers"))
      zodShape.customers = z
        .string()
        .min(1, "Please select at least one customer");
    if (requiredKeys.includes("areas"))
      zodShape.areas = z
        .string()
        .min(1, "Please select at least one Area Code");
    if (requiredKeys.includes("regions"))
      zodShape.regions = z
        .string()
        .min(1, "Please select at least one Region Code");
    if (requiredKeys.includes("nos"))
      zodShape.nos = z
        .string()
        .min(1, "Please enter at least one Document No");

    form.setSchema(z.object(zodShape) as unknown as ZodSchema<ReportProductionForm>);
  });

  function toIso(date: unknown): string {
    if (!date) return "";
    if (typeof date === "string") return new Date(date).toISOString();
    if (
      date &&
      typeof date === "object" &&
      "toDate" in date &&
      typeof (date as any).toDate === "function"
    ) {
      return (date as any).toDate(getLocalTimeZone()).toISOString();
    }
    return "";
  }

  function splitCSV(val: string): string[] {
    return val
      ? val
          .split(/[,;\s]+/)
          .map((s) => s.trim())
          .filter(Boolean)
      : [];
  }

  function getParams(
    values: ReportProductionForm,
    format: string = "PDF",
  ): ReportFetchParams {
    const range = values.dateRange;
    const from = range?.start ? toIso(range.start) : "";
    const to = range?.end ? toIso(range.end) : "";
    const user = getUser();
    // Use form respCenters only when user has >1 production location (select is shown); else use user.respCenter
    const prodLocs = (authStore.get().locations ?? []).filter(
      (l: { production?: unknown }) => l.production === 1,
    );
    const useRespCentersSelect = prodLocs.length > 1;
    let respCenters: string[];
    if (useRespCentersSelect) {
      respCenters = splitCSV(values.respCenters);
      if (respCenters.length === 0 && user?.respCenter)
        respCenters = [user.respCenter];
    } else {
      respCenters = user?.respCenter ? [user.respCenter] : [];
    }

    return {
      from,
      to,
      reportOutput: format,
      view: values.view || "All",
      type: values.type || "",
      respCenters,
      customers: splitCSV(values.customers),
      dealers: splitCSV(values.dealers),
      areas: splitCSV(values.areas),
      regions: splitCSV(values.regions),
      nos: splitCSV(values.nos),
      entityCode: user?.entityCode ?? undefined,
      entityType: user?.entityType ?? undefined,
      entityDepartment: user?.department ?? undefined,
      userSpecialToken: authStore.get().userSpecialToken || user?.userSpecialToken,
    };
  }

  let pdfData = $state<Uint8Array | null>(null);
  let pdfFileName = $state<string>("report.pdf");
  let loading = $state(false);
  let error = $state<string | null>(null);

  async function generateReport(format: string) {
    error = null;
    pdfData = null;
    loading = true;
    const output = format || "PDF";
    try {
      const params = getParams(form.values, output);
      console.log("params", params);
      console.log("report", form.values.report);
      const { blob, fileName } = await exportProductionReport(form.values.report, params);

      if (output === "Excel") {
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        toast.success("Excel downloaded successfully");
      } else {
        const buffer = await blob.arrayBuffer();
        pdfData = new Uint8Array(buffer);
        pdfFileName = fileName;
      }
    } catch (e: any) {
      if (e.status === 401) {
        isUnauthorized = true;
        clearAuth();
        toast.error("Session expired. Please login again.");
      } else {
        const msg =
          e instanceof Error ? e.message : "Failed to generate report";
        error = msg;
        toast.error(msg);
      }
    } finally {
      loading = false;
    }
  }
</script>

<div class="min-h-screen bg-background pb-20">
  <PageHeading backHref="/" icon="printer">
    {#snippet title()}
      <div class="flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-3">
        <span>Production Reports</span>
        {#if showWorkDateBadge}
          <div
            class="flex items-center gap-1.5 px-2 py-0.5 sm:px-3 sm:py-1 rounded-full bg-primary/10 border border-primary/20 text-primary animate-in fade-in zoom-in duration-500 w-fit"
          >
            <Icon name="calendar-days" class="size-3 sm:size-3.5" />
            <span
              class="text-[9px] sm:text-[11px] font-semibold uppercase tracking-wider"
            >
              Work Date: {new Date(workDate!).toLocaleDateString("en-IN", {
                day: "2-digit",
                month: "short",
                year: "numeric",
              })}
            </span>
          </div>
        {/if}
      </div>
    {/snippet}
  </PageHeading>

  {#if isUnauthorized}
    <div
      class="flex flex-col items-center justify-center p-12 text-center animate-in fade-in slide-in-from-bottom-4 duration-500"
    >
      <div
        class="size-24 rounded-full bg-destructive/10 text-destructive flex items-center justify-center mb-6"
      >
        <Icon name="lock" class="size-12" />
      </div>
      <h2 class="text-2xl font-bold tracking-tight">Session Expired</h2>
      <p class="text-muted-foreground mt-2 max-w-sm">
        Your session has expired or you don't have permission to access these
        reports. Please sign in again to continue.
      </p>
      <Button
        onclick={() => goto("/login")}
        class="mt-8 px-8 rounded-xl h-12 text-base shadow-lg shadow-primary/20"
      >
        Back to Login
      </Button>
    </div>
  {:else if metaLoading && !reportMetaList}
    <div class="flex items-center justify-center p-20">
      <Icon name="loader-2" class="w-8 h-8 animate-spin text-primary" />
    </div>
  {:else}
    <ReportViewer
      {form}
      {schema}
      {pdfData}
      fileName={pdfFileName}
      outputFormats={selectedMeta?.outputFormats}
      isLoading={loading}
      onFilter={(format) => generateReport(format)}
      class="w-full"
    />

    {#if error}
      <div class="px-6 py-2">
        <div
          class="text-destructive text-sm bg-destructive/10 p-3 rounded-lg flex items-center gap-2"
        >
          <Icon name="alert-circle" class="w-4 h-4" />
          {error}
        </div>
      </div>
    {/if}
  {/if}
</div>
