//! FortiClient VPN — detect installation, launch the uninstaller interactively,
//! download and cache the `.conf` file, and patch credentials before the user
//! restores it in FortiClient.
//!
//! ## What the .conf file contains (and what we can/cannot do)
//!
//! The FortiClient `.conf` (XML) stores VPN tunnel settings.  The
//! `<user_configuration>` block holds per-user registry values including
//! `DATA1` and `DATA2` (both `enc="1"`) — these are the **encrypted username
//! and password** locked with FortiClient's machine-bound `EncX` cipher.
//!
//! WE CANNOT DECRYPT OR RE-ENCRYPT `EncX` blobs without owning the
//! FortiClient private key.  What we CAN do — and what is safe and useful —
//! is to:
//!   • Set the plaintext `<username>` inside the tunnel connection block (the
//!     value FortiClient pre-fills in the "Username" field of the login dialog).
//!   • CLEAR `DATA1` and `DATA2` so that FortiClient does NOT try to replay a
//!     foreign machine's encrypted token — replaying a mismatched blob causes a
//!     silent auth failure that is very hard to diagnose.  Clearing forces
//!     FortiClient to prompt for fresh credentials after import.
//!
//! The tunnel is named "Tyresoles" (matching the `<name>` tag in the file).

use serde::{Deserialize, Serialize};

// ─── types ────────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiClientInstallStatus {
    pub installed: bool,
}

/// Result returned by `forticlient_conf_disk_status`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiClientConfStatus {
    /// Absolute path to the cached `.conf` file (if it exists).
    pub local_path: Option<String>,
    /// `true` when the file exists on disk and is ready to use.
    pub exists: bool,
    /// Size in bytes of the cached file, or `None` when absent.
    pub size_bytes: Option<u64>,
}

/// Argument block for `forticlient_conf_download`.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ConfDownloadArgs {
    /// Full URL to the `.conf` file on the back-end.
    pub url: String,
    /// Optional Bearer token for authenticated back-end endpoints.
    #[serde(default)]
    pub bearer_token: Option<String>,
    /// When `true`, overwrite an existing cached file.
    #[serde(default)]
    pub force: bool,
}

/// Argument block for `forticlient_conf_patch`.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ConfPatchArgs {
    /// The VPN username to inject into `<username>` inside the tunnel connection.
    /// Pass an empty string to leave the field blank (FortiClient will prompt).
    pub username: String,
}

/// Argument block for `forticlient_fcconfig_import_admin`.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FcConfigImportArgs {
    /// Password passed to FCConfig `-p` (import encryption / profile password).
    pub password: String,
    /// Absolute path to the `.conf` file. Defaults to the app cache `forticlient.conf`.
    #[serde(default)]
    pub conf_path: Option<String>,
    /// Optional full path to `FCConfig.exe` when not in the default install locations.
    #[serde(default)]
    pub fcconfig_exe: Option<String>,
}

// ─── conf cache directory ─────────────────────────────────────────────────────

/// `%LOCALAPPDATA%\com.tyresoles.app\forticlient-conf\`
fn conf_cache_dir() -> Result<std::path::PathBuf, String> {
    let base = dirs::data_local_dir()
        .ok_or_else(|| "Could not resolve local data directory".to_string())?;
    Ok(base
        .join(crate::app_config::APP_IDENTIFIER)
        .join("forticlient-conf"))
}

fn conf_cache_path() -> Result<std::path::PathBuf, String> {
    Ok(conf_cache_dir()?.join("forticlient.conf"))
}

// ─── Tauri commands ───────────────────────────────────────────────────────────

/// Returns whether a product whose display name contains "FortiClient" is
/// registered in the Windows uninstall registry.
#[tauri::command]
pub fn forticlient_installation_status() -> Result<FortiClientInstallStatus, String> {
    #[cfg(windows)]
    {
        Ok(FortiClientInstallStatus {
            installed: find_forticlient_uninstall_entry().is_some(),
        })
    }
    #[cfg(not(windows))]
    {
        Ok(FortiClientInstallStatus { installed: false })
    }
}

