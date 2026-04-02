<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import {
    checkServices,
    startService,
    stopService,
    restartService,
    type ServiceStatus,
    type ServiceDescriptor,
  } from "$lib/services/serviceChecker";
  import { isTauri } from "$lib/tauri";

  // ── Props ──────────────────────────────────────────────────────────────
  interface Props {
    /** List of services to monitor */
    services?: ServiceDescriptor[];
    /** Auto-refresh interval in ms (0 = disabled) */
    refreshInterval?: number;
    /** Show header card */
    showHeader?: boolean;
  }

  let {
    services = [
      { name: "TyrsolesApi", canStart: true, canStop: true },
      { name: "MSSQLSERVER", canStart: false, canStop: false },
      { name: "W3SVC", canStart: false, canStop: false },
    ],
    refreshInterval = 30_000,
    showHeader = true,
  }: Props = $props();

  // ── State ──────────────────────────────────────────────────────────────
  type ActionKind = "start" | "stop" | "restart" | null;
  interface ServiceRow extends ServiceStatus {
    actionLoading: ActionKind;
    lastError: string;
  }

  let rows = $state<ServiceRow[]>([]);
  let globalLoading = $state(false);
  let lastRefresh = $state<Date | null>(null);
  let refreshTimer: ReturnType<typeof setInterval> | null = null;
  let isDesktop = $state(false);

  // ── Computed ───────────────────────────────────────────────────────────
  const runningCount = $derived(rows.filter((r) => r.isRunning).length);
  const stoppedCount = $derived(rows.filter((r) => !r.isRunning && r.state !== "Unknown").length);
  const unknownCount = $derived(rows.filter((r) => r.state === "Unknown").length);
  const allHealthy = $derived(rows.length > 0 && runningCount === rows.length);

  // ── Helpers ────────────────────────────────────────────────────────────
  function makeRows(statuses: ServiceStatus[]): ServiceRow[] {
    return statuses.map((s) => ({
      ...s,
      actionLoading: null,
      lastError: "",
    }));
  }

  function mergeRows(fresh: ServiceStatus[]) {
    if (rows.length === 0) {
      rows = makeRows(fresh);
      return;
    }
    rows = fresh.map((s) => {
      const existing = rows.find((r) => r.name === s.name);
      return {
        ...s,
        actionLoading: existing?.actionLoading ?? null,
        lastError: existing?.lastError ?? "",
      };
    });
  }

  async function refresh() {
    if (globalLoading) return;
    globalLoading = true;
    try {
      const statuses = await checkServices(services);
      mergeRows(statuses);
      lastRefresh = new Date();
    } catch (e) {
      console.error("[ServiceChecker] refresh failed:", e);
    } finally {
      globalLoading = false;
    }
  }

  function updateRow(name: string, partial: Partial<ServiceRow>) {
    rows = rows.map((r) => (r.name === name ? { ...r, ...partial } : r));
  }

  async function handleAction(name: string, action: "start" | "stop" | "restart") {
    updateRow(name, { actionLoading: action, lastError: "" });
    try {
      let result: ServiceStatus;
      if (action === "start") result = await startService(name);
      else if (action === "stop") result = await stopService(name);
      else result = await restartService(name);

      updateRow(name, { ...result, actionLoading: null, lastError: "" });
      lastRefresh = new Date();
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      updateRow(name, { actionLoading: null, lastError: msg });
    }
  }

  function stateColor(state: string): string {
    if (state === "Running") return "running";
    if (state === "Stopped") return "stopped";
    if (state === "StartPending" || state === "StopPending") return "pending";
    return "unknown";
  }

  function formatTime(d: Date): string {
    return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
  }

  // ── Lifecycle ──────────────────────────────────────────────────────────
  onMount(async () => {
    isDesktop = isTauri();
    await refresh();
    if (refreshInterval > 0) {
      refreshTimer = setInterval(refresh, refreshInterval);
    }
  });

  onDestroy(() => {
    if (refreshTimer) clearInterval(refreshTimer);
  });
</script>

