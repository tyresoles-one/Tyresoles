//! Launch Windows RDP (mstsc) with host:port and credentials.
//! Uses cmdkey to store credentials for TERMSRV/<host> (and TERMSRV/<host:port> when using a custom port),
//! then launches mstsc via a temporary .rdp file so the client uses the saved credential.

#[cfg(windows)]
use std::process::Command;
#[cfg(windows)]
use std::{fs::File, io::Write, path::PathBuf};

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
        log::info!("[RDP] Launching mstsc for {} via {}", rdp_url, rdp_path.display());

        Command::new("mstsc")
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

/// Write a temporary .rdp file with full address and username so mstsc uses the saved credential.
/// Local resources: all drives, printers, clipboard (server policy may still restrict).
#[cfg(windows)]
fn write_rdp_file(rdp_url: &str, username: &str) -> Result<PathBuf, String> {
    let temp_dir = std::env::temp_dir();
    let name = format!("tyresoles_rdp_{}.rdp", std::time::SystemTime::now().duration_since(std::time::UNIX_EPOCH).unwrap_or_default().as_millis());
    let path = temp_dir.join(&name);
    // RDP file format: one key-value per line. full address can be host:port.
    // Credentials remain via cmdkey + username here; password is never written to the file.
    let content = format!(
        "full address:s:{}\r\n\
         username:s:{}\r\n\
         drivestoredirect:s:*\r\n\
         redirectprinters:i:1\r\n\
         redirectclipboard:i:1\r\n",
        rdp_url.replace('\r', "").replace('\n', ""),
        username.replace('\r', "").replace('\n', ""),
    );
    let mut f = File::create(&path).map_err(|e| format!("Failed to create .rdp file: {}", e))?;
    f.write_all(content.as_bytes())
        .map_err(|e| format!("Failed to write .rdp file: {}", e))?;
    Ok(path)
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
