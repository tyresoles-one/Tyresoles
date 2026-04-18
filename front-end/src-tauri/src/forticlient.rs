//! FortiClient VPN — detect installation and run a silent uninstall (Windows).
//!
//! Reads `DisplayName` under the standard `Uninstall` registry keys, then prefers
//! `QuietUninstallString`, else derives a silent command from `UninstallString`
//! (`msiexec /x {GUID} /qn /norestart` or Fortinet `Uninstall.exe /quiet`).

use serde::Serialize;

// #region agent log
#[cfg(windows)]
fn agent_debug_log(
    hypothesis_id: &str,
    location: &'static str,
    message: &str,
    data: serde_json::Value,
) {
    use std::io::Write;
    let ts = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_millis())
        .unwrap_or(0);
    let payload = serde_json::json!({
        "sessionId": "84fcde",
        "hypothesisId": hypothesis_id,
        "location": location,
        "message": message,
        "data": data,
        "timestamp": ts,
    });
    let path =
        std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../../debug-84fcde.log");
    if let Ok(mut f) = std::fs::OpenOptions::new()
        .create(true)
        .append(true)
        .open(&path)
    {
        let _ = writeln!(f, "{}", payload);
    }
}
// #endregion

#[cfg(windows)]
fn msi_status_considered_ok(status: &std::process::ExitStatus) -> bool {
    status.success() || status.code() == Some(3010)
}

#[cfg(windows)]
fn msi_user_hint(code: Option<i32>) -> String {
    match code {
        Some(1603) => "MSI fatal error (1603): often requires Administrator rights, closing all FortiClient processes, or completing a pending reboot. If this persists, run this app as Administrator and try again.".to_string(),
        Some(1618) => "Another installation or uninstall is already in progress (1618). Wait and retry.".to_string(),
        Some(1602) => "The uninstall was cancelled (1602).".to_string(),
        Some(3010) => "Uninstall completed; a system restart may be required (3010).".to_string(),
        Some(c) => format!("Windows Installer exit code {}.", c),
        None => "Unknown exit code.".to_string(),
    }
}

#[cfg(windows)]
fn run_exe_uninstall_elevated(exe: &std::path::Path) -> std::io::Result<std::process::ExitStatus> {
    use std::process::{Command, Stdio};
    let path_str = exe.to_string_lossy().replace('\'', "''");
    let ps = format!(
        "$p = Start-Process -FilePath '{}' -ArgumentList '/quiet','/norestart' -Verb RunAs -Wait -PassThru; \
         if ($null -eq $p) {{ exit 1 }} \
         elseif ($null -ne $p.ExitCode) {{ exit $p.ExitCode }} \
         else {{ exit 0 }}",
        path_str
    );
    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &ps,
        ])
        .stdin(Stdio::null())
        .status()
}

#[cfg(windows)]
/// Prefer QuietUninstallString, then Fortinet `Uninstall.exe` lines, then MSI (`msiexec`).
fn uninstall_entry_rank(entry: &UninstallEntry) -> u8 {
    if entry
        .quiet_uninstall
        .as_ref()
        .map(|s| !s.trim().is_empty())
        .unwrap_or(false)
    {
        return 0;
    }
    let u = entry
        .uninstall_string
        .as_ref()
        .map(|s| s.to_lowercase())
        .unwrap_or_default();
    if u.contains("uninstall.exe") {
        return 1;
    }
    if u.contains("msiexec") {
        return 3;
    }
    2
}

/// Reduces 1603 when Restart Manager blocks uninstall (files "in use").
#[cfg(windows)]
const MSI_UNINSTALL_EXTRAS: &[&str] = &["MSIRESTARTMANAGERCONTROL=Disable"];

#[cfg(windows)]
fn run_msiexec_uninstall_elevated(guid: &str) -> std::io::Result<std::process::ExitStatus> {
    use std::process::{Command, Stdio};
    let ps = format!(
        "$p = Start-Process -FilePath msiexec.exe -ArgumentList '/x','{}','/qn','/norestart','MSIRESTARTMANAGERCONTROL=Disable' -Verb RunAs -Wait -PassThru; \
         if ($null -eq $p) {{ exit 1 }} \
         elseif ($null -ne $p.ExitCode) {{ exit $p.ExitCode }} \
         else {{ exit 0 }}",
        guid
    );
    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &ps,
        ])
        .stdin(Stdio::null())
        .status()
}

