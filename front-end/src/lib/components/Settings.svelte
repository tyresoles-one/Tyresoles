<script lang="ts">
  import {
    appConfigStore,
    writeAppConfig,
    DEFAULT_APP_CONFIG,
    type AppConfig,
  } from "$lib/config/runtime";
  import { toast } from "svelte-sonner";
  import { onDestroy } from "svelte";

  import { isTauri } from "$lib/tauri";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";

  import { authStore } from "$lib/stores";

  // Local draft based on current store
  let draft = $state<AppConfig | null>(null);

  // Keep draft in sync if store changes
  const unsub = appConfigStore.subscribe((v) => {
    if (v) draft = { ...v };
  });

  onDestroy(unsub);

  let saving = $state(false);

  async function handleSave() {
    if (!draft) return;
    saving = true;
    try {
      await writeAppConfig(draft);
      toast.success("Settings saved successfully");
    } catch (e) {
      toast.error("Failed to save", { description: String(e) });
    } finally {
      saving = false;
    }
  }

  function handleReset() {
    if ($appConfigStore) {
      draft = { ...$appConfigStore };
    } else {
      draft = { ...DEFAULT_APP_CONFIG };
    }
  }
</script>

{#if draft}
  <div class="settings-container">
    <!-- Desktop App Promotion (Only on Web) -->
    {#if !isTauri()}
      <section class="download-banner">
        <div class="banner-glass"></div>
        <div class="banner-content">
          <div class="banner-icon-ring">
            <Icon name="monitor" class="size-8" />
          </div>
          <div class="banner-text">
            <h3>Get the Desktop Experience</h3>
            <p>
              For native ERP support, RDP launch, and multi-window workflows,
              download the Tyresoles app.
            </p>
          </div>
          <div class="banner-action">
            <a
              href={draft.downloadUrl}
              download
              data-sveltekit-reload
              target="_blank"
              rel="noopener noreferrer"
              class="btn btn--white"
            >
              <Icon name="download" class="size-4 mr-2" /> Download For Windows
            </a>
          </div>
        </div>
      </section>
    {/if}

    {#if $authStore.user?.userType.toUpperCase() === "SUPER"}
      <section class="settings-panel">
        <header class="settings-header">
          <div class="header-with-status">
            <h2>App Configuration</h2>
            {#if isTauri()}
              <span class="status-badge">
                <span class="status-dot"></span> Desktop App
              </span>
            {:else}
              <span class="status-badge web">
                <Icon name="globe" class="size-3 mr-1" /> Web Distribution
              </span>
            {/if}
          </div>
          <p class="settings-subtitle">
            Stored in your local environment — persisted across sessions
          </p>
        </header>

        <form
          class="settings-form"
          onsubmit={(e) => {
            e.preventDefault();
            handleSave();
          }}
        >
          <!-- Backend URL -->
          <div class="field">
            <label for="backendBaseUrl">Backend Base URL</label>
            <div class="input-wrapper">
              <Icon name="server" class="size-4 icon-prefix" />
              <input
                id="backendBaseUrl"
                type="url"
                bind:value={draft.backendBaseUrl}
                placeholder="http://api.tyresoles.net"
                required
              />
            </div>
            <span class="hint">The main API endpoint for data services.</span>
          </div>

          <div class="grid-2">
            <!-- Mode -->
            <div class="field">
              <label for="mode">Environment Mode</label>
              <select id="mode" bind:value={draft.mode}>
                <option value="User">User (Desktop)</option>
                <option value="Server">Server (Web)</option>
              </select>
            </div>

            <!-- Theme -->
            <div class="field">
              <label for="theme">UI Theme</label>
              <select id="theme" bind:value={draft.theme}>
                <option value="light">Light Mode</option>
                <option value="dark">Dark Mode</option>
              </select>
            </div>
          </div>

          <!-- Advanced Connection Settings (Restored from requirement) -->
          <div class="advanced-divider">
            <span>Connection Parameters</span>
          </div>

          <div class="grid-2">
            <div class="field">
              <label for="rdpUrl">RDP Gateway</label>
              <input id="rdpUrl" type="text" bind:value={draft.rdpUrl} />
            </div>
            <div class="field">
              <label for="maxRetries">Request Retries</label>
              <input
                id="maxRetries"
                type="number"
                min="0"
                max="10"
                bind:value={draft.maxRetries}
              />
            </div>
          </div>

          <div class="actions">
            <button
              type="button"
              class="btn btn--ghost"
              onclick={handleReset}
              disabled={saving}
            >
              Discard
            </button>
            <button type="submit" class="btn btn--primary" disabled={saving}>
              {saving ? "Applying..." : "Save Configuration"}
            </button>
          </div>
        </form>
      </section>
    {/if}
  </div>
{/if}

<style>
  .settings-container {
    max-width: 720px;
    margin: 2rem auto;
    display: flex;
    flex-direction: column;
    gap: 2rem;
  }

  /* Premium Download Banner */
  .download-banner {
    position: relative;
    border-radius: 20px;
    overflow: hidden;
    padding: 32px;
    background: linear-gradient(135deg, #2563eb 0%, #1e40af 100%);
    color: white;
    box-shadow: 0 10px 25px -5px rgba(37, 99, 235, 0.4);
  }

  .banner-glass {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: radial-gradient(
      circle at top right,
      rgba(255, 255, 255, 0.1) 0%,
      transparent 60%
    );
    pointer-events: none;
  }

  .banner-content {
    display: flex;
    align-items: center;
    gap: 24px;
    position: relative;
    z-index: 1;
  }

  .banner-icon-ring {
    width: 64px;
    height: 64px;
    background: rgba(255, 255, 255, 0.1);
    border: 1px solid rgba(255, 255, 255, 0.2);
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
    backdrop-filter: blur(4px);
  }

  .banner-text {
    flex: 1;
  }
  .banner-text h3 {
    margin: 0;
    font-size: 1.25rem;
    font-weight: 700;
  }
  .banner-text p {
    margin: 4px 0 0;
    font-size: 0.9rem;
    opacity: 0.9;
    line-height: 1.4;
  }

  /* Standard Settings Panel */
  .settings-panel {
    background: #0a0a0a;
    border: 1px solid #1a1a1a;
    border-radius: 20px;
    padding: 32px;
    color: #eee;
    box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);
  }

  .header-with-status {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 8px;
  }

  .status-badge {
    background: #022c22;
    color: #34d399;
    border: 1px solid #064e3b;
    padding: 4px 10px;
    border-radius: 999px;
    font-size: 0.75rem;
    font-weight: 600;
    display: flex;
    align-items: center;
  }
  .status-badge.web {
    background: #172554;
    color: #60a5fa;
    border-color: #1e3a8a;
  }

  .status-dot {
    width: 6px;
    height: 6px;
    background: #34d399;
    border-radius: 50%;
    margin-right: 6px;
  }

  .settings-form {
    display: flex;
    flex-direction: column;
    gap: 24px;
    margin-top: 32px;
  }
  .grid-2 {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 20px;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: 8px;
  }
  label {
    font-size: 0.75rem;
    font-weight: 700;
    color: #555;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .input-wrapper {
    position: relative;
    display: flex;
    align-items: center;
  }
  .icon-prefix {
    position: absolute;
    left: 12px;
    color: #444;
  }

  input,
  select {
    width: 100%;
    background: #000;
    border: 1px solid #222;
    border-radius: 10px;
    padding: 12px 14px;
    padding-left: 36px;
    color: #fff;
    font-size: 0.95rem;
    transition:
      border-color 0.2s,
      box-shadow 0.2s;
  }

  input:focus,
  select:focus {
    border-color: #3b82f6;
    outline: none;
    box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
  }

  .grid-2 input,
  .grid-2 select {
    padding-left: 14px;
  }

  .advanced-divider {
    display: flex;
    align-items: center;
    gap: 12px;
    margin: 12px 0;
  }
  .advanced-divider::after {
    content: "";
    flex: 1;
    height: 1px;
    background: #1a1a1a;
  }
  .advanced-divider span {
    font-size: 0.7rem;
    font-weight: 800;
    color: #333;
    text-transform: uppercase;
  }

  .hint {
    font-size: 0.75rem;
    color: #444;
    margin-top: 4px;
  }

  .actions {
    display: flex;
    gap: 16px;
    justify-content: flex-end;
    margin-top: 24px;
    padding-top: 32px;
    border-top: 1px solid #1a1a1a;
  }

  .btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 12px 24px;
    border-radius: 10px;
    font-weight: 600;
    cursor: pointer;
    border: none;
    transition:
      transform 0.1s,
      opacity 0.2s;
    text-decoration: none;
  }
  .btn:active {
    transform: scale(0.98);
  }
  .btn--white {
    background: white;
    color: #1e40af;
  }
  .btn--white:hover {
    background: #f8fafc;
  }
  .btn--ghost {
    background: transparent;
    color: #666;
    border: 1px solid #1a1a1a;
  }
  .btn--primary {
    background: #3b82f6;
    color: white;
  }
  .btn:disabled {
    opacity: 0.4;
    pointer-events: none;
  }

  .mr-2 {
    margin-right: 8px;
  }
  .mr-1 {
    margin-right: 4px;
  }
</style>
