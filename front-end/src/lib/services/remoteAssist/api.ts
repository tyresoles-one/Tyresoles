import { getBackendBaseUrl } from "$lib/config/system";

export type IceServerDto = {
	urls: string;
	username?: string;
	credential?: string;
};

export type IceServersResponse = {
	iceServers: IceServerDto[];
};

export type CreateSessionResponse = {
	sessionId: string;
	joinCode: string;
	expiresAtUtc: string;
	webSocketPath: string;
};

export type JoinSessionResponse = {
	sessionId: string;
	hostUserId: string;
	expiresAtUtc: string;
	webSocketPath: string;
};

function apiUrl(path: string): string {
	return `${getBackendBaseUrl()}${path}`;
}

export async function fetchIceServers(token: string): Promise<IceServersResponse> {
	const res = await fetch(apiUrl("/api/remote-assist/ice"), {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`ICE config failed: ${res.status}`);
	return res.json() as Promise<IceServersResponse>;
}

export async function createAssistSession(
	token: string,
	body: { hostDisplayName?: string },
): Promise<CreateSessionResponse> {
	const res = await fetch(apiUrl("/api/remote-assist/sessions"), {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify(body),
	});
	if (!res.ok) throw new Error(`Create session failed: ${res.status}`);
	return res.json() as Promise<CreateSessionResponse>;
}

async function readApiError(res: Response, fallback: string): Promise<string> {
	try {
		const j = (await res.json()) as { error?: string };
		if (j?.error) return j.error;
	} catch {
		/* ignore */
	}
	return fallback;
}

export async function joinAssistSession(
	token: string,
	body: { joinCode: string; viewerDisplayName?: string },
): Promise<JoinSessionResponse> {
	const res = await fetch(apiUrl("/api/remote-assist/sessions/join"), {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify(body),
	});
	if (!res.ok)
		throw new Error(await readApiError(res, `Join failed: ${res.status}`));
	return res.json() as Promise<JoinSessionResponse>;
}

export type ActiveAssistSessionItem = {
	sessionId: string;
	joinCode: string;
	hostUserId: string;
	hostDisplayName: string | null;
	status: string;
	viewerUserId: string | null;
	expiresAtUtc: string;
	createdAtUtc: string;
};

/** Lists non-ended assist sessions. Returns 403 if the caller is not configured as an assist admin. */
export async function listActiveAssistSessions(
	token: string,
): Promise<ActiveAssistSessionItem[]> {
	const res = await fetch(apiUrl("/api/remote-assist/sessions/active"), {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (res.status === 403) throw new Error("FORBIDDEN");
	if (!res.ok) throw new Error(`List active sessions failed: ${res.status}`);
	return res.json() as Promise<ActiveAssistSessionItem[]>;
}

/** Join as viewer by session id (assist admins only; replaces current viewer). */
export async function adminJoinAssistSession(
	token: string,
	sessionId: string,
	body?: { viewerDisplayName?: string },
): Promise<JoinSessionResponse> {
	const res = await fetch(apiUrl(`/api/remote-assist/sessions/${sessionId}/admin-join`), {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify(body ?? {}),
	});
	if (!res.ok)
		throw new Error(await readApiError(res, `Admin join failed: ${res.status}`));
	return res.json() as Promise<JoinSessionResponse>;
}

export async function endAssistSession(token: string, sessionId: string): Promise<void> {
	const res = await fetch(apiUrl(`/api/remote-assist/sessions/${sessionId}/end`), {
		method: "POST",
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`End session failed: ${res.status}`);
}

export type AssistSessionStatus = {
	sessionId: string;
	joinCode: string;
	status: string;
	hostUserId: string;
	viewerUserId: string | null;
	expiresAtUtc: string;
	endedAtUtc: string | null;
	controlApprovedAtUtc: string | null;
};

export async function getAssistSession(
	token: string,
	sessionId: string,
): Promise<AssistSessionStatus> {
	const res = await fetch(apiUrl(`/api/remote-assist/sessions/${sessionId}`), {
		headers: { Authorization: `Bearer ${token}` },
	});
	if (!res.ok) throw new Error(`Get session failed: ${res.status}`);
	return res.json() as Promise<AssistSessionStatus>;
}

export async function setRemoteControlApproved(
	token: string,
	sessionId: string,
	enabled: boolean,
): Promise<void> {
	const res = await fetch(apiUrl(`/api/remote-assist/sessions/${sessionId}/remote-control`), {
		method: "POST",
		headers: {
			Authorization: `Bearer ${token}`,
			"Content-Type": "application/json",
		},
		body: JSON.stringify({ enabled }),
	});
	if (!res.ok) throw new Error(`Remote control setting failed: ${res.status}`);
}