#[cfg(windows)]
fn forticlient_default_uninstall_exe_paths() -> Vec<std::path::PathBuf> {
    use std::path::PathBuf;
    let mut v = Vec::new();
    if let Ok(pf) = std::env::var("ProgramFiles") {
        v.push(PathBuf::from(pf).join(r"Fortinet\FortiClient\Uninstall.exe"));
    }
    v.push(PathBuf::from(r"C:\Program Files\Fortinet\FortiClient\Uninstall.exe"));
    if let Ok(pf86) = std::env::var("ProgramFiles(x86)") {
        v.push(PathBuf::from(pf86).join(r"Fortinet\FortiClient\Uninstall.exe"));
    }
    v.push(PathBuf::from(
        r"C:\Program Files (x86)\Fortinet\FortiClient\Uninstall.exe",
    ));
    v
}

#[cfg(windows)]
fn is_likely_fortinet_uninstall_exe(name_lower: &str) -> bool {
    if !name_lower.ends_with(".exe") {
        return false;
    }
    name_lower.contains("uninstall")
        || name_lower == "fcuninst.exe"
        || name_lower.starts_with("uninst")
        // Fortinet community / slim VPN builds sometimes ship these instead of Uninstall.exe
        || name_lower.contains("fcremove")
}

/// Lists `.exe` basenames under a directory tree (debug / discovery). Does not log paths beyond root.
#[cfg(windows)]
fn collect_exe_basenames_inventory(
    root: &std::path::Path,
    max_depth: usize,
    cap: usize,
    out: &mut std::collections::BTreeSet<String>,
) {
    use std::path::Path;
    fn walk(
        dir: &Path,
        depth: usize,
        max_depth: usize,
        out: &mut std::collections::BTreeSet<String>,
        cap: usize,
    ) {
        if out.len() >= cap || depth > max_depth {
            return;
        }
        let rd = match std::fs::read_dir(dir) {
            Ok(r) => r,
            Err(_) => return,
        };
        for ent in rd.flatten() {
            if out.len() >= cap {
                return;
            }
            let p = ent.path();
            if p.is_file() {
                if let Some(n) = p.file_name().and_then(|s| s.to_str()) {
                    if n.to_lowercase().ends_with(".exe") {
                        out.insert(n.to_string());
                    }
                }
            } else if p.is_dir() {
                walk(&p, depth + 1, max_depth, out, cap);
            }
        }
    }
    walk(root, 0, max_depth, out, cap);
}

/// Best-effort: stop common FortiClient processes so MSI uninstall is less likely to hit 1603 (locked files).
#[cfg(windows)]
fn stop_forticlient_processes() {
    use std::process::{Command, Stdio};
    const NAMES: &[&str] = &[
        "FortiClient.exe",
        "FortiTray.exe",
        "FortiSSLVPNdaemon.exe",
        "FortiClientSSLVPN.exe",
        "fortisslvpn.exe",
    ];
    let mut results = Vec::new();
    for im in NAMES {
        let st = Command::new("taskkill.exe")
            .args(["/F", "/T", "/IM", im])
            .stdin(Stdio::null())
            .stdout(Stdio::null())
            .stderr(Stdio::null())
            .status();
        results.push(serde_json::json!({
            "image": im,
            "exitCode": st.ok().and_then(|s| s.code()),
        }));
    }
    agent_debug_log(
        "H10",
        "forticlient:stop_forticlient_processes",
        "taskkill sweep",
        serde_json::json!({ "results": results }),
    );
}

/// FortiClient often ships `Uninstall.exe` under a subfolder (e.g. `bin`), not the install root.
#[cfg(windows)]
fn collect_uninstall_exes_under_dir(
    root: &std::path::Path,
    max_depth: usize,
    out: &mut std::collections::BTreeSet<String>,
) {
    use std::path::Path;
    fn walk(dir: &Path, depth: usize, max_depth: usize, out: &mut std::collections::BTreeSet<String>) {
        if depth > max_depth {
            return;
        }
        let rd = match std::fs::read_dir(dir) {
            Ok(r) => r,
            Err(_) => return,
        };
        for ent in rd.flatten() {
            let p = ent.path();
            if p.is_file() {
                let name = p
                    .file_name()
                    .and_then(|s| s.to_str())
                    .unwrap_or("")
                    .to_lowercase();
                if is_likely_fortinet_uninstall_exe(&name) {
                    out.insert(p.to_string_lossy().into_owned());
                }
            } else if p.is_dir() {
                walk(&p, depth + 1, max_depth, out);
            }
        }
    }
    walk(root, 0, max_depth, out);
}

