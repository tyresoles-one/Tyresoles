<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import { invoke } from "@tauri-apps/api/core";
  import { listen } from "@tauri-apps/api/event";
  import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Progress } from "$lib/components/ui/progress";
  import { Icon } from "$lib/components/venUI/icon";
  import { toast } from "svelte-sonner";

  type SyncProgress = {
    total_bytes: number;
    transferred_bytes: number;
    current_file: string;
    percent: number;
  };

  let isSyncing = $state(false);
  let isPaused = $state(false);
  let progressData = $state<SyncProgress>({
    total_bytes: 0,
    transferred_bytes: 0,
    current_file: "Idle",
    percent: 0,
  });

  let unlisten: () => void;

  onMount(async () => {
    // Listen to the throttled event emitted from Rust
    try {
      unlisten = await listen<SyncProgress>("sync-progress", (event) => {
        progressData = event.payload;
        if (progressData.percent >= 100) {
          isSyncing = false;
          toast.success("Sync Complete!");
        }
      });
    } catch (e) {
      console.warn("Tauri API not available (running in browser)");
    }
  });

  onDestroy(() => {
    if (unlisten) unlisten();
  });

  async function toggleSync() {
    try {
      if (isSyncing) {
        await invoke("pause_sync");
        isSyncing = false;
        isPaused = true;
        toast.info("Sync Paused");
      } else {
        isSyncing = true;
        isPaused = false;
        toast.success("Sync Started");
        await invoke("start_sync");
      }
    } catch (e: any) {
      toast.error("Action failed", { description: e.toString() });
      isSyncing = false;
    }
  }

  function formatBytes(bytes: number) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
</script>

<div class="h-full flex flex-col gap-6 max-w-5xl mx-auto py-6 px-4 sm:px-6">
  
  <div class="flex items-center justify-between">
    <div>
      <h1 class="text-2xl font-bold tracking-tight text-foreground">Device Sync Client</h1>
      <p class="text-sm text-muted-foreground mt-1">Securely back up your local files to Google Drive.</p>
    </div>
  </div>

  <Card class="border-border/50 shadow-sm overflow-hidden">
    <!-- Header Banner -->
    <div class="h-2 w-full bg-gradient-to-r from-emerald-400 to-cyan-500"></div>
    <CardHeader class="pb-4">
      <CardTitle class="flex items-center gap-2">
        <Icon name="cloud-upload" class="size-5 text-primary" />
        Sync Engine Status
        {#if isSyncing}
          <span class="relative flex size-2.5 ml-2">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full size-2.5 bg-emerald-500"></span>
          </span>
        {/if}
      </CardTitle>
      <CardDescription>Monitor your background deduplication and transfer progress.</CardDescription>
    </CardHeader>

    <CardContent>
      <div class="grid gap-6">
        
        <!-- Controls & Meta -->
        <div class="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 p-4 rounded-xl border bg-muted/20">
          <div class="flex items-center gap-3">
            <div class={`p-2.5 rounded-lg ${isSyncing ? 'bg-primary/10 text-primary' : 'bg-muted text-muted-foreground'}`}>
               <Icon name={isSyncing ? "refresh-cw" : isPaused ? "pause" : "check-circle"} class={`size-6 ${isSyncing ? 'animate-spin-slow' : ''}`} />
            </div>
            <div>
              <h4 class="font-semibold text-sm">
                {isSyncing ? "Uploading..." : isPaused ? "Paused" : "Up to date"}
              </h4>
              <p class="text-[12px] text-muted-foreground font-mono mt-0.5 max-w-[280px] sm:max-w-md truncate">
                 {isSyncing ? progressData.current_file : "All monitored folders are synced."}
              </p>
            </div>
          </div>
          
          <Button variant={isSyncing ? "secondary" : "default"} class="w-full sm:w-auto shadow-sm transition-all" onclick={toggleSync}>
            <Icon name={isSyncing ? "pause" : "play"} class="size-4 mr-2" />
            {isSyncing ? "Pause Sync" : "Start Sync"}
          </Button>
        </div>

        <!-- Progress Bar -->
        {#if isSyncing || isPaused || progressData.percent > 0}
          <div class="grid gap-2">
            <div class="flex items-center justify-between text-xs font-medium text-muted-foreground">
               <span>{formatBytes(progressData.transferred_bytes)} / {formatBytes(progressData.total_bytes)}</span>
               <span class="text-primary">{progressData.percent.toFixed(1)}%</span>
            </div>
            <Progress value={progressData.percent} class="h-2.5" />
          </div>
        {/if}
      </div>
    </CardContent>
  </Card>

  <!-- Dummy File Explorer Mockup for Read-Only View -->
  <Card class="border-border/50 shadow-sm flex-1">
    <CardHeader class="pb-3 border-b">
      <CardTitle class="text-sm font-semibold flex items-center gap-2">
        <Icon name="folder" class="size-4 text-amber-500" />
        Synced Archive (Read-Only)
      </CardTitle>
    </CardHeader>
    <CardContent class="p-0">
      <div class="divide-y divide-border/50">
         {#each [1, 2, 3] as i}
          <div class="flex items-center justify-between p-3 px-4 hover:bg-muted/30 transition-colors cursor-pointer">
            <div class="flex items-center gap-3">
               <Icon name="file-text" class="size-4 text-blue-500" />
               <span class="text-sm font-medium">Monthly_Report_2026_0{i}.pdf</span>
            </div>
            <div class="flex items-center gap-4">
              <span class="text-xs text-muted-foreground font-mono">2.4 MB</span>
              <Button variant="ghost" size="icon" class="size-7 hover:text-primary">
                <Icon name="download" class="size-3.5" />
              </Button>
            </div>
          </div>
         {/each}
      </div>
    </CardContent>
  </Card>
</div>
