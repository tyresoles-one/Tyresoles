import Database from "@tauri-apps/plugin-sql";
import { appDataDir, join } from "@tauri-apps/api/path";
import { stat, exists } from "@tauri-apps/plugin-fs";
import { toast } from "svelte-sonner";

export type SyncFolder = {
  id: string;
  localPath: string;
  isActive: boolean;
  syncMode: "auto" | "on-demand";
  includePatternsJson?: string | null;
  excludePatternsJson?: string | null;
  excludeDirectoriesJson?: string | null;
  maxFileSizeMb: number;
  largeFileThresholdMb: number;
  concurrentUploads: number;
  enableCompression: boolean;
  enableDifferential: boolean;
  enableRclone: boolean;
  rcloneBinaryPath?: string | null;
};

export type SyncFile = {
  id: string;
  folderId: string;
  localPath: string;
  size: number;
  mtimeMs: number;
  lastSyncedUtc: string | null;
  remoteFileId: string | null;
};

export type SyncChunk = {
  id: string;
  hash: string;
  originalSize: number;
  storedSize: number;
  encoding: "identity" | "gzip";
  uploadedAtUtc: string;
};

export type SyncManifest = {
  id: string;
  folderId: string;
  localPath: string;
  manifestHash: string;
  remoteFileId: string | null;
  createdAtUtc: string;
};

export type SyncGcJob = {
  id: string;
  chunkHash: string;
  status: "pending" | "running" | "failed";
  attempts: number;
  nextAttemptUtc: string;
  lastError: string | null;
  updatedAtUtc: string;
};

export type SyncGcJobStats = {
  pending: number;
  running: number;
  failed: number;
  total: number;
  oldestPendingUtc: string | null;
};

export type SyncUploadJob = {
  id: string;
  folderId: string;
  localPath: string;
  size: number;
  mtimeMs: number;
  sessionUrl: string;
  uploadedBytes: number;
  updatedAtUtc: string;
};

let dbPromise: Promise<Database> | null = null;

