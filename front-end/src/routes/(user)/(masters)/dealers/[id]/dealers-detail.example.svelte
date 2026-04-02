<script lang="ts">
	/**
	 * EXAMPLE: Dealer Detail Page - Refactored with Reusable Components
	 *
	 * Route: dealers/[id] — `id` is the dealer business code used for GraphQL lookup.
	 */
	import { page } from '$app/stores';
	import { goto } from '$app/navigation';

	// Reusable Composables
	import { useEntityDetail, useMutation } from '$lib/composables';

	// Reusable Components
	import { StatusBadge } from '$lib/components/venUI/statusBadge';

	// UI Components
	import { Button } from '$lib/components/ui/button';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Input } from '$lib/components/ui/input';
	import { Switch } from '$lib/components/ui/switch';
	import { Icon } from '$lib/components/venUI/icon';
	import { Select } from '$lib/components/venUI/select';
	import { DatePicker } from '$lib/components/venUI/date-picker';

	// GraphQL
	import {
		GetDealerByCodeDocument,
		UpdateDealerDocument
	} from '$lib/services/graphql/generated/types';
	import type {
		GetDealerByCodeQuery,
		UpdateDealerInput
	} from '$lib/services/graphql/generated/types';

	// Utilities
	import { formatDateForDisplay, prepareDateForMutation } from '$lib/utils/date';

	const dealerId = $derived(decodeURIComponent($page.params.id ?? '').trim());

	// Use entity detail composable (eliminates ~40 lines)
	const dealerDetail = useEntityDetail<GetDealerByCodeQuery, GetDealerByCodeQuery['dealerByCode']>({
		id: () => dealerId,
		query: GetDealerByCodeDocument,
		dataPath: 'dealerByCode',
		cacheKey: (id) => `dealer-${id}`
	});

	const dealer = $derived(dealerDetail.entity.value);

	// Use mutation composable (eliminates ~35 lines of save boilerplate)
	const updateDealerMutation = useMutation<any, { input: UpdateDealerInput }>({
		mutation: UpdateDealerDocument,
		successMessage: 'Dealer updated successfully',
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
			mobileNo: d.phoneNo ?? '',
			businessModel: d.businessModel ?? 0,
			product: d.product ?? 0,
			status: d.status ?? 0,
			investmentAmount: Number(d.investmentAmount ?? 0),
			dealershipExpDate: formatDateForDisplay(d.dealershipExpDate), // Using utility
			dealershipStartDate: formatDateForDisplay(d.dealershipStartDate), // Using utility
			dateOfBirth: formatDateForDisplay(d.dateOfBirth), // Using utility
			dateOfAniversary: formatDateForDisplay(d.dateOfAniversary), // Using utility
			brandedShop: (d.brandedShop ?? 0) === 1,
			panNo: d.panNo ?? '',
			gstNo: d.gstNo ?? '',
			aadharNo: d.aadharNo ?? '',
			bankName: d.bankName ?? '',
			bankACNo: d.bankACNo ?? '',
			bankBranch: d.bankBranch ?? '',
			bankIfsc: d.bankIfsc ?? ''
		};
	}

	async function saveDealer() {
		if (!dealer?.code) return;

		const input: UpdateDealerInput = {
			code: dealer.code,
			name: form.name,
			dealershipName: form.dealershipName,
			eMail: form.eMail,
			mobileNo: form.mobileNo,
			businessModel: Number(form.businessModel),
			product: Number(form.product),
			status: Number(form.status),
			investmentAmount: Number(form.investmentAmount),
			dealershipExpDate: prepareDateForMutation(form.dealershipExpDate), // Using utility
			dealershipStartDate: prepareDateForMutation(form.dealershipStartDate), // Using utility
			dateOfBirth: prepareDateForMutation(form.dateOfBirth), // Using utility
			dateOfAniversary: prepareDateForMutation(form.dateOfAniversary), // Using utility
			brandedShop: form.brandedShop ? 1 : 0,
			panNo: form.panNo,
			gstNo: form.gstNo,
			aadharNo: form.aadharNo,
			bankName: form.bankName,
			bankACNo: form.bankACNo,
			bankBranch: form.bankBranch,
			bankIfsc: form.bankIfsc
		};

		// Using mutation composable (no manual toast/error handling needed)
		await updateDealerMutation.execute({ input });
	}

	function cancelEdit() {
		if (dealer) syncFormFromDealer(dealer);
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
</script>

<div class="min-h-screen bg-background pb-12">
	<!-- Sticky Header -->
	<header class="sticky top-0 z-40 w-full border-b bg-background/80 backdrop-blur-xl">
		<div class="container mx-auto px-4 py-3 flex items-center justify-between">
			<div class="flex items-center gap-3">
				<Button variant="ghost" size="icon" onclick={() => goto('/dealers')} class="shrink-0 size-8">
					<Icon name="arrow-left" class="size-4" />
				</Button>
				<div>
					<h1 class="text-lg font-bold tracking-tight">
						{#if dealerDetail.loading}
							<Skeleton class="h-6 w-48" />
						{:else if dealer}
							{dealer.name || dealer.code}
						{:else}
							Dealer
						{/if}
					</h1>
					<p class="text-xs text-muted-foreground hidden sm:block">
						{#if dealer?.code}
							<code class="font-mono text-xs">{dealer.code}</code>
						{:else}
							Edit dealer details
						{/if}
					</p>
				</div>
			</div>
			<div class="flex items-center gap-2">
				{#if dealer}
					<!-- Using StatusBadge component -->
					<StatusBadge status={dealer.status} class="shrink-0 hidden sm:inline-flex text-xs" />
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
			</div>
		</div>
	</header>

	<main class="container mx-auto px-4 py-4 max-w-5xl space-y-3">
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
						Dealer <code class="rounded bg-muted px-1.5 py-0.5 font-mono text-xs">{dealerId}</code> not
						found.
					</p>
					<Button variant="outline" size="sm" class="mt-3" onclick={() => goto('/dealers')}>
						Back to list
					</Button>
				</CardContent>
			</Card>
		{:else}
			<!-- Form content remains the same -->
			<Card class="border-border/50 shadow-lg bg-gradient-to-br from-card via-card to-card/95">
				<CardHeader
					class="pb-6 pt-6 border-b border-border/40 bg-gradient-to-r from-primary/5 via-transparent to-transparent"
				>
					<div class="flex items-center gap-3">
						<div
							class="shrink-0 flex size-10 items-center justify-center rounded-xl bg-gradient-to-br from-primary to-primary/80 text-primary-foreground shadow-md"
						>
							<Icon name="store" class="size-5" />
						</div>
						<div class="min-w-0 flex-1">
							<CardTitle class="text-lg font-bold">Dealer Information</CardTitle>
							<CardDescription class="text-xs"
								>Complete dealer profile and business details</CardDescription
							>
						</div>
					</div>
				</CardHeader>

				<CardContent class="pt-6 pb-6 space-y-6">
					<!-- Rest of the form fields remain the same as current implementation -->
					<!-- This example focuses on showing the composable usage -->
				</CardContent>
			</Card>
		{/if}
	</main>
</div>
