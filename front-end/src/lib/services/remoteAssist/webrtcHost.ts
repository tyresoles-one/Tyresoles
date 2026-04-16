import {
	buildRemoteAssistWebSocketUrl,
	parseSignalingMessage,
	stringifySignalingMessage,
	type SignalingMessage,
} from "./signaling";

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
		displayStream = await navigator.mediaDevices.getDisplayMedia({
			video: true,
			audio: true,
		});
	} catch (e) {
		cleanup();
		throw e instanceof Error ? e : new Error("getDisplayMedia failed");
	}

	displayStream.getTracks().forEach((track) => {
		pc.addTrack(track, displayStream!);
	});

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
			} catch (e) {
				callbacks.onError(e instanceof Error ? e.message : "setRemoteDescription answer");
			}
			return;
		}
		if (msg.type === "ice") {
			try {
				await pc.addIceCandidate(msg.candidate);
			} catch {
				/* ignore late candidates */
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
