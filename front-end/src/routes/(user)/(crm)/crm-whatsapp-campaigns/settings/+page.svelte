<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Icon } from '$lib/components/venUI/icon';
	import { toast } from '$lib/components/venUI/toast';
	import PageHeading from '$lib/components/venUI/page-heading/PageHeading.svelte';
	import {
		GET_WHATSAPP_ACCOUNT_STATUS,
		GET_WHATSAPP_SUPPRESSION_LIST,
		GET_CRM_SETTINGS,
		SAVE_WHATSAPP_SETTINGS,
		ADD_WHATSAPP_SUPPRESSION,
		REMOVE_WHATSAPP_SUPPRESSION,
		type WabaHealthStatus,
		type CrmWhatsappSuppression
	} from '../whatsappQueries';

	// Settings state
	let wabaId = $state('');
	let phoneNumberId = $state('');
	let displayPhoneNumber = $state('+91 98803 34191');
	let accessToken = $state('');
	let appSecret = $state('');
	let webhookVerifyToken = $state('tyresoles_crm_wa_webhook_verify_2026');
	let simulationMode = $state(false);
	let costPerMessage = $state(0.80);

	let savingSettings = $state(false);
	let testingConnection = $state(false);
	let accountStatus = $state<WabaHealthStatus | null>(null);

	// Suppression List state
	let suppressionList = $state<CrmWhatsappSuppression[]>([]);
	let suppressionSearch = $state('');
	let loadingSuppression = $state(false);

	let newSuppressionPhone = $state('');
	let newSuppressionReason = $state('UserReplyStop');
	let newSuppressionNotes = $state('');
	let addingSuppression = $state(false);

	let webhookCallbackUrl = $derived.by(() => {
		if (typeof window !== 'undefined') {
			return `${window.location.origin}/api/campaigns/webhooks/whatsapp`;
		}
		return 'https://app.tyresoles.in/api/campaigns/webhooks/whatsapp';
	});

	async function loadSavedSettings() {
		try {
			const res = await graphqlQuery<{ getCrmSettings: Array<{ key: string; value: string }> }>(
				GET_CRM_SETTINGS
			);
			if (res.success && res.data?.getCrmSettings) {
				const map = new Map(res.data.getCrmSettings.map((s) => [s.key, s.value]));
				if (map.has('WHATSAPP_WABA_ID')) wabaId = map.get('WHATSAPP_WABA_ID') || '';
				if (map.has('WHATSAPP_PHONE_NUMBER_ID')) phoneNumberId = map.get('WHATSAPP_PHONE_NUMBER_ID') || '';
				if (map.has('WHATSAPP_DISPLAY_PHONE_NUMBER')) displayPhoneNumber = map.get('WHATSAPP_DISPLAY_PHONE_NUMBER') || '';
				if (map.has('WHATSAPP_ACCESS_TOKEN')) accessToken = map.get('WHATSAPP_ACCESS_TOKEN') || '';
				if (map.has('WHATSAPP_APP_SECRET')) appSecret = map.get('WHATSAPP_APP_SECRET') || '';
				if (map.has('WHATSAPP_WEBHOOK_VERIFY_TOKEN')) webhookVerifyToken = map.get('WHATSAPP_WEBHOOK_VERIFY_TOKEN') || 'tyresoles_crm_wa_webhook_verify_2026';
				if (map.has('WHATSAPP_SIMULATION_MODE')) simulationMode = map.get('WHATSAPP_SIMULATION_MODE') === 'True';
				if (map.has('WHATSAPP_COST_PER_MESSAGE')) {
					const val = parseFloat(map.get('WHATSAPP_COST_PER_MESSAGE') || '0.80');
					if (!isNaN(val)) costPerMessage = val;
				}
			}
		} catch (e) {
			console.error('Failed to load saved WhatsApp settings', e);
		}
	}

	async function loadAccountHealth() {
		testingConnection = true;
		try {
			const res = await graphqlQuery<{ getWhatsappAccountStatus: WabaHealthStatus }>(
				GET_WHATSAPP_ACCOUNT_STATUS
			);
			if (res.success && res.data?.getWhatsappAccountStatus) {
				accountStatus = res.data.getWhatsappAccountStatus;
			}
		} catch (e: any) {
			toast.error('Failed to query account health: ' + e.message);
		} finally {
			testingConnection = false;
		}
	}

	async function loadSuppression() {
		loadingSuppression = true;
		try {
			const res = await graphqlQuery<{ getCrmWhatsappSuppressionList: CrmWhatsappSuppression[] }>(
				GET_WHATSAPP_SUPPRESSION_LIST,
				{
					variables: { search: suppressionSearch || null }
				}
			);
			if (res.success && res.data?.getCrmWhatsappSuppressionList) {
				suppressionList = res.data.getCrmWhatsappSuppressionList;
			}
		} catch (e) {
			console.error('Failed to load suppression list', e);
		} finally {
			loadingSuppression = false;
		}
	}

	onMount(() => {
		loadSavedSettings();
		loadAccountHealth();
		loadSuppression();
	});

	async function handleSaveSettings() {
		savingSettings = true;
		try {
			const res = await graphqlMutation(SAVE_WHATSAPP_SETTINGS, {
				variables: {
					input: {
						wabaId: wabaId.trim() || null,
						phoneNumberId: phoneNumberId.trim() || null,
						displayPhoneNumber: displayPhoneNumber.trim() || null,
						accessToken: accessToken.trim() || null,
						appSecret: appSecret.trim() || null,
						webhookVerifyToken: webhookVerifyToken.trim() || null,
						simulationMode: simulationMode,
						costPerMessage: costPerMessage
					}
				}
			});

			if (res.success) {
				toast.success('WhatsApp settings successfully updated.');
				loadAccountHealth();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to save settings.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message);
		} finally {
			savingSettings = false;
		}
	}

	async function handleAddSuppression() {
		if (!newSuppressionPhone.trim()) {
			toast.error('Please enter a mobile number.');
			return;
		}

		addingSuppression = true;
		try {
			const res = await graphqlMutation(ADD_WHATSAPP_SUPPRESSION, {
				variables: {
					phoneNumber: newSuppressionPhone.trim(),
					reason: newSuppressionReason,
					notes: newSuppressionNotes.trim() || null
				}
			});

			if (res.success) {
				toast.success(`Number ${newSuppressionPhone} added to suppression list.`);
				newSuppressionPhone = '';
				newSuppressionNotes = '';
				loadSuppression();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to add number.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message);
		} finally {
			addingSuppression = false;
		}
	}

	async function handleRemoveSuppression(id: string) {
		if (!confirm('Are you sure you want to remove this number from suppression? They will be eligible to receive marketing messages again.')) return;
		try {
			const res = await graphqlMutation(REMOVE_WHATSAPP_SUPPRESSION, {
				variables: { id }
			});
			if (res.success) {
				toast.success('Number removed from suppression list.');
				loadSuppression();
			} else {
				const errMsg = typeof res.error === 'string' ? res.error : (res.error as any)?.message || 'Failed to remove number.';
				toast.error(errMsg);
			}
		} catch (e: any) {
			toast.error(e.message);
		}
	}

	function copyToClipboard(text: string, label: string) {
		navigator.clipboard.writeText(text);
		toast.success(`${label} copied to clipboard!`);
	}
