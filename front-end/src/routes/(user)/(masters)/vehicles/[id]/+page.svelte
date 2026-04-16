<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { useEntityDetail, useMutation } from '$lib/composables';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import FormField from '$lib/components/venUI/form/FormField.svelte';
	import FormSectionHeader from '$lib/components/venUI/form/FormSectionHeader.svelte';
	import { focusManager } from '$lib/components/venUI/form/focus-manager';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	import { Switch } from '$lib/components/ui/switch';
	import { cn } from '$lib/utils';
	import { secureFetch } from '$lib/services/api';
	import { authStore } from '$lib/stores/auth';
	import { buildMutation } from '$lib/services/graphql';
	import type { TypedDocumentNode } from '@graphql-typed-document-node/core';

	import { GetVehicleByNoDocument } from '$lib/services/graphql/generated/types';
	import type { GetVehicleByNoQuery } from '$lib/services/graphql/generated/types';

	type VehicleDetail = NonNullable<
		NonNullable<GetVehicleByNoQuery['vehicles']>['items']
	>[number];

	type VehicleForm = {
		name: string;
		mobileNo: string;
		gstNo: string;
		lineNo: number;
		responsibilityCenter: string;
		status: number;
	};

	type VehicleSaveGqlInput = {
		no: string;
		name?: string | null;
		mobileNo?: string | null;
		gstNo?: string | null;
		lineNo: number;
		responsibilityCenter?: string | null;
		status: number;
	};

	type SaveVehicleMutation = {
		saveVehicle: { success: boolean; message: string };
	};

	function normalizeString(value: string) {
		return value.trim().toUpperCase().replace(/\s/g, '');
	}

	const SaveVehicleDocument = buildMutation`
		mutation SaveVehicle($input: VehicleSaveInput!) {
			saveVehicle(input: $input) {
				success
				message
			}
		}
	` as unknown as TypedDocumentNode<SaveVehicleMutation, { input: VehicleSaveGqlInput }>;

	/** Route `[id]`: vehicle number (NAV `no`). */
	const vehicleId = $derived(decodeURIComponent($page.params.id ?? '').trim());

	const vehicleDetail = useEntityDetail<GetVehicleByNoQuery, VehicleDetail>({
		id: () => vehicleId,
		query: GetVehicleByNoDocument,
		dataPath: 'vehicles',
		cacheKey: (id) => `vehicle-${id}`,
		variables: (id) => {
			const u = authStore.get().user;
			return {
				entityType: u?.entityType ?? null,
				entityCode: u?.entityCode ?? null,
				department: u?.department ?? null,
				respCenter: u?.respCenter ?? null,
				take: 1,
				after: null,
				where: { no: { eq: id } },
				order: null
			};
		},
		mapResponse: (data) => {
			const items = data.vehicles?.items;
			if (!items?.length) return null;
			const row = items[0];
			if (!String(row?.no ?? '').trim()) return null;
			return row;
		}
	});

	const vehicle = $derived(vehicleDetail.entity.value);

	const saveVehicleMutation = useMutation<SaveVehicleMutation, { input: VehicleSaveGqlInput }>({
		mutation: SaveVehicleDocument,
		successMessage: 'Vehicle saved successfully',
		confirmTitle: 'Save vehicle',
		confirmMessage: 'Are you sure you want to save changes to this vehicle?',
		clearCache: (vars) => `vehicle-${vars.input.no}`,
		onSuccess: async () => {
			await vehicleDetail.reload();
		}
	});

	let form = $state<VehicleForm>({
		name: '',
		mobileNo: '',
		gstNo: '',
		lineNo: 0,
		responsibilityCenter: '',
		status: 0
	});

	let fieldErrors = $state<Partial<Record<keyof VehicleForm, string>>>({});

	function syncFormFromVehicle(v: VehicleDetail) {
		form = {
			name: v.name ?? '',
			mobileNo: v.mobileNo ?? '',
			gstNo: v.gstNo ?? '',
			lineNo: typeof v.lineNo === 'number' ? v.lineNo : 0,
			responsibilityCenter: v.responsibilityCenter ?? '',
			status: typeof v.status === 'number' ? v.status : 0
		};
		fieldErrors = {};
	}

	function validateVehicleForm(): boolean {
		const errors: Partial<Record<keyof VehicleForm, string>> = {};
		if (!form.name.trim()) errors.name = 'Name is required.';
		fieldErrors = errors;
		return Object.keys(errors).length === 0;
	}

	async function saveVehicle() {
		if (!vehicleId) return;
		if (!validateVehicleForm()) return;
		await saveVehicleMutation.execute({
			input: {
				no: vehicleId,
				name: form.name.trim(),
				mobileNo: form.mobileNo.trim(),
				gstNo: form.gstNo.trim(),
				lineNo: form.lineNo,
				responsibilityCenter: form.responsibilityCenter.trim(),
				status: form.status
			}
		});
	}

	function cancelEdit() {
		if (vehicle) syncFormFromVehicle(vehicle);
	}

	$effect(() => {
		if (vehicle) syncFormFromVehicle(vehicle);
	});

	let gstLegalName = $state('');
	let gstLookupLoading = $state(false);
	let gstLookupError = $state('');

	$effect(() => {
		const gstin = normalizeString(form.gstNo);
		const ac = new AbortController();

		if (gstin.length !== 15) {
			gstLegalName = '';
			gstLookupLoading = false;
			gstLookupError = '';
			return;
		}

		const tid = setTimeout(async () => {
			gstLookupLoading = true;
			gstLookupError = '';
			gstLegalName = '';
			try {
				const res = await secureFetch(`/api/protean/gstin/${encodeURIComponent(gstin)}`, {
					signal: ac.signal
				});
				if (ac.signal.aborted) return;
				if (!res.ok) {
					let msg = 'Could not verify GSTIN';
					try {
						const err = await res.json();
						if (err && typeof err.error === 'string') msg = err.error;
					} catch {
						/* ignore */
					}
					gstLookupError = msg;
					return;
				}
				const data = (await res.json()) as { legalName?: string };
				if (ac.signal.aborted) return;
				gstLegalName = data.legalName?.trim() ?? '';
				gstLookupError = '';
			} catch (e) {
				if (ac.signal.aborted || (e instanceof DOMException && e.name === 'AbortError')) return;
				gstLookupError = e instanceof Error ? e.message : 'GST lookup failed';
				gstLegalName = '';
			} finally {
				if (!ac.signal.aborted) gstLookupLoading = false;
			}
		}, 400);

		return () => {
			clearTimeout(tid);
			ac.abort();
		};
	});

	/** Bridge for MasterSelect (`form.values` + `setTouched`). */
	let vehicleFormForMasters = $state.raw({
		get values() {
			return form as unknown as Record<string, unknown>;
		},
		setTouched: (_name: string) => {},
		get errors() {
			return fieldErrors as Record<string, string | undefined>;
		}
	});
