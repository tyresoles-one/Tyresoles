import {
	buildRemoteAssistWebSocketUrl,
	parseSignalingMessage,
	stringifySignalingMessage,
	type SignalingMessage,
} from "./signaling";
import { Capacitor } from '@capacitor/core';

export type HostCallbacks = {
	onError: (message: string) => void;
	onConnectionState: (state: RTCPeerConnectionState) => void;
	/** Viewer-sent remote control (mouse); handle with Tauri on desktop host. */
	onControl?: (msg: Extract<SignalingMessage, { type: "control" }>) => void;
};

/** Host: shares screen via WebRTC; signaling over WebSocket relay. */
export async function runHostSession(opts: {
	token: string;
	sessionId: string;
	iceServers: RTCIceServer[];
	callbacks: HostCallbacks;
}): Promise<{ close: () => void }> {
	const { token, sessionId, iceServers, callbacks } = opts;
	const pc = new RTCPeerConnection({ iceServers });

	let displayStream: MediaStream | null = null;
	let ws: WebSocket | null = null;

	const cleanup = () => {
		try {
			ws?.close();
		} catch {
			/* ignore */
		}
		displayStream?.getTracks().forEach((t) => t.stop());
		pc.close();
	};

	let isRemoteDescriptionSet = false;
	const pendingCandidates: RTCIceCandidateInit[] = [];

	pc.onconnectionstatechange = () => {
		callbacks.onConnectionState(pc.connectionState);
	};

	pc.onicecandidate = (ev) => {
		if (ev.candidate && ws?.readyState === WebSocket.OPEN) {
			ws.send(
				stringifySignalingMessage({
					type: "ice",
					candidate: ev.candidate.toJSON(),
				}),
			);
		}
	};

	let offerCreated = false;
	async function createOfferIfNeeded() {
		if (offerCreated) return;
		offerCreated = true;
		try {
			const offer = await pc.createOffer();
			await pc.setLocalDescription(offer);
			if (ws?.readyState === WebSocket.OPEN && pc.localDescription) {
				ws.send(
					stringifySignalingMessage({
						type: "offer",
						sdp: pc.localDescription.sdp ?? "",
					}),
				);
			}
		} catch (e) {
			callbacks.onError(e instanceof Error ? e.message : "createOffer failed");
		}
	}

	try {
		if (Capacitor.isNativePlatform()) {
			// Mobile Native Host - Route completely through specialized WebRTC Plugin
			const { TyresolesWebRTCHost } = await import('./plugin/TyresolesWebRTCHost');
			
			await TyresolesWebRTCHost.addListener('onError', (err) => callbacks.onError(err.message));
			await TyresolesWebRTCHost.addListener('onConnectionStateChange', (s) => callbacks.onConnectionState(s.state as any));
			await TyresolesWebRTCHost.addListener('onBroadcastStopped', () => {
				callbacks.onError("Screen sharing was stopped interactively");
				cleanup();
			});

			await TyresolesWebRTCHost.initializeSession({
				token,
				sessionId,
				iceServers: iceServers as { urls: string; username?: string; credential?: string }[]
			});

			const bResult = await TyresolesWebRTCHost.startBroadcast();
			if (!bResult.success) {
				throw new Error(bResult.error ?? "Failed to initialize mobile broadcast");
			}

			// We bypass standard JS WebSockets because the native C++ WebRTC engine handles signaling internally in Route A architecture
			return {
				close: () => {
					void TyresolesWebRTCHost.stopBroadcast();
					cleanup();
				}
			};
		} else {
			// Web / Desktop Host
			displayStream = await navigator.mediaDevices.getDisplayMedia({
				video: true,
				audio: true,
			});

			displayStream.getTracks().forEach((track) => {
				pc.addTrack(track, displayStream!);
				track.onended = () => {
					callbacks.onError("Screen sharing was stopped interactively");
					cleanup();
				};
			});
		}
	} catch (e) {
		cleanup();
		throw e instanceof Error ? e : new Error("Screen capture initialization failed");
	}

	const wsUrl = buildRemoteAssistWebSocketUrl(token, sessionId, "host");
	ws = new WebSocket(wsUrl);

	ws.onmessage = async (ev) => {
		const msg = parseSignalingMessage(String(ev.data));
		if (!msg) return;
		if (msg.type === "ready") {
			await createOfferIfNeeded();
			return;
		}
		if (msg.type === "answer") {
			try {
				await pc.setRemoteDescription({ type: "answer", sdp: msg.sdp });
				isRemoteDescriptionSet = true;
				pendingCandidates.forEach(c => pc.addIceCandidate(c).catch(() => {}));
				pendingCandidates.length = 0;
			} catch (e) {
				callbacks.onError(e instanceof Error ? e.message : "setRemoteDescription answer");
			}
			return;
		}
		if (msg.type === "ice") {
			if (isRemoteDescriptionSet) {
				try {
					await pc.addIceCandidate(msg.candidate);
				} catch {
					/* ignore */
				}
			} else {
				pendingCandidates.push(msg.candidate);
			}
			return;
		}
		if (msg.type === "control") {
			callbacks.onControl?.(msg);
		}
	};

	ws.onerror = () => callbacks.onError("WebSocket error");
	ws.onclose = () => {
		if (pc.connectionState !== "closed") {
			callbacks.onError("Signaling closed");
		}
	};

	await new Promise<void>((resolve, reject) => {
		ws!.onopen = () => resolve();
		ws!.onerror = () => reject(new Error("WS connect failed"));
	});

	return {
		close: cleanup,
	};
}
