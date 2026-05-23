<script lang="ts">
	import { onMount } from 'svelte';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import {
		DataGrid,
		type DataGridColumn,
		type FilterRule,
		type FilterOperator
	} from '$lib/components/venUI/datagrid';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import * as Dialog from '$lib/components/ui/dialog';
	import { graphqlQuery, graphqlMutation, clearGraphQLCache } from '$lib/services/graphql';
	import { toast } from '$lib/components/venUI/toast';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { DatePicker } from '$lib/components/venUI/date-picker';
	import type { FetchParamsInput } from '$lib/services/graphql/generated/graphql';
	import { authStore } from '$lib/stores/auth';
	import { cn } from '$lib/utils';

	const LIST_Q = `
		query ProcurementConfigsList {
			productionProcurementConfigs {
				type
				itemNo
				market
				qty
				fromDate
				toDate
			}
		}
	`;

	const INSERT_M = `
		mutation InsertProcurementConfig($row: ProcurementConfigDtoInput!) {
			insertProductionProcurementConfig(row: $row) {
				success
				message
			}
		}
	`;

	const UPDATE_M = `
		mutation UpdateProcurementConfig($original: ProcurementConfigDtoInput!, $updated: ProcurementConfigDtoInput!) {
			updateProductionProcurementConfig(original: $original, updated: $updated) {
				success
				message
			}
		}
	`;

	const DELETE_M = `
		mutation DeleteProcurementConfig($key: ProcurementConfigDtoInput!) {
			deleteProductionProcurementConfig(key: $key) {
				success
				message
			}
		}
	`;

	type Row = {
		type: number;
		itemNo: string;
		market: string;
		qty: number;
		fromDate: string;
		toDate: string;
	};

	function normalizeProcurementRow(raw: Record<string, unknown>): Row {
		const pick = <T>(...keys: string[]): T | undefined => {
			for (const k of keys) {
				if (raw[k] !== undefined && raw[k] !== null) return raw[k] as T;
			}
			return undefined;
		};
		const toIso = (v: unknown): string => {
			if (v == null) return '';
			if (typeof v === 'string') return v.trim();
			if (typeof v === 'number' && Number.isFinite(v))
				return new Date(v).toISOString();
			if (typeof v === 'object' && v instanceof Date) return v.toISOString();
			return String(v);
		};
		return {
			type: Number(pick('type', 'Type')) || 0,
			itemNo: String(pick('itemNo', 'ItemNo') ?? '').trim(),
			market: String(pick('market', 'Market') ?? '').trim(),
			qty: Number(pick('qty', 'Qty')) || 0,
			fromDate: toIso(pick('fromDate', 'FromDate')),
			toDate: toIso(pick('toDate', 'ToDate'))
		};
	}

	type Mr = { success: boolean; message?: string | null };
	type DateRangeDate = { start?: Date; end?: Date };

	let rows = $state<Row[]>([]);
	let loading = $state(true);
	let searchQuery = $state('');
	let marketFilter = $state('');
	let minQtyRaw = $state('');
	let filterRules = $state<FilterRule[]>([]);

	let dialogOpen = $state(false);
	let saving = $state(false);
	let editingOriginal = $state<Row | null>(null);

	let draftQty = $state(0);
	let dateRange = $state<DateRangeDate | undefined>();

	let dialogForm = $state<{
		values: Record<string, string>;
		setTouched: (n: string) => void;
	}>({
		values: { itemNo: '', market: '' },
		setTouched() {}
	});

	function dateSortKey(iso: string): number {
		if (!iso?.trim()) return 0;
		const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso.trim());
		if (m) return Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
		const t = new Date(iso).getTime();
		return Number.isFinite(t) ? t : 0;
	}

	const purchaseItemParam = $derived.by((): FetchParamsInput | null => {
		const u = $authStore.user;
		if (!u) return null;
		return {
			regions: ['CASING'],
			type: 'FromGroupDetail',
			respCenters: u.respCenter ? [u.respCenter] : [],
			areas: [],
			nos: [],
			from: '',
			to: '',
			reportName: '',
			userCode: u.entityCode ?? '',
			userDepartment: u.department ?? '',
			userSpecialToken: u.userSpecialToken ?? '',
			userType: u.entityType ?? '',
			view: ''
		};
	});

	const productionMarketParam = $derived.by((): FetchParamsInput | null => {
		const u = $authStore.user;
		if (!u) return null;
		return {
			respCenters: u.respCenter ? [u.respCenter] : [],
			regions: [],
			areas: [],
			nos: [],
			from: '',
			to: '',
			reportName: '',
			type: '',
			userCode: u.entityCode ?? '',
			userDepartment: u.department ?? '',
			userSpecialToken: u.userSpecialToken ?? '',
			userType: u.entityType ?? '',
			view: ''
		};
	});

	const uniqueMarkets = $derived.by(() =>
		[...new Set(rows.map((r) => r.market).filter((m) => m?.trim()))].sort((a, b) =>
			a.localeCompare(b, undefined, { sensitivity: 'base' })
		)
	);

	const minQty = $derived.by(() => {
		const t = minQtyRaw.trim();
		if (t === '') return null;
		const n = Number(t);
		return Number.isFinite(n) ? n : null;
	});

	function fmtNavDate(iso: string): string {
		if (!iso?.trim()) return '—';
		const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso.trim());
		if (m) {
			const y = Number(m[1]);
			const day = Number(m[3]);
			const month = Number(m[2]);
			if (!Number.isFinite(y) || !Number.isFinite(month) || !Number.isFinite(day) || y <= 1)
				return '—';
			return `${String(day).padStart(2, '0')}/${String(month).padStart(2, '0')}/${y}`;
		}
		const d = new Date(iso);
		if (Number.isNaN(d.getTime())) return iso || '—';
		if (d.getUTCFullYear() <= 1) return '—';
		return d.toLocaleDateString('en-IN', {
			timeZone: 'UTC',
			year: 'numeric',
			month: '2-digit',
			day: '2-digit'
		});
	}

	function parseRuleNumber(raw: unknown): number | null {
		const n = Number(String(raw ?? '').replace(/,/g, '').trim());
		return Number.isFinite(n) ? n : null;
	}

	function parseFlexibleDateUtc(needle: string): number | null {
		const t = needle.trim();
		if (!t) return null;
		const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(t);
		if (m) return Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
		const m2 = /^(\d{1,2})\/(\d{1,2})\/(\d{4})/.exec(t);
		if (m2) return Date.UTC(Number(m2[3]), Number(m2[2]) - 1, Number(m2[1]));
		const ms = Date.parse(t);
		return Number.isFinite(ms) ? ms : null;
	}

	function dateTokensForRule(iso: string): string {
		const display = fmtNavDate(iso);
		const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso.trim());
		const ymd = m ? m[0].toLowerCase() : iso.trim().toLowerCase();
		return `${ymd} ${display.toLowerCase()}`;
	}

	function compareScalars(
		cell: number | string | null | undefined,
		needleRaw: unknown,
		op: FilterOperator
	): boolean {
		const needle = String(needleRaw ?? '').trim();
		const parts = needle
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean);

		switch (op) {
			case 'in': {
				const set =
					parts.length > 0 ? parts.map((s) => s.toLowerCase()) : [needle.toLowerCase()].filter(Boolean);
				if (!set.length) return true;
				const c = cell == null ? '' : typeof cell === 'number' ? String(cell) : String(cell).toLowerCase();
				return set.some((x) =>
					typeof cell === 'number'
						? String(cell) === x || Number(x.replace(/,/g, '')) === cell
						: c === x.toLowerCase()
				);
			}
			case 'notIn': {
				const set =
					parts.length > 0 ? parts.map((s) => s.toLowerCase()) : [needle.toLowerCase()].filter(Boolean);
				if (!set.length) return true;
				const c =
					cell == null ? '' : typeof cell === 'number' ? String(cell) : String(cell).toLowerCase();
				return !set.some((x) =>
					typeof cell === 'number'
						? String(cell) === x || Number(x.replace(/,/g, '')) === cell
						: c === x.toLowerCase()
				);
			}
			default:
				break;
		}

		if (typeof cell === 'number') {
			const tn = parseRuleNumber(needle);
			switch (op) {
				case 'contains':
					return needle.length === 0 ? true : String(cell).includes(needle.trim());
				case 'eq':
					return tn != null ? cell === tn : String(cell) === needle;
				case 'neq':
					return tn != null ? cell !== tn : String(cell) !== needle;
				case 'gt':
					return tn != null && cell > tn;
				case 'gte':
					return tn != null && cell >= tn;
				case 'lt':
					return tn != null && cell < tn;
				case 'lte':
					return tn != null && cell <= tn;
				case 'startsWith':
					return needle.length === 0 ? true : String(cell).startsWith(needle.trim());
				case 'endsWith':
					return needle.length === 0 ? true : String(cell).endsWith(needle.trim());
				default:
					return tn != null ? cell === tn : String(cell) === needle;
			}
		}

		const hay = cell == null ? '' : String(cell);
		const h = hay.toLowerCase();
		const n = needle.toLowerCase();
		switch (op) {
			case 'contains':
				return needle.length === 0 ? true : h.includes(n);
			case 'eq':
				return h === n;
			case 'neq':
				return h !== n;
			case 'startsWith':
				return needle.length === 0 ? true : h.startsWith(n);
			case 'endsWith':
				return needle.length === 0 ? true : h.endsWith(n);
			case 'gt':
			case 'gte':
			case 'lt':
			case 'lte': {
				const ca = hay.localeCompare(n, undefined, { numeric: true, sensitivity: 'base' });
				if (op === 'gt') return ca > 0;
				if (op === 'gte') return ca >= 0;
				if (op === 'lt') return ca < 0;
				return ca <= 0;
			}
			default:
				return needle.length === 0 ? true : h.includes(n);
		}
	}

	function compareDateColumn(iso: string, needleRaw: unknown, op: FilterOperator): boolean {
		const needle = String(needleRaw ?? '').trim();
		const hay = dateTokensForRule(iso);
		const rk = dateSortKey(iso);
		const nk = parseFlexibleDateUtc(needle);

		switch (op) {
			case 'contains':
				return needle.length === 0 || hay.includes(needle.toLowerCase());
			case 'startsWith':
				return needle.length === 0 || hay.startsWith(needle.toLowerCase());
			case 'endsWith':
				return needle.length === 0 || hay.endsWith(needle.toLowerCase());
			case 'eq':
				if (nk != null) return rk === nk || dateSortKey(iso.slice(0, 10)) === nk;
				return hay.includes(needle.toLowerCase()) && needle.length > 0;
			case 'neq':
				return !compareDateColumn(iso, needle, 'eq');
			case 'gt':
				return nk != null && rk > nk;
			case 'gte':
				return nk != null && rk >= nk;
			case 'lt':
				return nk != null && rk < nk;
			case 'lte':
				return nk != null && rk <= nk;
			case 'in': {
				const set = needle.split(',').map((s) => s.trim()).filter(Boolean);
				if (!set.length) return true;
				return set.some((frag) => compareDateColumn(iso, frag, 'eq'));
			}
			case 'notIn': {
				const set = needle.split(',').map((s) => s.trim()).filter(Boolean);
				if (!set.length) return true;
				return set.every((frag) => !compareDateColumn(iso, frag, 'eq'));
			}
			default:
				return needle.length === 0 ? true : hay.includes(needle.toLowerCase());
		}
	}

	function procurementRowMatchesRule(row: Row, rule: FilterRule): boolean {
		switch (rule.columnId) {
			case 'itemNo':
				return compareScalars(row.itemNo, rule.value, rule.operator);
			case 'market':
				return compareScalars(row.market, rule.value, rule.operator);
			case 'qty':
				return compareScalars(row.qty, rule.value, rule.operator);
			case 'fromDate':
				return compareDateColumn(row.fromDate, rule.value, rule.operator);
			case 'toDate':
				return compareDateColumn(row.toDate, rule.value, rule.operator);
			default:
				return true;
		}
	}

	function rowMatchesAdvancedFilters(row: Row, rules: FilterRule[]): boolean {
		if (!rules?.length) return true;
		return rules.every((r) => {
			if (String(r.value ?? '').trim() === '') return true;
			return procurementRowMatchesRule(row, r);
		});
	}

	/** Filtered list: search toolbar, market chips, min qty, then advanced Filters sheet (AND). Sorted by newest fromDate until user sorts columns. */
	const gridItems = $derived.by(() => {
		const q = searchQuery.trim().toLowerCase();
		let list = rows;
		if (q) {
			list = rows.filter(
				(r) =>
					r.itemNo.toLowerCase().includes(q) ||
					r.market.toLowerCase().includes(q) ||
					String(r.qty).includes(q) ||
					fmtNavDate(r.fromDate).toLowerCase().includes(q) ||
					fmtNavDate(r.toDate).toLowerCase().includes(q)
			);
		}
		if (marketFilter) list = list.filter((r) => r.market === marketFilter);
		if (minQty != null) list = list.filter((r) => r.qty >= minQty);
		list = list.filter((r) => rowMatchesAdvancedFilters(r, filterRules));
		return [...list].sort((a, b) => dateSortKey(b.fromDate) - dateSortKey(a.fromDate));
	});

	const cols: DataGridColumn<Row>[] = [
		{ accessorKey: 'itemNo', header: 'Item no.', enableSorting: true },
		{ accessorKey: 'market', header: 'Market', enableSorting: true },
		{
			accessorKey: 'qty',
			header: 'Qty',
			enableSorting: true,
			meta: { align: 'right' as const }
		},
		{
			id: 'fromDate',
			accessorKey: 'fromDate',
			header: 'From',
			enableSorting: true,
			sortingFn: (ra, rb) => dateSortKey(ra.original.fromDate) - dateSortKey(rb.original.fromDate),
			cell: ({ getValue }) => fmtNavDate(getValue() as string)
		},
		{
			id: 'toDate',
			accessorKey: 'toDate',
			header: 'To',
			enableSorting: true,
			sortingFn: (ra, rb) => dateSortKey(ra.original.toDate) - dateSortKey(rb.original.toDate),
			cell: ({ getValue }) => fmtNavDate(getValue() as string)
		}
	];

	function isoToCalendarRange(fromIso: string, toIso: string): DateRangeDate {
		if (!fromIso?.trim() || !toIso?.trim()) return todayCalendarRange();
		const a = new Date(fromIso);
		const b = new Date(toIso);
		const start = new Date(a.getFullYear(), a.getMonth(), a.getDate());
		const end = new Date(b.getFullYear(), b.getMonth(), b.getDate());
		return { start, end };
	}

	function rangeToIsoDates(r: DateRangeDate | undefined): { from: string; to: string } | null {
		if (!r?.start || !r?.end) return null;
		const from = new Date(r.start);
		from.setHours(0, 0, 0, 0);
		const to = new Date(r.end);
		to.setHours(23, 59, 59, 999);
		return { from: from.toISOString(), to: to.toISOString() };
	}

	function todayCalendarRange(): DateRangeDate {
		const t = new Date();
		const d = new Date(t.getFullYear(), t.getMonth(), t.getDate());
		return { start: d, end: d };
	}

	function rowPayload(typeFromRow: number): Row {
		const itemNo = String(dialogForm.values.itemNo ?? '').trim();
		const market = String(dialogForm.values.market ?? '').trim();
		const bounds = rangeToIsoDates(dateRange);
		return {
			type: typeFromRow,
			itemNo,
			market,
			qty: draftQty,
			fromDate: bounds?.from ?? new Date().toISOString(),
			toDate: bounds?.to ?? new Date().toISOString()
		};
	}

	function resetDialogForm() {
		dialogForm.values.itemNo = '';
		dialogForm.values.market = '';
	}

	function openNew() {
		editingOriginal = null;
		resetDialogForm();
		draftQty = 0;
		dateRange = todayCalendarRange();
		dialogOpen = true;
	}

	function openEdit(r: Row) {
		editingOriginal = { ...r };
		dialogForm.values.itemNo = r.itemNo ?? '';
		dialogForm.values.market = r.market ?? '';
		draftQty = r.qty;
		dateRange = isoToCalendarRange(r.fromDate, r.toDate);
		dialogOpen = true;
	}

	async function reload() {
		loading = true;
		try {
			const res = await graphqlQuery<{ productionProcurementConfigs: Record<string, unknown>[] }>(
				LIST_Q,
				{
					cacheKey: 'productionProcurementConfigs',
					skipCache: true
				}
			);
			if (!res.success || !res.data) {
				toast.error(res.error ?? 'Failed to load procurement configs.');
				return;
			}
			const raw = res.data.productionProcurementConfigs ?? [];
			rows = raw.map((r) => normalizeProcurementRow(r as unknown as Record<string, unknown>));
		} finally {
			loading = false;
		}
	}

	onMount(() => {
		reload();
	});

	async function onSave() {
		saving = true;
		try {
			const bounds = rangeToIsoDates(dateRange);
			if (!bounds) {
				toast.error('Select a validity date range.');
				return;
			}

			const typeVal = editingOriginal?.type ?? 0;
			const payload = rowPayload(typeVal);

			if (!payload.itemNo.trim()) {
				toast.error('Item no. is required.');
				return;
			}
			if (!payload.market.trim()) {
				toast.error('Market is required.');
				return;
			}

			if (editingOriginal) {
				const mu = await graphqlMutation<{
					updateProductionProcurementConfig: Mr;
				}>(UPDATE_M, {
					variables: { original: editingOriginal, updated: payload }
				});
				if (
					!mu.success ||
					!mu.data?.updateProductionProcurementConfig?.success
				) {
					toast.error(
						mu.error ??
							mu.data?.updateProductionProcurementConfig?.message ??
							'Update failed.'
					);
					return;
				}
				toast.success(mu.data.updateProductionProcurementConfig.message ?? 'Updated.');
			} else {
				const mu = await graphqlMutation<{
					insertProductionProcurementConfig: Mr;
				}>(INSERT_M, {
					variables: { row: payload }
				});
				if (
					!mu.success ||
					!mu.data?.insertProductionProcurementConfig?.success
				) {
					toast.error(
						mu.error ??
							mu.data?.insertProductionProcurementConfig?.message ??
							'Insert failed.'
					);
					return;
				}
				toast.success(mu.data.insertProductionProcurementConfig.message ?? 'Saved.');
			}
			dialogOpen = false;
			clearGraphQLCache('productionProcurementConfigs');
			await reload();
		} finally {
			saving = false;
		}
	}

	async function onDeleteFromDialog() {
		if (!editingOriginal) return;
		await onDelete(editingOriginal);
		dialogOpen = false;
	}

	async function onDelete(r: Row) {
		if (!confirm('Delete this procurement configuration row?')) return;
		saving = true;
		try {
			const mu = await graphqlMutation<{
				deleteProductionProcurementConfig: Mr;
			}>(DELETE_M, {
				variables: { key: r }
			});
			if (
				!mu.success ||
				!mu.data?.deleteProductionProcurementConfig?.success
			) {
				toast.error(
					mu.error ??
						mu.data?.deleteProductionProcurementConfig?.message ??
						'Delete failed.'
				);
				return;
			}
			toast.success(mu.data.deleteProductionProcurementConfig.message ?? 'Deleted.');
			clearGraphQLCache('productionProcurementConfigs');
			await reload();
		} finally {
			saving = false;
		}
	}