export async function getDb(): Promise<Database> {
  if (!dbPromise) {
    dbPromise = Database.load("sqlite:drivesync.db").then(async (db) => {
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_folders (
          id TEXT PRIMARY KEY,
          local_path TEXT NOT NULL UNIQUE,
          is_active INTEGER NOT NULL DEFAULT 1,
          sync_mode TEXT NOT NULL DEFAULT 'auto',
          include_patterns_json TEXT,
          exclude_patterns_json TEXT,
          exclude_directories_json TEXT,
          max_file_size_mb INTEGER NOT NULL DEFAULT 4096,
          large_file_threshold_mb INTEGER NOT NULL DEFAULT 64,
          concurrent_uploads INTEGER NOT NULL DEFAULT 2,
          enable_compression INTEGER NOT NULL DEFAULT 1,
          enable_differential INTEGER NOT NULL DEFAULT 1,
          enable_rclone INTEGER NOT NULL DEFAULT 0,
          rclone_binary_path TEXT
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_files (
          id TEXT PRIMARY KEY,
          folder_id TEXT NOT NULL,
          local_path TEXT NOT NULL UNIQUE,
          size INTEGER NOT NULL DEFAULT 0,
          mtime_ms INTEGER NOT NULL DEFAULT 0,
          last_synced_utc TEXT,
          remote_file_id TEXT,
          FOREIGN KEY (folder_id) REFERENCES sync_folders(id) ON DELETE CASCADE
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_logs (
          id TEXT PRIMARY KEY,
          message TEXT NOT NULL,
          level TEXT NOT NULL,
          created_at TEXT NOT NULL
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_chunks (
          id TEXT PRIMARY KEY,
          hash TEXT NOT NULL UNIQUE,
          original_size INTEGER NOT NULL,
          stored_size INTEGER NOT NULL,
          encoding TEXT NOT NULL,
          uploaded_at_utc TEXT NOT NULL
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_manifests (
          id TEXT PRIMARY KEY,
          folder_id TEXT NOT NULL,
          local_path TEXT NOT NULL,
          manifest_hash TEXT NOT NULL,
          remote_file_id TEXT,
          created_at_utc TEXT NOT NULL,
          UNIQUE(folder_id, local_path, manifest_hash)
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_manifest_chunks (
          folder_id TEXT NOT NULL,
          local_path TEXT NOT NULL,
          chunk_hash TEXT NOT NULL,
          PRIMARY KEY (folder_id, local_path, chunk_hash)
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_gc_jobs (
          id TEXT PRIMARY KEY,
          chunk_hash TEXT NOT NULL UNIQUE,
          status TEXT NOT NULL DEFAULT 'pending',
          attempts INTEGER NOT NULL DEFAULT 0,
          next_attempt_utc TEXT NOT NULL,
          last_error TEXT,
          updated_at_utc TEXT NOT NULL
        )
      `);
      await db.execute(`
        CREATE TABLE IF NOT EXISTS sync_upload_jobs (
          id TEXT PRIMARY KEY,
          folder_id TEXT NOT NULL,
          local_path TEXT NOT NULL,
          size INTEGER NOT NULL,
          mtime_ms INTEGER NOT NULL,
          session_url TEXT NOT NULL,
          uploaded_bytes INTEGER NOT NULL DEFAULT 0,
          updated_at_utc TEXT NOT NULL,
          UNIQUE(folder_id, local_path)
        )
      `);

      // Lightweight migrations for existing users.
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN include_patterns_json TEXT`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN exclude_patterns_json TEXT`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN exclude_directories_json TEXT`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN max_file_size_mb INTEGER NOT NULL DEFAULT 4096`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN large_file_threshold_mb INTEGER NOT NULL DEFAULT 64`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN concurrent_uploads INTEGER NOT NULL DEFAULT 2`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN enable_compression INTEGER NOT NULL DEFAULT 1`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN enable_differential INTEGER NOT NULL DEFAULT 1`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN enable_rclone INTEGER NOT NULL DEFAULT 0`).catch(() => {});
      await db.execute(`ALTER TABLE sync_folders ADD COLUMN rclone_binary_path TEXT`).catch(() => {});
      return db;
    });
  }
  return dbPromise;
}

export async function addSyncFolder(localPath: string, syncMode: "auto" | "on-demand" = "auto") {
  const db = await getDb();
  const id = crypto.randomUUID();
  try {
    await db.execute(
      `INSERT INTO sync_folders (
         id, local_path, is_active, sync_mode, include_patterns_json, exclude_patterns_json, exclude_directories_json,
         max_file_size_mb, large_file_threshold_mb, concurrent_uploads, enable_compression, enable_differential, enable_rclone, rclone_binary_path
       ) VALUES ($1, $2, 1, $3, NULL, NULL, NULL, 4096, 64, 2, 1, 1, 0, NULL)`,
      [id, localPath, syncMode]
    );
    return id;
  } catch (e: any) {
    if (String(e).includes("UNIQUE")) {
      toast.error("Folder is already added to sync.");
    } else {
      throw e;
    }
  }
}

export async function getSyncFolders(): Promise<SyncFolder[]> {
  const db = await getDb();
  const rows = await db.select<any[]>("SELECT * FROM sync_folders");
  return rows.map((r) => ({
    id: r.id,
    localPath: r.local_path,
    isActive: r.is_active === 1,
    syncMode: r.sync_mode,
    includePatternsJson: r.include_patterns_json ?? null,
    excludePatternsJson: r.exclude_patterns_json ?? null,
    excludeDirectoriesJson: r.exclude_directories_json ?? null,
    maxFileSizeMb: Number(r.max_file_size_mb ?? 4096),
    largeFileThresholdMb: Number(r.large_file_threshold_mb ?? 64),
    concurrentUploads: Number(r.concurrent_uploads ?? 2),
    enableCompression: Number(r.enable_compression ?? 1) === 1,
    enableDifferential: Number(r.enable_differential ?? 1) === 1,
    enableRclone: Number(r.enable_rclone ?? 0) === 1,
    rcloneBinaryPath: r.rclone_binary_path ?? null,
  }));
}

export async function updateSyncFolder(id: string, updates: Partial<SyncFolder>) {
  const db = await getDb();
  const keys = Object.keys(updates);
  if (keys.length === 0) return;
  const setParts = [];
  const args = [];
  let i = 1;
  for (const k of keys) {
    if (k === "localPath") setParts.push(`local_path = $${i}`);
    else if (k === "isActive") setParts.push(`is_active = $${i}`);
    else if (k === "syncMode") setParts.push(`sync_mode = $${i}`);
    else if (k === "includePatternsJson") setParts.push(`include_patterns_json = $${i}`);
    else if (k === "excludePatternsJson") setParts.push(`exclude_patterns_json = $${i}`);
    else if (k === "excludeDirectoriesJson") setParts.push(`exclude_directories_json = $${i}`);
    else if (k === "maxFileSizeMb") setParts.push(`max_file_size_mb = $${i}`);
    else if (k === "largeFileThresholdMb") setParts.push(`large_file_threshold_mb = $${i}`);
    else if (k === "concurrentUploads") setParts.push(`concurrent_uploads = $${i}`);
    else if (k === "enableCompression") setParts.push(`enable_compression = $${i}`);
    else if (k === "enableDifferential") setParts.push(`enable_differential = $${i}`);
    else if (k === "enableRclone") setParts.push(`enable_rclone = $${i}`);
    else if (k === "rcloneBinaryPath") setParts.push(`rclone_binary_path = $${i}`);
    
    let val = (updates as any)[k];
    if (typeof val === "boolean") val = val ? 1 : 0;
    args.push(val);
    i++;
  }
  args.push(id);
  await db.execute(`UPDATE sync_folders SET ${setParts.join(", ")} WHERE id = $${i}`, args);
}

export async function removeSyncFolder(id: string) {
  const db = await getDb();
  await db.execute("DELETE FROM sync_folders WHERE id = $1", [id]);
}

export async function getSyncFiles(folderId?: string): Promise<SyncFile[]> {
  const db = await getDb();
  let rows;
  if (folderId) {
    rows = await db.select<any[]>("SELECT * FROM sync_files WHERE folder_id = $1", [folderId]);
  } else {
    rows = await db.select<any[]>("SELECT * FROM sync_files");
  }
  return rows.map((r) => ({
    id: r.id,
    folderId: r.folder_id,
    localPath: r.local_path,
    size: r.size,
    mtimeMs: r.mtime_ms,
    lastSyncedUtc: r.last_synced_utc,
    remoteFileId: r.remote_file_id,
  }));
}

export async function upsertSyncFile(file: Omit<SyncFile, "id"> & { id?: string }) {
  const db = await getDb();
  const id = file.id || crypto.randomUUID();
  await db.execute(
    `INSERT INTO sync_files (id, folder_id, local_path, size, mtime_ms, last_synced_utc, remote_file_id)
     VALUES ($1, $2, $3, $4, $5, $6, $7)
     ON CONFLICT(local_path) DO UPDATE SET
       size = excluded.size,
       mtime_ms = excluded.mtime_ms,
       last_synced_utc = excluded.last_synced_utc,
       remote_file_id = excluded.remote_file_id
    `,
    [id, file.folderId, file.localPath, file.size, file.mtimeMs, file.lastSyncedUtc, file.remoteFileId]
  );
  return id;
}

export async function logSync(message: string, level: "info" | "error" | "success" = "info") {
  const db = await getDb();
  await db.execute(
    "INSERT INTO sync_logs (id, message, level, created_at) VALUES ($1, $2, $3, $4)",
    [crypto.randomUUID(), message, level, new Date().toISOString()]
  );
  
  // Randomly trim logs ~10% of the time to prevent DB bloat
  if (Math.random() < 0.1) {
    await trimSyncLogs();
  }
}

export async function trimSyncLogs() {
  try {
    const db = await getDb();
    // Keep only the latest 1000 logs
    await db.execute(`
      DELETE FROM sync_logs WHERE id NOT IN (
        SELECT id FROM sync_logs ORDER BY created_at DESC LIMIT 1000
      )
    `);
  } catch (e) {
    console.warn("Failed to trim logs", e);
  }
}

export async function getSyncLogs(): Promise<{ id: string; message: string; level: string; created_at: string }[]> {
  const db = await getDb();
  return await db.select<any[]>("SELECT * FROM sync_logs ORDER BY created_at DESC LIMIT 100");
}

export async function clearSyncLogs() {
  const db = await getDb();
  await db.execute("DELETE FROM sync_logs");
}

export async function clearSyncState() {
  const db = await getDb();
  await db.execute("DELETE FROM sync_files");
  await db.execute("DELETE FROM sync_manifests");
  await db.execute("DELETE FROM sync_manifest_chunks");
  await db.execute("DELETE FROM sync_gc_jobs");
  await db.execute("DELETE FROM sync_upload_jobs");
}

export async function getSyncChunk(hash: string): Promise<SyncChunk | null> {
  const db = await getDb();
  const rows = await db.select<any[]>("SELECT * FROM sync_chunks WHERE hash = $1 LIMIT 1", [hash]);
  const row = rows[0];
  if (!row) return null;
  return {
    id: row.id,
    hash: row.hash,
    originalSize: row.original_size,
    storedSize: row.stored_size,
    encoding: row.encoding,
    uploadedAtUtc: row.uploaded_at_utc,
  };
}

export async function upsertSyncChunk(chunk: Omit<SyncChunk, "id"> & { id?: string }) {
  const db = await getDb();
  const id = chunk.id || crypto.randomUUID();
  await db.execute(
    `INSERT INTO sync_chunks (id, hash, original_size, stored_size, encoding, uploaded_at_utc)
     VALUES ($1, $2, $3, $4, $5, $6)
     ON CONFLICT(hash) DO UPDATE SET
       original_size = excluded.original_size,
       stored_size = excluded.stored_size,
       encoding = excluded.encoding,
       uploaded_at_utc = excluded.uploaded_at_utc`,
    [id, chunk.hash, chunk.originalSize, chunk.storedSize, chunk.encoding, chunk.uploadedAtUtc],
  );
  return id;
}

export async function deleteSyncChunk(hash: string) {
  const db = await getDb();
  await db.execute("DELETE FROM sync_chunks WHERE hash = $1", [hash]);
}

export async function upsertSyncManifest(manifest: Omit<SyncManifest, "id"> & { id?: string }) {
  const db = await getDb();
  const id = manifest.id || crypto.randomUUID();
  await db.execute(
    `INSERT INTO sync_manifests (id, folder_id, local_path, manifest_hash, remote_file_id, created_at_utc)
     VALUES ($1, $2, $3, $4, $5, $6)
     ON CONFLICT(folder_id, local_path, manifest_hash) DO UPDATE SET
       remote_file_id = excluded.remote_file_id,
       created_at_utc = excluded.created_at_utc`,
    [id, manifest.folderId, manifest.localPath, manifest.manifestHash, manifest.remoteFileId, manifest.createdAtUtc],
  );
  return id;
}

export async function getLatestSyncManifest(folderId: string, localPath: string): Promise<SyncManifest | null> {
  const db = await getDb();
  const rows = await db.select<any[]>(
    `SELECT * FROM sync_manifests
     WHERE folder_id = $1 AND local_path = $2
     ORDER BY created_at_utc DESC
     LIMIT 1`,
    [folderId, localPath],
  );
  const r = rows[0];
  if (!r) return null;
  return {
    id: r.id,
    folderId: r.folder_id,
    localPath: r.local_path,
    manifestHash: r.manifest_hash,
    remoteFileId: r.remote_file_id,
    createdAtUtc: r.created_at_utc,
  };
}

export async function replaceManifestChunkRefs(folderId: string, localPath: string, hashes: string[]): Promise<string[]> {
  const db = await getDb();
  const previous = await db.select<any[]>(
    `SELECT chunk_hash FROM sync_manifest_chunks
     WHERE folder_id = $1 AND local_path = $2`,
    [folderId, localPath],
  );
  const previousSet = new Set(previous.map((r) => String(r.chunk_hash)));
  const nextSet = new Set(hashes);
  const removed = [...previousSet].filter((h) => !nextSet.has(h));

  await db.execute(
    "DELETE FROM sync_manifest_chunks WHERE folder_id = $1 AND local_path = $2",
    [folderId, localPath],
  );
  for (const hash of nextSet) {
    await db.execute(
      "INSERT OR IGNORE INTO sync_manifest_chunks (folder_id, local_path, chunk_hash) VALUES ($1, $2, $3)",
      [folderId, localPath, hash],
    );
  }
  return removed;
}

export async function getChunkReferenceCount(hash: string): Promise<number> {
  const db = await getDb();
  const rows = await db.select<any[]>(
    "SELECT COUNT(*) AS c FROM sync_manifest_chunks WHERE chunk_hash = $1",
    [hash],
  );
  return Number(rows[0]?.c ?? 0);
}

export async function enqueueGcChunkJobs(hashes: string[]) {
  if (!hashes.length) return;
  const db = await getDb();
  const now = new Date().toISOString();
  for (const hash of new Set(hashes)) {
    await db.execute(
      `INSERT OR IGNORE INTO sync_gc_jobs
       (id, chunk_hash, status, attempts, next_attempt_utc, last_error, updated_at_utc)
       VALUES ($1, $2, 'pending', 0, $3, NULL, $4)`,
      [crypto.randomUUID(), hash, now, now],
    );
  }
}

export async function claimGcJobs(limit = 8): Promise<SyncGcJob[]> {
  const db = await getDb();
  const now = new Date().toISOString();
  const rows = await db.select<any[]>(
    `SELECT *
     FROM sync_gc_jobs
     WHERE status IN ('pending', 'failed') AND next_attempt_utc <= $1
     ORDER BY updated_at_utc ASC
     LIMIT $2`,
    [now, Math.max(1, limit)],
  );
  const claimed: SyncGcJob[] = [];
  for (const row of rows) {
    const updatedAtUtc = new Date().toISOString();
    await db.execute(
      `UPDATE sync_gc_jobs
       SET status = 'running', updated_at_utc = $1
       WHERE id = $2`,
      [updatedAtUtc, row.id],
    );
    claimed.push({
      id: row.id,
      chunkHash: row.chunk_hash,
      status: "running",
      attempts: Number(row.attempts ?? 0),
      nextAttemptUtc: row.next_attempt_utc,
      lastError: row.last_error ?? null,
      updatedAtUtc,
    });
  }
  return claimed;
}

export async function completeGcJob(id: string) {
  const db = await getDb();
  await db.execute("DELETE FROM sync_gc_jobs WHERE id = $1", [id]);
}

export async function failGcJob(id: string, attempts: number, error: string) {
  const db = await getDb();
  const nextAttempts = Math.max(1, attempts + 1);
  const backoffMs = Math.min(10 * 60 * 1000, Math.pow(2, nextAttempts) * 1000);
  const nextAttemptUtc = new Date(Date.now() + backoffMs).toISOString();
  const updatedAtUtc = new Date().toISOString();
  await db.execute(
    `UPDATE sync_gc_jobs
     SET status = 'failed',
         attempts = $1,
         next_attempt_utc = $2,
         last_error = $3,
         updated_at_utc = $4
     WHERE id = $5`,
    [nextAttempts, nextAttemptUtc, error.slice(0, 2000), updatedAtUtc, id],
  );
}

export async function getGcJobStats(): Promise<SyncGcJobStats> {
  const db = await getDb();
  const rows = await db.select<any[]>(
    `SELECT
       SUM(CASE WHEN status = 'pending' THEN 1 ELSE 0 END) AS pending_count,
       SUM(CASE WHEN status = 'running' THEN 1 ELSE 0 END) AS running_count,
       SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END) AS failed_count,
       COUNT(*) AS total_count,
       MIN(CASE WHEN status IN ('pending', 'failed') THEN updated_at_utc ELSE NULL END) AS oldest_pending_utc
     FROM sync_gc_jobs`,
  );
  const row = rows[0] ?? {};
  return {
    pending: Number(row.pending_count ?? 0),
    running: Number(row.running_count ?? 0),
    failed: Number(row.failed_count ?? 0),
    total: Number(row.total_count ?? 0),
    oldestPendingUtc: row.oldest_pending_utc ? String(row.oldest_pending_utc) : null,
  };
}

export async function getSyncUploadJob(folderId: string, localPath: string): Promise<SyncUploadJob | null> {
  const db = await getDb();
  const rows = await db.select<any[]>(
    `SELECT * FROM sync_upload_jobs
     WHERE folder_id = $1 AND local_path = $2
     LIMIT 1`,
    [folderId, localPath],
  );
  const row = rows[0];
  if (!row) return null;
  return {
    id: row.id,
    folderId: row.folder_id,
    localPath: row.local_path,
    size: Number(row.size ?? 0),
    mtimeMs: Number(row.mtime_ms ?? 0),
    sessionUrl: String(row.session_url ?? ""),
    uploadedBytes: Number(row.uploaded_bytes ?? 0),
    updatedAtUtc: String(row.updated_at_utc ?? ""),
  };
}

export async function upsertSyncUploadJob(job: Omit<SyncUploadJob, "id" | "updatedAtUtc"> & { id?: string }) {
  const db = await getDb();
  const id = job.id || crypto.randomUUID();
  const updatedAtUtc = new Date().toISOString();
  await db.execute(
    `INSERT INTO sync_upload_jobs
     (id, folder_id, local_path, size, mtime_ms, session_url, uploaded_bytes, updated_at_utc)
     VALUES ($1, $2, $3, $4, $5, $6, $7, $8)
     ON CONFLICT(folder_id, local_path) DO UPDATE SET
       size = excluded.size,
       mtime_ms = excluded.mtime_ms,
       session_url = excluded.session_url,
       uploaded_bytes = excluded.uploaded_bytes,
       updated_at_utc = excluded.updated_at_utc`,
    [id, job.folderId, job.localPath, job.size, job.mtimeMs, job.sessionUrl, job.uploadedBytes, updatedAtUtc],
  );
  return id;
}

export async function deleteSyncUploadJob(folderId: string, localPath: string) {
  const db = await getDb();
  await db.execute("DELETE FROM sync_upload_jobs WHERE folder_id = $1 AND local_path = $2", [folderId, localPath]);
}
