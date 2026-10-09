<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Switch } from '$lib/components/ui/switch';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { DatePicker } from '$lib/components/venUI/date-picker';
	import { Icon } from '$lib/components/venUI/icon';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import type { CrmContact, CrmContactFleetDetail, CrmContactFleetDetailInput } from '../queries';
	import { GetCrmMasterItemsDocument, GetSalesUsersDocument } from '../queries';
	import WhatsappWidget from './WhatsappWidget.svelte';
	import { graphqlQuery } from '$lib/services/graphql';
	import { onMount } from 'svelte';

	let {
		selectedContact,
		onSaveCallLog,
		isSavingLog,
		fleetDetails = [],
		loadingFleet = false,
		onSaveFleetItem,
		onDeleteFleetItem,
		onOpenFleetTab
	}: {
		selectedContact: CrmContact | null;
		onSaveCallLog: (data: { outcome: string, notes: string, scheduleFollowUp: boolean, followUpDate: string, followUpNotes: string, isPositive: boolean, salesUserId?: string | null }) => Promise<void>;
		isSavingLog: boolean;
		fleetDetails?: CrmContactFleetDetail[];
		loadingFleet?: boolean;
		onSaveFleetItem?: (input: CrmContactFleetDetailInput) => Promise<boolean>;
		onDeleteFleetItem?: (id: string) => Promise<boolean>;
		onOpenFleetTab?: () => void;
	} = $props();

	let totalFleetVehicles = $derived(
		(fleetDetails || []).reduce((sum, item) => sum + (Number(item.quantity) || 0), 0)
	);

	let isSavingFleet = $state(false);
	let isSavingFleetRow = $state<string | null>(null);
	let showInlineQuickTotal = $state(false);
	let inlineQuickTotalValue = $state<number | null>(null);

	async function handleQuickAddFleetType(typeName: string) {
		if (!selectedContact || !onSaveFleetItem) return;
		const existing = (fleetDetails || []).find(
			(f) => f.vehicleType.toLowerCase() === typeName.toLowerCase() && !f.make && !f.model
		);

		if (existing) {
			await handleQuickUpdateFleetQty(existing, (Number(existing.quantity) || 0) + 1);
		} else {
			isSavingFleet = true;
			try {
				await onSaveFleetItem({
					contactId: selectedContact.id,
					vehicleType: typeName,
					quantity: 1
				});
			} finally {
				isSavingFleet = false;
			}
		}
	}

	async function handleQuickUpdateFleetQty(item: CrmContactFleetDetail, newQty: number) {
		if (newQty < 1 || !selectedContact || !onSaveFleetItem) return;
		isSavingFleetRow = item.id;
		try {
			await onSaveFleetItem({
				id: item.id,
				contactId: item.contactId || selectedContact.id,
				vehicleType: item.vehicleType,
				make: item.make || null,
				model: item.model || null,
				quantity: newQty,
				tyreSize: item.tyreSize || null,
				application: item.application || null
			});
		} finally {
			isSavingFleetRow = null;
		}
	}

	async function handleQuickDeleteFleet(id: string) {
		if (!onDeleteFleetItem) return;
		await onDeleteFleetItem(id);
	}

	async function handleSetInlineQuickTotal() {
		if (!inlineQuickTotalValue || inlineQuickTotalValue < 1 || !selectedContact || !onSaveFleetItem) return;
		isSavingFleet = true;
		try {
			if (fleetDetails && fleetDetails.length === 1 && !fleetDetails[0].make) {
				await handleQuickUpdateFleetQty(fleetDetails[0], inlineQuickTotalValue);
			} else {
				await onSaveFleetItem({
					contactId: selectedContact.id,
					vehicleType: 'Commercial Fleet',
					quantity: inlineQuickTotalValue
				});
			}
			showInlineQuickTotal = false;
			inlineQuickTotalValue = null;
		} finally {
			isSavingFleet = false;
		}
	}

	function handleInsertFleetIntoNotes() {
		if (!fleetDetails || fleetDetails.length === 0) return;
		const summaryParts = fleetDetails
			.map((f) => {
				let part = `${f.quantity} ${f.vehicleType}`;
				if (f.tyreSize) part += ` (${f.tyreSize})`;
				return part;
			})
			.join(', ');
		const fleetText = `Customer Fleet: ${totalFleetVehicles} vehicles [${summaryParts}]`;
		if (notes.includes(fleetText)) return;
		notes = notes ? `${notes}\n${fleetText}` : fleetText;
	}

	let outcome = $state('Answered');
	let salesUserId = $state('');
	let loadingSalesUsers = $state(true);
	let salesUsers = $state<{ value: string; label: string }[]>([]);
	let notes = $state('');
	let scheduleFollowUp = $state(false);
	let followUpDate = $state('');
	let followUpNotes = $state('');
	let showWhatsapp = $state(false);
	let loadingOutcomes = $state(true);

	let outcomes = $state<{ value: string; label: string; isPositive: boolean }[]>([
		{ value: 'Answered', label: 'Answered', isPositive: true } // default fallback
	]);

	let lastContactId = $state<string | null>(null);
	let lastRespCenter = $state<string | null>(null);
	const salesUsersCache = new Map<string, { value: string; label: string }[]>();

	async function loadSalesUsers(rc: string | null) {
		const cacheKey = rc ? rc.toUpperCase() : '__ALL__';
		if (salesUsersCache.has(cacheKey)) {
			salesUsers = salesUsersCache.get(cacheKey)!;
			loadingSalesUsers = false;
			return;
		}

		loadingSalesUsers = true;
		try {
			const usersRes = await graphqlQuery<any>(GetSalesUsersDocument, {
				variables: {
					where: {
						and: [
							{ state: { eq: 0 } },
							{ userType: { eq: 'SALES' } }
						]
					},
					take: 200,
					respCenter: rc || null
				}
			});

			if (usersRes.data?.users?.items?.length) {
				const mapped = usersRes.data.users.items.map((u: any) => ({
					value: u.userName,
					label: u.fullName ? `${u.fullName} (${u.userName})` : u.userName
				}));
				salesUsersCache.set(cacheKey, mapped);
				salesUsers = mapped;
			} else {
				salesUsersCache.set(cacheKey, []);
				salesUsers = [];
			}
		} catch (e) {
			console.error('Failed to load sales users for respCenter', rc, e);
			salesUsers = [];
		} finally {
			loadingSalesUsers = false;
		}
	}

	$effect(() => {
		const currentId = selectedContact?.id ?? null;
		const currentRc = selectedContact?.respCenter?.trim() || null;

		// If contact changed, reset the form
		if (currentId !== lastContactId) {
			lastContactId = currentId;
			lastRespCenter = currentRc;
			outcome = 'Answered';
			salesUserId = '';
			notes = '';
			scheduleFollowUp = false;
			followUpDate = '';
			followUpNotes = '';
			showWhatsapp = false;
			loadSalesUsers(currentRc);
		} else if (currentRc !== lastRespCenter) {
			lastRespCenter = currentRc;
			loadSalesUsers(currentRc);
			if (salesUserId && !salesUsers.some(u => u.value === salesUserId)) {
				salesUserId = '';
			}
		}
	});

	onMount(async () => {
		loadingOutcomes = true;
		try {
			// Fetch "Phone Call" activity type ID
			const typesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
				variables: { type: 'ACTIVITY_TYPE', where: { name: { eq: 'Phone Call' } } }
			});
			const phoneCallType = typesRes.data?.crmMasterItems?.[0];
			if (phoneCallType?.id) {
				const outcomesRes = await graphqlQuery<any>(GetCrmMasterItemsDocument, {
					variables: { type: 'ACTIVITY_OUTCOME', where: { parentId: { eq: phoneCallType.id } } }
				});
				if (outcomesRes.data?.crmMasterItems?.length) {
					outcomes = outcomesRes.data.crmMasterItems.map((o: any) => ({
						value: o.name,
						label: o.name,
						isPositive: o.isPositive ?? true
					}));
					if (outcomes.length > 0 && !outcomes.find(o => o.value === outcome)) {
						outcome = outcomes[0].value;
					}
				}
			}
		} catch (e) {
			console.error('Failed to load activity outcomes', e);
		} finally {
			loadingOutcomes = false;
		}
	});

	async function handleSave() {
		const selectedOutcome = outcomes.find(o => o.value === outcome);
		await onSaveCallLog({
			outcome,
			notes,
			scheduleFollowUp,
			followUpDate,
			followUpNotes,
			isPositive: selectedOutcome?.isPositive ?? true,
			salesUserId: salesUserId || null
		});
		// reset
		outcome = 'Answered';
		salesUserId = '';
		notes = '';
		scheduleFollowUp = false;
		followUpDate = '';
		followUpNotes = '';
		showWhatsapp = false;
	}
