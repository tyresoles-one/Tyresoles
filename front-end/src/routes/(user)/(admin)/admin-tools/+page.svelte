<script lang="ts">
	import { graphqlMutation } from '$lib/services/graphql/client';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import { Button } from '$lib/components/ui/button';
	import { Card, CardHeader, CardTitle, CardFooter } from '$lib/components/ui/card';

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

	let isRectifying = $state(false);
	let lastRectifyResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	const RECTIFY_LEDGERS_MUTATION = `
		mutation RectifyCustLedgers {
			rectifyCustLedgers {
				success
				message
			}
		}
	`;

	let isWipingCrm = $state(false);
	let lastWipeCrmResult = $state<{ success: boolean; message: string; timestamp: Date } | null>(null);

	const WIPE_CRM_MUTATION = `
		mutation WipeCrmCallingRecordsTemporary {
			wipeCrmCallingRecordsTemporary {
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

	async function runRectifyLedgers() {
		if (isRectifying) return;
		isRectifying = true;
		toast.info('Starting customer ledger rectification...');

		try {
			const res = await graphqlMutation<{
				rectifyCustLedgers: { success: boolean; message: string };
			}>(RECTIFY_LEDGERS_MUTATION);

			if (res.success && res.data?.rectifyCustLedgers?.success) {
				const msg = res.data.rectifyCustLedgers.message || 'Rectification completed successfully.';
				toast.success(msg);
				lastRectifyResult = { success: true, message: msg, timestamp: new Date() };
			} else {
				const errorMsg = res.data?.rectifyCustLedgers?.message || res.error || 'Rectification failed.';
				toast.error(errorMsg);
				lastRectifyResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during rectification.';
			toast.error(errorMsg);
			lastRectifyResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isRectifying = false;
		}
	}

	async function runWipeCrm() {
		if (isWipingCrm) return;
		
		const confirmed = confirm("CAUTION: This will permanently wipe all CRM calling allocations, logs, and reminders. Are you absolutely sure you want to proceed?");
		if (!confirmed) return;

		isWipingCrm = true;
		toast.info('Starting CRM calling records wipe...');

		try {
			const res = await graphqlMutation<{
				wipeCrmCallingRecordsTemporary: { success: boolean; message: string };
			}>(WIPE_CRM_MUTATION);

			if (res.success && res.data?.wipeCrmCallingRecordsTemporary?.success) {
				const msg = res.data.wipeCrmCallingRecordsTemporary.message || 'Wipe completed successfully.';
				toast.success(msg);
				lastWipeCrmResult = { success: true, message: msg, timestamp: new Date() };
			} else {
				const errorMsg = res.data?.wipeCrmCallingRecordsTemporary?.message || res.error || 'Wipe failed.';
				toast.error(errorMsg);
				lastWipeCrmResult = { success: false, message: errorMsg, timestamp: new Date() };
			}
		} catch (error: any) {
			const errorMsg = error.message || 'An unexpected error occurred during wipe.';
			toast.error(errorMsg);
			lastWipeCrmResult = { success: false, message: errorMsg, timestamp: new Date() };
		} finally {
			isWipingCrm = false;
		}
	}
</script>

<svelte:head>
	<title>Admin Tools | Tyresoles</title>
</svelte:head>

<div class="h-full flex flex-col gap-6 max-w-6xl mx-auto py-6 px-4 sm:px-6">
	<div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
		<div>
			<h1 class="text-2xl font-bold tracking-tight text-foreground">System Administration Tools</h1>
			<p class="text-sm text-muted-foreground mt-1">
				Critical system utilities, database operations, and diagnostic tests. Use with caution.
			</p>
		</div>
	</div>

	<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
		<Card>
			<CardHeader>
				<div class="flex items-center gap-2">
					<Icon name="phone" class="size-5 text-primary" />
					<CardTitle>Mobile Number Sanitation</CardTitle>
				</div>
			</CardHeader>
			<CardFooter class="pt-4 flex items-center">
				<Button onclick={runSanitizer} disabled={isLoading} class="gap-2 w-full">
					{#if isLoading}
						<Icon name="loader-2" class="size-4 animate-spin" /> Processing...
					{:else}
						<Icon name="sparkles" class="size-4" /> Run Sanitation Utility
					{/if}
				</Button>
			</CardFooter>
		</Card>

		<Card>
			<CardHeader>
				<div class="flex items-center gap-2">
					<Icon name="users" class="size-5 text-primary" />
					<CardTitle>CRM Contacts Import</CardTitle>
				</div>
			</CardHeader>
			<CardFooter class="pt-4 flex items-center">
				<Button onclick={runCrmImport} disabled={isImporting} class="gap-2 w-full">
					{#if isImporting}
						<Icon name="loader-2" class="size-4 animate-spin" /> Importing...
					{:else}
						<Icon name="download" class="size-4" /> Run CRM Import
					{/if}
				</Button>
			</CardFooter>
		</Card>

		<Card>
			<CardHeader>
				<div class="flex items-center gap-2">
					<Icon name="file-spreadsheet" class="size-5 text-primary" />
					<CardTitle>Ledger Rectification</CardTitle>
				</div>
			</CardHeader>
			<CardFooter class="pt-4 flex items-center">
				<Button onclick={runRectifyLedgers} disabled={isRectifying} class="gap-2 w-full">
					{#if isRectifying}
						<Icon name="loader-2" class="size-4 animate-spin" /> Rectifying...
					{:else}
						<Icon name="wrench" class="size-4" /> Run Ledger Rectification
					{/if}
				</Button>
			</CardFooter>
		</Card>

		<Card class="border-destructive/50">
			<CardHeader>
				<div class="flex items-center gap-2">
					<Icon name="trash-2" class="size-5 text-destructive" />
					<CardTitle class="text-destructive">Wipe CRM Records</CardTitle>
				</div>
			</CardHeader>
			<CardFooter class="pt-4 flex items-center">
				<Button onclick={runWipeCrm} disabled={isWipingCrm} variant="destructive" class="gap-2 w-full">
					{#if isWipingCrm}
						<Icon name="loader-2" class="size-4 animate-spin" /> Wiping...
					{:else}
						<Icon name="triangle-alert" class="size-4" /> Wipe Calling Records
					{/if}
				</Button>
			</CardFooter>
		</Card>
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