/// Default paths plus any likely uninstall `.exe` under known Fortinet folders and each
/// registry `InstallLocation` directory (when FortiClient is not under `Fortinet\FortiClient`).
#[cfg(windows)]
fn forticlient_uninstall_exe_candidates(entries: &[UninstallEntry]) -> Vec<std::path::PathBuf> {
    use std::collections::BTreeSet;
    use std::path::PathBuf;
    let mut set: BTreeSet<String> = BTreeSet::new();
    for p in forticlient_default_uninstall_exe_paths() {
        set.insert(p.to_string_lossy().into_owned());
    }
    let mut dir_candidates: Vec<PathBuf> = vec![
        std::env::var("ProgramFiles").map(|p| PathBuf::from(p).join(r"Fortinet\FortiClient")),
        Ok(PathBuf::from(r"C:\Program Files\Fortinet\FortiClient")),
        std::env::var("ProgramFiles(x86)").map(|p| PathBuf::from(p).join(r"Fortinet\FortiClient")),
        Ok(PathBuf::from(
            r"C:\Program Files (x86)\Fortinet\FortiClient",
        )),
    ]
    .into_iter()
    .flatten()
    .collect();
    for e in entries {
        if let Some(ref loc) = e.install_location {
            let t = loc.trim();
            if !t.is_empty() {
                dir_candidates.push(PathBuf::from(t));
            }
        }
    }
    dir_candidates.sort();
    dir_candidates.dedup();
    const TREE_DEPTH: usize = 6;
    for d in dir_candidates {
        if d.is_dir() {
            collect_uninstall_exes_under_dir(&d, TREE_DEPTH, &mut set);
        }
    }
    set.into_iter().map(PathBuf::from).collect()
}

/// After MSI + elevated MSI still fail (e.g. 1603), try Fortinet uninstall EXEs from disk (elevated).
#[cfg(windows)]
fn try_fortinet_uninstall_exe_after_msi_failure(entries: &[UninstallEntry]) -> Result<(), String> {
    stop_forticlient_processes();

    let mut inv: Vec<serde_json::Value> = Vec::new();
    for e in entries {
        if let Some(ref loc) = e.install_location {
            let t = loc.trim();
            if t.is_empty() {
                continue;
            }
            let root = std::path::Path::new(t);
            let read_ok = root.is_dir() && std::fs::read_dir(root).is_ok();
            let mut names = std::collections::BTreeSet::new();
            if read_ok {
                collect_exe_basenames_inventory(root, 8, 80, &mut names);
            }
            let exe_basenames: Vec<String> = names.into_iter().collect();
            inv.push(serde_json::json!({
                "installLocation": t,
                "readDirOk": read_ok,
                "exeBasenames": exe_basenames,
            }));
        }
    }
    agent_debug_log(
        "H9",
        "forticlient:try_fortinet_uninstall_exe_after_msi_failure",
        "exe basename inventory under InstallLocation (any .exe)",
        serde_json::json!({ "trees": inv }),
    );

    let candidates = forticlient_uninstall_exe_candidates(entries);
    let scan: Vec<serde_json::Value> = candidates
        .iter()
        .map(|p| {
            serde_json::json!({
                "path": p.to_string_lossy(),
                "isFile": p.is_file(),
            })
        })
        .collect();
    agent_debug_log(
        "H7",
        "forticlient:try_fortinet_uninstall_exe_after_msi_failure",
        "exe candidates before elevated run",
        serde_json::json!({
            "installLocations": entries.iter().map(|e| e.install_location.clone()).collect::<Vec<_>>(),
            "candidates": scan,
        }),
    );

    for p in candidates {
        if !p.is_file() {
            continue;
        }
        agent_debug_log(
            "H7",
            "forticlient:try_fortinet_uninstall_exe_after_msi_failure",
            "trying Fortinet uninstall exe fallback",
            serde_json::json!({ "path": p.to_string_lossy() }),
        );
        match run_exe_uninstall_elevated(&p) {
            Ok(st) => {
                agent_debug_log(
                    "H7",
                    "forticlient:try_fortinet_uninstall_exe_after_msi_failure",
                    "after uninstall exe elevated",
                    serde_json::json!({
                        "exitCode": st.code(),
                        "success": st.success(),
                    }),
                );
                if msi_status_considered_ok(&st) || st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H7",
                    "forticlient:try_fortinet_uninstall_exe_after_msi_failure",
                    "uninstall exe elevated spawn failed",
                    serde_json::json!({ "error": e.to_string(), "path": p.to_string_lossy() }),
                );
            }
        }
    }
    Err("Fortinet uninstall helper (.exe) not found under Fortinet\\FortiClient or did not succeed."
        .to_string())
}

