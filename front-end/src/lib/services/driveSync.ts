import { get } from "svelte/store";
import { getBackendBaseUrl, getGraphQLEndpoint } from "$lib/config/system";
import { authStore } from "$lib/stores/auth";

export type DriveSyncUploadCredentials = {
	accessToken: string;
	expiresAtUtc: string;
	folderId: string;
};

export type DriveSyncPreparedUploadSession = {
	uploadUrl: string;
	parentFolderId: string;
	expiresAtUtc: string;
};

export type DriveSyncBackupFileInfo = {
	id: string;
	name: string;
	size?: number | null;
	mimeType?: string | null;
	modifiedTimeUtc?: string | null;
};

export type DriveSyncUserConfig = {
	isActive: boolean;
	targetFolderId: string;
	quotaBytes: string;
	allowedExtensionsJson?: string | null;
};

function authHeader(): HeadersInit {
	const token = get(authStore).token;
	return token ? { Authorization: `Bearer ${token}` } : {};
}

async function gql<T>(query: string, variables?: Record<string, unknown>): Promise<T> {
	const res = await fetch(getGraphQLEndpoint(), {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			...authHeader(),
		},
		body: JSON.stringify({ query, variables }),
	});
	const json = (await res.json()) as {
		data?: T;
		errors?: { message?: string }[];
	};
	if (!res.ok) {
		throw new Error(`GraphQL HTTP ${res.status}`);
	}
	if (json.errors?.length) {
		throw new Error(json.errors.map((e) => e.message ?? "Error").join("; "));
	}
	if (!json.data) {
		throw new Error("Empty GraphQL response");
	}
	return json.data;
}

export async function getDriveSyncConfigQuery(): Promise<DriveSyncUserConfig | null> {
	const data = await gql<{
		getDriveSyncConfig: {
			isActive: boolean;
			targetFolderId: string;
			quotaBytes: unknown;
			allowedExtensionsJson?: string | null;
		} | null;
	}>(
		`query { getDriveSyncConfig(targetUserId: null) {
      isActive
      targetFolderId
      quotaBytes
      allowedExtensionsJson
    } }`,
	);
	const c = data.getDriveSyncConfig;
	if (!c) return null;
	return {
		isActive: c.isActive,
		targetFolderId: c.targetFolderId,
		quotaBytes: String(c.quotaBytes ?? "0"),
		allowedExtensionsJson: c.allowedExtensionsJson,
	};
}

export async function requestDriveSyncUploadCredentials(
	requestedUploadBytes: number,
): Promise<DriveSyncUploadCredentials> {
	const data = await gql<{
		requestDriveSyncUploadCredentials: DriveSyncUploadCredentials;
	}>(
		`mutation ($bytes: Long!) {
      requestDriveSyncUploadCredentials(requestedUploadBytes: $bytes) {
        accessToken
        expiresAtUtc
        folderId
      }
    }`,
		{ bytes: requestedUploadBytes },
	);
	return data.requestDriveSyncUploadCredentials;
}

export async function prepareDriveSyncUploadSession(
	fileName: string,
	fileSizeBytes: number,
	relativePath = "",
	mimeType?: string,
	explicitToken?: string,
): Promise<DriveSyncPreparedUploadSession> {
	const token = explicitToken || get(authStore).token;
	if (!token) throw new Error("Not logged in (prepareDriveSyncUploadSession)");

	const url = `${getBackendBaseUrl()}/api/drive-sync/prepare-upload`;
	const res = await fetch(url, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			Authorization: `Bearer ${token}`,
		},
		body: JSON.stringify({
			relativePath,
			fileName,
			fileSizeBytes,
			mimeType,
		}),
	});

	const body = (await res.json().catch(() => ({}))) as
		| DriveSyncPreparedUploadSession
		| { error?: string };
	if (!res.ok) {
		throw new Error(`Prepare upload failed (${res.status}): ${"error" in body ? (body.error ?? "Unknown error") : "Unknown error"}`);
	}
	return body as DriveSyncPreparedUploadSession;
}

export async function getDriveSyncBackupFiles(): Promise<DriveSyncBackupFileInfo[]> {
	const data = await gql<{ getDriveSyncBackupFiles: DriveSyncBackupFileInfo[] }>(
		`query {
      getDriveSyncBackupFiles {
        id
        name
        size
        mimeType
        modifiedTimeUtc
      }
    }`,
	);
	return data.getDriveSyncBackupFiles ?? [];
}