</script>

<div class="space-y-3">
	<!-- Row 1: Outcome, Sales Person & Follow-up Reminder Toggle (3-column ergonomic layout) -->
	<div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
		<div class="space-y-1">
			<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5 h-4.5">
				<span>Call Outcome</span>
				{#if loadingOutcomes}
					<Loader2 class="size-3 animate-spin text-primary shrink-0" />
				{/if}
			</span>
			<Select
				options={outcomes}
				bind:value={outcome}
				valueKey="value"
				labelKey="label"
				disabled={loadingOutcomes}
				placeholder="Select outcome..."
				class="rounded-lg h-9 w-full bg-background text-xs"
			/>
		</div>

		<div class="space-y-1">
			<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5 h-4.5">
				<span>Sales Person</span>
				{#if selectedContact?.respCenter}
					<span class="text-[10px] font-normal text-muted-foreground/80 normal-case">({selectedContact.respCenter})</span>
				{/if}
				{#if loadingSalesUsers}
					<Loader2 class="size-3 animate-spin text-primary shrink-0" />
				{/if}
			</span>
			<Select
				options={salesUsers}
				bind:value={salesUserId}
				valueKey="value"
				labelKey="label"
				disabled={loadingSalesUsers}
				clearable={true}
				placeholder={loadingSalesUsers ? "Loading sales persons..." : (salesUsers.length === 0 ? "No sales persons for this RC" : "Select sales person...")}
				class="rounded-lg h-9 w-full bg-background text-xs"
			/>
		</div>

		<div class="space-y-1">
			<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center h-4.5">
				<span>Follow-up Task</span>
			</span>
			<div class="flex items-center justify-between h-9 px-3 rounded-lg border border-border bg-muted/20">
				<label for="schedule-followup" class="text-xs font-semibold cursor-pointer flex items-center gap-1.5">
					<Icon name="calendar-clock" class="size-3.5 text-primary" />
					Schedule Follow-up
				</label>
				<Switch id="schedule-followup" bind:checked={scheduleFollowUp} />
			</div>
		</div>
	</div>

	<!-- Row 2: Follow-up Date/Time & Task Note (only when scheduled) -->
	{#if scheduleFollowUp}
		<div class="grid grid-cols-1 sm:grid-cols-2 gap-3 p-2.5 rounded-lg border border-primary/20 bg-primary/5 animate-in fade-in-50 duration-150">
			<div class="space-y-1">
				<span class="text-[11px] font-semibold text-muted-foreground">Reminder Date & Time</span>
				<DatePicker
					showTime
					valueType="text"
					placeholder="Select date & time..."
					bind:value={followUpDate}
				/>
			</div>
			<div class="space-y-1">
				<label for="reminder-notes" class="text-[11px] font-semibold text-muted-foreground">Task Note</label>
				<Input
					id="reminder-notes"
					bind:value={followUpNotes}
					placeholder="Leave blank to copy call notes..."
					class="rounded-lg h-9 text-xs"
				/>
			</div>
		</div>
	{/if}

	<!-- On-Call Handy Fleet Recorder Strip -->
	<div class="rounded-xl border border-amber-500/25 bg-amber-500/5 p-2.5 sm:p-3 space-y-2">
		<div class="flex items-center justify-between">
			<div class="flex items-center gap-2">
				<div class="p-1 rounded-md bg-amber-500/20 text-amber-700 dark:text-amber-400">
					<Icon name="truck" class="size-3.5" />
				</div>
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Customer Fleet</span>
				{#if loadingFleet}
					<Loader2 class="size-3 animate-spin text-amber-500" />
				{/if}
				<span class="text-xs font-mono font-bold px-2 py-0.5 rounded-md {totalFleetVehicles > 0 ? 'bg-amber-500/20 text-amber-800 dark:text-amber-300' : 'bg-muted text-muted-foreground'}">
					{totalFleetVehicles} {totalFleetVehicles === 1 ? 'vehicle' : 'vehicles'}
				</span>
			</div>

			<div class="flex items-center gap-2">
				{#if totalFleetVehicles > 0}
					<button
						type="button"
						onclick={handleInsertFleetIntoNotes}
						class="text-[11px] font-medium text-amber-700 dark:text-amber-400 hover:underline flex items-center gap-1 cursor-pointer"
						title="Append fleet summary to your call notes"
					>
						<Icon name="copy" class="size-3" />
						<span>Copy to Notes</span>
					</button>
				{/if}

				{#if onOpenFleetTab}
					<button
						type="button"
						onclick={onOpenFleetTab}
						class="text-[11px] font-semibold text-primary hover:underline flex items-center gap-1 cursor-pointer"
						title="Open detailed fleet editor with makes, models & tyre sizes"
					>
						<span>Detailed Lines</span>
						<Icon name="external-link" class="size-3" />
					</button>
				{/if}
			</div>
		</div>

		<!-- Existing Fleet Items with Live Steppers -->
		{#if fleetDetails && fleetDetails.length > 0}
			<div class="flex flex-wrap items-center gap-1.5 pt-0.5">
				{#each fleetDetails as item (item.id)}
					<div class="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-lg bg-background border border-border shadow-2xs text-xs">
						<span class="font-semibold text-foreground">{item.vehicleType}</span>
						{#if item.tyreSize}
							<span class="text-[10px] font-mono text-muted-foreground">({item.tyreSize})</span>
						{/if}
						<div class="flex items-center gap-0.5 bg-muted/50 rounded border border-border/60 px-0.5">
							<button
								type="button"
								onclick={() => handleQuickUpdateFleetQty(item, item.quantity - 1)}
								disabled={item.quantity <= 1 || isSavingFleetRow === item.id}
								class="size-5 hover:bg-card active:bg-muted font-bold text-xs flex items-center justify-center text-muted-foreground hover:text-foreground cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
								title="Minus 1"
							>-</button>
							<span class="w-6 text-center font-mono font-bold text-xs">
								{#if isSavingFleetRow === item.id}
									<Loader2 class="size-2.5 animate-spin mx-auto text-primary" />
								{:else}
									{item.quantity}
								{/if}
							</span>
							<button
								type="button"
								onclick={() => handleQuickUpdateFleetQty(item, item.quantity + 1)}
								disabled={isSavingFleetRow === item.id}
								class="size-5 hover:bg-card active:bg-muted font-bold text-xs flex items-center justify-center text-muted-foreground hover:text-foreground cursor-pointer disabled:opacity-50"
								title="Add 1"
							>+</button>
						</div>
						<button
							type="button"
							onclick={() => handleQuickDeleteFleet(item.id)}
							class="text-muted-foreground/60 hover:text-rose-500 p-0.5 cursor-pointer"
							title="Remove record"
						>
							<Icon name="x" class="size-3" />
						</button>
					</div>
				{/each}
			</div>
		{/if}

		<!-- 1-Click Fast Vehicle Type Adders & Quick Total Button -->
		<div class="flex items-center gap-1.5 flex-wrap pt-1 border-t border-amber-500/15">
			<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground mr-0.5">Record:</span>
			{#each ['Truck', 'Tipper', 'Trailer', 'Bus', 'LCV', 'Tanker'] as type}
				<button
					type="button"
					onclick={() => handleQuickAddFleetType(type)}
					disabled={isSavingFleet}
					class="inline-flex items-center gap-0.5 px-2 py-0.5 rounded-md text-[11px] font-semibold border border-border/70 bg-card hover:bg-amber-500/10 hover:border-amber-500/40 text-foreground transition-all cursor-pointer shadow-2xs active:scale-95 disabled:opacity-50"
					title="Add 1 {type}"
				>
					<span>+ {type}</span>
				</button>
			{/each}

			<button
				type="button"
				onclick={() => (showInlineQuickTotal = !showInlineQuickTotal)}
				class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[11px] font-semibold border border-dashed border-amber-400 dark:border-amber-600 bg-amber-50 dark:bg-amber-950/30 text-amber-800 dark:text-amber-300 hover:bg-amber-100 cursor-pointer ml-auto"
				title="Fast entry if customer just stated their total fleet count"
			>
				<Icon name="hash" class="size-3" />
				<span>Total Count</span>
			</button>
		</div>

		{#if showInlineQuickTotal}
			<div class="flex items-center gap-2 pt-1 animate-in fade-in-50 duration-150">
				<Input
					type="number"
					min="1"
					placeholder="Enter total fleet (e.g. 20)..."
					bind:value={inlineQuickTotalValue}
					class="h-7.5 text-xs font-mono font-bold w-44 bg-background rounded-lg"
					onkeydown={(e) => { if (e.key === 'Enter') handleSetInlineQuickTotal(); }}
				/>
				<Button
					size="sm"
					disabled={!inlineQuickTotalValue || inlineQuickTotalValue < 1 || isSavingFleet}
					onclick={handleSetInlineQuickTotal}
					class="h-7.5 px-2.5 text-xs font-semibold rounded-lg bg-primary cursor-pointer gap-1"
				>
					{#if isSavingFleet}<Loader2 class="size-3 animate-spin" />{/if}
					<span>Set Total</span>
				</Button>
				<button
					type="button"
					onclick={() => (showInlineQuickTotal = false)}
					class="text-xs text-muted-foreground hover:text-foreground cursor-pointer"
				>
					Cancel
				</button>
			</div>
		{/if}
	</div>

	<!-- Row 3: Notes / Conversation Summary (Compact & Focused) -->
	<div class="space-y-1">
		<label for="call-notes" class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Notes / Conversation Summary</label>
		<Textarea
			id="call-notes"
			bind:value={notes}
			placeholder="Type conversation summary, client requirements, or details here..."
			class="min-h-[70px] max-h-[140px] text-xs rounded-lg p-2.5 focus-visible:ring-1"
		/>
	</div>

	<!-- Collapsible WhatsApp Tools Drawer -->
	{#if showWhatsapp}
		<div class="pt-2 border-t border-border/60 animate-in fade-in-50 duration-150">
			<WhatsappWidget {selectedContact} />
		</div>
	{/if}

	<!-- Very Bottom Action Bar: WhatsApp Toggle on Left, Save Call Log prominently on Right -->
	<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pt-4 border-t border-border/70 mt-2">
		<button
			type="button"
			onclick={() => (showWhatsapp = !showWhatsapp)}
			class="text-xs font-semibold flex items-center gap-1.5 text-emerald-600 dark:text-emerald-400 hover:bg-emerald-500/10 px-2.5 py-1.5 rounded-lg border border-emerald-500/30 transition-all w-fit cursor-pointer"
		>
			<Icon name="message-circle" class="size-3.5 text-emerald-600 dark:text-emerald-400" />
			<span>{showWhatsapp ? 'Hide WhatsApp Offer Tools' : 'Send WhatsApp Offer / Brochure'}</span>
			<Icon name={showWhatsapp ? 'chevron-up' : 'chevron-down'} class="size-3" />
		</button>

		<Button
			disabled={isSavingLog || !outcome}
			onclick={handleSave}
			class="h-9 px-6 text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg gap-2 shadow-xs cursor-pointer ml-auto"
		>
			{#if isSavingLog}
				<Loader2 class="size-3.5 animate-spin shrink-0" />
			{/if}
			Save Call Log
		</Button>
	</div>
</div>
