//! Windows Service Checker & Starter — Tauri commands.
//!
//! Provides commands to query the status of one or more Windows services
//! (Running / Stopped / etc.) and to start or stop them.
//!
//! These are intended for the Windows Server 2022 host on which the Tauri
//! app is deployed in "Server" mode, so that operations staff can verify
//! that dependent services (e.g. the .NET back-end, SQL Server) are healthy
//! and restart them without opening Task Manager or PowerShell.
//!
//! All commands are Windows-only; on other platforms they return an error
//! so the TypeScript caller can hide the UI gracefully.
//!
//! ### Safety
//! Starting/stopping services requires the caller process to have
//! SERVICE_START / SERVICE_STOP access rights.  On Windows Server 2022
//! the app typically runs under an administrator account, so this works
//! out of the box.  Do NOT expose these commands to untrusted callers.

use serde::{Deserialize, Serialize};

// ── Public data types ──────────────────────────────────────────────────────

/// Status of a single Windows service as understood by the frontend.
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ServiceStatus {
    /// The short service name (same string passed to `sc query`).
    pub name: String,
    /// Human-readable display name (from `sc query` output), or the short name if unavailable.
    pub display_name: String,
    /// One of: "Running", "Stopped", "StartPending", "StopPending",
    ///         "PausePending", "Paused", "ContinuePending", "Unknown"
    pub state: String,
    /// Whether the service is currently in the "Running" state.
    pub is_running: bool,
    /// Whether an automatic start of this service is allowed by the config.
    pub can_start: bool,
    /// Whether an automatic stop of this service is allowed by the config.
    pub can_stop: bool,
}

/// Input payload for the `check_services` command.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CheckServicesInput {
    /// List of service short names to check.
    pub services: Vec<ServiceDescriptor>,
}

/// Descriptor for a single service the frontend wants to monitor.
#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ServiceDescriptor {
    pub name: String,
    #[serde(default)]
    pub can_start: bool,
    #[serde(default)]
    pub can_stop: bool,
}

// ── Tauri commands ─────────────────────────────────────────────────────────

/// Query the status of one or more services.
///
/// Frontend call:
/// ```ts
/// await invoke<ServiceStatus[]>('check_services', {
///   input: {
///     services: [
///       { name: 'TyrsolesApi', canStart: true, canStop: false },
///       { name: 'MSSQLSERVER',  canStart: false, canStop: false },
///     ]
///   }
/// });
/// ```
#[tauri::command]
pub fn check_services(input: CheckServicesInput) -> Result<Vec<ServiceStatus>, String> {
    #[cfg(windows)]
    {
        let mut results = Vec::new();
        for desc in &input.services {
            let status = query_service_status(&desc.name, desc.can_start, desc.can_stop)?;
            results.push(status);
        }
        Ok(results)
    }

    #[cfg(not(windows))]
    {
        let _ = input;
        Err("Service management is only supported on Windows".to_string())
    }
}

/// Start a Windows service by its short name.
///
/// Frontend call:
/// ```ts
/// await invoke('start_service', { serviceName: 'TyrsolesApi' });
/// ```
#[tauri::command]
pub fn start_service(service_name: String) -> Result<ServiceStatus, String> {
    #[cfg(windows)]
    {
        log::info!("[service] Starting service: {}", service_name);
        run_sc_command("start", &service_name)?;
        // Wait a moment and then poll state (up to ~5 s)
        poll_for_state(&service_name, "RUNNING", 10, 500)
    }

    #[cfg(not(windows))]
    {
        let _ = service_name;
        Err("Service management is only supported on Windows".to_string())
    }
}

/// Stop a Windows service by its short name.
///
/// Frontend call:
/// ```ts
/// await invoke('stop_service', { serviceName: 'TyrsolesApi' });
/// ```
#[tauri::command]
pub fn stop_service(service_name: String) -> Result<ServiceStatus, String> {
    #[cfg(windows)]
    {
        log::info!("[service] Stopping service: {}", service_name);
        run_sc_command("stop", &service_name)?;
        poll_for_state(&service_name, "STOPPED", 10, 500)
    }

    #[cfg(not(windows))]
    {
        let _ = service_name;
        Err("Service management is only supported on Windows".to_string())
    }
}

/// Restart a Windows service (stop → start).
///
/// Frontend call:
/// ```ts
/// await invoke('restart_service', { serviceName: 'TyrsolesApi' });
/// ```
#[tauri::command]
pub fn restart_service(service_name: String) -> Result<ServiceStatus, String> {
    #[cfg(windows)]
    {
        log::info!("[service] Restarting service: {}", service_name);
        // Ignore stop errors (service may already be stopped)
        let _ = run_sc_command("stop", &service_name);
        // Wait until stopped (or timeout)
        let _ = poll_for_state(&service_name, "STOPPED", 12, 500);
        // Now start
        run_sc_command("start", &service_name)?;
        poll_for_state(&service_name, "RUNNING", 12, 500)
    }

    #[cfg(not(windows))]
    {
        let _ = service_name;
        Err("Service management is only supported on Windows".to_string())
    }
}

// ── Internal helpers (Windows-only) ───────────────────────────────────────

