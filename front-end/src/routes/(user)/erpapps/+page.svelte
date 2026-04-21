<script lang="ts">
  import { onMount } from "svelte";
  import { get } from "svelte/store";
  import {
    appConfigStore,
    writeAppConfig,
    type AppConfig,
  } from "$lib/config/runtime";
  import { authStore, getAuthToken } from "$lib/stores/auth";
  import { Badge } from "$lib/components/ui/badge";
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
  import {
    Collapsible,
    CollapsibleContent,
    CollapsibleTrigger,
  } from "$lib/components/ui/collapsible";

  let config = $state<AppConfig | null>(null);
  let loadingCode = $state<string | null>(null);
  let forticlientInstalled = $state<boolean | null>(null);
  let forticlientStatusLoading = $state(false);
  let forticlientUninstallLoading = $state(false);
  let vpnInstallerLaunchLoading = $state(false);
  let vpnSetupRunning = $state(false);

  // ── VPN conf file state ──────────────────────────────────────────────────
  type ConfDiskStatus = {
    localPath: string | null;
    exists: boolean;
    sizeBytes: number | null;
  };

  let confStatus = $state<ConfDiskStatus | null>(null);
  let confStatusLoading = $state(false);
  let confDownloadLoading = $state(false);
  let confPatchLoading = $state(false);
  let fcConfigImportLoading = $state(false);
  /** Password for `FCConfig.exe -o import -p` (profile / export password). */
  let fcConfigImportPassword = $state("Pass1234");
  /** Username to inject into the .conf before the user restores it. */
  let confUsername = $state("");

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
  let vpnDownloadRunning = $state(false);
  let vpnProgress = $state<VpnInstallerProgressEvt | null>(null);
  let vpnUrlDraft = $state("");
  let vpnUrlSaving = $state(false);
  /** Avoid overwriting the input while typing; hydrate once when config first loads. */
  let vpnUrlDraftHydrated = $state(false);
  /** VPN settings panel: collapsed by default. */
  let vpnSectionOpen = $state(false);

  // ── FortiVPN CLI (`FortiVPN.exe --cli`) ───────────────────────────────────
  type FortiVpnStatusOutput = {
    exitCode: number;
    stdout: string;
    stderr: string;
    connected: boolean | null;
  };

  type FortiVpnCliOutput = {
    exitCode: number;
    stdout: string;
    stderr: string;
    /** Set by `fortivpn_cli_connect`: argv-style line (includes password). */
    commandLine?: string | null;
  };

  let fortivpnStatus = $state<FortiVpnStatusOutput | null>(null);
  let fortivpnStatusLoading = $state(false);
  let fortivpnConnectLoading = $state(false);
  let fortivpnDisconnectLoading = $state(false);
  /**
   * Legacy binding: always false (List CLI UI removed). Keeps any stale markup / HMR
   * from throwing ReferenceError if `loading={fortivpnListLoading}` is still present.
   */
  let fortivpnListLoading = $state(false);
  /** Last FortiVPN status poll (desktop). */
  let lastVpnStatusAt = $state<Date | null>(null);
  /** Directory VPN row (GraphQL user query). */
  let vpnProfile = $state<{
    user: {
      userId: string;
      vpnUserId?: string | null;
      vpnPassword?: string | null;
    };
  } | null>(null);
  let vpnProfileLoading = $state(false);
  let vpnProfileError = $state<string | null>(null);

  const fortivpnTunnelName = $derived.by(
    () => config?.fortivpnTunnel?.trim() || "Tyresoles",
  );

  const directoryVpnUser = $derived.by(
    () => vpnProfile?.user?.vpnUserId?.trim() ?? "",
  );
  const directoryVpnPassword = $derived.by(
    () => vpnProfile?.user?.vpnPassword?.trim() ?? "",
  );

  /** One-line label for the compact VPN toolbar. */
  const vpnConnectionLabel = $derived.by(() => {
    if (fortivpnStatusLoading) return "Checking…";
    if (!fortivpnStatus) return "No status";
    if (fortivpnStatus.connected === true) return "Connected";
    if (fortivpnStatus.connected === false) return "Disconnected";
    return "Unclear";
  });

  /** Short credential / directory hint for the toolbar (no secrets). */
  const vpnCredentialHint = $derived.by(() => {
    if (vpnProfileLoading) return "Loading…";
    if (vpnProfileError) return "Dir error";
    if (directoryVpnUser && directoryVpnPassword) return directoryVpnUser;
    if (directoryVpnUser) return `${directoryVpnUser} · no pwd`;
    return "No directory VPN";
  });

  async function refreshFortivpnStatus(): Promise<FortiVpnStatusOutput | null> {
    if (!isTauri()) return null;
    fortivpnStatusLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) {
        fortivpnStatus = null;
        return null;
      }
      const r = (await invoke("fortivpn_cli_status", {
        args: {},
      })) as FortiVpnStatusOutput;
      fortivpnStatus = r;
      lastVpnStatusAt = new Date();
      return r;
    } catch {
      fortivpnStatus = null;
      return null;
    } finally {
      fortivpnStatusLoading = false;
    }
  }

  async function handleFortivpnConnect() {
    if (!isTauri()) {
      toast.info("VPN connect is only available in the desktop app.");
      return;
    }
    const c = config;
    if (!c) {
      toast.error("Configuration not loaded.");
      return;
    }
    if (vpnProfileLoading) {
      toast.error("Still loading VPN credentials from the server.");
      return;
    }
    /** FortiVPN `--username` — from NAV User Vpn UserID (GraphQL `vpnUserId`); email-style logins normalized for CLI. */
    const vpnLoginRaw = directoryVpnUser;
    const vpnLogin = fortivpnUsernameForCli(vpnLoginRaw);
    if (!vpnLoginRaw || !vpnLogin) {
      toast.error(
        "No SSL-VPN username: set Vpn UserID on your NAV User record (directory).",
      );
      return;
    }
    const pwd = directoryVpnPassword;
    if (!pwd) {
      toast.error(
        "No SSL-VPN password: set Vpn Password on your NAV User record (directory).",
      );
      return;
    }

    fortivpnConnectLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const r = (await invoke("fortivpn_cli_connect", {
        args: {
          tunnel: fortivpnTunnelName,
          username: vpnLogin,
          password: pwd,
        },
      })) as FortiVpnCliOutput;
      const tail = [r.stdout, r.stderr].filter(Boolean).join("\n").trim();
      if (r.exitCode !== 0) {
        toast.error(tail || `FortiVPN connect exited with code ${r.exitCode}.`);
      } else {
        toast.success("VPN connect command completed.", tail.slice(0, 120) || undefined);
      }
      await refreshFortivpnStatus();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      fortivpnConnectLoading = false;
    }
  }

  async function handleFortivpnDisconnect() {
    if (!isTauri()) return;
    const c = config;
    if (!c) return;
    fortivpnDisconnectLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const r = (await invoke("fortivpn_cli_disconnect", {
        args: { tunnel: fortivpnTunnelName },
      })) as FortiVpnCliOutput;
      const tail = [r.stdout, r.stderr].filter(Boolean).join("\n").trim();
      if (r.exitCode !== 0) {
        toast.error(tail || `FortiVPN disconnect exited with code ${r.exitCode}.`);
      } else {
        toast.success("VPN disconnect command completed.");
      }
      await refreshFortivpnStatus();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      fortivpnDisconnectLoading = false;
    }
  }

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

  function formatClock(d: Date): string {
    return d.toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  }

  /**
   * FortiSSL VPN logon often matches AD `sAMAccountName` (e.g. ABHIRAJ.D). Web sessions may store
   * email as userId — use the local-part in uppercase when `@` is present.
   */
  function fortivpnUsernameForCli(raw: string): string {
    const t = raw.trim();
    if (!t.includes("@")) return t;
    return (t.split("@")[0] ?? t).toUpperCase();
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
        vpnUserId
        vpnPassword
      }
    }
  `;

  const VpnInstallerConfigQuery = buildQuery`
    query VpnInstallerConfig {
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

  const vpnInstallerServerResult = useGraphQLQuery<any>(VpnInstallerConfigQuery, {
    cacheKey: "vpn_installer_config_graphql",
  });

  /** Prefer server `VpnInstaller` config; fall back to local `downloadVpnUrl` in app-config. */
  const effectiveVpnDownloadUrl = $derived.by(() => {
    const u = vpnInstallerServerResult.data?.vpnInstallerConfig?.downloadUrl?.trim();
    if (u) return u;
    return config?.downloadVpnUrl?.trim() ?? "";
  });

  const effectiveVpnInstallerMeta = $derived.by(() => {
    const c = vpnInstallerServerResult.data?.vpnInstallerConfig;
    if (c?.downloadUrl?.trim()) return c;
    return null;
  });

  /** When the configured URL is a zip, the desktop layer extracts it. */
  const vpnUrlIsZipArchive = $derived.by(() => {
    const c = effectiveVpnInstallerMeta;
    if (c) return c.isZipArchive === true;
    return /\.zip(\?|#|$)/i.test(effectiveVpnDownloadUrl);
  });

  $effect(() => {
    const username =
      $authStore.username?.trim() || $authStore.user?.userId?.trim() || "";
    if (!username) {
      vpnProfile = null;
      vpnProfileLoading = false;
      vpnProfileError = null;
      return;
    }
    let cancelled = false;
    vpnProfileLoading = true;
    vpnProfileError = null;
    void graphqlQuery<{ user: { userId: string; vpnUserId?: string | null; vpnPassword?: string | null } }>(
      GetUserQuery,
      {
        variables: { username },
        skipCache: true,
        cacheKey: `erpapps_user_vpn_${username}`,
      },
    ).then((res) => {
      if (cancelled) return;
      vpnProfileLoading = false;
      if (res.success && res.data?.user) {
        vpnProfile = res.data;
      } else {
        vpnProfile = null;
        vpnProfileError = res.error ?? "Could not load directory VPN credentials.";
      }
    });
    return () => {
      cancelled = true;
    };
  });

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
        toast.error("Enter a valid URL, or leave empty to clear the saved value.");
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
    try {
      const invoke = getInvoke();
      if (!invoke) {
        vpnDiskStatus = null;
        return;
      }
      vpnDiskStatus = (await invoke("vpn_installer_disk_status")) as VpnDiskStatus;
    } catch {
      vpnDiskStatus = null;
    }
  }

  async function refreshConfStatus() {
    if (!isTauri()) return;
    confStatusLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) return;
      confStatus = (await invoke("forticlient_conf_disk_status")) as ConfDiskStatus;
    } catch {
      confStatus = null;
    } finally {
      confStatusLoading = false;
    }
  }

  /**
   * Download the FortiClient .conf from the back-end.
   * If ‌force=false the Rust layer skips re-download when the file exists.
   */
  async function handleConfDownload(force = false) {
    const url = effectiveVpnDownloadUrl;
    const confUrl = url.replace(/\.exe$|\.msi$|\.zip$/i, ".conf").trim();

    if (!confUrl) {
      toast.error(
        "No VPN installer URL in app config. Set downloadVpnUrl (the .conf URL is derived by swapping the file extension).",
      );
      return;
    }

    confDownloadLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const path = (await invoke("forticlient_conf_download", {
        args: {
          url: confUrl,
          bearerToken: getAuthToken() || null,
          force,
        },
      })) as string;
      const short = path.length > 80 ? path.slice(0, 80) + "…" : path;
      toast.success(force ? "Config re-downloaded." : "Config file ready.", short);
      await refreshConfStatus();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Config download failed.");
    } finally {
      confDownloadLoading = false;
    }
  }

  /**
   * Patch the cached .conf with the username entered by the user.
   * This sets <username> in the SSLVPN block and CLEARS the encrypted
   * DATA1/DATA2 credential blobs so FortiClient prompts for fresh credentials.
   */
  async function handleConfPatch() {
    confPatchLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const patchUser = confUsername.trim() || directoryVpnUser;
      await invoke("forticlient_conf_patch", {
        args: { username: patchUser },
      });
      toast.success(
        "Config patched.",
        patchUser
          ? `Username set to "${patchUser}". Encrypted credential blobs cleared.`
          : "Username left blank. Encrypted credential blobs cleared.",
      );
      await refreshConfStatus();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Patch failed.");
    } finally {
      confPatchLoading = false;
    }
  }

  /**
   * Runs Fortinet `FCConfig.exe -o import` elevated (UAC) against the cached `.conf`.
   */
  async function handleFcConfigImportAdmin() {
    if (!isTauri()) {
      toast.info("FCConfig import is only available in the desktop app.");
      return;
    }
    if (!confStatus?.exists || !confStatus.localPath) {
      toast.error("Download or cache the config file first.");
      return;
    }
    if (!fcConfigImportPassword.trim()) {
      toast.error(
        "Enter the FCConfig import password (-p). It is required for import; an empty password does not apply the profile.",
      );
      return;
    }
    fcConfigImportLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      await invoke("forticlient_fcconfig_import_admin", {
        args: {
          password: fcConfigImportPassword,
          confPath: confStatus.localPath,
        },
      });
      toast.success(
        "Administrator prompt opened.",
        "Approve UAC to run FCConfig import. Complete any FCConfig dialogs that appear.",
      );
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "FCConfig import could not be started.");
    } finally {
      fcConfigImportLoading = false;
    }
  }

  async function handleOpenConfFolder() {
    try {
      const { invoke } = await import("@tauri-apps/api/core");
      await invoke("forticlient_conf_open_folder");
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Could not open folder.");
    }
  }

  async function handleVpnDownload() {
    const url = effectiveVpnDownloadUrl;
    if (!url) {
      toast.error(
        "VPN installer URL is not set. Enter a URL above and save to app config.",
      );
      return;
    }
    if (vpnDownloadRunning) return;
    vpnDownloadRunning = true;
    vpnProgress = null;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      const meta = vpnInstallerServerResult.data?.vpnInstallerConfig;
      const path = (await invoke("vpn_installer_download", {
        args: {
          url,
          sha256Hex: meta?.sha256Hex ?? null,
          fileName: meta?.fileName ?? null,
          isZipArchive: vpnUrlIsZipArchive,
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

  async function handleLaunchVpnInstaller() {
    if (!isTauri()) {
      toast.info("Available in the desktop app.");
      return;
    }
    const path = vpnDiskStatus?.localFilePath;
    if (!path) {
      toast.error("Download the installer first.");
      return;
    }
    vpnInstallerLaunchLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      await invoke("vpn_installer_launch_file", { path });
      toast.success(
        "Installer started.",
        "Complete the FortiClient wizard, then return here to finish profile setup.",
      );
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      vpnInstallerLaunchLoading = false;
    }
  }

  /**
   * If FortiClient is missing: download installer, launch it, then stop (user completes wizard).
   * If installed: download .conf, patch with directory VPN user, prompt for FCConfig separately.
   */
  async function runVpnAutomatedSetup() {
    if (!isTauri()) {
      toast.info("Available in the desktop app.");
      return;
    }
    if (vpnSetupRunning || vpnDownloadRunning) return;
    const url = effectiveVpnDownloadUrl;
    if (!url) {
      toast.error(
        "No VPN download URL. Configure VpnInstaller on the server or downloadVpnUrl in app-config.",
      );
      return;
    }
    vpnSetupRunning = true;
    try {
      if (forticlientInstalled !== true) {
        toast.info("Downloading FortiClient installer…");
        await handleVpnDownload();
        await refreshVpnDiskStatus();
        const p = vpnDiskStatus?.localFilePath;
        if (p) {
          const invoke = getInvoke();
          if (invoke) {
            await invoke("vpn_installer_launch_file", { path: p });
            toast.success(
              "Installer launched.",
              "After FortiClient is installed, run automated setup again to download the VPN profile.",
            );
          }
        }
        return;
      }
      toast.info("Downloading VPN profile (.conf)…");
      await handleConfDownload(false);
      toast.info("Patching profile for your account…");
      await handleConfPatch();
      toast.success(
        "Config is ready.",
        "Use FCConfig import if your org requires it, then Connect VPN.",
      );
    } catch (e) {
      toast.error(e instanceof Error ? e.message : String(e));
    } finally {
      vpnSetupRunning = false;
    }
  }

  async function handleUninstallForticlient() {
    if (forticlientUninstallLoading || !forticlientInstalled) return;
    forticlientUninstallLoading = true;
    try {
      const invoke = getInvoke();
      if (!invoke) throw new Error("Tauri internals not found.");
      // Launches the uninstaller UI — user completes the wizard manually.
      await invoke("uninstall_forticlient");
      toast.success("Uninstaller launched. Please complete the wizard.");
      await refreshForticlientStatus();
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      toast.error(msg || "Could not launch uninstaller.");
    } finally {
      forticlientUninstallLoading = false;
    }
  }

  onMount(() => {
    let unlistenVpn: (() => void) | undefined;
    let vpnPollInterval: ReturnType<typeof setInterval> | undefined;

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

    if (isTauri()) {
      vpnPollInterval = setInterval(() => {
        if (document.visibilityState !== "visible") return;
        const c = get(appConfigStore);
        if (c?.mode !== "User") return;
        void refreshFortivpnStatus();
      }, 8000);
    }

    const unsub = appConfigStore.subscribe((c) => {
      config = c;
      if (c?.mode === "User" && isTauri()) {
        void refreshForticlientStatus();
        void refreshVpnDiskStatus();
        void refreshConfStatus();
        void refreshFortivpnStatus();
      }
    });
    return () => {
      unsub();
      unlistenVpn?.();
      if (vpnPollInterval) clearInterval(vpnPollInterval);
    };
  });

  async function handleAction(code: string) {
    if (loadingCode) return;

    const c = config;
    const auth = authStore.get();
    const username = auth.username || auth.user?.userId || "";

    if (!username) {
      toast.error("You must be logged in to perform this action.");
      return;
    }

    if (!c) {
      toast.error("Configuration not loaded.");
      return;
    }

    loadingCode = code;

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

        if (c.mode === "User") {
          // RDP Logic
          if (!c.rdpUrl) throw new Error("RDP URL is not configured.");

          if (code === "RDP") {
            const vpnSt = await refreshFortivpnStatus();
            if (vpnSt?.connected !== true) {
              vpnSectionOpen = true;
              throw new Error(
                "FortiClient VPN is not connected. Use “Connect VPN” in the banner, wait until status shows connected, then try again.",
              );
            }
          }

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
      {#if config.mode === "User" && isTauri()}
        <div
          class="mb-4 rounded-lg border bg-card px-2.5 py-2 shadow-sm"
          role="region"
          aria-label="FortiClient VPN status"
          title="Connect before RDP. SSL-VPN uses NAV Vpn UserID and Vpn Password from directory only."
        >
          <div
            class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between sm:gap-3"
          >
            <div class="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1">
              <span class="relative flex h-2 w-2 shrink-0 self-center" aria-hidden="true">
                {#if fortivpnStatusLoading}
                  <span
                    class="inline-flex h-2 w-2 animate-pulse rounded-full bg-muted-foreground/50"
                  ></span>
                {:else if fortivpnStatus?.connected === true}
                  <span
                    class="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400/60"
                  ></span>
                  <span class="relative inline-flex h-2 w-2 rounded-full bg-emerald-500"></span>
                {:else if fortivpnStatus?.connected === false}
                  <span class="inline-flex h-2 w-2 rounded-full bg-amber-500"></span>
                {:else}
                  <span class="inline-flex h-2 w-2 rounded-full bg-muted-foreground/40"></span>
                {/if}
              </span>
              <span class="text-xs font-semibold tracking-tight">VPN</span>
              <span class="font-mono text-[11px] text-muted-foreground">{fortivpnTunnelName}</span>
              <span class="hidden text-muted-foreground sm:inline" aria-hidden="true">·</span>
              <span
                class="text-xs font-medium tabular-nums"
                class:text-emerald-700={fortivpnStatus?.connected === true}
                class:dark:text-emerald-400={fortivpnStatus?.connected === true}
                class:text-amber-800={fortivpnStatus?.connected === false}
                class:dark:text-amber-200={fortivpnStatus?.connected === false}
                class:text-muted-foreground={fortivpnStatusLoading ||
                  fortivpnStatus == null ||
                  fortivpnStatus.connected === null}
              >
                {vpnConnectionLabel}
              </span>
              {#if lastVpnStatusAt && !fortivpnStatusLoading}
                <span class="text-[10px] text-muted-foreground tabular-nums"
                  >{formatClock(lastVpnStatusAt)}</span
                >
              {/if}
              <span class="hidden text-muted-foreground sm:inline" aria-hidden="true">·</span>
              <span
                class="max-w-[min(100%,14rem)] truncate font-mono text-[11px] text-muted-foreground sm:max-w-[18rem]"
                title={vpnProfileError ?? vpnCredentialHint}
              >
                {#if vpnProfileError}
                  <span class="text-amber-700 dark:text-amber-300">{vpnProfileError}</span>
                {:else}
                  {vpnCredentialHint}
                {/if}
              </span>
            </div>
            <div class="flex shrink-0 flex-wrap items-center gap-1 sm:justify-end">
              <Button
                variant="secondary"
                size="icon-sm"
                class="shrink-0"
                title="Refresh VPN status"
                loading={fortivpnStatusLoading}
                onclick={() => refreshFortivpnStatus()}
              >
                <Icon name="refresh-cw" class="size-3.5" />
              </Button>
              <Button
                variant="default"
                size="sm"
                class="h-8 gap-1 px-2.5"
                title="Connect SSL-VPN"
                loading={fortivpnConnectLoading}
                disabled={
                  fortivpnStatus?.connected === true ||
                  vpnProfileLoading ||
                  !directoryVpnUser ||
                  !directoryVpnPassword
                }
                onclick={() => handleFortivpnConnect()}
              >
                <Icon name="link" class="size-3.5" />
                Connect
              </Button>
              <Button
                variant="outline"
                size="sm"
                class="h-8 px-2.5"
                title="Disconnect"
                loading={fortivpnDisconnectLoading}
                onclick={() => handleFortivpnDisconnect()}
              >
                Off
              </Button>
            </div>
          </div>
        </div>
      {/if}

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
        <Collapsible bind:open={vpnSectionOpen} class="mt-10">
          <section class="overflow-hidden rounded-xl border bg-card shadow-sm">
            <CollapsibleTrigger
              class="flex w-full items-start justify-between gap-4 p-6 text-left transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
            >
              <div class="min-w-0 flex-1">
                <h2 class="flex items-center gap-2 text-lg font-semibold tracking-tight">
                  VPN settings
                  <Icon
                    name="chevron-down"
                    class="size-4 shrink-0 text-muted-foreground transition-transform duration-200 {vpnSectionOpen
                      ? 'rotate-180'
                      : ''}"
                  />
                </h2>
                <p class="mt-1 text-sm text-muted-foreground">
                  FortiClient VPN installer, config (.conf), and FCConfig import (Windows desktop
                  only).
                </p>
              </div>
              {#if forticlientStatusLoading}
                <div
                  class="size-5 shrink-0 border-2 border-primary border-t-transparent rounded-full animate-spin"
                  title="Checking FortiClient…"
                ></div>
              {/if}
            </CollapsibleTrigger>
            <CollapsibleContent>
              <div class="border-t px-6 pb-6 pt-4">
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
                    <Icon name="trash-2" class="size-4" /> Uninstall FortiClient
                  </Button>
                </div>

                <div class="mt-8 border-t pt-6">
                  <h3 class="text-base font-semibold tracking-tight">VPN installer download</h3>
                  {#if vpnInstallerServerResult.data?.vpnInstallerConfig?.downloadUrl?.trim()}
                    <p class="mt-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                      <Badge variant="outline" class="text-[10px] font-normal">Server</Badge>
                      Installer URL is provided by the API (<span class="font-mono">VpnInstaller</span>).
                    </p>
                  {:else}
                    <p class="mt-2 text-xs text-muted-foreground">
                      No server URL — using <span class="font-mono">downloadVpnUrl</span> from app-config when set.
                    </p>
                  {/if}

                  <div class="mt-4 max-w-xl space-y-2">
                    <label class="text-sm font-medium" for="erp-vpn-download-url">VPN installer URL (optional override)</label>
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
                  </div>
                  <div class="mt-4 space-y-3">
                    {#if vpnDownloadRunning && vpnProgress}
                      <div class="space-y-2">
                        <div
                          class="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground"
                        >
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
                      <Button
                        variant="outline"
                        disabled={!vpnDiskStatus?.localFilePath || vpnInstallerLaunchLoading}
                        loading={vpnInstallerLaunchLoading}
                        onclick={() => handleLaunchVpnInstaller()}
                      >
                        Run installer
                      </Button>
                      <Button
                        variant="default"
                        class="gap-1.5"
                        disabled={vpnSetupRunning || vpnDownloadRunning || !effectiveVpnDownloadUrl}
                        loading={vpnSetupRunning}
                        onclick={() => runVpnAutomatedSetup()}
                      >
                        <Icon name="zap" class="size-3.5" />
                        Automated setup
                      </Button>
                    </div>
                  </div>
                </div>

                <div class="mt-8 border-t pt-6">
                  <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <h3 class="text-base font-semibold tracking-tight">VPN Config file (.conf)</h3>
                    </div>
                    {#if confStatusLoading}
                      <div
                        class="size-4 border-2 border-primary border-t-transparent rounded-full animate-spin"
                        title="Checking…"
                      ></div>
                    {/if}
                  </div>

                  <div class="max-w-xl space-y-2 mb-4">
                    <label class="text-sm font-medium" for="conf-vpn-username">VPN username (for .conf patch)</label>
                    <Input
                      id="conf-vpn-username"
                      type="text"
                      class="font-mono text-sm"
                      placeholder="Leave blank to use directory Vpn UserID from NAV"
                      bind:value={confUsername}
                      disabled={confPatchLoading || confDownloadLoading}
                    />
                    <p class="text-xs text-muted-foreground">
                      <span class="font-medium">Connect VPN</span> uses directory credentials only. This field sets
                      <code class="rounded bg-muted px-1 py-0.5">&lt;username&gt;</code> in the .conf patch when you run Patch.
                      Encrypted DATA1/DATA2 blobs are always cleared on patch.
                    </p>
                  </div>

                  <div class="flex flex-wrap gap-2">
                    <Button
                      variant="default"
                      disabled={confDownloadLoading || confPatchLoading}
                      loading={confDownloadLoading}
                      onclick={() => handleConfDownload(false)}
                    >
                      {confStatus?.exists ? "Use cached" : "Download config"}
                    </Button>
                    <Button
                      variant="secondary"
                      disabled={confDownloadLoading || confPatchLoading}
                      loading={confDownloadLoading}
                      onclick={() => handleConfDownload(true)}
                    >
                      Re-download
                    </Button>
                    <Button
                      variant="outline"
                      disabled={!confStatus?.exists || confPatchLoading || confDownloadLoading}
                      loading={confPatchLoading}
                      onclick={() => handleConfPatch()}
                    >
                      Patch &amp; prepare config
                    </Button>
                    <Button variant="ghost" onclick={() => handleOpenConfFolder()}>
                      Open folder
                    </Button>
                  </div>

                  <div class="mt-6 max-w-xl space-y-2 rounded-lg border bg-muted/20 p-4">
                    <h4 class="text-sm font-semibold tracking-tight">
                      Restore with FCConfig (Administrator)
                    </h4>
                    <label class="text-sm font-medium" for="fcconfig-import-password">Import password</label>
                    <Input
                      id="fcconfig-import-password"
                      type="password"
                      class="font-mono text-sm"
                      placeholder="Required — same password used when the .conf was exported"
                      autocomplete="off"
                      bind:value={fcConfigImportPassword}
                      disabled={
                        fcConfigImportLoading ||
                        confDownloadLoading ||
                        confPatchLoading ||
                        !confStatus?.exists
                      }
                    />
                    <Button
                      variant="default"
                      class="mt-1 gap-2"
                      disabled={
                        !confStatus?.exists ||
                        !fcConfigImportPassword.trim() ||
                        fcConfigImportLoading ||
                        confDownloadLoading ||
                        confPatchLoading
                      }
                      loading={fcConfigImportLoading}
                      onclick={() => handleFcConfigImportAdmin()}
                    >
                      <Icon name="shield-check" class="size-4" />
                      Import via FCConfig (elevated)
                    </Button>
                  </div>
                </div>
              </div>
            </CollapsibleContent>
          </section>
        </Collapsible>
      {/if}
    {/if}
  </main>
</div>
