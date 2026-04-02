<script lang="ts">
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

	let einvLoading = $state(false);
	let ewbLoading = $state(false);

	let einvResult = $state<ProcessResult | null>(null);
	let ewbResult = $state<ProcessResult | null>(null);

	let einvError = $state<string | null>(null);
	let ewbError = $state<string | null>(null);

	async function runEInv() {
		einvLoading = true;
		einvError = null;
		einvResult = null;
		try {
			const res = await secureFetch('/api/protean/run-einv', { method: 'POST' });
			if (!res.ok) throw new Error(await res.text() || 'Failed to process E-Invoices');
			einvResult = await res.json();
		} catch (e: any) {
			einvError = e.message;
		} finally {
			einvLoading = false;
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
						<Button 
							size="lg" 
							onclick={runEInv} 
							disabled={einvLoading}
							class="w-full relative overflow-hidden group/btn"
						>
							{#if einvLoading}
								<Icon name="loader-2" class="mr-2 size-4 animate-spin" />
								Processing...
							{:else}
								<Icon name="rocket" class="mr-2 size-4 group-hover/btn:translate-x-1 group-hover/btn:-translate-y-1 transition-transform" />
								Generate Pending IRNs
							{/if}
						</Button>

						{#if einvError}
							<div transition:slide class="p-3 rounded-md bg-destructive/10 border border-destructive/20 text-destructive text-sm flex items-start gap-2">
								<Icon name="alert-circle" class="size-4 shrink-0 mt-0.5" />
								<span>{einvError}</span>
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
