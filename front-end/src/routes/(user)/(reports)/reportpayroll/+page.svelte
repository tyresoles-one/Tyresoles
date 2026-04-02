<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { page } from "$app/stores";
  import { toast } from "$lib/components/venUI/toast";
  import { z, type ZodSchema } from "zod";
  import {
    exportPayrollReport,
    getPayrollReportMeta,
  } from "$lib/services/payrollReports";
  import type {
    ReportFetchParams,
    ReportMeta,
  } from "$lib/services/salesReports";
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
    CalendarDateTime,
    toZoned,
  } from "@internationalized/date";
  import { authStore, getUser, clearAuth } from "$lib/stores/auth";
  import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";

  /** Optional comma-separated report codes from menu item with action `/reportpayroll`. */
  function getReportpayrollMenuOptions(): string | undefined {
    const menus = authStore.get().menus ?? [];
    for (const menu of menus) {
      for (const sub of menu.subMenus ?? []) {
        for (const item of sub.items ?? []) {
          const action = (item.action ?? "").trim().replace(/\/+$/, "") || "";          
          if (action === "/reportpayroll"){            
            return item.options?.trim() || undefined;
          }
            
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
            return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
          } catch {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        } else {
          try {
            return parseDate(workDate);
          } catch {
            return toCalendarDate(
              fromDate(new Date(workDate), getLocalTimeZone()),
            );
          }
        }
      }
      return today(getLocalTimeZone());
    } catch {
      return today(getLocalTimeZone());
    }
  });

  const showWorkDateBadge = $derived(
    workDate && ref.toString() !== today(getLocalTimeZone()).toString(),
  );

  /** Locations where payroll === 1 (user can run payroll reports for these). */
  const payrollLocations = $derived.by(() => {
    const locs = $authStore.locations ?? [];
    return locs.filter((l: { payroll?: unknown }) => l.payroll === 1);
  });

  type ReportPayrollForm = {
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

  const initialValues: ReportPayrollForm = {
    report: "",
    dateRange: undefined,
    view: "All",
    type: "",
    respCenters: "",
    customers: "",
    dealers: "",
    areas: "",
    regions: "",
    nos: "",
  };

  const form = new VenForm<ReportPayrollForm>({ initialValues });

  let reportMetaList = $state<ReportMeta[] | null>(null);
  let metaLoading = $state(true);
  let isUnauthorized = $state(false);

  const selectedMeta = $derived(
    reportMetaList?.find((m) => m.name === form.values.report),
  );

  /** Show resp. centers when multiple payroll locations or report requires it. */
  const showRespCentersSelect = $derived(
    payrollLocations.length > 1 || (selectedMeta?.showRespCenters ?? false),
  );

  function getDateRangeForPreset(presetString: string | undefined): {
    start: unknown;
    end: unknown;
  } {
    if (!presetString) return { start: ref, end: ref };

    const firstKey = presetString.split(",")[0].trim();
    const rangeLib = DatePresets.range as Record<
      string,
      (r: typeof ref) => { value: { start: unknown; end: unknown } }
    >;
    if (rangeLib[firstKey]) {
      const p = rangeLib[firstKey](ref);
      return p.value;
    }

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

  onMount(() => {
    const user = authStore.get().user;
    const urlId = $page.url.searchParams.get("id");
    const menuOpts = getReportpayrollMenuOptions();

    console.log("menuOpts", menuOpts);
    getPayrollReportMeta(getReportpayrollMenuOptions() ?? undefined)
      .then((list) => {
        let filtered: ReportMeta[] = list ?? [];
        console.log("list", filtered, list);
        if (menuOpts) {
          const codes = menuOpts
            .split(",")
            .map((s) => s.trim())
            .filter(Boolean);
          if (codes.length > 0) {
            filtered = filtered.filter((m) => codes.includes(m.code));
          }
        }
        console.log("filtered", filtered);
        reportMetaList = filtered;
        console.log("reportMetaList", reportMetaList);
        if (filtered.length > 0 && !form.values.report) {
          if (urlId) {
            const matched = filtered.find(
              (m) =>
                m.code === urlId ||
                m.id.toString() === urlId ||
                m.name === urlId,
            );
            form.setValue("report", matched ? matched.name : filtered[0].name);
          } else {
            form.setValue("report", filtered[0].name);
          }
        }
      })
      .catch((err: { status?: number; message?: string }) => {
        if (err.status === 401) {
          isUnauthorized = true;
          clearAuth();
        } else {
          reportMetaList = [];
          toast.error(err.message || "Failed to load payroll report settings");
        }
      })
      .finally(() => {
        metaLoading = false;
      });
  });

  // Update date range when report or workDate changes
  $effect(() => {
    if (selectedMeta) {
      const range = getDateRangeForPreset(selectedMeta.datePreset);
      form.setValue("dateRange", range);
    }
  });

  const schema = $derived.by<FormSchema>(() => {
    if (!reportMetaList?.length) return [] as FormSchema;

    const requiredKeys = selectedMeta?.requiredFields
      ? selectedMeta.requiredFields.split(",").map((n) => n.trim())
      : [];

    const fields: unknown[] = [
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
          respCenterType: "Payroll",
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

    const zodShape: Record<string, z.ZodTypeAny> = {
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
        .refine((d) => d != null && d.start != null && d.end != null, {
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
        .min(1, "Resp. center selection is required");
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
      zodShape.nos = z.string().min(1, "Please enter at least one document no.");

    form.setSchema(z.object(zodShape) as unknown as ZodSchema<ReportPayrollForm>);
  });

  /** Inclusive date filters for SQL: backend uses `sr.[Date] >= @from AND sr.[Date] <= @to`. Same calendar day must use start-of-day and end-of-day in local TZ — otherwise `from` and `to` are identical instants and rows for that day are excluded. */
  function toIsoStartOfLocalDay(date: unknown): string {
    if (!date) return "";
    try {
      const cd =
        typeof date === "string"
          ? parseDate(
              date.includes("T") ? (date.split("T")[0] as string) : date,
            )
          : toCalendarDate(date as never);
      const dt = new CalendarDateTime(
        cd.year,
        cd.month,
        cd.day,
        0,
        0,
        0,
        0,
      );
      return toZoned(dt, getLocalTimeZone()).toDate().toISOString();
    } catch {
      return "";
    }
  }

  function toIsoEndOfLocalDay(date: unknown): string {
    if (!date) return "";
    try {
      const cd =
        typeof date === "string"
          ? parseDate(
              date.includes("T") ? (date.split("T")[0] as string) : date,
            )
          : toCalendarDate(date as never);
      const dt = new CalendarDateTime(
        cd.year,
        cd.month,
        cd.day,
        23,
        59,
        59,
        999,
      );
      return toZoned(dt, getLocalTimeZone()).toDate().toISOString();
    } catch {
      return "";
    }
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
    values: ReportPayrollForm,
    format: string = "PDF",
  ): ReportFetchParams {
    const range = values.dateRange;
    const from = range?.start ? toIsoStartOfLocalDay(range.start) : "";
    const to = range?.end ? toIsoEndOfLocalDay(range.end) : "";
    const user = getUser();
    const payrollLocs = (authStore.get().locations ?? []).filter(
      (l: { payroll?: unknown }) => l.payroll === 1,
    );
    const meta = reportMetaList?.find((m) => m.name === values.report);
    const useRespCentersSelect =
      payrollLocs.length > 1 || (meta?.showRespCenters ?? false);
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
      const { blob, fileName } = await exportPayrollReport(
        form.values.report,
        params,
        output,
      );

      const isExcel =
        output.toUpperCase() === "EXCEL" || output === "Excel";
      if (isExcel) {
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
    } catch (e: unknown) {
      const status = e && typeof e === "object" && "status" in e ? (e as { status?: number }).status : undefined;
      if (status === 401) {
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
        <span>Payroll Reports</span>
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
  {:else if metaLoading && reportMetaList === null}
    <div class="flex items-center justify-center p-20">
      <Icon name="loader-2" class="w-8 h-8 animate-spin text-primary" />
    </div>
  {:else if !reportMetaList?.length}
    <div
      class="mx-auto max-w-md p-12 text-center text-muted-foreground border border-dashed rounded-xl mt-8"
    >
      <Icon name="file-search" class="size-10 mx-auto mb-3 opacity-60" />
      <p class="font-medium text-foreground">No payroll reports available</p>
      <p class="text-sm mt-2">
        There are no reports for your account, or menu filters excluded all
        items.
      </p>
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