/// **Launches the FortiClient uninstaller interactively** — the user will see
/// and complete the wizard themselves.  We no longer attempt silent removal.
///
/// On Windows we try the registry `UninstallString` / `QuietUninstallString`
/// (dropping any `/quiet`, `/qn`, `/silent` flags so the UI is shown), then
/// fall back to the well-known `Uninstall.exe` paths.
#[tauri::command]
pub fn uninstall_forticlient() -> Result<(), String> {
    #[cfg(windows)]
    {
        let entries = collect_forticlient_uninstall_entries();
        if entries.is_empty() {
            return Err("FortiClient is not installed (no entry found in registry).".to_string());
        }

        for entry in &entries {
            log::info!(
                "[FortiClient] Launching uninstaller interactively (display: {:?})",
                entry.display_name
            );
            match run_interactive_uninstall(entry) {
                Ok(()) => {
                    log::info!("[FortiClient] Uninstaller launched successfully.");
                    return Ok(());
                }
                Err(e) => {
                    log::warn!("[FortiClient] Launch attempt failed: {}", e);
                }
            }
        }

        // Last resort: try well-known Uninstall.exe paths
        for p in forticlient_default_uninstall_exe_paths() {
            if p.is_file() {
                log::info!("[FortiClient] Trying fallback path: {:?}", p);
                if let Ok(mut child) = std::process::Command::new(&p).spawn() {
                    // We don't wait — let the user complete the wizard
                    let _ = child.wait();
                    return Ok(());
                }
            }
        }

        Err("Could not find or launch the FortiClient uninstaller.".to_string())
    }
    #[cfg(not(windows))]
    {
        Err("FortiClient uninstall is only supported on Windows.".to_string())
    }
}

/// Returns disk status of the cached FortiClient `.conf` file.
#[tauri::command]
pub fn forticlient_conf_disk_status() -> Result<FortiClientConfStatus, String> {
    let path = conf_cache_path()?;
    if path.is_file() {
        let size = std::fs::metadata(&path).ok().map(|m| m.len());
        Ok(FortiClientConfStatus {
            local_path: Some(path.to_string_lossy().into_owned()),
            exists: true,
            size_bytes: size,
        })
    } else {
        Ok(FortiClientConfStatus {
            local_path: Some(path.to_string_lossy().into_owned()),
            exists: false,
            size_bytes: None,
        })
    }
}

/// Downloads the `.conf` file from the back-end and stores it in the stable
/// per-user cache folder.  If the file already exists and `force` is `false`,
/// returns successfully without re-downloading.
#[tauri::command]
pub async fn forticlient_conf_download(args: ConfDownloadArgs) -> Result<String, String> {
    if args.url.trim().is_empty() {
        return Err("Download URL is empty.".to_string());
    }

    let dir = conf_cache_dir()?;
    tokio::fs::create_dir_all(&dir)
        .await
        .map_err(|e| format!("Failed to create conf cache directory: {}", e))?;

    let dest = dir.join("forticlient.conf");

    // Check existence before downloading
    if dest.is_file() && !args.force {
        log::info!(
            "[FortiClient conf] File already exists at {:?}; skipping download (force=false).",
            dest
        );
        return Ok(dest.to_string_lossy().into_owned());
    }

    log::info!("[FortiClient conf] Downloading from {}", args.url);

    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(60))
        .build()
        .map_err(|e| format!("HTTP client: {}", e))?;

    let mut req = client
        .get(&args.url)
        .header("User-Agent", "TyresolesDesktop/1.0");

    if let Some(ref tok) = args.bearer_token {
        if !tok.trim().is_empty() {
            req = req.header("Authorization", format!("Bearer {}", tok));
        }
    }

    let resp = req.send().await.map_err(|e| format!("HTTP request failed: {}", e))?;

    if !resp.status().is_success() {
        return Err(format!(
            "Server returned HTTP {}: {}",
            resp.status(),
            resp.text().await.unwrap_or_default()
        ));
    }

    let bytes = resp
        .bytes()
        .await
        .map_err(|e| format!("Failed to read response body: {}", e))?;

    tokio::fs::write(&dest, &bytes)
        .await
        .map_err(|e| format!("Failed to write conf file: {}", e))?;

    log::info!("[FortiClient conf] Saved to {:?} ({} bytes)", dest, bytes.len());
    Ok(dest.to_string_lossy().into_owned())
}

