<script lang="ts">
  import * as Accordion from "$lib/components/ui/accordion";
  import * as ToggleGroup from "$lib/components/ui/toggle-group";
  import FormGenerator from "$lib/components/venUI/form/form-generator.svelte";
  import PdfViewer from "$lib/components/venUI/pdf-viewer/PdfViewer.svelte";
  import { Button } from "$lib/components/ui/button";
  import type { FormSchema } from "$lib/components/venUI/form/types";
  import type { VenForm } from "$lib/components/venUI/form/form.svelte.ts";
  import { cn } from "$lib/utils";
  import { Icon } from "$lib/components/venUI/icon";

  export type ReportOutputFormat = "PDF" | "Excel" | "Word";

  const FORMAT_OPTIONS: {
    value: ReportOutputFormat;
    icon: string;
    label: string;
  }[] = [
    { value: "PDF", icon: "file-text", label: "PDF" },
    { value: "Excel", icon: "table", label: "Excel" },
    { value: "Word", icon: "file-type", label: "Word" },
  ];

  let {
    form,
    schema,
    pdfUrl = "",
    pdfData = null,
    fileName = "report.pdf",
    isLoading = false,
    onFilter = () => {},
    /** When true (default), collapse the filter panel and show only report preview after generate completes. */
    collapseFilterAfterGenerate = true,
    /** Default export format when not bound. */
    defaultExportFormat = "PDF" as ReportOutputFormat,
    /** Comma-separated list of allowed formats (e.g. "pdf,excel"). If empty, show all. */
    outputFormats = "",
    class: className = "",
  } = $props<{
    form: VenForm<any>;
    schema: FormSchema;
    pdfUrl?: string;
    pdfData?: Uint8Array | ArrayBuffer | null;
    fileName?: string;
    isLoading?: boolean;
    /** Called when Generate is clicked; receives the selected export format (PDF | Excel | Word). */
    onFilter?: (format: ReportOutputFormat) => void;
    collapseFilterAfterGenerate?: boolean;
    defaultExportFormat?: ReportOutputFormat;
    /** Comma-separated allowed formats (e.g. "pdf,excel"). */
    outputFormats?: string;
    class?: string;
  }>();

  let accordionValue = $state(["filter", "report"]);
  let wasLoading = $state(false);
  let exportFormat = $state<ReportOutputFormat>(defaultExportFormat);
  let dataObjectUrl = $state<string | null>(null);

  const enabledFormatOptions = $derived.by(() => {
    if (!outputFormats) return FORMAT_OPTIONS;
    const allowed = outputFormats
      .toLowerCase()
      .split(",")
      .map((s: string) => s.trim());
    return FORMAT_OPTIONS.filter((o) => {
      const val = o.value.toLowerCase();
      // Match "pdf", "excel", "word" or extensions
      return (
        allowed.includes(val) ||
        (val === "excel" &&
          (allowed.includes("xlsx") || allowed.includes("xls"))) ||
        (val === "word" &&
          (allowed.includes("docx") || allowed.includes("doc")))
      );
    });
  });

  // Keep selection valid when some formats are disabled
  $effect(() => {
    if (
      enabledFormatOptions.length &&
      !enabledFormatOptions.some((o) => o.value === exportFormat)
    ) {
      exportFormat = enabledFormatOptions[0].value;
    }
  });

  // Object URL for pdfData when used as download (non-PDF). Only depend on pdfData
  // so we don't re-run when dataObjectUrl changes (avoids infinite loop).
  $effect(() => {
    const data = pdfData;
    if (!data) {
      return; // cleanup from previous run will have set dataObjectUrl = null
    }
    const blob =
      data instanceof ArrayBuffer ? new Blob([data]) : new Blob([data]);
    const url = URL.createObjectURL(blob);
    dataObjectUrl = url;
    return () => {
      URL.revokeObjectURL(url);
      dataObjectUrl = null;
    };
  });

  const downloadHref = $derived(pdfUrl || dataObjectUrl || "");
  const isPdf = $derived(exportFormat === "PDF");
  const showDownloadCard = $derived(!isPdf && (!!pdfUrl || !!pdfData));

  // When generate completes, hide filter panel and show only report (and scroll to it)
  $effect(() => {
    const loading = isLoading;
    if (
      wasLoading &&
      !loading &&
      (pdfData || pdfUrl) &&
      collapseFilterAfterGenerate
    ) {
      accordionValue = ["report"];
      setTimeout(() => {
        document
          .getElementById("ven-report-viewer-preview")
          ?.scrollIntoView({ behavior: "smooth", block: "start" });
      }, 300);
    }
    wasLoading = loading;
  });

  async function handleFilterClick() {
    const isValid = await form.validate();
    console.log("isValid", isValid, form.errors);
    if (!isValid) return;

    onFilter(exportFormat);
    if (window.innerWidth < 768) {
      accordionValue = ["report"];
      setTimeout(() => {
        document
          .getElementById("ven-report-viewer-preview")
          ?.scrollIntoView({ behavior: "smooth", block: "start" });
      }, 300);
    }
  }
