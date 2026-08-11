<script lang="ts">
	import { onMount } from "svelte";
	import { apiFetch, endpoints, getURLSearchParams } from "$lib/managers/network";
	import { PageWindow, toast } from "$lib/components";
	import { Select } from "$lib/components/venUI/select";
	import { Input } from "$lib/components/ui/input";
	import { Button } from "$lib/components/ui/button";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import type { EntityMapper } from "$lib/business/models";

	let companies = $state<{ code: string; name: string }[]>([]);
	let respCenters = $state<{ code: string; name: string }[]>([]);
	let partyTypes = [
		{ code: "Customer", name: "Customer" },
		{ code: "Vendor", name: "Vendor" }
	];

	let record = $state<EntityMapper>({
		newCode: "",
		oldCode: "",
		type: "",
		oldCompany: "",
		oldRespCenter: "",
		name: ""
	});

	let loading = $state(false);
	let fetchingRespCenters = $state(false);
	let checkingMapper = $state(false);
	let creatingCode = $state(false);

	onMount(() => {
		loading = true;
		apiFetch<string[]>(endpoints.merger.oldCompanies, {
			method: "GET"
		}).then((resp) => {
			loading = false;
			if (resp.success && Array.isArray(resp.data)) {
				companies = resp.data.map((x: string) => ({ code: x, name: x }));
			}
		});
	});

	$effect(() => {
		const company = record.oldCompany;
		record.oldRespCenter = "";
		respCenters = [];
		if (!company) return;

		fetchingRespCenters = true;
		apiFetch<string[]>(
			`${endpoints.merger.oldRespCenters}?${getURLSearchParams({ company }).toString()}`,
			{ method: "GET" }
		).then((resp) => {
			fetchingRespCenters = false;
			if (resp.success && Array.isArray(resp.data)) {
				respCenters = resp.data.map((x: string) => ({ code: x, name: x }));
			}
		});
	});

	const handleCheck = () => {
		if (!record.oldCompany) {
			toast.error("Please select an Old Company");
			return;
		}
		if (!record.oldRespCenter) {
			toast.error("Please select an Old Resp Center");
			return;
		}
		if (!record.type) {
			toast.error("Please select a Party Type");
			return;
		}
		if (!record.oldCode) {
			toast.error("Please enter a Party Code");
			return;
		}

		checkingMapper = true;
		apiFetch<EntityMapper>(endpoints.merger.getMapper, {
			method: "POST",
			body: record
		}).then((resp) => {
			checkingMapper = false;
			if (resp.success && resp.data) {
				record = resp.data;
				if (!record.name) {
					toast.info("No party found for specified details");
				}
			} else {
				toast.error(resp.error ?? "Failed to check entity mapper");
			}
		});
	};

	const handleCreateNewCode = () => {
		creatingCode = true;
		apiFetch<string>(endpoints.merger.prepareEntity, {
			method: "POST",
			body: record
		}).then((resp) => {
			creatingCode = false;
			if (resp.success && resp.data) {
				toast.success(`New code ${resp.data} created successfully`);
				record.newCode = resp.data;
			} else {
				toast.error(resp.error ?? "Failed to create new entity code");
			}
		});
	};
</script>

<div class="space-y-6">
	<PageHeading title="Merge Entity (Old ERP)" description="Map and create new entity codes from old ERP" />

	<PageWindow {loading}>
		<div class="rounded-lg border bg-card p-4 shadow-xs space-y-4">
			<div class="grid grid-cols-1 md:grid-cols-5 gap-4 items-end">
				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="company-select">
						Old Company
					</label>
					<Select
						options={companies}
						bind:value={record.oldCompany}
						valueKey="code"
						labelKey="name"
						placeholder="Select Company..."
						class="w-full h-9"
					/>
				</div>

				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="respcenter-select">
						Old Resp Center
					</label>
					<Select
						options={respCenters}
						bind:value={record.oldRespCenter}
						valueKey="code"
						labelKey="name"
						placeholder={fetchingRespCenters ? "Loading..." : "Select Resp Center..."}
						disabled={fetchingRespCenters || !record.oldCompany}
						class="w-full h-9"
					/>
				</div>

				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="party-type-select">
						Party Type
					</label>
					<Select
						options={partyTypes}
						bind:value={record.type}
						valueKey="code"
						labelKey="name"
						placeholder="Select Type..."
						class="w-full h-9"
					/>
				</div>

				<div class="space-y-1.5">
					<label class="text-xs font-medium text-muted-foreground" for="party-code-input">
						Party Code
					</label>
					<Input
						id="party-code-input"
						type="text"
						placeholder="Enter Party Code"
						bind:value={record.oldCode}
						class="h-9"
					/>
				</div>

				<div>
					<Button
						type="button"
						variant="default"
						class="h-9 w-full px-6"
						onclick={handleCheck}
						disabled={checkingMapper}
					>
						{#if checkingMapper}
							Checking...
						{:else}
							Check
						{/if}
					</Button>
				</div>
			</div>
		</div>

		{#if record.name}
			<div class="mt-6 rounded-lg border bg-card p-4 shadow-xs">
				<div class="overflow-x-auto">
					<table class="w-full text-sm text-left">
						<thead class="text-xs text-muted-foreground uppercase bg-muted/50 border-b">
							<tr>
								<th scope="col" class="px-6 py-3">Party Name</th>
								{#if record.newCode}
									<th scope="col" class="px-6 py-3">New Code</th>
								{:else}
									<th scope="col" class="px-6 py-3">Action</th>
								{/if}
							</tr>
						</thead>
						<tbody class="divide-y">
							<tr class="bg-background">
								<td class="px-6 py-4 font-medium text-foreground">{record.name}</td>
								{#if record.newCode}
									<td class="px-6 py-4">
										<span class="inline-flex items-center rounded-md bg-green-500/10 px-2.5 py-1 text-xs font-medium text-green-600 ring-1 ring-inset ring-green-500/20">
											{record.newCode}
										</span>
									</td>
								{:else}
									<td class="px-6 py-4">
										<Button
											type="button"
											variant="default"
											size="sm"
											onclick={handleCreateNewCode}
											disabled={creatingCode}
										>
											{#if creatingCode}
												Creating...
											{:else}
												Create New Code
											{/if}
										</Button>
									</td>
								{/if}
							</tr>
						</tbody>
					</table>
				</div>
			</div>
		{/if}
	</PageWindow>
</div>