</script>

<div class="max-w-5xl mx-auto space-y-8 pb-16">
	<!-- Page Heading -->
	<PageHeading
		backHref="/crm-whatsapp-campaigns"
		backLabel="Campaigns Hub"
		icon="settings"
		title="WhatsApp Business API Configuration & Compliance"
		description="Manage Meta Cloud API credentials, webhook endpoints, phone number quality health, and suppression opt-out lists."
	/>

	<!-- Section 1: Meta Cloud API Credentials -->
	<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6">
		<div class="flex items-center justify-between border-b border-border pb-4">
			<div>
				<h3 class="text-base font-bold text-foreground flex items-center gap-2">
					<Icon name="shield-check" class="w-5 h-5 text-emerald-600" />
					Meta WhatsApp Business API Credentials
				</h3>
				<p class="text-xs text-muted-foreground mt-0.5">
					Configure credentials generated from your Meta Business Manager and WhatsApp App.
				</p>
			</div>

			<Button
				variant="outline"
				size="sm"
				class="gap-1.5"
				onclick={loadAccountHealth}
				disabled={testingConnection}
			>
				<Icon name="activity" class="w-4 h-4 {testingConnection ? 'animate-spin text-primary' : 'text-emerald-600'}" />
				<span>{testingConnection ? 'Testing...' : 'Test Connection'}</span>
			</Button>
		</div>

		<!-- Live Health Banner -->
		{#if accountStatus}
			<div class="p-4 rounded-xl border {accountStatus.isConnected ? 'bg-emerald-50/50 dark:bg-emerald-950/20 border-emerald-200 dark:border-emerald-900' : 'bg-amber-50/50 dark:bg-amber-950/20 border-amber-200 dark:border-amber-900'} flex flex-col sm:flex-row sm:items-center justify-between gap-4 text-xs">
				<div class="space-y-1">
					<div class="font-bold text-foreground flex items-center gap-2">
						<span class="w-2 h-2 rounded-full {accountStatus.isConnected ? 'bg-emerald-500' : 'bg-amber-500'}"></span>
						<span>{accountStatus.statusMessage || 'Meta Cloud API Status'}</span>
					</div>
					<div class="text-muted-foreground">
						Tier: <strong class="text-foreground">{accountStatus.messagingLimitTier}</strong> • Daily Limit: <strong class="text-foreground">{accountStatus.dailyLimit.toLocaleString()} msgs/day</strong> • Quality: <strong class="text-foreground">{accountStatus.qualityRating}</strong>
					</div>
				</div>

				<div class="text-right">
					<div class="text-muted-foreground">Today's Volume Sent</div>
					<div class="font-black text-sm text-foreground">{accountStatus.currentDayUsage.toLocaleString()}</div>
				</div>
			</div>
		{/if}

		<div class="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
			<div class="space-y-1.5">
				<label class="font-medium text-foreground">WhatsApp Business Account ID (WABA ID)</label>
				<Input bind:value={wabaId} placeholder="e.g. 102938475647382" class="text-xs" />
				<p class="text-[10px] text-muted-foreground">From Meta Business Manager &gt; WhatsApp Accounts.</p>
			</div>

			<div class="space-y-1.5">
				<label class="font-medium text-foreground">Phone Number ID</label>
				<Input bind:value={phoneNumberId} placeholder="e.g. 192837465019283" class="text-xs" />
				<p class="text-[10px] text-muted-foreground">Phone Number ID from Meta App WhatsApp API setup.</p>
			</div>

			<div class="space-y-1.5">
				<label class="font-medium text-foreground">Display Phone Number</label>
				<Input bind:value={displayPhoneNumber} placeholder="+91 98200 12345" class="text-xs" />
				<p class="text-[10px] text-muted-foreground">The verified phone number displayed to customers.</p>
			</div>

			<div class="space-y-1.5">
				<label class="font-medium text-foreground">Marketing Message Cost (INR)</label>
				<Input type="number" step="0.01" bind:value={costPerMessage} class="text-xs" />
				<p class="text-[10px] text-muted-foreground">Meta charge per delivered marketing message (default ₹0.80).</p>
			</div>

			<div class="space-y-1.5 md:col-span-2">
				<label class="font-medium text-foreground">System User Permanent Access Token</label>
				<Input
					type="password"
					bind:value={accessToken}
					placeholder="EAAG... Meta Permanent Token"
					class="text-xs font-mono"
				/>
				<p class="text-[10px] text-muted-foreground">Requires permissions: <code class="bg-muted px-1 rounded">whatsapp_business_messaging</code>, <code class="bg-muted px-1 rounded">whatsapp_business_management</code>.</p>
			</div>

			<div class="space-y-1.5">
				<label class="font-medium text-foreground">App Secret (For Webhook HMAC Verification)</label>
				<Input
					type="password"
					bind:value={appSecret}
					placeholder="Meta App Secret"
					class="text-xs font-mono"
				/>
			</div>

			<div class="space-y-1.5">
				<label class="font-medium text-foreground">Webhook Verification Token</label>
				<Input
					bind:value={webhookVerifyToken}
					placeholder="Secret token for Meta hub.challenge"
					class="text-xs font-mono"
				/>
			</div>

			<div class="md:col-span-2 pt-2">
				<label class="flex items-center gap-3 p-3 bg-muted/40 border border-border rounded-xl cursor-pointer">
					<input type="checkbox" bind:checked={simulationMode} class="rounded text-primary" />
					<div>
						<div class="font-semibold text-xs text-foreground">Enable Sandbox / Simulation Mode</div>
						<div class="text-[11px] text-muted-foreground">
							Simulate WhatsApp campaign dispatches and webhook deliverability without calling live Meta servers or consuming Meta credits. Ideal for staging, team training, and preview tests.
						</div>
					</div>
				</label>
			</div>
		</div>

		<div class="flex justify-end pt-4 border-t border-border">
			<Button
				size="sm"
				class="bg-emerald-600 hover:bg-emerald-700 text-white gap-1.5"
				onclick={handleSaveSettings}
				disabled={savingSettings}
			>
				<Icon name="save" class="w-4 h-4 {savingSettings ? 'animate-spin' : ''}" />
				<span>{savingSettings ? 'Saving...' : 'Save Configuration'}</span>
			</Button>
		</div>
	</div>

	<!-- Section 2: Webhook Endpoint Guide -->
	<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-4 text-xs">
		<h3 class="text-base font-bold text-foreground flex items-center gap-2">
			<Icon name="radio" class="w-5 h-5 text-blue-600" />
			Meta Webhook Configuration (Real-Time Read & Delivery Tracking)
		</h3>
		<p class="text-muted-foreground">
			To receive double-blue-tick read receipts, delivery confirmation, and customer replies, configure this Webhook in your Meta App Dashboard under <strong>WhatsApp &gt; Configuration</strong>:
		</p>

		<div class="grid grid-cols-1 md:grid-cols-2 gap-4 pt-2">
			<div class="space-y-1.5">
				<label class="font-semibold text-foreground">Callback URL</label>
				<div class="flex items-center gap-2">
					<Input value={webhookCallbackUrl} readonly class="text-xs font-mono bg-muted/60" />
					<Button
						variant="outline"
						size="sm"
						class="h-9 px-3 shrink-0"
						onclick={() => copyToClipboard(webhookCallbackUrl, 'Callback URL')}
					>
						<Icon name="copy" class="w-3.5 h-3.5" />
					</Button>
				</div>
			</div>

			<div class="space-y-1.5">
				<label class="font-semibold text-foreground">Verify Token</label>
				<div class="flex items-center gap-2">
					<Input value={webhookVerifyToken} readonly class="text-xs font-mono bg-muted/60" />
					<Button
						variant="outline"
						size="sm"
						class="h-9 px-3 shrink-0"
						onclick={() => copyToClipboard(webhookVerifyToken, 'Verify Token')}
					>
						<Icon name="copy" class="w-3.5 h-3.5" />
					</Button>
				</div>
			</div>
		</div>

		<div class="p-3 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-900 rounded-xl text-blue-800 dark:text-blue-300">
			💡 <strong>Required Webhook Field:</strong> Under Webhook Fields, ensure you subscribe to <code>messages</code>. Meta will automatically send delivery statuses (sent, delivered, read, failed) and inbound customer replies.
		</div>

		<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 pt-3 border-t border-border">
			<div>
				<div class="font-semibold text-foreground">Webhook Diagnostics & Live Audit Trail</div>
				<div class="text-[11px] text-muted-foreground">
					Simulate Meta webhook events, verify HMAC signatures, and inspect live payload logs with 1-click.
				</div>
			</div>
			<Button
				variant="outline"
				size="sm"
				class="gap-1.5 border-blue-300 dark:border-blue-800 bg-blue-50 dark:bg-blue-950/30 text-blue-700 dark:text-blue-300 hover:bg-blue-100 dark:hover:bg-blue-900/50 shrink-0"
				onclick={() => goto('/crm-whatsapp-campaigns/inbox')}
			>
				<Icon name="radio" class="w-3.5 h-3.5 text-blue-600" />
				<span>Open Webhook Diagnostics & Simulator</span>
			</Button>
		</div>
	</div>

	<!-- Section 3: WhatsApp Suppression & STOP Opt-Out List -->
	<div class="bg-card border border-border rounded-2xl p-6 shadow-sm space-y-6 text-xs">
		<div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-b border-border pb-4">
			<div>
				<h3 class="text-base font-bold text-foreground flex items-center gap-2">
					<Icon name="user-x" class="w-5 h-5 text-red-500" />
					WhatsApp Suppression & Opt-Out List
				</h3>
				<p class="text-muted-foreground mt-0.5">
					Customers who replied STOP, unsubscribed, or requested not to be messaged. These numbers are permanently excluded from all WhatsApp campaigns.
				</p>
			</div>

			<div class="text-xs font-semibold px-3 py-1 bg-muted rounded-lg border border-border">
				{suppressionList.length} suppressed numbers
			</div>
		</div>

		<!-- Add Number Manually -->
		<div class="bg-muted/30 border border-border p-4 rounded-xl space-y-3">
			<div class="font-semibold text-foreground">Manually Opt-Out / Suppress Phone Number</div>
			<div class="grid grid-cols-1 sm:grid-cols-4 gap-3">
				<div class="sm:col-span-2">
					<Input
						bind:value={newSuppressionPhone}
						placeholder="Mobile number: e.g. 919876543210"
						class="text-xs"
					/>
				</div>
				<div>
					<select
						bind:value={newSuppressionReason}
						class="w-full py-1.5 px-3 text-xs bg-background border border-input rounded-lg focus:outline-none"
					>
						<option value="UserReplyStop">Customer Replied STOP</option>
						<option value="Unsubscribed">Customer Requested Opt-Out</option>
						<option value="InvalidNumber">Invalid / Inactive WhatsApp</option>
						<option value="RepeatedFailure">Delivery Failure</option>
						<option value="Manual">Manual Fleet Suppression</option>
					</select>
				</div>
				<div>
					<Button
						size="sm"
						class="w-full bg-red-600 hover:bg-red-700 text-white font-medium"
						onclick={handleAddSuppression}
						disabled={addingSuppression}
					>
						<Icon name="user-minus" class="w-3.5 h-3.5 mr-1" />
						<span>Add to Suppression</span>
					</Button>
				</div>
			</div>
		</div>

		<!-- Suppression Table -->
		{#if loadingSuppression}
			<div class="p-8 text-center text-muted-foreground">
				<Icon name="loader-2" class="w-5 h-5 animate-spin mx-auto mb-2 text-primary" />
				Loading suppression list...
			</div>
		{:else if suppressionList.length === 0}
			<div class="p-8 text-center text-muted-foreground">
				No suppressed numbers on file. When users reply STOP, they will appear here automatically.
			</div>
		{:else}
			<div class="overflow-x-auto border border-border rounded-xl">
				<table class="w-full text-left border-collapse">
					<thead class="bg-muted/50 border-b border-border text-muted-foreground uppercase text-[10px]">
						<tr>
							<th class="py-2.5 px-3 font-semibold">Phone Number</th>
							<th class="py-2.5 px-3 font-semibold">Reason</th>
							<th class="py-2.5 px-3 font-semibold">Source</th>
							<th class="py-2.5 px-3 font-semibold">Date Added</th>
							<th class="py-2.5 px-3 font-semibold text-right">Actions</th>
						</tr>
					</thead>
					<tbody class="divide-y divide-border">
						{#each suppressionList as s (s.id)}
							<tr class="hover:bg-muted/20">
								<td class="py-2.5 px-3 font-mono font-semibold text-foreground">
									+{s.phoneNumber}
								</td>
								<td class="py-2.5 px-3">
									<span class="inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-medium bg-red-100 text-red-800 dark:bg-red-950 dark:text-red-300">
										{s.reason}
									</span>
								</td>
								<td class="py-2.5 px-3 text-muted-foreground text-[11px]">
									{s.source}
								</td>
								<td class="py-2.5 px-3 text-muted-foreground text-[11px]">
									{new Date(s.createdAt).toLocaleDateString()}
								</td>
								<td class="py-2.5 px-3 text-right">
									<Button
										variant="ghost"
										size="sm"
										class="h-7 text-xs text-muted-foreground hover:text-red-600"
										onclick={() => handleRemoveSuppression(s.id)}
										title="Remove from suppression"
									>
										<Icon name="trash-2" class="w-3.5 h-3.5" />
									</Button>
								</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	</div>
</div>