/** Build set of allowed extensions (lowercase, no dot) from Nav field e.g. ".pdf, docx" or '["pdf"]'. */
export function parseAllowedExtensions(allowed: string | null | undefined): Set<string> | null {
	if (!allowed || !allowed.trim()) return null;
	const s = allowed.trim();
	if (s.startsWith("[")) {
		try {
			const arr = JSON.parse(s) as unknown;
			if (!Array.isArray(arr)) return new Set();
			return new Set(arr.map((x) => String(x).toLowerCase().replace(/^\./, "")));
		} catch {
			return new Set();
		}
	}
	return new Set(
		s
			.split(/[,;\s]+/)
			.map((x) => x.trim().toLowerCase().replace(/^\./, ""))
			.filter(Boolean),
	);
}

export function isExtensionAllowed(fileName: string, allowed: Set<string> | null): boolean {
	if (!allowed || allowed.size === 0) return true;
	const dot = fileName.lastIndexOf(".");
	const ext = dot >= 0 ? fileName.slice(dot + 1).toLowerCase() : "";
	return allowed.has(ext);
}

/**
 * Hybrid upload: browser → Google multipart upload using SA token from API.
 */
export async function uploadFileToGoogleDrive(
	file: File,
	creds: DriveSyncUploadCredentials,
): Promise<void> {
	const boundary = "tyresoles_" + crypto.randomUUID().replace(/-/g, "");
	const metadata = JSON.stringify({
		name: file.name,
		parents: [creds.folderId],
	});
	const head =
		`--${boundary}\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n${metadata}\r\n` +
		`--${boundary}\r\nContent-Type: ${file.type || "application/octet-stream"}\r\n\r\n`;
	const tail = `\r\n--${boundary}--`;

	const blob = new Blob([head, file, tail]);
	const res = await fetch(
		"https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&supportsAllDrives=true",
		{
			method: "POST",
			headers: {
				Authorization: `Bearer ${creds.accessToken}`,
				"Content-Type": `multipart/related; boundary=${boundary}`,
			},
			body: blob,
		},
	);
	if (!res.ok) {
		const t = await res.text();
		throw new Error(`Google Drive upload failed (${res.status}): ${t.slice(0, 500)}`);
	}
}

export async function uploadFileWithResumableSession(
	file: File,
	session: DriveSyncPreparedUploadSession,
): Promise<void> {
	const chunkSize = 8 * 1024 * 1024; // 8MB chunks
	let offset = 0;
	let attempts = 0;
	const maxAttempts = 4;

	while (offset < file.size) {
		const end = Math.min(offset + chunkSize, file.size);
		const chunk = file.slice(offset, end);
		const isFinal = end >= file.size;

		const res = await fetch(session.uploadUrl, {
			method: "PUT",
			headers: {
				"Content-Type": file.type || "application/octet-stream",
				"Content-Range": `bytes ${offset}-${end - 1}/${file.size}`,
			},
			body: chunk,
		});

		if (res.status === 308) {
			const range = res.headers.get("Range");
			if (range) {
				const m = /bytes=0-(\d+)/i.exec(range);
				if (m) {
					offset = Number(m[1]) + 1;
					attempts = 0;
					continue;
				}
			}
			offset = end;
			attempts = 0;
			continue;
		}

		if (res.ok) {
			offset = end;
			attempts = 0;
			if (isFinal) return;
			continue;
		}

		if (res.status >= 500 && attempts < maxAttempts) {
			attempts += 1;
			await new Promise((resolve) => setTimeout(resolve, 500 * 2 ** attempts));
			continue;
		}

		const body = await res.text().catch(() => "");
		throw new Error(`Resumable upload failed (${res.status}): ${body.slice(0, 400)}`);
	}
}

