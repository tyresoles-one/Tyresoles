<script lang="ts">
	import { get } from "svelte/store";
	import { fetchParamsStore, settingsStore, ensureFetchParams } from "$lib/managers/stores";
	import { Grid, type TableColumn, type ButtonProps, updateGoBackPath, toast } from "$lib/components";
	import { onMount, goto } from "$lib";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import {
		GetProductionProcurementDispatchOrdersDocument,
		type GetProductionProcurementDispatchOrdersQuery,
		type GetProductionProcurementDispatchOrdersQueryVariables
	} from "$lib/services/graphql/generated/graphql";
	import { graphqlQuery } from "$lib/services/graphql";
	import { toFetchParamsInput } from "../logic";

	type DispatchRow =
		GetProductionProcurementDispatchOrdersQuery["productionProcurementDispatchOrders"][number];

	let postedOrders = $state<DispatchRow[]>([]);
	let tableLoading = $state(false);

	const baseColumns: TableColumn[] = [
		{ label: "Date", name: "date" },
		{ label: "No", name: "no", aggregation: "count" },
		{ label: "Qty", name: "tyres", aggregation: "sum" },
		{ label: "Vehicle No", name: "vehicleNo" },
		{ label: "Mobile No", name: "mobileNo" },
		{ label: "Status", name: "status" }
	];

	const columns = $derived.by((): TableColumn[] => {
		const fp = get(fetchParamsStore);
		const multiRc = fp != null && (fp.respCenters?.length ?? 0) > 1;
		if (!multiRc) return baseColumns;
		return [...baseColumns, { label: "Factory", name: "destination" }];
	});

	async function fetchPostedOrders() {
		ensureFetchParams();
		const fp = get(fetchParamsStore);
		if (!fp) {
			toast.error("Session context missing. Please sign in again.");
			return;
		}
		tableLoading = true;
		try {
			const result = await graphqlQuery<
				GetProductionProcurementDispatchOrdersQuery,
				GetProductionProcurementDispatchOrdersQueryVariables
			>(GetProductionProcurementDispatchOrdersDocument, {
				variables: { param: toFetchParamsInput(fp) },
				skipCache: true,
				skipLoading: true
			});

			if (result.success && result.data?.productionProcurementDispatchOrders) {
				postedOrders = result.data.productionProcurementDispatchOrders;
			} else {
				toast.error(result.error ?? "Could not load posted shipments.");
			}
		} finally {
			tableLoading = false;
		}
	}

	const actions = $state<ButtonProps[]>([
		{
			icon: "RefreshCw",
			label: "Refresh",
			variant: "outline",
			class: "border-border/80",
			onclick: () => {
				void fetchPostedOrders();
			}
		}
	]);

	onMount(() => {
		void fetchPostedOrders();
		updateGoBackPath("/ecoproc");
	});
</script>

<div class="min-h-screen bg-background pb-safe">
	<PageHeading
		backHref="/ecoproc"
		icon="package"
		pageTitle="Posted shipments"
		class="border-b border-border/40"
	>
		{#snippet title()}
			<span class="truncate">Posted shipments</span>
		{/snippet}
		{#snippet description()}
			<span class="line-clamp-2 sm:line-clamp-none text-muted-foreground">
				Dispatched casing orders. Tap a row to open lines and actions.
			</span>
		{/snippet}		
	</PageHeading>

	<div class="container mx-auto max-w-6xl px-3 py-4 sm:px-4 sm:py-6">
		<div class="overflow-hidden rounded-xl border border-border/60 bg-card shadow-sm">
			<div
				class="-mx-3 overflow-x-auto px-3 sm:mx-0 sm:px-0 [scrollbar-gutter:stable]"
			>
				<Grid
					data={postedOrders}
					{columns}					
					loading={tableLoading}
					onRowClick={(row) => {
						settingsStore.update((s) => ({
							...s,
							activePage: `Posted shipment No. ${row?.no}`
						}));
						goto(`/ecoproc/postedship/${row?.no}`);
					}}
					dataKey="no"
				/>
			</div>
		</div>
	</div>
</div>
