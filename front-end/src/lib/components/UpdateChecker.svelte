<script lang="ts">
  import { onMount } from "svelte";
  import { get } from "svelte/store";
  import { isTauri } from "$lib/tauri";
  import { appConfigStore, DEFAULT_APP_CONFIG } from "$lib/config/runtime";

  // ── State ──────────────────────────────────────────────────────────
  let status: "idle" | "checking" | "available" | "downloading" | "error" =
    $state("idle");
  let updateVersion = $state("");
  let updateNotes = $state("");
  let downloadProgress = $state(0); // 0–100
  let errorMessage = $state("");
  let retryCount = $state(0);

  const MAX_RETRIES = 3;
  const CHECK_TIMEOUT_MS = 30_000;

  // Hold the update object between check and install
  let pendingUpdate: any = null;

  // ── Helpers ────────────────────────────────────────────────────────
  function getUpdateUrl(): string {
    const config = get(appConfigStore);
    return (
      config?.updateUrl ?? DEFAULT_APP_CONFIG.updateUrl
    );
  }

  async function sleep(ms: number) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  // ── Check for updates ──────────────────────────────────────────────
  async function checkForUpdate() {
    if (!isTauri()) return;

    // Skip update check if mode is Server
    const config = get(appConfigStore);
    if (config?.mode === "Server") {
      return;
    }

    status = "checking";
    errorMessage = "";

    try {
      const { check } = await import("@tauri-apps/plugin-updater");
      const updateUrl = getUpdateUrl();

      const update = await check({
        timeout: CHECK_TIMEOUT_MS,
        headers: {},
      });

      if (update) {
        pendingUpdate = update;
        updateVersion = update.version;
        updateNotes = update.body ?? "";
        status = "available";
        retryCount = 0;
      } else {
        status = "idle";
      }
    } catch (e: any) {
      console.warn("[updater] Check failed:", e);
      // Silent fail on check — don't bother the user
      status = "idle";
    }
  }

  // ── Download & Install ─────────────────────────────────────────────
  async function downloadAndInstall() {
    if (!pendingUpdate) return;
    status = "downloading";
    downloadProgress = 0;
    errorMessage = "";

    try {
      let downloaded = 0;
      let contentLength = 0;

      await pendingUpdate.downloadAndInstall(
        (event: { event: string; data: any }) => {
          switch (event.event) {
            case "Started":
              contentLength = event.data.contentLength ?? 0;
              downloadProgress = 0;
              break;
            case "Progress":
              downloaded += event.data.chunkLength;
              downloadProgress =
                contentLength > 0
                  ? Math.min(Math.round((downloaded / contentLength) * 100), 100)
                  : 0;
              break;
            case "Finished":
              downloadProgress = 100;
              break;
          }
        }
      );

      // On Windows the app auto-exits when install runs,
      // but if it doesn't, try to relaunch
      try {
        const { relaunch } = await import("@tauri-apps/plugin-process");
        await relaunch();
      } catch {
        // Install already closed the app
      }
    } catch (e: any) {
      console.error("[updater] Download/install failed:", e);
      errorMessage =
        e?.message ?? "Download failed. Please check your network connection.";
      status = "error";
    }
  }

  // ── Retry with backoff ─────────────────────────────────────────────
  async function retry() {
    if (retryCount >= MAX_RETRIES) {
      errorMessage = `Update failed after ${MAX_RETRIES} attempts. Please try again later.`;
      return;
    }
    retryCount++;
    const backoffMs = Math.min(1000 * Math.pow(2, retryCount - 1), 8000);
    status = "downloading";
    errorMessage = "";
    downloadProgress = 0;
    await sleep(backoffMs);
    await downloadAndInstall();
  }

  // ── Dismiss ────────────────────────────────────────────────────────
  function dismiss() {
    status = "idle";
    pendingUpdate = null;
  }

  // ── Auto-check on mount ────────────────────────────────────────────
  onMount(() => {
    // Small delay so the app UI loads first
    const timer = setTimeout(() => checkForUpdate(), 3000);
    return () => clearTimeout(timer);
  });
</script>

<!-- ── Template ──────────────────────────────────────────────────── -->