/// H9 often shows no `*uninstall*.exe`; removal is MSI-only. Try Windows Package Manager as last resort.
#[cfg(windows)]
fn run_winget_uninstall_elevated_id(package_id: &str) -> std::io::Result<std::process::ExitStatus> {
    use std::process::{Command, Stdio};
    let ps = format!(
        "$w=(Get-Command winget -ErrorAction SilentlyContinue).Source;if(-not $w){{exit 40}};$p=Start-Process -FilePath $w -ArgumentList 'uninstall','--id','{}','--silent','--disable-interactivity' -Verb RunAs -Wait -PassThru;if($null -eq $p){{exit 1}}elseif($null -ne $p.ExitCode){{exit $p.ExitCode}}else{{exit 0}}",
        package_id
    );
    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &ps,
        ])
        .stdin(Stdio::null())
        .status()
}

/// MSI product code `{GUID}` as registered in ARP — often the only stable match for non–winget installs.
#[cfg(windows)]
fn run_winget_uninstall_elevated_product_code(product_code: &str) -> std::io::Result<std::process::ExitStatus> {
    use std::process::{Command, Stdio};
    let pc = product_code.replace('\'', "''");
    let ps = format!(
        "$w=(Get-Command winget -ErrorAction SilentlyContinue).Source;if(-not $w){{exit 40}};$p=Start-Process -FilePath $w -ArgumentList 'uninstall','--product-code','{}','--silent','--disable-interactivity' -Verb RunAs -Wait -PassThru;if($null -eq $p){{exit 1}}elseif($null -ne $p.ExitCode){{exit $p.ExitCode}}else{{exit 0}}",
        pc
    );
    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &ps,
        ])
        .stdin(Stdio::null())
        .status()
}

/// Matches "Apps & features" display name when the product was not installed from winget (`--id` fails).
#[cfg(windows)]
fn run_winget_uninstall_elevated_display_name(name: &str) -> std::io::Result<std::process::ExitStatus> {
    use std::process::{Command, Stdio};
    let n = name.replace('\'', "''");
    let ps = format!(
        "$w=(Get-Command winget -ErrorAction SilentlyContinue).Source;if(-not $w){{exit 40}};$p=Start-Process -FilePath $w -ArgumentList 'uninstall','--name','{}','--exact','--silent','--disable-interactivity' -Verb RunAs -Wait -PassThru;if($null -eq $p){{exit 1}}elseif($null -ne $p.ExitCode){{exit $p.ExitCode}}else{{exit 0}}",
        n
    );
    Command::new("powershell.exe")
        .args([
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-WindowStyle",
            "Hidden",
            "-Command",
            &ps,
        ])
        .stdin(Stdio::null())
        .status()
}

#[cfg(windows)]
fn winget_exit_hint(code: Option<i32>) -> Option<&'static str> {
    match code {
        // Observed when `--id` does not match an installed winget/ARP mapping (MSI-only install).
        Some(-1_978_335_230) => {
            Some("winget: no package matched this query (common for Fortinet MSI installs).")
        }
        _ => None,
    }
}

