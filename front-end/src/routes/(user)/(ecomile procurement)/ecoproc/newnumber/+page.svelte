<script lang="ts">
	import { onMount } from "svelte";
	import { updateGoBackPath } from "$lib/components";
	import {
		DialogPage,
		Form,
		dialogHide,
		dialogShow,
		toast
	} from "$lib/components";
	import { settingsStore, fetchParamsStore, ensureFetchParams } from "$lib/managers/stores";
	import { required } from "$lib/managers/services/validation";
	import { incrementAlphanumeric } from "$lib/managers/services/text";
	import MasterList from "$lib/components/venUI/masterList/MasterList.svelte";
	import { Icon } from "$lib/components/venUI/icon";
	import { Button } from "$lib/components/ui/button";
	import Badge from "$lib/components/ui/badge/badge.svelte";
	import { DatePicker } from "$lib/components/venUI/date-picker";
	import { authStore } from "$lib/stores/auth";
	import {
		today,
		getLocalTimeZone,
		toCalendarDate,
		parseDateTime,
		parseDate,
		fromDate as fromJsDate
	} from "@internationalized/date";
	import { TableCell, TableRow, TableHead } from "$lib/components/ui/table";
	import { graphqlMutation, graphqlQuery } from "$lib/services/graphql";
	import {
		UpdateEcomileNewNumberLineDocument,
		procurementNewNumberingToOrderLineDispatchInput
	} from "$lib/services/graphql/operations/ecomile/updateEcomileNewNumberLine";
	import {
		GetProcurementNewNumberingPagedDocument,
		GetEcomileLastNewNumberDocument,
		type GetEcomileLastNewNumberQuery,
		type GetProcurementNewNumberingPagedQuery,
		type GetProcurementNewNumberingPagedQueryVariables,
		type ProcurementNewNumberingDtoFilterInput,
		type ProcurementNewNumberingDtoSortInput,
		SortEnumType,
		type ProcurementNewNumberingDto
	} from "$lib/services/graphql/generated/graphql";

	type LineNode = NonNullable<
		NonNullable<
			GetProcurementNewNumberingPagedQuery["procurementNewNumberingPaged"]
		>["nodes"]
	>[number];

	const PAGE_SIZE = 24;

	let view = $state<"Blank" | "All">("Blank");
	let ecomileLastNumber = $state<string>("");
	let openForm = $state<boolean>(false);
	let orderLine = $state<ProcurementNewNumberingDto | null>(null);

	let dispatchRange = $state<{ start: unknown; end: unknown }>({
		start: undefined,
		end: undefined
	});
	let filtersOpen = $state(true);

	const workDateRef = $derived.by(() => {
		const workDate = $authStore.user?.workDate;
		if (!workDate) return today(getLocalTimeZone());
		try {
			if (typeof workDate === "string") {
				if (workDate.includes("T")) {
					try {
						return toCalendarDate(parseDateTime(workDate.substring(0, 19)));
					} catch {
						return toCalendarDate(fromJsDate(new Date(workDate), getLocalTimeZone()));
					}
				}
				try {
					return parseDate(workDate);
				} catch {
					return toCalendarDate(fromJsDate(new Date(workDate), getLocalTimeZone()));
				}
			}
			return today(getLocalTimeZone());
		} catch {
			return today(getLocalTimeZone());
		}
	});

	function calendarToYmd(d: unknown): string {
		if (d == null) return "";
		if (typeof d === "object" && d !== null && "toString" in d)
			return String((d as { toString: () => string }).toString()).slice(0, 10);
		return "";
	}

	const fromDateYmd = $derived(calendarToYmd(dispatchRange.start));
	const toDateYmd = $derived(calendarToYmd(dispatchRange.end));

	let items = $state<LineNode[]>([]);
	let totalCount = $state(0);
	let cursor = $state<string | null>(null);
	let hasMore = $state(false);
	let loading = $state(false);
	let loadingMore = $state(false);
	let error = $state<string | undefined>(undefined);

	let searchQuery = $state("");
	let debouncedSearch = $state("");
	let currentSort = $state("sortNo_ASC");

	let viewMode = $state<"grid" | "table">("table");
	let listReady = $state(false);

	const sortOptions = [
		{ label: "Sort order (A–Z)", value: "sortNo_ASC" },
		{ label: "Sort order (Z–A)", value: "sortNo_DESC" },
		{ label: "Date (oldest first)", value: "date_ASC" },
		{ label: "Date (newest first)", value: "date_DESC" },
		{ label: "Tyre size (A–Z)", value: "no_ASC" },
		{ label: "Tyre size (Z–A)", value: "no_DESC" },
		{ label: "Make (A–Z)", value: "make_ASC" },
		{ label: "Make (Z–A)", value: "make_DESC" },
		{ label: "Serial No (A–Z)", value: "serialNo_ASC" },
		{ label: "Order No (A–Z)", value: "orderNo_ASC" },
		{ label: "Status (A–Z)", value: "orderStatus_ASC" },
		{ label: "Supplier (A–Z)", value: "supplier_ASC" }
	];

	function buildWhere(q: string): ProcurementNewNumberingDtoFilterInput | undefined {
		const t = q.trim();
		if (!t) return undefined;
		// Do not filter on sortNo, orderStatus, or inspection here: those DTO fields come from
		// SelectRaw/CASE in ProcurementOrderLinesNewNumbering; GraphQL `contains` becomes SQL WHERE
		// on [SortNo]/[OrderStatus]/[Inspection], which are not physical columns on the joined row
		// (WHERE cannot use SELECT aliases), so the query fails at runtime.
		const or: ProcurementNewNumberingDtoFilterInput[] = [
			{ no: { contains: t } },
			{ make: { contains: t } },
			{ serialNo: { contains: t } },
			{ newSerialNo: { contains: t } },
			{ supplier: { contains: t } },
			{ location: { contains: t } },
			{ orderNo: { contains: t } },
			{ dispatchOrderNo: { contains: t } },
			{ model: { contains: t } }
		];
		if (/^\d+$/.test(t)) {
			const n = parseInt(t, 10);
			if (Number.isSafeInteger(n)) or.push({ lineNo: { eq: n } });
		}
		return { or };
	}

	function buildOrder(key: string): ProcurementNewNumberingDtoSortInput[] {
		const map: Record<string, ProcurementNewNumberingDtoSortInput> = {
			sortNo_ASC: { sortNo: SortEnumType.Asc },
			sortNo_DESC: { sortNo: SortEnumType.Desc },
			date_ASC: { date: SortEnumType.Asc },
			date_DESC: { date: SortEnumType.Desc },
			no_ASC: { no: SortEnumType.Asc },
			no_DESC: { no: SortEnumType.Desc },
			make_ASC: { make: SortEnumType.Asc },
			make_DESC: { make: SortEnumType.Desc },
			serialNo_ASC: { serialNo: SortEnumType.Asc },
			serialNo_DESC: { serialNo: SortEnumType.Desc },
			orderNo_ASC: { orderNo: SortEnumType.Asc },
			orderStatus_ASC: { orderStatus: SortEnumType.Asc },
			supplier_ASC: { supplier: SortEnumType.Asc }
		};
		return [map[key] ?? { sortNo: SortEnumType.Asc }];
	}

	function toStartOfDayIso(dateStr: string): string {
		return new Date(`${dateStr}T00:00:00`).toISOString();
	}

	function toEndOfDayIso(dateStr: string): string {
		return new Date(`${dateStr}T23:59:59.999`).toISOString();
	}

	async function fetchPage(
		afterCursor: string | null,
		append: boolean,
		opts?: { skipCache?: boolean }
	) {
		if (append) loadingMore = true;
		else {
			loading = true;
			error = undefined;
		}

		try {
			const where = buildWhere(debouncedSearch);
			const order = buildOrder(currentSort);
			const variables: GetProcurementNewNumberingPagedQueryVariables = {
				first: PAGE_SIZE,
				after: afterCursor ?? undefined,
				respCenters: $fetchParamsStore?.respCenters?.[0],
				view: view === "Blank" ? "Blank" : undefined,
				fromDate: fromDateYmd ? toStartOfDayIso(fromDateYmd) : undefined,
				toDate: toDateYmd ? toEndOfDayIso(toDateYmd) : undefined,
				where: where ?? undefined,
				order
			};

			const res = await graphqlQuery<
				GetProcurementNewNumberingPagedQuery,
				GetProcurementNewNumberingPagedQueryVariables
			>(GetProcurementNewNumberingPagedDocument, {
				variables,
				skipLoading: true,
				skipCache: opts?.skipCache === true
			});

			if (res.success && res.data?.procurementNewNumberingPaged) {
				const data = res.data.procurementNewNumberingPaged;
				const next = (data.nodes ?? []) as LineNode[];
				if (append) {
					items = [...items, ...next];
				} else {
					items = next;
					totalCount = data.totalCount ?? 0;
				}
				cursor = data.pageInfo?.endCursor ?? null;
				hasMore = data.pageInfo?.hasNextPage ?? false;
			} else {
				error = res.error || "Failed to load new numbering lines.";
			}
		} catch {
			error = "An unexpected error occurred.";
		} finally {
			loading = false;
			loadingMore = false;
		}
	}

	function reload(opts?: { skipCache?: boolean }) {
		cursor = null;
		void fetchPage(null, false, opts);
	}

	function loadMore() {
		if (loadingMore || !hasMore || !cursor) return;
		void fetchPage(cursor, true);
	}

	function formatDate(iso: string | null | undefined) {
		if (!iso) return "—";
		const d = new Date(iso);
		return Number.isNaN(d.getTime())
			? String(iso)
			: d.toLocaleDateString("en-IN", {
					day: "2-digit",
					month: "short",
					year: "numeric"
				});
	}

	$effect(() => {
		settingsStore.update((s) => ({
			...s,
			activePage: `Last Used New Number ${ecomileLastNumber}`
		}));
	});

	$effect(() => {
		const q = searchQuery;
		const delay = q === "" ? 0 : 350;
		const id = setTimeout(() => {
			debouncedSearch = q;
		}, delay);
		return () => clearTimeout(id);
	});

	$effect(() => {
		if (!listReady) return;
		debouncedSearch;
		view;
		fromDateYmd;
		toDateYmd;
		currentSort;
		$fetchParamsStore?.respCenters?.[0];
		reload();
	});

	onMount(() => {
		ensureFetchParams();
		updateGoBackPath("/ecoproc");
		void getEcomileLastNumber();
		listReady = true;
	});

	const getEcomileLastNumber = async (skipCache = false) => {
		const res = await graphqlQuery<GetEcomileLastNewNumberQuery>(GetEcomileLastNewNumberDocument, {
			variables: { respCenter: $fetchParamsStore?.respCenters?.[0] || "BEL" },
			skipCache
		});
		if (res.success && res.data) {
			ecomileLastNumber = res.data.productionEcomileLastNewNumber;
		}
	};

	function openEdit(row: LineNode) {
		orderLine = { ...row } as ProcurementNewNumberingDto;
		if (!orderLine.newSerialNo) orderLine.newSerialNo = incrementAlphanumeric(ecomileLastNumber);
		openForm = true;
	}