{#if status === "available"}
  <div class="update-banner" role="alert">
    <div class="update-banner-content">
      <div class="update-info">
        <span class="update-icon">🚀</span>
        <div>
          <strong>Update v{updateVersion} available</strong>
          {#if updateNotes}
            <p class="update-notes">{updateNotes}</p>
          {/if}
        </div>
      </div>
      <div class="update-actions">
        <button class="btn-update" onclick={downloadAndInstall}>
          Update Now
        </button>
        <button class="btn-dismiss" onclick={dismiss} aria-label="Dismiss">
          ✕
        </button>
      </div>
    </div>
  </div>
{/if}

{#if status === "downloading"}
  <div class="update-banner downloading" role="alert">
    <div class="update-banner-content">
      <div class="update-info">
        <span class="update-icon">⬇️</span>
        <div>
          <strong>Downloading update…</strong>
          <span class="progress-text">{downloadProgress}%</span>
        </div>
      </div>
      <div class="progress-bar-track">
        <div
          class="progress-bar-fill"
          style="width: {downloadProgress}%"
        ></div>
      </div>
    </div>
  </div>
{/if}

{#if status === "error"}
  <div class="update-banner error" role="alert">
    <div class="update-banner-content">
      <div class="update-info">
        <span class="update-icon">⚠️</span>
        <div>
          <strong>Update failed</strong>
          <p class="update-notes">{errorMessage}</p>
        </div>
      </div>
      <div class="update-actions">
        {#if retryCount < MAX_RETRIES}
          <button class="btn-update" onclick={retry}>
            Retry ({retryCount}/{MAX_RETRIES})
          </button>
        {/if}
        <button class="btn-dismiss" onclick={dismiss} aria-label="Dismiss">
          ✕
        </button>
      </div>
    </div>
  </div>
{/if}

<!-- ── Styles ────────────────────────────────────────────────────── -->
<style>
  .update-banner {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    z-index: 9999;
    background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
    color: #f1f5f9;
    border-bottom: 2px solid #3b82f6;
    box-shadow: 0 4px 24px rgba(0, 0, 0, 0.3);
    animation: slideDown 0.35s cubic-bezier(0.22, 1, 0.36, 1);
  }

  .update-banner.downloading {
    border-bottom-color: #22c55e;
  }

  .update-banner.error {
    border-bottom-color: #ef4444;
  }

  .update-banner-content {
    max-width: 960px;
    margin: 0 auto;
    padding: 12px 20px;
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
  }

  .update-info {
    display: flex;
    align-items: center;
    gap: 10px;
    flex: 1;
    min-width: 200px;
  }

  .update-icon {
    font-size: 1.3rem;
    flex-shrink: 0;
  }

  .update-notes {
    margin: 2px 0 0;
    font-size: 0.8rem;
    opacity: 0.75;
    line-height: 1.3;
    max-width: 400px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .update-actions {
    display: flex;
    align-items: center;
    gap: 8px;
    flex-shrink: 0;
  }

  .btn-update {
    padding: 6px 18px;
    background: #3b82f6;
    color: #fff;
    border: none;
    border-radius: 6px;
    font-size: 0.85rem;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s, transform 0.1s;
  }
  .btn-update:hover {
    background: #2563eb;
  }
  .btn-update:active {
    transform: scale(0.97);
  }

  .btn-dismiss {
    padding: 4px 8px;
    background: transparent;
    color: #94a3b8;
    border: 1px solid #334155;
    border-radius: 4px;
    cursor: pointer;
    font-size: 0.85rem;
    transition: color 0.2s, border-color 0.2s;
  }
  .btn-dismiss:hover {
    color: #f1f5f9;
    border-color: #64748b;
  }

  .progress-text {
    font-size: 0.8rem;
    opacity: 0.7;
    margin-left: 6px;
  }

  .progress-bar-track {
    width: 100%;
    height: 4px;
    background: #1e293b;
    border-radius: 2px;
    overflow: hidden;
  }

  .progress-bar-fill {
    height: 100%;
    background: linear-gradient(90deg, #22c55e, #4ade80);
    border-radius: 2px;
    transition: width 0.3s ease;
  }

  @keyframes slideDown {
    from {
      transform: translateY(-100%);
      opacity: 0;
    }
    to {
      transform: translateY(0);
      opacity: 1;
    }
  }
</style>