#[cfg(windows)]
fn try_winget_uninstall_forticlient_elevated(
    registry_display_name: &str,
    msi_product_code: &str,
) -> Result<(), String> {
    use std::process::{Command, Stdio};
    const IDS: &[&str] = &["Fortinet.FortiClient", "Fortinet.FortiClientVPN"];

    let mut display_names: Vec<String> = Vec::new();
    let dn = registry_display_name.trim();
    if !dn.is_empty() {
        display_names.push(dn.to_string());
    }
    for n in ["FortiClient", "FortiClient VPN"] {
        if !display_names.iter().any(|x| x.eq_ignore_ascii_case(n)) {
            display_names.push(n.to_string());
        }
    }

    let has_winget = Command::new("where.exe")
        .arg("winget")
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .status()
        .map(|s| s.success())
        .unwrap_or(false);

    agent_debug_log(
        "H11",
        "forticlient:try_winget_uninstall_forticlient_elevated",
        "winget presence",
        serde_json::json!({ "whereWingetOk": has_winget }),
    );

    if !has_winget {
        return Err("winget is not available (install App Installer / Windows Package Manager).".to_string());
    }

    let pc = msi_product_code.trim();
    if !pc.is_empty() {
        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget user-scope by --product-code (MSI)",
            serde_json::json!({ "productCode": pc }),
        );
        match Command::new("winget.exe")
            .args([
                "uninstall",
                "--product-code",
                pc,
                "--silent",
                "--disable-interactivity",
            ])
            .stdin(Stdio::null())
            .status()
        {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget user-scope by product-code",
                    serde_json::json!({
                        "productCode": pc,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget user-scope by product-code spawn failed",
                    serde_json::json!({ "productCode": pc, "error": e.to_string() }),
                );
            }
        }

        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget elevated by --product-code (MSI)",
            serde_json::json!({ "productCode": pc }),
        );
        match run_winget_uninstall_elevated_product_code(pc) {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget elevated by product-code",
                    serde_json::json!({
                        "productCode": pc,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.code() == Some(40) {
                    return Err("winget not found in elevated session.".to_string());
                }
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget elevated by product-code spawn failed",
                    serde_json::json!({ "productCode": pc, "error": e.to_string() }),
                );
            }
        }
    }

    for id in IDS {
        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget user-scope attempt",
            serde_json::json!({ "packageId": id }),
        );
        match Command::new("winget.exe")
            .args([
                "uninstall",
                "--id",
                id,
                "--silent",
                "--disable-interactivity",
            ])
            .stdin(Stdio::null())
            .status()
        {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget user-scope",
                    serde_json::json!({
                        "packageId": id,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget user-scope spawn failed",
                    serde_json::json!({ "packageId": id, "error": e.to_string() }),
                );
            }
        }
    }

    for display_name in &display_names {
        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget user-scope by --name --exact",
            serde_json::json!({ "displayName": display_name }),
        );
        match Command::new("winget.exe")
            .args([
                "uninstall",
                "--name",
                display_name.as_str(),
                "--exact",
                "--silent",
                "--disable-interactivity",
            ])
            .stdin(Stdio::null())
            .status()
        {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget user-scope by name",
                    serde_json::json!({
                        "displayName": display_name,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget user-scope by name spawn failed",
                    serde_json::json!({ "displayName": display_name, "error": e.to_string() }),
                );
            }
        }
    }

    for id in IDS {
        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget elevated attempt",
            serde_json::json!({ "packageId": id }),
        );
        match run_winget_uninstall_elevated_id(id) {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget elevated",
                    serde_json::json!({
                        "packageId": id,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.code() == Some(40) {
                    return Err("winget not found in elevated session.".to_string());
                }
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget elevated spawn failed",
                    serde_json::json!({ "packageId": id, "error": e.to_string() }),
                );
            }
        }
    }

    for display_name in &display_names {
        agent_debug_log(
            "H11",
            "forticlient:try_winget_uninstall_forticlient_elevated",
            "winget elevated by --name --exact",
            serde_json::json!({ "displayName": display_name }),
        );
        match run_winget_uninstall_elevated_display_name(display_name) {
            Ok(st) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "after winget elevated by name",
                    serde_json::json!({
                        "displayName": display_name,
                        "exitCode": st.code(),
                        "success": st.success(),
                        "hint": winget_exit_hint(st.code()),
                    }),
                );
                if st.code() == Some(40) {
                    return Err("winget not found in elevated session.".to_string());
                }
                if st.success() {
                    return Ok(());
                }
            }
            Err(e) => {
                agent_debug_log(
                    "H11",
                    "forticlient:try_winget_uninstall_forticlient_elevated",
                    "winget elevated by name spawn failed",
                    serde_json::json!({ "displayName": display_name, "error": e.to_string() }),
                );
            }
        }
    }

    Err("winget uninstall did not succeed (--product-code, package id, display name with --exact).".to_string())
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FortiClientInstallStatus {
    pub installed: bool,
}

/// Returns whether a product whose display name contains "FortiClient" is registered.
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
        Ok(FortiClientInstallStatus {
            installed: false,
        })
    }
}