</script>

<MasterList
	title="New numbering"
	{items}
	filtersCollapsible={true}
	bind:filtersOpen
	gridKeyboardNav={true}
	{loading}
	{loadingMore}
	{error}
	{hasMore}
	{totalCount}
	bind:searchQuery
	bind:viewMode
	{sortOptions}
	bind:currentSort
	searchPlaceholder="Search tyre size, make, serial, order, supplier…"
	onRefresh={() => {
		void getEcomileLastNumber(true);
		reload({ skipCache: true });
	}}
	onSearchClear={() => {
		searchQuery = "";
	}}
	onLoadMore={loadMore}
	onRowClick={openEdit}
>
	{#snippet beforeList()}
		{#if ecomileLastNumber}
			<div
				class="mb-4 flex flex-wrap items-center gap-2 rounded-xl border border-primary/20 bg-primary/5 px-4 py-3 text-sm"
			>
				<Icon name="hash" class="size-4 text-primary" />
				<span class="text-muted-foreground">Last ecomile new number:</span>
				<span class="font-mono font-semibold tracking-wide text-foreground">{ecomileLastNumber}</span>
			</div>
		{/if}
	{/snippet}

	{#snippet filters()}
		<div class="flex flex-wrap items-end gap-3 sm:gap-4">
			<div class="flex flex-col gap-1.5 min-w-[140px]">
				<span class="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">View</span>
				<div class="inline-flex rounded-lg border border-border bg-muted/40 p-0.5">
					<Button
						type="button"
						variant={view === "Blank" ? "default" : "ghost"}
						size="sm"
						class="h-8 px-3 text-xs"
						onclick={() => {
							view = "Blank";
						}}>Blank</Button
					>
					<Button
						type="button"
						variant={view === "All" ? "default" : "ghost"}
						size="sm"
						class="h-8 px-3 text-xs"
						onclick={() => {
							view = "All";
						}}>All</Button
					>
				</div>
			</div>
			<div class="flex min-w-[200px] max-w-md flex-1 flex-col gap-1.5 sm:min-w-[260px]">
				<span class="text-[11px] font-medium uppercase tracking-wide text-muted-foreground"
					>Dispatch date range</span
				>
				<DatePicker
					bind:value={dispatchRange}
					mode="range"
					valueType="calendar"
					placeholder="Select dispatch date range"
					presetKeys="thisMonth,lastMonth,thisQuarter,lastQuarter,thisFinYear,lastFinYear"
					fiscal
					workdate={workDateRef}
				/>
			</div>
			<Button
				type="button"
				variant="secondary"
				size="sm"
				class="h-9 self-end sm:self-auto"
				onclick={() => {
					dispatchRange = { start: undefined, end: undefined };
				}}
			>
				Clear dates
			</Button>
		</div>
	{/snippet}

	{#snippet tableHeader()}
		<TableHead class="min-w-[100px] whitespace-nowrap">Sort</TableHead>
		<TableHead class="min-w-[90px]">Tyre</TableHead>
		<TableHead class="min-w-[100px]">Make</TableHead>
		<TableHead class="min-w-[110px]">Serial</TableHead>
		<TableHead class="min-w-[110px]">New serial</TableHead>
		<TableHead class="min-w-[120px] hidden lg:table-cell">Market</TableHead>
		<TableHead class="min-w-[100px] hidden md:table-cell">Date</TableHead>
		<TableHead class="min-w-[100px] hidden xl:table-cell">Status</TableHead>
		<TableHead class="min-w-[90px] hidden 2xl:table-cell">Order</TableHead>
	{/snippet}

	{#snippet tableRow(item)}
		<TableCell class="font-mono text-xs whitespace-nowrap">{item.sortNo || "—"}</TableCell>
		<TableCell class="font-medium">{item.no || "—"}</TableCell>
		<TableCell>{item.make || "—"}</TableCell>
		<TableCell class="font-mono text-xs">{item.serialNo || "—"}</TableCell>
		<TableCell class="font-mono text-xs">{item.newSerialNo || "—"}</TableCell>
		<TableCell class="hidden lg:table-cell max-w-[140px] truncate" title={item.location}
			>{item.location || "—"}</TableCell
		>
		<TableCell class="hidden md:table-cell whitespace-nowrap">{formatDate(item.date)}</TableCell>
		<TableCell class="hidden xl:table-cell">
			<Badge variant="secondary" class="text-[10px] font-normal">{item.orderStatus || "—"}</Badge>
		</TableCell>
		<TableCell class="hidden 2xl:table-cell font-mono text-xs">{item.orderNo || "—"}</TableCell>
	{/snippet}

	{#snippet gridItem(item)}
		<button
			type="button"
			class="flex h-full w-full flex-col gap-3 rounded-xl border bg-card p-4 text-left shadow-sm transition-all hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
			onclick={() => openEdit(item)}
		>
			<div class="flex items-start justify-between gap-2">
				<Badge variant="outline" class="font-mono text-[10px]">{item.sortNo || "—"}</Badge>
				<span class="text-xs text-muted-foreground">{formatDate(item.date)}</span>
			</div>
			<div>
				<p class="text-base font-semibold leading-tight">{item.no} · {item.make}</p>
				<p class="mt-1 font-mono text-xs text-muted-foreground">S/N {item.serialNo || "—"}</p>
			</div>
			<div class="mt-auto flex flex-wrap items-center gap-2 border-t pt-3 text-xs">
				<span class="text-muted-foreground">New:</span>
				<span class="font-mono font-medium">{item.newSerialNo || "—"}</span>
				<Badge variant="secondary" class="ml-auto text-[10px]">{item.orderStatus || "—"}</Badge>
			</div>
			{#if item.location}
				<p class="text-xs text-muted-foreground line-clamp-2 flex items-start gap-1">
					<Icon name="map-pin" class="size-3 shrink-0 mt-0.5" />
					{item.location}
				</p>
			{/if}
		</button>
	{/snippet}
</MasterList>

{#if orderLine}
	<DialogPage
		open={openForm}
		onOpenChange={(o) => (openForm = o)}
		onAction={() => {
			openForm = false;
			dialogShow({
				title: "Confirm Save",
				description: "Are you sure you want to save the changes to this tyre?",
				actionLabel: "Save",
				onAction: async () => {
					dialogHide();
					const row = orderLine;
					if (!row) return;
					const res = await graphqlMutation(UpdateEcomileNewNumberLineDocument, {
						variables: {
							line: procurementNewNumberingToOrderLineDispatchInput(row)
						},
						silent: true
					});
					if (res.success) {
						toast.success("Tyre saved");
						orderLine = null;
						openForm = false;
						cursor = null;
						await fetchPage(null, false, { skipCache: true });
						await getEcomileLastNumber(true);
					} else {
						toast.error(res.error ?? "Could not save the tyre.");
						openForm = true;
					}
				},
				onCancel: () => {
					reload();
				}
			});
		}}
		actionLabel="Save"
		title={`${orderLine.no} - ${orderLine.make} - ${orderLine.serialNo}`}
	>
		<Form
			bind:data={orderLine}
			layoutClass="flex flex-col sm:flex-col md:grid md:grid-cols-2 gap-2 p-4"
			fields={[
				{
					name: "newSerialNo",
					label: "New Serial No",
					type: "text",
					required: true,
					rules: [required("Serial No")]
				}
			]}
		/>
	</DialogPage>
{/if}
