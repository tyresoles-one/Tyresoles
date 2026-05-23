import {
  getSyncFolders,
  getSyncFiles,
  getChunkReferenceCount,
  claimGcJobs,
  completeGcJob,
  enqueueGcChunkJobs,
  failGcJob,
  getSyncChunk,
  getSyncUploadJob,
  deleteSyncChunk,
  deleteSyncUploadJob,
  replaceManifestChunkRefs,
  upsertSyncManifest,
  upsertSyncChunk,
  upsertSyncFile,
  upsertSyncUploadJob,
  logSync,
  type SyncFolder,
} from "./driveSyncLocalDb";
import { readDir, stat } from "@tauri-apps/plugin-fs";
import {
  getDriveSyncConfigQuery,
  prepareDriveSyncUploadSession,
  parseAllowedExtensions,
  isExtensionAllowed,
  requestDriveSyncUploadCredentials,
  deleteBackupFileByPathViaApi,
} from "./driveSync";
import { getBackendBaseUrl } from "$lib/config/system";
import { get, writable } from "svelte/store";
import { authStore } from "$lib/stores/auth";
import { invoke } from "@tauri-apps/api/core";

type LocalFile = { name: string; path: string; size: number; mtimeMs: number };
type FolderRules = {
  includePatterns: string[];
  excludePatterns: string[];
  excludeDirectories: string[];
  maxFileSizeBytes: number;
  largeFileThresholdBytes: number;
  concurrentUploads: number;
  enableCompression: boolean;
  enableDifferential: boolean;
  enableRclone: boolean;
  rcloneBinaryPath: string | null;
};

const LARGE_CHUNK_SIZE = 4 * 1024 * 1024;
const MAX_LARGE_FILE_CHUNK_WORKERS = 4;
const MIN_LARGE_FILE_CHUNK_WORKERS = 1;
const FETCH_TIMEOUT_MS = 90_000;
const SYNC_PASS_MAX_MS = 45 * 60 * 1000;

export type DriveSyncState = {
  isWatcherActive: boolean;
  isSyncingPass: boolean;
  currentFolder: string;
  currentFile: string;
  filesProcessed: number;
  filesTotal: number;
  uploadedBytes: number;
  totalBytes: number;
  bytesPerSecond: number;
  activeUploads: number;
  shouldStop: boolean;
};

export type DriveSyncGcWorkerState = {
  isRunning: boolean;
  lastRunAt: string | null;
  lastSuccessAt: string | null;
  lastError: string | null;
  lastProcessedJobs: number;
  adaptiveChunkWorkers: number;
  lastChunkLatencyMs: number;
};

export const driveSyncState = writable<DriveSyncState>({
  isWatcherActive: false,
  isSyncingPass: false,
  currentFolder: "",
  currentFile: "",
  filesProcessed: 0,
  filesTotal: 0,
  uploadedBytes: 0,
  totalBytes: 0,
  bytesPerSecond: 0,
  activeUploads: 0,
  shouldStop: false,
});

export const driveSyncGcState = writable<DriveSyncGcWorkerState>({
  isRunning: false,
  lastRunAt: null,
  lastSuccessAt: null,
  lastError: null,
  lastProcessedJobs: 0,
  adaptiveChunkWorkers: 2,
  lastChunkLatencyMs: 0,
});

let isWatching = false;
let watcherInterval: any = null;
let isSyncingPass = false;
let progressWindowStartMs = 0;
let progressWindowBytes = 0;
let queuedRunRequested = false;
let gcWorkerTimer: any = null;
let gcWorkerRunning = false;
let adaptiveChunkWorkerTarget = 2;

function parseJsonStringArray(raw?: string | null): string[] {
  if (!raw || !raw.trim()) return [];
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return [];
    return parsed.map((v) => String(v).trim()).filter(Boolean);
  } catch {
    return [];
  }
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function globToRegex(pattern: string): RegExp {
  let source = escapeRegExp(pattern.trim().replace(/\\/g, "/"));
  source = source.replace(/\\\*\\\*/g, ".*").replace(/\\\*/g, "[^/]*");
  return new RegExp(`^${source}$`, "i");
}