</script>

<div class="min-h-screen bg-background pb-12">
	<PageHeading
		backHref="/vehicles"
		backLabel="Back to list"
		loading={vehicleDetail.loading}
		icon="truck"
	>
		{#snippet title()}
			{#if vehicle}
				{vehicle.name || vehicle.no || 'Vehicle'}
			{:else}
				Vehicle
			{/if}
		{/snippet}
		{#snippet description()}
			{#if vehicle?.no}
				<code class="font-mono text-xs">{vehicle.no}</code>
			{:else}
				Edit vehicle details
			{/if}
		{/snippet}
		{#snippet actions()}
			{#if vehicle}
				<div class="flex items-center gap-2 flex-wrap justify-end">
					<Button
						variant="outline"
						size="sm"
						onclick={cancelEdit}
						disabled={vehicleDetail.loading || saveVehicleMutation.saving}
					>
						Cancel
					</Button>
					<Button
						size="sm"
						onclick={() => void saveVehicle()}
						disabled={vehicleDetail.loading || saveVehicleMutation.saving}
						class="min-w-[80px]"
					>
						{#if saveVehicleMutation.saving}
							<Icon name="loader-2" class="size-4 animate-spin mr-2" />
							Saving...
						{:else}
							<Icon name="save" class="size-4 mr-2" />
							Save
						{/if}
					</Button>
				</div>
			{/if}
		{/snippet}
	</PageHeading>

	<main class="container mx-auto px-4 py-6 max-w-5xl space-y-6">
		{#if !vehicleId}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">No vehicle number in URL.</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/vehmaster')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else if vehicleDetail.loading}
			<Card class="border-border/40">
				<CardContent class="space-y-3 pt-8">
					<Skeleton class="h-6 w-64" />
					<Skeleton class="h-4 w-full" />
					<Skeleton class="h-4 w-3/4" />
					<Skeleton class="h-4 w-1/2" />
				</CardContent>
			</Card>
		{:else if vehicleDetail.error}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-destructive">{vehicleDetail.error}</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => vehicleDetail.reload()}>
						Retry
					</Button>
				</CardContent>
			</Card>
		{:else if !vehicle}
			<Card class="border-border/40">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">
						Vehicle
						<code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{vehicleId}</code>
						not found or not in your scope.
					</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/vehmaster')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else}
			<Card
				class="border-border/50 shadow-lg bg-gradient-to-br from-card via-card to-card/95 backdrop-blur-sm overflow-hidden"
			>
				<CardContent class="pb-8 pt-8">
					<form
						class="contents"
						onsubmit={(e) => e.preventDefault()}
						use:focusManager={{ autoFocus: true }}
					>
						<div class="space-y-10">
							<div class="space-y-6">
								<FormSectionHeader title="Identification" icon="hash" />
								<div class="grid gap-6 sm:grid-cols-2">
									<FormField id="vehicle-no" label="Vehicle no" icon="hash">
										<Input
											id="vehicle-no"
											readonly
											class="h-10 bg-muted/50 font-mono"
											value={vehicle.no ?? ''}
										/>
									</FormField>
								</div>
							</div>

							<div class="space-y-6">
								<FormSectionHeader title="Vehicle details" icon="truck" />
								<div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
									<FormField id="vehicle-name" label="Name" icon="user" required error={fieldErrors.name}>
										<Input
											id="vehicle-name"
											bind:value={form.name}
											placeholder="Transporter name"
											class="h-10 bg-background/50 border-border/60 focus-visible:ring-primary"
										/>
									</FormField>
									<FormField id="vehicle-mobile" label="Mobile" icon="phone">
										<Input
											id="vehicle-mobile"
											bind:value={form.mobileNo}
											placeholder="Mobile number"
											class="h-10 bg-background/50 border-border/60 focus-visible:ring-primary"
										/>
									</FormField>
									<FormField id="vehicle-gst" label="GST no" icon="file-text">
										<Input
											id="vehicle-gst"
											bind:value={form.gstNo}
											placeholder="GSTIN"
											autocomplete="off"
											class={cn(
												'h-10 bg-background/50 border-border/60 focus-visible:ring-primary font-mono text-sm uppercase',
												gstLookupError && 'border-destructive'
											)}
										/>
										{#if gstLookupLoading}
											<p class="text-xs text-muted-foreground flex items-center gap-1.5 mt-1" aria-live="polite">
												<Icon name="loader-2" class="size-3 animate-spin shrink-0" />
												Fetching legal name…
											</p>
										{:else if gstLookupError}
											<p class="text-xs text-destructive mt-1" aria-live="polite">{gstLookupError}</p>
										{:else if gstLegalName}
											<p class="text-xs text-muted-foreground mt-1" aria-live="polite">
												<span class="font-medium text-foreground/80">Legal name:</span>
												{gstLegalName}
											</p>
										{/if}
									</FormField>
									<FormField id="vehicle-rc" label="Responsibility center" icon="building-2">
										<MasterSelect
											bind:form={vehicleFormForMasters}
											fieldName="responsibilityCenter"
											masterType="respCenters"
											label=""
											placeholder="Search resp. center…"
											respCenterType="Sale,Payroll,Production,Purchase"
											singleSelect
										/>
									</FormField>
									<FormField id="vehicle-line" label="Line no" icon="layers">
										<Input
											id="vehicle-line"
											type="number"
											readonly
											tabindex={-1}
											value={String(form.lineNo)}
											class="h-10 bg-muted/50 border-border/60 tabular-nums cursor-default"
										/>
									</FormField>
									<FormField id="vehicle-status" label="Status" icon="toggle-left">
										<div class="flex items-center gap-3 rounded-lg border border-border/60 bg-background/50 px-3 py-2 h-10">
											<Switch
												checked={form.status === 0}
												onCheckedChange={(checked) => {
													form.status = checked ? 0 : 1;
												}}
											/>
											<span class="text-sm font-medium text-foreground">
												{form.status === 0 ? 'Active' : 'Inactive'}
											</span>
										</div>
									</FormField>
								</div>
							</div>
						</div>
					</form>
				</CardContent>
			</Card>
		{/if}
	</main>
</div>
