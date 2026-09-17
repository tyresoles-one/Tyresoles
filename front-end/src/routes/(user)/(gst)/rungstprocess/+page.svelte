<script lang="ts">
	import { onMount } from 'svelte';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '$lib/components/ui/card';
	import { Button } from '$lib/components/ui/button';
	import { Icon } from '$lib/components/venUI/icon';
	import { secureFetch } from '$lib/services/api';
	import { slide } from 'svelte/transition';

	type ProcessResult = {
		status: string;
		processed: number;
		errors: number;
		message?: string;
	};

	type ClearAndRunResult = {
		status: string;
		totalCleared: number;
		clearedInvoices: string[];
		clearedCrMemos: string[];
		processed: number;
		errors: number;
		message?: string;
	};

	type SkippedSummary = {
		totalSkipped: number;
		invoices: string[];
		crMemos: string[];
	};

	let einvLoading = $state(false);
	let clearAndRunLoading = $state(false);
	let clearOnlyLoading = $state(false);
	let ewbLoading = $state(false);

	let einvResult = $state<ProcessResult | null>(null);
	let clearAndRunResult = $state<ClearAndRunResult | null>(null);
	let ewbResult = $state<ProcessResult | null>(null);

	let einvError = $state<string | null>(null);
	let clearError = $state<string | null>(null);
	let ewbError = $state<string | null>(null);

	let skippedInfo = $state<SkippedSummary | null>(null);
	let checkingSkipped = $state(false);
	let showSkippedList = $state(false);

	async function fetchSkippedSummary() {
		checkingSkipped = true;
		try {
			const res = await secureFetch('/api/protean/einv-errors');
			if (res.ok) {
				skippedInfo = await res.json();
			}
		} catch (e) {
			console.error('Failed to load skipped e-invoice count', e);
		} finally {
			checkingSkipped = false;
		}
	}

	onMount(() => {
		fetchSkippedSummary();
	});

	async function runEInv() {
		einvLoading = true;
		einvError = null;
		einvResult = null;
		try {
			const res = await secureFetch('/api/protean/run-einv', { method: 'POST' });
			if (!res.ok) throw new Error(await res.text() || 'Failed to process E-Invoices');
			einvResult = await res.json();
			await fetchSkippedSummary();
		} catch (e: any) {
			einvError = e.message;
		} finally {
			einvLoading = false;
		}
	}

	async function autoClearErrorsAndRun() {
		clearAndRunLoading = true;
		clearError = null;
		clearAndRunResult = null;
		einvResult = null;
		try {
			const res = await secureFetch('/api/protean/clear-and-run-einv', { method: 'POST' });
			if (!res.ok) throw new Error(await res.text() || 'Failed to clear errors and process E-Invoices');
			clearAndRunResult = await res.json();
			await fetchSkippedSummary();
		} catch (e: any) {
			clearError = e.message;
		} finally {
			clearAndRunLoading = false;
		}
	}

	async function clearErrorsOnly() {
		clearOnlyLoading = true;
		clearError = null;
		try {
			const res = await secureFetch('/api/protean/clear-einv-errors', { method: 'POST' });
			if (!res.ok) throw new Error(await res.text() || 'Failed to clear E-Invoice errors');
			const data = await res.json();
			clearAndRunResult = {
				status: data.status || 'COMPLETED',
				totalCleared: data.totalCleared ?? 0,
				clearedInvoices: data.clearedInvoices ?? [],
				clearedCrMemos: data.clearedCrMemos ?? [],
				processed: 0,
				errors: 0,
				message: data.message || `Cleared ${data.totalCleared ?? 0} skipped document(s).`
			};
			await fetchSkippedSummary();
		} catch (e: any) {
			clearError = e.message;
		} finally {
			clearOnlyLoading = false;
		}
	}

	async function runEwb() {
		ewbLoading = true;
		ewbError = null;
		ewbResult = null;
		try {
			const res = await secureFetch('/api/protean/run-ewb', { method: 'POST' });
			if (!res.ok) throw new Error(await res.text() || 'Failed to process E-Waybills');
			ewbResult = await res.json();
		} catch (e: any) {
			ewbError = e.message;
		} finally {
			ewbLoading = false;
		}
	}
