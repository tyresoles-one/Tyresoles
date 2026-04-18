//! Launch Windows RDP (mstsc) with host:port and credentials.
//! Uses cmdkey to store credentials for TERMSRV/<host> (and TERMSRV/<host:port> when using a custom port),
//! then launches mstsc via a `.rdp` file so the client uses the saved credential.
//!
//! ## Drive / clipboard consent dialogs (Windows 10/11, especially post–2026 updates)
//! Microsoft intentionally shows a security dialog when opening `.rdp` files that request local
//! resources (drives, clipboard, etc.). Unsigned files show an “unknown publisher” flow; the
//! consent UI is by design to mitigate RDP phishing.
//!
//! **What this module can do in-app**
//! - Write a minimal `.rdp` (only the redirections you need: drives + clipboard + printers).
//! - Store files under `%LOCALAPPDATA%\\com.tyresoles.app\\rdp\\` instead of `%TEMP%` (clearer origin).
//! - Optionally **digitally sign** the `.rdp` with `rdpsign.exe` when
//!   `TYRESOLES_RDP_SIGN_CERT_SHA256` is set to your code-signing cert SHA256 thumbprint (cert must
//!   be in the user or machine store). Signed files show a verified publisher in the dialog.
//!
//! **Enterprise (no per-user consent for your fleet)**  
//! IT can deploy the Terminal Services **client** policies Microsoft documents for these warnings
//! (e.g. under `Software\\Policies\\Microsoft\\Windows NT\\Terminal Services\\Client`). That is the
//! supported way to suppress the new redirection warning for managed PCs—not something the app should
//! silently write to `HKLM` for standard users.

#[cfg(windows)]
use std::path::Path;
#[cfg(windows)]
use std::process::Command;
#[cfg(windows)]
use std::{fs, fs::File, io::Write, path::PathBuf};

/// Launch RDP connection to host:port with the given username and password.
/// On Windows: adds credential via cmdkey (direct call to avoid cmd.exe mangling the password),
/// then launches mstsc using a temporary .rdp file so saved credentials are used.
/// On non-Windows: returns an error.
#[tauri::command]
pub fn launch_rdp(rdp_url: String, username: String, password: String) -> Result<(), String> {
    #[cfg(windows)]
    {
        let (host, _port) = parse_rdp_url(&rdp_url)?;

        log::info!("[RDP] Preparing credentials for {} as {} (password length: {})", host, username, password.len());

        if password.is_empty() {
            log::warn!("[RDP] Password is empty! cmdkey will likely fail or prompt.");
        }

        // Call cmdkey directly (no cmd /c) so the password is not parsed by the shell (avoids special-char mangling).
        let cmdkey_target = format!("TERMSRV/{}", host);
        let add_out = Command::new("cmdkey")
            .args([
                &format!("/add:{}", cmdkey_target),
                &format!("/user:{}", username),
                &format!("/pass:{}", password),
            ])
            .output()
            .map_err(|e| format!("cmdkey failed: {}", e))?;

        if !add_out.status.success() {
            let stderr = String::from_utf8_lossy(&add_out.stderr);
            log::error!("[RDP] cmdkey failed for {}: {}", host, stderr);
            return Err("cmdkey failed to add credential".to_string());
        }

        // On some Win10 builds, mstsc looks up credentials by host:port when using a custom port.
        if rdp_url.contains(':') {
            let cmdkey_target_port = format!("TERMSRV/{}", rdp_url);
            let add_port_out = Command::new("cmdkey")
                .args([
                    &format!("/add:{}", cmdkey_target_port),
                    &format!("/user:{}", username),
                    &format!("/pass:{}", password),
                ])
                .output()
                .map_err(|e| format!("cmdkey (host:port) failed: {}", e))?;
            if !add_port_out.status.success() {
                log::warn!("[RDP] cmdkey for TERMSRV/{} failed (non-fatal)", rdp_url);
            } else {
                log::info!("[RDP] Credential also stored for TERMSRV/{}", rdp_url);
            }
        }

        // Launch via .rdp file so mstsc uses the saved credential (avoids prompt on some builds).
        let rdp_path = write_rdp_file(&rdp_url, &username)?;
        try_sign_rdp_file(&rdp_path);

        let mstsc = mstsc_exe_path();
        log::info!(
            "[RDP] Launching {} for {} via {}",
            mstsc.display(),
            rdp_url,
            rdp_path.display()
        );

        Command::new(&mstsc)
            .arg(&rdp_path)
            .spawn()
            .map_err(|e| format!("mstsc failed: {}", e))?;

        Ok(())
    }

    #[cfg(not(windows))]
    {
        let _ = (rdp_url, username, password);
        Err("RDP launch is only supported on Windows".to_string())
    }
}

