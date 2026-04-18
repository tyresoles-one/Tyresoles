<script lang="ts">
	import { onDestroy, onMount } from "svelte";
	import { browser } from "$app/environment";
	import { page } from "$app/stores";
	import { get } from "svelte/store";
	import { Button } from "$lib/components/ui/button";
	import { Input } from "$lib/components/ui/input";
	import { authStore } from "$lib/stores/auth";
	import {
		adminJoinAssistSession,
		fetchIceServers,
		joinAssistSession,
		endAssistSession,
		getAssistSession,
	} from "$lib/services/remoteAssist/api";
	import { runViewerSession } from "$lib/services/remoteAssist/webrtcViewer";
	import type { SignalingMessage } from "$lib/services/remoteAssist/signaling";
	import { Toast } from "$lib/components/venUI/toast";
	import Maximize from "@lucide/svelte/icons/maximize";
	import Minimize from "@lucide/svelte/icons/minimize";
	import MousePointer2 from "@lucide/svelte/icons/mouse-pointer-2";
	import LogOut from "@lucide/svelte/icons/log-out";
	import Loader2 from "@lucide/svelte/icons/loader-2";
	import AlertCircle from "@lucide/svelte/icons/alert-circle";
	import Signal from "@lucide/svelte/icons/signal";

	let joinCodeInput = $state("");
	let status = $state<"idle" | "joining" | "connected" | "error">("idle");
	let errorMessage = $state("");
	let sessionId = $state("");
	let connectionState = $state("");
	let videoEl = $state<HTMLVideoElement | null>(null);
	let containerEl = $state<HTMLDivElement | null>(null);
	let cleanup = $state<(() => void) | null>(null);
	let sendControl = $state<((msg: Extract<SignalingMessage, { type: "control" }>) => void) | null>(null);
	let controlEnabled = $state(false);
	let hostApprovedControl = $state(false);
	let isFullscreen = $state(false);

	const guidRe =
		/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/;

	async function startViewerWebRtc(connectedSessionId: string) {
		const token = authStore.get().token;
		if (!token) throw new Error("Not signed in.");
		const ice = await fetchIceServers(token);
		const rtcServers: RTCIceServer[] = ice.iceServers.map((s) => ({
			urls: s.urls,
			...(s.username != null && s.credential != null
				? { username: s.username, credential: s.credential }
				: {}),
		}));
		sessionId = connectedSessionId;
		const { close, sendControl: controlFn } = await runViewerSession({
			token,
			sessionId: connectedSessionId,
			iceServers: rtcServers,
			callbacks: {
				onRemoteStream: (stream) => {
					if (videoEl) {
						videoEl.srcObject = stream;
						void videoEl.play().catch(() => {});
					}
				},
				onError: (m) => {
					errorMessage = m;
					status = "error";
				},
				onConnectionState: (s) => {
					connectionState = s;
					if (s === "connected") status = "connected";
				},
			},
		});
		cleanup = close;
		sendControl = controlFn;
		status = "connected";
	}

	function pointerNorm(e: PointerEvent, el: HTMLElement) {
		const r = el.getBoundingClientRect();
		const x = (e.clientX - r.left) / r.width;
		const y = (e.clientY - r.top) / r.height;
		return {
			x: Math.min(1, Math.max(0, x)),
			y: Math.min(1, Math.max(0, y)),
		};
	}

	function onPointerDown(e: PointerEvent) {
		if (!controlEnabled || !hostApprovedControl || !sendControl || !containerEl) return;
		e.preventDefault();
		(containerEl as HTMLElement).setPointerCapture(e.pointerId);
		const { x, y } = pointerNorm(e, containerEl);
		sendControl({ type: "control", action: "down", x, y, button: e.button });
	}

	function onPointerMove(e: PointerEvent) {
		if (!controlEnabled || !hostApprovedControl || !sendControl || !containerEl) return;
		const { x, y } = pointerNorm(e, containerEl);
		sendControl({ type: "control", action: "move", x, y });
	}

	function onPointerUp(e: PointerEvent) {
		if (!controlEnabled || !hostApprovedControl || !sendControl || !containerEl) return;
		const { x, y } = pointerNorm(e, containerEl);
		sendControl({ type: "control", action: "up", x, y, button: e.button });
		try {
			(containerEl as HTMLElement).releasePointerCapture(e.pointerId);
		} catch {
			/* ignore */
		}
	}

	function onWheel(e: WheelEvent) {
		if (!controlEnabled || !hostApprovedControl || !sendControl || !containerEl) return;
		e.preventDefault();
		const { x, y } = pointerNorm(e, containerEl);
		sendControl({ type: "control", action: "wheel", x, y, deltaY: Math.sign(e.deltaY) });
	}

	function toggleFullscreen() {
		if (!document.fullscreenElement && containerEl) {
			containerEl.requestFullscreen().catch(() => {});
		} else if (document.fullscreenElement) {
			document.exitFullscreen().catch(() => {});
		}
	}

	$effect(() => {
		if (!browser) return;
		const handleFsChange = () => {
			isFullscreen = !!document.fullscreenElement;
		};
		document.addEventListener("fullscreenchange", handleFsChange);
		return () => document.removeEventListener("fullscreenchange", handleFsChange);
	});

	$effect(() => {
		if (!browser || !sessionId || status !== "connected") {
			hostApprovedControl = false;
			return;
		}
		const token = authStore.get().token;
		if (!token) return;

		let cancelled = false;
		async function poll() {
			try {
				const s = await getAssistSession(token, sessionId);
				if (!cancelled) {
					hostApprovedControl = !!s.controlApprovedAtUtc;
				}
			} catch {
				/* ignore */
			}
		}
		void poll();
		const id = setInterval(poll, 2000);
		return () => {
			cancelled = true;
			clearInterval(id);
		};
	});

	async function join() {
		const token = authStore.get().token;
		if (!token) {
			Toast.error("Not signed in.");
			return;
		}
		const code = joinCodeInput.trim().toUpperCase();
		if (code.length < 4) {
			Toast.error("Enter a valid join code.");
			return;
		}
		status = "joining";
		errorMessage = "";
		hostApprovedControl = false;
		controlEnabled = false;
		try {
			const joined = await joinAssistSession(token, { joinCode: code });
			await startViewerWebRtc(joined.sessionId);
		} catch (e) {
			errorMessage = e instanceof Error ? e.message : "Join failed";
			status = "error";
		}
	}

	onMount(() => {
		if (!browser) return;
		const sid = get(page).url.searchParams.get("sessionId")?.trim();
		if (!sid || !guidRe.test(sid)) return;
		const token = authStore.get().token;
		if (!token) {
			Toast.error("Not signed in.");
			return;
		}
		status = "joining";
		errorMessage = "";
		hostApprovedControl = false;
		controlEnabled = false;
		void (async () => {
			try {
				const joined = await adminJoinAssistSession(token, sid);
				await startViewerWebRtc(joined.sessionId);
			} catch (e) {
				errorMessage = e instanceof Error ? e.message : "Admin join failed";
				status = "error";
			}
		})();
	});

	async function leave() {
		const token = authStore.get().token;
		if (document.fullscreenElement) {
			await document.exitFullscreen().catch(() => {});
		}
		if (cleanup) {
			cleanup();
			cleanup = null;
		}
		sendControl = null;
		if (videoEl) videoEl.srcObject = null;
		if (token && sessionId) {
			try {
				await endAssistSession(token, sessionId);
			} catch {
				/* ignore */
			}
		}
		sessionId = "";
		connectionState = "";
		hostApprovedControl = false;
		status = "idle";
	}

	onDestroy(() => {
		if (cleanup) cleanup();
	});