function matchesAnyPattern(path: string, patterns: string[]): boolean {
  if (!patterns.length) return false;
  const normalized = path.replace(/\\/g, "/");
  return patterns.some((p) => {
    try {
      return globToRegex(p).test(normalized);
    } catch {
      return false;
    }
  });
}

function computeFolderRules(folder: SyncFolder): FolderRules {
  return {
    includePatterns: parseJsonStringArray(folder.includePatternsJson),
    excludePatterns: parseJsonStringArray(folder.excludePatternsJson),
    excludeDirectories: parseJsonStringArray(folder.excludeDirectoriesJson).map((s) => s.replace(/\\/g, "/").toLowerCase()),
    maxFileSizeBytes: Math.max(1, folder.maxFileSizeMb || 4096) * 1024 * 1024,
    largeFileThresholdBytes: Math.max(8, folder.largeFileThresholdMb || 64) * 1024 * 1024,
    concurrentUploads: Math.max(1, Math.min(6, folder.concurrentUploads || 2)),
    enableCompression: !!folder.enableCompression,
    enableDifferential: !!folder.enableDifferential,
    enableRclone: !!folder.enableRclone,
    rcloneBinaryPath: folder.rcloneBinaryPath?.trim() || null,
  };
}

async function sha256Hex(data: Uint8Array): Promise<string> {
  const copy = data.slice();
  const hash = await crypto.subtle.digest("SHA-256", copy);
  return Array.from(new Uint8Array(hash)).map((b) => b.toString(16).padStart(2, "0")).join("");
}

async function compressChunkIfUseful(data: Uint8Array): Promise<{ payload: Uint8Array; encoding: "identity" | "gzip" }> {
  try {
    if (typeof CompressionStream === "undefined" || data.length < 128 * 1024) return { payload: data, encoding: "identity" };
    const stream = new Blob([data.slice()]).stream().pipeThrough(new CompressionStream("gzip"));
    const compressed = new Uint8Array(await new Response(stream).arrayBuffer());
    if (compressed.length + 128 < data.length) return { payload: compressed, encoding: "gzip" };
  } catch {
    // ignore
  }
  return { payload: data, encoding: "identity" };
}

function addProgress(bytes: number) {
  progressWindowBytes += bytes;
  const now = Date.now();
  if (!progressWindowStartMs) progressWindowStartMs = now;
  const elapsed = now - progressWindowStartMs;
  if (elapsed >= 1000) {
    const bps = Math.round((progressWindowBytes * 1000) / elapsed);
    driveSyncState.update((s) => ({ ...s, bytesPerSecond: bps }));
    progressWindowBytes = 0;
    progressWindowStartMs = now;
  }
  driveSyncState.update((s) => ({ ...s, uploadedBytes: Math.min(s.totalBytes, s.uploadedBytes + bytes) }));
}

async function yieldToEventLoop() {
  await new Promise((resolve) => setTimeout(resolve, 0));
}