</script>

<svelte:head>
	<title>Procurement configs</title>
</svelte:head>

<div class="flex min-h-svh flex-col bg-background text-foreground">
	<PageHeading backHref="/menu" icon="sliders-horizontal" pageTitle="Procurement configs">
		{#snippet title()}Procurement configs{/snippet}
		{#snippet actions()}
			<Button size="sm" class="gap-2" onclick={openNew} disabled={loading || saving}>
				<Icon name="plus" class="size-3.5" />
				Add row
			</Button>
		{/snippet}
	</PageHeading>

	<main class="flex-1 space-y-3 pb-20 pt-2 px-4">
		<DataGrid
			title=""
			items={gridItems}
			columns={cols}
			bind:searchQuery
			bind:filterRules
			showFilters={true}
			{loading}
			loadingMore={false}
			onRowClick={(r: Row) => openEdit(r)}
			mobileCardTitleKey="itemNo"
			mobileCardSubtitleKey="market"
		>
			{#snippet actions()}
				<div class="flex w-full min-w-0 flex-col gap-2 sm:flex-row sm:flex-wrap sm:items-center sm:justify-end">
					<div class="flex min-w-0 flex-wrap items-center gap-1.5">
						<span class="text-xs font-medium text-muted-foreground">Market</span>
						<button
							type="button"
							class={cn(
								'rounded-md border px-2 py-1 text-xs font-medium transition-colors',
								!marketFilter
									? 'border-primary/40 bg-primary/10 text-primary'
									: 'border-border/60 bg-muted/40 text-muted-foreground hover:bg-muted'
							)}
							onclick={() => {
								marketFilter = '';
							}}
						>
							All
						</button>
						{#each uniqueMarkets as m}
							<button
								type="button"
								class={cn(
									'max-w-[120px] truncate rounded-md border px-2 py-1 text-xs font-medium transition-colors',
									marketFilter === m
										? 'border-primary/40 bg-primary/10 text-primary'
										: 'border-border/60 bg-muted/40 text-muted-foreground hover:bg-muted'
								)}
								title={m}
								onclick={() => {
									marketFilter = marketFilter === m ? '' : m;
								}}
							>
								{m}
							</button>
						{/each}
						<label class="ml-1 flex items-center gap-1.5 text-xs">
							<span class="text-muted-foreground whitespace-nowrap">Min qty</span>
							<Input
								type="number"
								min="0"
								step="1"
								class="h-8 w-20"
								bind:value={minQtyRaw}
							/>
						</label>
					</div>
					<Button
						variant="outline"
						size="sm"
						class="gap-1.5 shrink-0"
						onclick={() => void reload()}
						disabled={loading || saving}
						title="Reload from NAV"
					>
						<Icon name="refresh-cw" class="size-3.5 {!loading ? '' : 'animate-spin'}" />
						Refresh
					</Button>
				</div>
			{/snippet}
		</DataGrid>
	</main>
</div>

<Dialog.Root bind:open={dialogOpen}>
	<Dialog.Content class="sm:max-w-lg">
		<Dialog.Header>
			<Dialog.Title>{editingOriginal ? 'Edit row' : 'New row'}</Dialog.Title>
		</Dialog.Header>

		<div class="grid gap-4 py-2">
			<MasterSelect
				bind:form={dialogForm}
				fieldName="itemNo"
				masterType="purchaseItems"
				label="Item no."
				placeholder={purchaseItemParam ? 'Search casing / tyre size…' : 'Sign in…'}
				singleSelect
				disabled={!purchaseItemParam || saving}
				purchaseItemParam={purchaseItemParam ?? null}
				respCenterType="Production,Purchase,Sale,Payroll"
			/>

			<MasterSelect
				bind:form={dialogForm}
				fieldName="market"
				masterType="productionProcurementMarkets"
				label="Market"
				placeholder={productionMarketParam ? 'Search market…' : 'Sign in…'}
				singleSelect
				disabled={!productionMarketParam || saving}
				productionFetchParam={productionMarketParam ?? null}
				respCenterType="Production,Purchase,Sale,Payroll"
			/>

			<label class="grid gap-1.5 text-sm">
				<span class="font-medium leading-none text-foreground">Qty</span>
				<Input type="number" bind:value={draftQty} disabled={saving} />
			</label>

			<div class="grid gap-1.5 text-sm">
				<span class="font-medium leading-none text-foreground">Validity period</span>
				<DatePicker
					bind:value={dateRange}
					mode="range"
					valueType="date"
					disabled={saving}
					placeholder="Select from / to dates"
				/>
			</div>
		</div>

		<Dialog.Footer class="flex flex-wrap gap-2 sm:justify-between">
			<div>
				{#if editingOriginal}
					<Button variant="destructive" onclick={onDeleteFromDialog} disabled={saving}>
						Delete
					</Button>
				{/if}
			</div>
			<div class="flex gap-2">
				<Button variant="outline" onclick={() => (dialogOpen = false)} disabled={saving}>Cancel</Button>
				<Button onclick={onSave} disabled={saving}>
					{saving ? 'Saving…' : 'Save'}
				</Button>
			</div>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
