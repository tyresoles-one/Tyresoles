<script lang="ts">
	import * as Command from '$lib/components/ui/command';
	import * as Popover from '$lib/components/ui/popover';
	import Check from '@lucide/svelte/icons/check';
	import ChevronsUpDown from '@lucide/svelte/icons/chevrons-up-down';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import X from '@lucide/svelte/icons/x';
	import { cn } from '$lib/utils';
	import { graphqlQuery } from '$lib/services/graphql';
	import { authStore } from '$lib/stores/auth';
	import {
		getMasterQuery,
		buildWhereFilter,
		getValueKey,
		getProductionArrayFieldKey,
		isProductionArrayMaster,
		PAGE_SIZE,
		type MasterType,
		type MasterOption,
		type ProductionArrayQueryResult,
	} from './masters-api';
	import * as Field from '$lib/components/ui/field';
	import type { FetchParamsInput } from '$lib/services/graphql/generated/graphql';
	import { tick } from 'svelte';

	type Props = {
		form: { 
			values: Record<string, unknown>; 
			setTouched: (name: string) => void;
			errors?: Record<string, string | undefined>;
		};
		fieldName: string;
		masterType: MasterType;
		label?: string;
		placeholder?: string;
		disabled?: boolean;
		orientation?: 'vertical' | 'horizontal' | 'responsive';
		respCenterType?: string;
		/** When true, picking an item sets the value and closes the dropdown (single-select mode). */
		singleSelect?: boolean;
		/** Override the respCenter variable sent to the GraphQL query. Can be string or array (array used for vehicles). */
		respCenterOverride?: string | string[] | null;
		/** For `masterType="vendors"`, limit to these vendor categories (e.g. casing procurement). Default: all (`[]`). */
		vendorCategories?: string[];
		/** Required for `masterType="purchaseItems"` — Hot Chocolate field `purchaseItemNos` (C# `GetPurchaseItemNos`) / legacy ItemNos. */
		purchaseItemParam?: FetchParamsInput | null;
		/** Required for `production*` master types — maps to Query.cs `ProductionFetchParams param`. */
		productionFetchParam?: FetchParamsInput | null;
	};

	let {
		form,
		fieldName,
		masterType,
		label,
		placeholder = 'Search and select...',
		disabled = false,
		orientation = 'vertical',
		respCenterType = 'Sale,Payroll,Production,Purchase',
		singleSelect = false,
		respCenterOverride = undefined,
		vendorCategories,
		purchaseItemParam = null,
		productionFetchParam = null,
	}: Props = $props();

	const user = $derived(authStore.get().user);
	const entityContext = $derived({
		entityType: user?.entityType ?? null,
		entityCode: user?.entityCode ?? null,
		department: user?.department ?? null,
		respCenter: user?.respCenter ?? null,
	});

	let open = $state(false);
	/** Combobox trigger — used to dispatch `ven-form:next-focus` after selection / Enter. */
	let triggerRef = $state<HTMLButtonElement | null>(null);
	let searchQuery = $state('');
	let debouncedSearch = $state('');
	let options = $state<MasterOption[]>([]);
	let loading = $state(false);
	let loadingMore = $state(false);
	let hasNextPage = $state(false);
	let endCursor = $state<string | null>(null);
	let totalCount = $state(0);
	/** Full list from production array queries; `options` is filtered by search client-side. */
	let productionCache = $state<MasterOption[]>([]);

	const valueStr = $derived(String(form.values[fieldName] ?? ''));
	const selectedValues = $derived(
		valueStr
			.split(',')
			.map((s) => s.trim())
			.filter(Boolean)
	);

	// Debounce search (300ms)
	$effect(() => {
		const q = searchQuery;
		const t = setTimeout(() => {
			debouncedSearch = q;
		}, 300);
		return () => clearTimeout(t);
	});

	function nodeToOption(node: { code?: string | null; no?: string | null; name?: string | null; category?: string | null }): MasterOption {
		if (masterType === 'purchaseItems') {
			const n = node as { code?: string | null; name?: string | null; category?: string | null };
			const value = n.code ?? '';
			// Show code only in the list; search still filters by code/name/category via GraphQL where.
			return { label: value, value };
		}
		/** GetProductionMakeSubMake: Code=Category (make), Name=sub-make code — persist Name as value. */
		if (masterType === 'productionMakeSubMake') {
			const n = node as { code?: string | null; name?: string | null };
			const sub = String(n.name ?? '').trim();
			return {
				value: sub,
				label: sub,
			};
		}
		if (
			masterType === 'productionMakes' ||
			masterType === 'productionProcurementMarkets' ||
			masterType === 'productionProcurementInspection'
		) {
			const valueKey = getValueKey(masterType);
			const raw =
				valueKey === 'no'
					? (node as { no?: string | null }).no
					: (node as { code?: string | null }).code;
			const value = raw ?? '';
			return { label: value, value };
		}
		const valueKey = getValueKey(masterType);
		const raw = valueKey === 'no' ? (node as { no?: string | null }).no : (node as { code?: string | null }).code;
		const value = raw ?? '';
		const name = node.name ?? '';
		return { label: name ? `${value} - ${name}` : value, value };
	}

	function applyProductionFilter() {
		const q = debouncedSearch.trim().toLowerCase();
		options = q
			? productionCache.filter(
					(o) =>
						o.label.toLowerCase().includes(q) || o.value.toLowerCase().includes(q)
				)
			: productionCache;
	}

	async function loadProductionList() {
		if (!isProductionArrayMaster(masterType)) return;
		if (!productionFetchParam) {
			productionCache = [];
			applyProductionFilter();
			return;
		}
		if (
			masterType === 'productionMakeSubMake' &&
			!String(productionFetchParam.type ?? '').trim()
		) {
			productionCache = [];
			applyProductionFilter();
			return;
		}
		const fieldKey = getProductionArrayFieldKey(masterType);
		if (!fieldKey) return;
		loading = true;
		try {
			const query = getMasterQuery(masterType);
			const result = await graphqlQuery<ProductionArrayQueryResult>(query, {
				variables: { param: productionFetchParam },
				skipLoading: true,
				skipCache: true,
			});
			if (!result.success || !result.data) return;
			const rows = result.data[fieldKey] ?? [];
			productionCache = (rows as { code?: string; name?: string }[]).map((n) =>
				nodeToOption(n as { code?: string | null; no?: string | null; name?: string | null })
			);
			hasNextPage = false;
		} finally {
			loading = false;
		}
		applyProductionFilter();
	}

	async function fetchPage(append: boolean) {
		if (isProductionArrayMaster(masterType)) return;
		if (masterType === 'purchaseItems' && !purchaseItemParam) return;

		const query = getMasterQuery(masterType);
		const where = buildWhereFilter(masterType, debouncedSearch);
		const variables: Record<string, any> =
			masterType === 'purchaseItems'
				? {
						param: purchaseItemParam,
						first: PAGE_SIZE,
						after: append ? endCursor : undefined,
						where: where ?? undefined,
					}
				: {
						...entityContext,
						first: PAGE_SIZE,
						after: append ? endCursor : undefined,
						where: where ?? undefined,
					};
		// respCenterOverride wins over the entityContext.respCenter/respCenters when provided
		if (masterType !== 'purchaseItems' && respCenterOverride !== undefined) {
			variables.respCenter = Array.isArray(respCenterOverride) ? respCenterOverride[0] : respCenterOverride;
		}
		if (masterType === 'respCenters') {
			variables.type = respCenterType;
		}
		if (masterType === 'vendors') {
			variables.categories = vendorCategories ?? [];
			/** Ecomile procurement: scope vendors to eco manager matching logged-in user code (`userId`). */
			if (user?.userSpecialToken) {
				variables.ecoMgr = user.userId ?? undefined;
			}
		}

		if (append) loadingMore = true;
		else loading = true;
		try {
			const result = await graphqlQuery<{
				myRegions?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myCustomers?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myAreas?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myDealers?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myRespCenters?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myVehicles?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				myVendors?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
				purchaseItemNos?: { nodes: unknown[]; pageInfo: { hasNextPage: boolean; endCursor: string | null }; totalCount: number };
			}>(query, { variables, skipLoading: true, skipCache: true });
			if (!result.success || !result.data) return;
			const conn =
				result.data.myRegions ??
				result.data.myCustomers ??
				result.data.myAreas ??
				result.data.myDealers ??
				result.data.myRespCenters ??
				result.data.myVehicles ??
				result.data.myVendors ??
				result.data.purchaseItemNos;
			if (!conn?.nodes) return;
			const newOptions = (conn.nodes as Record<string, unknown>[]).map((n) =>
				nodeToOption(n as { code?: string | null; no?: string | null; name?: string | null })
			);
			if (append) options = [...options, ...newOptions];
			else options = newOptions;
			hasNextPage = conn.pageInfo?.hasNextPage ?? false;
			endCursor = conn.pageInfo?.endCursor ?? null;
			totalCount = conn.totalCount ?? 0;
		} finally {
			loading = false;
			loadingMore = false;
		}
	}

	// When popover opens, fetch first page (or refetch when search / purchase scope changes)
	$effect(() => {
		if (!open) return;
		if (isProductionArrayMaster(masterType)) return;
		debouncedSearch;
		purchaseItemParam;
		fetchPage(false);
	});

	// Production masters: full array from API, filter client-side (Query.cs list<CodeName>).
	$effect(() => {
		if (!open) return;
		if (!isProductionArrayMaster(masterType)) return;
		productionFetchParam;
		void loadProductionList();
	});

	$effect(() => {
		if (!isProductionArrayMaster(masterType)) return;
		debouncedSearch;
		productionCache;
		applyProductionFilter();
	});

	// Scroll-to-bottom: load more
	function viewport(node: HTMLElement) {
		const observer = new IntersectionObserver(
			(entries) => {
				if (entries[0].isIntersecting && hasNextPage && !loadingMore && !loading) {
					fetchPage(true);
				}
			},
			{ root: null, rootMargin: '80px' }
		);
		observer.observe(node);
		return { destroy: () => observer.disconnect() };
	}

	async function toggleSelection(val: string) {
		if (singleSelect) {
			// Single-select: replace value and close the popover
			(form.values as Record<string, string>)[fieldName] = val;
			form.setTouched(fieldName);
			open = false;
			searchQuery = '';
			await tick();
			requestAnimationFrame(() => {
				triggerRef?.dispatchEvent(
					new CustomEvent('ven-form:next-focus', { bubbles: true })
				);
			});
		} else {
			const idx = selectedValues.indexOf(val);
			let next: string[];
			if (idx >= 0) next = selectedValues.filter((_, i) => i !== idx);
			else next = [...selectedValues, val];
			(form.values as Record<string, string>)[fieldName] = next.join(', ');
			form.setTouched(fieldName);
		}
	}

	function handleClear(e: MouseEvent) {
		e.stopPropagation();
		(form.values as Record<string, string>)[fieldName] = '';
		form.setTouched(fieldName);
	}

	const selectedLabel = $derived(
		selectedValues.length === 0
			? placeholder
			: selectedValues.length === 1
				? options.find((o) => o.value === selectedValues[0])?.label ?? selectedValues[0]
				: `${selectedValues.length} selected`
	);
	const error = $derived(form.errors?.[fieldName]);
