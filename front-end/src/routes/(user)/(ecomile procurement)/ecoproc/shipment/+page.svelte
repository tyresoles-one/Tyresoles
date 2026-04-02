<script lang="ts">
	import { get } from "svelte/store";
	import { fetchParamsStore, ensureFetchParams } from "$lib/managers/stores";
	import { onMount, goto } from "$lib";
	import {
		Grid,
		type ButtonProps,
		Form,
		type InputProps,
		toast,
		dialogShow,
		dialogHide,
		updateGoBackPath
	} from "$lib/components";
	import * as Tabs from "$lib/components/ui/tabs";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import { Input } from "$lib/components/ui/input";
	import { Button } from "$lib/components/ui/button";
	import { Label } from "$lib/components/ui/label";
	import { Badge } from "$lib/components/ui/badge";
	import * as Sheet from "$lib/components/ui/sheet";
	import {
		Collapsible,
		CollapsibleContent,
		CollapsibleTrigger
	} from "$lib/components/ui/collapsible";
	import Search from "@lucide/svelte/icons/search";
	import SlidersHorizontal from "@lucide/svelte/icons/sliders-horizontal";
	import ChevronDown from "@lucide/svelte/icons/chevron-down";
	import { cn } from "$lib/utils";
	import { graphqlQuery, graphqlMutation } from "$lib/services/graphql";
	import {
		GetProductionProcurementOrderLinesDispatchDocument,
		GetProductionShipmentOrderForMergerDocument,
		NewProductionProcShipNoDocument,
		UpdateProductionProcOrdLineDispatchDocument,
		UpdateProductionProcOrdLineDropDocument,
		type GetProductionProcurementOrderLinesDispatchQuery,
		type GetProductionProcurementOrderLinesDispatchQueryVariables,
		type GetProductionShipmentOrderForMergerQuery,
		type GetProductionShipmentOrderForMergerQueryVariables,
		type NewProductionProcShipNoMutation,
		type NewProductionProcShipNoMutationVariables,
		type UpdateProductionProcOrdLineDispatchMutation,
		type UpdateProductionProcOrdLineDispatchMutationVariables,
		type UpdateProductionProcOrdLineDropMutation,
		type UpdateProductionProcOrdLineDropMutationVariables,
		type OrderLineDispatchInput
	} from "$lib/services/graphql/generated/graphql";
	import type { ShipmentInfo } from "$lib/business/models";
	import { required, mobileNo, vehicleNo } from "$lib/managers/services/validation";
	import { toFetchParamsInput } from "../logic";

	/** Fixed dispatch destination (field hidden on this page). */
	const DESTINATION_CODE = "BEL";

	let data = $state<Record<string, unknown>[]>([]);
	let activeTab = $state<string>("tyres");
	let selection: Map<string, object> = $state(new Map());
	/** Synced with Grid for clearing after bulk actions */
	let selectedRowKeys = $state<Set<string>>(new Set());
	let tableLoading = $state(false);
	let formBusy = $state(false);
	let newShipmentLoading = $state(false);
	let editShipmentLoading = $state(false);

	let searchQuery = $state("");
	let filtersSheetOpen = $state(false);
	/** Collapsed filter panel (search + advanced); quick access via trigger row */
	let filtersPanelOpen = $state(true);

	type ColFilterKey =
		| "sortNo"
		| "no"
		| "make"
		| "serialNo"
		| "date"
		| "supplier"
		| "orderNo";

	const COL_KEYS: ColFilterKey[] = [
		"sortNo",
		"no",
		"make",
		"serialNo",
		"date",
		"supplier",
		"orderNo"
	];

	const COLUMN_FILTER_DEFS: { key: ColFilterKey; label: string }[] = [
		{ key: "sortNo", label: "Sort No" },
		{ key: "no", label: "Tyre size" },
		{ key: "make", label: "Make" },
		{ key: "serialNo", label: "Serial No" },
		{ key: "date", label: "Date" },
		{ key: "supplier", label: "Supplier" },
		{ key: "orderNo", label: "Order No" }
	];
	const columnFilterPlaceholder = "Contains…";

	let colFilters = $state<Record<ColFilterKey, string>>({
		sortNo: "",
		no: "",
		make: "",
		serialNo: "",
		date: "",
		supplier: "",
		orderNo: ""
	});

	const tyreSearchFields = [
		"sortNo",
		"no",
		"make",
		"serialNo",
		"date",
		"supplier",
		"orderNo"
	] as const;

	function setColFilter(key: ColFilterKey, value: string) {
		colFilters = { ...colFilters, [key]: value };
	}

	function clearAllFilters() {
		searchQuery = "";
		colFilters = {
			sortNo: "",
			no: "",
			make: "",
			serialNo: "",
			date: "",
			supplier: "",
			orderNo: ""
		};
	}

	const filteredTyres = $derived.by(() => {
		let rows = data;
		for (const key of COL_KEYS) {
			const f = colFilters[key].trim().toLowerCase();
			if (!f) continue;
			rows = rows.filter((row) =>
				String(row[key] ?? "")
					.toLowerCase()
					.includes(f)
			);
		}
		const q = searchQuery.trim().toLowerCase();
		if (!q) return rows;
		return rows.filter((row) =>
			tyreSearchFields.some((k) => String(row[k] ?? "").toLowerCase().includes(q))
		);
	});

	/** Column-only filters (for badge on “Advanced”) */
	const advancedFilterActiveCount = $derived.by(() =>
		COL_KEYS.reduce((n, k) => n + (colFilters[k].trim() ? 1 : 0), 0)
	);

	const totalFilterActiveCount = $derived.by(
		() => advancedFilterActiveCount + (searchQuery.trim() ? 1 : 0)
	);

	/** Matches Grid `dataKey="orderNo,lineNo"` */
	function tyreRowKey(row: Record<string, unknown>): string {
		return "orderNo,lineNo"
			.split(",")
			.map((k) => row[k.trim()])
			.join(",");
	}

	/** Keep `selection` row objects from full `data` so filters do not drop selected tyres. */
	function syncSelectionFromKeys() {
		const map = new Map<string, object>();
		for (const row of data) {
			const key = tyreRowKey(row);
			if (selectedRowKeys.has(key)) map.set(key, row);
		}
		selection = map;
	}

	const tyresTabLabel = $derived.by(() =>
		selection.size > 0
			? `${selection.size} Tyre${selection.size > 1 ? "s" : ""} selected`
			: "Select tyres"
	);

	let shipment = $state<ShipmentInfo>({
		ShipmentNo: "",
		date: new Date().toISOString().split("T")[0],
		vehicleNo: "",
		mobileNo: "",
		transport: "",
		destination: DESTINATION_CODE
	});

	const fields = $state<InputProps[]>([
		{
			name: "ShipmentNo",
			label: "Shipment No",
			type: "text",
			readonly: true,
			rules: [required("Shipment No")]
		},
		{
			name: "transport",
			label: "Transport",
			rules: [required("Transport")]
		},
		{
			name: "vehicleNo",
			label: "Vehicle No",
			rules: [required("Vehicle No"), vehicleNo()]
		},
		{
			name: "mobileNo",
			label: "Mobile No",
			rules: [required("Mobile No"), mobileNo()]
		}
	]);

	async function onNewShipmentNo() {
		const fp = get(fetchParamsStore);
		if (!fp) {
			toast.error("Session context missing.");
			return;
		}
		newShipmentLoading = true;
		try {
			const res = await graphqlMutation<NewProductionProcShipNoMutation, NewProductionProcShipNoMutationVariables>(
				NewProductionProcShipNoDocument,
				{
					variables: {
						param: {
							...toFetchParamsInput(fp),
							from: String(shipment.date ?? "")
						}
					}
				}
			);
			if (res.success && res.data?.newProductionProcShipNo != null) {
				shipment.ShipmentNo = res.data.newProductionProcShipNo;
				fields[0].type = "text";
				fields[0].label = "Shipment No";
				fields[0].readonly = true;
				toast.success("Shipment number assigned.");
			} else {
				toast.error(res.error ?? "Could not create shipment number.");
			}
		} finally {
			newShipmentLoading = false;
		}
	}

	/** Loads dispatched shipments for merger (Live: Db.Production.ShipmentOrderForMerger). */
	async function loadShipmentOrderForMerger() {
		const fp = get(fetchParamsStore);
		if (!fp) {
			toast.error("Session context missing.");
			return;
		}
		editShipmentLoading = true;
		try {
			shipment.ShipmentNo = "";
			const res = await graphqlQuery<
				GetProductionShipmentOrderForMergerQuery,
				GetProductionShipmentOrderForMergerQueryVariables
			>(GetProductionShipmentOrderForMergerDocument, {
				variables: { param: toFetchParamsInput(fp) },
				skipCache: true
			});
			if (res.success && res.data?.productionShipmentOrderForMerger) {
				fields[0].type = "list";
				fields[0].label = "Select Shipment No";
				fields[0].data = res.data.productionShipmentOrderForMerger as object[];
				fields[0].dataKey = "no";
				fields[0].labelKey = "no";
				fields[0].columns = [
					{ name: "no", label: "No" },
					{ name: "date", label: "Date" }
				];
				fields[0].hideHeader = true;
				fields[0].readonly = false;
				fields[0].onListSelect = (value, lookup) => {
					const record = lookup.get(String(value));
					if (record) {
						const r = record as Record<string, string>;
						shipment.ShipmentNo = r.no ?? "";
						shipment.date = r.date ?? shipment.date;
						shipment.destination = DESTINATION_CODE;
						shipment.transport = r.transport ?? "";
						shipment.vehicleNo = r.vehicleNo ?? "";
						shipment.mobileNo = r.mobileNo ?? "";
					}
				};
			} else {
				toast.error(res.error ?? "Could not load shipments.");
			}
		} finally {
			editShipmentLoading = false;
		}
	}

	function dispatchQueryVariables(): GetProductionProcurementOrderLinesDispatchQueryVariables | null {
		const fp = get(fetchParamsStore);
		if (!fp) return null;
		return {
			param: {
				...toFetchParamsInput(fp),
				view: "Posted",
				type: "",
				nos: []
			}
		};
	}

	async function fetchTyres() {
		const variables = dispatchQueryVariables();
		if (!variables) {
			toast.error("Session context missing. Please sign in again.");
			return;
		}
		tableLoading = true;
		try {
			const result = await graphqlQuery<
				GetProductionProcurementOrderLinesDispatchQuery,
				GetProductionProcurementOrderLinesDispatchQueryVariables
			>(GetProductionProcurementOrderLinesDispatchDocument, {
				variables,
				skipCache: true
			});
			if (result.success && result.data?.productionProcurementOrderLinesDispatch) {
				data = result.data.productionProcurementOrderLinesDispatch as Record<string, unknown>[];
				const valid = new Set(data.map((r) => tyreRowKey(r)));
				selectedRowKeys = new Set([...selectedRowKeys].filter((k) => valid.has(k)));
				syncSelectionFromKeys();
			} else {
				toast.error(result.error ?? "Could not load tyres.");
			}
		} finally {
			tableLoading = false;
		}
	}

	const actions: ButtonProps[] = [
		{
			icon: "RefreshCw",
			label: "Refresh",
			variant: "outline",
			class: "border-border/80",
			onclick: () => {
				void fetchTyres();
			}
		},
		{
			icon: "Trash2",
			label: "Remove casings",
			variant: "destructive",
			class: "border-border/80",
			onclick: () => {
				handleRemove();
			}
		}
	];

	const formActions = $derived.by((): ButtonProps[] => [
		{
			icon: "Plus",
			label: "New shipment",
			loading: newShipmentLoading,
			loadingLabel: "Creating…",
			variant: "outline",
			onclick: () => {
				void onNewShipmentNo();
			}
		},
		{
			icon: "Pencil",
			label: "Edit shipment",
			loading: editShipmentLoading,
			loadingLabel: "Loading…",
			variant: "outline",
			onclick: () => {
				void loadShipmentOrderForMerger();
			}
		},
		{
			type: "submit",
			icon: "Send",
			label: "Post shipment",
			variant: "default",
			class: "border-border/80"
		}
	]);

	onMount(() => {
		ensureFetchParams();
		void fetchTyres();
		updateGoBackPath("/ecoproc");
	});

	function lineToInputDispatch(v: Record<string, unknown>): OrderLineDispatchInput {
		return {
			button: "",
			date: "",
			dispatchDate: String(shipment.date ?? ""),
			dispatchDestination: DESTINATION_CODE,
			dispatchMobileNo: String(shipment.mobileNo ?? ""),
			dispatchOrderNo: String(shipment.ShipmentNo ?? ""),
			dispatchTransporter: String(shipment.transport ?? ""),
			dispatchVehicleNo: String(shipment.vehicleNo ?? ""),
			factInspector: "",
			factInspection: "",
			factInspectorFinal: "",
			inspection: "",
			inspector: "",
			lineNo: Number(v.lineNo ?? 0),
			location: "",
			make: "",
			model: "",
			newSerialNo: "",
			no: "",
			orderNo: String(v.orderNo ?? ""),
			orderStatus: "",
			rejectionReason: "",
			remark: "",
			serialNo: "",
			sortNo: "",
			supplier: ""
		};
	}

	function lineToInputDrop(v: Record<string, unknown>): OrderLineDispatchInput {
		return {
			button: "",
			date: "",
			dispatchDate: "",
			dispatchDestination: "",
			dispatchMobileNo: "",
			dispatchOrderNo: "",
			dispatchTransporter: "",
			dispatchVehicleNo: "",
			factInspector: "",
			factInspection: "",
			factInspectorFinal: "",
			inspection: "",
			inspector: "",
			lineNo: Number(v.lineNo ?? 0),
			location: "",
			make: "",
			model: "",
			newSerialNo: "",
			no: "",
			orderNo: String(v.orderNo ?? ""),
			orderStatus: "",
			rejectionReason: "Casing Removed",
			remark: "",
			serialNo: "",
			sortNo: "",
			supplier: ""
		};
	}

	function handleSubmit(_data: Record<string, unknown>) {
		if (selection.size === 0) {
			toast.error("No tyres selected.");
			return;
		}
		if (!shipment.ShipmentNo) {
			toast.error("Shipment No is required.");
			return;
		}
		dialogShow({
			title: "Confirm shipment",
			description: "Are you sure you want to post this shipment?",
			actionLabel: "Post",
			onAction: () => {
				dialogHide();
				void (async () => {
					formBusy = true;
					try {
						const lines: OrderLineDispatchInput[] = [];
						for (const [, value] of selection) {
							lines.push(lineToInputDispatch(value as Record<string, unknown>));
						}
						const res = await graphqlMutation<
							UpdateProductionProcOrdLineDispatchMutation,
							UpdateProductionProcOrdLineDispatchMutationVariables
						>(UpdateProductionProcOrdLineDispatchDocument, {
							variables: { lines }
						});
						if (res.success) {
							toast.success("Shipment posted successfully.");
							goto("/ecoproc");
						} else {
							toast.error(res.error ?? "Post failed.");
						}
					} finally {
						formBusy = false;
					}
				})();
			}
		});
	}

	function handleRemove() {
		if (selection.size === 0) {
			toast.error("No tyres selected.");
			return;
		}
		dialogShow({
			title: "Confirm removal",
			description:
				"Remove casings from the selected tyres? This cannot be undone.",
			actionLabel: "Remove",
			onAction: () => {
				dialogHide();
				void (async () => {
					tableLoading = true;
					try {
						const lines: OrderLineDispatchInput[] = [];
						for (const [, value] of selection) {
							lines.push(lineToInputDrop(value as Record<string, unknown>));
						}
						const res = await graphqlMutation<
							UpdateProductionProcOrdLineDropMutation,
							UpdateProductionProcOrdLineDropMutationVariables
						>(UpdateProductionProcOrdLineDropDocument, {
							variables: { lines }
						});
						if (res.success) {
							toast.success("Casings removed successfully.");
							selection = new Map();
							selectedRowKeys = new Set();
							await fetchTyres();
						} else {
							toast.error(res.error ?? "Removal failed.");
						}
					} finally {
						tableLoading = false;
					}
				})();
			}
		});
	}
