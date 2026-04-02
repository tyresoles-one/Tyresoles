<script lang="ts">
  import ReportViewer from "$lib/components/venUI/report-viewer/ReportViewer.svelte";
  import { CreateForm } from "$lib/components/venUI/form/form.svelte.ts";
  import { z } from "zod";
  import type { FormSchema } from "$lib/components/venUI/form/types";
  import { toast } from "$lib/components/venUI/toast";

  const schema: FormSchema = [
    {
      type: "group",
      class: "grid grid-cols-1 md:grid-cols-2 gap-4",
      children: [
        {
          type: "field",
          name: "startDate",
          inputType: "date-picker",
          label: "Start Date",
          placeholder: "Select start date",
          colSpan: 1,
        },
        {
          type: "field",
          name: "endDate",
          inputType: "date-picker",
          label: "End Date",
          placeholder: "Select end date",
          colSpan: 1,
        },
        {
          type: "field",
          name: "department",
          inputType: "select",
          label: "Department",
          placeholder: "Select department",
          options: [
            { label: "Sales", value: "sales" },
            { label: "Marketing", value: "marketing" },
            { label: "Engineering", value: "engineering" },
            { label: "HR", value: "hr" },
          ],
          colSpan: 1,
        },
        {
          type: "field",
          name: "status",
          inputType: "select",
          label: "Status",
          placeholder: "Select status",
          options: [
            { label: "Active", value: "active" },
            { label: "Inactive", value: "inactive" },
            { label: "Pending", value: "pending" },
          ],
          colSpan: 1,
        },
      ],
    },
  ];

  const zodSchema = z.object({
    startDate: z.string().min(1, "Start date is required"),
    endDate: z.string().min(1, "End date is required"),
    department: z.string().optional(),
    status: z.string().optional(),
  });

  const form = CreateForm({
    initialValues: {},
    zodSchema,
    onSubmit: async (values) => {
      console.log("Form submitted:", values);
    },
  });

  let pdfUrl = $state("");
  let isLoading = $state(false);

  async function handleFilter() {
    const isValid = await form.validate();
    if (!isValid) {
      toast.error("Please fix validation errors");
      return;
    }

    isLoading = true;

    // Simulate API call delay
    setTimeout(() => {
      // faster-web.pdf is a sample PDF often found in dev environments or we can use a placeholder
      // For this demo, we'll try to use a common sample if available, or just a placeholder URL that won't load but shows the UI
      // Using a dummy PDF URL for demonstration
      pdfUrl =
        "https://raw.githubusercontent.com/mozilla/pdf.js/ba2edeae/examples/learning/helloworld.pdf";
      isLoading = false;
      toast.success("Report generated successfully");
    }, 1500);
  }
</script>

<div class="container mx-auto py-10 space-y-6">
  <div class="space-y-2">
    <h1 class="text-3xl font-bold">Report Viewer Example</h1>
    <p class="text-muted-foreground">
      A combined component for filtering data and viewing the resulting PDF
      report.
    </p>
  </div>

  <ReportViewer
    {form}
    {schema}
    {pdfUrl}
    {isLoading}
    onFilter={handleFilter}
    fileName="department-report.pdf"
  />
</div>
