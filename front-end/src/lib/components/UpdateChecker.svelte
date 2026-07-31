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

{#if status !== "idle" && status !== "checking"}
  <div class="update-overlay" role="dialog" aria-modal="true">
    <div class="update-modal">
      {#if status === "available"}
        <span class="update-icon">🚀</span>
        <h2>Update Required</h2>
        <p class="update-msg">A new version (v{updateVersion}) of Tyresoles is available. You must update to continue using the application.</p>
        {#if updateNotes}
          <div class="update-notes-container">
            <strong>What's New:</strong>
            <p class="update-notes">{updateNotes}</p>
          </div>
        {/if}
        <button class="btn-update" onclick={downloadAndInstall}>
          Update Now
        </button>
      {/if}

      {#if status === "downloading"}
        <span class="update-icon anim-bounce">⬇️</span>
        <h2>Installing Update</h2>
        <p class="update-msg">Downloading and preparing v{updateVersion}. Please wait, the app will automatically restart.</p>
        <div class="progress-container">
          <div class="progress-bar-track">
            <div
              class="progress-bar-fill"
              style="width: {downloadProgress}%"
            ></div>
          </div>
          <span class="progress-text">{downloadProgress}%</span>
        </div>
      {/if}

      {#if status === "error"}
        <span class="update-icon">⚠️</span>
        <h2>Update Failed</h2>
        <p class="update-msg">{errorMessage}</p>
        <div class="update-actions">
          {#if retryCount < MAX_RETRIES}
            <button class="btn-update" onclick={retry}>
              Retry ({retryCount}/{MAX_RETRIES})
            </button>
          {:else}
            <p class="error-support-msg">Please restart the app or contact support if the problem persists.</p>
          {/if}
        </div>
      {/if}
    </div>
  </div>
{/if}

<!-- ── Styles ────────────────────────────────────────────────────── -->
<style>
  .update-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    z-index: 999999;
    background: rgba(15, 23, 42, 0.85); /* Slate 900 with opacity */
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 24px;
    animation: fadeIn 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  }

  .update-modal {
    width: 100%;
    max-width: 480px;
    background: linear-gradient(145deg, #1e293b 0%, #0f172a 100%);
    border: 1px solid rgba(59, 130, 246, 0.2); /* Subtle blue border */
    border-radius: 16px;
    padding: 32px;
    box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
    text-align: center;
    color: #f1f5f9;
    display: flex;
    flex-direction: column;
    align-items: center;
    animation: scaleIn 0.4s cubic-bezier(0.16, 1, 0.3, 1);
  }

  .update-icon {
    font-size: 3rem;
    margin-bottom: 16px;
    display: inline-block;
  }

  .anim-bounce {
    animation: bounce 2s infinite;
  }

  h2 {
    font-size: 1.5rem;
    font-weight: 700;
    margin: 0 0 12px 0;
    letter-spacing: -0.025em;
    background: linear-gradient(to right, #60a5fa, #3b82f6);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
  }

  .update-msg {
    font-size: 0.95rem;
    line-height: 1.5;
    color: #94a3b8;
    margin: 0 0 24px 0;
  }

  .update-notes-container {
    width: 100%;
    background: rgba(30, 41, 59, 0.5);
    border: 1px solid rgba(255, 255, 255, 0.05);
    border-radius: 8px;
    padding: 16px;
    margin-bottom: 24px;
    text-align: left;
    box-sizing: border-box;
  }

  .update-notes-container strong {
    font-size: 0.85rem;
    color: #cbd5e1;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .update-notes {
    margin: 8px 0 0 0;
    font-size: 0.9rem;
    line-height: 1.4;
    color: #94a3b8;
    max-height: 120px;
    overflow-y: auto;
    white-space: pre-wrap;
  }

  .update-actions {
    width: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
  }

  .btn-update {
    width: 100%;
    padding: 12px 24px;
    background: linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);
    color: #ffffff;
    border: none;
    border-radius: 8px;
    font-size: 1rem;
    font-weight: 600;
    cursor: pointer;
    box-shadow: 0 4px 14px rgba(59, 130, 246, 0.3);
    transition: all 0.25s ease;
  }

  .btn-update:hover {
    background: linear-gradient(135deg, #2563eb 0%, #1e40af 100%);
    box-shadow: 0 6px 20px rgba(59, 130, 246, 0.45);
    transform: translateY(-1px);
  }

  .btn-update:active {
    transform: translateY(1px);
  }

  .progress-container {
    width: 100%;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 8px;
  }

  .progress-bar-track {
    width: 100%;
    height: 6px;
    background: rgba(30, 41, 59, 0.8);
    border-radius: 3px;
    overflow: hidden;
  }

  .progress-bar-fill {
    height: 100%;
    background: linear-gradient(90deg, #3b82f6, #10b981);
    border-radius: 3px;
    transition: width 0.15s ease-out;
  }

  .progress-text {
    font-size: 0.85rem;
    font-weight: 600;
    color: #60a5fa;
  }

  .error-support-msg {
    font-size: 0.85rem;
    color: #f87171;
    margin: 8px 0 0 0;
  }

  @keyframes fadeIn {
    from { opacity: 0; }
    to { opacity: 1; }
  }

  @keyframes scaleIn {
    from {
      opacity: 0;
      transform: scale(0.95);
    }
    to {
      opacity: 1;
      transform: scale(1);
    }
  }

  @keyframes bounce {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-6px); }
  }
</style>
