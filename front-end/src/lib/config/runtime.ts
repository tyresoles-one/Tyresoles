/**
 * Runtime app config — single config file for both Svelte (web) and Tauri (desktop).
 * Web: fetched from /app-config.json (same origin). Tauri: read via Rust command from
 * the installed folder (exe directory or per-user config). Validated with Zod; falls
 * back to DEFAULT_APP_CONFIG when missing or invalid.
 *
 * In Tauri production we must use the invoke path so config is read from the filesystem.
 * If isTauri() were false (e.g. only checking window.__TAURI__ when withGlobalTauri is
 * false), the app would fetch /app-config.json from the asset origin and get the
 * bundled static file instead of the external file next to the exe.
 */

import { z } from "zod";
import { writable } from "svelte/store";
import { isTauri } from "$lib/tauri";

export const AppConfigSchema = z.object({
  backendBaseUrl: z.string().url().default("http://api.tyresoles.net"),
  frontendUrl: z.string().url().default("http://localhost:5173"),
  updateUrl: z
    .string()
    .url()
    .default("http://app.tyresoles.net/updates/update.json"),
  version: z.string().default("1.0"),
  mode: z.enum(["User", "Server"]).default("User"),
  theme: z.enum(["light", "dark"]).default("light"),
  maxRetries: z.number().int().min(0).max(10).default(3),
  rdpUrl: z.string().default("10.10.10.8:8224"),
  navExePath: z
    .string()
    .default(
      "C:\\Program Files (x86)\\Microsoft Dynamics NAV\\71\\RoleTailored Client\\Microsoft.Dynamics.Nav.Client.exe",
    ),
  rdpPassword: z.string().default("Tyre@$tr0ng2026"),
  oldNavConfig: z.string().default("OldG01"),
  webErpUrl: z
    .string()
    .url()
    .default("http://20.207.200.140:8080/DynamicsNAV71/WebClient/"),
  downloadUrl: z
    .string()
    .url()
    .default(
      "http://app.tyresoles.net/downloads/Tyresoles_Latest_x64_en-US.msi",
    ),
  /** VPN installer URL; overrides API `vpnInstallerConfig.downloadUrl` when non-empty. */
  downloadVpnUrl: z
    .union([z.string().url(), z.literal("")])
    .default(""),
});

export type AppConfig = z.infer<typeof AppConfigSchema>;

export const DEFAULT_APP_CONFIG: AppConfig = {
  backendBaseUrl: "http://api.tyresoles.net",
  frontendUrl: "http://localhost:5173",
  updateUrl: "http://app.tyresoles.net/updates/update.json",
  version: "1.0",
  mode: "User",
  theme: "light",
  maxRetries: 3,
  rdpUrl: "10.10.10.8:8224",
  navExePath:
    "C:\\Program Files (x86)\\Microsoft Dynamics NAV\\71\\RoleTailored Client\\Microsoft.Dynamics.Nav.Client.exe",
  rdpPassword: "Tyre@$tr0ng2026",
  webErpUrl: "http://20.207.200.140:8080/DynamicsNAV71/WebClient/",
  oldNavConfig: "OldG01",
  downloadUrl:
    "http://app.tyresoles.net/downloads/Tyresoles_Latest_x64_en-US.msi",
  downloadVpnUrl: "",
};

const CONFIG_FILENAME = "app-config.json";
let configPromise: Promise<AppConfig> | null = null;

type TauriInvoke = (cmd: string, args?: unknown) => Promise<unknown>;
function getTauriInvoke(): TauriInvoke | undefined {
  const w = window as Window & {
    __TAURI_INTERNALS__?: { invoke?: TauriInvoke };
    __TAURI__?: { core?: { invoke?: TauriInvoke } };
  };
  return w.__TAURI_INTERNALS__?.invoke ?? w.__TAURI__?.core?.invoke;
}

function parseAndValidate(raw: unknown): AppConfig {
  const normalized =
    raw === null || raw === undefined ? {} : raw;
  if (typeof normalized !== "object" || Array.isArray(normalized)) {
    console.warn(
      "[config] Invalid app-config (expected a JSON object), using default.",
    );
    return DEFAULT_APP_CONFIG;
  }
  const result = AppConfigSchema.safeParse(normalized);
  if (result.success) return result.data;
  const detail = result.error.issues
    .map((i) => `${i.path.join(".") || "(root)"}: ${i.message}`)
    .join("; ");
  console.warn("[config] Invalid app-config, using default:", detail);
  return DEFAULT_APP_CONFIG;
}

async function loadConfigWeb(): Promise<AppConfig> {
  try {
    const base = typeof window !== "undefined" ? window.location.origin : "";
    const res = await fetch(`${base}/${CONFIG_FILENAME}`, {
      cache: "no-store",
    });
    if (!res.ok) return DEFAULT_APP_CONFIG;
    const raw = await res.json();
    return parseAndValidate(raw);
  } catch {
    return DEFAULT_APP_CONFIG;
  }
}

async function loadConfigTauri(): Promise<AppConfig> {
  try {
    const invoke = getTauriInvoke();
    if (typeof invoke !== "function") return DEFAULT_APP_CONFIG;
    const raw = await invoke("read_app_config");
    return parseAndValidate(raw);
  } catch (e) {
    console.warn("[config] Tauri read_app_config failed, using default:", e);
    return DEFAULT_APP_CONFIG;
  }
}

/**
 * Load app config once (cached). In browser: fetch /app-config.json or invoke Tauri command.
 * In SSR/build: returns default.
 */
export async function getAppConfig(): Promise<AppConfig> {
  if (typeof window === "undefined") return DEFAULT_APP_CONFIG;
  if (configPromise) return configPromise;
  configPromise = isTauri() ? loadConfigTauri() : loadConfigWeb();
  return configPromise;
}

/** Store populated when config is ready; use after initAppConfig() has been awaited in layout. */
export const appConfigStore = writable<AppConfig | null>(null);

/**
 * Initialize config and fill appConfigStore. Call once from root layout; await before rendering API-dependent UI.
 */
export async function initAppConfig(): Promise<AppConfig> {
  const config = await getAppConfig();
  appConfigStore.set(config);
  return config;
}

/**
 * Write config (Tauri only). No-op in web.
 */
export async function writeAppConfig(config: AppConfig): Promise<void> {
  if (typeof window === "undefined") return;
  if (!isTauri()) return;
  try {
    const invoke = getTauriInvoke();
    if (typeof invoke !== "function") return;
    await invoke("write_app_config", { config });
    appConfigStore.set(config);
  } catch (e) {
    console.error("[config] write_app_config failed:", e);
    throw e;
  }
}