<div class="sc-wrapper">
  {#if showHeader}
    <div class="sc-header">
      <div class="sc-header-left">
        <div class="sc-icon-wrap" class:healthy={allHealthy}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8">
            <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          </svg>
        </div>
        <div>
          <h2 class="sc-title">Service Monitor</h2>
          <p class="sc-subtitle">Windows Server 2022 — Real-time service health</p>
        </div>
      </div>

      <div class="sc-header-right">
        <div class="sc-badge running">{runningCount} Running</div>
        {#if stoppedCount > 0}
          <div class="sc-badge stopped">{stoppedCount} Stopped</div>
        {/if}
        {#if unknownCount > 0}
          <div class="sc-badge unknown">{unknownCount} Unknown</div>
        {/if}
        <button
          class="sc-refresh-btn"
          onclick={refresh}
          disabled={globalLoading}
          title="Refresh"
          aria-label="Refresh service status"
        >
          <svg
            class:spin={globalLoading}
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
          >
            <path d="M23 4v6h-6" />
            <path d="M1 20v-6h6" />
            <path
              d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15"
            />
          </svg>
        </button>
      </div>
    </div>
  {/if}

  {#if !isDesktop}
    <div class="sc-notice">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <circle cx="12" cy="12" r="10" />
        <line x1="12" y1="8" x2="12" y2="12" />
        <line x1="12" y1="16" x2="12.01" y2="16" />
      </svg>
      Service management is only available in the desktop (Tauri) app.
    </div>
  {:else if rows.length === 0 && globalLoading}
    <div class="sc-loading-state">
      <div class="sc-spinner"></div>
      <span>Checking services…</span>
    </div>
  {:else}
    <div class="sc-grid">
      {#each rows as row (row.name)}
        {@const color = stateColor(row.state)}
        <div class="sc-card {color}">
          <!-- Status dot -->
          <div class="sc-dot {color}">
            {#if row.state === "StartPending" || row.state === "StopPending"}
              <span class="sc-dot-pulse"></span>
            {/if}
          </div>

          <!-- Info block -->
          <div class="sc-info">
            <div class="sc-name" title={row.name}>{row.displayName}</div>
            <div class="sc-service-id">{row.name}</div>
            <div class="sc-state-label {color}">{row.state}</div>
            {#if row.lastError}
              <div class="sc-error">{row.lastError}</div>
            {/if}
          </div>

          <!-- Actions -->
          <div class="sc-actions">
            {#if row.canStart && !row.isRunning && row.state !== "StartPending"}
              <button
                class="sc-btn sc-btn-start"
                onclick={() => handleAction(row.name, "start")}
                disabled={row.actionLoading !== null}
                title="Start service"
              >
                {#if row.actionLoading === "start"}
                  <span class="sc-btn-spinner"></span>
                {:else}
                  <svg viewBox="0 0 24 24" fill="currentColor">
                    <polygon points="5 3 19 12 5 21 5 3" />
                  </svg>
                {/if}
                Start
              </button>
            {/if}

            {#if row.canStop && row.isRunning && row.state !== "StopPending"}
              <button
                class="sc-btn sc-btn-stop"
                onclick={() => handleAction(row.name, "stop")}
                disabled={row.actionLoading !== null}
                title="Stop service"
              >
                {#if row.actionLoading === "stop"}
                  <span class="sc-btn-spinner"></span>
                {:else}
                  <svg viewBox="0 0 24 24" fill="currentColor">
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                  </svg>
                {/if}
                Stop
              </button>
            {/if}

            {#if (row.canStart || row.canStop) && row.state !== "StartPending" && row.state !== "StopPending"}
              <button
                class="sc-btn sc-btn-restart"
                onclick={() => handleAction(row.name, "restart")}
                disabled={row.actionLoading !== null}
                title="Restart service"
              >
                {#if row.actionLoading === "restart"}
                  <span class="sc-btn-spinner"></span>
                {:else}
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <path d="M23 4v6h-6" />
                    <path d="M3.51 9a9 9 0 0114.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0020.49 15" />
                  </svg>
                {/if}
                Restart
              </button>
            {/if}
          </div>
        </div>
      {/each}
    </div>
  {/if}

  {#if lastRefresh && isDesktop}
    <div class="sc-footer">
      <span>Last updated: {formatTime(lastRefresh)}</span>
      {#if refreshInterval > 0}
        <span>· Auto-refresh every {refreshInterval / 1000}s</span>
      {/if}
    </div>
  {/if}
</div>

<style>
  /* ── Reset & tokens ─────────────────────────────────────────────── */
  .sc-wrapper {
    --sc-radius: 14px;
    --sc-green: #22c55e;
    --sc-green-bg: rgba(34, 197, 94, 0.08);
    --sc-red: #ef4444;
    --sc-red-bg: rgba(239, 68, 68, 0.08);
    --sc-yellow: #f59e0b;
    --sc-yellow-bg: rgba(245, 158, 11, 0.08);
    --sc-grey: #64748b;
    --sc-grey-bg: rgba(100, 116, 139, 0.08);
    --sc-card-bg: hsl(220 18% 8%);
    --sc-surface: hsl(220 16% 12%);
    --sc-border: hsl(220 14% 18%);
    --sc-text: hsl(215 20% 92%);
    --sc-text-muted: hsl(215 12% 55%);

    font-family: "Inter", system-ui, sans-serif;
    color: var(--sc-text);
  }

  /* ── Header ─────────────────────────────────────────────────────── */
  .sc-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 12px;
    padding: 20px 24px;
    background: var(--sc-surface);
    border: 1px solid var(--sc-border);
    border-radius: var(--sc-radius);
    margin-bottom: 20px;
  }

  .sc-header-left {
    display: flex;
    align-items: center;
    gap: 14px;
  }

  .sc-icon-wrap {
    width: 46px;
    height: 46px;
    border-radius: 12px;
    background: var(--sc-grey-bg);
    border: 1.5px solid var(--sc-border);
    display: flex;
    align-items: center;
    justify-content: center;
    color: var(--sc-grey);
    transition: all 0.3s;
    flex-shrink: 0;
  }
  .sc-icon-wrap.healthy {
    background: var(--sc-green-bg);
    border-color: rgba(34, 197, 94, 0.25);
    color: var(--sc-green);
  }
  .sc-icon-wrap svg {
    width: 22px;
    height: 22px;
  }

  .sc-title {
    font-size: 1.1rem;
    font-weight: 700;
    letter-spacing: -0.01em;
    margin: 0 0 2px 0;
  }

  .sc-subtitle {
    font-size: 0.78rem;
    color: var(--sc-text-muted);
    margin: 0;
  }

  .sc-header-right {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-wrap: wrap;
  }

  /* ── Badges ─────────────────────────────────────────────────────── */
  .sc-badge {
    font-size: 0.72rem;
    font-weight: 600;
    padding: 4px 10px;
    border-radius: 20px;
    letter-spacing: 0.02em;
    text-transform: uppercase;
  }
  .sc-badge.running { background: var(--sc-green-bg); color: var(--sc-green); border: 1px solid rgba(34,197,94,0.2); }
  .sc-badge.stopped { background: var(--sc-red-bg);   color: var(--sc-red);   border: 1px solid rgba(239,68,68,0.2); }
  .sc-badge.unknown { background: var(--sc-grey-bg);  color: var(--sc-grey);  border: 1px solid rgba(100,116,139,0.2); }

  /* ── Refresh btn ─────────────────────────────────────────────────── */
  .sc-refresh-btn {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 36px;
    height: 36px;
    border-radius: 8px;
    background: transparent;
    border: 1.5px solid var(--sc-border);
    color: var(--sc-text-muted);
    cursor: pointer;
    transition: all 0.2s;
  }
  .sc-refresh-btn:hover:not(:disabled) {
    background: var(--sc-grey-bg);
    color: var(--sc-text);
    border-color: var(--sc-grey);
  }
  .sc-refresh-btn:disabled { opacity: 0.5; cursor: not-allowed; }
  .sc-refresh-btn svg { width: 16px; height: 16px; }

  @keyframes spin { to { transform: rotate(360deg); } }
  .spin { animation: spin 0.8s linear infinite; }

  /* ── Info / Notice ──────────────────────────────────────────────── */
  .sc-notice {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 16px 20px;
    background: var(--sc-surface);
    border: 1px solid var(--sc-border);
    border-radius: var(--sc-radius);
    font-size: 0.88rem;
    color: var(--sc-text-muted);
  }
  .sc-notice svg { width: 18px; height: 18px; flex-shrink: 0; }

  .sc-loading-state {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 40px;
    justify-content: center;
    color: var(--sc-text-muted);
    font-size: 0.9rem;
  }

  .sc-spinner {
    width: 22px;
    height: 22px;
    border: 2.5px solid var(--sc-border);
    border-top-color: hsl(217 91% 60%);
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
    flex-shrink: 0;
  }

  /* ── Grid ───────────────────────────────────────────────────────── */
  .sc-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
    gap: 14px;
  }

  /* ── Card ───────────────────────────────────────────────────────── */
  .sc-card {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: 10px;
    padding: 18px 18px 14px;
    background: var(--sc-surface);
    border: 1.5px solid var(--sc-border);
    border-radius: var(--sc-radius);
    transition: border-color 0.25s, box-shadow 0.25s, transform 0.2s;
    overflow: hidden;
  }
  .sc-card::before {
    content: "";
    position: absolute;
    inset: 0;
    border-radius: var(--sc-radius);
    opacity: 0;
    transition: opacity 0.3s;
    pointer-events: none;
  }
  .sc-card:hover { transform: translateY(-2px); }

  .sc-card.running {
    border-color: rgba(34, 197, 94, 0.35);
    box-shadow: 0 0 0 1px rgba(34,197,94,0.07), 0 4px 20px rgba(34,197,94,0.06);
  }
  .sc-card.running::before { background: radial-gradient(ellipse at top-left, rgba(34,197,94,0.07) 0%, transparent 65%); opacity: 1; }

  .sc-card.stopped {
    border-color: rgba(239, 68, 68, 0.35);
    box-shadow: 0 0 0 1px rgba(239,68,68,0.07), 0 4px 20px rgba(239,68,68,0.06);
  }
  .sc-card.stopped::before { background: radial-gradient(ellipse at top-left, rgba(239,68,68,0.07) 0%, transparent 65%); opacity: 1; }

  .sc-card.pending {
    border-color: rgba(245, 158, 11, 0.35);
    box-shadow: 0 0 0 1px rgba(245,158,11,0.07), 0 4px 20px rgba(245,158,11,0.06);
  }

  /* ── Status dot ─────────────────────────────────────────────────── */
  .sc-dot {
    position: relative;
    width: 10px;
    height: 10px;
    border-radius: 50%;
    flex-shrink: 0;
    align-self: flex-start;
    margin-top: 2px;
  }
  .sc-dot.running  { background: var(--sc-green); box-shadow: 0 0 8px rgba(34,197,94,0.5); }
  .sc-dot.stopped  { background: var(--sc-red);   box-shadow: 0 0 8px rgba(239,68,68,0.4); }
  .sc-dot.pending  { background: var(--sc-yellow); box-shadow: 0 0 8px rgba(245,158,11,0.4); }
  .sc-dot.unknown  { background: var(--sc-grey); }

  @keyframes pulse-ring {
    0%   { transform: scale(1); opacity: 0.8; }
    100% { transform: scale(2.4); opacity: 0; }
  }
  .sc-dot-pulse {
    position: absolute;
    inset: 0;
    border-radius: 50%;
    background: inherit;
    animation: pulse-ring 1.2s ease-out infinite;
  }

  /* ── Info ───────────────────────────────────────────────────────── */
  .sc-info {
    flex: 1;
    min-width: 0;
  }

  .sc-name {
    font-size: 0.95rem;
    font-weight: 600;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .sc-service-id {
    font-size: 0.72rem;
    color: var(--sc-text-muted);
    font-family: "JetBrains Mono", "Fira Code", monospace;
    margin-top: 1px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .sc-state-label {
    display: inline-block;
    font-size: 0.72rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-top: 6px;
    padding: 2px 7px;
    border-radius: 4px;
  }
  .sc-state-label.running { background: var(--sc-green-bg); color: var(--sc-green); }
  .sc-state-label.stopped { background: var(--sc-red-bg);   color: var(--sc-red); }
  .sc-state-label.pending { background: var(--sc-yellow-bg); color: var(--sc-yellow); }
  .sc-state-label.unknown { background: var(--sc-grey-bg);  color: var(--sc-grey); }

  .sc-error {
    font-size: 0.72rem;
    color: var(--sc-red);
    margin-top: 5px;
    line-height: 1.4;
    opacity: 0.9;
  }

  /* ── Action buttons ──────────────────────────────────────────────── */
  .sc-actions {
    display: flex;
    gap: 7px;
    flex-wrap: wrap;
    margin-top: 4px;
  }

  .sc-btn {
    display: inline-flex;
    align-items: center;
    gap: 5px;
    padding: 5px 12px;
    font-size: 0.75rem;
    font-weight: 600;
    border-radius: 7px;
    border: 1.5px solid transparent;
    cursor: pointer;
    transition: all 0.18s;
    letter-spacing: 0.02em;
    white-space: nowrap;
  }
  .sc-btn:disabled { opacity: 0.45; cursor: not-allowed; }
  .sc-btn svg { width: 13px; height: 13px; flex-shrink: 0; }

  .sc-btn-start {
    background: var(--sc-green-bg);
    color: var(--sc-green);
    border-color: rgba(34, 197, 94, 0.25);
  }
  .sc-btn-start:hover:not(:disabled) {
    background: rgba(34,197,94,0.18);
    border-color: var(--sc-green);
    box-shadow: 0 0 8px rgba(34,197,94,0.25);
  }

  .sc-btn-stop {
    background: var(--sc-red-bg);
    color: var(--sc-red);
    border-color: rgba(239, 68, 68, 0.25);
  }
  .sc-btn-stop:hover:not(:disabled) {
    background: rgba(239,68,68,0.18);
    border-color: var(--sc-red);
    box-shadow: 0 0 8px rgba(239,68,68,0.25);
  }

  .sc-btn-restart {
    background: var(--sc-grey-bg);
    color: var(--sc-text-muted);
    border-color: var(--sc-border);
  }
  .sc-btn-restart:hover:not(:disabled) {
    background: rgba(100,116,139,0.15);
    color: var(--sc-text);
    border-color: var(--sc-grey);
  }

  .sc-btn-spinner {
    display: inline-block;
    width: 11px;
    height: 11px;
    border: 1.5px solid rgba(255,255,255,0.2);
    border-top-color: currentColor;
    border-radius: 50%;
    animation: spin 0.7s linear infinite;
  }

  /* ── Footer ─────────────────────────────────────────────────────── */
  .sc-footer {
    margin-top: 14px;
    font-size: 0.72rem;
    color: var(--sc-text-muted);
    text-align: right;
  }
</style>
