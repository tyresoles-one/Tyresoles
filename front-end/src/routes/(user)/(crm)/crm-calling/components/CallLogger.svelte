<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Switch } from '$lib/components/ui/switch';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { DatePicker } from '$lib/components/venUI/date-picker';
	import { Icon } from '$lib/components/venUI/icon';
	import Loader2 from '@lucide/svelte/icons/loader-2';
	import type { CrmContact } from '../queries';
	import { GetCrmMasterItemsDocument } from '../queries';
	import WhatsappWidget from './WhatsappWidget.svelte';
	import { graphqlQuery } from '$lib/services/graphql';
	import { onMount } from 'svelte';

	let {
		selectedContact,
		onSaveCallLog,
		isSavingLog
	}: {
		selectedContact: CrmContact | null;
		onSaveCallLog: (data: { outcome: string, notes: string, scheduleFollowUp: boolean, followUpDate: string, followUpNotes: string, isPositive: boolean }) => Promise<void>;
		isSavingLog: boolean;
	} = $props();

	let outcome = $state('Answered');
	let notes = $state('');
	let scheduleFollowUp = $state(false);
	let followUpDate = $state('');
	let followUpNotes = $state('');
	let showWhatsapp = $state(false);
	let loadingOutcomes = $state(true);

	let outcomes = $state<{ value: string; label: string; isPositive: boolean }[]>([
		{ value: 'Answered', label: 'Answered', isPositive: true } // default fallback
	]);

	onMount(async () => {
		loadingOutcomes = true;
		try {
			// First fetch "Phone Call" activity type ID
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
			isPositive: selectedOutcome?.isPositive ?? true
		});
		// reset
		outcome = 'Answered';
		notes = '';
		scheduleFollowUp = false;
		followUpDate = '';
		followUpNotes = '';
		showWhatsapp = false;
	}
</script>

<div class="space-y-3">
	<!-- Row 1: Outcome & Follow-up Reminder Toggle (2-column ergonomic layout) -->
	<div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
		<div class="space-y-1">
			<div class="flex items-center justify-between">
				<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
					<span>Call Outcome</span>
					{#if loadingOutcomes}
						<Loader2 class="size-3 animate-spin text-primary shrink-0" />
					{/if}
				</span>
				<a
					href="/crm-masters?tab=ACTIVITY_OUTCOME"
					target="_blank"
					class="text-[11px] text-primary hover:underline flex items-center gap-0.5"
					title="Configure Call Outcomes in CRM Masters"
				>
					Outcomes <Icon name="external-link" class="size-2.5" />
				</a>
			</div>
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
			<span class="text-[11px] font-bold uppercase tracking-wider text-muted-foreground">Follow-up Task</span>
			<div class="flex items-center justify-between h-9 px-3 rounded-lg border border-border bg-muted/20">
				<label for="schedule-followup" class="text-xs font-semibold cursor-pointer flex items-center gap-1.5">
					<Icon name="calendar-clock" class="size-3.5 text-primary" />
					Schedule Follow-up Reminder
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
