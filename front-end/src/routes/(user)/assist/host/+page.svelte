<script lang="ts">
	import { onDestroy } from "svelte";
	import { Button } from "$lib/components/ui/button";
	import { authStore } from "$lib/stores/auth";
	import {
		fetchIceServers,
		createAssistSession,
		endAssistSession,
		setRemoteControlApproved,
	} from "$lib/services/remoteAssist/api";
	import { runHostSession } from "$lib/services/remoteAssist/webrtcHost";
	import { Toast } from "$lib/components/venUI/toast";

	let status = $state<"idle" | "creating" | "sharing" | "error">("idle");
	let errorMessage = $state("");
	let joinCode = $state("");
	let sessionId = $state("");
	let connectionState = $state<string>("");
	let cleanup = $state<(() => void) | null>(null);
	let controlApproved = $state(false);
	let controlBusy = $state(false);

	const isTauri = typeof import.meta.env.TAURI_ENV_PLATFORM === "string";

	async function startShare() {
		const token = authStore.get().token;
		if (!token) {
			Toast.error("Not signed in.");
			return;
		}
		status = "creating";
		errorMessage = "";
		controlApproved = false;
		try {
			const ice = await fetchIceServers(token);
			const rtcServers: RTCIceServer[] = ice.iceServers.map((s) => ({
				urls: s.urls,
				...(s.username != null && s.credential != null
					? { username: s.username, credential: s.credential }
					: {}),
			}));
			const session = await createAssistSession(token, {});
			sessionId = session.sessionId;
			joinCode = session.joinCode;

			const host = await runHostSession({
				token,
				sessionId,
				iceServers: rtcServers,
				callbacks: {
					onError: (m) => {
						errorMessage = m;
						status = "error";
					},
					onConnectionState: (s) => {
						connectionState = s;
					},
					onControl: async (msg) => {
						if (!isTauri) return;
						try {
							const { invoke } = await import("@tauri-apps/api/core");
							await invoke("remote_assist_pointer", {
								xNorm: msg.x,
								yNorm: msg.y,
								action: msg.action,
							});
						} catch (e) {
							console.warn("remote_assist_pointer", e);
						}
					},
				},
			});
			cleanup = host.close;
			status = "sharing";
		} catch (e) {
			errorMessage = e instanceof Error ? e.message : "Failed to start";
			status = "error";
		}
	}

	async function toggleRemoteControl(enabled: boolean) {
		const token = authStore.get().token;
		if (!token || !sessionId) return;
		controlBusy = true;
		try {
			await setRemoteControlApproved(token, sessionId, enabled);
			controlApproved = enabled;
			Toast.success(
				enabled ? "Remote control enabled" : "Remote control revoked",
				enabled
					? "The viewer can send mouse events when they enable control on their side."
					: "Control messages will no longer be relayed.",
			);
		} catch (e) {
			Toast.error(e instanceof Error ? e.message : "Could not update remote control.");
		} finally {
			controlBusy = false;
		}
	}

	async function stopShare() {
		const token = authStore.get().token;
		if (cleanup) {
			cleanup();
			cleanup = null;
		}
		if (token && sessionId) {
			try {
				await endAssistSession(token, sessionId);
			} catch {
				/* ignore */
			}
		}
		sessionId = "";
		joinCode = "";
		connectionState = "";
		controlApproved = false;
		status = "idle";
	}

	onDestroy(() => {
		if (cleanup) cleanup();
	});
</script>

<svelte:head>
	<title>Remote assist — Host</title>
</svelte:head>

<div class="mx-auto max-w-lg space-y-4 p-6">
	<h1 class="text-xl font-semibold">Share screen</h1>
	{#if status === "sharing"}
		<div
			class="flex items-center gap-2 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-sm text-amber-950 dark:text-amber-100"
			role="status"
		>
			<span class="inline-block size-2 animate-pulse rounded-full bg-amber-500"></span>
			<strong>Sharing active.</strong> Your screen is visible to the joined viewer. Use Stop when finished.
		</div>
	{/if}
	{#if !isTauri}
		<p class="text-muted-foreground text-sm">
			Remote control (mouse injection) is available in the <strong>Tyresoles desktop app</strong> on Windows. In the
			browser you can still share your screen for viewing.
		</p>
	{/if}
	{#if status === "idle" || status === "error"}
		<Button onclick={startShare}>Start sharing</Button>
	{/if}
	{#if status === "creating"}
		<p class="text-muted-foreground text-sm">Starting…</p>
	{/if}
	{#if status === "sharing" || status === "error"}
		<div class="rounded-md border border-border bg-muted/40 p-3 text-sm">
			<p><span class="text-muted-foreground">Join code:</span> <strong class="font-mono text-lg">{joinCode}</strong></p>
			<p class="mt-1 text-muted-foreground">Share this code with the viewer. Connection: {connectionState || "…"}</p>
		</div>
	{/if}
	{#if status === "sharing"}
		<div class="flex flex-col gap-2 sm:flex-row">
			{#if !controlApproved}
				<Button variant="secondary" disabled={controlBusy} onclick={() => toggleRemoteControl(true)}>
					Allow remote control
				</Button>
			{:else}
				<Button variant="outline" disabled={controlBusy} onclick={() => toggleRemoteControl(false)}>
					Revoke remote control
				</Button>
			{/if}
			<Button variant="destructive" onclick={stopShare}>Stop sharing</Button>
		</div>
		<p class="text-muted-foreground text-xs">
			Approve remote control only for trusted viewers. The viewer must use the control option on the join page after you
			allow.
		</p>
	{/if}
	{#if errorMessage}
		<p class="text-destructive text-sm">{errorMessage}</p>
	{/if}
</div>
