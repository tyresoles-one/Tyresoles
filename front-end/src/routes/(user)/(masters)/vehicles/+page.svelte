<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';

	import { usePaginatedList } from '$lib/composables';
	import { authStore } from '$lib/stores/auth';
	import { DataGrid, type DataGridColumn, type FilterRule } from '$lib/components/venUI/datagrid';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import * as Dialog from '$lib/components/ui/dialog';
	import * as Field from '$lib/components/ui/field';
	import { Icon } from '$lib/components/venUI/icon';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery, graphqlMutation, buildMutation } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

	import { GetVehicleMasterListDocument } from '$lib/services/graphql/generated/types';
	import type { GetVehicleMasterListQuery } from '$lib/services/graphql/generated/types';

	type SaveVehicleMut = {
		saveVehicle: { success: boolean; message: string };
	};

	const SaveVehicleDocument = buildMutation`
		mutation SaveVehicle($input: VehicleSaveInput!) {
			saveVehicle(input: $input) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<SaveVehicleMut, { input: VehicleSaveInput }>;

	type VehicleSaveInput = {
		no: string;
		name?: string | null;
		mobileNo?: string | null;
		gstNo?: string | null;
		lineNo: number;
		responsibilityCenter?: string | null;
		status: number;
	};

	type VehicleRow = NonNullable<
		NonNullable<GetVehicleMasterListQuery['vehicles']>['items']
	>[number];

	let filterRules = $state<FilterRule[]>([]);

	function buildFilterRulesWhere(rules: FilterRule[]): Record<string, unknown>[] | undefined {
		if (rules.length === 0) return undefined;
		return rules.map((rule) => {
			return { [rule.columnId]: { [rule.operator]: rule.value } };
		});
	}

	/** Maps search box + filter rules to Hot Chocolate `where` on `myVehicles`. */
	function vehiclesSearchToWhere(
		term: string,
		rulesForWhere: FilterRule[] = filterRules
	): Record<string, unknown> {
		const q = term.trim();
		const filterConds = buildFilterRulesWhere(rulesForWhere);
		const andConds: Record<string, unknown>[] = [];

		if (q) {
			andConds.push({
				or: [
					{ no: { contains: q } },
					{ name: { contains: q } },
					{ mobileNo: { contains: q } },
					{ gstNo: { contains: q } },
					{ responsibilityCenter: { contains: q } }
				]
			});
		}

		if (filterConds && filterConds.length > 0) {
			andConds.push(...filterConds);
		}

		if (andConds.length === 0) {
			return { where: null };
		}
		if (andConds.length === 1) {
			return { where: andConds[0] };
		}
		return { where: { and: andConds } };
	}

	const list = usePaginatedList<VehicleRow>({
		query: GetVehicleMasterListDocument,
		dataPath: 'vehicles',
		pageSize: 50,
		mapSearchToVariables: (term) => vehiclesSearchToWhere(term, filterRules),
		serverVariableAllowlist: ['entityType', 'entityCode', 'department', 'respCenter', 'where', 'order'],
		paginationMode: 'cursor',
		pageInfoPath: 'vehicles.pageInfo'
	});

	onMount(() => {
		const unsub = authStore.subscribe((auth) => {
			const u = auth.user;
			list.pagination.setVariables({
				entityType: u?.entityType ?? null,
				entityCode: u?.entityCode ?? null,
				department: u?.department ?? null,
				respCenter: u?.respCenter ?? null
			});
		});
		return unsub;
	});

	function handleFilterRulesChange(rules: FilterRule[]) {
		filterRules = rules;
		const vars = vehiclesSearchToWhere(list.searchQuery.value, rules);
		list.pagination.setVariables(vars);
		list.onRefresh();
	}

	function vehicleDetailPath(v: VehicleRow) {
		const id = v.no?.trim();
		return id ? `/vehicles/${encodeURIComponent(id)}` : '/vehmaster';
	}

	/** Strip whitespace and non-alphanumeric characters; uppercase (NAV / Indian RC format). */
	function normalizeVehicleNumber(input: string): string {
		return input.replace(/\s+/g, '').replace(/[^A-Za-z0-9]/g, '').toUpperCase();
	}

	function normalizeVehicleNo(raw: string): string {
		return normalizeVehicleNumber(raw);
	}

	/**
	 * Indian vehicle registration: standard state code + district + series + number, or Bharat (BH) series.
	 * @see https://parivahan.gov.in (formats vary slightly by state; this matches common patterns.)
	 */
	function isValidIndianVehicleNumber(input: string): boolean {
		const clean = normalizeVehicleNumber(input);
		if (!clean) return false;
		const regex =
			/^(?:[A-Z]{2}\d{1,2}(?:[A-Z]{1,3})?\d{1,4}|\d{2}BH\d{4}[A-Z]{2})$/;
		return regex.test(clean);
	}

	const MAX_VEHICLE_NO_LEN = 40;

	/** Returns a user-facing error message, or `null` when the normalized no is valid for lookup. */
	function validateNormalizedVehicleNo(normalized: string, hasTypedSomething: boolean): string | null {
		if (!hasTypedSomething) return null;
		if (!normalized) {
			return 'Use at least one letter or number (spaces and special characters are removed).';
		}
		if (normalized.length > MAX_VEHICLE_NO_LEN) {
			return `Vehicle number must be at most ${MAX_VEHICLE_NO_LEN} characters.`;
		}
		if (!isValidIndianVehicleNumber(normalized)) {
			return 'Enter a valid Indian registration number (e.g. KA01AB1234 or 21BH1234AA).';
		}
		return null;
	}

	/** Login stores locations on auth root (same as reports / GST pages). */
	const showRespCenterSelect = $derived(($authStore.locations ?? []).length > 1);
	const userRespCenterHint = $derived($authStore.user?.respCenter ?? '');

	let addDialogOpen = $state(false);
	let newVehicleNoInput = $state('');
	/** Bound to MasterSelect when multiple locations; default from user.respCenter on open. */
	let addVehicleFormValues = $state({ responsibilityCenter: '' });
	let availabilityLoading = $state(false);
	let availabilityChecked = $state<'idle' | 'available' | 'taken' | 'error'>('idle');
	let createLoading = $state(false);

	const normalizedNewNo = $derived(normalizeVehicleNo(newVehicleNoInput));

	const vehicleNoValidationError = $derived(
		validateNormalizedVehicleNo(normalizedNewNo, newVehicleNoInput.trim().length > 0)
	);

	const canCreateRespCenter = $derived(
		!showRespCenterSelect || !!addVehicleFormValues.responsibilityCenter.trim()
	);

	let addVehicleFormForMasters = $state.raw({
		get values() {
			return addVehicleFormValues as Record<string, unknown>;
		},
		setTouched: (_name: string) => {},
		get errors() {
			return {} as Record<string, string | undefined>;
		}
	});

	function scopeVars() {
		const u = authStore.get().user;
		return {
			entityType: u?.entityType ?? null,
			entityCode: u?.entityCode ?? null,
			department: u?.department ?? null,
			respCenter: u?.respCenter ?? null
		};
	}

	async function checkVehicleNoAvailability() {
		const no = normalizedNewNo;
		const err = validateNormalizedVehicleNo(no, newVehicleNoInput.trim().length > 0);
		if (!no || err) {
			availabilityChecked = 'idle';
			return;
		}
		availabilityLoading = true;
		availabilityChecked = 'idle';
		try {
			const res = await graphqlQuery<GetVehicleMasterListQuery>(GetVehicleMasterListDocument, {
				variables: {
					...scopeVars(),
					take: 1,
					after: null,
					where: { no: { eq: no } },
					order: null
				},
				skipLoading: true,
				skipCache: true
			});
			if (!res.success || !res.data?.vehicles) {
				availabilityChecked = 'error';
				return;
			}
			const count = res.data.vehicles.totalCount ?? 0;
			const items = res.data.vehicles.items ?? [];
			availabilityChecked = count > 0 || items.length > 0 ? 'taken' : 'available';
		} catch {
			availabilityChecked = 'error';
		} finally {
			availabilityLoading = false;
		}
	}

	let checkTimer: ReturnType<typeof setTimeout> | undefined;
	function scheduleAvailabilityCheck() {
		clearTimeout(checkTimer);
		checkTimer = setTimeout(() => void checkVehicleNoAvailability(), 400);
	}

	$effect(() => {
		if (!addDialogOpen) return;
		newVehicleNoInput;
		vehicleNoValidationError;
		clearTimeout(checkTimer);
		if (!normalizedNewNo) {
			availabilityChecked = 'idle';
			availabilityLoading = false;
			return;
		}
		if (vehicleNoValidationError) {
			availabilityChecked = 'idle';
			availabilityLoading = false;
			return;
		}
		scheduleAvailabilityCheck();
		return () => clearTimeout(checkTimer);
	});

	function resolveSaveResponsibilityCenter(): string {
		const u = authStore.get().user;
		if (showRespCenterSelect) {
			return addVehicleFormValues.responsibilityCenter.trim();
		}
		return (u?.respCenter ?? '').trim();
	}

	async function createVehicle() {
		const no = normalizedNewNo;
		const err = validateNormalizedVehicleNo(no, newVehicleNoInput.trim().length > 0);
		if (err) {
			toast.error(err);
			return;
		}
		const responsibilityCenter = resolveSaveResponsibilityCenter();
		if (showRespCenterSelect && !responsibilityCenter) {
			toast.error('Select a responsibility center');
			return;
		}
		if (!no || availabilityChecked !== 'available') return;
		createLoading = true;
		try {
			const res = await graphqlMutation<SaveVehicleMut>(SaveVehicleDocument, {
				variables: {
					input: {
						no,
						name: '',
						mobileNo: '',
						gstNo: '',
						lineNo: 0,
						responsibilityCenter,
						status: 0
					}
				}
			});
			if (!res.success || !res.data?.saveVehicle) {
				toast.error(res.error || 'Failed to create vehicle');
				return;
			}
			const { success, message } = res.data.saveVehicle;
			if (!success) {
				toast.error(message || 'Failed to create vehicle');
				return;
			}
			toast.success(message || 'Vehicle created');
			addDialogOpen = false;
			newVehicleNoInput = '';
			addVehicleFormValues.responsibilityCenter = '';
			availabilityChecked = 'idle';
			list.onRefresh();
			goto(`/vehicles/${encodeURIComponent(no)}`);
		} catch (e) {
			toast.error(e instanceof Error ? e.message : 'Failed to create vehicle');
		} finally {
			createLoading = false;
		}
	}

	function openAddDialog() {
		newVehicleNoInput = '';
		addVehicleFormValues.responsibilityCenter = authStore.get().user?.respCenter ?? '';
		availabilityChecked = 'idle';
		addDialogOpen = true;
	}

	const columns: DataGridColumn<VehicleRow>[] = [
		{ accessorKey: 'no', header: 'Vehicle No' },
		{ accessorKey: 'name', header: 'Name' },
		{ accessorKey: 'mobileNo', header: 'Mobile' },
		{ accessorKey: 'responsibilityCenter', header: 'Resp. center' },
		
		{
			accessorKey: 'status',
			header: 'Status',
			meta: { align: 'right' as const },
			cell: ({ getValue }) => (getValue() === 0 ? 'Active' : 'Inactive')
		}
	];
</script>

<div class="min-h-screen bg-background pb-20 pt-8">
	<DataGrid
		title="Vehicle master"
		description="View vehicles in your scope"
		items={list.items}
		{columns}
		pagination={list.pagination}
		loading={list.loading}
		loadingMore={list.loadingMore}
		bind:searchQuery={list.searchQuery.value}
		mobileCardTitleKey="name"
		mobileCardSubtitleKey="no"
		showFilters={true}
		{filterRules}
		onFilterRulesChange={handleFilterRulesChange}
		onRowClick={(v: VehicleRow) => goto(vehicleDetailPath(v))}
	>
		{#snippet actions()}
			<Button
				size="sm"
				class="gap-2 shrink-0 bg-primary/90 hover:bg-primary shadow-sm hover:shadow-md transition-all"
				onclick={openAddDialog}
			>
				<Icon name="plus" class="size-3.5" />
				<span class="hidden sm:inline">Add vehicle</span>
				<span class="sm:hidden">Add</span>
			</Button>
		{/snippet}
	</DataGrid>
</div>

<Dialog.Root
	bind:open={addDialogOpen}
	onOpenChange={(open) => {
		if (!open) {
			newVehicleNoInput = '';
			addVehicleFormValues.responsibilityCenter = '';
			availabilityChecked = 'idle';
		}
	}}
>
	<Dialog.Content class="sm:max-w-md">
		<Dialog.Header>
			<Dialog.Title>Add vehicle</Dialog.Title>			
		</Dialog.Header>

		<div class="flex flex-col gap-4 py-2">
			<Field.Field class="w-full">
				<Field.Label for="new-vehicle-no">Vehicle no</Field.Label>
				<Field.Content>
					<Input
						id="new-vehicle-no"
						bind:value={newVehicleNoInput}
						placeholder="e.g. KA01AB1234"
						autocomplete="off"
						class="font-mono uppercase"
						aria-invalid={vehicleNoValidationError || availabilityChecked === 'taken' ? true : undefined}
						aria-describedby={vehicleNoValidationError ? 'new-vehicle-no-validation' : undefined}
					/>
					{#if vehicleNoValidationError}
						<p id="new-vehicle-no-validation" class="text-xs text-destructive mt-1.5" role="alert">
							{vehicleNoValidationError}
						</p>
					{:else if normalizedNewNo}
						<p class="text-xs text-muted-foreground mt-1.5">
							Will save as:{' '}
							<span class="font-mono font-medium text-foreground">{normalizedNewNo}</span>
						</p>
					{/if}
					{#if availabilityLoading && !vehicleNoValidationError}
						<p class="text-xs text-muted-foreground flex items-center gap-1.5 mt-1.5" aria-live="polite">
							<Loader2 class="size-3 animate-spin shrink-0" />
							Checking availability…
						</p>
					{:else if normalizedNewNo && !vehicleNoValidationError && availabilityChecked === 'available'}
						<p class="text-xs text-emerald-600 dark:text-emerald-400 mt-1.5" role="status">
							This vehicle number is available.
						</p>
					{:else if normalizedNewNo && !vehicleNoValidationError && availabilityChecked === 'taken'}
						<p class="text-xs text-destructive mt-1.5" role="alert">
							This vehicle number already exists.
						</p>
					{:else if normalizedNewNo && !vehicleNoValidationError && availabilityChecked === 'error'}
						<p class="text-xs text-destructive mt-1.5" role="alert">
							Could not verify availability. Try again.
						</p>
					{/if}
				</Field.Content>
			</Field.Field>

			{#if showRespCenterSelect}
				<Field.Field class="w-full">
					<Field.Label for="new-vehicle-rc">Responsibility center</Field.Label>
					<Field.Content>
						<MasterSelect
							bind:form={addVehicleFormForMasters}
							fieldName="responsibilityCenter"
							masterType="respCenters"
							label=""
							placeholder="Search resp. center…"
							respCenterType="Sale,Payroll,Production,Purchase"
							singleSelect
						/>
					</Field.Content>
				</Field.Field>
			{:else if userRespCenterHint}
				<p class="text-xs text-muted-foreground -mt-1">
					Responsibility center:
					<span class="font-mono font-medium text-foreground">{userRespCenterHint}</span>
				</p>
			{/if}
		</div>

		<Dialog.Footer class="flex gap-2 justify-end">
			<Button type="button" variant="outline" disabled={createLoading} onclick={() => (addDialogOpen = false)}>
				Cancel
			</Button>
			<Button
				type="button"
				disabled={
					!normalizedNewNo ||
					!!vehicleNoValidationError ||
					!canCreateRespCenter ||
					availabilityLoading ||
					availabilityChecked !== 'available' ||
					createLoading
				}
				onclick={() => void createVehicle()}
				class="gap-2"
			>
				{#if createLoading}
					<Loader2 class="size-4 animate-spin" />
				{/if}
				Create
			</Button>
		</Dialog.Footer>
	</Dialog.Content>
</Dialog.Root>
