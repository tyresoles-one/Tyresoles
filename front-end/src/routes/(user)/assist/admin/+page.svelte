<script lang="ts">
	import { onMount } from "svelte";
	import { browser } from "$app/environment";
	import { goto } from "$app/navigation";
	import { Button } from "$lib/components/ui/button";
	import { authStore } from "$lib/stores/auth";
	import {
		listActiveAssistSessions,
		type ActiveAssistSessionItem,
	} from "$lib/services/remoteAssist/api";
	import {
		Table,
		TableBody,
		TableCell,
		TableHead,
		TableHeader,
		TableRow,
	} from "$lib/components/ui/table";
	import Loader2 from "@lucide/svelte/icons/loader-2";
	import RefreshCw from "@lucide/svelte/icons/refresh-cw";
	import ArrowLeft from "@lucide/svelte/icons/arrow-left";
	import Shield from "@lucide/svelte/icons/shield";
	import { formatDistanceToNow } from "date-fns";

	let loading = $state(true);
	let forbidden = $state(false);
	let rows = $state<ActiveAssistSessionItem[]>([]);
	let loadError = $state("");

	async function load() {
		const token = authStore.get().token;
		if (!token) {
			loadError = "Not signed in.";
			loading = false;
			return;
		}
		loading = true;
		loadError = "";
		forbidden = false;
		try {
			rows = await listActiveAssistSessions(token);
		} catch (e) {
			const msg = e instanceof Error ? e.message : "Failed to load";
			if (msg === "FORBIDDEN") {
				forbidden = true;
			} else {
				loadError = msg;
			}
			rows = [];
		} finally {
			loading = false;
		}
	}

	function connectTo(row: ActiveAssistSessionItem) {
		void goto(`/assist/join?sessionId=${encodeURIComponent(row.sessionId)}`);
	}

	onMount(() => {
		if (browser) void load();
	});
</script>

<svelte:head>
	<title>Assist — Active host sessions</title>
</svelte:head>

<div class="mx-auto max-w-5xl space-y-6 p-6">
	<div class="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
		<div class="flex items-center gap-3">
			<Button variant="ghost" size="icon" href="/assist" aria-label="Back to assist">
				<ArrowLeft class="h-5 w-5" />
			</Button>
			<div>
				<h1 class="text-xl font-semibold tracking-tight flex items-center gap-2">
					<Shield class="h-6 w-6 text-primary" />
					Active assist sessions
				</h1>
				<p class="text-muted-foreground text-sm">
					Hosts with a live assist session. Select a row to join as viewer without a join code.
				</p>
			</div>
		</div>
		<Button variant="outline" onclick={() => load()} disabled={loading}>
			<RefreshCw class="mr-2 h-4 w-4 {loading ? 'animate-spin' : ''}" />
			Refresh
		</Button>
	</div>

	{#if forbidden}
		<div class="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm">
			<p class="font-medium text-destructive">Access denied</p>
			<p class="text-muted-foreground mt-1">
				Your account is not configured for assist admin (see server
				<code class="rounded bg-muted px-1 py-0.5 text-xs">RemoteAssist:AssistAdminUserTypes</code>
				and
				<code class="rounded bg-muted px-1 py-0.5 text-xs">AssistAdminUserIds</code>).
			</p>
		</div>
	{:else if loadError}
		<div class="rounded-lg border border-destructive/30 bg-destructive/10 p-4 text-sm text-destructive">{loadError}</div>
	{:else if loading}
		<div class="flex items-center justify-center gap-2 py-16 text-muted-foreground">
			<Loader2 class="h-6 w-6 animate-spin" />
			Loading sessions…
		</div>
	{:else if rows.length === 0}
		<p class="text-muted-foreground py-12 text-center">No active assist sessions right now.</p>
	{:else}
		<div class="rounded-md border">
			<Table>
				<TableHeader>
					<TableRow>
						<TableHead>Host</TableHead>
						<TableHead>Join code</TableHead>
						<TableHead>Viewer</TableHead>
						<TableHead>Status</TableHead>
						<TableHead>Expires</TableHead>
						<TableHead class="text-right">Action</TableHead>
					</TableRow>
				</TableHeader>
				<TableBody>
					{#each rows as row (row.sessionId)}
						<TableRow>
							<TableCell class="font-mono text-xs">
								<div class="max-w-[220px] truncate" title={row.hostUserId}>
									{row.hostDisplayName || row.hostUserId}
								</div>
								{#if row.hostDisplayName}
									<div class="text-muted-foreground truncate text-[11px]" title={row.hostUserId}>{row.hostUserId}</div>
								{/if}
							</TableCell>
							<TableCell class="font-mono text-xs">{row.joinCode}</TableCell>
							<TableCell class="font-mono text-xs max-w-[180px] truncate" title={row.viewerUserId ?? "—"}>
								{row.viewerUserId ?? "—"}
							</TableCell>
							<TableCell>{row.status}</TableCell>
							<TableCell class="text-xs text-muted-foreground whitespace-nowrap">
								{formatDistanceToNow(new Date(row.expiresAtUtc), { addSuffix: true })}
							</TableCell>
							<TableCell class="text-right">
								<Button size="sm" onclick={() => connectTo(row)}>Join</Button>
							</TableCell>
						</TableRow>
					{/each}
				</TableBody>
			</Table>
		</div>
	{/if}
</div>