/// Uninstalls FortiClient silently if present. No-op error if not installed.
#[tauri::command]
pub fn uninstall_forticlient_silent() -> Result<(), String> {
    #[cfg(windows)]
    {
        let entries = collect_forticlient_uninstall_entries();
        if entries.is_empty() {
            return Err("FortiClient is not installed.".to_string());
        }

        agent_debug_log(
            "H7",
            "forticlient:uninstall_forticlient_silent",
            "registry entries (sorted preference)",
            serde_json::json!({
                "count": entries.len(),
                "entries": entries.iter().map(|e| {
                    serde_json::json!({
                        "displayName": e.display_name,
                        "rank": uninstall_entry_rank(e),
                    })
                }).collect::<Vec<_>>(),
            }),
        );

        let mut last_err: Option<String> = None;
        for entry in &entries {
            log::info!(
                "[FortiClient] Uninstall attempt (display: {:?})",
                entry.display_name
            );

            agent_debug_log(
                "H2",
                "forticlient:uninstall_forticlient_silent",
                "entry before run_silent_uninstall",
                serde_json::json!({
                    "displayName": entry.display_name,
                    "installLocation": entry.install_location,
                    "hasQuietUninstall": entry.quiet_uninstall.as_ref().map(|s| !s.trim().is_empty()).unwrap_or(false),
                    "hasUninstallString": entry.uninstall_string.as_ref().map(|s| !s.trim().is_empty()).unwrap_or(false),
                }),
            );

            match run_silent_uninstall(entry, &entries) {
                Ok(()) => {
                    log::info!("[FortiClient] Silent uninstall finished.");
                    return Ok(());
                }
                Err(e) => {
                    log::warn!("[FortiClient] Attempt failed: {}", e);
                    last_err = Some(e);
                }
            }
        }

        return Err(last_err.unwrap_or_else(|| "FortiClient uninstall failed.".to_string()));
    }
    #[cfg(not(windows))]
    {
        Err("FortiClient uninstall is only supported on Windows.".to_string())
    }
}

#[cfg(windows)]
#[derive(Debug)]
struct UninstallEntry {
    display_name: String,
    quiet_uninstall: Option<String>,
    uninstall_string: Option<String>,
    /// `InstallLocation` from the uninstall registry key (product install root).
    install_location: Option<String>,
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
            let install_location: Option<String> = subkey.get_value("InstallLocation").ok();

            out.push(UninstallEntry {
                display_name,
                quiet_uninstall,
                uninstall_string,
                install_location,
            });
        }
    }
    out.sort_by_key(uninstall_entry_rank);
    out
}

#[cfg(windows)]
fn find_forticlient_uninstall_entry() -> Option<UninstallEntry> {
    let mut v = collect_forticlient_uninstall_entries();
    if v.is_empty() {
        None
    } else {
        Some(v.remove(0))
    }
}

