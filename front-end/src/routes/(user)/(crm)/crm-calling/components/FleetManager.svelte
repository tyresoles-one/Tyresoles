<script lang="ts">
	import { onMount } from 'svelte';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import { graphqlQuery } from '$lib/services/graphql';
	import {
		GetCrmMasterItemsDocument,
		type CrmContactFleetDetail,
		type CrmContactFleetDetailInput
	} from '../queries';

	let {
		contactId,
		fleetDetails = [],
		loading = false,
		onSaveFleetItem,
		onDeleteFleetItem,
		mode = 'full'
	}: {
		contactId: string;
		fleetDetails: CrmContactFleetDetail[];
		loading?: boolean;
		onSaveFleetItem: (input: CrmContactFleetDetailInput) => Promise<boolean>;
		onDeleteFleetItem: (id: string) => Promise<boolean>;
		mode?: 'full' | 'compact';
	} = $props();

	// Quick common vehicle types for 1-click addition
	const QUICK_VEHICLE_TYPES = [
		{ name: 'Truck', icon: 'truck' },
		{ name: 'Tipper', icon: 'shield-alert' },
		{ name: 'Trailer', icon: 'container' },
		{ name: 'Bus', icon: 'bus' },
		{ name: 'LCV', icon: 'car' },
		{ name: 'Tanker', icon: 'droplet' }
	];

	// Common tyre sizes in commercial vehicles / tyre retreading
	const COMMON_TYRE_SIZES = [
		'295/80R22.5',
		'10.00R20',
		'11R22.5',
		'11.00R20',
		'10.00-20',
		'8.25-16',
		'7.50-16',
		'12.00R20',
		'12.00R24',
		'315/80R22.5',
		'385/65R22.5'
	];

	const COMMON_MAKES = [
		'Tata Motors',
		'Ashok Leyland',
		'BharatBenz',
		'Eicher',
		'Mahindra',
		'Volvo',
		'Scania',
		'Force'
	];

	const COMMON_APPLICATIONS = [
		'Long Haul / Highway',
		'Regional Haul',
		'Mining / Quarry Tipper',
		'Construction / Infra',
		'Passenger / Intercity',
		'Local Distribution'
	];

	// Master options loaded from backend
	let vehicleTypes = $state<{ id: number; name: string }[]>([]);
	let vehicleMakes = $state<{ id: number; name: string }[]>([]);
	let applications = $state<{ id: number; name: string }[]>([]);

	// Editing / row expansion state
	let expandedRowId = $state<string | null>(null);
	let isSavingRow = $state<string | null>(null);
	let isDeletingRow = $state<string | null>(null);

	// Quick Total input state (for customers who only give an overall fleet number)
	let quickTotalInput = $state<number | null>(null);
	let showQuickTotalBox = $state(false);

	// New custom row state
	let showAddNewRow = $state(false);
	let newRowType = $state('Truck');
	let newRowQty = $state(1);
	let newRowMake = $state('');
	let newRowModel = $state('');
	let newRowTyreSize = $state('');
	let newRowApplication = $state('');
	let isSavingNew = $state(false);

	// Automatic Totals & Derivations
	let totalVehicles = $derived(
		fleetDetails.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0)
	);

	let typeSummary = $derived.by(() => {
		const map = new Map<string, number>();
		for (const item of fleetDetails) {
			const type = item.vehicleType?.trim() || 'General';
			const qty = Number(item.quantity) || 0;
			map.set(type, (map.get(type) || 0) + qty);
		}
		return Array.from(map.entries()).map(([type, qty]) => ({ type, qty }));
	});

	onMount(async () => {
		try {
			const [resT, resM, resA] = await Promise.all([
				graphqlQuery<any>(GetCrmMasterItemsDocument, { variables: { type: 'VEHICLE_TYPE' } }),
				graphqlQuery<any>(GetCrmMasterItemsDocument, { variables: { type: 'VEHICLE_MAKE' } }),
				graphqlQuery<any>(GetCrmMasterItemsDocument, { variables: { type: 'APPLICATION' } })
			]);
			if (resT.success && resT.data?.crmMasterItems) vehicleTypes = resT.data.crmMasterItems;
			if (resM.success && resM.data?.crmMasterItems) vehicleMakes = resM.data.crmMasterItems;
			if (resA.success && resA.data?.crmMasterItems) applications = resA.data.crmMasterItems;
		} catch (e) {
			console.error('Failed to load fleet taxonomy masters', e);
		}
	});

	// Handy 1-Click addition of vehicle type
	async function handleQuickAddType(typeName: string) {
		// Check if a line with this type already exists; if so, increment its quantity!
		const existing = fleetDetails.find(
			(f) => f.vehicleType.toLowerCase() === typeName.toLowerCase() && !f.make && !f.model
		);

		if (existing) {
			await handleUpdateQuantity(existing, (Number(existing.quantity) || 0) + 1);
		} else {
			isSavingNew = true;
			try {
				await onSaveFleetItem({
					contactId,
					vehicleType: typeName,
					quantity: 1
				});
			} finally {
				isSavingNew = false;
			}
		}
	}

	// In-line quantity stepper (+ / -)
	async function handleUpdateQuantity(item: CrmContactFleetDetail, newQty: number) {
		if (newQty < 1) return;
		isSavingRow = item.id;
		try {
			await onSaveFleetItem({
				id: item.id,
				contactId: item.contactId || contactId,
				vehicleType: item.vehicleType,
				make: item.make || null,
				model: item.model || null,
				quantity: newQty,
				tyreSize: item.tyreSize || null,
				application: item.application || null
			});
		} finally {
			isSavingRow = null;
		}
	}

	// Save detailed row edits
	async function handleSaveRowDetails(item: CrmContactFleetDetail) {
		isSavingRow = item.id;
		try {
			const success = await onSaveFleetItem({
				id: item.id,
				contactId: item.contactId || contactId,
				vehicleType: item.vehicleType,
				make: item.make || null,
				model: item.model || null,
				quantity: Number(item.quantity) || 1,
				tyreSize: item.tyreSize || null,
				application: item.application || null
			});
			if (success) {
				expandedRowId = null;
			}
		} finally {
			isSavingRow = null;
		}
	}

	// Delete row
	async function handleDeleteRow(id: string) {
		if (!confirm('Are you sure you want to remove this fleet record?')) return;
		isDeletingRow = id;
		try {
			await onDeleteFleetItem(id);
			if (expandedRowId === id) expandedRowId = null;
		} finally {
			isDeletingRow = null;
		}
	}

	// Handle Quick Single Total (e.g. customer says "We have 20 vehicles")
	async function handleSetQuickTotal() {
		if (!quickTotalInput || quickTotalInput < 1) return;
		isSavingNew = true;
		try {
			// If existing items, ask or replace, or if empty, create Commercial Fleet item
			if (fleetDetails.length === 1 && !fleetDetails[0].make) {
				await handleUpdateQuantity(fleetDetails[0], quickTotalInput);
			} else {
				await onSaveFleetItem({
					contactId,
					vehicleType: 'Commercial Fleet',
					quantity: quickTotalInput
				});
			}
			showQuickTotalBox = false;
			quickTotalInput = null;
		} finally {
			isSavingNew = false;
		}
	}

	// Add new custom detailed row
	async function handleSaveNewCustomRow() {
		if (!newRowType.trim() || newRowQty < 1) return;
		isSavingNew = true;
		try {
			const success = await onSaveFleetItem({
				contactId,
				vehicleType: newRowType.trim(),
				quantity: newRowQty,
				make: newRowMake.trim() || null,
				model: newRowModel.trim() || null,
				tyreSize: newRowTyreSize.trim() || null,
				application: newRowApplication.trim() || null
			});
			if (success) {
				showAddNewRow = false;
				newRowType = 'Truck';
				newRowQty = 1;
				newRowMake = '';
				newRowModel = '';
				newRowTyreSize = '';
				newRowApplication = '';
			}
		} finally {
			isSavingNew = false;
		}
	}