</script>

<svelte:head>
	<title>Join Session — Tyresoles Assist</title>
</svelte:head>

{#if status !== "connected"}
<div class="flex min-h-[calc(100vh-4rem)] flex-col items-center justify-center p-4 bg-gradient-to-br from-background to-muted/20">
	<div class="w-full max-w-md space-y-8 animate-in fade-in slide-in-from-bottom-8 duration-500 ease-out bg-card text-card-foreground p-8 rounded-3xl border shadow-sm">
		<div class="text-center space-y-2">
			<h1 class="text-3xl font-bold tracking-tight">Join Session</h1>
			<p class="text-muted-foreground text-sm leading-relaxed">
				{#if $page.url.searchParams.get("sessionId")}
					Connecting as assist admin (session id in URL). Otherwise enter the join code from the host.
				{:else}
					Enter the join code provided by the host to view their screen.
				{/if}
			</p>
		</div>

		<div class="space-y-4 pt-4">
			<div class="space-y-2">
				<label for="join-code" class="text-sm font-medium pl-1">Join Code</label>
				<Input 
					id="join-code" 
					bind:value={joinCodeInput} 
					placeholder="e.g. L93J569F" 
					class="font-mono uppercase h-14 text-center text-xl tracking-widest rounded-xl" 
					maxlength={12} 
					onkeydown={(e) => e.key === 'Enter' && join()}
				/>
			</div>
			
			<Button onclick={join} disabled={status === "joining"} class="w-full h-14 text-lg rounded-xl transition-all mt-2">
				{#if status === "joining"}
					<Loader2 class="w-5 h-5 mr-2 animate-spin" /> Connecting to Host...
				{:else}
					Join Session
				{/if}
			</Button>

			{#if errorMessage}
				<div class="flex items-center gap-3 p-4 text-sm text-destructive bg-destructive/10 rounded-xl mt-4 animate-in fade-in slide-in-from-top-2">
					<AlertCircle class="w-5 h-5 flex-shrink-0" />
					<p class="font-medium">{errorMessage}</p>
				</div>
			{/if}
			{#if connectionState && connectionState !== "connected"}
				<div class="flex items-center justify-center gap-2 mt-4 text-muted-foreground text-sm">
					<Signal class="w-4 h-4 animate-pulse" />
					<span>WebRTC: {connectionState}...</span>
				</div>
			{/if}
		</div>
	</div>
</div>
{:else}
<!-- Full Screen Viewer UI Base -->
<div 
	bind:this={containerEl}
	class="fixed inset-0 z-50 bg-black touch-none flex flex-col overflow-hidden outline-none"
	class:ring-4={controlEnabled && hostApprovedControl}
	class:ring-primary={controlEnabled && hostApprovedControl}
	onpointerdown={onPointerDown}
	onpointermove={onPointerMove}
	onpointerup={onPointerUp}
	onpointercancel={onPointerUp}
	onwheel={onWheel}
	role="application"
	aria-label="Remote Screen View"
	tabindex="-1"
>
	<!-- Video Feed -->
	<!-- svelte-ignore a11y_media_has_caption -->
	<video bind:this={videoEl} class="w-full h-full object-contain pointer-events-none" autoplay playsinline muted></video>

	<!-- Header / Top Bar (Floating) -->
	<div class="absolute top-0 left-0 right-0 p-4 flex items-start justify-between pointer-events-none z-10 bg-gradient-to-b from-black/60 to-transparent pb-10">
		<div class="flex flex-col gap-1 pointer-events-auto">
			<div class="flex items-center gap-2 bg-black/50 backdrop-blur-md rounded-full px-4 py-1.5 border border-white/10 text-white/90 text-sm font-medium">
				<Signal class="w-4 h-4 text-green-400" />
				<span>Connected to Host</span>
			</div>
			{#if status === "connected" && !hostApprovedControl}
				<div class="pl-2.5 text-xs text-white/60">Host has not approved remote control</div>
			{/if}
		</div>
	</div>

	<!-- Bottom Toolbar (Floating) -->
	<div class="absolute bottom-6 left-1/2 -translate-x-1/2 pointer-events-auto z-20 flex items-center gap-2 bg-zinc-900/90 backdrop-blur-xl border border-white/10 p-2 rounded-2xl shadow-2xl transition-all duration-300 hover:bg-zinc-900">
		
		<Button 
			variant="ghost" 
			size="icon" 
			class={`rounded-xl w-12 h-12 transition-all ${controlEnabled ? 'bg-primary text-primary-foreground hover:bg-primary/90' : 'text-white/70 hover:bg-white/10 hover:text-white'}`}
			disabled={!hostApprovedControl}
			onclick={() => controlEnabled = !controlEnabled}
			title={!hostApprovedControl ? "Waiting for host approval" : "Toggle Mouse Control"}
		>
			<MousePointer2 class="w-5 h-5" />
		</Button>

		<div class="w-px h-8 bg-white/10 mx-1"></div>

		<Button 
			variant="ghost" 
			size="icon" 
			class="rounded-xl w-12 h-12 text-white/70 hover:bg-white/10 hover:text-white transition-all"
			onclick={toggleFullscreen}
			title="Toggle Fullscreen"
		>
			{#if isFullscreen}
				<Minimize class="w-5 h-5" />
			{:else}
				<Maximize class="w-5 h-5" />
			{/if}
		</Button>

		<div class="w-px h-8 bg-white/10 mx-1"></div>

		<Button 
			variant="destructive" 
			class="rounded-xl h-12 px-5 font-semibold transition-all hover:bg-red-600 shadow-sm"
			onclick={leave}
		>
			<LogOut class="w-4 h-4 mr-2" />
			Leave
		</Button>
	</div>

	<!-- Control Active Indicator -->
	{#if controlEnabled && hostApprovedControl}
		<div class="pointer-events-none absolute top-4 right-4 bg-primary text-primary-foreground font-semibold px-4 py-2 rounded-full shadow-lg text-sm flex items-center gap-2 animate-in slide-in-from-right-4 fade-in">
			<MousePointer2 class="w-4 h-4 animate-pulse" />
			Control Active
		</div>
	{/if}
</div>
{/if}