</script>

<div class={cn("flex w-full flex-col pb-safe", className)}>
  <Accordion.Root type="multiple" bind:value={accordionValue} class="w-full">
    <Accordion.Item value="filter">
      <Accordion.Trigger
        class="px-4 py-3 hover:no-underline hover:bg-muted/50 transition-colors"
      >
        <div class="flex items-center gap-2 font-medium">
          <Icon name="filter" class="size-4" />
          <span>Filter Criteria</span>
        </div>
      </Accordion.Trigger>
      <Accordion.Content>
        <div class="p-4 pt-1">
          <div
            class="rounded-lg border bg-card p-4 text-card-foreground shadow-sm"
          >
            <!-- Added pb-4 to form container for extra spacing on mobile -->
            <div class="space-y-6 pb-2">
              <FormGenerator {schema} {form} root={true} />

              <!-- Mobile: export on top, buttons below. Desktop: same row -->
              <div
                class="flex flex-col sm:flex-row sm:flex-wrap sm:items-center gap-3 pt-4 border-t border-border/50 mt-4"
              >
                <!-- Export as: label on desktop only, toggle (inverted colors) -->
                <div
                  class="flex items-center gap-2 flex-wrap min-w-0 w-full sm:w-auto sm:flex-1"
                >
                  <span
                    class="hidden sm:inline text-sm font-medium text-muted-foreground shrink-0"
                    >Export as</span
                  >
                  <ToggleGroup.Root
                    type="single"
                    bind:value={exportFormat}
                    variant="outline"
                    size="sm"
                    class={cn(
                      "inline-flex rounded-xl p-1 gap-0.5 min-w-0 w-full sm:w-auto",
                      "bg-muted/40 border border-border/50",
                      "data-[variant=outline]:border-0 data-[variant=outline]:shadow-none",
                    )}
                    aria-label="Export format"
                  >
                    {#each enabledFormatOptions as opt}
                      <ToggleGroup.Item
                        value={opt.value}
                        class={cn(
                          "flex items-center justify-center gap-2 rounded-lg px-3 py-2.5 min-h-9 flex-1 sm:flex-initial min-w-0",
                          "data-[state=on]:bg-primary data-[state=on]:text-primary-foreground data-[state=on]:shadow-sm",
                          "data-[state=off]:bg-transparent data-[state=off]:text-muted-foreground data-[state=off]:hover:bg-muted/60 data-[state=off]:hover:text-foreground",
                          "transition-all duration-200",
                        )}
                        aria-label={opt.label}
                      >
                        <Icon name={opt.icon} class="size-4 shrink-0" />
                        <span class="text-sm font-medium">{opt.label}</span>
                      </ToggleGroup.Item>
                    {/each}
                  </ToggleGroup.Root>
                </div>

                <!-- Reset + Generate -->
                <div
                  class="flex flex-wrap items-center gap-2 shrink-0 w-full sm:w-auto flex-col-reverse sm:flex-row"
                >
                  <Button
                    variant="outline"
                    class="w-full sm:w-auto"
                    onclick={() => form.reset()}
                    disabled={isLoading}
                  >
                    Reset
                  </Button>
                  <Button
                    onclick={handleFilterClick}
                    disabled={isLoading}
                    class="w-full sm:w-auto min-w-[140px]"
                  >
                    {#if isLoading}
                      <Icon name="loader-2" class="mr-2 size-4 animate-spin" />
                      Generating...
                    {:else}
                      <Icon name="file-text" class="mr-2 size-4" />
                      Generate Report
                    {/if}
                  </Button>
                </div>
              </div>
            </div>
            <!-- Spacer for keyboard handling on mobile -->
            <div class="h-0 sm:h-0 transition-all duration-200"></div>
          </div>
        </div>
      </Accordion.Content>
    </Accordion.Item>

    <Accordion.Item value="report" id="ven-report-viewer-preview">
      <Accordion.Trigger
        class="px-4 py-3 hover:no-underline hover:bg-muted/50 transition-colors"
      >
        <div class="flex items-center gap-2 font-medium">
          <Icon name="file-box" class="size-4" />
          <span>Report Preview</span>
        </div>
      </Accordion.Trigger>
      <Accordion.Content>
        <div class="p-4 pt-1">
          <div
            class={cn(
              "relative flex w-full flex-col overflow-hidden rounded-lg border bg-muted/10 transition-all",
              "h-[60vh] min-h-[400px] md:h-[800px]",
            )}
          >
            {#if !pdfUrl && !pdfData && !isLoading}
              <div
                class="flex h-full flex-col items-center justify-center text-muted-foreground p-6 text-center"
              >
                <div class="mb-4 rounded-full bg-muted p-6">
                  <Icon name="file-search" class="size-12 opacity-50" />
                </div>
                <h3 class="text-lg font-medium">No Report Generated</h3>
                <p class="text-sm max-w-xs mx-auto mt-1">
                  Please apply filters above and click Generate to view the
                  report.
                </p>
              </div>
            {:else if isLoading && !pdfUrl && !pdfData}
              <div
                class="flex h-full flex-col items-center justify-center text-muted-foreground"
              >
                <Icon
                  name="loader-2"
                  class="mb-4 size-10 animate-spin text-primary"
                />
                <p>Generating report...</p>
              </div>
            {:else if showDownloadCard && downloadHref}
              <div
                class="flex h-full flex-col items-center justify-center p-8 text-center"
              >
                <div
                  class="flex max-w-md flex-col items-center gap-6 rounded-2xl border border-border bg-card px-8 py-10 shadow-sm"
                >
                  <div
                    class="flex size-16 items-center justify-center rounded-full bg-primary/10 text-primary"
                  >
                    <Icon
                      name={exportFormat === "Excel" ? "table" : "file-type"}
                      class="size-8"
                    />
                  </div>
                  <div class="space-y-1">
                    <h3 class="text-lg font-semibold">Report ready</h3>
                    <p class="text-sm text-muted-foreground">
                      Your {exportFormat} report has been generated. Download it below.
                    </p>
                  </div>
                  <a
                    href={downloadHref}
                    download={fileName}
                    class={cn(
                      "inline-flex items-center justify-center gap-2 rounded-lg px-6 py-3 text-sm font-medium",
                      "bg-primary text-primary-foreground shadow-sm",
                      "hover:bg-primary/90 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
                      "transition-colors",
                    )}
                  >
                    <Icon name="download" class="size-4" />
                    Download {exportFormat}
                  </a>
                </div>
              </div>
            {:else}
              <PdfViewer url={pdfUrl} data={pdfData} {fileName} />
            {/if}
          </div>
        </div>
      </Accordion.Content>
    </Accordion.Item>
  </Accordion.Root>
</div>
