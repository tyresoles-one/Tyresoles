import {
	buildRemoteAssistWebSocketUrl,
	parseSignalingMessage,
	stringifySignalingMessage,
	type SignalingMessage,
} from "./signaling";

export type ViewerCallbacks = {
	onRemoteStream: (stream: MediaStream) => void;
	onError: (message: string) => void;
	onConnectionState: (state: RTCPeerConnectionState) => void;
};

/** Viewer: receives remote screen; sends `ready` then handles offer/answer/ICE. */
export async function runViewerSession(opts: {
	token: string;
	sessionId: string;
	iceServers: RTCIceServer[];
	callbacks: ViewerCallbacks;
}): Promise<{
	close: () => void;
	sendControl: (msg: Extract<SignalingMessage, { type: "control" }>) => void;
}> {
	const { token, sessionId, iceServers, callbacks } = opts;
	const wsUrl = buildRemoteAssistWebSocketUrl(token, sessionId, "viewer");
	const ws = new WebSocket(wsUrl);
	const pc = new RTCPeerConnection({ iceServers });

	const cleanup = () => {
		try {
			ws.close();
		} catch {
			/* ignore */
		}
		pc.close();
	};

	pc.ontrack = (ev) => {
		if (ev.streams[0]) {
			callbacks.onRemoteStream(ev.streams[0]);
		}
	};

	let isRemoteDescriptionSet = false;
	const pendingCandidates: RTCIceCandidateInit[] = [];

	pc.onconnectionstatechange = () => {
		callbacks.onConnectionState(pc.connectionState);
	};

	pc.onicecandidate = (ev) => {
		if (ev.candidate && ws.readyState === WebSocket.OPEN) {
			ws.send(
				stringifySignalingMessage({
					type: "ice",
					candidate: ev.candidate.toJSON(),
				}),
			);
		}
	};

	ws.onmessage = async (ev) => {
		const msg = parseSignalingMessage(String(ev.data));
		if (!msg) return;
		if (msg.type === "offer") {
			try {
				await pc.setRemoteDescription({ type: "offer", sdp: msg.sdp });
				isRemoteDescriptionSet = true;
				pendingCandidates.forEach((c) => pc.addIceCandidate(c).catch(() => {}));
				pendingCandidates.length = 0;

				const answer = await pc.createAnswer();
				await pc.setLocalDescription(answer);
				if (ws.readyState === WebSocket.OPEN && pc.localDescription) {
					ws.send(
						stringifySignalingMessage({
							type: "answer",
							sdp: pc.localDescription.sdp ?? "",
						}),
					);
				}
			} catch (e) {
				callbacks.onError(e instanceof Error ? e.message : "Answer failed");
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
		}
	};

	ws.onerror = () => callbacks.onError("WebSocket error");

	await new Promise<void>((resolve, reject) => {
		ws.onopen = () => resolve();
		ws.onerror = () => reject(new Error("WS connect failed"));
	});

	ws.send(stringifySignalingMessage({ type: "ready" }));

	function sendControl(msg: Extract<SignalingMessage, { type: "control" }>) {
		if (ws.readyState === WebSocket.OPEN) {
			ws.send(stringifySignalingMessage(msg));
		}
	}

	return { close: cleanup, sendControl };
}