</script>

<div class="min-h-screen bg-background pb-12">
	<PageHeading title="GST Processing Center" description="Generate E-Invoices and E-Waybills for pending documents." icon="shield-check" backHref="/" />

	<main class="container mx-auto px-4 py-8 max-w-4xl">
		<div class="grid gap-6 md:grid-cols-2">
			<!-- E-Invoice Processing Card -->
			<Card class="relative overflow-hidden group border-primary/20 hover:border-primary/40 transition-all duration-300 shadow-sm hover:shadow-md bg-gradient-to-br from-card to-primary/5">
				<div class="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
					<Icon name="file-text" class="size-24" />
				</div>
				<CardHeader>
					<div class="flex items-center gap-3 mb-2">
						<div class="p-2 rounded-lg bg-primary/10 text-primary">
							<Icon name="file-text" class="size-5" />
						</div>
						<CardTitle>E-Invoice (IRN)</CardTitle>
					</div>
					<CardDescription>
						Submit pending sales invoices and credit memos to the IRP portal for IRN generation.
					</CardDescription>
				</CardHeader>
				<CardContent class="space-y-4">
					<div class="flex flex-col gap-3">
						<!-- Skipped Document Alert / Banner -->
						{#if skippedInfo && skippedInfo.totalSkipped > 0}
							<div transition:slide class="p-3 rounded-lg bg-amber-500/10 border border-amber-500/20 text-amber-700 dark:text-amber-400 text-xs flex flex-col gap-1.5">
								<div class="flex items-center justify-between">
									<div class="flex items-center gap-2">
										<Icon name="alert-circle" class="size-4 shrink-0 text-amber-500" />
										<span>
											<strong>{skippedInfo.totalSkipped}</strong> document{skippedInfo.totalSkipped > 1 ? 's' : ''} skipped in last 30 days ([E-Inv Skip] = 1)
										</span>
									</div>
									<button 
										type="button" 
										class="text-[11px] font-medium underline hover:text-amber-800 dark:hover:text-amber-300 transition-colors"
										onclick={() => showSkippedList = !showSkippedList}
									>
										{showSkippedList ? 'Hide' : 'View'}
									</button>
								</div>
								{#if showSkippedList}
									<div transition:slide class="max-h-24 overflow-y-auto mt-1 p-2 rounded bg-background/80 text-[11px] font-mono border text-muted-foreground space-y-1">
										{#if skippedInfo.invoices.length > 0}
											<div><strong class="text-foreground">Invoices ({skippedInfo.invoices.length}):</strong> {skippedInfo.invoices.join(', ')}</div>
										{/if}
										{#if skippedInfo.crMemos.length > 0}
											<div><strong class="text-foreground">Credit Memos ({skippedInfo.crMemos.length}):</strong> {skippedInfo.crMemos.join(', ')}</div>
										{/if}
									</div>
								{/if}
							</div>
						{/if}

						<!-- Main Primary Action: Generate Pending IRNs -->
						<Button 
							size="lg" 
							onclick={runEInv} 
							disabled={einvLoading || clearAndRunLoading || clearOnlyLoading}
							class="w-full relative overflow-hidden group/btn shadow-sm"
						>
							{#if einvLoading}
								<Icon name="loader-2" class="mr-2 size-4 animate-spin" />
								Processing Pending IRNs...
							{:else}
								<Icon name="rocket" class="mr-2 size-4 group-hover/btn:translate-x-0.5 group-hover/btn:-translate-y-0.5 transition-transform" />
								Generate Pending IRNs
							{/if}
						</Button>

						<!-- Secondary & Optional Controls: Auto-Clear Errors & Generate, Clear Only -->
						<div class="pt-2 border-t border-border/40 flex flex-col gap-1.5">
							<div class="flex items-center justify-between text-[11px] text-muted-foreground">
								<span>Optional Error Recovery</span>
								{#if skippedInfo && skippedInfo.totalSkipped > 0}
									<span class="font-mono text-amber-600 dark:text-amber-400 font-semibold">{skippedInfo.totalSkipped} skipped</span>
								{/if}
							</div>
							<div class="flex gap-2">
								<Button 
									size="sm" 
									variant="outline"
									onclick={autoClearErrorsAndRun} 
									disabled={clearAndRunLoading || einvLoading || clearOnlyLoading}
									class="flex-1 text-xs border-amber-500/30 hover:bg-amber-500/10 hover:text-amber-700 dark:hover:text-amber-300"
									title="Clear [E-Inv Skip] = 1 for the last 30 days and generate pending IRNs"
								>
									{#if clearAndRunLoading}
										<Icon name="loader-2" class="mr-1.5 size-3.5 animate-spin" />
										Clearing & Generating...
									{:else}
										<Icon name="rotate-ccw" class="mr-1.5 size-3.5 text-amber-500" />
										Auto-Clear Errors & Generate
									{/if}
								</Button>

								<Button 
									size="sm" 
									variant="ghost"
									onclick={clearErrorsOnly} 
									disabled={clearOnlyLoading || clearAndRunLoading || einvLoading}
									class="text-xs text-muted-foreground hover:text-foreground"
									title="Reset [E-Inv Skip] = 0 without running generation"
								>
									{#if clearOnlyLoading}
										<Icon name="loader-2" class="mr-1 size-3 animate-spin" />
										Clearing...
									{:else}
										<Icon name="check" class="mr-1 size-3" />
										Clear Only
									{/if}
								</Button>
							</div>
						</div>

						{#if clearError}
							<div transition:slide class="p-3 rounded-md bg-destructive/10 border border-destructive/20 text-destructive text-sm flex items-start gap-2">
								<Icon name="alert-circle" class="size-4 shrink-0 mt-0.5" />
								<span>{clearError}</span>
							</div>
						{/if}

						{#if einvError}
							<div transition:slide class="p-3 rounded-md bg-destructive/10 border border-destructive/20 text-destructive text-sm flex items-start gap-2">
								<Icon name="alert-circle" class="size-4 shrink-0 mt-0.5" />
								<span>{einvError}</span>
							</div>
						{/if}

						{#if clearAndRunResult}
							<div transition:slide class="space-y-3 p-4 rounded-xl bg-background/50 border border-border/50 backdrop-blur-sm">
								<div class="flex items-center justify-between text-sm">
									<span class="text-muted-foreground">Auto-Clear & IRN Result</span>
									<span class="font-semibold text-primary">{clearAndRunResult.status}</span>
								</div>
								<div class="grid grid-cols-3 gap-2">
									<div class="p-2.5 rounded-lg bg-amber-500/10 border border-amber-500/20 text-center">
										<div class="text-xl font-bold text-amber-600 dark:text-amber-400">{clearAndRunResult.totalCleared}</div>
										<div class="text-[9px] uppercase tracking-wider font-semibold text-amber-600/70">Cleared</div>
									</div>
									<div class="p-2.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-center">
										<div class="text-xl font-bold text-emerald-600">{clearAndRunResult.processed}</div>
										<div class="text-[9px] uppercase tracking-wider font-semibold text-emerald-600/70">Generated</div>
									</div>
									<div class="p-2.5 rounded-lg bg-rose-500/10 border border-rose-500/20 text-center">
										<div class="text-xl font-bold text-rose-600">{clearAndRunResult.errors}</div>
										<div class="text-[9px] uppercase tracking-wider font-semibold text-rose-600/70">Errors</div>
									</div>
								</div>
								{#if clearAndRunResult.message}
									<p class="text-xs text-center text-muted-foreground italic">"{clearAndRunResult.message}"</p>
								{/if}
							</div>
						{/if}

						{#if einvResult}
							<div transition:slide class="space-y-3 p-4 rounded-xl bg-background/50 border border-border/50 backdrop-blur-sm">
								<div class="flex items-center justify-between text-sm">
									<span class="text-muted-foreground">Status</span>
									<span class="font-semibold text-primary">{einvResult.status}</span>
								</div>
								<div class="grid grid-cols-2 gap-4">
									<div class="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-center">
										<div class="text-2xl font-bold text-emerald-600">{einvResult.processed}</div>
										<div class="text-[10px] uppercase tracking-wider font-semibold text-emerald-600/70">Success</div>
									</div>
									<div class="p-3 rounded-lg bg-rose-500/10 border border-rose-500/20 text-center">
										<div class="text-2xl font-bold text-rose-600">{einvResult.errors}</div>
										<div class="text-[10px] uppercase tracking-wider font-semibold text-rose-600/70">Errors</div>
									</div>
								</div>
								{#if einvResult.message}
									<p class="text-xs text-center text-muted-foreground italic">"{einvResult.message}"</p>
								{/if}
							</div>
						{/if}
					</div>
				</CardContent>
			</Card>

			<!-- E-Waybill Processing Card -->
			<Card class="relative overflow-hidden group border-indigo-500/20 hover:border-indigo-500/40 transition-all duration-300 shadow-sm hover:shadow-md bg-gradient-to-br from-card to-indigo-500/5">
				<div class="absolute top-0 right-0 p-4 opacity-10 group-hover:opacity-20 transition-opacity">
					<Icon name="truck" class="size-24" />
				</div>
				<CardHeader>
					<div class="flex items-center gap-3 mb-2">
						<div class="p-2 rounded-lg bg-indigo-500/10 text-indigo-600">
							<Icon name="truck" class="size-5" />
						</div>
						<CardTitle>E-Waybill</CardTitle>
					</div>
					<CardDescription>
						Generate or cancel E-Waybills for documents pending transport documentation.
					</CardDescription>
				</CardHeader>
				<CardContent class="space-y-4">
					<div class="flex flex-col gap-3">
						<Button 
							size="lg" 
							onclick={runEwb} 
							disabled={ewbLoading}
							class="w-full bg-indigo-600 hover:bg-indigo-700 text-white relative overflow-hidden group/btn"
						>
							{#if ewbLoading}
								<Icon name="loader-2" class="mr-2 size-4 animate-spin" />
								Processing...
							{:else}
								<Icon name="share-2" class="mr-2 size-4 group-hover/btn:rotate-12 transition-transform" />
								Generate Pending E-Waybills
							{/if}
						</Button>

						{#if ewbError}
							<div transition:slide class="p-3 rounded-md bg-destructive/10 border border-destructive/20 text-destructive text-sm flex items-start gap-2">
								<Icon name="alert-circle" class="size-4 shrink-0 mt-0.5" />
								<span>{ewbError}</span>
							</div>
						{/if}

						{#if ewbResult}
							<div transition:slide class="space-y-3 p-4 rounded-xl bg-background/50 border border-border/50 backdrop-blur-sm">
								<div class="flex items-center justify-between text-sm">
									<span class="text-muted-foreground">Status</span>
									<span class="font-semibold text-indigo-600">{ewbResult.status}</span>
								</div>
								<div class="grid grid-cols-2 gap-4">
									<div class="p-3 rounded-lg bg-emerald-500/10 border border-emerald-500/20 text-center">
										<div class="text-2xl font-bold text-emerald-600">{ewbResult.processed}</div>
										<div class="text-[10px] uppercase tracking-wider font-semibold text-emerald-600/70">Success</div>
									</div>
									<div class="p-3 rounded-lg bg-rose-500/10 border border-rose-500/20 text-center">
										<div class="text-2xl font-bold text-rose-600">{ewbResult.errors}</div>
										<div class="text-[10px] uppercase tracking-wider font-semibold text-rose-600/70">Errors</div>
									</div>
								</div>
								{#if ewbResult.message}
									<p class="text-xs text-center text-muted-foreground italic">"{ewbResult.message}"</p>
								{/if}
							</div>
						{/if}
					</div>
				</CardContent>
			</Card>
		</div>

		<!-- Info Footer -->
		<div class="mt-8 flex items-center justify-center gap-6 text-xs text-muted-foreground">
			<div class="flex items-center gap-1.5">
				<div class="size-2 rounded-full bg-emerald-500 animate-pulse"></div>
				GSP Service: Active
			</div>
			<div class="flex items-center gap-1.5">
				<Icon name="info" class="size-3" />
				Batch size: 5 (Parallel)
			</div>
		</div>
	</main>
</div>

<style>
	/* Subtle custom gradient borders/animations can go here if needed */
</style>
