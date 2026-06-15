<script lang="ts">
	import { graphqlMutation } from '$lib/services/graphql/client';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { Button } from '$lib/components/ui/button';

	let isLoading = $state(false);
	let lastRunResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	let isImporting = $state(false);
	let lastImportResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	const SANITIZE_MUTATION = `
		mutation SanitizeMobileNumbers {
			sanitizeSalesInvoiceHeaderMobileNumbers {
				success
				message
			}
		}
	`;

	const IMPORT_CRM_MUTATION = `
		mutation ImportCrmContacts {
			importCrmContactsFromInvoices {
				success
				message
			}
		}
	`;

	async function runSanitizer() {
		if (isLoading) return;
		isLoading = true;
		toast.info('Starting mobile number sanitation run...');

		try {
			const res = await graphqlMutation<{
				sanitizeSalesInvoiceHeaderMobileNumbers: { success: boolean; message: string };
			}>(SANITIZE_MUTATION);

			if (res.success && res.data?.sanitizeSalesInvoiceHeaderMobileNumbers?.success) {
				const msg = res.data.sanitizeSalesInvoiceHeaderMobileNumbers.message || 'Sanitation completed successfully.';
				toast.success(msg);
				lastRunResult = {
					success: true,
					message: msg,
					timestamp: new Date()
				};
			} else {
				const errorMsg = res.data?.sanitizeSalesInvoiceHeaderMobileNumbers?.message || res.error || 'Sanitation execution failed.';
				toast.error(errorMsg);
				lastRunResult = {
					success: false,
					message: errorMsg,
					timestamp: new Date()
				};
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during sanitation.';
			toast.error(errorMsg);
			lastRunResult = {
				success: false,
				message: errorMsg,
				timestamp: new Date()
			};
		} finally {
			isLoading = false;
		}
	}

	async function runCrmImport() {
		if (isImporting) return;
		isImporting = true;
		toast.info('Starting CRM contacts import from invoices...');

		try {
			const res = await graphqlMutation<{
				importCrmContactsFromInvoices: { success: boolean; message: string };
			}>(IMPORT_CRM_MUTATION);

			if (res.success && res.data?.importCrmContactsFromInvoices?.success) {
				const msg = res.data.importCrmContactsFromInvoices.message || 'Import completed successfully.';
				toast.success(msg);
				lastImportResult = { success: true, message: msg, timestamp: new Date() };
			} else {
				const errorMsg = res.data?.importCrmContactsFromInvoices?.message || res.error || 'Import failed.';
				toast.error(errorMsg);
				lastImportResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during import.';
			toast.error(errorMsg);
			lastImportResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isImporting = false;
		}
	}
</script>

<svelte:head>
	<title>Admin Tools | Tyresoles</title>
</svelte:head>

<div class="min-h-screen bg-[#090a0f] text-slate-100 pb-20 selection:bg-indigo-500/30">
	<!-- Top decorative ambient glow -->
	<div class="absolute top-0 left-1/2 -translate-x-1/2 w-full max-w-7xl h-[350px] bg-gradient-to-b from-indigo-500/10 via-purple-500/5 to-transparent blur-3xl pointer-events-none"></div>

	<div class="max-w-5xl mx-auto px-4 pt-8 relative z-10">
		<PageHeading backHref="/" icon="shield-alert">
			{#snippet title()}System Administration Tools{/snippet}
		</PageHeading>

		<p class="text-slate-400 mt-2 text-sm max-w-2xl">
			Critical system utilities, database operations, and diagnostic tests. Use with caution.
		</p>

		<div class="grid grid-cols-1 md:grid-cols-3 gap-6 mt-8">
			<!-- Main Tool Card -->
			<div class="md:col-span-2 rounded-2xl border border-slate-800 bg-slate-900/50 backdrop-blur-md p-6 shadow-2xl relative overflow-hidden transition-all duration-300 hover:border-slate-700/80">
				<!-- Subtle border glow -->
				<div class="absolute top-0 left-0 w-full h-[2px] bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500"></div>

				<div class="flex items-start justify-between gap-4">
					<div class="flex items-center gap-3">
						<div class="p-3 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
							<Icon name="phone" class="size-6" />
						</div>
						<div>
							<h2 class="text-lg font-semibold text-slate-200">Mobile Number Sanitation</h2>
							<span class="inline-flex items-center gap-1.5 px-2 py-0.5 mt-1.5 text-[10px] font-medium rounded-full bg-amber-500/10 text-amber-400 border border-amber-500/20">
								<Icon name="database" class="size-2.5" />
								Modifies Database
							</span>
						</div>
					</div>
				</div>

				<p class="text-slate-400 text-sm mt-4 leading-relaxed">
					Extracts, cleans, and normalizes Indian mobile numbers from:
				</p>
				<ul class="mt-2 space-y-1.5 text-xs text-slate-400 list-disc pl-5">
					<li><strong class="text-slate-300">Customer Name:</strong> Extracts digits matching phone patterns</li>
					<li><strong class="text-slate-300">Address / Address 2:</strong> Searches details for mobile candidates</li>
					<li><strong class="text-slate-300">Customer Phone No:</strong> Normalizes existing phone records</li>
				</ul>
				<p class="text-slate-400 text-sm mt-2 leading-relaxed">
					Updates the <code class="text-indigo-400 bg-indigo-950/40 px-1.5 py-0.5 rounded text-xs font-mono">Mobile No_</code> column in the <code class="text-indigo-400 bg-indigo-950/40 px-1.5 py-0.5 rounded text-xs font-mono">Sales Invoice Header</code> table for entries where it is currently empty or blank.
				</p>

				<div class="mt-6 pt-6 border-t border-slate-800 flex flex-wrap items-center justify-between gap-4">
					<div class="text-xs text-slate-500">
						Status: 
						{#if isLoading}
							<span class="text-indigo-400 font-medium inline-flex items-center gap-1">
								<Icon name="loader-2" class="size-3.5 animate-spin" /> Running...
							</span>
						{:else}
							<span class="text-emerald-400 font-medium">Ready</span>
						{/if}
					</div>

					<Button 
						onclick={runSanitizer}
						disabled={isLoading}
						class="bg-indigo-600 hover:bg-indigo-500 text-white font-medium shadow-lg hover:shadow-indigo-500/20 px-6 py-2 rounded-xl transition-all duration-300 flex items-center gap-2"
					>
						{#if isLoading}
							<Icon name="loader-2" class="size-4 animate-spin" />
							Processing Sanitation...
						{:else}
							<Icon name="sparkles" class="size-4" />
							Run Sanitation Utility
						{/if}
					</Button>
				</div>
			</div>

			<!-- Quick Info & Results Panel -->
			<div class="rounded-2xl border border-slate-800 bg-slate-900/40 backdrop-blur-md p-6 shadow-2xl flex flex-col justify-between">
				<div>
					<h3 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Execution Log</h3>
					
					{#if lastRunResult}
						<div class="mt-4 p-4 rounded-xl border {lastRunResult.success ? 'bg-emerald-950/10 border-emerald-900/30 text-emerald-400' : 'bg-rose-950/10 border-rose-900/30 text-rose-400'}">
							<div class="flex items-center gap-2 font-medium text-xs">
								<Icon name={lastRunResult.success ? 'check-circle' : 'alert-circle'} class="size-4 shrink-0" />
								<span>{lastRunResult.success ? 'Success' : 'Failed'}</span>
							</div>
							<p class="text-xs text-slate-300 mt-2 break-words leading-relaxed font-mono">
								{lastRunResult.message}
							</p>
							<div class="text-[10px] text-slate-500 mt-3 flex items-center gap-1">
								<Icon name="clock" class="size-3" />
								{lastRunResult.timestamp.toLocaleTimeString()}
							</div>
						</div>
					{:else}
						<div class="mt-8 flex flex-col items-center justify-center text-center p-6 border border-dashed border-slate-800 rounded-xl">
							<Icon name="terminal" class="size-8 text-slate-700 mb-2" />
							<span class="text-xs text-slate-500">No recent runs recorded. Run the sanitizer utility to view logs.</span>
						</div>
					{/if}
				</div>

				<div class="mt-6 pt-6 border-t border-slate-800/60 text-xs text-slate-500 leading-relaxed">
					<div class="flex items-center gap-1.5 text-slate-400 font-medium mb-1">
						<Icon name="info" class="size-3.5 text-indigo-400" />
						Safety Warning
					</div>
					This operation runs a batch update directly on live tables. It checks and processes un-sanitized sales headers. Ensure database backups are current.
				</div>
			</div>

			<!-- CRM Contacts Import Tool Card -->
			<div class="md:col-span-2 rounded-2xl border border-slate-800 bg-slate-900/50 backdrop-blur-md p-6 shadow-2xl relative overflow-hidden transition-all duration-300 hover:border-slate-700/80">
				<!-- Subtle border glow -->
				<div class="absolute top-0 left-0 w-full h-[2px] bg-gradient-to-r from-emerald-500 via-teal-500 to-cyan-500"></div>

				<div class="flex items-start justify-between gap-4">
					<div class="flex items-center gap-3">
						<div class="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
							<Icon name="users" class="size-6" />
						</div>
						<div>
							<h2 class="text-lg font-semibold text-slate-200">CRM Contacts Import</h2>
							<span class="inline-flex items-center gap-1.5 px-2 py-0.5 mt-1.5 text-[10px] font-medium rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
								<Icon name="database" class="size-2.5" />
								Creates Records
							</span>
						</div>
					</div>
				</div>

				<p class="text-slate-400 text-sm mt-4 leading-relaxed">
					Fetches unique mobile numbers from NAV Sales Invoice Header and creates new CRM Contact records:
				</p>
				<ul class="mt-2 space-y-1.5 text-xs text-slate-400 list-disc pl-5">
					<li><strong class="text-slate-300">Deduplication:</strong> Skips mobile numbers already present in CRM Contacts</li>
					<li><strong class="text-slate-300">Normalization:</strong> Extracts and validates 10-digit Indian mobile numbers</li>
					<li><strong class="text-slate-300">Auto-fill:</strong> Maps Name, Address, City, Resp Center, and ERP Customer No</li>
				</ul>

				<div class="mt-6 pt-6 border-t border-slate-800 flex flex-wrap items-center justify-between gap-4">
					<div class="text-xs text-slate-500">
						Status:
						{#if isImporting}
							<span class="text-emerald-400 font-medium inline-flex items-center gap-1">
								<Icon name="loader-2" class="size-3.5 animate-spin" /> Importing...
							</span>
						{:else if lastImportResult}
							<span class="{lastImportResult.success ? 'text-emerald-400' : 'text-rose-400'} font-medium">
								{lastImportResult.success ? 'Done' : 'Failed'}
							</span>
						{:else}
							<span class="text-emerald-400 font-medium">Ready</span>
						{/if}
					</div>

					<Button
						onclick={runCrmImport}
						disabled={isImporting}
						class="bg-emerald-600 hover:bg-emerald-500 text-white font-medium shadow-lg hover:shadow-emerald-500/20 px-6 py-2 rounded-xl transition-all duration-300 flex items-center gap-2"
					>
						{#if isImporting}
							<Icon name="loader-2" class="size-4 animate-spin" />
							Importing Contacts...
						{:else}
							<Icon name="download" class="size-4" />
							Run CRM Import
						{/if}
					</Button>
				</div>

				{#if lastImportResult}
					<div class="mt-4 p-4 rounded-xl border {lastImportResult.success ? 'bg-emerald-950/10 border-emerald-900/30 text-emerald-400' : 'bg-rose-950/10 border-rose-900/30 text-rose-400'}">
						<div class="flex items-center gap-2 font-medium text-xs">
							<Icon name={lastImportResult.success ? 'check-circle' : 'alert-circle'} class="size-4 shrink-0" />
							<span>{lastImportResult.success ? 'Success' : 'Failed'}</span>
						</div>
						<p class="text-xs text-slate-300 mt-2 break-words leading-relaxed font-mono">{lastImportResult.message}</p>
						<div class="text-[10px] text-slate-500 mt-3 flex items-center gap-1">
							<Icon name="clock" class="size-3" />
							{lastImportResult.timestamp.toLocaleTimeString()}
						</div>
					</div>
				{/if}
			</div>
		</div>
	</div>
</div>

<style>
	:global(.animate-spin) {
		animation: spin 1s linear infinite;
	}

	@keyframes spin {
		from {
			transform: rotate(0deg);
		}
		to {
			transform: rotate(360deg);
		}
	}
</style>