</script>

<div class="space-y-3 select-none">
	<!-- Top Overview Card: Automatic Total + Type Breakdown -->
	<div class="p-3 sm:p-3.5 rounded-xl border border-amber-500/30 bg-amber-500/5 shadow-2xs space-y-2.5">
		<div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
			<div class="flex items-center gap-2">
				<div class="p-2 rounded-lg bg-amber-500/15 text-amber-600 dark:text-amber-400 shrink-0">
					<Icon name="truck" class="size-5" />
				</div>
				<div>
					<div class="flex items-center gap-2">
						<span class="text-xs font-bold uppercase tracking-wider text-muted-foreground">Total Customer Fleet</span>
						{#if loading}
							<Loader2 class="size-3.5 animate-spin text-primary shrink-0" />
						{/if}
					</div>
					<div class="flex items-baseline gap-1.5 mt-0.5">
						<span class="text-xl sm:text-2xl font-black text-foreground font-mono">
							{totalVehicles}
						</span>
						<span class="text-xs font-semibold text-muted-foreground">
							Vehicle{totalVehicles === 1 ? '' : 's'} Total
						</span>
					</div>
				</div>
			</div>

			<!-- Quick Actions: Single Total or Add Line -->
			<div class="flex items-center gap-1.5 flex-wrap">
				<Button
					variant="outline"
					size="sm"
					onclick={() => (showQuickTotalBox = !showQuickTotalBox)}
					class="h-8 px-2.5 text-xs font-semibold rounded-lg border-amber-300 dark:border-amber-700/60 bg-amber-100/50 dark:bg-amber-950/40 text-amber-800 dark:text-amber-300 hover:bg-amber-200/50 gap-1.5 cursor-pointer"
					title="Fast entry for customers who only state total fleet size"
				>
					<Icon name="hash" class="size-3.5" />
					<span>Quick Total</span>
				</Button>

				<Button
					size="sm"
					onclick={() => (showAddNewRow = !showAddNewRow)}
					class="h-8 px-3 text-xs font-semibold rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 gap-1.5 shadow-2xs cursor-pointer"
				>
					<Icon name="plus" class="size-3.5" />
					<span>Add Line</span>
				</Button>
			</div>
		</div>

		<!-- Type Breakdown Pills (Automatically updated) -->
		{#if typeSummary.length > 0}
			<div class="flex flex-wrap items-center gap-1.5 pt-1 border-t border-amber-500/20">
				<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground mr-1">Breakdown:</span>
				{#each typeSummary as item}
					<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-md text-[11px] font-semibold bg-background border border-border shadow-2xs text-foreground">
						<span>{item.type}:</span>
						<strong class="font-mono font-bold text-primary">{item.qty}</strong>
					</span>
				{/each}
			</div>
		{/if}
	</div>

	<!-- Quick Single Total Box (Popdown when customer just says "I have 15 vehicles") -->
	{#if showQuickTotalBox}
		<div class="p-3 rounded-xl border border-primary/30 bg-primary/5 space-y-2 animate-in fade-in-50 duration-150">
			<div class="flex items-center justify-between">
				<span class="text-xs font-bold text-foreground flex items-center gap-1.5">
					<Icon name="zap" class="size-3.5 text-amber-500" />
					<span>Quick Set Total Fleet Count</span>
				</span>
				<button type="button" onclick={() => (showQuickTotalBox = false)} class="text-muted-foreground hover:text-foreground text-xs">
					<Icon name="x" class="size-3.5" />
				</button>
			</div>
			<p class="text-[11px] text-muted-foreground">
				Use when customer quickly says a total number (e.g. "We have 15 vehicles").
			</p>
			<div class="flex items-center gap-2">
				<Input
					type="number"
					min="1"
					max="5000"
					placeholder="Enter total fleet size (e.g. 15)..."
					bind:value={quickTotalInput}
					class="h-8.5 rounded-lg text-xs font-mono font-bold w-48 bg-background"
					onkeydown={(e) => { if (e.key === 'Enter') handleSetQuickTotal(); }}
				/>
				<Button
					size="sm"
					disabled={!quickTotalInput || quickTotalInput < 1 || isSavingNew}
					onclick={handleSetQuickTotal}
					class="h-8.5 px-3 text-xs font-semibold rounded-lg bg-primary cursor-pointer gap-1.5"
				>
					{#if isSavingNew}<Loader2 class="size-3.5 animate-spin" />{/if}
					<span>Set Total</span>
				</Button>
				<Button
					variant="ghost"
					size="sm"
					onclick={() => (showQuickTotalBox = false)}
					class="h-8.5 text-xs text-muted-foreground"
				>
					Cancel
				</Button>
			</div>
		</div>
	{/if}

	<!-- 1-Click Fast Type Chips for Active Live Calls -->
	<div class="flex items-center gap-1.5 flex-wrap">
		<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground mr-1 flex items-center gap-1">
			<Icon name="plus-circle" class="size-3 text-primary" />
			1-Click Add:
		</span>
		{#each QUICK_VEHICLE_TYPES as vt}
			<button
				type="button"
				onclick={() => handleQuickAddType(vt.name)}
				disabled={isSavingNew}
				class="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-semibold border border-border/70 bg-card hover:bg-primary/10 hover:border-primary/40 text-foreground transition-all cursor-pointer shadow-2xs active:scale-95 disabled:opacity-50"
				title="Add 1 {vt.name} to customer fleet"
			>
				<Icon name={vt.icon} class="size-3 text-primary" />
				<span>+ {vt.name}</span>
			</button>
		{/each}
	</div>

	<!-- Add Detailed Row Box (Expandable) -->
	{#if showAddNewRow}
		<div class="p-3.5 rounded-xl border border-primary/30 bg-card shadow-xs space-y-3 animate-in fade-in-50 duration-150">
			<div class="flex items-center justify-between pb-1 border-b border-border/60">
				<span class="text-xs font-bold text-foreground flex items-center gap-1.5">
					<Icon name="plus" class="size-3.5 text-primary" />
					<span>Add Detailed Fleet Record</span>
				</span>
				<button type="button" onclick={() => (showAddNewRow = false)} class="text-muted-foreground hover:text-foreground">
					<Icon name="x" class="size-3.5" />
				</button>
			</div>

			<div class="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
				<!-- Vehicle Type -->
				<div class="space-y-1">
					<label for="new-vtype" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Vehicle Type *</label>
					<input
						id="new-vtype"
						type="text"
						list="vtypes-list"
						bind:value={newRowType}
						placeholder="e.g. Truck, Tipper..."
						class="w-full h-8 px-2.5 rounded-lg border border-border bg-background text-xs"
					/>
					<datalist id="vtypes-list">
						{#each vehicleTypes as vt}<option value={vt.name}></option>{/each}
						<option value="Truck"></option>
						<option value="Tipper"></option>
						<option value="Trailer"></option>
						<option value="Bus"></option>
						<option value="LCV"></option>
						<option value="Tanker"></option>
						<option value="Tractor / OTR"></option>
					</datalist>
				</div>

				<!-- Quantity -->
				<div class="space-y-1">
					<label for="new-vqty" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Quantity *</label>
					<div class="flex items-center gap-1">
						<button
							type="button"
							onclick={() => newRowQty = Math.max(1, newRowQty - 1)}
							class="size-8 rounded-lg border border-border bg-muted/30 hover:bg-muted flex items-center justify-center font-bold text-xs cursor-pointer"
						>-</button>
						<Input
							id="new-vqty"
							type="number"
							min="1"
							bind:value={newRowQty}
							class="h-8 rounded-lg text-xs font-mono font-bold text-center w-full"
						/>
						<button
							type="button"
							onclick={() => newRowQty = newRowQty + 1}
							class="size-8 rounded-lg border border-border bg-muted/30 hover:bg-muted flex items-center justify-center font-bold text-xs cursor-pointer"
						>+</button>
					</div>
				</div>

				<!-- Make -->
				<div class="space-y-1">
					<label for="new-vmake" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Make (Manufacturer)</label>
					<input
						id="new-vmake"
						type="text"
						list="vmakes-list"
						bind:value={newRowMake}
						placeholder="e.g. Tata, Ashok Leyland..."
						class="w-full h-8 px-2.5 rounded-lg border border-border bg-background text-xs"
					/>
					<datalist id="vmakes-list">
						{#each COMMON_MAKES as mk}<option value={mk}></option>{/each}
						{#each vehicleMakes as vm}<option value={vm.name}></option>{/each}
					</datalist>
				</div>
			</div>

			<div class="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
				<!-- Model -->
				<div class="space-y-1">
					<label for="new-vmodel" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Model (Optional)</label>
					<Input
						id="new-vmodel"
						bind:value={newRowModel}
						placeholder="e.g. 3118, 2823, Signa..."
						class="h-8 rounded-lg text-xs"
					/>
				</div>

				<!-- Tyre Size -->
				<div class="space-y-1">
					<label for="new-vtyre" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Tyre Size</label>
					<input
						id="new-vtyre"
						type="text"
						list="vtyres-list"
						bind:value={newRowTyreSize}
						placeholder="e.g. 295/80R22.5, 10.00R20..."
						class="w-full h-8 px-2.5 rounded-lg border border-border bg-background text-xs"
					/>
					<datalist id="vtyres-list">
						{#each COMMON_TYRE_SIZES as ts}<option value={ts}></option>{/each}
					</datalist>
				</div>

				<!-- Application -->
				<div class="space-y-1">
					<label for="new-vapp" class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Application</label>
					<input
						id="new-vapp"
						type="text"
						list="vapps-list"
						bind:value={newRowApplication}
						placeholder="e.g. Long Haul, Mining..."
						class="w-full h-8 px-2.5 rounded-lg border border-border bg-background text-xs"
					/>
					<datalist id="vapps-list">
						{#each COMMON_APPLICATIONS as ap}<option value={ap}></option>{/each}
						{#each applications as appItem}<option value={appItem.name}></option>{/each}
					</datalist>
				</div>
			</div>

			<div class="flex items-center justify-end gap-2 pt-1">
				<Button variant="ghost" size="sm" onclick={() => (showAddNewRow = false)} class="h-8 text-xs">
					Cancel
				</Button>
				<Button
					size="sm"
					disabled={!newRowType.trim() || newRowQty < 1 || isSavingNew}
					onclick={handleSaveNewCustomRow}
					class="h-8 px-4 text-xs font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white gap-1.5 cursor-pointer"
				>
					{#if isSavingNew}<Loader2 class="size-3.5 animate-spin" />{/if}
					<span>Save Fleet Record</span>
				</Button>
			</div>
		</div>
	{/if}

	<!-- Fleet Records List -->
	<div class="space-y-2">
		{#if loading && fleetDetails.length === 0}
			<div class="text-center py-8 text-muted-foreground text-xs flex flex-col items-center justify-center gap-2">
				<Loader2 class="size-5 animate-spin text-primary" />
				<span>Loading fleet details...</span>
			</div>
		{:else if fleetDetails.length === 0}
			<div class="text-center py-6 px-4 rounded-xl border border-dashed border-border bg-muted/10 text-muted-foreground text-xs space-y-2">
				<Icon name="truck" class="size-7 mx-auto opacity-40 text-muted-foreground" />
				<p class="font-medium text-foreground">No Fleet Records for this Contact</p>
				<p class="text-[11px] text-muted-foreground max-w-sm mx-auto">
					Ask the customer on call how many vehicles they run. Use the 1-click buttons above or "Quick Total" to record their fleet in seconds.
				</p>
			</div>
		{:else}
			{#each fleetDetails as item (item.id)}
				{@const isExpanded = expandedRowId === item.id}
				<div class="rounded-xl border border-border bg-card transition-all shadow-2xs hover:border-border/80">
					<!-- Main Summary Row -->
					<div class="p-2.5 sm:p-3 flex items-center justify-between gap-2">
						<!-- Left: Type, Make, Model, Tyre & App tags -->
						<div class="flex items-center gap-2.5 min-w-0 flex-1">
							<div class="size-8 rounded-lg bg-primary/10 text-primary flex items-center justify-center shrink-0">
								<Icon name="truck" class="size-4" />
							</div>

							<div class="min-w-0 space-y-0.5">
								<div class="flex items-center gap-2 flex-wrap">
									<span class="font-bold text-xs text-foreground truncate">
										{item.vehicleType}
									</span>

									{#if item.make || item.model}
										<span class="text-[11px] font-semibold text-muted-foreground">
											{[item.make, item.model].filter(Boolean).join(' · ')}
										</span>
									{/if}

									{#if item.tyreSize}
										<span class="px-1.5 py-0.2 rounded text-[10px] font-bold bg-amber-500/10 text-amber-700 dark:text-amber-300 border border-amber-500/20 font-mono">
											{item.tyreSize}
										</span>
									{/if}

									{#if item.application}
										<span class="px-1.5 py-0.2 rounded text-[10px] font-medium bg-muted text-muted-foreground">
											{item.application}
										</span>
									{/if}
								</div>
							</div>
						</div>

						<!-- Right: Stepper [-] [ Qty ] [+] + Edit Details & Delete -->
						<div class="flex items-center gap-2 shrink-0">
							<!-- In-line Stepper for fast count changes during call -->
							<div class="flex items-center gap-1 bg-muted/40 p-0.5 rounded-lg border border-border/60">
								<button
									type="button"
									disabled={item.quantity <= 1 || isSavingRow === item.id}
									onclick={() => handleUpdateQuantity(item, item.quantity - 1)}
									class="size-6 rounded-md hover:bg-card active:bg-muted font-bold text-xs flex items-center justify-center text-muted-foreground hover:text-foreground cursor-pointer disabled:opacity-30 disabled:cursor-not-allowed"
									title="Decrease quantity by 1"
								>-</button>

								<span class="w-8 text-center font-mono font-black text-xs text-foreground">
									{#if isSavingRow === item.id}
										<Loader2 class="size-3 animate-spin mx-auto text-primary" />
									{:else}
										{item.quantity}
									{/if}
								</span>

								<button
									type="button"
									disabled={isSavingRow === item.id}
									onclick={() => handleUpdateQuantity(item, item.quantity + 1)}
									class="size-6 rounded-md hover:bg-card active:bg-muted font-bold text-xs flex items-center justify-center text-muted-foreground hover:text-foreground cursor-pointer disabled:opacity-30"
									title="Increase quantity by 1"
								>+</button>
							</div>

							<!-- Edit Details Toggle Button -->
							<Button
								variant="ghost"
								size="sm"
								onclick={() => expandedRowId = isExpanded ? null : item.id}
								class="size-7 p-0 rounded-lg text-muted-foreground hover:text-foreground cursor-pointer {isExpanded ? 'bg-muted' : ''}"
								title={isExpanded ? 'Close details' : 'Edit details (Make, Model, Tyre Size, Application)'}
							>
								<Icon name={isExpanded ? 'chevron-up' : 'edit-2'} class="size-3.5" />
							</Button>

							<!-- Delete Button -->
							<Button
								variant="ghost"
								size="sm"
								disabled={isDeletingRow === item.id}
								onclick={() => handleDeleteRow(item.id)}
								class="size-7 p-0 rounded-lg text-rose-500 hover:text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30 cursor-pointer"
								title="Delete this fleet record"
							>
								{#if isDeletingRow === item.id}
									<Loader2 class="size-3.5 animate-spin" />
								{:else}
									<Icon name="trash-2" class="size-3.5" />
								{/if}
							</Button>
						</div>
					</div>

					<!-- Expandable Detailed Line Editor -->
					{#if isExpanded}
						<div class="px-3 pb-3 pt-2 border-t border-border/50 bg-muted/15 space-y-2.5 animate-in fade-in-50 duration-150">
							<div class="grid grid-cols-1 sm:grid-cols-4 gap-2">
								<div class="space-y-1">
									<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Vehicle Type</span>
									<input
										type="text"
										bind:value={item.vehicleType}
										class="w-full h-7.5 px-2 rounded-lg border border-border bg-background text-xs font-semibold"
									/>
								</div>

								<div class="space-y-1">
									<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Make</span>
									<input
										type="text"
										list="vmakes-list"
										bind:value={item.make}
										placeholder="e.g. Tata..."
										class="w-full h-7.5 px-2 rounded-lg border border-border bg-background text-xs"
									/>
								</div>

								<div class="space-y-1">
									<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Model</span>
									<Input
										bind:value={item.model}
										placeholder="e.g. 3118..."
										class="h-7.5 text-xs rounded-lg"
									/>
								</div>

								<div class="space-y-1">
									<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Tyre Size</span>
									<input
										type="text"
										list="vtyres-list"
										bind:value={item.tyreSize}
										placeholder="e.g. 295/80R22.5..."
										class="w-full h-7.5 px-2 rounded-lg border border-border bg-background text-xs font-mono font-medium"
									/>
								</div>
							</div>

							<!-- Quick Tyre Size Chips for 1-click selection -->
							<div class="flex items-center gap-1 flex-wrap pt-0.5">
								<span class="text-[9px] font-bold uppercase tracking-wider text-muted-foreground mr-1">Quick Size:</span>
								{#each COMMON_TYRE_SIZES.slice(0, 6) as size}
									<button
										type="button"
										onclick={() => item.tyreSize = size}
										class="px-1.5 py-0.5 rounded text-[10px] font-mono border border-border bg-background hover:bg-primary/10 hover:border-primary/40 cursor-pointer {item.tyreSize === size ? 'bg-primary/10 border-primary text-primary font-bold' : 'text-muted-foreground'}"
									>
										{size}
									</button>
								{/each}
							</div>

							<div class="flex items-center justify-between pt-1">
								<div class="flex-1 max-w-xs space-y-1">
									<span class="text-[10px] font-bold uppercase tracking-wider text-muted-foreground">Application</span>
									<input
										type="text"
										list="vapps-list"
										bind:value={item.application}
										placeholder="e.g. Long Haul, Mining..."
										class="w-full h-7.5 px-2 rounded-lg border border-border bg-background text-xs"
									/>
								</div>

								<div class="flex items-center gap-1.5">
									<Button
										variant="ghost"
										size="sm"
										onclick={() => (expandedRowId = null)}
										class="h-7 px-2.5 text-xs"
									>
										Cancel
									</Button>
									<Button
										size="sm"
										disabled={isSavingRow === item.id || !item.vehicleType}
										onclick={() => handleSaveRowDetails(item)}
										class="h-7 px-3 text-xs font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white gap-1 cursor-pointer"
									>
										{#if isSavingRow === item.id}<Loader2 class="size-3 animate-spin" />{/if}
										Save Changes
									</Button>
								</div>
							</div>
						</div>
					{/if}
				</div>
			{/each}
		{/if}
	</div>
</div>
