<script lang="ts">
	import { get } from "svelte/store";
	import { page } from "$app/stores";
	import { afterNavigate } from "$app/navigation";
	import { browser } from "$app/environment";
	import { z } from "zod";
	import { fetchParamsStore, ensureFetchParams } from "$lib/managers/stores";
	import { authStore } from "$lib/stores/auth";
	import { goto, onMount } from "$lib";
	import {
		Grid,
		type TableColumn,
		type ButtonProps,
		DialogPage,
		Input,
		dialogShow,
		dialogHide,
		toast,
		updateGoBackPath
	} from "$lib/components";
	import { CreateForm, FormGenerator, type FormSchema } from "$lib/components/venUI/form";
	import type { FormNode } from "$lib/components/venUI/form/types";
	import MasterSelect from "$lib/components/venUI/master-select/MasterSelect.svelte";
	import { Label } from "$lib/components/ui/label";
	import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
	import {
		GetProductionProcurementOrderLinesDispatchDocument,
		UpdateProductionProcOrdLineReceiptDocument,
		UpdateProductionProcOrdLineRemoveDocument,
		GenerateProductionGrAsDocument,
		UpdateProductionProcOrdLineDispatchSingleDocument,
		type GetProductionProcurementOrderLinesDispatchQuery,
		type GetProductionProcurementOrderLinesDispatchQueryVariables,
		type UpdateProductionProcOrdLineReceiptMutation,
		type UpdateProductionProcOrdLineReceiptMutationVariables,
		type UpdateProductionProcOrdLineRemoveMutation,
		type UpdateProductionProcOrdLineRemoveMutationVariables,
		type GenerateProductionGrAsMutation,
		type GenerateProductionGrAsMutationVariables,
		type UpdateProductionProcOrdLineDispatchSingleMutation,
		type UpdateProductionProcOrdLineDispatchSingleMutationVariables,
		type OrderLineDispatchInput,
		type FetchParamsInput
	} from "$lib/services/graphql/generated/graphql";
	import { graphqlQuery, graphqlMutation } from "$lib/services/graphql";
	import { toFetchParamsInput } from "../../logic";

	const orderId = $derived(decodeURIComponent($page.params.orderID ?? ""));

	/** Hot Chocolate returns `0` on success for these mutations — do not use truthy checks on the payload. */
	function mutationReturnedIntOk(
		res: { success: boolean; data?: Record<string, unknown> | null },
		field: string
	): boolean {
		return (
			res.success &&
			res.data != null &&
			typeof res.data[field] === "number"
		);
	}

	type LineRow = NonNullable<
		GetProductionProcurementOrderLinesDispatchQuery["productionProcurementOrderLinesDispatch"]
	>[number];

	type TyreDispatchFormValues = {
		no: string;
		make: string;
		model: string;
		serialNo: string;
		orderStatus: string;
	};

	const tyreDispatchSchema = z.object({
		no: z.string().min(1, "Tyre Size is required"),
		make: z.string().min(1, "Make is required"),
		model: z.string().default(""),
		serialNo: z.string().min(1, "Serial No is required"),
		orderStatus: z.string().min(1, "Status is required")
	});

	const tyreForm = CreateForm<TyreDispatchFormValues>({
		schema: tyreDispatchSchema,
		initialValues: {
			no: "",
			make: "",
			model: "",
			serialNo: "",
			orderStatus: ""
		}
	});

	/** Session RC list — same pattern as ecoproc line page when no order header RC. */
	const purchaseItemParam = $derived.by((): FetchParamsInput | null => {
		ensureFetchParams();
		const fp = get(fetchParamsStore);
		if (!fp) return null;
		return toFetchParamsInput({
			...fp,
			regions: ["CASING"],
			type: "FromGroupDetail",
			respCenters: fp.respCenters ?? []
		});
	});

	const productionMakesParam = $derived.by((): FetchParamsInput | null => {
		ensureFetchParams();
		const fp = get(fetchParamsStore);
		if (!fp) return null;
		return toFetchParamsInput({
			...fp,
			regions: ["TYREMAKE"],
			type: "casing",
			respCenters: fp.respCenters ?? []
		});
	});

	const productionMakeSubMakeParam = $derived.by((): FetchParamsInput | null => {
		const base = productionMakesParam;
		const itemNo = String(tyreForm.values.no ?? "").trim();
		const make = String(tyreForm.values.make ?? "").trim();
		if (!base || !itemNo || !make) return null;
		return { ...base, type: `${itemNo},${make}` };
	});

	const showSubMakeField = $derived(productionMakeSubMakeParam != null);

	let data = $state<LineRow[]>([]);
	let selectedValues = $state<Set<string>>(new Set());
	let selections = $state<Map<string, object>>(new Map());
	let openForm = $state(false);
	let purchaseOrders = $state<string[]>([]);
	let orderLine = $state<LineRow | null>(null);
	let tableLoading = $state(false);

	/** Toolbar async task — shows spinner on matching action; others disabled via `taskLock`. */
	let taskLoading = $state<string | null>(null);

	async function withTask<T>(key: string, fn: () => Promise<T>): Promise<T | undefined> {
		if (taskLoading != null) return undefined;
		taskLoading = key;
		try {
			return await fn();
		} finally {
			taskLoading = null;
		}
	}

	function taskLock(key: string): Pick<ButtonProps, "loading" | "disabled"> {
		return {
			loading: taskLoading === key,
			disabled: taskLoading !== null && taskLoading !== key
		};
	}

	let prevMakeSubMakeKey = $state<string | null>(null);

	/** Load row into the dialog form — must run before the sub-make key effect so `model` is not cleared on open/switch. */
	$effect(() => {
		if (!openForm || !orderLine) return;
		prevMakeSubMakeKey = null;
		tyreForm.values.no = String(orderLine.no ?? "");
		tyreForm.values.make = String(orderLine.make ?? "");
		tyreForm.values.model = String(orderLine.model ?? "");
		tyreForm.values.serialNo = String(orderLine.serialNo ?? "");
		tyreForm.values.orderStatus = String(orderLine.orderStatus ?? "");
		tyreForm.errors = {};
	});

	/** Sub-make list key is `itemNo,make` — clear model when item or make changes (ecoproc line page parity). */
	$effect(() => {
		const itemNo = String(tyreForm.values.no ?? "").trim();
		const make = String(tyreForm.values.make ?? "").trim();
		const key = itemNo && make ? `${itemNo},${make}` : "";
		if (prevMakeSubMakeKey !== null && key !== prevMakeSubMakeKey) {
			tyreForm.values.model = "";
		}
		prevMakeSubMakeKey = key || null;
	});

	const tyreFormSchema = $derived.by<FormSchema>(() => {
		const fields: FormNode[] = [
			{
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
					purchaseItemParam
				}
			},
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
					disabled: !productionMakesParam,
					productionFetchParam: productionMakesParam
				}
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
								disabled: !productionMakeSubMakeParam,
								productionFetchParam: productionMakeSubMakeParam
							}
						}
					] satisfies FormNode[])
				: []),
			{
				type: "field",
				name: "serialNo",
				label: "Serial No",
				placeholder: "e.g. ABC123456",
				required: true,
				colSpan: 2,
				class: "w-full"
			},
			{
				type: "field",
				name: "orderStatus",
				label: "Status",
				inputType: "select",
				required: true,
				colSpan: 2,
				class: "w-full",
				options: [
					{ label: "Dispatched", value: "Dispatched" },
					{ label: "Rejected", value: "Rejected" },
					{ label: "Dropped", value: "Dropped" },
					{ label: "Returned", value: "Returned" }
				]
			}
		];
		return fields;
	});

	function buildDispatchLineFromForm(): OrderLineDispatchInput {
		if (!orderLine) {
			throw new Error("No line selected");
		}
		return {
			...orderLine,
			no: tyreForm.values.no,
			make: tyreForm.values.make,
			model: tyreForm.values.model ?? "",
			serialNo: tyreForm.values.serialNo,
			orderStatus: tyreForm.values.orderStatus
		} as OrderLineDispatchInput;
	}

	/** Administration-only actions (Receive, Remove, Generate GRAs, tyre edit) — from login user, not fetch params. */
	const isAdminUser = $derived($authStore.user?.department === "Administration");

	let enableSelection = $derived.by<boolean>(() => {
		return (
			isAdminUser &&
			data.length > 0 &&
			data.find((d) => d.orderStatus === "Dispatched") !== undefined
		);
	});

	function buildToolbarActions(): ButtonProps[] {
		const id = orderId;
		const out: ButtonProps[] = [
			{
				icon: "RefreshCw",
				label: "Refresh",
				variant: "outline",
				class: "border-border/80",
				...taskLock("refresh"),
				onclick: async () => {
					await withTask("refresh", async () => {
						await fetchOrderLines();
					});
				}
			},
			{
				icon: "Printer",
				label: "Print",
				variant: "outline",
				class: "border-border/80",
				disabled: taskLoading !== null,
				onclick: () => {
					goto(`/ecoproc/postedship/${orderId}/print`);
				}
			}
		];

		const authUser = authStore.get().user;
		if (authUser?.department !== "Administration") {
			return out;
		}

		const dispatched = data.find((d) => d.orderStatus === "Dispatched");
		const recieved = data.find((d) => d.orderStatus === "Received At Factory");

		if (recieved) {
			out.push({
				icon: "FileInput",
				label: "Generate GRAs",
				variant: "outline",
				class: "border-border/80",
				...taskLock("generateGRA"),
				onclick: () => {
					dialogShow({
						title: "Confirm Generation of GRAs",
						description: "Are you sure you want to generate GRAs for this shipment?",
						actionLabel: "Generate",
						onAction: async () => {
							dialogHide();
							await withTask("generateGRA", async () => {
								ensureFetchParams();
								const fp = get(fetchParamsStore);
								if (!fp) {
									toast.error("Session context missing. Please sign in again.");
									return;
								}
								purchaseOrders = [];
								const res = await graphqlMutation<
									GenerateProductionGrAsMutation,
									GenerateProductionGrAsMutationVariables
								>(GenerateProductionGrAsDocument, {
									variables: {
										param: {
											...toFetchParamsInput(fp),
											reportName: id
										}
									},
									skipLoading: true
								});
								if (
									res.success &&
									res.data != null &&
									typeof res.data.generateProductionGRAs === "string"
								) {
									purchaseOrders = String(res.data.generateProductionGRAs)
										.split(",")
										.map((s) => s.trim())
										.filter(Boolean);
									toast.success(`${purchaseOrders.length} GRAs Generated`);
								} else {
									toast.error(res.error || "Failed to generate GRAs");
								}
							});
						}
					});
				}
			});
		} else if (dispatched) {
			out.push(
				{
					icon: "Download",
					label: "Recieve",
					variant: "outline",
					class: "border-border/80",
					...taskLock("receive"),
					onclick: () => {
						if (!selections.size) {
							toast.error("There should be at least one tyre selected.");
							return;
						}
						dialogShow({
							title: "Confirm Recieve",
							description: "Are you sure you want to recieve these tyres?",
							actionLabel: "Recieve",
							onAction: async () => {
								dialogHide();
								await withTask("receive", async () => {
									const ic = authStore.get().user?.entityCode ?? "";
									const lines = Array.from(selections.values()).map(
										(obj: object) =>
											({
												...obj,
												inspector: ic
											}) as OrderLineDispatchInput
									);

									const res = await graphqlMutation<
										UpdateProductionProcOrdLineReceiptMutation,
										UpdateProductionProcOrdLineReceiptMutationVariables
									>(UpdateProductionProcOrdLineReceiptDocument, {
										variables: { lines },
										skipLoading: true
									});

									if (mutationReturnedIntOk(res, "updateProductionProcOrdLineReceipt")) {
										selectedValues = new Set();
										selections = new Map();
										await fetchOrderLines();
										toast.success("Tyres Recieved");
									} else {
										toast.error(res.error || "Failed to receive tyres");
									}
								});
							}
						});
					}
				},
				{
					icon: "PackageMinus",
					label: "Remove",
					variant: "outline",
					class: "border-border/80",
					...taskLock("remove"),
					onclick: () => {
						if (!selections.size) {
							toast.error("There should be at least one tyre selected.");
							return;
						}
						dialogShow({
							title: "Confirm Remove",
							description: "Are you sure you want to remove these tyres?",
							actionLabel: "Remove",
							onAction: async () => {
								dialogHide();
								await withTask("remove", async () => {
									const ic = authStore.get().user?.entityCode ?? "";
									const lines = Array.from(selections.values()).map(
										(obj: object) =>
											({
												...obj,
												inspector: ic
											}) as OrderLineDispatchInput
									);

									const res = await graphqlMutation<
										UpdateProductionProcOrdLineRemoveMutation,
										UpdateProductionProcOrdLineRemoveMutationVariables
									>(UpdateProductionProcOrdLineRemoveDocument, {
										variables: { lines },
										skipLoading: true
									});

									if (mutationReturnedIntOk(res, "updateProductionProcOrdLineRemove")) {
										selectedValues = new Set();
										selections = new Map();
										await fetchOrderLines();
										toast.success("Tyres Removed");
									} else {
										toast.error(res.error || "Failed to remove tyres");
									}
								});
							}
						});
					}
				}
			);
		}

		return out;
	}

	const actions = $derived.by((): ButtonProps[] => buildToolbarActions());

	async function fetchOrderLines() {
		ensureFetchParams();
		const fp = get(fetchParamsStore);
		if (!fp) {
			toast.error("Session context missing. Please sign in again.");
			return;
		}
		const id = orderId;
		if (!id) return;

		tableLoading = true;
		try {
			const p = toFetchParamsInput(fp);
			const result = await graphqlQuery<
				GetProductionProcurementOrderLinesDispatchQuery,
				GetProductionProcurementOrderLinesDispatchQueryVariables
			>(GetProductionProcurementOrderLinesDispatchDocument, {
				variables: {
					param: {
						...p,
						nos: [id],
						type: "Dispatch"
					}
				},
				skipLoading: true,
				skipCache: true
			});

			if (result.success && result.data?.productionProcurementOrderLinesDispatch) {
				data = result.data.productionProcurementOrderLinesDispatch as LineRow[];
			} else {
				toast.error(result.error ?? "Could not load shipment lines.");
			}
		} finally {
			tableLoading = false;
		}
	};

	onMount(() => {
		void fetchOrderLines();
		updateGoBackPath("/ecoproc/postedship");
	});

	afterNavigate(({ from, to }) => {
		if (!browser) return;
		const next = to?.params?.orderID;
		if (!next) return;
		const prev = from?.params?.orderID;
		if (
			from &&
			prev != null &&
			decodeURIComponent(prev) !== decodeURIComponent(next)
		) {
			purchaseOrders = [];
			void fetchOrderLines();
		}
	});

	const columns = $state<TableColumn[]>([
		{ label: "Size", name: "no", aggregation: "count" },
		{ label: "Make", name: "make" },
		{ label: "Serial No", name: "serialNo" },
		{ label: "Supplier", name: "supplier" },
		{ label: "Market", name: "location" },
		{ label: "Inspector", name: "inspector" },
		{ label: "Inspection", name: "inspection" },
		{ label: "Status", name: "orderStatus" }
	]);

	const inputReadonlyClass =
		"h-9 min-h-9 w-full touch-manipulation text-sm sm:h-9 bg-muted/30 border-border/60";