/// Parse `sc query <name>` stdout and return a [`ServiceStatus`].
#[cfg(windows)]
fn query_service_status(
    name: &str,
    can_start: bool,
    can_stop: bool,
) -> Result<ServiceStatus, String> {
    use std::process::Command;

    let out = Command::new("sc")
        .args(["query", name])
        .output()
        .map_err(|e| format!("Failed to run 'sc query {}': {}", name, e))?;

    let stdout = String::from_utf8_lossy(&out.stdout);
    let stderr = String::from_utf8_lossy(&out.stderr);

    log::debug!("[service] sc query {}: stdout={} stderr={}", name, stdout.trim(), stderr.trim());

    // sc query exits with code 1060 when the service doesn't exist.
    if !out.status.success() && !stdout.contains("STATE") {
        return Ok(ServiceStatus {
            name: name.to_string(),
            display_name: name.to_string(),
            state: "Unknown".to_string(),
            is_running: false,
            can_start,
            can_stop,
        });
    }

    let state = parse_sc_state(&stdout);
    let display_name = parse_sc_display_name(&stdout).unwrap_or_else(|| name.to_string());
    let is_running = state == "Running";

    Ok(ServiceStatus {
        name: name.to_string(),
        display_name,
        state,
        is_running,
        can_start,
        can_stop,
    })
}

/// Extract the STATE line from `sc query` output.
/// Input example: `        STATE              : 4  RUNNING`
#[cfg(windows)]
fn parse_sc_state(output: &str) -> String {
    for line in output.lines() {
        let trimmed = line.trim();
        if trimmed.starts_with("STATE") {
            // Format: "STATE              : 4  RUNNING"
            if let Some(colon_pos) = trimmed.find(':') {
                let after = trimmed[colon_pos + 1..].trim();
                // after is like "4  RUNNING" — take the last token
                let state_str = after
                    .split_whitespace()
                    .last()
                    .unwrap_or("Unknown")
                    .to_ascii_uppercase();

                return match state_str.as_str() {
                    "RUNNING"          => "Running".to_string(),
                    "STOPPED"          => "Stopped".to_string(),
                    "START_PENDING"    => "StartPending".to_string(),
                    "STOP_PENDING"     => "StopPending".to_string(),
                    "PAUSE_PENDING"    => "PausePending".to_string(),
                    "PAUSED"           => "Paused".to_string(),
                    "CONTINUE_PENDING" => "ContinuePending".to_string(),
                    _                  => "Unknown".to_string(),
                };
            }
        }
    }
    "Unknown".to_string()
}

/// Extract the DISPLAY_NAME line from `sc query` output.
#[cfg(windows)]
fn parse_sc_display_name(output: &str) -> Option<String> {
    for line in output.lines() {
        let trimmed = line.trim();
        if trimmed.starts_with("DISPLAY_NAME") {
            if let Some(colon_pos) = trimmed.find(':') {
                let name = trimmed[colon_pos + 1..].trim().to_string();
                if !name.is_empty() {
                    return Some(name);
                }
            }
        }
    }
    None
}

/// Run `sc <verb> <name>` (e.g. `sc start TyrsolesApi`).
#[cfg(windows)]
fn run_sc_command(verb: &str, name: &str) -> Result<(), String> {
    use std::process::Command;

    let out = Command::new("sc")
        .args([verb, name])
        .output()
        .map_err(|e| format!("Failed to run 'sc {} {}': {}", verb, name, e))?;

    let stdout = String::from_utf8_lossy(&out.stdout);
    let stderr = String::from_utf8_lossy(&out.stderr);
    log::info!(
        "[service] sc {} {} → exit={} stdout={} stderr={}",
        verb,
        name,
        out.status,
        stdout.trim(),
        stderr.trim()
    );

    // sc returns 0 on success; 1056 means "service is already running" which we treat as OK.
    // Exit code 1062 = "service has not been started" when stopping — also OK for restart flow.
    let code = out.status.code().unwrap_or(-1);
    if out.status.success() || code == 1056 || code == 1062 {
        Ok(())
    } else {
        Err(format!("sc {} {} failed (exit {}): {}", verb, name, code, stdout.trim()))
    }
}

/// Poll `sc query <name>` up to `max_polls` times with `delay_ms` between polls
/// until the service reaches `wanted_state` (raw uppercase, e.g. "RUNNING").
/// Returns the final [`ServiceStatus`] (may not yet be in the wanted state if timed out).
#[cfg(windows)]
fn poll_for_state(
    name: &str,
    wanted_state: &str,
    max_polls: u32,
    delay_ms: u64,
) -> Result<ServiceStatus, String> {
    for attempt in 0..max_polls {
        std::thread::sleep(std::time::Duration::from_millis(delay_ms));
        let status = query_service_status(name, true, true)?;
        let raw_state = status.state.to_ascii_uppercase();
        let raw_state_no_space = raw_state.replace([' ', '_'], ""); // normalise
        let wanted_no_space = wanted_state.replace([' ', '_'], "");
        log::debug!(
            "[service] poll [{}/{}] {} state={}",
            attempt + 1,
            max_polls,
            name,
            status.state
        );
        if raw_state_no_space == wanted_no_space {
            return Ok(status);
        }
    }
    // Return whatever we got on the last poll
    query_service_status(name, true, true)
}
