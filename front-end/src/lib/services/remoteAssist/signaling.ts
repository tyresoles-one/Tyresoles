import { getBackendBaseUrl } from "$lib/config/system";

/** WebSocket URL for WebRTC signaling (JWT in query — browsers cannot set WS headers). */
export function buildRemoteAssistWebSocketUrl(
	token: string,
	sessionId: string,
	role: "host" | "viewer",
): string {
	const base = getBackendBaseUrl();
	const u = new URL(base);
	const wsProto = u.protocol === "https:" ? "wss:" : "ws:";
	const qs = new URLSearchParams({
		access_token: token,
		sessionId,
		role,
	});
	return `${wsProto}//${u.host}/ws/remote-assist?${qs.toString()}`;
}

export type SignalingMessage =
	| { type: "ready" }
	| { type: "offer"; sdp: string }
	| { type: "answer"; sdp: string }
	| { type: "ice"; candidate: RTCIceCandidateInit }
	| {
			type: "control";
			action: "move" | "down" | "up" | "wheel";
			x: number;
			y: number;
			button?: number;
			deltaY?: number;
	  };

export function parseSignalingMessage(data: string): SignalingMessage | null {
	try {
		return JSON.parse(data) as SignalingMessage;
	} catch {
		return null;
	}
}

export function stringifySignalingMessage(msg: SignalingMessage): string {
	return JSON.stringify(msg);
}
