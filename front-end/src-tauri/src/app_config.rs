//! Runtime app config read/write for Tauri.
//!
//! **Read precedence (production-safe):**
//! 1. `app-config.json` located **next to the installed executable** (same folder as the
//!    `.exe` on Windows / binary on other platforms). This allows shipping a config file
//!    alongside the installer and having it applied immediately on first launch.
//! 2. Legacy per-user config under the OS config directory (see below).
//!
//! **Write location (unchanged, writable):**
//! - Windows: `%LOCALAPPDATA%\com.tyresoles.app\app-config.json`
//!   e.g. `C:\Users\<user>\AppData\Local\com.tyresoles.app\app-config.json`
//! - Other:   `~/.config/com.tyresoles.app/app-config.json`
//!
//! The per-user path is always writable without Admin rights and is used for
//! `write_app_config` and for bootstrapping a default config when none exists.
//! The file is also created eagerly in the Tauri `setup()` hook so it exists
//! before the frontend ever calls `read_app_config`.

use serde::{Deserialize, Serialize};
use std::fs;
use std::path::PathBuf;

const CONFIG_FILENAME: &str = "app-config.json";
pub(crate) const APP_IDENTIFIER: &str = "com.tyresoles.app";

/// Returns the **Installation-specific config file path**.
/// This is located next to the installed `.exe` (or binary).
/// This satisfies the "Per Installation" requirement (not per user).
fn app_config_folder() -> Result<PathBuf, String> {
    let exe = std::env::current_exe().map_err(|e| {
        log::error!("[config] Failed to get current exe path: {}", e);
        format!("Failed to determine executable directory: {}", e)
    })?;

    let dir = exe
        .parent()
        .ok_or_else(|| "Failed to get exe parent directory".to_string())?;
    Ok(dir.to_path_buf())
}

fn app_config_path() -> Result<PathBuf, String> {
    Ok(app_config_folder()?.join(CONFIG_FILENAME))
}

/// Returns the writable config path (strictly per-installation).
fn app_config_write_path() -> Result<PathBuf, String> {
    app_config_path()
}

