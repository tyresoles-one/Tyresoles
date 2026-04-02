<script lang="ts">
	import { page } from "$app/stores";
	import { onMount } from "svelte";
	import { goto } from "$app/navigation";
	import { z, type ZodSchema } from "zod";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import ReportViewer from "$lib/components/venUI/report-viewer/ReportViewer.svelte";
	import { VenForm, type FormSchema } from "$lib/components/venUI/form";
	import { Button } from "$lib/components/ui/button";
	import { updateGoBackPath } from "$lib/components";
	import { toast } from "$lib/components/venUI/toast";
	import { Icon } from "$lib/components/venUI/icon";
	import { exportProductionReport } from "$lib/services/productionReports";
	import type { ReportFetchParams } from "$lib/services/salesReports";
	import { getUser, clearAuth } from "$lib/stores/auth";

	/** Parity with Tyresoles.Live `postedship/[orderID]/print` report menu (labels + report names). */
	const REPORT_MENU = [
		{ label: "Average Cost", value: "Casing Average Cost", icon: "IndianRupee" },
		{ label: "Details", value: "Posted Dispatch Details", icon: "List" },
		{ label: "Summary", value: "Posted Dispatch Order", icon: "File" },
		{ label: "Inspection", value: "Casing Inspection", icon: "File" },
		{ label: "New Numbering", value: "Casing New Numbering", icon: "ListPlus" },
		{ label: "Vendor Bills", value: "Vendor Bill", icon: "File" }
	] as const;

	type PostedShipPrintForm = { report: string };

	const orderId = $derived(decodeURIComponent($page.params.orderID ?? ""));
	const closePath = $derived(`/ecoproc/postedship/${orderId}`);

	const form = new VenForm<PostedShipPrintForm>({
		initialValues: { report: "Posted Dispatch Order" }
	});

	const schema = $derived.by<FormSchema>(() => [
		{
			type: "field",
			name: "report",
			label: "Report",
			inputType: "select",
			required: true,
			options: REPORT_MENU.map((m) => ({ label: m.label, value: m.value }))
		}
	]);

	$effect(() => {
		form.setSchema(
			z.object({
				report: z.string().min(1, "Select a report")
			}) as unknown as ZodSchema<PostedShipPrintForm>
		);
	});

	/** Show PDF + Excel for all posted-shipment production reports (backend may reject unsupported combos). */
	const outputFormats = "pdf,excel";

	function buildParams(format: string): ReportFetchParams {
		const user = getUser();
		const r = form.values.report;
		return {
			from: "",
			to: "",
			reportOutput: format,
			view: "All",
			//type: r === "Vendor Bill" ? "Posted" : "",
			respCenters: user?.respCenter ? [user.respCenter] : [],
			nos: orderId ? [orderId] : [],
			entityCode: user?.entityCode ?? undefined,
			entityType: user?.entityType ?? undefined,
			entityDepartment: user?.department ?? undefined
		};
	}

	let pdfData = $state<Uint8Array | null>(null);
	let pdfFileName = $state("report.pdf");
	let loading = $state(false);
	let isUnauthorized = $state(false);

	async function generateReport(format: string) {
		isUnauthorized = false;
		pdfData = null;
		loading = true;
		const output = format || "PDF";
		try {
			const params = buildParams(output);
			const { blob, fileName } = await exportProductionReport(form.values.report, params);

			if (output === "Excel" || output === "Word") {
				const url = URL.createObjectURL(blob);
				const a = document.createElement("a");
				a.href = url;
				a.download = fileName;
				document.body.appendChild(a);
				a.click();
				document.body.removeChild(a);
				URL.revokeObjectURL(url);
				toast.success(`${output} downloaded successfully`);
			} else {
				const buffer = await blob.arrayBuffer();
				pdfData = new Uint8Array(buffer);
				pdfFileName = fileName;
			}
		} catch (e: unknown) {
			const status = (e as { status?: number })?.status;
			if (status === 401) {
				isUnauthorized = true;
				clearAuth();
				toast.error("Session expired. Please login again.");
			} else {
				const msg = e instanceof Error ? e.message : "Failed to generate report";
				toast.error(msg);
			}
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		updateGoBackPath(closePath);
	});
</script>

<div class="min-h-screen bg-background pb-safe">
	<PageHeading backHref={closePath} icon="printer" pageTitle="Posted shipment print">
		{#snippet title()}
			<span class="truncate font-semibold tracking-tight">Shipment {orderId}</span>
		{/snippet}
		{#snippet description()}
			<span class="text-muted-foreground">Choose a report variant, then generate (PDF / Excel where available).</span>
		{/snippet}
	</PageHeading>

	{#if isUnauthorized}
		<div class="flex flex-col items-center justify-center px-4 py-16 text-center">
			<div class="mb-6 flex size-24 items-center justify-center rounded-full bg-destructive/10 text-destructive">
				<Icon name="lock" class="size-12" />
			</div>
			<h2 class="text-2xl font-bold tracking-tight">Session Expired</h2>
			<p class="mt-2 max-w-sm text-muted-foreground">
				Your session expired or you do not have permission to run this report.
			</p>
			<Button class="mt-8" onclick={() => goto("/login")}>Back to Login</Button>
		</div>
	{:else}
		<div class="container mx-auto max-w-4xl px-3 py-4 sm:px-4">
			

			<ReportViewer
				{form}
				{schema}
				{pdfData}
				fileName={pdfFileName}
				{outputFormats}
				isLoading={loading}
				onFilter={(format) => generateReport(format)}
				class="w-full"
			/>
		</div>
	{/if}
</div>