/// Patches the cached `.conf` XML file in-place:
///
/// 1. **Sets `<username>`** inside the `<sslvpn><connections><connection>` block
///    to the supplied username (so FortiClient pre-fills the login field).
/// 2. **Clears `DATA1` and `DATA2`** in `<user_configuration>` — these hold
///    machine-bound `EncX`-encrypted credentials.  Clearing them prevents
///    FortiClient from replaying a stale/foreign encrypted token (which always
///    fails silently) and ensures it prompts the user for fresh credentials.
///
/// Returns the absolute path of the patched file.
#[tauri::command]
pub fn forticlient_conf_patch(args: ConfPatchArgs) -> Result<String, String> {
    let path = conf_cache_path()?;
    if !path.is_file() {
        return Err(
            "No cached FortiClient .conf file found. Download it first.".to_string(),
        );
    }

    let xml = std::fs::read_to_string(&path)
        .map_err(|e| format!("Failed to read .conf file: {}", e))?;

    let patched = patch_forticlient_conf_xml(&xml, &args.username)?;

    std::fs::write(&path, patched.as_bytes())
        .map_err(|e| format!("Failed to write patched .conf file: {}", e))?;

    log::info!(
        "[FortiClient conf] Patched successfully at {:?} (username={})",
        path,
        if args.username.is_empty() { "(empty)" } else { "<set>" }
    );

    Ok(path.to_string_lossy().into_owned())
}

/// Opens the conf cache folder in Windows Explorer.
#[cfg(windows)]
#[tauri::command]
pub fn forticlient_conf_open_folder() -> Result<(), String> {
    let dir = conf_cache_dir()?;
    std::fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    std::process::Command::new("explorer.exe")
        .arg(&dir)
        .spawn()
        .map_err(|e| format!("explorer: {}", e))?;
    Ok(())
}

#[cfg(not(windows))]
#[tauri::command]
pub fn forticlient_conf_open_folder() -> Result<(), String> {
    Err("Opening a folder is only supported on Windows.".to_string())
}

/// Launches **FCConfig.exe** with verb `runas` so Windows shows UAC and the import runs elevated.
///
/// Equivalent to:
/// `FCConfig.exe -o import -p "<password>" -f "<conf path>"`
///
/// Defaults: `FCConfig.exe` under Program Files (64- or 32-bit Fortinet paths), `.conf` from the app cache.
#[tauri::command]
pub fn forticlient_fcconfig_import_admin(args: FcConfigImportArgs) -> Result<(), String> {
    #[cfg(windows)]
    {
        if args.password.trim().is_empty() {
            return Err(
                "FCConfig import password (-p) cannot be empty — FortiClient ignores import without it."
                    .to_string(),
            );
        }

        let conf = match args.conf_path.as_deref().map(str::trim).filter(|s| !s.is_empty()) {
            Some(p) => std::path::PathBuf::from(p),
            None => conf_cache_path()?,
        };
        if !conf.is_file() {
            return Err(format!(
                "FortiClient config file not found: {}",
                conf.display()
            ));
        }

        let exe = resolve_fcconfig_exe_path(args.fcconfig_exe.as_deref())?;
        if !exe.is_file() {
            return Err(format!("FCConfig.exe not found at: {}", exe.display()));
        }

        let conf_s = conf.to_string_lossy();
        let params = build_fcconfig_import_params(&args.password, conf_s.as_ref());
        let work_dir = exe
            .parent()
            .map(|p| p.to_string_lossy().into_owned());

        shell_execute_ex_runas(
            exe.to_string_lossy().as_ref(),
            &params,
            work_dir.as_deref(),
        )?;

        log::info!(
            "[FortiClient] Elevated FCConfig import started for {}",
            conf.display()
        );
        Ok(())
    }
    #[cfg(not(windows))]
    {
        let _ = args;
        Err("FCConfig import is only supported on Windows.".to_string())
    }
}

