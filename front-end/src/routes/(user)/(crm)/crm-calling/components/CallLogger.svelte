<script lang="ts">
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Textarea } from '$lib/components/ui/textarea';
	import { Switch } from '$lib/components/ui/switch';
	import Select from '$lib/components/venUI/select/select.svelte';
	import { DatePicker } from '$lib/components/venUI/date-picker';
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

	let outcomes = $state<{ value: string; label: string; isPositive: boolean }[]>([
		{ value: 'Answered', label: 'Answered', isPositive: true } // default fallback
	]);

	onMount(async () => {
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
	}
</script>

<div class="space-y-5">
	<div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
		<div class="space-y-2 flex flex-col">
			<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Call Outcome</span>
			<Select
				options={outcomes}
				bind:value={outcome}
				valueKey="value"
				labelKey="label"
				placeholder="Select outcome..."
				class="rounded-xl h-10 w-full bg-card"
			/>
		</div>
	</div>

	<div class="space-y-2">
		<label for="call-notes" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Notes / Conversation Summary</label>
		<Textarea
			id="call-notes"
			bind:value={notes}
			placeholder="Type conversation summary, client requirements, or details here..."
			class="min-h-[100px] rounded-xl focus-visible:ring-1"
		/>
	</div>

	<!-- Switch for Reminders -->
	<div class="border border-border bg-muted/10 rounded-xl p-4 space-y-4">
		<div class="flex items-center justify-between">
			<div class="space-y-0.5">
				<label for="schedule-followup" class="text-sm font-semibold cursor-pointer">Schedule Follow-up Reminder</label>
				<p class="text-xs text-muted-foreground">Automatically create a task to call back this client later.</p>
			</div>
			<Switch id="schedule-followup" bind:checked={scheduleFollowUp} />
		</div>

		{#if scheduleFollowUp}
			<div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
				<div class="space-y-1.5 flex flex-col">
					<span class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Reminder Date & Time</span>
					<DatePicker
						showTime
						valueType="text"
						placeholder="Select date & time..."
						bind:value={followUpDate}
						class="w-full bg-card"
					/>
				</div>
				<div class="space-y-1.5">
					<label for="reminder-notes" class="text-xs font-semibold text-muted-foreground uppercase tracking-wider">Reminder Task Note</label>
					<Input
						id="reminder-notes"
						bind:value={followUpNotes}
						placeholder="Leave blank to copy call notes..."
						class="rounded-xl h-10 focus-visible:ring-1"
					/>
				</div>
			</div>
		{/if}
	</div>

	<WhatsappWidget {selectedContact} />

	<div class="flex justify-end pt-2">
		<Button
			disabled={isSavingLog || !outcome}
			onclick={handleSave}
			class="bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl gap-2 font-semibold shadow-md px-6 h-10"
		>
			{#if isSavingLog}
				<Loader2 class="size-4 animate-spin shrink-0" />
			{/if}
			Save Call Log
		</Button>
	</div>
</div>
