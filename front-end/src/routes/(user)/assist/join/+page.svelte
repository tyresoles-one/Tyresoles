<script lang="ts">
	import { onDestroy } from "svelte";
	import { browser } from "$app/environment";
	import { Button } from "$lib/components/ui/button";
	import { Input } from "$lib/components/ui/input";
	import { authStore } from "$lib/stores/auth";
	import {
		fetchIceServers,
		joinAssistSession,
		endAssistSession,
		getAssistSession,
	} from "$lib/services/remoteAssist/api";
	import { runViewerSession } from "$lib/services/remoteAssist/webrtcViewer";
	import type { SignalingMessage } from "$lib/services/remoteAssist/signaling";
	import { Toast } from "$lib/components/venUI/toast";

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
		try {
			const ice = await fetchIceServers(token);
			const rtcServers: RTCIceServer[] = ice.iceServers.map((s) => ({
				urls: s.urls,
				...(s.username != null && s.credential != null
					? { username: s.username, credential: s.credential }
					: {}),
			}));
			const joined = await joinAssistSession(token, { joinCode: code });
			sessionId = joined.sessionId;

			const { close, sendControl: controlFn } = await runViewerSession({
				token,
				sessionId,
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
		} catch (e) {
			errorMessage = e instanceof Error ? e.message : "Join failed";
			status = "error";
		}
	}

	async function leave() {
		const token = authStore.get().token;
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
	<title>Remote assist — Join</title>
</svelte:head>

<div class="mx-auto max-w-3xl space-y-4 p-6">
	<h1 class="text-xl font-semibold">Join session</h1>
	<p class="text-muted-foreground text-sm">
		Enter the join code from the host. Remote control works only after the host allows it in the desktop app (Windows)
		and you enable it below.
	</p>
	<div class="flex flex-col gap-3 sm:flex-row sm:items-end">
		<div class="flex-1 space-y-1">
			<label for="join-code" class="text-sm font-medium">Join code</label>
			<Input id="join-code" bind:value={joinCodeInput} placeholder="e.g. ABCD1234" class="font-mono uppercase" />
		</div>
		<Button onclick={join} disabled={status === "joining"}>Join</Button>
	</div>
	<label class="flex cursor-pointer items-center gap-2 text-sm">
		<input type="checkbox" bind:checked={controlEnabled} disabled={status !== "connected" || !hostApprovedControl} />
		<span>
			Send mouse control to host
			{#if status === "connected" && !hostApprovedControl}
				<span class="text-muted-foreground">(waiting for host approval…)</span>
			{/if}
		</span>
	</label>
	{#if status === "joining"}
		<p class="text-muted-foreground text-sm">Connecting…</p>
	{/if}
	{#if connectionState}
		<p class="text-muted-foreground text-sm">WebRTC: {connectionState}</p>
	{/if}
	<div
		bind:this={containerEl}
		class="relative aspect-video w-full overflow-hidden rounded-md bg-black touch-none"
		class:ring-2={controlEnabled && hostApprovedControl}
		class:ring-primary={controlEnabled && hostApprovedControl}
		onpointerdown={onPointerDown}
		onpointermove={onPointerMove}
		onpointerup={onPointerUp}
		onpointercancel={onPointerUp}
		role="application"
		aria-label="Remote screen"
	>
		<!-- svelte-ignore a11y_media_has_caption -->
		<video bind:this={videoEl} class="h-full w-full object-contain" autoplay playsinline muted></video>
		{#if controlEnabled && hostApprovedControl}
			<div
				class="pointer-events-none absolute bottom-2 left-2 rounded bg-black/60 px-2 py-1 text-xs text-white"
			>
				Control on — tap/drag on video
			</div>
		{/if}
	</div>
	{#if status === "connected" || status === "error"}
		<Button variant="outline" onclick={leave}>Leave session</Button>
	{/if}
	{#if errorMessage}
		<p class="text-destructive text-sm">{errorMessage}</p>
	{/if}
</div>