#[cfg(windows)]
fn build_fcconfig_import_params(password: &str, conf_path: &str) -> String {
    format!(
        "-o import -p {} -f {}",
        quote_win_cmd_token(password),
        quote_win_cmd_token(conf_path),
    )
}

/// Double-quote a token for `lpParameters` (embedded `"` → `""`).
#[cfg(windows)]
fn quote_win_cmd_token(s: &str) -> String {
    let escaped = s.replace('"', "\"\"");
    format!("\"{escaped}\"")
}

#[cfg(windows)]
fn resolve_fcconfig_exe_path(override_path: Option<&str>) -> Result<std::path::PathBuf, String> {
    if let Some(p) = override_path.map(str::trim).filter(|s| !s.is_empty()) {
        return Ok(std::path::PathBuf::from(p));
    }
    let mut candidates: Vec<std::path::PathBuf> = Vec::new();
    if let Ok(pf) = std::env::var("ProgramFiles") {
        candidates.push(std::path::PathBuf::from(pf).join(r"Fortinet\FortiClient\FCConfig.exe"));
    }
    candidates.push(std::path::PathBuf::from(
        r"C:\Program Files\Fortinet\FortiClient\FCConfig.exe",
    ));
    if let Ok(pf86) = std::env::var("ProgramFiles(x86)") {
        candidates.push(
            std::path::PathBuf::from(pf86).join(r"Fortinet\FortiClient\FCConfig.exe"),
        );
    }
    candidates.push(std::path::PathBuf::from(
        r"C:\Program Files (x86)\Fortinet\FortiClient\FCConfig.exe",
    ));
    for p in candidates {
        if p.is_file() {
            return Ok(p);
        }
    }
    Err(
        "FCConfig.exe not found. Install FortiClient VPN or pass fcconfigExe.".to_string(),
    )
}

/// `ShellExecuteExW` with verb `runas` — triggers UAC; does not capture child stdout.
#[cfg(windows)]
fn shell_execute_ex_runas(
    exe: &str,
    parameters: &str,
    working_dir: Option<&str>,
) -> Result<(), String> {
    use std::ffi::OsStr;
    use std::os::windows::ffi::OsStrExt;

    fn to_wide_nul(s: &str) -> Vec<u16> {
        OsStr::new(s)
            .encode_wide()
            .chain(std::iter::once(0))
            .collect()
    }

    let verb = to_wide_nul("runas");
    let file = to_wide_nul(exe);
    let params = to_wide_nul(parameters);
    let dir_storage = working_dir.map(to_wide_nul);

    use windows::core::PCWSTR;
    use windows::Win32::Foundation::HWND;
    use windows::Win32::UI::Shell::{ShellExecuteExW, SHELLEXECUTEINFOW};
    use windows::Win32::UI::WindowsAndMessaging::SW_SHOWNORMAL;

    let mut sei = SHELLEXECUTEINFOW {
        cbSize: std::mem::size_of::<SHELLEXECUTEINFOW>() as u32,
        hwnd: HWND::default(),
        lpVerb: PCWSTR(verb.as_ptr()),
        lpFile: PCWSTR(file.as_ptr()),
        lpParameters: PCWSTR(params.as_ptr()),
        lpDirectory: PCWSTR(match &dir_storage {
            Some(d) => d.as_ptr(),
            None => std::ptr::null(),
        }),
        nShow: SW_SHOWNORMAL.0 as i32,
        ..Default::default()
    };

    unsafe {
        ShellExecuteExW(&mut sei).map_err(|e| {
            format!("Could not start elevated process (UAC cancelled or error): {e}")
        })?;
    }
    Ok(())
}

