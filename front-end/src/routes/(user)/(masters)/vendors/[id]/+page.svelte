<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { useEntityDetail, useMutation } from '$lib/composables';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Input } from '$lib/components/ui/input';
	import { Switch } from '$lib/components/ui/switch';
	import { Icon } from '$lib/components/venUI/icon';
	
    import FormField from '$lib/components/venUI/form/FormField.svelte';
	import FormSectionHeader from '$lib/components/venUI/form/FormSectionHeader.svelte';
	import MasterSelect from '$lib/components/venUI/master-select/MasterSelect.svelte';
	
	import { secureFetch } from '$lib/services/api';
	import { focusManager } from '$lib/components/venUI/form/focus-manager';
	import {
		GetVendorByCodeDocument,
		UpdateProductionVendorDocument,
		type GetVendorByCodeQuery
	} from '$lib/services/graphql/generated/types';

	type VendorModel = {
		no: string;
		name: string;
		address: string;
		address2: string;
		city: string;
		category: string;
		detail: string;
		respCenter: string;
		mobileNo: string;
		ecoMgrCode: string;
		nameOnInvoice: string;
		bankName: string;
		bankIFSC: string;
		bankAccNo: string;
		postCode: string;
		bankBranch: string;
		selfInvoice: boolean;
		panNo: string;
		adhaarNo: string;
		gstRegistrationNo: string;
		balance: number;
		stateCode: string;
	};

	type UpdateProductionVendorInput = { param: VendorModel };

	/** Maps `Query.vendorByCode` (NAV `Vendor`) to `VendorModelInput` / form fields. */
	function mapVendorRowToModel(
		v: NonNullable<GetVendorByCodeQuery['vendorByCode']>
	): VendorModel {
		const bal = v.balance;
		const balanceNum =
			typeof bal === 'number' ? bal : bal != null && bal !== '' ? Number(bal) : NaN;
		return {
			no: v.no ?? '',
			name: v.name ?? '',
			address: v.address ?? '',
			address2: v.address2 ?? '',
			city: v.city ?? '',
			category: v.groupCategory ?? '',
			detail: v.groupDetails ?? '',
			respCenter: v.responsibilityCenter ?? '',
			mobileNo: v.phoneNo ?? '',
			ecoMgrCode: v.ecomileProcMgr ?? '',
			nameOnInvoice: v.nameOnInvoice ?? '',
			bankName: v.bankName ?? '',
			bankIFSC: v.bankIFSCCode ?? '',
			bankAccNo: v.bankACNo ?? '',
			postCode: v.postCode ?? '',
			bankBranch: v.bankBranch ?? '',
			selfInvoice: !!v.selfInvoice,
			panNo: v.panNo ?? '',
			adhaarNo: v.adhaarNo ?? '',
			gstRegistrationNo: v.gstRegistrationNo ?? '',
			balance: Number.isFinite(balanceNum) ? balanceNum : 0,
			stateCode: v.stateCode ?? ''
		};
	}

	function normalizeString(value: string) {
		return value.trim().toUpperCase().replace(/\s/g, '');
	}

	/** Route `[id]`: vendor number passed in the URL. */
	const vendorId = $derived(decodeURIComponent($page.params.id ?? '').trim());

	const vendorDetail = useEntityDetail<GetVendorByCodeQuery, VendorModel>({
		id: () => vendorId,
		query: GetVendorByCodeDocument,
		variables: (id) => ({ code: id }),
		dataPath: 'vendorByCode',
		mapResponse: (data) => {
			const row = data.vendorByCode;
			if (!row || !String(row.no ?? '').trim()) return null;
			return mapVendorRowToModel(row);
		},
		cacheKey: (id) => `vendor-${id}`
	});

	const vendor = $derived(vendorDetail.entity.value);

	const updateVendorMutation = useMutation<unknown, UpdateProductionVendorInput>({
		mutation: UpdateProductionVendorDocument,
		successMessage: 'Vendor saved successfully',
		confirmTitle: 'Save vendor',
		confirmMessage: 'Are you sure you want to save changes to this vendor?',
		clearCache: (vars) => `vendor-${vars.param.no}`,
		onSuccess: async () => {
			await vendorDetail.reload();
		}
	});

	type FormData = Omit<VendorModel, 'balance'>;

	let form = $state<FormData>({
		no: '',
		name: '',
		address: '',
		address2: '',
		city: '',
		category: '',
		detail: '',
		respCenter: '',
		mobileNo: '',
		ecoMgrCode: '',
		nameOnInvoice: '',
		bankName: '',
		bankIFSC: '',
		bankAccNo: '',
		postCode: '',
		bankBranch: '',
		selfInvoice: false,
		panNo: '',
		adhaarNo: '',
		gstRegistrationNo: '',
		stateCode: ''
	});

	let fieldErrors = $state<Partial<Record<keyof FormData, string>>>({});

	/** Stable reference for MasterSelect `bind:form` (avoid inline `{ values: form }` each render). */
	let vendorMasterForm = $state.raw({
		get values() {
			return form;
		},
		setTouched(_n: string) {}
	});

	function syncFormFromVendor(v: VendorModel) {
		form = {
			no: v.no || '',
			name: v.name || '',
			address: v.address || '',
			address2: v.address2 || '',
			city: v.city || '',
			category: v.category || '',
			detail: v.detail || '',
			respCenter: v.respCenter || '',
			mobileNo: v.mobileNo || '',
			ecoMgrCode: v.ecoMgrCode || '',
			nameOnInvoice: v.nameOnInvoice || '',
			bankName: v.bankName || '',
			bankIFSC: v.bankIFSC || '',
			bankAccNo: v.bankAccNo || '',
			postCode: v.postCode || '',
			bankBranch: v.bankBranch || '',
			selfInvoice: !!v.selfInvoice,
			panNo: v.panNo || '',
			adhaarNo: v.adhaarNo || '',
			gstRegistrationNo: v.gstRegistrationNo || '',
			stateCode: v.stateCode || ''
		};
        fieldErrors = {};
	}

    function validateVendorForm(): boolean {
        const errors: Partial<Record<keyof FormData, string>> = {};
        if (!form.name.trim()) errors.name = 'Name is required.';
        if (!form.mobileNo.trim()) errors.mobileNo = 'Mobile number is required.';
        if (form.bankIFSC.trim() && form.bankIFSC.trim().length !== 11) errors.bankIFSC = 'IFSC must be 11 characters.';
        
        fieldErrors = errors;
        return Object.keys(errors).length === 0;
    }

	function formatVendorBalanceInr(value: number): string {
		return value.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
	}

	async function saveVendor() {
		if (!vendorId) return;
        if (!validateVendorForm()) return;

		await updateVendorMutation.execute({ param: { ...form, no: vendorId, balance: vendor?.balance || 0 } });
	}

	function cancelEdit() {
		if (vendor) syncFormFromVendor(vendor);
	}

	/** NAV Post Code row: sync PIN + city + state when picked from either City or Post code MasterSelect. */
	function onPostCodePicked(d: { value: string; meta?: Record<string, unknown> }) {
		const city = typeof d.meta?.city === 'string' ? d.meta.city.trim() : '';
		const st = typeof d.meta?.stateCode === 'string' ? d.meta.stateCode.trim() : '';
		if (city) form.city = city;
		if (st) form.stateCode = st;
	}

	function triggerNextFocus() {
		const trigger = document.activeElement as HTMLElement | null;
		if (trigger) {
			trigger.dispatchEvent(new CustomEvent('ven-form:next-focus', { bubbles: true }));
		}
	}	

	// Sync form when vendor loads
	$effect(() => {
		if (vendor) syncFormFromVendor(vendor);
	});

	let ifscLookupLoading = $state(false);
	let ifscLookupError = $state('');

	let gstLegalName = $state('');
	let gstLookupLoading = $state(false);	
	let gstLookupError = $state('');

	$effect(() => {
		const gstin = normalizeString(form.gstRegistrationNo);
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

	$effect(() => {
		const ifsc = normalizeString(form.bankIFSC);
		const ac = new AbortController();

		if (ifsc.length !== 11) {
			ifscLookupLoading = false;
			ifscLookupError = '';
			return;
		}

		const tid = setTimeout(async () => {
			ifscLookupLoading = true;
			ifscLookupError = '';
			
			try {
				const res = await fetch(`https://ifsc.razorpay.com/${encodeURIComponent(ifsc)}`).catch(() => {
					ifscLookupError = "Could't fetch IFSC";
				}).finally(() => {
					ifscLookupLoading = false;
				})
				
				if (ac.signal.aborted) return;
				if (typeof res !== 'object' || !res) {
					ifscLookupError ="Could't fetch IFSC details.";
					return;
				} 
				const data = (await res.json()) as { BANK?: string, BRANCH?: string };
				if (ac.signal.aborted) return;				
				form.bankBranch = data.BRANCH ?? "";
				form.bankName = data.BANK ?? "";
				ifscLookupError = '';
			} catch (e) {
				if (ac.signal.aborted || (e instanceof DOMException && e.name === 'AbortError')) return;
				ifscLookupError = e instanceof Error ? e.message : 'IFSC lookup failed';
			} finally {
				if (!ac.signal.aborted) ifscLookupLoading = false;
			}
		}, 400);

		return () => {
			clearTimeout(tid);
			ac.abort();
		};
	});
</script>

<div class="min-h-screen bg-background pb-12">
	<PageHeading
		backHref="/vendors"
		backLabel="Back to list"
		loading={vendorDetail.loading}
		icon="truck"
	>
		{#snippet title()}
			{#if vendor}
				{vendor.name || vendor.no}
			{:else}
				Vendor
			{/if}
		{/snippet}
		{#snippet description()}
			{#if vendor?.no}
				<code class="font-mono text-xs">{vendor.no}</code>
			{:else}
				Edit vendor details
			{/if}
		{/snippet}
		{#snippet actions()}
			{#if vendor}
				<div class="flex items-center gap-2 flex-wrap justify-end">
					<div
						class="px-2.5 py-0.5 rounded-full border text-[10px] font-semibold uppercase tracking-wider inline-flex items-center gap-1 tabular-nums {vendor.balance < 0
							? 'bg-destructive/10 text-destructive border-destructive/25'
							: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20'}"
					>
						Balance: ₹{formatVendorBalanceInr(vendor.balance)}
					</div>
					<Button variant="outline" size="sm" onclick={cancelEdit} disabled={vendorDetail.loading || updateVendorMutation.saving}>
						Cancel
					</Button>
					<Button
						size="sm"
						onclick={saveVendor}
						disabled={vendorDetail.loading || updateVendorMutation.saving}
						class="min-w-[80px]"
					>
						{#if updateVendorMutation.saving}
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
		{#if !vendorId}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">No vendor ID in URL.</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/vendors')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else if vendorDetail.loading}
			<Card class="border-border/40">
				<CardHeader>
					<Skeleton class="h-6 w-64" />
				</CardHeader>
				<CardContent class="space-y-3">
					<Skeleton class="h-4 w-full" />
					<Skeleton class="h-4 w-3/4" />
					<Skeleton class="h-4 w-1/2" />
				</CardContent>
			</Card>
		{:else if vendorDetail.error}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-destructive">{vendorDetail.error}</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => vendorDetail.reload()}>
						Retry
					</Button>
				</CardContent>
			</Card>
		{:else if !vendor}
			<Card class="border-border/40">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">
						Vendor <code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{vendorId}</code> not found.
					</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/vendors')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else}
			<Card class="border-border/50 shadow-lg bg-gradient-to-br from-card via-card to-card/95 backdrop-blur-sm overflow-hidden">				
				<CardContent class=" pb-8 space-y-10">
					<form
						class="contents"
						onsubmit={(e) => e.preventDefault()}
						use:focusManager={{ autoFocus: true }}
					>
						<!-- Basic Information Section -->
						<div class="space-y-6">
							<FormSectionHeader title="Basic Details" icon="user" />
							
							<div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
								<FormField id="vendor-name" label="Vendor Name" icon="user" required error={fieldErrors.name}>
									<Input
										id="vendor-name"
										bind:value={form.name}
										placeholder="Enter vendor name"
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
									/>
								</FormField>
								<FormField id="vendor-mobile" label="Mobile Number" icon="phone" required error={fieldErrors.mobileNo}>
									<Input
										id="vendor-mobile"
										bind:value={form.mobileNo}
										placeholder="Enter mobile number"
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
									/>
								</FormField>								
							</div>
						</div>

						<!-- Address Section -->
						<div class="space-y-6 mt-4">
							<FormSectionHeader title="Address Details" icon="map-pin" />
							<div class="grid gap-6 sm:grid-cols-2">
								<FormField id="vendor-addr1" label="Address Line 1" icon="map">
									<Input
										id="vendor-addr1"
										bind:value={form.address}
										placeholder="Building, street..."
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
									/>
								</FormField>
								<FormField id="vendor-addr2" label="Address Line 2" icon="map">
									<Input
										id="vendor-addr2"
										bind:value={form.address2}
										placeholder="Area, landmark..."
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
									/>
								</FormField>
								<div class="grid grid-cols-2 gap-4">
									<FormField id="vendor-city" label="City" icon="building-2">
										<MasterSelect
											bind:form={vendorMasterForm}
											fieldName="postCode"
											masterType="postCodes"
											label=""
											placeholder="Search city or PIN…"
											singleSelect
											onPicked={onPostCodePicked}
										/>
									</FormField>
									<FormField id="vendor-pincode" label="Post Code" icon="hash">
										<MasterSelect
											bind:form={vendorMasterForm}
											fieldName="postCode"
											masterType="postCodes"
											label=""
											placeholder="Search PIN / city…"
											singleSelect
											onPicked={onPostCodePicked}
										/>
									</FormField>
								</div>
								<FormField id="vendor-state" label="State Code" icon="flag">
									<MasterSelect
										bind:form={vendorMasterForm}
										fieldName="stateCode"
										masterType="states"
										label=""
										placeholder="Search state code or name…"
										singleSelect
									/>
								</FormField>
							</div>
						</div>

						<!-- Banking & Tax Section -->
						<div class="space-y-6 mt-4">
							<FormSectionHeader title="Tax & Banking" icon="credit-card" />
							<div class="grid gap-6 sm:grid-cols-4">
								<FormField id="vendor-gst" label="GST Number" icon="file-text" error={gstLookupError}>
									<Input
										id="vendor-gst"
										bind:value={form.gstRegistrationNo}
										placeholder="GST No"
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50 font-mono text-sm uppercase"
										autocomplete="off"
									/>
									{#if gstLookupLoading}
										<p class="text-[10px] text-muted-foreground flex items-center gap-1.5 mt-1" aria-live="polite">
											<Icon name="loader-2" class="size-3 animate-spin shrink-0" />
											Fetching legal name…
										</p>
									{:else if gstLegalName}
										<p class="text-[10px] text-muted-foreground mt-1" aria-live="polite">
											<span class="font-medium text-foreground/80">Legal name:</span>
											{gstLegalName}
										</p>
									{/if}
								</FormField>
								<FormField id="vendor-pan" label="PAN Number" icon="id-card">
									<Input
										id="vendor-pan"
										bind:value={form.panNo}
										placeholder="PAN No"
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50 font-mono text-sm uppercase"
									/>
								</FormField>
								<FormField id="vendor-aadhar" label="Aadhar Number" icon="fingerprint">
									<Input
										id="vendor-aadhar"
										bind:value={form.adhaarNo}
										placeholder="Aadhar No"
										class="h-10 bg-background/50 border-border/60 focus:border-primary/50 font-mono text-sm"
									/>
								</FormField>
								<div class="flex items-end pb-1.5">
									<label class="flex items-center gap-3 cursor-pointer px-4 py-2.5 rounded-lg hover:bg-accent/50 transition-all border border-transparent hover:border-border/40 w-full group">
										<Switch bind:checked={form.selfInvoice} />
										<span class="text-xs font-semibold text-muted-foreground group-hover:text-foreground transition-colors flex items-center gap-2">
											<Icon name="file-check" class="size-3.5" />
											Self Invoice
										</span>
									</label>
								</div>
							</div>

							<div class="pt-6 mt-4 border-t border-border/30">
								<div class="grid gap-6 sm:grid-cols-2">
									<FormField id="bank-ifsc" label="IFSC Code" icon="key" error={ifscLookupError || fieldErrors.bankIFSC}>
										<div class="relative">
											<Input
												id="bank-ifsc"
												bind:value={form.bankIFSC}
												placeholder="IFSC code"
												class="h-10 bg-background/50 border-border/60 focus:border-primary/50 font-mono uppercase pr-10"
											/>
											{#if ifscLookupLoading}
												<div class="absolute right-3 top-1/2 -translate-y-1/2">
													<Icon name="loader-2" class="size-4 animate-spin text-primary" />
												</div>
											{/if}
										</div>
									</FormField>
									<FormField id="bank-name" label="Bank Name" icon="landmark">
										<Input
											id="bank-name"
											bind:value={form.bankName}
											placeholder="Bank name"
											class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
										/>
									</FormField>
									<FormField id="bank-branch" label="Bank Branch" icon="map-pin">
										<Input
											id="bank-branch"
											bind:value={form.bankBranch}
											placeholder="Branch name"
											class="h-10 bg-background/50 border-border/60 focus:border-primary/50"
										/>
									</FormField>
									<FormField id="bank-ac" label="Account Number" icon="hash">
										<Input
											id="bank-ac"
											bind:value={form.bankAccNo}
											placeholder="Account number"
											class="h-10 bg-background/50 border-border/60 focus:border-primary/50 font-mono"
										/>
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