</script>

<div class="min-h-screen bg-background pb-safe">
	{#snippet columnFiltersForm(idPrefix: string)}
		<div
			class="grid grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-x-4 sm:gap-y-4 lg:grid-cols-2 xl:grid-cols-3"
		>
			{#each COLUMN_FILTER_DEFS as col (col.key)}
				<div class="min-w-0 space-y-1.5">
					<Label
						for={`${idPrefix}-flt-${col.key}`}
						class="text-xs font-medium leading-tight text-muted-foreground"
					>
						{col.label}
					</Label>
					<Input
						id={`${idPrefix}-flt-${col.key}`}
						type="text"
						placeholder={columnFilterPlaceholder}
						class="h-11 min-h-11 w-full touch-manipulation text-base sm:text-sm"
						value={colFilters[col.key]}
						oninput={(e) => setColFilter(col.key, e.currentTarget.value)}
						autocomplete="off"
					/>
				</div>
			{/each}
		</div>
	{/snippet}

	<PageHeading
		backHref="/ecoproc"
		icon="truck"
		pageTitle="Casing shipment"
		class="border-b border-border/40"
	>
		{#snippet title()}
			<span class="truncate">Casing shipment</span>
		{/snippet}
		{#snippet description()}
			<span class="line-clamp-2 sm:line-clamp-none text-muted-foreground">
				Choose posted tyres, then open Transport to enter vehicle details and post.
			</span>
		{/snippet}
	</PageHeading>

	<div class="container mx-auto max-w-6xl px-3 py-4 sm:px-4 sm:py-6">
		<Tabs.Root bind:value={activeTab} class="flex w-full flex-col gap-0">
			<div
				class="overflow-hidden rounded-xl border border-border/60 bg-card shadow-sm"
			>
				<Tabs.List
					class={cn(
						"grid h-auto min-h-13 w-full grid-cols-2 gap-1 rounded-none border-b border-border/60 bg-muted/40 p-1.5 sm:min-h-14 sm:p-2"
					)}
				>
					<Tabs.Trigger
						value="tyres"
						class={cn(
							"min-h-11 touch-manipulation rounded-lg px-2 py-2.5 text-sm font-medium transition-colors",
							"data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm",
							"data-[state=inactive]:text-muted-foreground data-[state=inactive]:hover:text-foreground"
						)}
					>
						<span class="line-clamp-2 text-center leading-snug">{tyresTabLabel}</span>
					</Tabs.Trigger>
					<Tabs.Trigger
						value="vehicle"
						class={cn(
							"min-h-11 touch-manipulation rounded-lg px-2 py-2.5 text-sm font-medium transition-colors",
							"data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm",
							"data-[state=inactive]:text-muted-foreground data-[state=inactive]:hover:text-foreground"
						)}
					>
						Transport details
					</Tabs.Trigger>
				</Tabs.List>

				<div class="p-3 sm:p-4 md:p-6">
					<Tabs.Content value="tyres" class="mt-0 outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-md">						
						<Collapsible bind:open={filtersPanelOpen} class="mb-4">
							<CollapsibleTrigger
								class={cn(
									"flex w-full min-h-11 touch-manipulation items-center justify-between gap-3 rounded-xl border border-border/60 bg-muted/25 px-3 py-2.5 text-left transition-colors",
									"hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background"
								)}
								aria-label={filtersPanelOpen ? "Collapse search and filters" : "Expand search and filters"}
							>
								<span class="flex min-w-0 items-center gap-2">
									<ChevronDown
										class={cn(
											"size-4 shrink-0 text-muted-foreground transition-transform duration-200",
											filtersPanelOpen && "rotate-180"
										)}
										aria-hidden="true"
									/>
									<span class="text-sm font-semibold tracking-tight">Search & filters</span>
									{#if totalFilterActiveCount > 0}
										<Badge variant="secondary" class="tabular-nums text-xs font-semibold">
											{totalFilterActiveCount}
										</Badge>
									{/if}
								</span>
								<span class="shrink-0 text-xs tabular-nums text-muted-foreground sm:text-sm" aria-live="polite">
									<span class="font-medium text-foreground">{filteredTyres.length}</span>
									<span> / </span>
									<span>{data.length}</span>
									<span class="hidden sm:inline"> tyres</span>
								</span>
							</CollapsibleTrigger>

							<CollapsibleContent class="overflow-hidden">
								<div class="mt-3 flex flex-col gap-3 border-t border-border/40 pt-3">
									<div class="relative min-w-0 w-full">
										<Search
											class="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
											aria-hidden="true"
										/>
										<Input
											type="search"
											enterkeyhint="search"
											placeholder="Quick search (any column)…"
											class="h-11 min-h-11 w-full touch-manipulation pl-10 text-base sm:h-10 sm:min-h-10 sm:text-sm"
											bind:value={searchQuery}
											autocomplete="off"
											aria-label="Quick search across all columns"
										/>
									</div>

									<div class="flex flex-wrap items-center justify-end gap-2 sm:gap-3">
										{#if totalFilterActiveCount > 0}
											<Button
												type="button"
												variant="ghost"
												size="sm"
												class="h-9 touch-manipulation px-2 text-xs sm:text-sm"
												onclick={() => clearAllFilters()}
											>
												Clear all ({totalFilterActiveCount})
											</Button>
										{/if}
										<Button
											type="button"
											variant="outline"
											size="default"
											class="h-11 min-h-11 touch-manipulation gap-2 lg:hidden"
											onclick={() => (filtersSheetOpen = true)}
										>
											<SlidersHorizontal class="size-4 shrink-0" aria-hidden="true" />
											<span>Advanced filters</span>
											{#if advancedFilterActiveCount > 0}
												<Badge variant="secondary" class="tabular-nums px-1.5 py-0 text-xs font-semibold">
													{advancedFilterActiveCount}
												</Badge>
											{/if}
										</Button>
									</div>

									<div
										class="hidden rounded-xl border border-border/60 bg-muted/20 p-4 lg:block"
									>
										<div class="mb-3 flex items-center justify-between gap-2">
											<h3 class="text-sm font-semibold tracking-tight">Advanced filters</h3>
											{#if advancedFilterActiveCount > 0}
												<Badge variant="outline" class="tabular-nums text-xs">
													{advancedFilterActiveCount} active
												</Badge>
											{/if}
										</div>
										{@render columnFiltersForm("desk")}
									</div>
								</div>
							</CollapsibleContent>
						</Collapsible>

						<Sheet.Root bind:open={filtersSheetOpen}>
							<Sheet.Content
								side="bottom"
								class="flex max-h-[min(90dvh,640px)] flex-col gap-0 rounded-t-2xl border-t p-0 pb-[max(1rem,env(safe-area-inset-bottom))] pt-0"
							>
								<div class="sticky top-0 z-10 flex shrink-0 flex-col gap-1 border-b border-border/60 bg-background/95 px-4 pb-3 pt-4 backdrop-blur supports-backdrop-filter:bg-background/80">
									<div class="mx-auto mb-2 h-1 w-10 shrink-0 rounded-full bg-muted-foreground/25" aria-hidden="true"></div>
									<Sheet.Header class="space-y-1 p-0 text-left">
										<Sheet.Title class="text-lg font-semibold leading-tight">
											Advanced filters
										</Sheet.Title>
										<Sheet.Description class="text-xs text-muted-foreground">
											Each field narrows rows when it matches (contains). All active filters apply together.
										</Sheet.Description>
									</Sheet.Header>
								</div>
								<div class="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 py-4">
									{@render columnFiltersForm("sheet")}
								</div>
								<div
									class="flex shrink-0 flex-col gap-2 border-t border-border/60 bg-muted/15 px-4 py-3 sm:flex-row sm:justify-end"
								>
									<Button
										type="button"
										variant="outline"
										class="h-11 min-h-11 w-full touch-manipulation sm:w-auto"
										onclick={() => clearAllFilters()}
									>
										Clear all
									</Button>
									<Button
										type="button"
										class="h-11 min-h-11 w-full touch-manipulation sm:min-w-36"
										onclick={() => (filtersSheetOpen = false)}
									>
										Done
									</Button>
								</div>
							</Sheet.Content>
						</Sheet.Root>

						<div
							class="-mx-3 overflow-x-auto px-3 sm:mx-0 sm:px-0 [scrollbar-gutter:stable]"
						>
							<Grid
								data={filteredTyres}
								{actions}
								loading={tableLoading}
								enableSelection
								selectionType="multiple"
								dataKey="orderNo,lineNo"
								bind:selectedValues={selectedRowKeys}
								columns={[
									{ name: "sortNo", label: "Sort No" },
									{ name: "no", label: "Tyre Size", aggregation: "count" },
									{ name: "make", label: "Make" },
									{ name: "serialNo", label: "Serial No" },
									{ name: "date", label: "Date" },
									{ name: "supplier", label: "Supplier" },
									{ name: "orderNo", label: "Order No" }
								]}
								onSelectionChange={() => {
									syncSelectionFromKeys();
								}}
							/>
						</div>
					</Tabs.Content>

					<Tabs.Content
						value="vehicle"
						class="mt-0 outline-none focus-visible:ring-2 focus-visible:ring-ring rounded-md"
					>
						{#if shipment}							
							<Form
								loading={formBusy || newShipmentLoading || editShipmentLoading}
								{fields}
								actions={formActions}
								bind:data={shipment}
								onSubmit={handleSubmit}
								layoutClass="grid w-full grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-x-5 sm:gap-y-4"
							/>
						{/if}
					</Tabs.Content>
				</div>
			</div>
		</Tabs.Root>
	</div>
</div>
