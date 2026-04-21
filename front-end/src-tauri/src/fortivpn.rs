//! FortiClient VPN CLI (`FortiVPN.exe`) — status, tunnel list, connect, disconnect.
//!
//! Runs with `CREATE_NO_WINDOW` on Windows so console flashes are avoided.

use serde::{Deserialize, Serialize};
use std::path::PathBuf;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiVpnCliOutput {
    pub exit_code: i32,
    pub stdout: String,
    pub stderr: String,
    /// Populated for `fortivpn_cli_connect`: argv-style line for display (includes password).
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub command_line: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiVpnStatusOutput {
    pub exit_code: i32,
    pub stdout: String,
    pub stderr: String,
    /// Heuristic parse of combined output; `None` when unclear.
    pub connected: Option<bool>,
}

#[derive(Debug, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct FortiVpnOptionalExeArgs {
    #[serde(default)]
    pub exe_path: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiVpnConnectArgs {
    pub tunnel: String,
    pub username: String,
    pub password: String,
    #[serde(default)]
    pub exe_path: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiVpnDisconnectArgs {
    pub tunnel: String,
    #[serde(default)]
    pub exe_path: Option<String>,
}

#[cfg(windows)]
const CREATE_NO_WINDOW: u32 = 0x08000000;

#[cfg(windows)]
fn resolve_fortivpn_exe(override_path: Option<&str>) -> Result<PathBuf, String> {
    if let Some(p) = override_path.map(str::trim).filter(|s| !s.is_empty()) {
        return Ok(PathBuf::from(p));
    }
    let mut candidates: Vec<PathBuf> = Vec::new();
    if let Ok(pf) = std::env::var("ProgramFiles") {
        candidates.push(PathBuf::from(pf).join(r"Fortinet\FortiClient\FortiVPN.exe"));
    }
    candidates.push(PathBuf::from(
        r"C:\Program Files\Fortinet\FortiClient\FortiVPN.exe",
    ));
    if let Ok(pf86) = std::env::var("ProgramFiles(x86)") {
        candidates.push(
            PathBuf::from(pf86).join(r"Fortinet\FortiClient\FortiVPN.exe"),
        );
    }
    candidates.push(PathBuf::from(
        r"C:\Program Files (x86)\Fortinet\FortiClient\FortiVPN.exe",
    ));
    for p in candidates {
        if p.is_file() {
            return Ok(p);
        }
    }
    Err("FortiVPN.exe not found. Install FortiClient VPN.".to_string())
}

#[cfg(windows)]
fn run_fortivpn<I, S>(exe_override: Option<&str>, args: I) -> Result<FortiVpnCliOutput, String>
where
    I: IntoIterator<Item = S>,
    S: AsRef<std::ffi::OsStr>,
{
    use std::os::windows::process::CommandExt;
    use std::process::Command;

    let exe = resolve_fortivpn_exe(exe_override)?;
    let out = Command::new(&exe)
        .args(args)
        .creation_flags(CREATE_NO_WINDOW)
        .output()
        .map_err(|e| format!("Failed to run FortiVPN.exe: {}", e))?;

    Ok(FortiVpnCliOutput {
        exit_code: out.status.code().unwrap_or(-1),
        stdout: String::from_utf8_lossy(&out.stdout).into_owned(),
        stderr: String::from_utf8_lossy(&out.stderr).into_owned(),
        command_line: None,
    })
}

#[cfg(windows)]
fn quote_for_cmd_display(s: &str) -> String {
    s.replace('"', "'")
}

fn interpret_connected(stdout: &str, stderr: &str) -> Option<bool> {
    let s = format!("{stdout}\n{stderr}").to_lowercase();
    if s.contains("disconnected") || s.contains("not connected") || s.contains("no connection") {
        return Some(false);
    }
    if s.contains("connected") {
        return Some(true);
    }
    None
}

#[tauri::command]
pub fn fortivpn_cli_status(args: FortiVpnOptionalExeArgs) -> Result<FortiVpnStatusOutput, String> {
    #[cfg(windows)]
    {
        let o = run_fortivpn(args.exe_path.as_deref(), ["--cli", "--status"])?;
        let connected = interpret_connected(&o.stdout, &o.stderr);
        Ok(FortiVpnStatusOutput {
            exit_code: o.exit_code,
            stdout: o.stdout,
            stderr: o.stderr,
            connected,
        })
    }
    #[cfg(not(windows))]
    {
        let _ = args;
        Err("FortiVPN CLI is only available on Windows.".to_string())
    }
}

#[tauri::command]
pub fn fortivpn_cli_list(args: FortiVpnOptionalExeArgs) -> Result<FortiVpnCliOutput, String> {
    #[cfg(windows)]
    {
        run_fortivpn(args.exe_path.as_deref(), ["--cli", "--list"])
    }
    #[cfg(not(windows))]
    {
        let _ = args;
        Err("FortiVPN CLI is only available on Windows.".to_string())
    }
}

/// `FortiVPN.exe --cli --connect --tunnel … --username … --password … --keeprunning -savecredentials`
#[tauri::command]
pub fn fortivpn_cli_connect(args: FortiVpnConnectArgs) -> Result<FortiVpnCliOutput, String> {
    #[cfg(windows)]
    {
        let tunnel = args.tunnel.trim();
        let user = args.username.trim();
        if tunnel.is_empty() || user.is_empty() {
            return Err("Tunnel name and username are required.".to_string());
        }
        let exe = resolve_fortivpn_exe(args.exe_path.as_deref())?;
        let for_log = format!(
            r#""{}" --cli --connect --tunnel "{}" --username "{}" --password "<redacted>" --keeprunning -savecredentials"#,
            exe.display(),
            quote_for_cmd_display(tunnel),
            quote_for_cmd_display(user),
        );
        log::info!("[FortiVPN] connect (password redacted in log): {}", for_log);

        let command_line = format!(
            r#""{}" --cli --connect --tunnel "{}" --username "{}" --password "{}" --keeprunning -savecredentials"#,
            exe.display(),
            quote_for_cmd_display(tunnel),
            quote_for_cmd_display(user),
            quote_for_cmd_display(args.password.as_str()),
        );

        let out = run_fortivpn(
            args.exe_path.as_deref(),
            [
                "--cli",
                "--connect",
                "--tunnel",
                tunnel,
                "--username",
                user,
                "--password",
                args.password.as_str(),
                "--keeprunning",
                "-savecredentials",
            ],
        )?;
        Ok(FortiVpnCliOutput {
            exit_code: out.exit_code,
            stdout: out.stdout,
            stderr: out.stderr,
            command_line: Some(command_line),
        })
    }
    #[cfg(not(windows))]
    {
        let _ = args;
        Err("FortiVPN CLI is only available on Windows.".to_string())
    }
}

#[tauri::command]
pub fn fortivpn_cli_disconnect(args: FortiVpnDisconnectArgs) -> Result<FortiVpnCliOutput, String> {
    #[cfg(windows)]
    {
        let tunnel = args.tunnel.trim();
        if tunnel.is_empty() {
            return Err("Tunnel name is required.".to_string());
        }
        run_fortivpn(
            args.exe_path.as_deref(),
            ["--cli", "--disconnect", "--tunnel", tunnel],
        )
    }
    #[cfg(not(windows))]
    {
        let _ = args;
        Err("FortiVPN CLI is only available on Windows.".to_string())
    }
}
