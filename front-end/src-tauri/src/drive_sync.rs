use serde::Serialize;
use std::process::Command;

#[derive(Debug, Serialize)]
pub struct RcloneExecResult {
    pub code: i32,
    pub success: bool,
    pub stdout: String,
    pub stderr: String,
}

#[tauri::command]
pub async fn run_rclone_copyto(
    binary_path: Option<String>,
    source_path: String,
    remote_path: String,
    drive_token_json: String,
) -> Result<RcloneExecResult, String> {
    let binary = binary_path
        .as_deref()
        .filter(|s| !s.trim().is_empty())
        .unwrap_or("rclone")
        .to_string();

    let output = tauri::async_runtime::spawn_blocking(move || {
        Command::new(binary)
            .arg("copyto")
            .arg(&source_path)
            .arg(&remote_path)
            .arg("--drive-token")
            .arg(&drive_token_json)
            .arg("--retries")
            .arg("2")
            .arg("--low-level-retries")
            .arg("2")
            .arg("--checkers")
            .arg("4")
            .arg("--transfers")
            .arg("2")
            .output()
    })
    .await
    .map_err(|e| format!("failed to join rclone task: {e}"))?
    .map_err(|e| format!("failed to execute rclone: {e}"))?;

    let code = output.status.code().unwrap_or(-1);
    Ok(RcloneExecResult {
        code,
        success: output.status.success(),
        stdout: String::from_utf8_lossy(&output.stdout).to_string(),
        stderr: String::from_utf8_lossy(&output.stderr).to_string(),
    })
}
