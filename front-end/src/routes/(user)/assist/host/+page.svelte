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
	import MonitorUp from "@lucide/svelte/icons/monitor-up";
	import ShieldCheck from "@lucide/svelte/icons/shield-check";
	import Copy from "@lucide/svelte/icons/copy";
	import Check from "@lucide/svelte/icons/check";
	import MousePointer2 from "@lucide/svelte/icons/mouse-pointer-2";
	import StopCircle from "@lucide/svelte/icons/circle-stop"; // circle-stop or stop-circle
	import Loader2 from "@lucide/svelte/icons/loader-2";
	import Signal from "@lucide/svelte/icons/signal";

	let status = $state<"idle" | "creating" | "sharing" | "error">("idle");
	let errorMessage = $state("");
	let joinCode = $state("");
	let sessionId = $state("");
	let connectionState = $state<string>("");
	let cleanup = $state<(() => void) | null>(null);
	let controlApproved = $state(false);
	let controlBusy = $state(false);
	let copied = $state(false);

	const isTauri = typeof import.meta.env.TAURI_ENV_PLATFORM === "string";

	async function copyJoinCode() {
		if (joinCode) {
			try {
				await navigator.clipboard.writeText(joinCode);
				copied = true;
				Toast.success("Join code copied to clipboard", "Share this with the remote viewer.");
				setTimeout(() => copied = false, 2000);
			} catch (e) {
				Toast.error("Failed to copy", "Please select and copy the code manually.");
			}
		}
	}

	async function startShare() {
		const token = authStore.get().token;
		if (!token) {
			Toast.error("Not signed in.");
			return;
		}
		status = "creating";
		errorMessage = "";
		controlApproved = false;
		copied = false;
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
								deltaY: msg.deltaY ?? 0,
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
				enabled ? "Remote Control Enabled" : "Remote Control Revoked",
				enabled
					? "The viewer can now control your mouse in the desktop app."
					: "Mouse input relay has been disabled.",
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
	<title>Host Session — Tyresoles Assist</title>
</svelte:head>

<div class="flex min-h-[calc(100vh-4rem)] flex-col items-center justify-center p-4 bg-gradient-to-br from-background to-muted/20">
	<div class="w-full max-w-lg space-y-6 animate-in fade-in slide-in-from-bottom-8 duration-500 ease-out bg-card text-card-foreground p-8 rounded-3xl border shadow-sm">
		<div class="flex items-center gap-4 border-b pb-6">
			<div class="p-3 bg-primary/10 rounded-2xl text-primary">
				<MonitorUp class="w-6 h-6" />
			</div>
			<div>
				<h1 class="text-2xl font-bold tracking-tight">Broadcast Screen</h1>
				<p class="text-muted-foreground text-sm">Host a secure, remote assist session.</p>
			</div>
		</div>

		{#if !isTauri}
			<div class="flex items-start gap-3 p-4 bg-blue-500/10 text-blue-800 dark:text-blue-300 rounded-xl text-sm">
				<ShieldCheck class="w-5 h-5 flex-shrink-0 mt-0.5" />
				<p class="leading-relaxed">
					<strong>Note:</strong> You are hosting from a web browser. Screen sharing works perfectly, but remote mouse control from the viewer requires hosting via the Tyresoles Desktop App.
				</p>
			</div>
		{/if}

		{#if errorMessage}
			<div class="p-4 bg-destructive/10 text-destructive text-sm rounded-xl font-medium border border-destructive/20">
				{errorMessage}
			</div>
		{/if}

		{#if status === "idle" || status === "error"}
			<div class="pt-4">
				<Button onclick={startShare} size="lg" class="w-full text-lg h-14 rounded-xl shadow-md">
					Select Screen & Start Sharing
				</Button>
			</div>
		{/if}

		{#if status === "creating"}
			<div class="py-12 flex flex-col items-center justify-center space-y-4 text-muted-foreground">
				<div class="p-4 bg-muted rounded-full animate-pulse">
					<Loader2 class="w-8 h-8 animate-spin text-primary" />
				</div>
				<p class="font-medium">Initializing secure session...</p>
			</div>
		{/if}

		{#if status === "sharing"}
			<div class="space-y-6 animate-in fade-in slide-in-from-bottom-4 pt-2">
				<!-- Live Indicator -->
				<div class="flex items-center justify-center gap-2 px-4 py-2 bg-green-500/10 text-green-700 dark:text-green-400 rounded-xl border border-green-500/20 font-medium text-sm">
					<span class="relative flex h-3 w-3">
						<span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-500 opacity-75"></span>
						<span class="relative inline-flex rounded-full h-3 w-3 bg-green-500"></span>
					</span>
					Screen Sharing Active
				</div>

				<!-- Join Code Card -->
				<div class="bg-muted/40 rounded-2xl p-6 text-center border space-y-3 relative overflow-hidden">
					<div class="absolute inset-0 bg-gradient-to-r from-transparent via-background/20 to-transparent translate-x-[-100%] animate-[shimmer_2s_infinite]"></div>
					<p class="text-sm font-medium text-muted-foreground">Share this Join Code</p>
					<div class="flex items-center justify-center gap-3">
						<span class="font-mono text-5xl font-bold tracking-widest text-foreground selection:bg-primary/20">
							{joinCode}
						</span>
						<Button variant="ghost" size="icon" class="rounded-xl h-12 w-12 text-muted-foreground hover:text-foreground" onclick={copyJoinCode} title="Copy to clipboard">
							{#if copied}
								<Check class="w-6 h-6 text-green-500" />
							{:else}
								<Copy class="w-6 h-6" />
							{/if}
						</Button>
					</div>
					{#if connectionState}
						<div class="flex items-center justify-center gap-1.5 pt-2 text-xs text-muted-foreground font-medium">
							<Signal class={`w-3.5 h-3.5 ${connectionState === 'connected' ? 'text-green-500' : 'animate-pulse'}`} />
							<span>Status: {connectionState}</span>
						</div>
					{/if}
				</div>

				<!-- Controls -->
				<div class="grid gap-3">
					<Button 
						variant={controlApproved ? "outline" : "default"} 
						disabled={controlBusy} 
						onclick={() => toggleRemoteControl(!controlApproved)}
						class={`h-12 rounded-xl text-base transition-all ${controlApproved ? 'border-primary text-primary hover:bg-primary/10' : ''}`}
					>
						<MousePointer2 class="w-5 h-5 mr-3" />
						{#if controlBusy}
							<Loader2 class="w-5 h-5 mr-3 animate-spin absolute" />
						{/if}
						<span class={controlBusy ? 'opacity-0' : ''}>
							{controlApproved ? "Revoke Mouse Control" : "Allow Remote Control"}
						</span>
					</Button>
					
					<Button variant="destructive" onclick={stopShare} class="h-12 rounded-xl text-base font-semibold shadow-sm hover:bg-red-600 transition-colors">
						<StopCircle class="w-5 h-5 mr-2" />
						Stop Sharing
					</Button>
				</div>
			</div>
		{/if}
	</div>
</div>
