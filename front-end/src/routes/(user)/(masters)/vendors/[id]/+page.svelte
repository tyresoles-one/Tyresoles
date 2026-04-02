<script lang="ts">
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';
	import { useEntityDetail, useMutation } from '$lib/composables';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { StatusBadge } from '$lib/components/venUI/statusBadge';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Input } from '$lib/components/ui/input';
	import { Switch } from '$lib/components/ui/switch';
	import { Icon } from '$lib/components/venUI/icon';
	import { Select } from '$lib/components/venUI/select';
	import { DatePicker } from '$lib/components/venUI/date-picker';
	import { gql } from 'graphql-request';
	const GetDealerByCodeDocument: any = gql`
		query GetDealerByCode($code: String!) {
			dealerByCode(code: $code) {
				code
				name
				dealershipName
				eMail
				mobileNo
				businessModel
				product
				status
				investmentAmount
				dealershipExpDate
				dealershipStartDate
				dateOfBirth
				dateOfAniversary
				brandedShop
				panNo
				gstNo
				aadharNo
				bankName
				bankACNo
				bankBranch
				bankIFSC
			}
		}
	`;
	const UpdateDealerDocument: any = gql`
		mutation SaveDealer($input: SaveDealerInput!) {
			saveDealer(input: $input) {
				success
				message
			}
		}
	`;
	type GetDealerByCodeQuery = { dealerByCode: Record<string, any> };
	type SaveDealerInput = {
		code: string;
		name: string;
		dealershipName: string;
		eMail: string;
		mobileNo: string;
		businessModel: number;
		product: number;
		status: number;
		investmentAmount: number;
		dealershipStartDate: string;
		dealershipExpDate: string;
		dateOfBirth: string;
		dateOfAniversary: string;
		brandedShop: number;
		panNo: string;
		gstNo: string;
		aadharNo: string;
		bankName: string;
		bankACNo: string;
		bankBranch: string;
		bankIFSC: string;
	};
	type UpdateDealerInput = { input: SaveDealerInput };
	import { formatDateForDisplay, prepareDateForMutation } from '$lib/utils/date';
	import { secureFetch } from '$lib/services/api';
	import { focusManager } from '$lib/components/venUI/form/focus-manager';

	function normalizeString(value: string) {
		return value.trim().toUpperCase().replace(/\s/g, '');
	}

	/** Route `[id]`: dealer business code passed in the URL (GraphQL `code` argument). */
	const dealerId = $derived(decodeURIComponent($page.params.id ?? '').trim());

	const dealerDetail = useEntityDetail<GetDealerByCodeQuery, GetDealerByCodeQuery['dealerByCode']>({
		id: () => dealerId,
		query: GetDealerByCodeDocument,
		dataPath: 'dealerByCode',
		cacheKey: (id) => `dealer-${id}`
	});

	const dealer = $derived(dealerDetail.entity.value);

	const updateDealerMutation = useMutation<unknown, { input: SaveDealerInput }>({
		mutation: UpdateDealerDocument,
		successMessage: 'Dealer saved successfully',
		confirmTitle: 'Save dealer',
		confirmMessage: 'Are you sure you want to save changes to this dealer?',
		clearCache: (vars) => `dealer-${vars.input.code}`,
		onSuccess: async () => {
			await dealerDetail.reload();
		}
	});

	const businessModelOptions = [
		{ value: 0, label: '' },
		{ value: 1, label: 'CPA-Ecomiles' },
		{ value: 2, label: 'CNC-Ecomiles' },
		{ value: 3, label: 'CNC-Ecomiles & Rtd' },
		{ value: 4, label: 'CNC-Rtd' },
		{ value: 5, label: 'CPA-Ecomiles & Rtd on demand' }
	];

	const productOptions = [
		{ value: 0, label: '' },
		{ value: 1, label: 'Ecomile' },
		{ value: 2, label: 'Retd' }
	];

	type FormData = {
		name: string;
		dealershipName: string;
		eMail: string;
		mobileNo: string;
		businessModel: number;
		product: number;
		status: number;
		investmentAmount: number;
		dealershipExpDate: string;
		dealershipStartDate: string;
		dateOfBirth: string;
		dateOfAniversary: string;
		brandedShop: boolean;
		panNo: string;
		gstNo: string;
		aadharNo: string;
		bankName: string;
		bankACNo: string;
		bankBranch: string;
		bankIfsc: string;
	};

	let form = $state<FormData>({
		name: '',
		dealershipName: '',
		eMail: '',
		mobileNo: '',
		businessModel: 0,
		product: 0,
		status: 0,
		investmentAmount: 0,
		dealershipExpDate: '',
		dealershipStartDate: '',
		dateOfBirth: '',
		dateOfAniversary: '',
		brandedShop: false,
		panNo: '',
		gstNo: '',
		aadharNo: '',
		bankName: '',
		bankACNo: '',
		bankBranch: '',
		bankIfsc: ''
	});

	function syncFormFromDealer(d: NonNullable<typeof dealer>) {
		form = {
			name: d.name ?? '',
			dealershipName: d.dealershipName ?? '',
			eMail: d.eMail ?? '',
			mobileNo: d.mobileNo ?? '',
			businessModel: d.businessModel ?? 0,
			product: d.product ?? 0,
			status: d.status ?? 0,
			investmentAmount: Number(d.investmentAmount ?? 0),
			dealershipExpDate: formatDateForDisplay(d.dealershipExpDate),
			dealershipStartDate: formatDateForDisplay(d.dealershipStartDate),
			dateOfBirth: formatDateForDisplay(d.dateOfBirth),
			dateOfAniversary: formatDateForDisplay(d.dateOfAniversary),
			brandedShop: (d.brandedShop ?? 0) === 1,
			panNo: d.panNo ?? '',
			gstNo: d.gstNo ?? '',
			aadharNo: d.aadharNo ?? '',
			bankName: d.bankName ?? '',
			bankACNo: d.bankACNo ?? '',
			bankBranch: d.bankBranch ?? '',
			bankIfsc: d.bankIFSC ?? ''
		};
	}

	async function saveDealer() {
		if (!dealer?.code) return;
		const input: SaveDealerInput = {
			code: dealer.code,
			name: form.name,
			dealershipName: form.dealershipName,
			eMail: form.eMail,
			mobileNo: form.mobileNo,
			businessModel: Number(form.businessModel),
			product: Number(form.product),
			status: Number(form.status),
			investmentAmount: Number(form.investmentAmount),
			dealershipExpDate: prepareDateForMutation(form.dealershipExpDate),
			dealershipStartDate: prepareDateForMutation(form.dealershipStartDate),
			dateOfBirth: prepareDateForMutation(form.dateOfBirth),
			dateOfAniversary: prepareDateForMutation(form.dateOfAniversary),
			brandedShop: form.brandedShop ? 1 : 0,
			panNo: form.panNo,
			gstNo: form.gstNo,
			aadharNo: form.aadharNo,
			bankName: form.bankName,
			bankACNo: form.bankACNo,
			bankBranch: form.bankBranch,
			bankIFSC: form.bankIfsc
		};
		await updateDealerMutation.execute({ input });
	}

	function cancelEdit() {
		if (dealer) syncFormFromDealer(dealer);
	}

	/** Match FormGenerator: after VenSelect picks a value, move focus like Enter would. */
	function triggerNextFocus() {
		const trigger = document.activeElement as HTMLElement | null;
		if (trigger) {
			trigger.dispatchEvent(new CustomEvent('ven-form:next-focus', { bubbles: true }));
		}
	}

	// Auto-calculate expiry date as 1 year after start date
	$effect(() => {
		if (form.dealershipStartDate && form.dealershipStartDate !== '1753-01-01') {
			const startDate = new Date(form.dealershipStartDate);
			const expiryDate = new Date(startDate);
			expiryDate.setFullYear(expiryDate.getFullYear() + 1);
			form.dealershipExpDate = expiryDate.toISOString().slice(0, 10);
		}
	});

	// Sync form when dealer loads
	$effect(() => {
		if (dealer) syncFormFromDealer(dealer);
	});

	let gstLegalName = $state('');
	let gstLookupLoading = $state(false);	
	let gstLookupError = $state('');

	let ifscLookupLoading = $state(false);
	let ifscLookupError = $state('');

	$inspect(form);

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

	$effect(() => {
		const ifsc = normalizeString(form.bankIfsc);
		const ac = new AbortController();

		console.log(ifsc, 'ifsc');

		if (ifsc.length !== 11) {
			
			ifscLookupLoading = false;
			ifscLookupError = '';
			return;
		}

		const tid = setTimeout(async () => {
			ifscLookupLoading = true;
			ifscLookupError = '';
			
			try {
				const res = await fetch(`https://ifsc.razorpay.com/${encodeURIComponent(ifsc)}`).catch((ex)=>{
					ifscLookupError = "Could't fetch IFSC";
				}).finally(()=>{
					ifscLookupLoading = false;
				})
				
				if (ac.signal.aborted) return;
				if (typeof res !== 'object') {
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
		backHref="/dealers"
		backLabel="Back to list"
		loading={dealerDetail.loading}
		icon="store"
	>
		{#snippet title()}
			{#if dealer}
				{dealer.name || dealer.code}
			{:else}
				Dealer
			{/if}
		{/snippet}
		{#snippet description()}
			{#if dealer?.code}
				<code class="font-mono text-xs">{dealer.code}</code>
			{:else}
				Edit dealer details
			{/if}
		{/snippet}
		{#snippet actions()}
			{#if dealer}
				<StatusBadge status={dealer.status ?? 0} class="shrink-0 hidden sm:inline-flex text-xs" />
				<Button variant="outline" size="sm" onclick={cancelEdit} disabled={dealerDetail.loading || updateDealerMutation.saving}>
					Cancel
				</Button>
				<Button
					size="sm"
					onclick={saveDealer}
					disabled={dealerDetail.loading || updateDealerMutation.saving}
					class="min-w-[80px]"
				>
					{#if updateDealerMutation.saving}
						<Icon name="loader-2" class="size-4 animate-spin mr-2" />
						Saving...
					{:else}
						<Icon name="save" class="size-4 mr-2" />
						Save
					{/if}
				</Button>
			{/if}
		{/snippet}
	</PageHeading>

	<main class="container mx-auto px-4 py-6 max-w-5xl space-y-6">
		{#if !dealerId}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">No dealer ID in URL.</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/dealers')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else if dealerDetail.loading}
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
		{:else if dealerDetail.error}
			<Card class="border-destructive/30">
				<CardContent class="pt-6">
					<p class="text-sm text-destructive">{dealerDetail.error}</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => dealerDetail.reload()}>
						Retry
					</Button>
				</CardContent>
			</Card>
		{:else if !dealer}
			<Card class="border-border/40">
				<CardContent class="pt-6">
					<p class="text-sm text-muted-foreground">
						Dealer <code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{dealerId}</code> not found.
					</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/dealers')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else}
			<!-- Single Unified Card -->
			<Card class="border-border/50 shadow-lg bg-gradient-to-br from-card via-card to-card/95 backdrop-blur-sm">	
				<CardContent class="pt-6 pb-6 space-y-6">
					<form
						class="contents"
						onsubmit={(e) => e.preventDefault()}
						use:focusManager={{ autoFocus: true }}
					>
					<!-- Basic Information Section -->
					<div class="space-y-3">
						<div class="flex items-center gap-2 mb-4">
							<div class="h-px flex-1 bg-gradient-to-r from-primary/20 via-primary/40 to-transparent"></div>
							<h3 class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2">
								<Icon name="user" class="size-3.5" />
								Basic Details
							</h3>
							<div class="h-px flex-1 bg-gradient-to-l from-primary/20 via-primary/40 to-transparent"></div>
						</div>
						<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
							<div class="space-y-1.5">
								<label for="dealer-name" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="user" class="size-3" />
									Name
								</label>
								<Input
									id="dealer-name"
									bind:value={form.name}
									placeholder="Enter dealer name"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
								/>
							</div>
							<div class="space-y-1.5">
								<label for="dealer-dealership" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="building" class="size-3" />
									Dealership Name
								</label>
								<Input
									id="dealer-dealership"
									bind:value={form.dealershipName}
									placeholder="Enter dealership name"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
								/>
							</div>
							<div class="space-y-1.5">
								<label for="dealer-phone" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="phone" class="size-3" />
									Mobile Number
								</label>
								<Input
									id="dealer-phone"
									bind:value={form.mobileNo}
									placeholder="Enter mobile number"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
								/>
							</div>
							<div class="space-y-1.5">
								<label for="dealer-email" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="mail" class="size-3" />
									Email Address
								</label>
								<Input
									id="dealer-email"
									type="email"
									bind:value={form.eMail}
									placeholder="Enter email"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
								/>
							</div>
							<div class="space-y-1.5">
								<label for="dealer-status" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="activity" class="size-3" />
									Status
								</label>
								<select
									id="dealer-status"
									bind:value={form.status}
									class="flex h-9 w-full rounded-md border border-border/60 bg-background px-3 py-1 text-sm shadow-sm outline-none focus:border-primary/50 focus-visible:ring-2 focus-visible:ring-ring transition-colors"
								>
									<option value={0}>Active</option>
									<option value={1}>Inactive</option>
								</select>
							</div>
						</div>
					</div>

					<!-- Business Details Section -->
					<div class="space-y-3">
						<div class="flex items-center gap-2 mb-4">
							<div class="h-px flex-1 bg-gradient-to-r from-primary/20 via-primary/40 to-transparent"></div>
							<h3 class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2 p-4">
								<Icon name="briefcase" class="size-3.5" />
								Business Information
							</h3>
							<div class="h-px flex-1 bg-gradient-to-l from-primary/20 via-primary/40 to-transparent"></div>
						</div>
						<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="layers" class="size-3" />
									Business Model
								</label>
								<Select
									options={businessModelOptions}
									bind:value={form.businessModel}
									valueKey="value"
									labelKey="label"
									placeholder="Select model..."
									searchPlaceholder="Search..."
									class="h-9 w-full text-sm bg-background border-border/60"
									onSelect={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="package" class="size-3" />
									Product
								</label>
								<Select
									options={productOptions}
									bind:value={form.product}
									valueKey="value"
									labelKey="label"
									placeholder="Select product..."
									searchPlaceholder="Search..."
									class="h-9 w-full text-sm bg-background border-border/60"
									onSelect={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
							<div class="space-y-1.5">
								<label for="investment" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="indian-rupee" class="size-3" />
									Investment Amount
								</label>
								<Input
									id="investment"
									type="number"
									bind:value={form.investmentAmount}
									placeholder="0.00"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
									step="0.01"
								/>
							</div>
							<div class="space-y-1.5 flex items-end">
								<label class="flex items-center gap-2.5 cursor-pointer px-3 py-2 rounded-md hover:bg-accent/50 transition-colors w-full">
									<Switch bind:checked={form.brandedShop} />
									<span class="text-xs font-medium flex items-center gap-1.5">
										<Icon name="badge-check" class="size-3.5" />
										Branded Shop
									</span>
								</label>
							</div>
						</div>
					</div>

					<!-- Important Dates Section -->
					<div class="space-y-3">
						<div class="flex items-center gap-2 mb-4">
							<div class="h-px flex-1 bg-gradient-to-r from-border/40 via-border/60 to-transparent"></div>
							<h3 class="text-xs font-semibold uppercase tracking-wider text-foreground flex items-center gap-2 p-4">
								<Icon name="calendar" class="size-3.5" />
								Important Dates
							</h3>
							<div class="h-px flex-1 bg-gradient-to-l from-border/40 via-border/60 to-transparent"></div>
						</div>
						<div class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="calendar-plus" class="size-3" />
									Dealership Start
								</label>
								<DatePicker
									valueType="text"
									valueFormat="yyyy-MM-dd"
									bind:value={form.dealershipStartDate}
									placeholder="Select date..."
									onValueChange={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="calendar-x" class="size-3" />
									Dealership Expiry
								</label>
								<DatePicker
									valueType="text"
									valueFormat="yyyy-MM-dd"
									bind:value={form.dealershipExpDate}
									placeholder="Select date..."
									onValueChange={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="cake" class="size-3" />
									Date of Birth
								</label>
								<DatePicker
									valueType="text"
									valueFormat="yyyy-MM-dd"
									bind:value={form.dateOfBirth}
									placeholder="Select date..."
									onValueChange={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
							<div class="space-y-1.5">
								<label class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="heart" class="size-3" />
									Anniversary Date
								</label>
								<DatePicker
									valueType="text"
									valueFormat="yyyy-MM-dd"
									bind:value={form.dateOfAniversary}
									placeholder="Select date..."
									onValueChange={() => setTimeout(triggerNextFocus, 50)}
								/>
							</div>
						</div>
					</div>

					<!-- Tax & Bank Details Section -->
					<div class="space-y-3">
						<div class="flex items-center gap-2 mb-4">
							<div class="h-px flex-1 bg-gradient-to-r from-primary/20 via-primary/40 to-transparent"></div>
							<h3 class="text-xs font-semibold uppercase tracking-wider text-primary flex items-center gap-2 p-4">
								<Icon name="credit-card" class="size-3.5" />
								Tax & Banking
							</h3>
							<div class="h-px flex-1 bg-gradient-to-l from-primary/20 via-primary/40 to-transparent"></div>
						</div>

						<!-- Tax IDs -->
						<div class="grid gap-3 sm:grid-cols-3">
							<div class="space-y-1.5">
								<label for="dealer-gst" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="file-text" class="size-3" />
									GST Number
								</label>
								<Input
									id="dealer-gst"
									bind:value={form.gstNo}
									placeholder="GST No"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors font-mono"
									autocomplete="off"
								/>
								{#if gstLookupLoading}
									<p class="text-xs text-muted-foreground flex items-center gap-1.5" aria-live="polite">
										<Icon name="loader-2" class="size-3 animate-spin shrink-0" />
										Fetching legal name…
									</p>
								{:else if gstLookupError}
									<p class="text-xs text-destructive" aria-live="polite">{gstLookupError}</p>
								{:else if gstLegalName}
									<p class="text-xs text-muted-foreground" aria-live="polite">
										<span class="font-medium text-foreground/80">Legal name:</span>
										{gstLegalName}
									</p>
								{/if}
							</div>
							<div class="space-y-1.5">
								<label for="dealer-pan" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="id-card" class="size-3" />
									PAN Number
								</label>
								<Input
									id="dealer-pan"
									bind:value={form.panNo}
									placeholder="PAN No"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors font-mono"
								/>
							</div>
							<div class="space-y-1.5">
								<label for="dealer-aadhar" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
									<Icon name="fingerprint-pattern" class="size-3" />
									Aadhar Number
								</label>
								<Input
									id="dealer-aadhar"
									bind:value={form.aadharNo}
									placeholder="Aadhar No"
									class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors font-mono"
								/>
							</div>
						</div>

						<!-- Bank Details -->
						<div class="pt-3 mt-2 border-t border-border/30">
							<div class="grid gap-3 sm:grid-cols-2">
								<div class="space-y-1.5">
									<label for="bank-ifsc" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
										<Icon name="key" class="size-3" />
										IFSC Code
									</label>
									<Input
										id="bank-ifsc"
										bind:value={form.bankIfsc}
										placeholder="IFSC code"
										class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors font-mono"
									/>
									{#if ifscLookupLoading}
										<p class="text-xs text-muted-foreground flex items-center gap-1.5" aria-live="polite">
											<Icon name="loader-2" class="size-3 animate-spin shrink-0" />
											Fetching Bank Details…
										</p>
									{:else if ifscLookupError}
										<p class="text-xs text-destructive" aria-live="polite">{ifscLookupError}</p>
									{/if}
								</div>
								<div class="space-y-1.5">
									<label for="bank-name" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
										<Icon name="landmark" class="size-3" />
										Bank Name
									</label>
									<Input
										id="bank-name"
										bind:value={form.bankName}
										placeholder="Enter bank name"
										class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
									/>
								</div>
								<div class="space-y-1.5">
									<label for="bank-branch" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
										<Icon name="map-pin" class="size-3" />
										Bank Branch
									</label>
									<Input
										id="bank-branch"
										bind:value={form.bankBranch}
										placeholder="Enter branch"
										class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors"
									/>
								</div>
								<div class="space-y-1.5">
									<label for="bank-ac" class="text-xs font-medium text-muted-foreground flex items-center gap-1">
										<Icon name="hash" class="size-3" />
										Account Number
									</label>
									<Input
										id="bank-ac"
										bind:value={form.bankACNo}
										placeholder="Account number"
										class="h-9 text-sm bg-background border-border/60 focus:border-primary/50 transition-colors font-mono"
									/>
								</div>
								
							</div>
						</div>
					</div>
					</form>
				</CardContent>
			</Card>
		{/if}
	</main>
</div>