// ─── XML patching logic ───────────────────────────────────────────────────────

/// Pure function: applies credential patches to the FortiClient XML config
/// string and returns the modified XML.
///
/// Rules
/// -----
/// * `<username>` inside `<sslvpn><connections><connection>` → set to
///   `new_username` (empty string clears the field).
/// * `<value name="DATA1" ... enc="1">` → clear the `data` attribute (wipe
///   the encrypted username blob).
/// * `<value name="DATA2" ... enc="1">` → clear the `data` attribute (wipe
///   the encrypted password blob).
///
/// We intentionally use regex-free text manipulation to avoid pulling in an
/// extra dependency — the XML structure of FortiClient configs is well-defined
/// and stable.
fn patch_forticlient_conf_xml(xml: &str, new_username: &str) -> Result<String, String> {
    let mut out = String::with_capacity(xml.len() + 64);

    // We process line-by-line.  FortiClient exports are nicely indented with
    // one element per line in the relevant sections.
    for line in xml.lines() {
        let trimmed = line.trim();

        // ── 1. Patch <username> inside the SSLVPN connection block ──────────
        // The line looks like:  <username />  or  <username>foo</username>
        if trimmed.starts_with("<username") && !trimmed.starts_with("<username_format") {
            // Determine indentation prefix
            let indent: &str = &line[..line.len() - line.trim_start().len()];
            if new_username.is_empty() {
                out.push_str(&format!("{}<username />\r\n", indent));
            } else {
                // Escape XML special chars in the username
                let escaped = xml_escape(new_username);
                out.push_str(&format!("{}<username>{}</username>\r\n", indent, escaped));
            }
            continue;
        }

        // ── 2. Clear DATA1 / DATA2 encrypted blobs ──────────────────────────
        // These lines look like:
        //   <value key="Sslvpn\Tunnels\Tyresoles" name="DATA1" type="1"
        //          data="EncX 3095..." enc="1" />
        //   <value key="..." name="DATA2" ... data="EncX 38DC..." enc="1" />
        if (trimmed.contains("name=\"DATA1\"") || trimmed.contains("name=\"DATA2\""))
            && trimmed.contains("enc=\"1\"")
        {
            // Replace the data="..." attribute value with an empty string so
            // FortiClient treats it as absent/cleared.
            let cleared = clear_data_attribute(line);
            out.push_str(&cleared);
            out.push_str("\r\n");
            continue;
        }

        out.push_str(line);
        out.push_str("\r\n");
    }

    Ok(out)
}

/// Replace `data="<anything>"` in the line with `data=""`.
fn clear_data_attribute(line: &str) -> String {
    // Find data=" and then the matching closing quote
    if let Some(start) = line.find("data=\"") {
        let after = &line[start + 6..]; // skip  data="
        if let Some(end_offset) = after.find('"') {
            let before = &line[..start + 6]; // up to and including data="
            let rest = &line[start + 6 + end_offset..]; // from the closing " onward
            return format!("{}\"{}",  before, rest);
        }
    }
    line.to_string()
}

/// Minimal XML character escaping for attribute/element values.
fn xml_escape(s: &str) -> String {
    s.replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&apos;")
}

// ─── Windows registry helpers ─────────────────────────────────────────────────

#[cfg(windows)]
#[derive(Debug)]
struct UninstallEntry {
    display_name: String,
    quiet_uninstall: Option<String>,
    uninstall_string: Option<String>,
}