</script>

<Field.Field {orientation} class="w-full" data-invalid={!!error}>
	{#if label}
		<Field.Label class={cn(
			(orientation === 'horizontal' || orientation === 'responsive') && "flex-none sm:w-32 md:w-40"
		)}>
			{label}
		</Field.Label>
	{/if}
	<Field.Content>
		<!-- Clear must sit outside the trigger (valid HTML; nested buttons break focus order). -->
		<div
			class={cn(
				'flex w-full min-w-0 items-stretch gap-1 rounded-md ring-offset-background transition-[box-shadow]',
				error
					? 'focus-within:ring-[3px] focus-within:ring-destructive/20 dark:focus-within:ring-destructive/40 focus-within:border-destructive focus-within:ring-offset-2'
					: 'focus-within:ring-[3px] focus-within:ring-ring/50 focus-within:border-ring focus-within:ring-offset-2',
			)}
		>
		<!--
			Capture before bits-ui trigger onkeydown (Enter/Space toggle open).
			- Enter: move to next field (ven form focus manager).
			- ArrowDown/ArrowUp (closed): open list (combobox pattern).
			- Space: not handled here — bits-ui opens/closes the popover.
		-->
		<div
			class="min-w-0 flex-1"
			onkeydowncapture={(e: KeyboardEvent) => {
				if (open) return;
				const t = e.target as HTMLElement | null;
				if (!triggerRef || t !== triggerRef) return;

				if (e.key === 'Enter') {
					e.preventDefault();
					e.stopPropagation();
					triggerRef.dispatchEvent(
						new CustomEvent('ven-form:next-focus', { bubbles: true })
					);
					return;
				}

				if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
					e.preventDefault();
					e.stopPropagation();
					open = true;
				}
			}}
		>
	<Popover.Root bind:open>
		<Popover.Trigger
			bind:ref={triggerRef}
			data-ven-form-combobox-trigger
			class={cn(
				'border-input ring-offset-background placeholder:text-muted-foreground flex h-9 w-full min-w-0 items-center justify-between whitespace-nowrap rounded-md border bg-transparent px-3 py-2 text-sm shadow-xs focus:outline-none focus:ring-0 focus-visible:ring-0 disabled:cursor-not-allowed disabled:opacity-50 [&>span]:line-clamp-1',
				'aria-invalid:border-destructive'
			)}
			disabled={disabled}
			aria-invalid={!!error}
		>
			<span class="truncate">{selectedLabel}</span>
			<ChevronsUpDown class="ml-2 size-4 shrink-0 opacity-50" />
		</Popover.Trigger>
		<Popover.Content
			class="min-w-[200px] p-0 max-w-[calc(100vw-2rem)] w-(--bits-popover-anchor-width)"
			align="start"
			sideOffset={4}
		>
			<Command.Root shouldFilter={false} class="flex flex-col max-h-[min(80vh,400px)]">
				<Command.Input placeholder="Search..." bind:value={searchQuery} />
				<Command.List class="overflow-x-hidden overflow-y-auto flex-1 max-h-none min-h-[120px]">
					{#if loading}
						<div class="flex items-center justify-center py-8 text-muted-foreground">
							<Loader2 class="size-5 animate-spin" />
						</div>
					{:else}
						<Command.Empty>No results.</Command.Empty>
						<Command.Group class="overflow-visible min-w-full w-max">
							{#each options as opt}
								{@const isSelected = selectedValues.includes(opt.value)}
								<Command.Item
									value={opt.value}
									keywords={[]}
									onSelect={() => toggleSelection(opt.value)}
									class="cursor-pointer pr-4 min-w-full w-max flex items-center gap-2"
								>
									<div class="flex h-4 w-4 shrink-0 items-center justify-center">
										<Check class={cn('size-4', isSelected ? 'opacity-100' : 'opacity-0')} />
									</div>
									{opt.label}
								</Command.Item>
							{/each}
							{#if hasNextPage && options.length > 0}
								<div use:viewport class="h-6 flex items-center justify-center">
									{#if loadingMore}
										<Loader2 class="size-4 animate-spin text-muted-foreground" />
									{:else}
										<span class="text-muted-foreground text-xs">Scroll for more</span>
									{/if}
								</div>
							{/if}
						</Command.Group>
					{/if}
				</Command.List>
			</Command.Root>
		</Popover.Content>
	</Popover.Root>
		</div>
		{#if selectedValues.length > 0}
			<button
				type="button"
				tabindex="-1"
				data-ven-form-slot
				class="border-input text-muted-foreground hover:text-foreground hover:bg-muted/50 flex h-9 w-9 shrink-0 items-center justify-center rounded-md border bg-transparent shadow-xs transition-colors outline-none ring-0 focus:outline-none focus-visible:outline-none focus-visible:ring-0 disabled:opacity-50"
				onclick={handleClear}
				aria-label="Clear selection"
			>
				<X class="size-4" />
			</button>
		{/if}
		</div>
	</Field.Content>
	{#if error}
		<Field.Error>{error}</Field.Error>
	{/if}
</Field.Field>
