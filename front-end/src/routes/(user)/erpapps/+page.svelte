<script lang="ts">
  import { onMount } from "svelte";
  import {
    appConfigStore,
    writeAppConfig,
    type AppConfig,
  } from "$lib/config/runtime";
  import { authStore, getAuthToken } from "$lib/stores/auth";
  import { isTauri, getInvoke } from "$lib/tauri";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Progress } from "$lib/components/ui/progress";
  import { toast } from "$lib/components/venUI/toast";
  import {
    useGraphQLQuery,
    buildQuery,
    graphqlQuery,
  } from "$lib/services/graphql";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";

  let config = $state<AppConfig | null>(null);
  let loadingCode = $state<string | null>(null);
  let forticlientInstalled = $state<boolean | null>(null);
  let forticlientStatusLoading = $state(false);
  let forticlientUninstallLoading = $state(false);

  type VpnInstallerProgressEvt = {
    phase: string;
    receivedBytes: number;
    totalBytes: number | null;
    message: string;
  };

  type VpnDiskStatus = {
    directory: string;
    phase: string;
    receivedBytes: number;
    totalBytes: number | null;
    localFilePath: string | null;
    lastError: string | null;
    url: string | null;
  };

  let vpnDiskStatus = $state<VpnDiskStatus | null>(null);
  let vpnDiskLoading = $state(false);
  let vpnDownloadRunning = $state(false);
  let vpnProgress = $state<VpnInstallerProgressEvt | null>(null);
  let vpnUrlDraft = $state("");
  let vpnUrlSaving = $state(false);
  /** Avoid overwriting the input while typing; hydrate once when config first loads. */
  let vpnUrlDraftHydrated = $state(false);

  $effect(() => {
    if (config && !vpnUrlDraftHydrated) {
      vpnUrlDraft = config.downloadVpnUrl ?? "";
      vpnUrlDraftHydrated = true;
    }
  });

  function formatBytes(n: number): string {
    if (n === 0) return "0 B";
    const k = 1024;
    const i = Math.floor(Math.log(n) / Math.log(k));
    const u = ["B", "KB", "MB", "GB", "TB"][Math.min(i, 4)] ?? "B";
    return `${parseFloat((n / Math.pow(k, i)).toFixed(2))} ${u}`;
  }

  const vpnBarPercent = $derived.by(() => {
    const p = vpnProgress;
    if (!p?.totalBytes || p.totalBytes <= 0) return 0;
    return Math.min(100, Math.round((p.receivedBytes / p.totalBytes) * 100));
  });

  const GetGroupDetailsQuery = buildQuery`
    query GetGroupDetails($category: String!, $codes: String) {
      groupDetails(category: $category, codes: $codes) {
        code
        category
        name
        value
        extraValue
      }
    }
  `;

  const GetUserQuery = buildQuery`
    query GetUser($username: String!) {
      user(username: $username) {
        userId
        fullName
        rdpPassword
        navConfigName
      }
    }
  `;

  const GetVpnInstallerConfigQuery = buildQuery`
    query GetVpnInstallerConfig {
      vpnInstallerConfig {
        downloadUrl
        sha256Hex
        fileName
        isZipArchive
        zipEntryName
      }
    }
  `;

  // Fetch RUNERP group details on mount
  const runErpResult = useGraphQLQuery<any>(GetGroupDetailsQuery, {
    variables: { category: "RUNERP" },
    cacheKey: "group_details_runerp",
  });

  const vpnInstallerGql = useGraphQLQuery<any>(GetVpnInstallerConfigQuery, {
    variables: {},
    cacheKey: "vpn_installer_config",
  });

  /** VPN download URL: `app-config.json` `downloadVpnUrl` wins over API. */
  const effectiveVpnDownloadUrl = $derived.by(() => {
    const local = config?.downloadVpnUrl?.trim() ?? "";
    const api =
      vpnInstallerGql.data?.vpnInstallerConfig?.downloadUrl?.trim() ?? "";
    return local || api;
  });

  const vpnInstallerMeta = $derived.by(
    () => vpnInstallerGql.data?.vpnInstallerConfig,
  );

  async function refreshForticlientStatus() {
    if (!isTauri()) return;
    forticlientStatusLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) {
        forticlientInstalled = null;
        return;
      }
      const r = (await invoke("forticlient_installation_status")) as {
        installed: boolean;
      };
      forticlientInstalled = r.installed;
    } catch {
      forticlientInstalled = null;
    } finally {
      forticlientStatusLoading = false;
    }
  }

  async function saveVpnDownloadUrl() {
    const c = config;
    if (!c) {
      toast.error("Configuration not loaded.");
      return;
    }
    if (!isTauri()) {
      toast.info("Saving the VPN URL is only available in the desktop app.");
      return;
    }
    const trimmed = vpnUrlDraft.trim();
    if (trimmed.length > 0) {
      try {
        const u = new URL(trimmed);
        if (u.protocol !== "https:" && u.protocol !== "http:") {
          toast.error("URL must start with http:// or https://");
          return;
        }
      } catch {
        toast.error("Enter a valid URL, or leave empty to use the API only.");
        return;
      }
    }
    vpnUrlSaving = true;
    try {
      await writeAppConfig({
        ...c,
        downloadVpnUrl: trimmed,
      });
      vpnUrlDraft = trimmed;
      toast.success("VPN installer URL saved.");
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      vpnUrlSaving = false;
    }
  }

  async function refreshVpnDiskStatus() {
    if (!isTauri()) return;
    vpnDiskLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) {
        vpnDiskStatus = null;
        return;
      }
      vpnDiskStatus = (await invoke("vpn_installer_disk_status")) as VpnDiskStatus;
    } catch {
      vpnDiskStatus = null;
    } finally {
      vpnDiskLoading = false;
    }
  }

  async function handleVpnDownload() {
    const url = effectiveVpnDownloadUrl;
    if (!url) {
      toast.error(
        "VPN installer URL is not set. Enter a URL above or configure VpnInstaller on the API.",
      );
      return;
    }
    const meta = vpnInstallerGql.data?.vpnInstallerConfig;
    if (vpnDownloadRunning) return;
    vpnDownloadRunning = true;
    vpnProgress = null;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const path = (await invoke("vpn_installer_download", {
        args: {
          url,
          sha256Hex: meta?.sha256Hex ?? null,
          fileName: meta?.fileName ?? null,
          isZipArchive: !!meta?.isZipArchive,
          zipEntryName: meta?.zipEntryName ?? null,
          bearerToken: getAuthToken() || null,
        },
      })) as string;
      const shortPath = path.length > 80 ? path.slice(0, 80) + "…" : path;
      toast.success("VPN installer downloaded.", shortPath);
      await refreshVpnDiskStatus();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      if (!/cancel/i.test(msg)) {
        toast.error(msg || "Download failed.");
      } else {
        toast.info("Download cancelled.");
      }
    } finally {
      vpnDownloadRunning = false;
      vpnProgress = null;
      await refreshVpnDiskStatus();
    }
  }

  async function handleVpnCancel() {
    try {
      const { invoke } = await import("@tauri-apps/api/core");
      await invoke("vpn_installer_cancel");
    } catch {
      /* noop */
    }
  }

  async function handleOpenVpnFolder() {
    try {
      const { invoke } = await import("@tauri-apps/api/core");
      await invoke("vpn_installer_open_folder");
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Could not open folder.");
    }
  }

  async function handleUninstallForticlient() {
    if (forticlientUninstallLoading || !forticlientInstalled) return;
    forticlientUninstallLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      await invoke("uninstall_forticlient_silent");
      toast.success("FortiClient was uninstalled silently.");
      await refreshForticlientStatus();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Uninstall failed.");
    } finally {
      forticlientUninstallLoading = false;
    }
  }

  onMount(() => {
    let unlistenVpn: (() => void) | undefined;

    void (async () => {
      if (isTauri()) {
        try {
          const { listen } = await import("@tauri-apps/api/event");
          unlistenVpn = await listen<VpnInstallerProgressEvt>(
            "vpn-installer-progress",
            (event) => {
              vpnProgress = event.payload;
            },
          );
        } catch {
          /* not in Tauri */
        }
      }
    })();

    const unsub = appConfigStore.subscribe((c) => {
      config = c;
      if (c?.mode === "User" && isTauri()) {
        void refreshForticlientStatus();
        void refreshVpnDiskStatus();
      }
    });
    return () => {
      unsub();
      unlistenVpn?.();
    };
  });

  $inspect(config);

  async function handleAction(code: string) {
    if (loadingCode) return;
    loadingCode = code;
    
    const c = config;
    const auth = authStore.get();
    const username = auth.username || auth.user?.userId || "";

    console.log(c);
    if (!username) {
      toast.error("You must be logged in to perform this action.");
      return;
    }

    if (!c) {
      toast.error("Configuration not loaded.");
      return;
    }

    try {
      if (isTauri()) {
        const invoke = getInvoke();
        if (!invoke) throw new Error("Tauri internals not found.");

        // Fetch User props for RDP/NAV
        const userRes = await graphqlQuery<any>(GetUserQuery, {
          variables: { username },
        });

        if (!userRes.success || !userRes.data?.user) {
          throw new Error("Failed to fetch user connection details.");
        }

        const userDetails = userRes.data.user;

        console.log("userDetails", userDetails);

        if (c.mode === "User") {
          // RDP Logic
          if (!c.rdpUrl) throw new Error("RDP URL is not configured.");

          await invoke("launch_rdp", {
            rdpUrl: c.rdpUrl,
            username: username,
            password: userDetails.rdpPassword || c.rdpPassword || "",
          });
          toast.success("Opening ERP (RDP)...");
        } else if (c.mode === "Server") {
          // NAV Logic
          if (!c.navExePath) throw new Error("NAV Exe Path is not configured.");
          
          const navConfig = code === "2" ? "Old.config" : userDetails.navConfigName;
          
          if (!navConfig)
            throw new Error("NAV Config Name not found for user.");

          await invoke("launch_nav", {
            navExePath: c.navExePath,
            navConfigName: navConfig,
          });
          toast.success("Launching NAV Client...");
        }
      } else {
        // Web mode logic - based on code 3
        if (code === "3") {
          if (!c.webErpUrl) throw new Error("Web ERP URL is not configured.");
          window.open(c.webErpUrl, "_blank", "noopener,noreferrer");
        } else {
          toast.info("This application is only available in the desktop app.");
        }
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Action failed.");
    } finally {
      loadingCode = null;
    }
  }

  function getButtons() {
    if (!runErpResult.data?.groupDetails) return [];
    const details = runErpResult.data.groupDetails;
    const isDesktop = isTauri();

    if (isDesktop && config) {
      if (config.mode === "User") {
        return [{ name: "Open ERP (RDP)", code: "RDP" }];
      } else if (config.mode === "Server") {
        return details.filter((d: any) => d.code === "1" || d.code === "2");
      }
    } else if (!isDesktop && config) {
      // Web mode - code 3
      return details.filter((d: any) => d.code === "3");
    }
    return [];
  }
</script>

<div class="min-h-screen bg-muted/30 pb-16">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="layout-dashboard"
    class="border-b bg-background"
  >
    {#snippet title()}
      ERP Applications
    {/snippet}
    {#snippet description()}
      Connect to your Enterprise Resource Planning suite securely
    {/snippet}
  </PageHeading>

  <main class="container mx-auto px-4 py-8 md:px-6">
    <div class="flex items-center justify-between mb-6">
      <h2 class="text-xl font-semibold tracking-tight">Available Apps</h2>
      {#if runErpResult.loading}
        <div
          class="size-5 border-2 border-primary border-t-transparent rounded-full animate-spin"
          title="Loading available apps..."
        ></div>
      {/if}
    </div>

    {#if config === null}
      <div
        class="flex flex-col items-center justify-center py-16 gap-4 text-muted-foreground bg-card rounded-xl border shadow-sm"
      >
        <div
          class="size-10 border-4 border-muted border-t-primary rounded-full animate-spin"
        ></div>
        <p class="font-medium text-lg">Initializing configuration...</p>
      </div>
    {:else}
      <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
        {#each getButtons() as btn}
          <div
            class="group relative flex flex-col justify-between overflow-hidden rounded-xl border bg-card p-6 transition-all duration-300 hover:-translate-y-1 hover:border-primary/50 hover:shadow-lg"
          >
            <div>
              <div
                class="mb-5 flex size-12 items-center justify-center rounded-lg bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground"
              >
                {#if btn.code === "RDP"}
                  <Icon name="monitor" class="size-6" />
                {:else if btn.code === "1" || btn.code === "2"}
                  <Icon name="folder-open" class="size-6" />
                {:else if btn.code === "3"}
                  <Icon name="globe" class="size-6" />
                {:else}
                  <Icon name="app-window" class="size-6" />
                {/if}
              </div>
              <h3 class="mb-2 block text-xl font-semibold tracking-tight">
                {btn.name}
              </h3>
              <p class="mb-6 text-sm text-muted-foreground">
                {#if btn.code === "RDP"}
                  Connect to ERP natively via Remote Desktop seamlessly.
                {:else if btn.code === "1" || btn.code === "2"}
                  Launch the local Dynamics NAV instance directly on your
                  machine.
                {:else if btn.code === "3"}
                  Open the ERP portal via your default web browser.
                {/if}
              </p>
            </div>

            <Button
              variant="default"
              class="w-full gap-2 transition-all group-hover:shadow-md"
              onclick={() => handleAction(btn.code)}
              loading={loadingCode === btn.code}
            >
              Launch App <Icon name="arrow-right" class="size-4" />
            </Button>
          </div>
        {:else}
          {#if !runErpResult.loading}
            <div
              class="col-span-full py-16 text-center border-2 border-dashed rounded-xl bg-muted/20"
            >
              <div
                class="mx-auto mb-4 flex size-12 items-center justify-center rounded-full bg-muted"
              >
                <Icon
                  name="alert-circle"
                  class="size-6 text-muted-foreground"
                />
              </div>
              <h3 class="mb-1 text-lg font-semibold">
                No Applications Available
              </h3>
              <p class="text-muted-foreground">
                There are no ERP applications configured for your current
                environment mode ({config.mode}).
              </p>
            </div>
          {/if}
        {/each}
      </div>

      {#if config.mode === "User" && isTauri()}
        <section class="mt-10 rounded-xl border bg-card p-6 shadow-sm">
          <div class="mb-4 flex flex-wrap items-start justify-between gap-4">
            <div>
              <h2 class="text-lg font-semibold tracking-tight">VPN settings</h2>
              <p class="mt-1 text-sm text-muted-foreground">
                FortiClient VPN: remove the client silently when it is no longer needed (Windows
                desktop app only).
              </p>
            </div>
            {#if forticlientStatusLoading}
              <div
                class="size-5 shrink-0 border-2 border-primary border-t-transparent rounded-full animate-spin"
                title="Checking FortiClient…"
              ></div>
            {/if}
          </div>
          <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <p class="text-sm">
              <span class="text-muted-foreground">Status:</span>
              {#if forticlientInstalled === null && !forticlientStatusLoading}
                <span class="font-medium">Could not detect</span>
              {:else if forticlientInstalled}
                <span class="font-medium text-foreground">FortiClient is installed</span>
              {:else}
                <span class="font-medium text-muted-foreground">FortiClient is not installed</span>
              {/if}
            </p>
            <Button
              variant="outline"
              class="shrink-0 gap-2 sm:ml-auto"
              disabled={!forticlientInstalled || forticlientUninstallLoading}
              loading={forticlientUninstallLoading}
              onclick={() => handleUninstallForticlient()}
            >
              Uninstall FortiClient (silent)
            </Button>
          </div>

          <div class="mt-8 border-t pt-6">
            <h3 class="text-base font-semibold tracking-tight">VPN installer download</h3>
            <p class="mt-1 text-sm text-muted-foreground">
              Resumable download under your profile. Set the URL below (saved to app config); it overrides
              the API <code class="rounded bg-muted px-1 py-0.5 text-xs">VpnInstaller:DownloadUrl</code> when
              non-empty. Zip metadata (hash, entry) can still come from the API.
            </p>

            <div class="mt-4 max-w-xl space-y-2">
              <label class="text-sm font-medium" for="erp-vpn-download-url">Local VPN installer URL (optional)</label>
              <div class="flex flex-col gap-2 sm:flex-row sm:items-center">
                <Input
                  id="erp-vpn-download-url"
                  type="url"
                  class="font-mono text-sm"
                  placeholder="https://cdn.example.com/FortiClientVPN.zip"
                  bind:value={vpnUrlDraft}
                  disabled={vpnUrlSaving || vpnDownloadRunning}
                />
                <Button
                  type="button"
                  variant="secondary"
                  class="shrink-0"
                  disabled={vpnUrlSaving || vpnDownloadRunning || !config}
                  loading={vpnUrlSaving}
                  onclick={() => saveVpnDownloadUrl()}
                >
                  Save URL
                </Button>
              </div>
              <p class="text-xs text-muted-foreground">
                Leave empty to rely only on the server URL. Desktop app only for saving.
              </p>
            </div>

            {#if vpnInstallerGql.loading}
              <p class="mt-3 text-sm text-muted-foreground">Loading optional API metadata…</p>
            {/if}
            {#if vpnInstallerGql.error && !config.downloadVpnUrl?.trim()}
              <p class="mt-2 text-sm text-destructive">
                Could not load VPN metadata from API (zip/hash). Set the URL above or fix the network
                session.
              </p>
            {:else if vpnInstallerGql.error && config.downloadVpnUrl?.trim()}
              <p class="mt-2 text-sm text-amber-800 dark:text-amber-200">
                API metadata unavailable; using app-config URL only.
              </p>
            {/if}

            <div class="mt-4 space-y-3">
              <div class="text-sm space-y-2">
                <div>
                  <span class="text-muted-foreground">App config URL (priority):</span>
                  {#if config.downloadVpnUrl?.trim()}
                    <span class="ml-1 font-mono text-xs break-all">{config.downloadVpnUrl}</span>
                  {:else}
                    <span class="ml-1 text-muted-foreground">(not set)</span>
                  {/if}
                </div>
                <div>
                  <span class="text-muted-foreground">API fallback URL:</span>
                  {#if vpnInstallerMeta?.downloadUrl?.trim()}
                    <span class="ml-1 font-mono text-xs break-all">{vpnInstallerMeta.downloadUrl}</span>
                  {:else}
                    <span class="ml-1 text-muted-foreground">(not set)</span>
                  {/if}
                </div>
                <div>
                  <span class="text-muted-foreground">Effective download URL:</span>
                  {#if effectiveVpnDownloadUrl}
                    <span class="ml-1 font-mono text-xs break-all font-medium text-foreground"
                      >{effectiveVpnDownloadUrl}</span
                    >
                  {:else}
                    <span class="ml-1 font-medium text-amber-700 dark:text-amber-400">None configured</span>
                  {/if}
                </div>
              </div>
              {#if vpnInstallerMeta?.isZipArchive}
                <p class="text-xs text-muted-foreground">
                  Archive format: zip (compressed). Entry:
                  {vpnInstallerMeta.zipEntryName?.trim() || "first .exe or .msi in archive"}.
                </p>
              {/if}
                {#if vpnDiskLoading}
                  <p class="text-xs text-muted-foreground">Reading local cache…</p>
                {:else if vpnDiskStatus}
                  <div class="rounded-lg border bg-muted/30 p-3 text-xs">
                    <p class="font-medium text-foreground">Local storage</p>
                    <p class="mt-1 break-all font-mono text-muted-foreground">
                      {vpnDiskStatus.directory}
                    </p>
                    {#if vpnDiskStatus.localFilePath}
                      <p class="mt-2 break-all">
                        <span class="text-muted-foreground">Ready file:</span>
                        <span class="ml-1 font-mono">{vpnDiskStatus.localFilePath}</span>
                      </p>
                    {:else if vpnDiskStatus.lastError && vpnDiskStatus.phase === "error"}
                      <p class="mt-2 text-destructive">{vpnDiskStatus.lastError}</p>
                    {/if}
                  </div>
                {/if}

                {#if vpnDownloadRunning && vpnProgress}
                  <div class="space-y-2">
                    <div class="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                      <span>{vpnProgress.message}</span>
                      <span class="font-mono">
                        {formatBytes(vpnProgress.receivedBytes)}
                        {#if vpnProgress.totalBytes}
                          / {formatBytes(vpnProgress.totalBytes)}
                        {:else}
                          (size unknown)
                        {/if}
                      </span>
                    </div>
                    <Progress
                      value={vpnBarPercent}
                      class={!vpnProgress.totalBytes ? "opacity-60" : ""}
                    />
                  </div>
                {/if}

              <div class="flex flex-wrap gap-2 pt-1">
                <Button
                  variant="default"
                  disabled={vpnDownloadRunning || !effectiveVpnDownloadUrl}
                  loading={vpnDownloadRunning}
                  onclick={() => handleVpnDownload()}
                >
                  {vpnDiskStatus?.localFilePath ? "Download again" : "Download installer"}
                </Button>
                <Button
                  variant="outline"
                  disabled={!vpnDownloadRunning}
                  onclick={() => handleVpnCancel()}
                >
                  Cancel download
                </Button>
                <Button variant="secondary" onclick={() => handleOpenVpnFolder()}>
                  Open folder
                </Button>
              </div>
            </div>
          </div>
        </section>
      {/if}
    {/if}
  </main>
</div>