async function fetchWithTimeout(input: RequestInfo | URL, init: RequestInit, timeoutMs = FETCH_TIMEOUT_MS): Promise<Response> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(input, { ...init, signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

async function runGcWorkerOnce() {
  if (gcWorkerRunning) return;
  gcWorkerRunning = true;
  driveSyncGcState.update((s) => ({ ...s, isRunning: true }));
  try {
    const token = get(authStore).token;
    if (!token || get(driveSyncState).shouldStop) return;
    const jobs = await claimGcJobs(8);
    let processed = 0;
    for (const job of jobs) {
      try {
        const refs = await getChunkReferenceCount(job.chunkHash);
        if (refs > 0) {
          await completeGcJob(job.id);
          continue;
        }
        const chunk = await getSyncChunk(job.chunkHash);
        if (!chunk) {
          await completeGcJob(job.id);
          continue;
        }
        const chunkName = `${job.chunkHash}.${chunk.encoding === "gzip" ? "gz" : "bin"}`;
        await deleteBackupFileByPathViaApi(`.tyresoles-diff/chunks/${job.chunkHash.slice(0, 2)}`, chunkName);
        await deleteSyncChunk(job.chunkHash);
        await completeGcJob(job.id);
        processed++;
      } catch (e: any) {
        await failGcJob(job.id, job.attempts, e?.message || String(e));
        driveSyncGcState.update((s) => ({ ...s, lastError: e?.message || String(e) }));
      }
      await yieldToEventLoop();
    }
    const nowIso = new Date().toISOString();
    driveSyncGcState.update((s) => ({
      ...s,
      lastRunAt: nowIso,
      lastSuccessAt: nowIso,
      lastProcessedJobs: processed,
      lastError: null,
    }));
  } finally {
    gcWorkerRunning = false;
    driveSyncGcState.update((s) => ({ ...s, isRunning: false }));
  }
}

function tuneAdaptiveChunkWorkers(sampleMs: number, hadError: boolean) {
  if (hadError) {
    adaptiveChunkWorkerTarget = Math.max(MIN_LARGE_FILE_CHUNK_WORKERS, adaptiveChunkWorkerTarget - 1);
    driveSyncGcState.update((s) => ({ ...s, adaptiveChunkWorkers: adaptiveChunkWorkerTarget, lastChunkLatencyMs: sampleMs }));
    return;
  }
  if (sampleMs <= 1500) {
    adaptiveChunkWorkerTarget = Math.min(MAX_LARGE_FILE_CHUNK_WORKERS, adaptiveChunkWorkerTarget + 1);
    driveSyncGcState.update((s) => ({ ...s, adaptiveChunkWorkers: adaptiveChunkWorkerTarget, lastChunkLatencyMs: sampleMs }));
    return;
  }
  if (sampleMs >= 6000) {
    adaptiveChunkWorkerTarget = Math.max(MIN_LARGE_FILE_CHUNK_WORKERS, adaptiveChunkWorkerTarget - 1);
  }
  driveSyncGcState.update((s) => ({ ...s, adaptiveChunkWorkers: adaptiveChunkWorkerTarget, lastChunkLatencyMs: sampleMs }));
}

async function uploadPayloadToPreparedSession(sessionUrl: string, payload: Uint8Array, token: string) {
  const range = payload.length ? `bytes 0-${payload.length - 1}/${payload.length}` : "bytes */0";
  const bodyBuffer = payload.slice().buffer;
  try {
    const res = await fetchWithTimeout(sessionUrl, {
      method: "PUT",
      headers: { "Content-Type": "application/octet-stream", "Content-Range": range },
      body: bodyBuffer,
    });
    if (res.ok || res.status === 308) return;
    const body = await res.text().catch(() => "");
    throw new Error(`Upload failed (${res.status}): ${body.slice(0, 280)}`);
  } catch {
    const proxyRes = await fetchWithTimeout(`${getBackendBaseUrl()}/api/drive-sync/upload-proxy-chunk`, {
      method: "POST",
      headers: {
        "Content-Type": "application/octet-stream",
        Authorization: `Bearer ${token}`,
        "X-Upload-Url": sessionUrl,
        "X-Content-Range": range,
      },
      body: bodyBuffer,
    }, FETCH_TIMEOUT_MS);
    if (!proxyRes.ok && proxyRes.status !== 308) {
      const body = await proxyRes.text().catch(() => "");
      throw new Error(`Proxy upload failed (${proxyRes.status})`);
    }
  }
}

async function uploadFileHandleResumable(
  sessionUrl: string,
  fileHandle: any,
  fileSize: number,
  token: string,
  initialOffset = 0,
  onCommittedBytes?: (uploadedBytes: number) => Promise<void>,
): Promise<string | null> {
  const { SeekMode } = await import("@tauri-apps/plugin-fs");
  const chunkSize = 8 * 1024 * 1024;
  const maxChunkAttempts = 5;
  let offset = Math.max(0, initialOffset);
  let remoteFileId: string | null = null;
  const parseCommittedOffset = (res: Response): number | null => {
    const range = res.headers.get("Range");
    if (!range) return null;
    const m = /bytes=0-(\d+)/i.exec(range);
    return m ? Number(m[1]) + 1 : null;
  };
  const probeCommittedOffset = async (): Promise<number> => {
    const probe = await fetchWithTimeout(sessionUrl, {
      method: "PUT",
      headers: { "Content-Range": `bytes */${fileSize}` },
    }, FETCH_TIMEOUT_MS);
    if (probe.status === 404 || probe.status === 410) throw new Error("Upload session expired");
    if (probe.ok) return fileSize;
    if (probe.status === 308) return parseCommittedOffset(probe) ?? 0;
    return offset;
  };
  while (offset < fileSize) {
    const end = Math.min(offset + chunkSize, fileSize);
    const len = end - offset;
    await fileHandle.seek(offset, SeekMode.Start);
    const buf = new Uint8Array(len);
    const n = await fileHandle.read(buf);
    const payload = buf.subarray(0, n ?? len);
    const range = `bytes ${offset}-${offset + payload.length - 1}/${fileSize}`;
    const bodyBuffer = payload.slice().buffer;

    let chunkCommitted = false;
    let attempts = 0;
    while (!chunkCommitted) {
      attempts += 1;
      try {
        let res: Response;
        try {
          res = await fetchWithTimeout(sessionUrl, {
            method: "PUT",
            headers: { "Content-Type": "application/octet-stream", "Content-Range": range },
            body: bodyBuffer,
          });
        } catch {
          res = await fetchWithTimeout(`${getBackendBaseUrl()}/api/drive-sync/upload-proxy-chunk`, {
            method: "POST",
            headers: {
              "Content-Type": "application/octet-stream",
              Authorization: `Bearer ${token}`,
              "X-Upload-Url": sessionUrl,
              "X-Content-Range": range,
            },
            body: bodyBuffer,
          }, FETCH_TIMEOUT_MS);
        }

        if (res.status === 404 || res.status === 410) {
          throw new Error("Upload session expired");
        }
        if (res.status === 308 || res.ok) {
          const prevOffset = offset;
          if (res.status === 308) {
            offset = Math.min(end, Math.max(offset, parseCommittedOffset(res) ?? end));
          } else {
            offset = end;
            const text = await res.text().catch(() => "");
            try {
              const parsed = text ? JSON.parse(text) : {};
              if (typeof parsed?.id === "string") remoteFileId = parsed.id;
            } catch {
              // ignore
            }
          }
          if (offset > prevOffset) {
            addProgress(offset - prevOffset);
            if (onCommittedBytes) await onCommittedBytes(offset);
          }
          chunkCommitted = true;
          continue;
        }

        const retryable = res.status === 408 || res.status === 429 || res.status >= 500;
        const text = await res.text().catch(() => "");
        if (!retryable || attempts >= maxChunkAttempts) {
          throw new Error(`Chunk upload failed (${res.status}): ${text.slice(0, 300)}`);
        }
      } catch (e: any) {
        if (String(e?.message || e).toLowerCase().includes("session expired")) throw e;
        if (attempts >= maxChunkAttempts) throw e;
      }

      await new Promise((resolve) => setTimeout(resolve, 500 * Math.pow(2, attempts)));
      const probedOffset = await probeCommittedOffset();
      if (probedOffset > offset) {
        const prevOffset = offset;
        offset = probedOffset;
        addProgress(offset - prevOffset);
        if (onCommittedBytes) await onCommittedBytes(offset);
      }
      if (offset >= end) chunkCommitted = true;
    }
    await yieldToEventLoop();
  }
  return remoteFileId;
}

async function uploadViaRcloneIfEnabled(
  localFilePath: string,
  folderRules: FolderRules,
  remoteRelativePath: string,
  requestedBytes: number,
): Promise<boolean> {
  if (!folderRules.enableRclone) return false;
  try {
    const creds = await requestDriveSyncUploadCredentials(requestedBytes);
    const remote = `:drive,root_folder_id=${creds.folderId}:${remoteRelativePath.replace(/\\/g, "/")}`;
    const tokenJson = JSON.stringify({
      access_token: creds.accessToken,
      token_type: "Bearer",
      expiry: creds.expiresAtUtc,
    });
    const out = await invoke<{ success: boolean; stderr?: string }>("run_rclone_copyto", {
      binaryPath: folderRules.rcloneBinaryPath,
      sourcePath: localFilePath,
      remotePath: remote,
      driveTokenJson: tokenJson,
    });
    return !!out?.success;
  } catch {
    return false;
  }
}

async function scanDirectory(localPath: string, rules: FolderRules): Promise<LocalFile[]> {
  const result: LocalFile[] = [];
  try {
    const entries = await readDir(localPath);
    for (const entry of entries) {
      if (!entry.name || entry.name === ".DS_Store") continue;
      const fullPath = `${localPath}/${entry.name}`;
      if (entry.isDirectory) {
        const normalized = fullPath.replace(/\\/g, "/").toLowerCase();
        if (rules.excludeDirectories.some((d) => normalized.includes(d))) continue;
        result.push(...await scanDirectory(fullPath, rules));
      } else if (entry.isFile) {
        try {
          const info = await stat(fullPath);
          if (info.size > rules.maxFileSizeBytes) continue;
          result.push({ name: entry.name, path: fullPath, size: info.size, mtimeMs: info.mtime?.getTime() || 0 });
        } catch {
          // skip broken entries
        }
      }
      await yieldToEventLoop();
    }
  } catch (e) {
    await logSync(`Failed to read directory: ${localPath} - ${e}`, "error");
  }
  return result;
}

async function processFile(runId: string, folder: SyncFolder, rules: FolderRules, lf: LocalFile, token: string) {
  const { tempDir, join } = await import("@tauri-apps/api/path");
  const { open, SeekMode, copyFile, remove } = await import("@tauri-apps/plugin-fs");

  const tmpDir = await tempDir();
  const snapshotPath = await join(tmpDir, `tyresoles_sync_${crypto.randomUUID()}.tmp`);
  await copyFile(lf.path, snapshotPath);
  const fileHandle = await open(snapshotPath, { read: true });
  try {
    let relPath = lf.path.replace(folder.localPath, "").replace(/\\/g, "/");
    if (relPath.startsWith("/")) relPath = relPath.slice(1);
    if (relPath.endsWith(lf.name)) relPath = relPath.slice(0, Math.max(0, relPath.length - lf.name.length));
    if (relPath.endsWith("/")) relPath = relPath.slice(0, -1);

    if (rules.enableDifferential && lf.size >= rules.largeFileThresholdBytes) {
      const totalChunks = Math.ceil(lf.size / LARGE_CHUNK_SIZE);
      const manifestChunks: Array<{ hash: string; encoding: "identity" | "gzip" }> = new Array(totalChunks);
      const chunkWorkers = Math.max(
        MIN_LARGE_FILE_CHUNK_WORKERS,
        Math.min(MAX_LARGE_FILE_CHUNK_WORKERS, rules.concurrentUploads, adaptiveChunkWorkerTarget, totalChunks),
      );
      let nextChunkIndex = 0;
      let firstChunkError: Error | null = null;
      let latencySamples = 0;
      let latencyMsSum = 0;

      const chunkWorkerTasks = Array.from({ length: chunkWorkers }, async () => {
        const workerHandle = await open(snapshotPath, { read: true });
        try {
          while (!firstChunkError && !get(driveSyncState).shouldStop) {
            const chunkIndex = nextChunkIndex++;
            if (chunkIndex >= totalChunks) break;
            const chunkStartMs = Date.now();

            const offset = chunkIndex * LARGE_CHUNK_SIZE;
            const end = Math.min(offset + LARGE_CHUNK_SIZE, lf.size);
            const len = end - offset;
            await workerHandle.seek(offset, SeekMode.Start);
            const buf = new Uint8Array(len);
            const n = await workerHandle.read(buf);
            const chunk = buf.subarray(0, n ?? len);
            const hash = await sha256Hex(chunk);
            let chunkEncoding: "identity" | "gzip" = "identity";
            const known = await getSyncChunk(hash);
            if (!known) {
              const encoded = rules.enableCompression ? await compressChunkIfUseful(chunk) : { payload: chunk, encoding: "identity" as const };
              chunkEncoding = encoded.encoding;
              const name = `${hash}.${encoded.encoding === "gzip" ? "gz" : "bin"}`;
              const path = `.tyresoles-diff/chunks/${hash.slice(0, 2)}`;
              const { tempDir, join } = await import("@tauri-apps/api/path");
              const { writeFile, remove } = await import("@tauri-apps/plugin-fs");
              const chunkRemotePath = `${path}/${name}`;
              let uploadedWithRclone = false;
              if (rules.enableRclone) {
                const tmpDir = await tempDir();
                const tmpChunkPath = await join(tmpDir, `tyresoles_chunk_${hash}_${Date.now()}_${chunkIndex}.bin`);
                await writeFile(tmpChunkPath, encoded.payload);
                uploadedWithRclone = await uploadViaRcloneIfEnabled(tmpChunkPath, rules, chunkRemotePath, encoded.payload.length);
                await remove(tmpChunkPath).catch(() => {});
              }
              if (!uploadedWithRclone) {
                const session = await prepareDriveSyncUploadSession(name, encoded.payload.length, path, "application/octet-stream", token);
                await uploadPayloadToPreparedSession(session.uploadUrl, encoded.payload, token);
              }
              await upsertSyncChunk({
                hash,
                originalSize: chunk.length,
                storedSize: encoded.payload.length,
                encoding: encoded.encoding,
                uploadedAtUtc: new Date().toISOString(),
              });
            } else {
              chunkEncoding = known.encoding;
            }
            manifestChunks[chunkIndex] = { hash, encoding: chunkEncoding };
            addProgress(chunk.length);
            latencySamples += 1;
            latencyMsSum += Math.max(1, Date.now() - chunkStartMs);
            await yieldToEventLoop();
          }
        } catch (e: any) {
          if (!firstChunkError) firstChunkError = new Error(e?.message || String(e));
        } finally {
          await workerHandle.close();
        }
      });
      await Promise.all(chunkWorkerTasks);
      const avgChunkMs = latencySamples ? latencyMsSum / latencySamples : 0;
      tuneAdaptiveChunkWorkers(avgChunkMs, !!firstChunkError);
      if (firstChunkError) throw firstChunkError;
      if (manifestChunks.some((c) => !c)) throw new Error("Differential chunk worker did not produce complete manifest.");

      const manifest = {
        version: 1,
        fileName: lf.name,
        localPath: lf.path,
        relativePath: relPath,
        size: lf.size,
        mtimeMs: lf.mtimeMs,
        chunkSize: LARGE_CHUNK_SIZE,
        chunks: manifestChunks,
        createdAtUtc: new Date().toISOString(),
      };
      const manifestBytes = new TextEncoder().encode(JSON.stringify(manifest));
      const manifestHash = await sha256Hex(manifestBytes);
      const encodedPath = btoa(encodeURIComponent(lf.path)).replace(/[+/=]/g, "_");
      const manifestFileName = `${encodedPath}.manifest.json`;
      let manifestUploadedByRclone = false;
      if (rules.enableRclone) {
        const { tempDir, join } = await import("@tauri-apps/api/path");
        const { writeFile, remove } = await import("@tauri-apps/plugin-fs");
        const tmpDir = await tempDir();
        const tmpManifestPath = await join(tmpDir, `tyresoles_manifest_${manifestHash}.json`);
        await writeFile(tmpManifestPath, manifestBytes);
        manifestUploadedByRclone = await uploadViaRcloneIfEnabled(
          tmpManifestPath,
          rules,
          `.tyresoles-diff/manifests/${manifestFileName}`,
          manifestBytes.length,
        );
        await remove(tmpManifestPath).catch(() => {});
      }
      if (!manifestUploadedByRclone) {
        const manifestSession = await prepareDriveSyncUploadSession(manifestFileName, manifestBytes.length, ".tyresoles-diff/manifests", "application/json", token);
        await uploadPayloadToPreparedSession(manifestSession.uploadUrl, manifestBytes, token);
      }
      await upsertSyncManifest({
        folderId: folder.id,
        localPath: lf.path,
        manifestHash,
        remoteFileId: null,
        createdAtUtc: new Date().toISOString(),
      });
      const removed = await replaceManifestChunkRefs(folder.id, lf.path, manifestChunks.map((c) => c.hash));
      await enqueueGcChunkJobs(removed);
    } else {
      let remoteFileId: string | null = null;
      const remoteRelPath = relPath ? `${relPath}/${lf.name}` : lf.name;
      const uploadedWithRclone = await uploadViaRcloneIfEnabled(snapshotPath, rules, remoteRelPath, lf.size);
      if (!uploadedWithRclone) {
        const existingJob = await getSyncUploadJob(folder.id, lf.path);
        let sessionUrl = existingJob?.sessionUrl ?? "";
        let startOffset = 0;
        if (existingJob && existingJob.size === lf.size && existingJob.mtimeMs === lf.mtimeMs) {
          startOffset = Math.max(0, Math.min(lf.size, existingJob.uploadedBytes));
        } else if (existingJob) {
          await deleteSyncUploadJob(folder.id, lf.path);
        }

        if (!sessionUrl) {
          const session = await prepareDriveSyncUploadSession(lf.name, lf.size, relPath, undefined, token);
          sessionUrl = session.uploadUrl;
          await upsertSyncUploadJob({
            folderId: folder.id,
            localPath: lf.path,
            size: lf.size,
            mtimeMs: lf.mtimeMs,
            sessionUrl,
            uploadedBytes: 0,
          });
        }

        const persistCommitted = async (uploadedBytes: number) => {
          await upsertSyncUploadJob({
            folderId: folder.id,
            localPath: lf.path,
            size: lf.size,
            mtimeMs: lf.mtimeMs,
            sessionUrl,
            uploadedBytes,
          });
        };

        try {
          remoteFileId = await uploadFileHandleResumable(sessionUrl, fileHandle, lf.size, token, startOffset, persistCommitted);
        } catch (e: any) {
          if (String(e?.message || e).toLowerCase().includes("session expired")) {
            const session = await prepareDriveSyncUploadSession(lf.name, lf.size, relPath, undefined, token);
            sessionUrl = session.uploadUrl;
            await persistCommitted(0);
            remoteFileId = await uploadFileHandleResumable(sessionUrl, fileHandle, lf.size, token, 0, persistCommitted);
          } else {
            throw e;
          }
        }
        await deleteSyncUploadJob(folder.id, lf.path);
      } else {
        addProgress(lf.size);
        await deleteSyncUploadJob(folder.id, lf.path);
      }
      await upsertSyncFile({
        folderId: folder.id,
        localPath: lf.path,
        size: lf.size,
        mtimeMs: lf.mtimeMs,
        lastSyncedUtc: new Date().toISOString(),
        remoteFileId,
      });
      await logSync(`Successfully synced: ${lf.path}`, "success");
      return;
    }

    await upsertSyncFile({
      folderId: folder.id,
      localPath: lf.path,
      size: lf.size,
      mtimeMs: lf.mtimeMs,
      lastSyncedUtc: new Date().toISOString(),
      remoteFileId: null,
    });
    await logSync(`Successfully synced: ${lf.path}`, "success");
  } finally {
    await fileHandle.close();
    await remove(snapshotPath).catch(() => {});
  }
}

export function pauseSync() {
  driveSyncState.update((s) => ({ ...s, shouldStop: true }));
}

export function playSync() {
  driveSyncState.update((s) => ({ ...s, shouldStop: false, isWatcherActive: true }));
  if (!isWatching) initDriveSyncWatcher();
  runSyncPass().catch(console.error);
}

export function stopSync() {
  driveSyncState.update((s) => ({ ...s, isWatcherActive: false, shouldStop: true }));
  stopDriveSyncWatcher();
}

export async function runSyncPass(force = false) {
  const runId = `run-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  if (isSyncingPass || get(driveSyncState).shouldStop) {
    queuedRunRequested = true;
    return;
  }

  isSyncingPass = true;
  queuedRunRequested = false;
  progressWindowStartMs = Date.now();
  progressWindowBytes = 0;
  const passStartMs = Date.now();
  driveSyncState.update((s) => ({ ...s, isSyncingPass: true, filesProcessed: 0, filesTotal: 0, bytesPerSecond: 0, activeUploads: 0 }));
  try {
    const token = get(authStore).token;
    if (!token) return;
    const config = await getDriveSyncConfigQuery();
    if (!config || !config.isActive) return;
    const allowedExtensions = parseAllowedExtensions(config.allowedExtensionsJson);
    const folders = await getSyncFolders();

    for (const folder of folders) {
      if (Date.now() - passStartMs > SYNC_PASS_MAX_MS) {
        throw new Error("Sync pass exceeded watchdog timeout and was aborted safely.");
      }
      if (!folder.isActive) continue;
      if (folder.syncMode !== "auto" && !force) continue;
      const rules = computeFolderRules(folder);
      const tracked = await getSyncFiles(folder.id);
      const map = new Map(tracked.map((f) => [f.localPath, f]));
      const files = await scanDirectory(folder.localPath, rules);
      const candidates = files.filter((lf) => {
        const normalized = lf.path.replace(/\\/g, "/");
        if (lf.name.startsWith("~$") || lf.name.toLowerCase().endsWith(".tmp") || lf.name.toLowerCase() === "thumbs.db") return false;
        if (!isExtensionAllowed(lf.name, allowedExtensions)) return false;
        if (rules.includePatterns.length && !matchesAnyPattern(normalized, rules.includePatterns)) return false;
        if (matchesAnyPattern(normalized, rules.excludePatterns)) return false;
        const existing = map.get(lf.path);
        return !existing || existing.mtimeMs < lf.mtimeMs || existing.size !== lf.size;
      }).sort((a, b) => a.size - b.size || a.path.localeCompare(b.path));

      driveSyncState.update((s) => ({
        ...s,
        currentFolder: folder.localPath,
        filesProcessed: 0,
        filesTotal: candidates.length,
        totalBytes: candidates.reduce((sum, f) => sum + f.size, 0),
        uploadedBytes: 0,
      }));

      const queue = [...candidates];
      const workers = Array.from({ length: rules.concurrentUploads }, async () => {
        while (!get(driveSyncState).shouldStop && queue.length) {
          if (Date.now() - passStartMs > SYNC_PASS_MAX_MS) {
            throw new Error("Sync worker exceeded watchdog timeout.");
          }
          const lf = queue.shift();
          if (!lf) break;
          driveSyncState.update((s) => ({ ...s, currentFile: lf.name, activeUploads: s.activeUploads + 1 }));
          try {
            await processFile(runId, folder, rules, lf, token);
          } catch (e: any) {
            await logSync(`Failed to sync ${lf.path}: ${e?.message || String(e)}`, "error");
          } finally {
            driveSyncState.update((s) => ({
              ...s,
              filesProcessed: s.filesProcessed + 1,
              activeUploads: Math.max(0, s.activeUploads - 1),
            }));
          }
          await yieldToEventLoop();
        }
      });
      await Promise.all(workers);
      await yieldToEventLoop();
    }
  } finally {
    isSyncingPass = false;
    driveSyncState.update((s) => ({
      ...s,
      isSyncingPass: false,
      currentFolder: "",
      currentFile: "",
      uploadedBytes: 0,
      totalBytes: 0,
      bytesPerSecond: 0,
      activeUploads: 0,
    }));
    if (queuedRunRequested && !get(driveSyncState).shouldStop) {
      queuedRunRequested = false;
      setTimeout(() => {
        runSyncPass(force).catch(console.error);
      }, 0);
    }
  }
}

export function initDriveSyncWatcher() {
  if (isWatching) return;
  isWatching = true;
  driveSyncState.update((s) => ({ ...s, isWatcherActive: true, shouldStop: false }));
  gcWorkerTimer = setInterval(() => {
    runGcWorkerOnce().catch(console.error);
  }, 5_000);
  setTimeout(() => {
    runGcWorkerOnce().catch(console.error);
  }, 2_000);
  watcherInterval = setInterval(() => {
    runSyncPass().catch(console.error);
  }, 5 * 60 * 1000);
  setTimeout(() => {
    runSyncPass().catch(console.error);
  }, 30000);
}

export function stopDriveSyncWatcher() {
  if (watcherInterval) {
    clearInterval(watcherInterval);
    watcherInterval = null;
  }
  if (gcWorkerTimer) {
    clearInterval(gcWorkerTimer);
    gcWorkerTimer = null;
  }
  isWatching = false;
  driveSyncState.update((s) => ({ ...s, isWatcherActive: false }));
}