#[cfg(windows)]
fn uninstall_entry_rank(entry: &UninstallEntry) -> u8 {
    // Prefer entries that have an UninstallString pointing to Uninstall.exe
    // over MSI-only entries — for interactive launch the exe gives a nicer UI.
    let u = entry
        .uninstall_string
        .as_ref()
        .map(|s| s.to_lowercase())
        .unwrap_or_default();
    if u.contains("uninstall.exe") || u.contains("fcuninst") {
        return 0;
    }
    if entry.quiet_uninstall.as_ref().map(|s| !s.trim().is_empty()).unwrap_or(false) {
        return 1;
    }
    if u.contains("msiexec") {
        return 2;
    }
    3
}

#[cfg(windows)]
fn collect_forticlient_uninstall_entries() -> Vec<UninstallEntry> {
    use winreg::enums::HKEY_LOCAL_MACHINE;
    use winreg::RegKey;

    const SUBKEYS: [&str; 2] = [
        r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];

    let mut out = Vec::new();
    let hklm = RegKey::predef(HKEY_LOCAL_MACHINE);
    for sub in SUBKEYS {
        let key = match hklm.open_subkey(sub) {
            Ok(k) => k,
            Err(_) => continue,
        };
        for name in key.enum_keys().filter_map(|r| r.ok()) {
            let subkey = match key.open_subkey(&name) {
                Ok(k) => k,
                Err(_) => continue,
            };
            let display_name: String = match subkey.get_value("DisplayName") {
                Ok(v) => v,
                Err(_) => continue,
            };
            if !display_name.to_lowercase().contains("forticlient") {
                continue;
            }
            let quiet_uninstall: Option<String> = subkey.get_value("QuietUninstallString").ok();
            let uninstall_string: Option<String> = subkey.get_value("UninstallString").ok();

            out.push(UninstallEntry {
                display_name,
                quiet_uninstall,
                uninstall_string,
            });
        }
    }
    out.sort_by_key(|e| uninstall_entry_rank(e));
    out
}

#[cfg(windows)]
fn find_forticlient_uninstall_entry() -> Option<UninstallEntry> {
    let mut v = collect_forticlient_uninstall_entries();
    if v.is_empty() { None } else { Some(v.remove(0)) }
}

/// Launch the uninstaller without any silent flags — let the user complete it.
#[cfg(windows)]
fn run_interactive_uninstall(entry: &UninstallEntry) -> Result<(), String> {
    use std::process::Command;

    // Prefer UninstallString over QuietUninstallString for interactive use
    // (QuietUninstall might skip the UI entirely on some builds).
    let raw = entry
        .uninstall_string
        .as_ref()
        .or(entry.quiet_uninstall.as_ref())
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .ok_or_else(|| "No uninstall information in registry.".to_string())?;

    let lower = raw.to_lowercase();

    if lower.contains("msiexec") {
        // For MSI-based installs, open msiexec in interactive mode (no /qn)
        let guid = extract_msi_product_code(&raw)
            .ok_or_else(|| "Could not parse MSI product code.".to_string())?;
        // Open the standard MSI uninstall UI
        Command::new("msiexec.exe")
            .args(["/x", &guid])
            .spawn()
            .map_err(|e| format!("Failed to launch msiexec: {}", e))?
            .wait()
            .map_err(|e| format!("msiexec wait error: {}", e))?;
        return Ok(());
    }

    // EXE-based uninstaller — parse and launch without appending /quiet
    let parts = shell_words::split(&raw).map_err(|e| e.to_string())?;
    if parts.is_empty() {
        return Err("UninstallString could not be parsed.".to_string());
    }

    // Strip any silent flags that were embedded in the string
    let exe = &parts[0];
    let filtered_args: Vec<&str> = parts[1..]
        .iter()
        .map(|s| s.as_str())
        .filter(|s| {
            let l = s.to_lowercase();
            !l.eq("/quiet")
                && !l.eq("/qn")
                && !l.eq("/silent")
                && !l.eq("/verysilent")
                && !l.eq("/s")
                && !l.eq("-q")
                && !l.eq("-s")
        })
        .collect();

    Command::new(exe)
        .args(&filtered_args)
        .spawn()
        .map_err(|e| format!("Failed to launch uninstaller: {}", e))?
        .wait()
        .map_err(|e| format!("Uninstaller wait error: {}", e))?;

    Ok(())
}