#[cfg(windows)]
fn run_silent_uninstall(
    entry: &UninstallEntry,
    all_entries: &[UninstallEntry],
) -> Result<(), String> {
    use std::process::Command;
    use std::process::Stdio;

    if let Some(quiet) = &entry.quiet_uninstall {
        let quiet = quiet.trim();
        if !quiet.is_empty() {
            agent_debug_log(
                "H2",
                "forticlient:run_silent_uninstall",
                "branch quiet_uninstall_string",
                serde_json::json!({
                    "quietPrefix": quiet.chars().take(120).collect::<String>(),
                }),
            );
            let status = Command::new("cmd.exe")
                .args(["/C", quiet])
                .stdin(Stdio::null())
                .status()
                .map_err(|e| format!("Failed to run QuietUninstallString: {}", e))?;
            agent_debug_log(
                "H2",
                "forticlient:run_silent_uninstall",
                "after quiet cmd status",
                serde_json::json!({ "exitCode": status.code(), "success": status.success() }),
            );
            if !status.success() {
                return Err(format!(
                    "Quiet uninstall exited with code {:?}",
                    status.code()
                ));
            }
            return Ok(());
        }
    }

    agent_debug_log(
        "H2",
        "forticlient:run_silent_uninstall",
        "skipped or empty QuietUninstallString",
        serde_json::json!({ "fellThrough": true }),
    );

    let uninstall = entry
        .uninstall_string
        .as_ref()
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .ok_or_else(|| "No uninstall information in registry.".to_string())?;

    let lower = uninstall.to_lowercase();
    if lower.contains("msiexec") {
        let guid = extract_msi_product_code(&uninstall).ok_or_else(|| {
            "Could not parse MSI product code from UninstallString.".to_string()
        })?;
        agent_debug_log(
            "H1_H3_H4",
            "forticlient:run_silent_uninstall",
            "branch msiexec",
            serde_json::json!({
                "productCode": guid,
                "uninstallStringPrefix": uninstall.chars().take(200).collect::<String>(),
            }),
        );
        stop_forticlient_processes();
        let status = Command::new("msiexec.exe")
            .args([
                "/x",
                guid.as_str(),
                "/qn",
                "/norestart",
                MSI_UNINSTALL_EXTRAS[0],
            ])
            .stdin(Stdio::null())
            .status()
            .map_err(|e| format!("msiexec failed: {}", e))?;
        agent_debug_log(
            "H1",
            "forticlient:run_silent_uninstall",
            "after msiexec status",
            serde_json::json!({
                "exitCode": status.code(),
                "success": status.success(),
                "note1603": "MSI fatal error — often admin rights, reboot pending, or locked FortiClient processes",
            }),
        );
        if msi_status_considered_ok(&status) {
            return Ok(());
        }
        if status.code() == Some(1603) {
            agent_debug_log(
                "H1",
                "forticlient:run_silent_uninstall",
                "1603: attempting elevated msiexec via PowerShell RunAs",
                serde_json::json!({ "productCode": guid }),
            );
            stop_forticlient_processes();
            match run_msiexec_uninstall_elevated(&guid) {
                Ok(st2) => {
                    agent_debug_log(
                        "H1",
                        "forticlient:run_silent_uninstall",
                        "after elevated msiexec",
                        serde_json::json!({
                            "exitCode": st2.code(),
                            "success": st2.success(),
                            "msiOk": msi_status_considered_ok(&st2),
                        }),
                    );
                    if msi_status_considered_ok(&st2) {
                        return Ok(());
                    }
                    if try_fortinet_uninstall_exe_after_msi_failure(all_entries).is_ok() {
                        return Ok(());
                    }
                    match try_winget_uninstall_forticlient_elevated(&entry.display_name, &guid) {
                        Ok(()) => return Ok(()),
                        Err(winget_err) => {
                            return Err(format!(
                                "msiexec failed (including elevated retry): code {:?}. {} Winget fallback: {}",
                                st2.code(),
                                msi_user_hint(st2.code()),
                                winget_err
                            ));
                        }
                    }
                }
                Err(e) => {
                    agent_debug_log(
                        "H1",
                        "forticlient:run_silent_uninstall",
                        "elevated msiexec spawn failed",
                        serde_json::json!({ "error": e.to_string() }),
                    );
                    return Err(format!(
                        "msiexec exited with code {:?}. Elevated retry could not start: {}. {}",
                        status.code(),
                        e,
                        msi_user_hint(status.code())
                    ));
                }
            }
        }
        return Err(format!(
            "msiexec exited with code {:?}. {}",
            status.code(),
            msi_user_hint(status.code())
        ));
    }

    // Fortinet-style uninstaller: ...\Uninstall.exe [args] — add /quiet /norestart if missing.
    let parts = shell_words::split(&uninstall).map_err(|e| e.to_string())?;
    if parts.is_empty() {
        return Err("UninstallString could not be parsed.".to_string());
    }

    let mut cmd = Command::new(&parts[0]);
    if parts.len() > 1 {
        cmd.args(&parts[1..]);
    }
    let joined = parts.join(" ").to_lowercase();
    if !joined.contains("/quiet") && !joined.contains("/qn") {
        cmd.arg("/quiet");
    }
    if !joined.contains("/norestart") {
        cmd.arg("/norestart");
    }

    let status = cmd
        .stdin(Stdio::null())
        .status()
        .map_err(|e| format!("Failed to run uninstaller: {}", e))?;

    agent_debug_log(
        "H5",
        "forticlient:run_silent_uninstall",
        "after fortinet uninstall.exe status",
        serde_json::json!({ "exitCode": status.code(), "success": status.success() }),
    );

    if !status.success() {
        return Err(format!("Uninstaller exited with code {:?}", status.code()));
    }
    Ok(())
}

#[cfg(windows)]
fn extract_msi_product_code(uninstall: &str) -> Option<String> {
    // Product code appears as {XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX}
    let start = uninstall.find('{')?;
    let end = uninstall.find('}')?;
    if end <= start {
        return None;
    }
    Some(uninstall[start..=end].to_string())
}