/** Proxied restore: only through Tyresoles API (no Google token in browser for download). */
export async function downloadBackupFileViaApi(fileId: string, suggestedName: string): Promise<void> {
	const token = get(authStore).token;
	if (!token) throw new Error("Not logged in");

	const url = `${getBackendBaseUrl()}/api/drive-sync/download/${encodeURIComponent(fileId)}`;
	const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
	if (!res.ok) {
		const t = await res.text();
		throw new Error(`Download failed (${res.status}): ${t.slice(0, 300)}`);
	}
	const blob = await res.blob();
	const a = document.createElement("a");
	a.href = URL.createObjectURL(blob);
	a.download = suggestedName || "backup-download";
	a.click();
	URL.revokeObjectURL(a.href);
}

export async function deleteBackupFileByPathViaApi(relativePath: string, fileName: string): Promise<boolean> {
	const token = get(authStore).token;
	if (!token) throw new Error("Not logged in");
	const url = `${getBackendBaseUrl()}/api/drive-sync/delete-file-by-path`;
	const res = await fetch(url, {
		method: "POST",
		headers: {
			"Content-Type": "application/json",
			Authorization: `Bearer ${token}`,
		},
		body: JSON.stringify({ relativePath, fileName }),
	});
	const body = (await res.json().catch(() => ({}))) as { deleted?: boolean; error?: string };
	if (!res.ok) {
		throw new Error(`Delete by path failed (${res.status}): ${body.error ?? "Unknown error"}`);
	}
	return !!body.deleted;
}

export async function fetchBackupFileBytesViaApi(fileId: string): Promise<Uint8Array> {
	const token = get(authStore).token;
	if (!token) throw new Error("Not logged in");
	const url = `${getBackendBaseUrl()}/api/drive-sync/download/${encodeURIComponent(fileId)}`;
	const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
	if (!res.ok) {
		const t = await res.text().catch(() => "");
		throw new Error(`Download bytes failed (${res.status}): ${t.slice(0, 300)}`);
	}
	return new Uint8Array(await res.arrayBuffer());
}

export async function restoreDifferentialBackupToPath(localPath: string, outputPath: string): Promise<void> {
	const encodedPath = btoa(encodeURIComponent(localPath)).replace(/[+/=]/g, "_");
	const files = await getDriveSyncBackupFiles();
	const manifests = files
		.filter((f) => f.name.includes(".manifest.json") && f.name.startsWith(`${encodedPath}.`))
		.sort((a, b) => new Date(b.modifiedTimeUtc ?? 0).getTime() - new Date(a.modifiedTimeUtc ?? 0).getTime());
	if (!manifests.length) {
		throw new Error("No differential manifest found for the selected file.");
	}

	const manifestBytes = await fetchBackupFileBytesViaApi(manifests[0].id);
	const manifestText = new TextDecoder().decode(manifestBytes);
	const manifest = JSON.parse(manifestText) as {
		chunks: Array<{ hash: string; encoding: "identity" | "gzip" }>;
	};

	const fileByName = new Map<string, DriveSyncBackupFileInfo[]>();
	for (const f of files) {
		const arr = fileByName.get(f.name) ?? [];
		arr.push(f);
		fileByName.set(f.name, arr);
	}

	const assembledParts: Uint8Array[] = [];
	for (const chunk of manifest.chunks ?? []) {
		const preferredName = `${chunk.hash}.${chunk.encoding === "gzip" ? "gz" : "bin"}`;
		const fallbackName = `${chunk.hash}.bin`;
		const candidates = [...(fileByName.get(preferredName) ?? []), ...(fileByName.get(fallbackName) ?? [])]
			.sort((a, b) => new Date(b.modifiedTimeUtc ?? 0).getTime() - new Date(a.modifiedTimeUtc ?? 0).getTime());
		if (!candidates.length) {
			throw new Error(`Missing chunk in backup store: ${chunk.hash}`);
		}
		let bytes = await fetchBackupFileBytesViaApi(candidates[0].id);
		if (chunk.encoding === "gzip") {
			const stream = new Blob([bytes.slice().buffer]).stream().pipeThrough(new DecompressionStream("gzip"));
			bytes = new Uint8Array(await new Response(stream).arrayBuffer());
		}
		assembledParts.push(bytes);
	}

	const totalSize = assembledParts.reduce((acc, p) => acc + p.length, 0);
	const merged = new Uint8Array(totalSize);
	let offset = 0;
	for (const p of assembledParts) {
		merged.set(p, offset);
		offset += p.length;
	}

	const { writeFile } = await import("@tauri-apps/plugin-fs");
	await writeFile(outputPath, merged);
}