/// Returns the well-known `Uninstall.exe` paths for FortiClient.
#[cfg(windows)]
fn forticlient_default_uninstall_exe_paths() -> Vec<std::path::PathBuf> {
    use std::path::PathBuf;
    let mut v = Vec::new();
    if let Ok(pf) = std::env::var("ProgramFiles") {
        v.push(PathBuf::from(pf).join(r"Fortinet\FortiClient\Uninstall.exe"));
    }
    v.push(PathBuf::from(
        r"C:\Program Files\Fortinet\FortiClient\Uninstall.exe",
    ));
    if let Ok(pf86) = std::env::var("ProgramFiles(x86)") {
        v.push(PathBuf::from(pf86).join(r"Fortinet\FortiClient\Uninstall.exe"));
    }
    v.push(PathBuf::from(
        r"C:\Program Files (x86)\Fortinet\FortiClient\Uninstall.exe",
    ));
    v
}

#[cfg(windows)]
fn extract_msi_product_code(uninstall: &str) -> Option<String> {
    let start = uninstall.find('{')?;
    let end = uninstall.find('}')?;
    if end <= start {
        return None;
    }
    Some(uninstall[start..=end].to_string())
}

// ─── unit tests ───────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    const SAMPLE_XML: &str = r#"<?xml version="1.0" encoding="UTF-8" ?>
<forticlient_configuration generatedby="FCT-7.4.6.2001">
    <vpn>
        <sslvpn>
            <connections>
                <connection>
                    <name>Tyresoles</name>
                    <server>20.198.86.183:4433</server>
                    <username />
                    <vpn_before_logon>
                        <username_format>username</username_format>
                    </vpn_before_logon>
                </connection>
            </connections>
        </sslvpn>
    </vpn>
    <user_configuration>
        <value key="Sslvpn\Tunnels\Tyresoles" name="DATA1" type="1" data="EncX ABC123" enc="1" />
        <value key="Sslvpn\Tunnels\Tyresoles" name="DATA2" type="1" data="EncX DEF456" enc="1" />
    </user_configuration>
</forticlient_configuration>
"#;

    #[test]
    fn test_patch_sets_username_and_clears_blobs() {
        let patched = patch_forticlient_conf_xml(SAMPLE_XML, "john.doe").unwrap();
        assert!(patched.contains("<username>john.doe</username>"), "username not set");
        assert!(!patched.contains("EncX ABC123"), "DATA1 blob not cleared");
        assert!(!patched.contains("EncX DEF456"), "DATA2 blob not cleared");
        // DATA1/DATA2 lines should still be present but with empty data
        assert!(patched.contains("name=\"DATA1\""), "DATA1 line removed");
        assert!(patched.contains("name=\"DATA2\""), "DATA2 line removed");
        // username_format should be untouched
        assert!(patched.contains("<username_format>username</username_format>"));
    }

    #[test]
    fn test_patch_clears_username_when_empty() {
        let patched = patch_forticlient_conf_xml(SAMPLE_XML, "").unwrap();
        assert!(patched.contains("<username />"), "username not cleared");
    }

    #[test]
    fn test_clear_data_attribute() {
        let line = r#"        <value key="Sslvpn\Tunnels\Tyresoles" name="DATA1" type="1" data="EncX ABC123" enc="1" />"#;
        let cleared = clear_data_attribute(line);
        assert!(cleared.contains("data=\"\""), "data attribute not cleared: {}", cleared);
        assert!(!cleared.contains("EncX"), "EncX blob still present: {}", cleared);
    }

    #[test]
    fn test_xml_escape() {
        assert_eq!(xml_escape("a&b<c>d\"e'f"), "a&amp;b&lt;c&gt;d&quot;e&apos;f");
    }
}