</script>

<div class="min-h-screen bg-background pb-safe">
	<PageHeading
		backHref="/ecoproc/postedship"
		icon="package"
		pageTitle={`Posted shipment ${orderId}`}
		class="border-b border-border/40"
	>
		{#snippet title()}
			<span class="truncate font-semibold tracking-tight">Shipment {orderId}</span>
		{/snippet}
		{#snippet description()}
			<span class="line-clamp-2 sm:line-clamp-none text-muted-foreground">
				Dispatched lines for this shipment. When your account department is Administration, you can receive,
				remove, generate GRAs, and edit tyre lines.
			</span>
		{/snippet}
	</PageHeading>

	<div class="container mx-auto max-w-6xl px-3 py-4 sm:px-4 sm:py-6">
		<section
			aria-label="Shipment transport details"
			class="mb-4 rounded-xl border border-border/60 bg-card p-3 shadow-sm sm:p-4"
		>
			<div
				class="grid grid-cols-1 gap-3 sm:grid-cols-2 sm:gap-4 lg:grid-cols-5"
			>
				<div class="min-w-0 space-y-1.5">
					<Label class="text-xs font-medium leading-tight text-muted-foreground">Date</Label>
					<Input value={data[0]?.date ?? ""} readonly class={inputReadonlyClass} />
				</div>
				<div class="min-w-0 space-y-1.5">
					<Label class="text-xs font-medium leading-tight text-muted-foreground">Destination</Label>
					<Input value={data[0]?.dispatchDestination ?? ""} readonly class={inputReadonlyClass} />
				</div>
				<div class="min-w-0 space-y-1.5">
					<Label class="text-xs font-medium leading-tight text-muted-foreground">Transport</Label>
					<Input value={data[0]?.dispatchTransporter ?? ""} readonly class={inputReadonlyClass} />
				</div>
				<div class="min-w-0 space-y-1.5">
					<Label class="text-xs font-medium leading-tight text-muted-foreground">Vehicle No</Label>
					<Input value={data[0]?.dispatchVehicleNo ?? ""} readonly class={inputReadonlyClass} />
				</div>
				<div class="min-w-0 space-y-1.5">
					<Label class="text-xs font-medium leading-tight text-muted-foreground">Mobile No</Label>
					<Input value={data[0]?.dispatchMobileNo ?? ""} readonly class={inputReadonlyClass} />
				</div>
			</div>
		</section>

		{#if purchaseOrders.length > 0}
			<div
				class="mb-4 rounded-xl border border-border/60 bg-card p-4 shadow-sm sm:p-5"
				aria-live="polite"
			>
				<p class="mb-3 text-sm font-semibold tracking-tight">
					Generated GRAs
					<span class="text-muted-foreground font-normal">({purchaseOrders.length})</span>
				</p>
				<div
					class="grid max-h-[min(50vh,360px)] grid-cols-1 gap-2 overflow-y-auto pr-1 sm:grid-cols-2 lg:grid-cols-3"
				>
					{#each purchaseOrders as po}
						<div
							class="rounded-md border border-border/50 bg-muted/20 px-3 py-2 font-mono text-sm text-foreground"
						>
							{po}
						</div>
					{/each}
				</div>
			</div>
		{/if}

		<div class="overflow-hidden rounded-xl border border-border/60 bg-card shadow-sm">
			<div
				class="-mx-3 overflow-x-auto px-3 sm:mx-0 sm:px-0 [scrollbar-gutter:stable]"
			>
				<Grid
					{data}
					{columns}
					loading={tableLoading}
					{actions}
					{enableSelection}
					bind:selectedValues
					selectionType="multiple"
					dataKey="orderNo,lineNo"
					selectOnRowClick={false}
					onSelectionChange={(selection) => {
						selections = selection;
					}}
					onRowClick={(row) => {
						orderLine = row as LineRow;
						if (
							orderLine.orderStatus === "Purchased" ||
							data.find((d) => d.orderStatus === "Purchased")
						)
							return;
						openForm = true;
					}}
				/>
			</div>
		</div>
	</div>
</div>

{#if orderLine && isAdminUser}
	<DialogPage
		open={openForm}
		onOpenChange={(o) => (openForm = o)}
		onAction={() => {
			for (const key of Object.keys(tyreForm.values) as (keyof TyreDispatchFormValues)[]) {
				tyreForm.touched[key as string] = true;
			}
			if (!tyreForm.validate()) return;
			openForm = false;
			dialogShow({
				title: "Confirm Save",
				description: "Are you sure you want to save the changes to this tyre?",
				actionLabel: "Save",
				onAction: async () => {
					dialogHide();
					await withTask("saveTyre", async () => {
						const res = await graphqlMutation<
							UpdateProductionProcOrdLineDispatchSingleMutation,
							UpdateProductionProcOrdLineDispatchSingleMutationVariables
						>(UpdateProductionProcOrdLineDispatchSingleDocument, {
							variables: {
								line: buildDispatchLineFromForm()
							},
							skipLoading: true
						});

						if (mutationReturnedIntOk(res, "updateProductionProcOrdLineDispatchSingle")) {
							void fetchOrderLines();
							toast.success("Tyre Saved");
						} else {
							toast.error(res.error || "Failed to save tyre");
						}
					});
				},
				onCancel: () => {
					void fetchOrderLines();
				}
			});
		}}
		actionLabel="Save"
		title="Tyre Details"
	>
		<div class="flex min-w-0 flex-col gap-3 sm:grid sm:grid-cols-2 sm:gap-4">
			<FormGenerator form={tyreForm} schema={tyreFormSchema} root={true} autoFocus={openForm} />
		</div>
	</DialogPage>
{/if}
