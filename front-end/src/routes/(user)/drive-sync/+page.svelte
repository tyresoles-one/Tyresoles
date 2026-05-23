<script lang="ts">
  import { onMount } from "svelte";
  import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Icon } from "$lib/components/venUI/icon";
  import * as Tabs from "$lib/components/ui/tabs";
  import { toast } from "svelte-sonner";
  import { open, save } from "@tauri-apps/plugin-dialog";
  
  import {
    getDriveSyncConfigQuery,
    prepareDriveSyncUploadSession,
    restoreDifferentialBackupToPath,
    uploadFileWithResumableSession,
    type DriveSyncUserConfig,
  } from "$lib/services/driveSync";
  
  import {
    getSyncFolders,
    getSyncFiles,
    getSyncLogs,
    clearSyncLogs,
    clearSyncState,
    addSyncFolder,
    updateSyncFolder,
    removeSyncFolder,
    getGcJobStats,
    type SyncGcJobStats,
    type SyncFolder,
    type SyncFile
  } from "$lib/services/driveSyncLocalDb";
  
  import { driveSyncState, driveSyncGcState, playSync, pauseSync, stopSync } from "$lib/services/driveSyncWatcher";
  import { appLocalDataDir } from "@tauri-apps/api/path";
  import { readFile, copyFile } from "@tauri-apps/plugin-fs";
  
  let config = $state<DriveSyncUserConfig | null>(null);
  let folders = $state<SyncFolder[]>([]);
  let files = $state<SyncFile[]>([]);
  let logs = $state<any[]>([]);
  
  let loading = $state(false);
  let syncing = $state(false);
  let gcStats = $state<SyncGcJobStats>({
    pending: 0,
    running: 0,
    failed: 0,
    total: 0,
    oldestPendingUtc: null,
  });
  
  async function refreshAll() {
    loading = true;
    try {
      config = await getDriveSyncConfigQuery();
      folders = await getSyncFolders();
      files = await getSyncFiles();
      logs = await getSyncLogs();
      gcStats = await getGcJobStats();
    } catch (e: any) {
      toast.error(e.message || "Failed to load data");
    } finally {
      loading = false;
    }
  }
  
  onMount(() => {
    void refreshAll();
  });
  
  $effect(() => {
    if ($driveSyncState.isSyncingPass) {
      const interval = setInterval(async () => {
        logs = await getSyncLogs();
        gcStats = await getGcJobStats();
      }, 2000);
      return () => clearInterval(interval);
    }
  });

  $effect(() => {
    const interval = setInterval(async () => {
      gcStats = await getGcJobStats();
    }, 5000);
    return () => clearInterval(interval);
  });
  
  async function handleAddFolder() {
    try {
      const selectedPath = await open({ directory: true, multiple: false });
      if (selectedPath && typeof selectedPath === "string") {
        await addSyncFolder(selectedPath, "auto");
        toast.success("Folder added to sync list");
        await refreshAll();
      }
    } catch (e: any) {
      toast.error("Error picking folder: " + e.message);
    }
  }
  
  async function handleToggleFolder(f: SyncFolder) {
    try {
      await updateSyncFolder(f.id, { isActive: !f.isActive });
      await refreshAll();
    } catch (e: any) {
      toast.error(e.message);
    }
  }

  async function handleSaveFolderRules(folder: SyncFolder) {
    try {
      await updateSyncFolder(folder.id, {
        includePatternsJson: folder.includePatternsJson || null,
        excludePatternsJson: folder.excludePatternsJson || null,
        excludeDirectoriesJson: folder.excludeDirectoriesJson || null,
        maxFileSizeMb: folder.maxFileSizeMb,
        largeFileThresholdMb: folder.largeFileThresholdMb,
        concurrentUploads: folder.concurrentUploads,
        enableCompression: folder.enableCompression,
        enableDifferential: folder.enableDifferential,
        enableRclone: folder.enableRclone,
        rcloneBinaryPath: folder.rcloneBinaryPath || null,
      });
      toast.success("Folder sync rules updated");
      await refreshAll();
    } catch (e: any) {
      toast.error(e.message || "Failed to save folder rules");
    }
  }
  
  async function handleRemoveFolder(f: SyncFolder) {
    try {
      await removeSyncFolder(f.id);
      await refreshAll();
    } catch (e: any) {
      toast.error(e.message);
    }
  }
  
  async function handleManualSync() {
    playSync();
    toast.info("Started sync pass...");
  }

  async function handleRestoreFile(file: SyncFile) {
    try {
      const defaultName = file.localPath.split("/").pop() || file.localPath.split("\\").pop() || "restored-file";
      const outputPath = await save({
        title: "Restore backup to...",
        defaultPath: defaultName,
      });
      if (!outputPath || typeof outputPath !== "string") return;
      toast.info("Restoring differential backup...");
      await restoreDifferentialBackupToPath(file.localPath, outputPath);
      toast.success("Restore completed.");
    } catch (e: any) {
      toast.error(`Restore failed: ${e.message || String(e)}`);
    }
  }
  
  async function handleBackupDb() {
    try {
      toast.info("Backing up database to Drive...");
      const dbPath = await appLocalDataDir() + "/drivesync.db";
      const bytes = await readFile(dbPath);
      const blob = new Blob([bytes]);
      const file = new File([blob], "drivesync.db", { type: "application/octet-stream" });
      
      const session = await prepareDriveSyncUploadSession("drivesync.db", file.size, "system-backups");
      await uploadFileWithResumableSession(file, session);
      toast.success("Database backed up successfully");
    } catch (e: any) {
      toast.error("Backup failed: " + e.message);
    }
  }

  async function handleClearLogs() {
    try {
      await clearSyncLogs();
      toast.success("All logs cleared successfully");
      await refreshAll();
    } catch (e: any) {
      toast.error("Failed to clear logs: " + e.message);
    }
  }

  async function handleResetState() {
    if (!confirm("Are you sure you want to reset the local sync state? This will force the system to completely resync all files on the next pass.")) return;
    try {
      await clearSyncState();
      toast.success("Sync state reset successfully. All files will be uploaded on the next sync pass.");
      await refreshAll();
    } catch (e: any) {
      toast.error("Failed to reset sync state: " + e.message);
    }
  }
  
  function formatBytes(n: number) {
    if (n === 0) return "0 B";
    const k = 1024;
    const sizes = ["B", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(n) / Math.log(k));
    return `${parseFloat((n / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
  }
</script>

<div class="h-full flex flex-col gap-6 max-w-6xl mx-auto py-6 px-4 sm:px-6">
  <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-foreground">Drive Sync Manager</h1>
      <p class="text-sm text-muted-foreground mt-1">
        Fully automated background synchronization for Google Drive with local database state.
      </p>
    </div>
    <div class="flex gap-2">
      <Button variant="outline" size="sm" onclick={refreshAll} disabled={loading || $driveSyncState.isSyncingPass}>
        <Icon name="refresh-cw" class="size-4 mr-2 {(loading || $driveSyncState.isSyncingPass) ? 'animate-spin' : ''}" />
        Refresh
      </Button>
      {#if $driveSyncState.isWatcherActive}
        {#if $driveSyncState.shouldStop}
           <Button variant="outline" size="sm" disabled>
             <Icon name="loader-2" class="size-4 mr-2 animate-spin" />
             Stopping...
           </Button>
        {:else}
           <Button variant="outline" size="sm" onclick={pauseSync} class="text-amber-600 hover:text-amber-700 hover:bg-amber-50">
             <Icon name="pause" class="size-4 mr-2" />
             Pause
           </Button>
           <Button variant="outline" size="sm" onclick={stopSync} class="text-red-600 hover:text-red-700 hover:bg-red-50">
             <Icon name="square" class="size-4 mr-2" />
             Stop
           </Button>
        {/if}
      {:else}
        <Button variant="default" size="sm" onclick={handleManualSync} disabled={loading || $driveSyncState.isSyncingPass}>
          <Icon name="play" class="size-4 mr-2" />
          Play / Start Sync
        </Button>
      {/if}
    </div>
  </div>

  {#if $driveSyncState.isSyncingPass}
    <Card class="border-blue-500/30 bg-blue-500/5">
      <CardContent class="p-4 flex flex-col gap-2">
        <div class="flex items-center justify-between text-sm">
          <div class="font-medium flex items-center gap-2">
            <Icon name="loader-2" class="size-4 text-blue-500 animate-spin" />
            <span>Sync in progress...</span>
          </div>
          <div class="text-muted-foreground font-mono">
            {formatBytes($driveSyncState.uploadedBytes)} / {formatBytes($driveSyncState.totalBytes)}
          </div>
        </div>
        <div class="text-xs text-muted-foreground flex items-center justify-between">
          <span>Throughput: {formatBytes($driveSyncState.bytesPerSecond)}/s</span>
          <span>Active uploads: {$driveSyncState.activeUploads}</span>
        </div>
        
        <!-- Progress bar -->
        <div class="w-full bg-slate-200 dark:bg-slate-800 h-2 rounded-full overflow-hidden">
          <div 
            class="bg-blue-500 h-full transition-all duration-300" 
            style="width: {$driveSyncState.totalBytes ? Math.round(($driveSyncState.uploadedBytes / $driveSyncState.totalBytes) * 100) : 0}%"
          ></div>
        </div>

        <div class="flex items-center justify-between text-xs text-muted-foreground mt-1">
          <span class="truncate pr-4 flex-1">
            {#if $driveSyncState.currentFile}
               Uploading: <strong>{$driveSyncState.currentFile}</strong>
            {:else}
               Scanning files...
            {/if}
          </span>
          <span class="shrink-0">
            File {$driveSyncState.filesProcessed} of {$driveSyncState.filesTotal}
          </span>
        </div>
      </CardContent>
    </Card>
  {/if}

  <Card class="border-emerald-500/20 bg-emerald-500/5">
    <CardContent class="p-4">
      <div class="flex items-center justify-between text-sm">
        <div class="font-medium flex items-center gap-2">
          <Icon name="shield-check" class="size-4 text-emerald-600" />
          <span>Background GC Health</span>
        </div>
        <span class="text-xs text-muted-foreground">
          {$driveSyncGcState.isRunning ? 'Worker running' : 'Worker idle'}
        </span>
      </div>
      <div class="mt-2 grid grid-cols-2 md:grid-cols-6 gap-2 text-xs">
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Pending</div>
          <div class="font-semibold">{gcStats.pending}</div>
        </div>
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Running</div>
          <div class="font-semibold">{gcStats.running}</div>
        </div>
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Failed</div>
          <div class="font-semibold">{gcStats.failed}</div>
        </div>
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Last run</div>
          <div class="font-semibold">{$driveSyncGcState.lastRunAt ? new Date($driveSyncGcState.lastRunAt).toLocaleTimeString() : 'N/A'}</div>
        </div>
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Last batch</div>
          <div class="font-semibold">{$driveSyncGcState.lastProcessedJobs} jobs</div>
        </div>
        <div class="rounded border p-2 bg-background/40">
          <div class="text-muted-foreground">Adaptive chunk workers</div>
          <div class="font-semibold">{$driveSyncGcState.adaptiveChunkWorkers}</div>
          <div class="text-[10px] text-muted-foreground">
            {$driveSyncGcState.lastChunkLatencyMs > 0 ? `${Math.round($driveSyncGcState.lastChunkLatencyMs)} ms/chunk` : "No sample yet"}
          </div>
        </div>
      </div>
      {#if $driveSyncGcState.lastError}
        <div class="mt-2 text-xs text-red-500 break-all">
          Last GC error: {$driveSyncGcState.lastError}
        </div>
      {/if}
    </CardContent>
  </Card>

  {#if !loading && config && !config.isActive}
    <Card class="border-amber-500/30 bg-amber-500/5">
      <CardHeader>
        <CardTitle class="text-base">Not Enabled</CardTitle>
        <CardDescription>
          An administrator must set your <strong>Backup Folder ID</strong> on your Nav user record before you can use this module.
        </CardDescription>
      </CardHeader>
    </Card>
  {/if}

  <Tabs.Root value="explorer" class="w-full flex flex-col flex-1 min-h-0">
    <Tabs.List class="grid w-full grid-cols-3 max-w-md mx-auto mb-4">
      <Tabs.Trigger value="explorer">Explorer</Tabs.Trigger>
      <Tabs.Trigger value="config">Configuration</Tabs.Trigger>
      <Tabs.Trigger value="logs">Logs & Db</Tabs.Trigger>
    </Tabs.List>

    <Tabs.Content value="explorer" class="flex-1 mt-0">
      <Card class="border-border/50 shadow-sm h-full flex flex-col">
        <CardHeader class="pb-3 border-b">
          <CardTitle class="text-sm font-semibold flex items-center gap-2">
            <Icon name="folder-tree" class="size-4 text-blue-500" />
            File Explorer (Synced files)
          </CardTitle>
          <CardDescription class="text-xs">
            Files discovered in your configured local folders that are tracked by the local database.
          </CardDescription>
        </CardHeader>
        <CardContent class="p-0 flex-1 overflow-y-auto max-h-[600px]">
          {#if files.length === 0}
            <div class="p-8 text-center flex flex-col items-center justify-center text-muted-foreground">
              <Icon name="folder-open" class="size-10 mb-3 opacity-20" />
              <p>No files are currently tracked.</p>
              <p class="text-xs mt-1">Go to Configuration to add folders, then force a sync.</p>
            </div>
          {:else}
            <div class="divide-y divide-border/50">
              {#each files as f}
                <div class="flex items-center justify-between p-3 px-4 hover:bg-muted/30 transition-colors gap-3">
                  <div class="flex items-center gap-3 min-w-0 flex-1">
                    <Icon name="file" class="size-4 text-slate-500 shrink-0" />
                    <div class="flex flex-col min-w-0">
                      <span class="text-sm font-medium truncate">{f.localPath.split('/').pop() || f.localPath.split('\\').pop()}</span>
                      <span class="text-xs text-muted-foreground truncate">{f.localPath}</span>
                    </div>
                  </div>
                  <div class="flex items-center gap-4 shrink-0 text-right">
                    <div class="flex flex-col">
                      <span class="text-xs font-mono">{formatBytes(f.size)}</span>
                      <span class="text-[10px] text-muted-foreground">
                        {f.lastSyncedUtc ? new Date(f.lastSyncedUtc).toLocaleString() : 'Never synced'}
                      </span>
                    </div>
                    {#if f.lastSyncedUtc}
                      <Icon name="check-circle-2" class="size-4 text-green-500" />
                    {:else}
                      <Icon name="clock" class="size-4 text-amber-500" />
                    {/if}
                    <Button variant="outline" size="sm" class="h-8" onclick={() => handleRestoreFile(f)}>
                      <Icon name="download" class="size-4 mr-1" />
                      Restore
                    </Button>
                  </div>
                </div>
              {/each}
            </div>
          {/if}
        </CardContent>
      </Card>
    </Tabs.Content>

    <Tabs.Content value="config" class="flex-1 mt-0">
      <Card class="border-border/50 shadow-sm h-full">
        <CardHeader class="pb-3 border-b flex flex-row items-center justify-between">
          <div>
            <CardTitle class="text-sm font-semibold flex items-center gap-2">
              <Icon name="settings" class="size-4 text-emerald-500" />
              Sync Folders
            </CardTitle>
            <CardDescription class="text-xs mt-1">
              Select local folders to automatically upload to your Google Drive backup folder.
            </CardDescription>
          </div>
          <Button size="sm" onclick={handleAddFolder}>
            <Icon name="folder-plus" class="size-4 mr-2" />
            Add Folder
          </Button>
        </CardHeader>
        <CardContent class="p-0 max-h-[600px] overflow-y-auto">
          {#if folders.length === 0}
             <div class="p-8 text-center flex flex-col items-center justify-center text-muted-foreground">
              <Icon name="folder-search" class="size-10 mb-3 opacity-20" />
              <p>No local folders configured for sync.</p>
            </div>
          {:else}
            <div class="divide-y divide-border/50">
              {#each folders as folder}
                <div class="p-4 hover:bg-muted/30 transition-colors gap-3 border-b border-border/40">
                  <div class="flex items-center justify-between gap-3">
                  <div class="flex items-center gap-3 min-w-0 flex-1">
                    <Icon name="folder" class="size-5 text-amber-500 shrink-0" />
                    <div class="flex flex-col">
                      <span class="text-sm font-medium font-mono">{folder.localPath}</span>
                      <span class="text-xs text-muted-foreground">
                        Mode: <span class="capitalize">{folder.syncMode}</span>
                      </span>
                    </div>
                  </div>
                  <div class="flex items-center gap-2 shrink-0">
                    <Button 
                      variant={folder.isActive ? "default" : "secondary"} 
                      size="sm" 
                      class="h-8" 
                      onclick={() => handleToggleFolder(folder)}
                    >
                      {folder.isActive ? 'Active' : 'Paused'}
                    </Button>
                    <Button variant="destructive" size="sm" class="h-8 w-8 p-0" onclick={() => handleRemoveFolder(folder)}>
                      <Icon name="trash-2" class="size-4" />
                    </Button>
                  </div>
                  </div>
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-3 mt-4">
                    <div class="space-y-1">
                      <div class="text-xs text-muted-foreground">Include patterns (JSON array)</div>
                      <input class="w-full border rounded px-2 py-1 text-xs bg-background"
                             bind:value={folder.includePatternsJson}
                             placeholder='["**/*.pst","**/*.ost"]' />
                    </div>
                    <div class="space-y-1">
                      <div class="text-xs text-muted-foreground">Exclude patterns (JSON array)</div>
                      <input class="w-full border rounded px-2 py-1 text-xs bg-background"
                             bind:value={folder.excludePatternsJson}
                             placeholder='["**/*.tmp","**/~$*"]' />
                    </div>
                    <div class="space-y-1">
                      <div class="text-xs text-muted-foreground">Exclude directories (JSON array)</div>
                      <input class="w-full border rounded px-2 py-1 text-xs bg-background"
                             bind:value={folder.excludeDirectoriesJson}
                             placeholder='["node_modules",".git","AppData/Local/Temp"]' />
                    </div>
                    <div class="grid grid-cols-2 gap-2">
                      <div class="space-y-1">
                        <div class="text-xs text-muted-foreground">Max file size (MB)</div>
                        <input class="w-full border rounded px-2 py-1 text-xs bg-background" type="number" min="1" bind:value={folder.maxFileSizeMb} />
                      </div>
                      <div class="space-y-1">
                        <div class="text-xs text-muted-foreground">Differential threshold (MB)</div>
                        <input class="w-full border rounded px-2 py-1 text-xs bg-background" type="number" min="8" bind:value={folder.largeFileThresholdMb} />
                      </div>
                    </div>
                    <div class="grid grid-cols-2 gap-2">
                      <div class="space-y-1">
                        <div class="text-xs text-muted-foreground">Concurrent uploads</div>
                        <input class="w-full border rounded px-2 py-1 text-xs bg-background" type="number" min="1" max="6" bind:value={folder.concurrentUploads} />
                      </div>
                      <div class="space-y-1 flex items-end gap-3 pb-1">
                        <label class="text-xs flex items-center gap-2"><input type="checkbox" bind:checked={folder.enableCompression} /> Compression</label>
                        <label class="text-xs flex items-center gap-2"><input type="checkbox" bind:checked={folder.enableDifferential} /> Differential</label>
                        <label class="text-xs flex items-center gap-2"><input type="checkbox" bind:checked={folder.enableRclone} /> rclone transport</label>
                      </div>
                    </div>
                    <div class="space-y-1">
                      <div class="text-xs text-muted-foreground">rclone binary path (optional)</div>
                      <input class="w-full border rounded px-2 py-1 text-xs bg-background"
                             bind:value={folder.rcloneBinaryPath}
                             placeholder='rclone or C:\\Tools\\rclone.exe' />
                    </div>
                  </div>
                  <div class="mt-3 flex justify-end">
                    <Button size="sm" variant="outline" onclick={() => handleSaveFolderRules(folder)}>Save Rules</Button>
                  </div>
                </div>
              {/each}
            </div>
          {/if}
        </CardContent>
      </Card>
    </Tabs.Content>

    <Tabs.Content value="logs" class="flex-1 mt-0">
      <Card class="border-border/50 shadow-sm h-full flex flex-col">
        <CardHeader class="pb-3 border-b flex flex-row items-center justify-between">
          <div>
            <CardTitle class="text-sm font-semibold flex items-center gap-2">
              <Icon name="database" class="size-4 text-purple-500" />
              Local Database & Logs
            </CardTitle>
            <CardDescription class="text-xs mt-1">
              View system events or manually backup your local state to reproduce across devices.
            </CardDescription>
          </div>
          <div class="flex gap-2">
            <Button variant="outline" size="sm" onclick={handleResetState} class="text-orange-500 hover:text-orange-600 hover:bg-orange-50">
              <Icon name="refresh-ccw" class="size-4 mr-2" />
              Reset Sync State
            </Button>
            <Button variant="outline" size="sm" onclick={handleClearLogs} class="text-red-500 hover:text-red-600 hover:bg-red-50">
              <Icon name="trash-2" class="size-4 mr-2" />
              Clear Logs
            </Button>
            <Button variant="outline" size="sm" onclick={handleBackupDb}>
              <Icon name="cloud-upload" class="size-4 mr-2 text-blue-500" />
              Backup State
            </Button>
          </div>
        </CardHeader>
        <CardContent class="p-0 flex-1 overflow-y-auto max-h-[600px] bg-black/5 dark:bg-black/20 font-mono text-xs">
           {#if logs.length === 0}
             <p class="p-6 text-muted-foreground text-center">No logs recorded yet.</p>
           {:else}
             <div class="p-4 space-y-1">
               {#each logs as log}
                 <div class="flex gap-3 {log.level === 'error' ? 'text-red-500' : log.level === 'success' ? 'text-green-500' : 'text-slate-600 dark:text-slate-400'}">
                   <span class="shrink-0 opacity-70">[{new Date(log.created_at).toLocaleTimeString()}]</span>
                   <span class="shrink-0 uppercase w-[60px]">{log.level}</span>
                   <span class="break-all">{log.message}</span>
                 </div>
               {/each}
             </div>
           {/if}
        </CardContent>
      </Card>
    </Tabs.Content>
  </Tabs.Root>
</div>
