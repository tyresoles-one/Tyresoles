use serde::{Deserialize, Serialize};
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use tauri::{AppHandle, Emitter};

pub struct SyncState {
    pub is_paused: AtomicBool,
}

impl Default for SyncState {
    fn default() -> Self {
        Self {
            is_paused: AtomicBool::new(false),
        }
    }
}

#[derive(Clone, Serialize, Deserialize)]
pub struct SyncProgress {
    pub total_bytes: u64,
    pub transferred_bytes: u64,
    pub current_file: String,
    pub percent: f64,
}

#[tauri::command]
pub async fn start_sync(app: AppHandle, state: tauri::State<'_, Arc<SyncState>>) -> Result<(), String> {
    log::info!("Starting background Drive Sync engine...");
    state.is_paused.store(false, Ordering::Relaxed);
    
    let state_clone = Arc::clone(&state);
    
    // Asynchronously spawn the engine so the UI thread is not locked! (Solves Pitfall 5)
    tauri::async_runtime::spawn(async move {
        // Here we would use tokio::process::Command to launch Rclone/Kopia sidecar.
        // We simulate the 4-times-per-second throttling to prevent UI white-screens.
        for i in 1..=10 {
            if state_clone.is_paused.load(Ordering::Relaxed) {
                log::info!("Sync gracefully paused.");
                break;
            }
            
            let progress = SyncProgress {
                total_bytes: 1_000_000,
                transferred_bytes: i * 100_000,
                current_file: format!("C:\\Users\\Mock\\Documents\\heavy_archive_{}.pst", i),
                percent: (i as f64) * 10.0,
            };
            
            let _ = app.emit("sync-progress", &progress);
            tokio::time::sleep(std::time::Duration::from_millis(250)).await;
        }
        log::info!("Sync complete or halted.");
    });
    
    Ok(())
}

#[tauri::command]
pub fn pause_sync(state: tauri::State<'_, Arc<SyncState>>) -> Result<(), String> {
    log::warn!("Sync pause requested by user.");
    state.is_paused.store(true, Ordering::Relaxed);
    Ok(())
}

#[tauri::command]
pub async fn fetch_remote_state() -> Result<String, String> {
    log::info!("Fetching remote directory state from Google Drive...");
    // Future: Call rclone lsjson to reconstruct the local DB
    Ok("[]".to_string())
}
