//! Resumable VPN installer download to a stable per-user folder, optional SHA-256 verify,
//! and zip extraction (deflate) when the artifact is a compressed archive.
//!
//! Storage: `%LOCALAPPDATA%\\com.tyresoles.app\\vpn-installer\\` (see `vpn_installer_dir`).
//! Resume uses HTTP `Range` + a sidecar `vpn-installer-state.json`. Transport uses
//! `Accept-Encoding: identity` so byte ranges match the on-disk file (no gzip transport).

use crate::app_config::APP_IDENTIFIER;
use futures_util::StreamExt;
use serde::{Deserialize, Serialize};
use sha2::Digest;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Instant;
use tauri::{AppHandle, Emitter};
use tokio::fs::{self as tfs, OpenOptions};
use tokio::io::AsyncWriteExt;

const STATE_FILE: &str = "vpn-installer-state.json";
const PART_SUFFIX: &str = ".part";

/// Shared cancel + single-flight guard for downloads.
pub struct VpnDownloadControl {
    cancel: Arc<AtomicBool>,
    in_progress: Arc<AtomicBool>,
}

impl Default for VpnDownloadControl {
    fn default() -> Self {
        Self {
            cancel: Arc::new(AtomicBool::new(false)),
            in_progress: Arc::new(AtomicBool::new(false)),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VpnInstallerDiskStatus {
    pub directory: String,
    pub phase: String,
    pub received_bytes: u64,
    pub total_bytes: Option<u64>,
    pub local_file_path: Option<String>,
    pub last_error: Option<String>,
    pub url: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
struct PersistedState {
    version: u32,
    url: String,
    #[serde(default)]
    etag: Option<String>,
    #[serde(default)]
    total_bytes: Option<u64>,
    #[serde(default)]
    received_bytes: u64,
    #[serde(default)]
    local_file_path: Option<String>,
    #[serde(default)]
    last_error: Option<String>,
    #[serde(default)]
    phase: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VpnInstallerDownloadArgs {
    pub url: String,
    #[serde(default)]
    pub sha256_hex: Option<String>,
    #[serde(default)]
    pub file_name: Option<String>,
    #[serde(default)]
    pub is_zip_archive: bool,
    #[serde(default)]
    pub zip_entry_name: Option<String>,
    /// Optional `Authorization: Bearer …` for authenticated CDN URLs.
    #[serde(default)]
    pub bearer_token: Option<String>,
}

#[derive(Debug, Serialize, Clone)]
#[serde(rename_all = "camelCase")]
pub struct VpnInstallerProgressPayload {
    pub phase: String,
    pub received_bytes: u64,
    pub total_bytes: Option<u64>,
    pub message: String,
}

fn vpn_installer_dir() -> Result<PathBuf, String> {
    let base = dirs::data_local_dir().ok_or_else(|| "Could not resolve local data directory".to_string())?;
    Ok(base.join(APP_IDENTIFIER).join("vpn-installer"))
}

fn sanitize_file_component(name: &str) -> String {
    name.chars()
        .map(|c| match c {
            '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*' => '_',
            c if c.is_control() => '_',
            c => c,
        })
        .collect::<String>()
        .trim()
        .to_string()
}

fn guess_from_url(url: &str) -> Option<String> {
    let path = url.split(['?', '#']).next()?;
    path.rsplit('/').next().filter(|s| !s.is_empty()).map(|s| s.to_string())
}

fn derived_base_name(args: &VpnInstallerDownloadArgs) -> Result<String, String> {
    if let Some(ref n) = args.file_name {
        let s = sanitize_file_component(n);
        if s.is_empty() {
            return Err("fileName is invalid".to_string());
        }
        return Ok(s);
    }
    guess_from_url(&args.url)
        .map(|s| sanitize_file_component(&s))
        .filter(|s| !s.is_empty())
        .ok_or_else(|| "Could not derive a file name; set fileName in server config".to_string())
}

fn state_path(dir: &Path) -> PathBuf {
    dir.join(STATE_FILE)
}

async fn read_persisted(dir: &Path) -> Option<PersistedState> {
    let p = state_path(dir);
    let s = tfs::read_to_string(&p).await.ok()?;
    serde_json::from_str(&s).ok()
}

async fn write_persisted(dir: &Path, state: &PersistedState) -> Result<(), String> {
    let p = state_path(dir);
    if let Some(parent) = p.parent() {
        tfs::create_dir_all(parent)
            .await
            .map_err(|e| format!("create_dir_all: {}", e))?;
    }
    let json = serde_json::to_string_pretty(state).map_err(|e| e.to_string())?;
    tfs::write(&p, json).await.map_err(|e| e.to_string())
}

#[tauri::command]
pub async fn vpn_installer_disk_status() -> Result<VpnInstallerDiskStatus, String> {
    let dir = vpn_installer_dir()?;
    let disk = read_persisted(&dir).await;
    let mut received = 0u64;
    let mut total = None;
    let mut phase = "idle".to_string();
    let mut local = None;
    let mut err = None;
    let mut url = None;

    if let Some(st) = disk {
        url = Some(st.url.clone());
        total = st.total_bytes;
        received = st.received_bytes;
        phase = st.phase.clone();
        local = st.local_file_path.clone();
        err = st.last_error.clone();
        if let Some(ref lp) = st.local_file_path {
            if Path::new(lp).is_file() {
                if let Ok(m) = std::fs::metadata(lp) {
                    received = m.len();
                }
            }
        }
    }

    Ok(VpnInstallerDiskStatus {
        directory: dir.to_string_lossy().into_owned(),
        phase,
        received_bytes: received,
        total_bytes: total,
        local_file_path: local,
        last_error: err,
        url,
    })
}

#[tauri::command]
pub fn vpn_installer_cancel(control: tauri::State<'_, Arc<VpnDownloadControl>>) -> Result<(), String> {
    control.cancel.store(true, Ordering::SeqCst);
    Ok(())
}

#[tauri::command]
pub async fn vpn_installer_download(
    app: AppHandle,
    control: tauri::State<'_, Arc<VpnDownloadControl>>,
    args: VpnInstallerDownloadArgs,
) -> Result<String, String> {
    if args.url.trim().is_empty() {
        return Err("Download URL is empty.".to_string());
    }

    if !control
        .in_progress
        .compare_exchange(false, true, Ordering::AcqRel, Ordering::Acquire)
        .is_ok()
    {
        return Err("A VPN installer download is already in progress.".to_string());
    }

    control.cancel.store(false, Ordering::SeqCst);

    let dir = vpn_installer_dir()?;
    tfs::create_dir_all(&dir)
        .await
        .map_err(|e| format!("Failed to create VPN installer directory: {}", e))?;

    let base_name = derived_base_name(&args)?;
    let part_path = dir.join(format!("{}{}", base_name, PART_SUFFIX));

    let cancel = Arc::clone(&control.cancel);
    let result = run_download_inner(app.clone(), &dir, &part_path, &base_name, args, cancel.clone()).await;

    control.in_progress.store(false, Ordering::Release);
    control.cancel.store(false, Ordering::SeqCst);

    result
}

async fn emit(app: &AppHandle, p: VpnInstallerProgressPayload) {
    let _ = app.emit("vpn-installer-progress", &p);
}

async fn run_download_inner(
    app: AppHandle,
    dir: &Path,
    part_path: &Path,
    base_name: &str,
    args: VpnInstallerDownloadArgs,
    cancel: Arc<AtomicBool>,
) -> Result<String, String> {
    emit(
        &app,
        VpnInstallerProgressPayload {
            phase: "starting".into(),
            received_bytes: 0,
            total_bytes: None,
            message: "Preparing download…".into(),
        },
    )
    .await;

    let mut persisted = read_persisted(dir).await.unwrap_or(PersistedState {
        version: 1,
        url: String::new(),
        etag: None,
        total_bytes: None,
        received_bytes: 0,
        local_file_path: None,
        last_error: None,
        phase: "idle".into(),
    });

    if persisted.url != args.url {
        persisted = PersistedState {
            version: 1,
            url: args.url.clone(),
            etag: None,
            total_bytes: None,
            received_bytes: 0,
            local_file_path: None,
            last_error: None,
            phase: "downloading".into(),
        };
        if part_path.exists() {
            tfs::remove_file(part_path).await.ok();
        }
    } else {
        persisted.phase = "downloading".into();
        persisted.last_error = None;
    }

    let resume_from = if part_path.exists() {
        tfs::metadata(part_path)
            .await
            .map(|m| m.len())
            .unwrap_or(0)
    } else {
        0
    };

    persisted.received_bytes = resume_from;
    write_persisted(dir, &persisted).await?;

    let client = reqwest::Client::builder()
        .timeout(std::time::Duration::from_secs(60 * 45))
        .connect_timeout(std::time::Duration::from_secs(45))
        .redirect(reqwest::redirect::Policy::limited(16))
        .build()
        .map_err(|e| format!("HTTP client: {}", e))?;

    let mut attempt = 0u32;
    let mut offset = resume_from;

    loop {
        if cancel.load(Ordering::SeqCst) {
            persisted.phase = "cancelled".into();
            persisted.last_error = Some("Cancelled by user.".into());
            write_persisted(dir, &persisted).await.ok();
            emit(
                &app,
                VpnInstallerProgressPayload {
                    phase: "cancelled".into(),
                    received_bytes: offset,
                    total_bytes: persisted.total_bytes,
                    message: "Cancelled.".into(),
                },
            )
            .await;
            return Err("Download cancelled.".to_string());
        }

        attempt += 1;
        if attempt > 64 {
            return Err("Too many retries while resuming download.".to_string());
        }

        let mut req = client
            .get(&args.url)
            .header("Accept-Encoding", "identity")
            .header("User-Agent", "TyresolesDesktop/1.0");

        if let Some(ref t) = args.bearer_token {
            if !t.is_empty() {
                req = req.header("Authorization", format!("Bearer {}", t));
            }
        }

        if offset > 0 {
            req = req.header("Range", format!("bytes={}-", offset));
        }

        let response = req.send().await.map_err(|e| format!("HTTP request failed: {}", e))?;
        let status = response.status();

        if status == reqwest::StatusCode::RANGE_NOT_SATISFIABLE {
            tfs::remove_file(part_path).await.ok();
            offset = 0;
            persisted.received_bytes = 0;
            persisted.total_bytes = None;
            write_persisted(dir, &persisted).await.ok();
            continue;
        }

        if status == reqwest::StatusCode::OK && offset > 0 {
            tfs::remove_file(part_path).await.ok();
            offset = 0;
            persisted.received_bytes = 0;
            persisted.total_bytes = None;
            write_persisted(dir, &persisted).await.ok();
            continue;
        }

        if !status.is_success() {
            let msg = format!("HTTP {}: {}", status, response.text().await.unwrap_or_default());
            persisted.last_error = Some(msg.clone());
            persisted.phase = "error".into();
            write_persisted(dir, &persisted).await.ok();
            emit(
                &app,
                VpnInstallerProgressPayload {
                    phase: "error".into(),
                    received_bytes: offset,
                    total_bytes: persisted.total_bytes,
                    message: msg.clone(),
                },
            )
            .await;
            return Err(msg);
        }

        let total_hint = if status == reqwest::StatusCode::PARTIAL_CONTENT {
            parse_content_range_total(response.headers().get(reqwest::header::CONTENT_RANGE))
                .or_else(|| content_length(response.headers()))
        } else {
            content_length(response.headers())
        };

        if let Some(t) = total_hint {
            persisted.total_bytes = Some(t);
        }

        let mut file = if offset == 0 {
            OpenOptions::new()
                .write(true)
                .create(true)
                .truncate(true)
                .open(part_path)
                .await
                .map_err(|e| format!("Failed to open partial file: {}", e))?
        } else {
            OpenOptions::new()
                .write(true)
                .append(true)
                .open(part_path)
                .await
                .map_err(|e| format!("Failed to append partial file: {}", e))?
        };

        let mut stream = response.bytes_stream();
        let mut last_emit = Instant::now();
        let mut since_emit: u64 = 0;

        while let Some(chunk) = stream.next().await {
            if cancel.load(Ordering::SeqCst) {
                drop(file);
                persisted.phase = "cancelled".into();
                persisted.last_error = Some("Cancelled by user.".into());
                persisted.received_bytes = offset;
                write_persisted(dir, &persisted).await.ok();
                emit(
                    &app,
                    VpnInstallerProgressPayload {
                        phase: "cancelled".into(),
                        received_bytes: offset,
                        total_bytes: persisted.total_bytes,
                        message: "Cancelled.".into(),
                    },
                )
                .await;
                return Err("Download cancelled.".to_string());
            }

            let chunk = chunk.map_err(|e| format!("Download stream error: {}", e))?;
            let n = chunk.len() as u64;
            file
                .write_all(&chunk)
                .await
                .map_err(|e| format!("Failed writing file: {}", e))?;
            offset += n;
            since_emit += n;
            persisted.received_bytes = offset;

            let should_emit = since_emit >= 512 * 1024 || last_emit.elapsed().as_secs() >= 1;
            if should_emit {
                since_emit = 0;
                last_emit = Instant::now();
                write_persisted(dir, &persisted).await.ok();
                emit(
                    &app,
                    VpnInstallerProgressPayload {
                        phase: "downloading".into(),
                        received_bytes: offset,
                        total_bytes: persisted.total_bytes,
                        message: "Downloading…".into(),
                    },
                )
                .await;
            }
        }

        file.flush().await.ok();
        drop(file);

        if let Some(expected) = persisted.total_bytes {
            if offset < expected {
                continue;
            }
        }

        break;
    }

    emit(
        &app,
        VpnInstallerProgressPayload {
            phase: "verifying".into(),
            received_bytes: offset,
            total_bytes: persisted.total_bytes,
            message: "Verifying…".into(),
        },
    )
    .await;

    let final_path = if args.is_zip_archive {
        let stem = Path::new(base_name)
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or(base_name);
        let zip_path = dir.join(format!("{}.zip", sanitize_file_component(stem)));
        tfs::rename(part_path, &zip_path)
            .await
            .map_err(|e| format!("Failed to finalize zip path: {}", e))?;

        emit(
            &app,
            VpnInstallerProgressPayload {
                phase: "extracting".into(),
                received_bytes: offset,
                total_bytes: persisted.total_bytes,
                message: "Extracting compressed archive (zip/deflate)…".into(),
            },
        )
        .await;

        let out = extract_zip_sync(&zip_path, dir, args.zip_entry_name.as_deref())?;
        tfs::remove_file(&zip_path).await.ok();
        out
    } else {
        let dest = dir.join(base_name);
        if dest != part_path {
            tfs::rename(part_path, &dest)
                .await
                .map_err(|e| format!("Failed to move installer into place: {}", e))?;
        }
        dest
    };

    if let Some(ref hex_expect) = args.sha256_hex {
        let expect = normalize_sha256_hex(hex_expect)?;
        emit(
            &app,
            VpnInstallerProgressPayload {
                phase: "verifying".into(),
                received_bytes: offset,
                total_bytes: persisted.total_bytes,
                message: "Computing SHA-256…".into(),
            },
        )
        .await;
        let got = sha256_file_sync(&final_path)?;
        if got != expect {
            let msg = "SHA-256 mismatch after download.".to_string();
            persisted.last_error = Some(msg.clone());
            persisted.phase = "error".into();
            write_persisted(dir, &persisted).await.ok();
            emit(
                &app,
                VpnInstallerProgressPayload {
                    phase: "error".into(),
                    received_bytes: offset,
                    total_bytes: persisted.total_bytes,
                    message: msg.clone(),
                },
            )
            .await;
            return Err(msg);
        }
    }

    let path_str = final_path.to_string_lossy().into_owned();
    persisted.local_file_path = Some(path_str.clone());
    persisted.phase = "complete".into();
    persisted.received_bytes = std::fs::metadata(&final_path).map(|m| m.len()).unwrap_or(offset);
    persisted.last_error = None;
    write_persisted(dir, &persisted).await?;

    emit(
        &app,
        VpnInstallerProgressPayload {
            phase: "complete".into(),
            received_bytes: persisted.received_bytes,
            total_bytes: persisted.total_bytes,
            message: "Ready.".into(),
        },
    )
    .await;

    Ok(path_str)
}

fn content_length(headers: &reqwest::header::HeaderMap) -> Option<u64> {
    headers
        .get(reqwest::header::CONTENT_LENGTH)
        .and_then(|v| v.to_str().ok())
        .and_then(|s| s.parse::<u64>().ok())
}

fn parse_content_range_total(value: Option<&reqwest::header::HeaderValue>) -> Option<u64> {
    let v = value?.to_str().ok()?;
    // bytes 0-1023/2048
    let slash = v.rfind('/')?;
    v[slash + 1..].parse::<u64>().ok()
}

fn normalize_sha256_hex(s: &str) -> Result<String, String> {
    let hex: String = s.chars().filter(|c| !c.is_whitespace()).collect();
    if hex.len() != 64 || !hex.chars().all(|c| c.is_ascii_hexdigit()) {
        return Err("Invalid sha256Hex (expected 64 hex characters).".to_string());
    }
    Ok(hex.to_ascii_lowercase())
}

fn sha256_file_sync(path: &Path) -> Result<String, String> {
    use sha2::Sha256;
    use std::fs::File;
    use std::io::Read;

    let mut f = File::open(path).map_err(|e| e.to_string())?;
    let mut hasher = Sha256::new();
    let mut buf = [0u8; 256 * 1024];
    loop {
        let n = f.read(&mut buf).map_err(|e| e.to_string())?;
        if n == 0 {
            break;
        }
        hasher.update(&buf[..n]);
    }
    Ok(hex::encode(hasher.finalize()))
}

fn extract_zip_sync(zip_path: &Path, dest_dir: &Path, entry_hint: Option<&str>) -> Result<PathBuf, String> {
    use std::fs::File;
    use std::io::Write;
    use zip::ZipArchive;

    let file = File::open(zip_path).map_err(|e| format!("Open zip: {}", e))?;
    let mut archive = ZipArchive::new(file).map_err(|e| format!("Read zip: {}", e))?;

    let index = if let Some(hint) = entry_hint {
        let hint = hint.replace('\\', "/").trim().trim_start_matches('/').to_string();
        let mut found = None;
        for i in 0..archive.len() {
            let entry = archive.by_index(i).map_err(|e| e.to_string())?;
            let name = entry
                .enclosed_name()
                .map(|p| p.to_string_lossy().replace('\\', "/"))
                .unwrap_or_default();
            if name == hint || name.ends_with(&hint) {
                found = Some(i);
                break;
            }
        }
        found.ok_or_else(|| format!("Zip entry not found: {}", hint))?
    } else {
        let mut found = None;
        for i in 0..archive.len() {
            let entry = archive.by_index(i).map_err(|e| e.to_string())?;
            let n = entry.name().to_lowercase();
            if n.ends_with(".exe") || n.ends_with(".msi") {
                found = Some(i);
                break;
            }
        }
        found.ok_or_else(|| "No .exe or .msi entry found in zip.".to_string())?
    };

    let mut entry = archive.by_index(index).map_err(|e| e.to_string())?;
    let raw_name = entry
        .enclosed_name()
        .and_then(|p| p.file_name().map(|f| f.to_string_lossy().into_owned()))
        .filter(|s| !s.is_empty())
        .unwrap_or_else(|| "installer.exe".to_string());
    let safe = sanitize_file_component(&raw_name);
    let out_path = dest_dir.join(&safe);

    let mut out = File::create(&out_path).map_err(|e| format!("Create extracted file: {}", e))?;
    std::io::copy(&mut entry, &mut out).map_err(|e| format!("Extract: {}", e))?;
    out.flush().ok();

    Ok(out_path)
}

#[cfg(windows)]
#[tauri::command]
pub fn vpn_installer_open_folder() -> Result<(), String> {
    let dir = vpn_installer_dir()?;
    std::fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    std::process::Command::new("explorer.exe")
        .arg(&dir)
        .spawn()
        .map_err(|e| format!("explorer: {}", e))?;
    Ok(())
}

#[cfg(not(windows))]
#[tauri::command]
pub fn vpn_installer_open_folder() -> Result<(), String> {
    Err("Opening a folder is only supported on Windows.".to_string())
}