/// Returns the read path (strictly per-installation).
fn app_config_read_path() -> Result<PathBuf, String> {
    app_config_path()
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppConfig {
    #[serde(default = "default_backend_base_url")]
    pub backend_base_url: String,
    #[serde(default = "default_frontend_url")]
    pub frontend_url: String,
    #[serde(default = "default_update_url")]
    pub update_url: String,
    #[serde(default = "default_version")]
    pub version: String,
    #[serde(default = "default_mode")]
    pub mode: String,
    #[serde(default = "default_theme")]
    pub theme: String,
    #[serde(default = "default_max_retries")]
    pub max_retries: u32,
    #[serde(default = "default_rdp_url")]
    pub rdp_url: String,
    #[serde(default = "default_rdp_password")]
    pub rdp_password: String,
    #[serde(default = "default_web_erp_url")]
    pub web_erp_url: String,
    #[serde(default = "default_nav_exe_path")]
    pub nav_exe_path: String,
    #[serde(default = "default_old_nav_config")]
    pub old_nav_config: String,
    #[serde(default = "default_download_url")]
    pub download_url: String,
    #[serde(default = "default_download_vpn_url")]
    pub download_vpn_url: String,
    /// SSL VPN tunnel name for `FortiVPN.exe --cli` (e.g. Tyresoles).
    #[serde(default = "default_fortivpn_tunnel")]
    pub fortivpn_tunnel: String,
    /// Optional SSL-VPN logon override when it differs from ERP userId / email (e.g. `ABHIRAJ.D`).
    #[serde(default)]
    pub fortivpn_username: String,
    /// SSL-VPN password for `FortiVPN.exe --cli --connect` (not used for RDP).
    #[serde(default)]
    pub fortivpn_password: String,
}

fn default_backend_base_url() -> String {
    "http://api.tyresoles.net".to_string()
}

fn default_frontend_url() -> String {
    "http://localhost:5173".to_string()
}

fn default_update_url() -> String {
    "http://app.tyresoles.net/updates/update.json".to_string()
}

fn default_version() -> String {
    "1.0".to_string()
}

fn default_mode() -> String {
    "User".to_string()
}

fn default_theme() -> String {
    "light".to_string()
}

fn default_max_retries() -> u32 {
    3
}

fn default_rdp_url() -> String {
    "10.10.10.8:8224".to_string()
}

fn default_rdp_password() -> String {
    "Tyre@$tr0ng2026".to_string()
}

fn default_web_erp_url() -> String {
    "http://20.207.200.140:8080/DynamicsNAV71/WebClient/".to_string()
}

fn default_nav_exe_path() -> String {
    "C:\\Program Files (x86)\\Microsoft Dynamics\\NAV\\100\\RoleTailored Client\\Microsoft.Dynamics.Nav.Client.exe".to_string()
}

fn default_old_nav_config() -> String {
    "OldG01".to_string()
}

fn default_download_url() -> String {
    "http://app.tyresoles.net/downloads/Tyresoles_Latest_x64_en-US.msi".to_string()
}

fn default_download_vpn_url() -> String {
    String::new()
}

fn default_fortivpn_tunnel() -> String {
    "Tyresoles".to_string()
}

impl Default for AppConfig {
    fn default() -> Self {
        Self {
            backend_base_url: "http://api.tyresoles.net".to_string(),
            frontend_url: default_frontend_url(),
            update_url: default_update_url(),
            version: default_version(),
            mode: default_mode(),
            theme: default_theme(),
            max_retries: default_max_retries(),
            rdp_url: default_rdp_url(),
            rdp_password: default_rdp_password(),
            web_erp_url: default_web_erp_url(),
            nav_exe_path: default_nav_exe_path(),
            old_nav_config: default_old_nav_config(),
            download_url: default_download_url(),
            download_vpn_url: default_download_vpn_url(),
            fortivpn_tunnel: default_fortivpn_tunnel(),
            fortivpn_username: String::new(),
            fortivpn_password: String::new(),
        }
    }
}

fn write_app_config_inner(config: &AppConfig) -> Result<(), String> {
    let path = app_config_write_path()?;
    log::info!("[config] Writing config to: {:?}", path);
    if let Some(parent) = path.parent() {
        log::debug!("[config] Ensuring directory exists: {:?}", parent);
        fs::create_dir_all(parent).map_err(|e| {
            log::error!("[config] Failed to create directory {:?}: {}", parent, e);
            format!("Failed to create config directory: {}", e)
        })?;
    }
    let s = serde_json::to_string_pretty(config).map_err(|e| e.to_string())?;
    fs::write(&path, &s).map_err(|e| {
        log::error!("[config] Failed to write file {:?}: {}", path, e);
        format!("Failed to write config file: {}", e)
    })?;
    log::info!("[config] Config written successfully to {:?}", path);
    Ok(())
}

/// Called eagerly from lib.rs setup() hook to ensure the config file exists
/// before the frontend starts. Returns the loaded/created config.
pub fn init_config() -> AppConfig {
    // Try reading from the preferred read path first (exe directory can override).
    match app_config_read_path() {
        Err(e) => {
            log::error!("[config] Cannot determine config path: {}", e);
            AppConfig::default()
        }
        Ok(path) => {
            log::info!("[config] Config path: {:?}", path);
            if path.exists() {
                log::info!("[config] File exists, loading...");
                match fs::read_to_string(&path) {
                    Err(e) => {
                        log::error!("[config] Failed to read file: {}", e);
                        AppConfig::default()
                    }
                    Ok(s) => match serde_json::from_str::<AppConfig>(&s) {
                        Err(e) => {
                            log::error!("[config] Failed to parse JSON: {}. Using defaults.", e);
                            AppConfig::default()
                        }
                        Ok(config) => {
                            log::info!(
                                "[config] Config loaded: backendBaseUrl={}",
                                config.backend_base_url
                            );
                            config
                        }
                    },
                }
            } else {
                log::info!(
                    "[config] File not found at {:?}. Creating per-user defaults...",
                    path
                );
                let config = AppConfig::default();
                match write_app_config_inner(&config) {
                    Ok(()) => log::info!(
                        "[config] Default per-user config created successfully at {:?}",
                        app_config_write_path().ok()
                    ),
                    Err(e) => log::error!("[config] Failed to create default config: {}", e),
                }
                config
            }
        }
    }
}

#[tauri::command]
pub fn read_app_config() -> Result<AppConfig, String> {
    let path = app_config_read_path()?;
    log::info!(
        "[config/invoke] read_app_config called. Resolved path: {:?}",
        path
    );
    if path.exists() {
        let s = fs::read_to_string(&path).map_err(|e| {
            log::error!("[config/invoke] Read failed: {}", e);
            e.to_string()
        })?;
        let parsed: AppConfig = serde_json::from_str(&s).map_err(|e| {
            let err_msg = format!("Parse failed for {:?}: {}. Using default.", path, e);
            log::error!("{}", err_msg);
            err_msg
        })?;
        if parsed.backend_base_url.is_empty() {
            log::warn!("[config/invoke] backend_base_url empty, returning defaults");
            return Ok(AppConfig::default());
        }
        log::info!(
            "[config/invoke] Returning config: backendBaseUrl={}",
            parsed.backend_base_url
        );
        Ok(parsed)
    } else {
        log::warn!("[config/invoke] File not found, returning defaults (init_config should have created it)");
        Ok(AppConfig::default())
    }
}

#[tauri::command]
pub fn write_app_config(config: AppConfig) -> Result<(), String> {
    if config.backend_base_url.is_empty() {
        return Err("backend_base_url must not be empty".to_string());
    }
    write_app_config_inner(&config)
}

#[tauri::command]
pub fn get_windows_user() -> String {
    match (std::env::var("USERDOMAIN"), std::env::var("USERNAME")) {
        (Ok(domain), Ok(user)) => format!("{}\\{}", domain, user),
        (Ok(domain), Err(_)) => domain,
        (Err(_), Ok(user)) => user,
        _ => "Unknown".to_string(),
    }
}
