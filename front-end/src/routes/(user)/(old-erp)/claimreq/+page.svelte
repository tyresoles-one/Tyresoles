<script lang="ts">
	import { onMount } from "svelte";
	import { apiFetch, endpoints, getURLSearchParams } from "$lib/managers/network";
	import { PageWindow, Grid, toast } from "$lib/components";
	import { Select } from "$lib/components/venUI/select";
	import { Input } from "$lib/components/ui/input";
	import { Button } from "$lib/components/ui/button";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import type { InvoiceMapper, InvLineMapper } from "$lib/business/models";

	let data = $state<InvoiceMapper>({
		oldCompany: "",
		invoiceNo: ""
	});

	let companies = $state<{ code: string; name: string }[]>([]);
	let records = $state<InvLineMapper[]>([]);
	let selected = $state<Map<string, object>>(new Map());
	let selectedValues = $state<Set<string>>(new Set());
	let loading = $state(false);
	let fetchingDetails = $state(false);
	let submittingClaim = $state(false);

	onMount(() => {
		loading = true;
		apiFetch<string[]>(
			`${endpoints.merger.oldCompanies}?${getURLSearchParams({ business: "tyre" }).toString()}`,
			{ method: "GET" }
		).then((resp) => {
			loading = false;
			if (resp.success && Array.isArray(resp.data)) {
				companies = resp.data.map((x: string) => ({ code: x, name: x }));
			}
		});
	});

	const handleGetDetails = () => {
		if (!data.oldCompany || !data.invoiceNo) {
			toast.error("Please select company and enter invoice no");
			return;
		}
		records = [];
		selected.clear();
		selectedValues = new Set();
		fetchingDetails = true;

		apiFetch<InvLineMapper[]>(endpoints.merger.getOldInvLines, {
			method: "POST",
			body: data
		}).then((resp) => {
			fetchingDetails = false;
			if (resp.success && Array.isArray(resp.data)) {
				records = resp.data;
				if (records.length === 0) {
					toast.info("No records found for specified invoice");
				}
			} else {
				toast.error(resp.error ?? "Failed to fetch invoice lines");
			}
		});
	};

	const handleCreateClaim = () => {
		if (selected.size === 0) {
			toast.error("Please select a record to create claim request");
			return;
		}
		if (selected.size > 1) {
			toast.error("Please select only one record to create claim request");
			return;
		}
		let line = Array.from(selected.values())[0] as InvLineMapper;
		submittingClaim = true;

		apiFetch<string>(endpoints.merger.createClaimOnOldInv, {
			method: "POST",
			body: line
		}).then((resp) => {
			submittingClaim = false;
			if (resp.success) {
				toast.success(`Claim ${resp.data} created successfully`);
				selected.clear();
				selectedValues = new Set();
			} else {
				toast.error(resp.error ?? "Failed to create claim request");
			}
		});
	};
</script>

<div class="space-y-6">
	<PageHeading title="Claim Request (Old ERP)" description="Create claim request on old invoice lines" />

	<PageWindow {loading}>
		<div class="rounded-lg border bg-card p-4 shadow-xs space-y-4">
			<div class="grid grid-cols-1 md:grid-cols-3 gap-4 items-end">
				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="company-select">
						Select Company
					</label>
					<Select
						options={companies}
						bind:value={data.oldCompany}
						valueKey="code"
						labelKey="name"
						placeholder="Select Company..."
						class="w-full h-9"
					/>
				</div>

				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="invoice-no-input">
						Invoice No
					</label>
					<Input
						id="invoice-no-input"
						type="text"
						placeholder="Enter Invoice No"
						bind:value={data.invoiceNo}
						class="h-9"
					/>
				</div>

				<div>
					<Button
						type="button"
						variant="default"
						class="h-9 w-full md:w-auto px-6"
						onclick={handleGetDetails}
						disabled={fetchingDetails}
					>
						{#if fetchingDetails}
							Fetching...
						{:else}
							Get Details
						{/if}
					</Button>
				</div>
			</div>
		</div>

		<div class="mt-4">
			{#if records.length === 0}
				<div class="text-center py-12 text-sm text-muted-foreground border rounded-lg bg-card">
					No records to display
				</div>
			{:else}
				<Grid
					actions={[
						{
							label: submittingClaim ? "Creating..." : "Create Claim",
							onclick: handleCreateClaim,
							disabled: submittingClaim
						}
					]}
					enableSelection
					selectionType="single"
					dataKey="lineNo"
					data={records}
					bind:selectedValues
					onSelectionChange={(selection) => (selected = selection)}
					columns={[
						{ name: "tyre", label: "Tyre" },
						{ name: "serialNo", label: "Serial No" },
						{ name: "make", label: "Make" }
					]}
				/>
			{/if}
		</div>
	</PageWindow>
</div>