/// Launch Dynamics NAV client via local exe path and settings file name.
/// On Windows: runs <nav_exe_path> -settings:"<nav_config_name>".
#[tauri::command]
pub fn launch_nav(app: tauri::AppHandle, nav_exe_path: String, nav_config_name: String) -> Result<(), String> {
    #[cfg(windows)]
    {
        use tauri::Manager;

        if nav_exe_path.is_empty() {
            return Err("NAV executable path is empty".to_string());
        }

        // Determine the expected filename
        let config_name = if nav_config_name.contains('.') {
            nav_config_name.clone()
        } else {
            format!("{}.config", nav_config_name)
        };

        // Resolve resource directory
        let res_dir = app.path().resource_dir().map_err(|e| e.to_string())?;

        // 1. Try to find the file in several likely locations (bundled as configs/ or at root)
        let mut final_path = None;
        let p_configs = res_dir.join("configs").join(&config_name);
        let p_root = res_dir.join(&config_name);

        if p_configs.exists() {
            final_path = Some(p_configs);
        } else if p_root.exists() {
            final_path = Some(p_root);
        }

        let config_abs_path = match final_path {
            Some(p) => {
                let mut s = p.to_string_lossy().into_owned();
                // Strip Windows UNC prefix for legacy app compatibility
                if s.starts_with(r"\\?\") {
                    s = s.trim_start_matches(r"\\?\").to_string();
                }
                s
            }
            None => {
                let err = format!("NAV config file '{}' not found. Searched root and 'configs/' folder in resources.", config_name);
                log::error!("[NAV] {}", err);
                return Err(err);
            }
        };

        log::info!("[NAV] Launching {} with config {}", nav_exe_path, config_abs_path);

        // Let Rust handle the quoting automatically for spaces
        Command::new(&nav_exe_path)
            .arg(format!("-settings:{}", config_abs_path))
            .spawn()
            .map_err(|e| {
                log::error!("[NAV] Failed to launch {}: {}", nav_exe_path, e);
                format!("Failed to launch NAV: {}", e)
            })?;

        Ok(())
    }

    #[cfg(not(windows))]
    {
        let _ = (nav_exe_path, nav_config_name);
        Err("NAV launch is only supported on Windows".to_string())
    }
}

/// Write a `.rdp` file under the app’s LocalAppData folder (not `%TEMP%`) so the file has a stable,
/// application-scoped origin. Password is never written; cmdkey supplies the secret.
///
/// Redirections are limited to drives + clipboard + printers. Other categories (camera, audio capture,
/// smart cards) stay off so Windows does not add extra consent rows on newer builds.
#[cfg(windows)]
fn write_rdp_file(rdp_url: &str, username: &str) -> Result<PathBuf, String> {
    let dir = app_local_rdp_dir();
    fs::create_dir_all(&dir).map_err(|e| format!("Failed to create RDP directory: {}", e))?;
    let name = format!(
        "tyresoles_rdp_{}.rdp",
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default()
            .as_millis()
    );
    let path = dir.join(&name);

    let addr = sanitize_rdp_line(rdp_url);
    let user = sanitize_rdp_line(username);

    // RDP file: one `name:type:value` per line (`\r\n`). See Microsoft “Supported RDP properties”.
    // - drivestoredirect + redirectdrives: cover modern + legacy clients.
    // - prompt for credentials:i:0 + negotiate security layer + CredSSP: use cmdkey-saved creds cleanly.
    // - audiocapturemode:i:0: do not request microphone redirection (avoids an extra consent item).
    let content = format!(
        "full address:s:{addr}\r\n\
         username:s:{user}\r\n\
         drivestoredirect:s:*\r\n\
         redirectdrives:i:1\r\n\
         redirectprinters:i:1\r\n\
         redirectclipboard:i:1\r\n\
         redirectsmartcards:i:0\r\n\
         audiocapturemode:i:0\r\n\
         prompt for credentials:i:0\r\n\
         negotiate security layer:i:1\r\n\
         enablecredsspsupport:i:1\r\n\
         authentication level:i:2\r\n"
    );

    let mut f = File::create(&path).map_err(|e| format!("Failed to create .rdp file: {}", e))?;
    f.write_all(content.as_bytes())
        .map_err(|e| format!("Failed to write .rdp file: {}", e))?;
    Ok(path)
}

#[cfg(windows)]
fn app_local_rdp_dir() -> PathBuf {
    let local = std::env::var("LOCALAPPDATA").unwrap_or_else(|_| std::env::temp_dir().to_string_lossy().into_owned());
    PathBuf::from(local)
        .join(crate::app_config::APP_IDENTIFIER)
        .join("rdp")
}

#[cfg(windows)]
fn sanitize_rdp_line(s: &str) -> String {
    s.replace('\r', "").replace('\n', "")
}

#[cfg(windows)]
fn mstsc_exe_path() -> PathBuf {
    let win = std::env::var("SystemRoot").unwrap_or_else(|_| "C:\\Windows".to_string());
    Path::new(&win).join("System32").join("mstsc.exe")
}

/// If `TYRESOLES_RDP_SIGN_CERT_SHA256` is set to a certificate thumbprint (hex, optionally with
/// spaces), signs the `.rdp` with `%SystemRoot%\\System32\\rdpsign.exe` so mstsc can show a
/// verified publisher. Failure is logged and the unsigned file is still used.
#[cfg(windows)]
fn try_sign_rdp_file(path: &Path) {
    let thumb = match std::env::var("TYRESOLES_RDP_SIGN_CERT_SHA256") {
        Ok(t) => {
            let t = t.chars().filter(|c| !c.is_whitespace()).collect::<String>();
            if t.is_empty() {
                return;
            }
            t
        }
        Err(_) => return,
    };

    let rdpsign = mstsc_exe_path()
        .parent()
        .map(|p| p.join("rdpsign.exe"))
        .unwrap_or_else(|| PathBuf::from(r"C:\Windows\System32\rdpsign.exe"));

    if !rdpsign.is_file() {
        log::warn!("[RDP] rdpsign.exe not found at {}", rdpsign.display());
        return;
    }

    let path_str = match path.to_str() {
        Some(s) => s.to_string(),
        None => {
            log::warn!("[RDP] RDP path is not valid UTF-16 path for rdpsign; skipping sign");
            return;
        }
    };

    log::info!("[RDP] Signing {} with rdpsign (SHA256 thumbprint configured)", path.display());

    let out = Command::new(&rdpsign)
        .args(["/sha256", &thumb, &path_str])
        .output();

    match out {
        Ok(o) if o.status.success() => log::info!("[RDP] rdpsign completed successfully"),
        Ok(o) => {
            let err = String::from_utf8_lossy(&o.stderr);
            let out = String::from_utf8_lossy(&o.stdout);
            log::warn!(
                "[RDP] rdpsign failed (exit {:?}): stderr={} stdout={}",
                o.status.code(),
                err,
                out
            );
        }
        Err(e) => log::warn!("[RDP] rdpsign could not run: {}", e),
    }
}

/// Parse "host" or "host:port" into (host, port_str for display).
fn parse_rdp_url(rdp_url: &str) -> Result<(String, String), String> {
    let s = rdp_url.trim();
    if s.is_empty() {
        return Err("rdpUrl is empty".to_string());
    }
    if let Some((host, port)) = s.split_once(':') {
        Ok((host.to_string(), port.to_string()))
    } else {
        Ok((s.to_string(), "3389".to_string()))
    }
}
