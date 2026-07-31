<script lang="ts">
  import { onMount } from "svelte";
  import { isTauri } from "$lib/tauri";
  import { PageHeading } from "$lib/components/venUI/page-heading";

  interface PstFileInfo {
    name: string;
    path: string;
    sizeMb: number;
    isLocked: boolean;
  }

  interface PstIndexerStatus {
    wsearchRunning: boolean;
    registryEnabled: boolean;
    outlookRunning: boolean;
    turboModeEnabled: boolean;
    osVersion: string;
    edbSizeMb: number;
    itemsToIndex: number;
    discoveredFiles: PstFileInfo[];
    scanpstAvailable: boolean;
    scanpstPath: string;
    catalogStatus: string;
  }

  interface DiagnosticStepResult {
    step: number;
    name: string;
    status: "ok" | "fixed" | "warning" | "error";
    details: string;
  }

  let status = $state<PstIndexerStatus | null>(null);
  let isLoading = $state(true);
  let isExecuting = $state(false);
  let isRoutineRunning = $state(false);
  let logs = $state<string[]>([]);
  let selectedPstPath = $state<string>("");
  let stagedRepairedPath = $state<string | null>(null);
  let diagnosticSteps = $state<DiagnosticStepResult[]>([]);

  // Log Modal
  let showLogModal = $state(false);
  let repairLogText = $state("");

  function addLog(msg: string) {
    const timestamp = new Date().toLocaleTimeString();
    logs = [...logs, `[${timestamp}] ${msg}`];
  }

  async function loadStatus() {
    isLoading = true;
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        status = await invoke<PstIndexerStatus>("get_pst_indexer_status");
        if (status?.discoveredFiles?.length && !selectedPstPath) {
          selectedPstPath = status.discoveredFiles[0].path;
        }
        addLog(`Status refreshed on ${status?.osVersion || "Windows"}. Queue: ${status?.itemsToIndex || 0} items.`);
      } else {
        // Mock data for browser dev environment
        status = {
          wsearchRunning: true,
          registryEnabled: true,
          outlookRunning: false,
          turboModeEnabled: true,
          osVersion: "Windows 11 Pro (Build 22631)",
          edbSizeMb: 1420.5,
          itemsToIndex: 84,
          discoveredFiles: [
            { name: "Outlook.pst", path: "C:\\Users\\User\\Documents\\Outlook Files\\Outlook.pst", sizeMb: 2450.5, isLocked: false },
            { name: "Archive2025.pst", path: "C:\\Users\\User\\AppData\\Local\\Microsoft\\Outlook\\Archive2025.pst", sizeMb: 890.1, isLocked: false }
          ],
          scanpstAvailable: true,
          scanpstPath: "C:\\Program Files\\Microsoft Office\\root\\Office16\\SCANPST.EXE",
          catalogStatus: "Active"
        };
        if (status.discoveredFiles.length && !selectedPstPath) {
          selectedPstPath = status.discoveredFiles[0].path;
        }
        addLog("[Browser Dev] Loaded mock status.");
      }
    } catch (err: any) {
      addLog(`Error loading status: ${err?.message || err}`);
    } finally {
      isLoading = false;
    }
  }

  async function handleSystematicRoutine() {
    isRoutineRunning = true;
    isExecuting = true;
    diagnosticSteps = [];
    addLog("🚀 Starting 7-Step Systematic Diagnostic & Remediation Routine...");
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        diagnosticSteps = await invoke<DiagnosticStepResult[]>("run_systematic_pst_repair_routine");
        for (const s of diagnosticSteps) {
          addLog(`[Step ${s.step}: ${s.name}] ${s.status.toUpperCase()} — ${s.details}`);
        }
      } else {
        await new Promise((r) => setTimeout(r, 1200));
        diagnosticSteps = [
          { step: 1, name: "OS & Environment Audit", status: "ok", details: "Detected OS: Windows 11 (Build 22631)" },
          { step: 2, name: "Outlook Handle Locks", status: "ok", details: "No active Outlook file locks detected." },
          { step: 3, name: "WSearch & Registry Policies", status: "fixed", details: "Enforced PreventIndexingOutlook=0 & EnableFdHost=1." },
          { step: 4, name: "Turbo Indexing Mode", status: "fixed", details: "Enabled DisableBackoff=1 to bypass idle throttling." },
          { step: 5, name: "Search DB Integrity", status: "ok", details: "Windows.edb database healthy (1,420 MB)." },
          { step: 6, name: "Crawl Scope Registration", status: "fixed", details: "Registered Outlook folders in Crawl Scope." },
          { step: 7, name: "COM Re-Index Trigger", status: "fixed", details: "Triggered SystemIndex rebuild via COM." }
        ];
        for (const s of diagnosticSteps) {
          addLog(`[Step ${s.step}: ${s.name}] ${s.status.toUpperCase()} — ${s.details}`);
        }
      }
      addLog("✅ Systematic Diagnostic Routine Completed Successfully!");
      await loadStatus();
    } catch (err: any) {
      addLog(`Routine Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
      isRoutineRunning = false;
    }
  }

  async function handleToggleTurbo(enable: boolean) {
    isExecuting = true;
    addLog(`${enable ? "Enabling" : "Disabling"} Turbo High-Speed Indexing Mode...`);
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("set_turbo_indexing_mode", { enabled: enable });
        addLog(`[OK] ${res}`);
      } else {
        await new Promise((r) => setTimeout(r, 500));
        addLog(`[OK] Turbo Indexing set to ${enable ? "Enabled" : "Disabled"}`);
        if (status) status.turboModeEnabled = enable;
      }
      await loadStatus();
    } catch (err: any) {
      addLog(`Turbo Mode Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleHardResetDb() {
    if (!confirm("CAUTION: This will delete/rename corrupt Windows.edb search database and force Windows Search to create a brand new index. Proceed?")) return;

    isExecuting = true;
    addLog("⚡ Executing Hard Reset of Windows Search DB (Windows.edb)...");
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("reset_windows_search_db");
        addLog(res);
      } else {
        await new Promise((r) => setTimeout(r, 1200));
        addLog("[OK] WSearch stopped, corrupt Windows.edb reset, WSearch restarted with clean database.");
      }
      await loadStatus();
    } catch (err: any) {
      addLog(`DB Reset Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleRegisterScope(path: string) {
    if (!path) return;
    isExecuting = true;
    addLog(`Registering path in Crawl Scope: ${path}`);
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("register_pst_crawl_scope", { folderPath: path });
        addLog(`[OK] ${res}`);
      } else {
        await new Promise((r) => setTimeout(r, 600));
        addLog(`[OK] Registered '${path}' in Windows Search Crawl Scope.`);
      }
    } catch (err: any) {
      addLog(`Crawl Scope Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleAutoFix() {
    isExecuting = true;
    addLog("Initiating 1-Click Auto-Fix & Indexing...");
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("run_pst_auto_fix");
        addLog(res);
      } else {
        await new Promise((r) => setTimeout(r, 1000));
        addLog("[OK] Enforced Registry Policies (PreventIndexingOutlook = 0)");
        addLog("[OK] Started WSearch Service");
        addLog("[OK] Triggered SystemIndex Rebuild via COM");
      }
      addLog("Auto-Fix completed successfully! Refreshing status...");
      await loadStatus();
    } catch (err: any) {
      addLog(`Auto-Fix Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleRebuildCatalog() {
    isExecuting = true;
    addLog("Triggering Windows Search Catalog Rebuild...");
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("rebuild_pst_search_catalog");
        addLog(res);
      } else {
        await new Promise((r) => setTimeout(r, 800));
        addLog("[Browser Dev] Rebuild triggered.");
      }
    } catch (err: any) {
      addLog(`Catalog Rebuild Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleRunScanPst() {
    if (!selectedPstPath) return;
    isExecuting = true;
    addLog(`Starting ScanPST Staging Copy & Repair for: ${selectedPstPath}`);
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("run_scanpst_repair_staging", { filePath: selectedPstPath });
        addLog(res);
        
        const fileName = selectedPstPath.split(/[\/\\]/).pop() || "";
        stagedRepairedPath = `C:\\Users\\Temp\\AppData\\Local\\Temp\\PST_Staging\\${fileName}`;
      } else {
        await new Promise((r) => setTimeout(r, 1500));
        addLog("[Browser Dev] Staging copy created and 2 ScanPST repair passes completed.");
        stagedRepairedPath = `C:\\Temp\\PST_Staging\\${selectedPstPath.split(/[\/\\]/).pop()}`;
      }
    } catch (err: any) {
      addLog(`ScanPST Repair Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleBrowsePst() {
    try {
      if (isTauri()) {
        const { open } = await import("@tauri-apps/plugin-dialog");
        const selected = await open({
          multiple: false,
          filters: [{ name: "Outlook Storage Files (*.pst, *.ost)", extensions: ["pst", "ost"] }]
        });
        if (selected && typeof selected === "string") {
          const fileName = selected.split(/[\/\\]/).pop() || "Custom.pst";
          const newFile: PstFileInfo = {
            name: fileName,
            path: selected,
            sizeMb: 0,
            isLocked: false
          };
          if (!status) {
            status = {
              wsearchRunning: true,
              registryEnabled: true,
              outlookRunning: false,
              turboModeEnabled: true,
              osVersion: "Windows 10/11",
              edbSizeMb: 0,
              itemsToIndex: 0,
              discoveredFiles: [newFile],
              scanpstAvailable: true,
              scanpstPath: "",
              catalogStatus: "Active"
            };
          } else {
            if (!status.discoveredFiles.some((f) => f.path === selected)) {
              status.discoveredFiles = [...status.discoveredFiles, newFile];
            }
          }
          selectedPstPath = selected;
          addLog(`Selected custom PST file: ${selected}`);
        }
      } else {
        const mockPath = "C:\\CustomFolder\\ExternalBackup.pst";
        if (status && !status.discoveredFiles.some((f) => f.path === mockPath)) {
          status.discoveredFiles = [
            ...status.discoveredFiles,
            { name: "ExternalBackup.pst", path: mockPath, sizeMb: 1200.0, isLocked: false }
          ];
        }
        selectedPstPath = mockPath;
        addLog("[Browser Dev] Selected custom PST file.");
      }
    } catch (err: any) {
      addLog(`Error selecting custom file: ${err?.message || err}`);
    }
  }

  async function handleCloseOutlook() {
    isExecuting = true;
    addLog("Closing OUTLOOK.EXE process...");
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("close_outlook_process");
        addLog(`[OK] ${res}`);
      } else {
        await new Promise((r) => setTimeout(r, 600));
        addLog("[Browser Dev] Outlook process terminated.");
        if (status) status.outlookRunning = false;
      }
      await loadStatus();
    } catch (err: any) {
      addLog(`Error closing Outlook: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleRestorePst() {
    if (!selectedPstPath || !stagedRepairedPath) return;
    if (!confirm(`Are you sure you want to replace original PST:\n${selectedPstPath}\nwith the repaired file?\nA .bak copy of original will be created automatically.`)) return;

    isExecuting = true;
    addLog(`Restoring repaired PST over original file...`);
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        const res = await invoke<string>("restore_repaired_pst", {
          stagedPath: stagedRepairedPath,
          originalPath: selectedPstPath
        });
        addLog(`[OK] ${res}`);
      } else {
        await new Promise((r) => setTimeout(r, 1000));
        addLog(`[OK] Successfully restored repaired PST to ${selectedPstPath}. Backup saved at ${selectedPstPath}.bak`);
      }
      await loadStatus();
    } catch (err: any) {
      addLog(`Restore Error: ${err?.message || err}`);
    } finally {
      isExecuting = false;
    }
  }

  async function handleViewLog() {
    if (!selectedPstPath) return;
    try {
      if (isTauri()) {
        const { invoke } = await import("@tauri-apps/api/core");
        repairLogText = await invoke<string>("get_scanpst_repair_log", { filePath: selectedPstPath });
      } else {
        repairLogText = `========================================================================\nMicrosoft Outlook Inbox Repair Tool (ScanPST.exe) Detailed Log Report\n========================================================================\nTarget PST File: ${selectedPstPath}\nScan Timestamp : ${new Date().toLocaleString()}\n\nPhase 1 - File System & Header Structure Check...\n  [OK] Valid PST 64-Bit NDB Header signature detected.\n  [OK] Block Allocation Map verified.\n\nPhase 2 - Folder & Node Structure Verification...\n  Scanned 1,842 Folders.\n  Scanned 28,490 Messages & Items.\n  Fixed 3 Index Page Header Discrepancies.\n\nPhase 3 - Repair Execution Summary...\n  Corrupt Items Found : 0\n  Orphaned Nodes Recovered: 2\n  Status: Repair passes completed successfully.\n========================================================================`;
      }
      showLogModal = true;
    } catch (err: any) {
      addLog(`Failed to fetch ScanPST log: ${err?.message || err}`);
    }
  }

  onMount(() => {
    loadStatus();
  });
</script>

<svelte:head>
  <title>Outlook PST Indexer & Repair — Tyresoles</title>
  <meta name="description" content="Automated Search & PST Indexer repair for Windows 10 & Windows 11." />
</svelte:head>

<div class="page-root">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="search"
    class="border-b bg-background"
  >
    {#snippet title()}
      <div class="flex items-center gap-3">
        <span>Outlook PST Indexer & Search Repair</span>
        {#if status?.osVersion}
          <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-primary/10 text-primary border border-primary/20">
            {status.osVersion}
          </span>
        {/if}
      </div>
    {/snippet}
    {#snippet description()}
      Seamless Windows 10 & 11 Search fix, automated 7-step systematic root-cause repair, Turbo Indexing mode, and PST recovery.
    {/snippet}
    {#snippet actions()}
      <div class="flex items-center gap-2">
        <button
          class="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-border bg-card text-xs font-semibold hover:bg-muted transition-colors disabled:opacity-50 cursor-pointer"
          onclick={loadStatus}
          disabled={isLoading || isExecuting}
        >
          <svg class:animate-spin={isLoading} class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M23 4v6h-6" /><path d="M1 20v-6h6" />
            <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
          </svg>
          Refresh
        </button>

        <button
          class="flex items-center gap-2 px-4 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-md transition-all disabled:opacity-50 cursor-pointer"
          onclick={handleSystematicRoutine}
          disabled={isLoading || isExecuting}
        >
          <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M22 11.08V12a10 10 0 11-5.93-9.14" /><polyline points="22 4 12 14.01 9 11.01" />
          </svg>
          Run 7-Step Systematic Auto-Fix
        </button>
      </div>
    {/snippet}
  </PageHeading>

  <main class="page-main">
    <!-- Outlook Lock Warning Alert Banner -->
    {#if status?.outlookRunning}
      <div class="mb-6 p-4 rounded-2xl bg-amber-500/10 border border-amber-500/20 text-amber-600 dark:text-amber-400 flex items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <svg class="size-5 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z" />
            <line x1="12" y1="9" x2="12" y2="13" />
            <line x1="12" y1="17" x2="12.01" y2="17" />
          </svg>
          <div>
            <div class="text-xs font-bold">Outlook (`OUTLOOK.EXE`) is currently running</div>
            <div class="text-[11px] opacity-90">Outlook holds file locks on PST/OST stores. Close Outlook before restoring or running repairs for best results.</div>
          </div>
        </div>
        <button
          class="px-3 py-1.5 rounded-lg bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold transition-colors shrink-0 disabled:opacity-50 cursor-pointer"
          onclick={handleCloseOutlook}
          disabled={isExecuting}
        >
          1-Click Close Outlook
        </button>
      </div>
    {/if}

    <!-- Systematic Diagnostic Stepper (Shown when routine has executed) -->
    {#if diagnosticSteps.length > 0}
      <div class="bg-card border border-border/60 rounded-2xl p-6 mb-8 shadow-sm">
        <div class="flex items-center justify-between mb-4">
          <h3 class="text-sm font-bold uppercase tracking-wider text-foreground">7-Step Systematic Diagnostic & Remediation Results</h3>
          <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">All Steps Executed</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-7 gap-2">
          {#each diagnosticSteps as step}
            <div class="p-3 rounded-xl border flex flex-col justify-between text-xs transition-all
              {step.status === 'ok' ? 'bg-emerald-500/5 border-emerald-500/30' : ''}
              {step.status === 'fixed' ? 'bg-indigo-500/10 border-indigo-500/40 text-indigo-400' : ''}
              {step.status === 'warning' ? 'bg-amber-500/10 border-amber-500/30' : ''}
              {step.status === 'error' ? 'bg-rose-500/10 border-rose-500/30' : ''}
            ">
              <div class="flex items-center justify-between mb-1">
                <span class="font-bold text-[10px] uppercase opacity-70">Step {step.step}</span>
                <span class="text-[10px] font-bold uppercase px-1.5 py-0.2 rounded-md
                  {step.status === 'ok' ? 'bg-emerald-500/20 text-emerald-500' : ''}
                  {step.status === 'fixed' ? 'bg-indigo-500/20 text-indigo-400' : ''}
                  {step.status === 'warning' ? 'bg-amber-500/20 text-amber-500' : ''}
                  {step.status === 'error' ? 'bg-rose-500/20 text-rose-500' : ''}
                ">{step.status}</span>
              </div>
              <div class="font-semibold text-foreground text-[11px] leading-tight mb-1">{step.name}</div>
              <div class="text-[10px] text-muted-foreground line-clamp-2" title={step.details}>{step.details}</div>
            </div>
          {/each}
        </div>
      </div>
    {/if}

    <!-- Status Cards Grid -->
    <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-5 gap-4 mb-8">
      <!-- Card 1: Windows Search Service -->
      <div class="p-4 rounded-2xl bg-card border border-border/60 shadow-sm relative overflow-hidden group">
        <div class="flex items-center justify-between mb-2">
          <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Windows Search</span>
          {#if status?.wsearchRunning}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">Running</span>
          {:else}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-rose-500/10 text-rose-500 border border-rose-500/20">Stopped</span>
          {/if}
        </div>
        <div class="text-base font-bold">{status?.wsearchRunning ? "Active Service" : "Service Offline"}</div>
        <p class="text-[10px] text-muted-foreground mt-1">Background email indexer.</p>
      </div>

      <!-- Card 2: Registry Indexing Policy -->
      <div class="p-4 rounded-2xl bg-card border border-border/60 shadow-sm relative overflow-hidden group">
        <div class="flex items-center justify-between mb-2">
          <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Registry Policy</span>
          {#if status?.registryEnabled}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">Allowed</span>
          {:else}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-amber-500/10 text-amber-500 border border-amber-500/20">Blocked</span>
          {/if}
        </div>
        <div class="text-base font-bold">{status?.registryEnabled ? "PST Allowed" : "Restricted"}</div>
        <p class="text-[10px] text-muted-foreground mt-1">Outlook indexing policy.</p>
      </div>

      <!-- Card 3: Turbo Indexing Mode -->
      <div class="p-4 rounded-2xl bg-card border border-border/60 shadow-sm relative overflow-hidden group">
        <div class="flex items-center justify-between mb-2">
          <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Turbo Mode</span>
          {#if status?.turboModeEnabled}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">Turbo Active</span>
          {:else}
            <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-muted text-muted-foreground border border-border">Throttled</span>
          {/if}
        </div>
        <div class="text-base font-bold">{status?.turboModeEnabled ? "5x High-Speed" : "Normal Speed"}</div>
        <p class="text-[10px] text-muted-foreground mt-1">Bypasses user idle delay.</p>
      </div>

      <!-- Card 4: Search DB Size (Windows.edb) -->
      <div class="p-4 rounded-2xl bg-card border border-border/60 shadow-sm relative overflow-hidden group">
        <div class="flex items-center justify-between mb-2">
          <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Search Database</span>
          <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-primary/10 text-primary border border-primary/20">Windows.edb</span>
        </div>
        <div class="text-base font-bold">{status?.edbSizeMb ? `${status.edbSizeMb} MB` : "Active DB"}</div>
        <p class="text-[10px] text-muted-foreground mt-1">Database file size.</p>
      </div>

      <!-- Card 5: Indexing Queue Counter -->
      <div class="p-4 rounded-2xl bg-card border border-border/60 shadow-sm relative overflow-hidden group">
        <div class="flex items-center justify-between mb-2">
          <span class="text-[11px] font-semibold text-muted-foreground uppercase tracking-wider">Index Queue</span>
          <span class="px-2 py-0.5 rounded-full text-[10px] font-medium bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">
            {status?.itemsToIndex || 0} Items
          </span>
        </div>
        <div class="text-base font-bold">{status?.itemsToIndex ? `${status.itemsToIndex} Remaining` : "Up to Date"}</div>
        <p class="text-[10px] text-muted-foreground mt-1">Items in Windows Search queue.</p>
      </div>
    </div>

    <!-- Quick Action Controls & Turbo Mode Toggle -->
    <div class="bg-card border border-border/60 rounded-2xl p-6 mb-8 shadow-sm">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-sm font-bold uppercase tracking-wider text-foreground">Advanced Optimization & Safe Hard Repair Controls</h3>
        
        <!-- Turbo Mode Switch -->
        <div class="flex items-center gap-3 bg-muted/40 border border-border px-3.5 py-1.5 rounded-xl">
          <span class="text-xs font-semibold">Turbo Indexing Mode (`DisableBackoff`)</span>
          <button
            class="px-3 py-1 rounded-lg text-xs font-bold transition-all cursor-pointer
              {status?.turboModeEnabled ? 'bg-indigo-600 text-white shadow-sm' : 'bg-muted border border-border text-muted-foreground hover:text-foreground'}
            "
            onclick={() => handleToggleTurbo(!status?.turboModeEnabled)}
            disabled={isExecuting}
          >
            {status?.turboModeEnabled ? "ENABLED ⚡" : "DISABLED"}
          </button>
        </div>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
        <!-- Control 1: Hard Reset Search DB -->
        <div class="flex items-center justify-between p-4 rounded-xl border border-rose-500/20 bg-rose-500/5">
          <div class="mr-2">
            <div class="font-semibold text-sm text-rose-500 dark:text-rose-400">Hard Reset `Windows.edb`</div>
            <div class="text-[11px] text-muted-foreground">Stops service, deletes corrupt DB, and creates fresh index.</div>
          </div>
          <button
            class="px-3.5 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold transition-colors disabled:opacity-50 shrink-0 cursor-pointer"
            onclick={handleHardResetDb}
            disabled={isExecuting}
          >
            Reset DB
          </button>
        </div>

        <!-- Control 2: Rebuild Search Catalog -->
        <div class="flex items-center justify-between p-4 rounded-xl border border-border bg-muted/20">
          <div>
            <div class="font-semibold text-sm">Rebuild Search Catalog</div>
            <div class="text-xs text-muted-foreground">Forces Windows Search to clear and re-catalog Outlook files.</div>
          </div>
          <button
            class="px-3.5 py-2 rounded-lg bg-primary text-primary-foreground text-xs font-semibold hover:opacity-90 transition-opacity disabled:opacity-50 shrink-0 cursor-pointer"
            onclick={handleRebuildCatalog}
            disabled={isExecuting}
          >
            Rebuild Index
          </button>
        </div>

        <!-- Control 3: Headless ScanPST Repair -->
        <div class="flex items-center justify-between p-4 rounded-xl border border-border bg-muted/20">
          <div class="mr-2">
            <div class="font-semibold text-sm">ScanPST Safe Repair</div>
            <div class="text-xs text-muted-foreground">Staging copy multi-pass repair.</div>
          </div>
          <div class="flex items-center gap-1.5 shrink-0">
            <button
              class="px-3 py-2 rounded-lg bg-amber-600 text-white text-xs font-semibold hover:bg-amber-500 transition-colors disabled:opacity-50 cursor-pointer"
              onclick={handleRunScanPst}
              disabled={isExecuting || !selectedPstPath}
            >
              Repair
            </button>
            {#if stagedRepairedPath}
              <button
                class="px-3 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold transition-colors disabled:opacity-50 cursor-pointer"
                onclick={handleRestorePst}
                disabled={isExecuting}
                title="Restore repaired file"
              >
                Restore
              </button>
            {/if}
          </div>
        </div>
      </div>
    </div>

    <!-- Discovered PST & OST Files Section -->
    <div class="bg-card border border-border/60 rounded-2xl p-6 mb-8 shadow-sm">
      <div class="flex items-center justify-between mb-4">
        <h3 class="text-sm font-bold uppercase tracking-wider text-foreground">Detected Outlook PST / OST Data Files</h3>
        <div class="flex items-center gap-2">
          {#if selectedPstPath}
            <button
              class="px-3 py-1.5 rounded-lg border border-border bg-muted/50 hover:bg-muted text-xs font-semibold transition-colors cursor-pointer"
              onclick={() => handleRegisterScope(selectedPstPath)}
              title="Register this PST directory in Windows Search Crawl Scope"
            >
              Add to Crawl Scope
            </button>
            <button
              class="px-3 py-1.5 rounded-lg border border-border bg-muted/50 hover:bg-muted text-xs font-semibold transition-colors cursor-pointer"
              onclick={handleViewLog}
            >
              View ScanPST Log
            </button>
          {/if}
          <button
            class="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-primary text-primary-foreground text-xs font-semibold hover:opacity-90 transition-opacity cursor-pointer"
            onclick={handleBrowsePst}
          >
            <svg class="size-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M22 19a2 2 0 01-2 2H4a2 2 0 01-2-2V5a2 2 0 012-2h5l2 3h9a2 2 0 012 2z" />
              <line x1="12" y1="11" x2="12" y2="17" /><line x1="9" y1="14" x2="15" y2="14" />
            </svg>
            Browse Custom PST...
          </button>
        </div>
      </div>

      {#if status?.discoveredFiles && status.discoveredFiles.length > 0}
        <div class="overflow-x-auto">
          <table class="w-full text-left border-collapse text-xs">
            <thead>
              <tr class="border-b border-border/60 text-muted-foreground uppercase tracking-wider font-semibold">
                <th class="py-2.5 px-3">Select</th>
                <th class="py-2.5 px-3">Filename</th>
                <th class="py-2.5 px-3">Size (MB)</th>
                <th class="py-2.5 px-3">Health / Alert</th>
                <th class="py-2.5 px-3">File Path</th>
                <th class="py-2.5 px-3">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-border/40">
              {#each status.discoveredFiles as file}
                <tr class="hover:bg-muted/30 transition-colors">
                  <td class="py-3 px-3">
                    <input
                      type="radio"
                      name="selectedPst"
                      value={file.path}
                      bind:group={selectedPstPath}
                      class="cursor-pointer accent-primary"
                    />
                  </td>
                  <td class="py-3 px-3 font-semibold text-foreground">{file.name}</td>
                  <td class="py-3 px-3 font-mono">{file.sizeMb > 0 ? `${file.sizeMb} MB` : "Custom"}</td>
                  <td class="py-3 px-3">
                    {#if file.sizeMb > 15000}
                      <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-rose-500/10 text-rose-500 border border-rose-500/20">Critical Size (>15GB)</span>
                    {:else if file.sizeMb > 8000}
                      <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-amber-500/10 text-amber-500 border border-amber-500/20">Large (>8GB)</span>
                    {:else}
                      <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-500 border border-emerald-500/20">Healthy</span>
                    {/if}
                  </td>
                  <td class="py-3 px-3 font-mono text-muted-foreground truncate max-w-xs" title={file.path}>{file.path}</td>
                  <td class="py-3 px-3">
                    <button
                      class="text-[11px] font-semibold text-primary hover:underline cursor-pointer"
                      onclick={() => handleRegisterScope(file.path)}
                    >
                      Scope +
                    </button>
                  </td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <div class="py-8 text-center text-muted-foreground text-xs">
          No PST or OST files detected in standard Outlook paths. Click "Browse Custom PST..." to select a file manually.
        </div>
      {/if}
    </div>

    <!-- Diagnostic Log Console -->
    <div class="bg-slate-950 text-slate-100 rounded-2xl p-6 border border-slate-800 shadow-lg">
      <div class="flex items-center justify-between mb-3 border-b border-slate-800 pb-3">
        <div class="flex items-center gap-2">
          <div class="size-3 rounded-full bg-emerald-500 animate-pulse"></div>
          <span class="text-xs font-mono font-bold uppercase tracking-wider text-slate-300">Live Diagnostic & Execution Terminal</span>
        </div>
        <button
          class="text-[11px] text-slate-400 hover:text-white transition-colors cursor-pointer"
          onclick={() => logs = []}
        >
          Clear Terminal
        </button>
      </div>

      <div class="font-mono text-xs space-y-1 max-h-60 overflow-y-auto pr-2 text-slate-300">
        {#if logs.length === 0}
          <div class="text-slate-600 italic">Terminal ready. Click "Run 7-Step Systematic Auto-Fix" to initiate full diagnostic routine...</div>
        {:else}
          {#each logs as logLine}
            <div class="leading-relaxed">
              {#if logLine.includes("[OK]") || logLine.includes("FIXED") || logLine.includes("Successfully")}
                <span class="text-emerald-400 font-semibold">{logLine}</span>
              {:else if logLine.includes("Error") || logLine.includes("WARNING") || logLine.includes("CAUTION")}
                <span class="text-amber-400 font-semibold">{logLine}</span>
              {:else}
                <span class="text-cyan-300">{logLine}</span>
              {/if}
            </div>
          {/each}
        {/if}
      </div>
    </div>
  </main>
</div>

<!-- ScanPST Log Report Viewer Modal -->
{#if showLogModal}
  <div class="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
    <div class="bg-card border border-border rounded-2xl max-w-3xl w-full max-h-[85vh] flex flex-col shadow-2xl overflow-hidden">
      <div class="p-4 border-b border-border flex items-center justify-between bg-muted/20">
        <div class="flex items-center gap-2 font-bold text-sm">
          <svg class="size-4 text-primary" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" /><polyline points="14 2 14 8 20 8" />
          </svg>
          ScanPST Repair Log Report
        </div>
        <button
          class="text-muted-foreground hover:text-foreground text-xs font-semibold px-2 py-1 rounded-md hover:bg-muted transition-colors cursor-pointer"
          onclick={() => showLogModal = false}
        >
          Close ✕
        </button>
      </div>

      <div class="p-4 overflow-y-auto flex-1 bg-slate-950 text-slate-200 font-mono text-xs whitespace-pre-wrap leading-relaxed">
        {repairLogText}
      </div>

      <div class="p-3 border-t border-border bg-muted/10 flex justify-end">
        <button
          class="px-4 py-1.5 rounded-lg bg-primary text-primary-foreground text-xs font-semibold hover:opacity-90 transition-opacity cursor-pointer"
          onclick={() => showLogModal = false}
        >
          Done
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  .page-root {
    min-height: 100svh;
    background: var(--background);
    color: var(--foreground);
    padding-bottom: 80px;
  }

  .page-main {
    max-width: 1200px;
    margin: 0 auto;
    padding: 32px 20px;
  }
</style>
